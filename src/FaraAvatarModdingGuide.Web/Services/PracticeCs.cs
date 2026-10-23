using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Render;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>wwwroot/js/gl.js(薄い WebGL 2 層)の呼び出し。バッファの中身は変わったときだけ送り、1 フレームの描画の命令は 1 回の呼び出しで渡す。</summary>
internal static partial class PracticeGl
{
    [JSImport("start", "gl")]
    [return: JSMarshalAs<JSType.Boolean>]
    internal static partial bool Start(
        string rootId,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> frame,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number, JSType.Boolean>>] Func<int, double, double, bool> pointerDown,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, double, int> pointerMove,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> pointerUp,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Boolean>>] Func<double, bool> wheel,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.String, JSType.Number, JSType.Boolean>>] Func<string, string, int, bool> key,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> keyup,
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> focus,
        string canvasHostId);

    [JSImport("uploadBuffer", "gl")]
    internal static partial void UploadBuffer(int id, int layout, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("releaseBuffer", "gl")]
    internal static partial void ReleaseBuffer(int id);

    [JSImport("draw", "gl")]
    internal static partial void Draw([JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("dispose", "gl")]
    internal static partial void Dispose();
}

/// <summary>js/practice-cs.js(確認用のデバッグ口とフォーカス)の呼び出し。</summary>
internal static partial class PracticeCsJs
{
    [JSImport("focusRoot", "practice-cs")]
    internal static partial void FocusRoot(string rootId);

    /// <summary>?debug=1 のとき、window.__practice(練習の状態。tools/verify/practice.mjs が読む)を作る。select は、オブジェクトの番号を渡して選ばせる関数。</summary>
    [JSImport("exposeDebug", "practice-cs")]
    internal static partial void ExposeDebug([JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> select);

    [JSImport("setDebugState", "practice-cs")]
    internal static partial void SetDebugState(string json);

    [JSImport("clearDebug", "practice-cs")]
    internal static partial void ClearDebug();
}

/// <summary>
/// /practice の 3D の C# 版(?engine=cs)。操作のロジック(<see cref="ViewportCore"/>・お題 <see cref="PracticeSession"/>)と描画の命令(<see cref="SceneRenderer"/>)を持ち、
/// gl.js が数値にしたポインター・ホイール・キーを ViewportCore に渡し、フレームごとに描画の命令を gl.js に渡す。HUD・お題の一覧は、画面(Razor)が <see cref="Core"/>・<see cref="Session"/> の状態を読んで描く。
/// 状態が変わったら <see cref="Changed"/> を出す(画面は描き直す)。
/// <b>契約</b>: <see cref="SceneRenderer"/> は <see cref="ViewportCore"/> を作った直後(操作の前)に作る(編集モードの描画データの通知を取りこぼさないため)。
/// </summary>
public sealed class PracticeCsEngine : IPracticeCs
{
    private static Task? importing;

    private readonly string rootId;
    private readonly string canvasHostId;
    private readonly Func<bool> emulateNumpad;
    private readonly bool debug;
    private ViewportCore? core;
    private SceneRenderer? renderer;
    private float[] packed = new float[FramePacker.HeaderFloats + FramePacker.ItemFloats * 32];
    private ViewRect rect = new(0, 0, 0, 0);
    private string lastSignature = "";
    private bool started;
    private bool disposed;

    public PracticeCsEngine(string rootId, string canvasHostId, Func<bool> emulateNumpad, bool debug)
    {
        this.rootId = rootId;
        this.canvasHostId = canvasHostId;
        this.emulateNumpad = emulateNumpad;
        this.debug = debug;
    }

    public ViewportCore Core => core ?? throw new InvalidOperationException("始めていません");
    public PracticeSession Session { get; private set; } = null!;
    public PracticeHud Hud { get; private set; } = new();

    /// <summary>画面に出す状態が変わった(HUD・お題の一覧を描き直す)。</summary>
    public event Action? Changed;

