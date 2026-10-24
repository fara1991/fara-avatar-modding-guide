using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Render;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// /practice・記事内チュートリアルの 3D の C# 版が共通で持つもの: 操作のロジック(<see cref="ViewportCore"/>)・描画の命令(<see cref="SceneRenderer"/>)・gl.js とのやり取り・HUD の状態。
/// 状態が変わったら <see cref="Changed"/> を出す(画面は描き直す)。
/// <b>契約</b>: <see cref="SceneRenderer"/> は <see cref="ViewportCore"/> を作った直後(操作の前)に作る(編集モードの描画データの通知を取りこぼさないため)。
/// </summary>
public abstract class CsViewEngine : IDisposable
{
    private static Task? importing;
    private static Task? importingHelper;

    protected readonly string RootId;
    protected readonly string CanvasHostId;
    protected readonly Func<bool> EmulateNumpadFn;
    protected readonly bool Debug;
    private ViewportCore? core;
    private SceneRenderer? renderer;
    private float[] packed = new float[FramePacker.HeaderFloats + FramePacker.ItemFloats * 32];
    private ViewRect rect = new(0, 0, 0, 0);
    private (double, double) lastClient;
    private string lastSignature = "";
    private bool started;
    private bool disposed;

    protected CsViewEngine(string rootId, string canvasHostId, Func<bool> emulateNumpad, bool debug)
    {
        RootId = rootId;
        CanvasHostId = canvasHostId;
        EmulateNumpadFn = emulateNumpad;
        Debug = debug;
    }

    public ViewportCore Core => core ?? throw new InvalidOperationException("始めていません");
    public PracticeHud Hud { get; protected set; } = new();
    public bool IsDisposed => disposed;

    /// <summary>画面に出す状態が変わった(HUD・お題の一覧などを描き直す)。</summary>
    public event Action? Changed;

    /// <summary>gl.js を読み込む(ページごとではなく、1 回だけ)。</summary>
    public static Task ImportAsync() => importing ??= JSHost.ImportAsync("gl", "../js/gl.js");

    /// <summary>practice-cs.js を読み込む(1 回だけ)。</summary>
    public static Task ImportHelperAsync() => importingHelper ??= JSHost.ImportAsync("practice-cs", "../js/practice-cs.js");

    /// <summary>ビューのオプション(最初のシーン・視点・ホイールの扱いなど)。</summary>
    protected abstract ViewportOptions CreateOptions();

    /// <summary>ビューを作った直後(操作の前。<see cref="SceneRenderer"/> を作ったあと)。</summary>
    protected virtual void OnCoreCreated(ViewportCore core) { }

    /// <summary>画面の状態の署名に足すもの(お題の進みなど)。</summary>
    protected virtual string ExtraSignature() => "";

    /// <summary>HUD に足す、お題の一覧(練習 1 のみ)。</summary>
    protected virtual (IReadOnlyList<string> Texts, IReadOnlyList<bool> Done, int Index) Tasks() => ([], [], 0);

    /// <summary>始める。WebGL 2 が使えなければ false。</summary>
    public bool Start()
    {
        core = new ViewportCore(new ViewRect(0, 0, 960, 540), CreateOptions());
        renderer = new SceneRenderer(core);   // 操作の前に作る(契約)
        OnCoreCreated(core);
        started = PracticeGl.Start(RootId, OnFrame, OnPointerDown, OnPointerMove, OnPointerUp, OnWheel, OnKey, OnKeyUp, OnFocus, CanvasHostId, null, null);
        if (!started) return false;
        AfterInput();
        return true;
    }

    /// <summary>ビューのボタン(頂点・辺・面・透過・統計・ミラー)。</summary>
    public void ClickTool(string tool)
    {
        core?.ClickTool(tool);
        AfterInput();
        PracticeCsJs.FocusRoot(RootId);
    }

    public void ChooseMenu(int index)
    {
        core?.ChooseMenu(index);
        AfterInput();
        PracticeCsJs.FocusRoot(RootId);
    }

    public void ChangeNPanel(string prop, string axis, string value)
    {
        core?.ChangeNPanel(prop, axis, value);
        AfterInput();
    }

