namespace FaraAvatarModdingGuide.Practice;

/// <summary>お題の判定に渡す、現在の状態(practice.js の checkTasks が作る ctx と同じ中身)。</summary>
public sealed record TaskContext(
    IReadOnlyList<SceneObject> Objects,
    IReadOnlyList<SceneObject> Selected,
    string ViewName,
    bool Ortho,
    string Mode,
    string SelectMode,
    /// <summary>編集モードで選択している面の法線(オブジェクトモードでは空)。</summary>
    IReadOnlyList<Vec3> FaceNormals,
    TaskFlags Flags);

/// <summary>お題の判定に使う「起きたことの記録」(イベントから立てる。リセットで戻す)。</summary>
public sealed record TaskFlags
{
    public bool UndoUsed { get; init; }
    public bool SphereAdded { get; init; }
    public bool Extruded { get; init; }
    public bool Inset { get; init; }
    public bool Loopcut { get; init; }
    public bool Bevel { get; init; }
}

/// <summary>お題 1 件。</summary>
public sealed record PracticeTask(string Text, Func<TaskContext, bool> Check);

/// <summary>練習 1(/practice)のお題(practice.js の TASKS と同じ文言・同じ判定)と、達成の進め方(checkTasks)。</summary>
public static class PracticeTasks
{
    private static bool Cube(SceneObject o) => o.Kind == "cube";
    private static bool Sphere(SceneObject o) => o.Kind == "sphere";

    /// <summary>|a - b| が eps 以内か(viewport.js の near)。</summary>
    public static bool Near(double a, double b, double eps = 0.05) => Math.Abs(a - b) <= eps;

    /// <summary>角度(ラジアン)が deg 度に近いか。360 度の周りを考える(viewport.js の nearAngle)。</summary>
    public static bool NearAngle(double rad, double deg, double eps = 1)
    {
        var d = ((rad / Js.Deg - deg) % 360 + 540) % 360 - 180;
        return Math.Abs(d) <= eps;
    }

    public static readonly IReadOnlyList<PracticeTask> Tasks =
    [
        new("立方体をクリックして選択する", s => s.Selected.Any(Cube)),
        new("G → Z → 2 → Enter で、立方体を上に 2 m 動かす", s => s.Objects.Any(o => Cube(o) && Near(o.Position.Z, 2))),
        new("R → Z → 45 → Enter で、立方体を 45° 回転する", s => s.Objects.Any(o => Cube(o) && NearAngle(o.Rotation.Z, 45))),
        new("S → 2 → Enter で、立方体を 2 倍にする", s => s.Objects.Any(o => Cube(o) && Near(o.Scale.X, 2) && Near(o.Scale.Z, 2))),
        new("Shift+A で UV 球を追加する", s => s.Objects.Any(Sphere)),
        new("球を選んで G → X → 3 → Enter で、X 方向に 3 m 動かす", s => s.Objects.Any(o => Sphere(o) && Near(o.Position.X, 3))),
        new("立方体を選んで Shift+D で複製し、Y 方向に 3 m 離す", s => s.Objects.Count(Cube) >= 2 && s.Objects.Any(o => Cube(o) && Near(o.Position.Y, 3))),
        new("Numpad 1 で正面から見る（テンキーがなければ「テンキーを模倣」をオンにして 1）", s => s.ViewName == "前"),
        new("Numpad 5 で平行投影に切り替える", s => s.Ortho),
        new("Ctrl+Z で直前の操作を元に戻す", s => s.Flags.UndoUsed),
        new("球を選んで X で削除する", s => s.Flags.SphereAdded && !s.Objects.Any(Sphere)),
        // ここから編集モード
        new("立方体を選んで Tab で編集モードに入る", s => s.Mode == "edit"),
        new("3 で面選択モードにして、上の面をクリックで選ぶ（「テンキーを模倣」がオンなら、オフにするか上の「面」ボタンで切り替え）", s => s.Mode == "edit" && s.SelectMode == "face" && s.FaceNormals.Count == 1 && s.FaceNormals[0].Z > 0.9),
        new("E → 1 → Enter で、上の面を押し出す", s => s.Flags.Extruded),
        new("I → 0.3 → Enter で、面を差し込む", s => s.Flags.Inset),
        new("Ctrl+R で側面にループカットを入れる（ホイールで本数）", s => s.Flags.Loopcut),
        new("2 で辺選択モードにして辺を 1 本選び、Ctrl+B でベベル（「テンキーを模倣」がオンなら上の「辺」ボタンで切り替え）", s => s.Flags.Bevel),
        new("Tab でオブジェクトモードに戻る", s => s.Mode == "object"),
    ];
}

/// <summary>お題の進み具合(practice.js の taskIndex / taskDone と checkTasks)。前のお題が済むまで、先のお題は進まない。</summary>
public sealed class TaskProgress
{
    private readonly bool[] done = new bool[PracticeTasks.Tasks.Count];

    /// <summary>いま取り組むお題の番号(全部済んだら Tasks.Count)。</summary>
    public int Index { get; private set; }

    public IReadOnlyList<bool> Done => done;

    public bool AllDone => Index >= PracticeTasks.Tasks.Count;

    public void Reset()
    {
        Index = 0;
        Array.Clear(done);
    }

    /// <summary>状態を見て、達成したお題を順に進める。進んだら true(一覧を描き直す合図)。</summary>
    public bool Check(TaskContext context)
    {
        var changed = false;
        while (Index < PracticeTasks.Tasks.Count && PracticeTasks.Tasks[Index].Check(context))
        {
            done[Index] = true;
            Index++;
            changed = true;
        }
        return changed;
    }
}

/// <summary>
/// 練習 1(/practice)の進行(practice.js の Practice クラスから、DOM を除いたもの)。ビューのイベントから「起きたこと」の記録を立て、そのたびに状態を見てお題を進める。
/// 現行は on("*") のハンドラーでイベントが出た瞬間に判定する(出た時点の状態を見る)ので、C# も同じにする。
/// </summary>
public sealed class PracticeSession
{
    public PracticeSession(ViewportCore core)
    {
        Core = core;
        core.EventEmitted += OnEvent;
        Reset();
    }

    public ViewportCore Core { get; }
    public TaskFlags Flags { get; private set; } = new();
    public TaskProgress Progress { get; } = new();

    private void OnEvent(ViewEvent e)
    {
        var d = e.Detail;
        if (e.Type == "add" && d.TryGetValue("kind", out var k) && (k as string) == "sphere") Flags = Flags with { SphereAdded = true };
        if (e.Type == "undo") Flags = Flags with { UndoUsed = true };
        if (e.Type == "confirm" && d.TryGetValue("extrude", out var x) && x is true) Flags = Flags with { Extruded = true };
        if (e.Type == "confirm" && d.TryGetValue("kind", out var ck) && (ck as string) == "I") Flags = Flags with { Inset = true };
        if (e.Type == "loopcut") Flags = Flags with { Loopcut = true };
        if (e.Type == "confirm" && d.TryGetValue("kind", out var bk) && (bk as string) == "B") Flags = Flags with { Bevel = true };
        CheckTasks();
    }

    /// <summary>状態を見て、達成したお題を順に進める。進んだら true(一覧を描き直す合図)。</summary>
    public bool CheckTasks() => Progress.Check(Core.GetTaskContext(Flags));

    /// <summary>最初に戻す(シーン・記録・お題)。</summary>
    public void Reset()
    {
        Core.ResetScene();
        Flags = new TaskFlags();
        Progress.Reset();
    }
}
