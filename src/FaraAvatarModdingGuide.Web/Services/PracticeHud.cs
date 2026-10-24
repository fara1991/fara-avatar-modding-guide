namespace FaraAvatarModdingGuide.Web.Services;

// /practice の 3D の C# 版(?engine=cs)で、画面(Razor)が描く HUD・お題の一覧の状態。
// 画面(Practice.razor)は、操作のロジック(FaraAvatarModdingGuide.Practice。起動のときには読み込まず、?engine=cs のときだけ遅れて読む)の型を直接は使わず、
// ここの単純な型だけを読む(読み込みの前に、画面の描画の関数が Practice の型を必要としないように)。

public sealed record HudTool(string Key, bool On, bool Disabled);

public sealed record HudNPanelRow(string Prop, string Axis, string Value, bool Disabled);

public sealed record HudMenu(string Title, IReadOnlyList<string> Items, double X, double Y);

public sealed record HudBox(double X, double Y, double Width, double Height);

/// <summary>画面に出す状態(viewport.js の HUD・N パネル・メニュー・ボックス選択の枠と、お題の一覧)。</summary>
public sealed class PracticeHud
{
    public string ModeLabel { get; init; } = "オブジェクトモード";
    public string ViewLabel { get; init; } = "ユーザー・透視投影";
    public bool StatsHidden { get; init; } = true;
    public IReadOnlyList<string> StatsLines { get; init; } = [];
    public bool ToolsEditHidden { get; init; } = true;
    public IReadOnlyList<HudTool> Tools { get; init; } = [];
    public bool NPanelHidden { get; init; } = true;
    public string NPanelName { get; init; } = "—";
    public IReadOnlyList<HudNPanelRow> NPanelRows { get; init; } = [];
    public HudMenu? Menu { get; init; }
    public HudBox? Box { get; init; }
    public string Status { get; init; } = "";
    public string Numeric { get; init; } = "";
    /// <summary>案内(チュートリアル): 左上の視点の表示を強調するか・強調するボタン・矢印(非表示か、ラベル、ビューポート内のピクセル位置)。</summary>
    public bool ViewHighlight { get; init; }
    public string? HighlightTool { get; init; }
    public bool MarkerHidden { get; init; } = true;
    public string MarkerLabel { get; init; } = "";
    public double MarkerX { get; init; }
    public double MarkerY { get; init; }
    public IReadOnlyList<string> TaskTexts { get; init; } = [];
    public IReadOnlyList<bool> TaskDone { get; init; } = [];
    public int TaskIndex { get; init; }
}

/// <summary>/practice の 3D の C# 版の窓口(画面が使う分だけ。実装は <see cref="PracticeCsEngine"/>)。</summary>
public interface IPracticeCs : IDisposable
{
    PracticeHud Hud { get; }
    event Action? Changed;
    bool Start();
    void Reset();
    void ClickTool(string tool);
    void ChooseMenu(int index);
    void ChangeNPanel(string prop, string axis, string value);
}

/// <summary>記事内チュートリアルの手順 1 つ(画像で見る・手順の一覧用)。</summary>
public sealed record TutorialStepInfo(string Text, IReadOnlyList<string> Keys, string? Mouse);

/// <summary>記事内チュートリアル 1 本の、画面に要る情報(題・手順・テンキーの有無)。</summary>
public sealed record TutorialInfo(string Name, string Title, bool Numpad, IReadOnlyList<TutorialStepInfo> Steps);

/// <summary>画面に出す、いまの手順・メッセージ・完了の状態。</summary>
public sealed class TutorialUiState
{
    public int Index { get; init; }
    public int Count { get; init; }
    public bool Finished { get; init; }
    public string StepText { get; init; } = "";
    public IReadOnlyList<string> Keys { get; init; } = [];
    public string? Mouse { get; init; }
    /// <summary>メッセージ(HTML)と種類(hint / warn / info / ok)。無ければ null。</summary>
    public string? Message { get; init; }
    public string MessageKind { get; init; } = "";
    /// <summary>「できました」の合図の回数(増えるたびに、チェックマークと吹き出しのアニメーションをやり直す)。</summary>
    public int OkFlash { get; init; }
    /// <summary>光らせているキー(画面下のキー表示の label)。</summary>
    public IReadOnlyList<string> PressedKeys { get; init; } = [];
    public string CompleteText { get; init; } = "";
    /// <summary>3D ビュー(WebGL)を持っているか・読み込み中か・一度は始めたか。</summary>
    public bool HasView { get; init; }
    public bool Loading { get; init; }
    public bool Started { get; init; }
    public bool ViewFailed { get; init; }
}

/// <summary>記事内チュートリアル 1 本の窓口(画面が使う分。実装は <see cref="TutorialController"/>)。</summary>
public interface ITutorialCs : IDisposable
{
    TutorialInfo Info { get; }
    TutorialUiState State { get; }
    PracticeHud Hud { get; }
    event Action? Changed;
    /// <summary>3D ビューを始める(canvas は #<c>canvasHostId</c>、操作を受ける要素は #<c>rootId</c>)。ほかのチュートリアルのビューは休止する。</summary>
    Task ActivateAsync();
    /// <summary>3D ビューを手放す(進み具合は残す)。</summary>
    void Suspend();
    void Retry();
    void Restart();
    void Skip();
    void ClickTool(string tool);
    void ChooseMenu(int index);
    void ChangeNPanel(string prop, string axis, string value);
    bool Numpad { get; set; }
}

/// <summary>画面が C# 版を作る入口。ここから先(<see cref="PracticeCsEngine"/>)が Practice・Render の型を使うので、読み込みのあとに呼ぶ。</summary>
public static class PracticeCsLoader
{
    public static async Task<IPracticeCs> CreateAsync(Func<bool> emulateNumpad, bool debug)
    {
        await PracticeCsEngine.ImportAsync();
        await PracticeCsEngine.ImportHelperAsync();
        return new PracticeCsEngine("practice-viewport", "practice-gl", emulateNumpad, debug);
    }
}

/// <summary>記事内チュートリアルの C# 版の入口(Practice・Render を遅れて読み込んだあとに呼ぶ)。</summary>
public static class TutorialCsLoader
{
    /// <summary>name の C# 版のチュートリアル(無ければ null。Unity のものなど)。rootId / canvasHostId は、ビューを置く要素の id。</summary>
    public static async Task<ITutorialCs?> CreateAsync(string name, string rootId, string canvasHostId, string imageBase)
    {
        await PracticeCsEngine.ImportAsync();
        await PracticeCsEngine.ImportHelperAsync();
        return TutorialController.Create(name, rootId, canvasHostId, imageBase);
    }
}

/// <summary>Practice・Render(?engine=cs のときだけ遅れて読み込むアセンブリ)の読み込み。ページごとではなく、1 回だけ・全員で共有する(同じ wasm を二重に読むと、本文の読み込みが衝突する)。</summary>
public static class PracticeCsAssemblies
{
    private static Task? loading;

    public static async Task LoadAsync(IServiceProvider services)
    {
        loading ??= services.GetRequiredService<Microsoft.AspNetCore.Components.WebAssembly.Services.LazyAssemblyLoader>()
            .LoadAssembliesAsync(["FaraAvatarModdingGuide.Practice.wasm", "FaraAvatarModdingGuide.Render.wasm"]);
        try { await loading; }
        catch { loading = null; throw; }   // 失敗したら、次に開いたときにやり直せるように
    }
}
