namespace FaraAvatarModdingGuide.Practice;

/// <summary>お題 1 つ(unity-practice.js の TASKS の 1 件)。Check は、いまの状態(と、SDK を開いた・Build &amp; Test で衣装がついてきた記録)で達成か。</summary>
public sealed record UnityTask(string Text, Func<UnityEditorCore, UnityPracticeState, bool> Check);

/// <summary>お題の判定に使う、操作の記録(unity-practice.js の state)。</summary>
public sealed class UnityPracticeState
{
    public bool Sdk { get; set; }
    public bool BuiltMerged { get; set; }
}

/// <summary>
/// /practice/unity のお題の進行(unity-practice.js の UnityPractice から、画面(DOM)を除いたもの)。エディターのイベントのたびに、いまのお題が達成かを調べ、
/// 達成なら次へ進む(続けて達成なら、そのまま進む)。
/// </summary>
public sealed class UnityPracticeSession
{
    public static readonly IReadOnlyList<UnityTask> Tasks =
    [
        new("Project の Hoodie を、Hierarchy の Avatar の上にドラッグして入れる", (ed, _) => ed.FindByName("Avatar/Hoodie") is not null),
        new("Hoodie を右クリック > Modular Avatar ▸ Setup Outfit", (ed, _) => ed.FindByName("Avatar/Hoodie/Armature")?.Components.Any(c => c.Type == "MAMergeArmature") == true),
        new("Shirt_Default を無効にして、Tag を EditorOnly にする", (ed, _) => { var n = ed.FindByName("Avatar/Shirt_Default"); return n is null || (!n.Active && n.Tag == "EditorOnly"); }),
        new("Hoodie_Mesh の Materials に Hoodie_lilToon（または Hoodie_Red）を設定する", (ed, _) =>
        {
            var m = ed.FindByName("Hoodie/Hoodie_Mesh")?.Smr?.Materials is { Count: > 0 } list ? list[0] : "";
            return m.Contains("lilToon", StringComparison.Ordinal) || m.Contains("Red", StringComparison.Ordinal);
        }),
        new("Hoodie_Mesh に Add Component で MA Blendshape Sync を追加する", (ed, _) => ed.FindByName("Hoodie/Hoodie_Mesh")?.Components.Any(c => c.Type == "MABlendshapeSync") == true),
        new("VRChat SDK > Show Control Panel でランクを確認する", (_, s) => s.Sdk),
        new("Build & Test で、衣装がついてくることを確かめる", (_, s) => s.BuiltMerged),
    ];

    private readonly UnityEditorCore core;

    public UnityPracticeSession(UnityEditorCore core)
    {
        this.core = core;
        core.EventEmitted += OnEvent;
        Reset();
    }

    public UnityPracticeState State { get; private set; } = new();

    /// <summary>いまのお題の番号(全部できたら <c>Tasks.Count</c>)。</summary>
    public int Index { get; private set; }

    public bool AllDone => Index >= Tasks.Count;

    public event Action? Changed;

    private void OnEvent(ViewEvent e)
    {
        if (e.Type == "sdk" && e.Flag("open")) State.Sdk = true;
        if (e.Type == "build" && e.StrIs("kind", "test") && e.Flag("merged")) State.BuiltMerged = true;
        Check();
    }

    /// <summary>最初からやり直す(シーンもお題も)。</summary>
    public void Reset()
    {
        core.ResetScene();
        State = new UnityPracticeState();
        Index = 0;
        Changed?.Invoke();
    }

    private void Check()
    {
        var changed = false;
        while (Index < Tasks.Count && Tasks[Index].Check(core, State)) { Index++; changed = true; }
        if (changed) Changed?.Invoke();
    }
}
