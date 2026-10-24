namespace FaraAvatarModdingGuide.Web.Services;

// Unity 風の画面の C# 版(?engine=cs。Phase 3b S5)で、画面(Razor)が描く状態。
// 画面(UnityEditorView.razor・PracticeUnity.razor・TutorialEmbed.razor)は、操作のロジック(FaraAvatarModdingGuide.Practice。?engine=cs のときだけ遅れて読み込む)の型を直接は使わず、
// ここの単純な型だけを読む(読み込みの前に、画面の描画の関数が Practice の型を必要としないように。PracticeHud.cs と同じ決まり)。

public sealed record UnityHudRow(string Id, string Name, int Depth, bool HasKids, bool Expanded, bool Selected, bool Inactive, bool Prefab, string Icon, bool EditorOnly);

public sealed record UnityHudField(string Kind, string Label, string Text, string? Icon, string? Color, int Slot, string? Hl);

public sealed record UnityHudComp(string Type, string Label, IReadOnlyList<UnityHudField> Fields);

public sealed record UnityHudInspector(string NodeId, string Name, bool Active, string Tag, IReadOnlyList<string> P, IReadOnlyList<string> R, IReadOnlyList<string> S, IReadOnlyList<UnityHudComp> Components);

public sealed record UnityHudAsset(string Key, string Label, string Type, string? Color, string Title);

public sealed record UnityHudSdkRow(string Label, string Value, int Rank, string RankName);

public sealed record UnityHudSdk(bool HasAvatar, string AvatarName, IReadOnlyList<(string Level, string Text)> Notes, int Overall, string OverallName, IReadOnlyList<UnityHudSdkRow> Rows);

public sealed record UnityHudAddItem(string Type, string Label, string Path, bool Highlight);

/// <summary>開いているポップアップ。Kind: "context"(NodeName)・"add"(Search・Items)・"menu"(MenuName)。Anchor は、出す位置の決め方(<c>UnityAnchor</c> と同じ)。</summary>
public sealed record UnityHudPopup(string Kind, string Key, string AnchorKind, double X, double Y, string? Selector, string? NodeName, string? MenuName, string Search, IReadOnlyList<UnityHudAddItem> Items);

public sealed record UnityHudPreview(string Text, bool Busy);

/// <summary>案内中の場所。Kind: "hierarchy"(Key = 行の ID)・"project"・"inspector"・"menu"・"sdk"・"tool"・"popup"・"scene"。見つからない・隠れているときは null。</summary>
public sealed record UnityHudHighlight(string Kind, string Key, string? Label);

/// <summary>画面に出す状態(unity.js の画面のうち、Hierarchy・Inspector・Project・Scene の文字・ツールバー・メニュー・SDK・ポップアップ・案内)。</summary>
public sealed class UnityHud
{
    public string Tool { get; init; } = "move";
    public string Status { get; init; } = "";
    public string Info { get; init; } = "";
    public IReadOnlyList<UnityHudRow> Rows { get; init; } = [];
    public UnityHudInspector? Inspector { get; init; }
    public IReadOnlyList<UnityHudAsset> Assets { get; init; } = [];
    public UnityHudPreview? Preview { get; init; }
    public bool SdkOpen { get; init; }
    public UnityHudSdk? Sdk { get; init; }
    public UnityHudPopup? Popup { get; init; }
    public UnityHudHighlight? Highlight { get; init; }
    /// <summary>/practice/unity のお題の一覧(ページにあるものだけ)。</summary>
    public IReadOnlyList<string> TaskTexts { get; init; } = [];
    public int TaskIndex { get; init; }
}

/// <summary>Unity 風の画面の C# 版の窓口(画面が使う分だけ。実装は <see cref="UnityCsEngine"/>)。</summary>
public interface IUnityCs : IDisposable
{
    UnityHud Hud { get; }
    event Action? Changed;
    /// <summary>WebGL を始める(Scene ビューの要素が DOM にできたあとに呼ぶ)。WebGL 2 が使えなければ false。</summary>
    bool StartGl();
    bool GlStarted { get; }
    void ClickTool(string tool);
    void OpenMenu(string name, string selector);
    void MenuItem(string item);
    void ContextItem(string item);
    void AddItem(string type);
    void OpenAddComponent();
    void CloseSdk();
    void BuildSdk(string kind);
    void ClickBackground();
    void SetActive(bool on);
    void SetTag(string tag);
    void SetTransform(string prop, int axis, string text);
    void SetAddSearch(string text);
    void AddSearchKey(string key);
    void ToggleFold(string id);
    void SelectRow(string id);
    void ContextAt(string id, double x, double y);
    void DoubleClickRow(string id);
    void ClearSelection();
    void ClickAsset(string asset);
    void DropNode(string id, string targetKind, string? targetId);
    void DropAsset(string asset, string targetKind, string? targetId, int slot, double x, double y);
    void SetFocused(bool on);
    void Reset();
    /// <summary>外から状態を変えたあと(チュートリアルの再生・案内)に、画面に出す状態を作り直す。</summary>
    void Refresh();
    /// <summary>/practice/unity のお題の進行を付ける。</summary>
    void AttachPractice();
}

/// <summary>画面が Unity 風の画面の C# 版を作る入口。ここから先(<see cref="UnityCsEngine"/>)が Practice・Render の型を使うので、読み込みのあとに呼ぶ。</summary>
public static class UnityCsLoader
{
    public static async Task<IUnityCs> CreateAsync(string rootId, string sceneViewId, string glHostId, bool wheelNeedsFocus, string[] instances, bool debug)
    {
        await CsViewEngine.ImportAsync();
        await CsViewEngine.ImportHelperAsync();
        await UnityCsEngine.ImportUiAsync();
        return new UnityCsEngine(rootId, sceneViewId, glHostId, wheelNeedsFocus, instances, debug);
    }
}