    /// <summary>gl.js を読み込む(ページごとではなく、1 回だけ)。</summary>
    public static Task ImportAsync() => importing ??= JSHost.ImportAsync("gl", "../js/gl.js");

    /// <summary>practice-cs.js を読み込む(1 回だけ)。</summary>
    private static Task? importingHelper;
    public static Task ImportHelperAsync() => importingHelper ??= JSHost.ImportAsync("practice-cs", "../js/practice-cs.js");

    /// <summary>始める。WebGL 2 が使えなければ false。</summary>
    public bool Start()
    {
        core = new ViewportCore(new ViewRect(0, 0, 960, 540), new ViewportOptions { EmulateNumpad = emulateNumpad });
        renderer = new SceneRenderer(core);   // 操作の前に作る(契約)
        Session = new PracticeSession(core);
        if (debug)
        {
            PracticeCsJs.ExposeDebug(index => { if (core is not null && index >= 0 && index < core.Objects.Count) { core.SetSelection([core.Objects[index]]); AfterInput(); } });
        }
        started = PracticeGl.Start(rootId, OnFrame, OnPointerDown, OnPointerMove, OnPointerUp, OnWheel, OnKey, OnKeyUp, OnFocus, canvasHostId);
        if (!started) return false;
        AfterInput();
        return true;
    }

    /// <summary>最初からやり直す(シーン・お題)。</summary>
    public void Reset()
    {
        Session.Reset();
        AfterInput();
        PracticeCsJs.FocusRoot(rootId);
    }

    /// <summary>ビューのボタン(頂点・辺・面・透過・統計・ミラー)。</summary>
    public void ClickTool(string tool)
    {
        core?.ClickTool(tool);
        AfterInput();
        PracticeCsJs.FocusRoot(rootId);
    }

    public void ChooseMenu(int index)
    {
        core?.ChooseMenu(index);
        AfterInput();
        PracticeCsJs.FocusRoot(rootId);
    }

    public void ChangeNPanel(string prop, string axis, string value)
    {
        core?.ChangeNPanel(prop, axis, value);
        AfterInput();
    }

    private static Modifiers Mods(int bits) => new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);

    private void OnFrame(double width, double height)
    {
        if (core is null || renderer is null || disposed) return;
        var next = new ViewRect(0, 0, width, height);
        if (next != rect)
        {
            rect = next;
            core.Resize(next);
        }
        var frame = renderer.Build();
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
    private void AfterInput()
    {
        if (core is null || disposed) return;
        var signature = Signature();
        if (signature == lastSignature) return;
        lastSignature = signature;
        Hud = BuildHud();
        if (debug) PracticeCsJs.SetDebugState(DebugJson());
        Changed?.Invoke();
    }

    private PracticeHud BuildHud()
    {
        var c = core!;
        var n = c.NPanel;
        var p = Session.Progress;
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
            TaskTexts = [.. PracticeTasks.Tasks.Select(t => t.Text)],
            TaskDone = [.. p.Done],
            TaskIndex = p.Index,
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
        sb.Append('|').Append(Session.Progress.Index).Append(string.Join("", Session.Progress.Done.Select(d => d ? '1' : '0')));
        return sb.ToString();
    }

    /// <summary>practice.mjs が window.__practice から読む状態(オブジェクトの種類・位置・回転・スケール・モード・平行投影・視点の名前)。</summary>
    private string DebugJson()
    {
        var c = core!;
        string F(double d) => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        string V(Vec3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";
        var objects = string.Join(",", c.Objects.Select(o => $"{{\"kind\":\"{o.Kind}\",\"p\":{V(o.Position)},\"r\":{V(o.Rotation)},\"s\":{V(o.Scale)}}}"));
        return $"{{\"objects\":[{objects}],\"mode\":\"{c.Mode}\",\"ortho\":{(c.View.Ortho ? "true" : "false")},\"view\":\"{c.View.Name}\"}}";
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) PracticeGl.Dispose();
        if (debug) PracticeCsJs.ClearDebug();
        renderer?.Dispose();
        renderer = null;
        core = null;
    }
}
