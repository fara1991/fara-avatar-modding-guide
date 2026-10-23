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