    private static Modifiers Mods(int bits) => new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);

    /// <summary>width / height は getBoundingClientRect(枠を含む小数)、client は clientWidth * 65536 + clientHeight(枠を除いた整数。縦横比に使う)。</summary>
    private void OnFrame(double width, double height, double client)
    {
        if (core is null || renderer is null || disposed) return;
        var next = new ViewRect(0, 0, width, height);
        var clientSize = (Math.Floor(client / 65536), client % 65536);
        if (next != rect || clientSize != lastClient)
        {
            rect = next;
            lastClient = clientSize;
            core.Resize(next, clientSize.Item1, clientSize.Item2);
            Invalidate();   // 大きさが変わると、矢印の位置が変わる
        }
        var frame = renderer.Build(PracticeCsJs.Now());
        foreach (var buffer in frame.Uploads) PracticeGl.UploadBuffer(buffer.Id, (int)buffer.Layout, MemoryMarshal.AsBytes(buffer.Data.AsSpan()));
        foreach (var id in frame.Releases) PracticeGl.ReleaseBuffer(id);
        var length = FramePacker.LengthOf(frame);
        if (packed.Length < length) packed = new float[length * 2];
        FramePacker.Pack(frame, packed);
        PracticeGl.Draw(MemoryMarshal.AsBytes(packed.AsSpan(0, length)));
    }

    /// <summary>button の下位 4 ビットがボタン、その上が修飾キー(1 Shift・2 Ctrl・4 Alt・8 Meta)。</summary>
    private bool OnPointerDown(int buttonAndMods, double x, double y)
    {
        if (core is null) return false;
        var prevented = core.PointerDown(buttonAndMods & 15, x, y, Mods(buttonAndMods >> 4));
        AfterInput();
        return prevented;
    }

    private void OnPointerMove(double x, double y, int mods)
    {
        if (core is null) return;
        core.PointerMove(x, y, Mods(mods));
        AfterInput();
    }

    private void OnPointerUp(double x, double y)
    {
        if (core is null) return;
        core.PointerUp(x, y);
        AfterInput();
    }

    private bool OnWheel(double deltaY)
    {
        if (core is null) return false;
        var prevented = core.Wheel(deltaY);
        AfterInput();
        return prevented;
    }

    private bool OnKey(string code, string key, int mods)
    {
        if (core is null) return false;
        var prevented = core.KeyDown(code, key, Mods(mods));
        AfterInput();
        return prevented;
    }

    private void OnKeyUp(int mods)
    {
        if (core is null) return;
        core.KeyUp(Mods(mods));
        AfterInput();
    }

    private void OnFocus(bool on)
    {
        if (on) core?.Focus(); else core?.Blur();
    }

    /// <summary>入力のあと: 画面に出る状態が変わっていたら Changed を出す(毎回描き直さない。マウスの移動の大半は何も変わらない)。デバッグ用の状態も更新する。</summary>
    protected void AfterInput()
    {
        if (core is null || disposed) return;
        core.TakeEvents();   // ViewportCore がためるイベントの一覧は、ここでは使わない(EventEmitted で見る)ので、捨てる(ためたままにしない)
        var signature = Signature();
        if (signature == lastSignature) return;
        lastSignature = signature;
        Hud = BuildHud();
        if (Debug) PracticeCsJs.SetDebugState(DebugJson());
        Changed?.Invoke();
    }

    /// <summary>状態の署名を捨てて、次の AfterInput で必ず描き直す(画面の外の状態が変わったとき)。</summary>
    protected void Invalidate()
    {
        lastSignature = "";
        AfterInput();
    }

    private PracticeHud BuildHud()
    {
        var c = core!;
        var n = c.NPanel;
        var (texts, done, index) = Tasks();
        var marker = c.MarkerScreen;
        return new PracticeHud
        {
            ModeLabel = c.ModeLabel,
            ViewLabel = c.View.Label,
            StatsHidden = c.StatsHidden,
            StatsLines = c.StatsLines,
            ToolsEditHidden = c.ToolsEditHidden,
            Tools = [.. c.Tools.Select(kv => new HudTool(kv.Key, kv.Value.On, kv.Value.Disabled))],
            NPanelHidden = n.Hidden,
            NPanelName = n.Name,
            NPanelRows = [.. n.Rows.Select(r => new HudNPanelRow(r.Prop, r.Axis, r.Value, r.Disabled))],
            Menu = c.Menu is { } m ? new HudMenu(m.Title, [.. m.Items.Select(i => i.Label)], c.MenuPosition.X, c.MenuPosition.Y) : null,
            Box = c.BoxRect is { } b ? new HudBox(b.X, b.Y, b.Width, b.Height) : null,
            Status = c.Status,
            Numeric = c.NumericHud,
            ViewHighlight = c.Highlight.Hud == "view",
            HighlightTool = c.Highlight.Tool,
            MarkerHidden = marker is null,
            MarkerLabel = c.MarkerLabel,
            MarkerX = marker?.X ?? 0,
            MarkerY = marker?.Y ?? 0,
            TaskTexts = texts,
            TaskDone = done,
            TaskIndex = index,
        };
    }

    /// <summary>画面に出るものを 1 つの文字にする(前回と同じなら、描き直さない)。</summary>
    private string Signature()
    {
        var c = core!;
        var sb = new StringBuilder();
        sb.Append(c.ModeLabel).Append('|').Append(c.View.Label).Append('|').Append(c.Status).Append('|').Append(c.NumericHud).Append('|');
        sb.Append(c.StatsHidden ? "-" : string.Join("\n", c.StatsLines)).Append('|').Append(c.ToolsEditHidden).Append('|');
        foreach (var (k, t) in c.Tools) sb.Append(k).Append(t.On ? '1' : '0').Append(t.Disabled ? '1' : '0');
        var n = c.NPanel;
        sb.Append('|').Append(n.Hidden).Append(n.Name);
        foreach (var r in n.Rows) sb.Append(r.Value).Append(r.Disabled ? '1' : '0').Append(',');
        sb.Append('|');
        if (c.Menu is { } m) sb.Append(m.Title).Append(string.Join(",", m.Items.Select(i => i.Label))).Append(c.MenuPosition);
        sb.Append('|').Append(c.BoxRect);
        sb.Append('|').Append(c.Highlight).Append(c.MarkerScreen);
        sb.Append('|').Append(ExtraSignature());
        return sb.ToString();
    }

    /// <summary>practice.mjs が window.__practice から読む状態(オブジェクトの種類・位置・回転・スケール・モード・平行投影・視点の名前)。</summary>
    private string DebugJson()
    {
        var c = core!;
        static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        static string V(Vec3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";
        var objects = string.Join(",", c.Objects.Select(o => $"{{\"kind\":\"{o.Kind}\",\"p\":{V(o.Position)},\"r\":{V(o.Rotation)},\"s\":{V(o.Scale)}}}"));
        return $"{{\"objects\":[{objects}],\"mode\":\"{c.Mode}\",\"ortho\":{(c.View.Ortho ? "true" : "false")},\"view\":\"{c.View.Name}\"}}";
    }

    /// <summary>Changed を、状態の署名と関係なく出す(画面の外の状態が変わったとき)。</summary>
    protected void RaiseChanged() => Changed?.Invoke();

    public virtual void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) PracticeGl.Dispose();
        if (Debug) PracticeCsJs.ClearDebug();
        renderer?.Dispose();
        renderer = null;
        core = null;
    }
}

/// <summary>/practice の 3D の C# 版(?engine=cs)。お題の進行 <see cref="PracticeSession"/> を持つ。</summary>
public sealed class PracticeCsEngine : CsViewEngine, IPracticeCs
{
    public PracticeCsEngine(string rootId, string canvasHostId, Func<bool> emulateNumpad, bool debug) : base(rootId, canvasHostId, emulateNumpad, debug) { }

    public PracticeSession Session { get; private set; } = null!;

    protected override ViewportOptions CreateOptions() => new() { EmulateNumpad = EmulateNumpadFn };

    protected override void OnCoreCreated(ViewportCore core)
    {
        Session = new PracticeSession(core);
        if (Debug)
        {
            PracticeCsJs.ExposeDebug(index => { if (index >= 0 && index < Core.Objects.Count) { Core.SetSelection([Core.Objects[index]]); AfterInput(); } });
        }
    }

    protected override string ExtraSignature() => Session.Progress.Index + string.Join("", Session.Progress.Done.Select(d => d ? '1' : '0'));

    protected override (IReadOnlyList<string> Texts, IReadOnlyList<bool> Done, int Index) Tasks() =>
        ([.. PracticeTasks.Tasks.Select(t => t.Text)], [.. Session.Progress.Done], Session.Progress.Index);

    /// <summary>最初からやり直す(シーン・お題)。</summary>
    public void Reset()
    {
        Session.Reset();
        AfterInput();
        PracticeCsJs.FocusRoot(RootId);
    }
}
