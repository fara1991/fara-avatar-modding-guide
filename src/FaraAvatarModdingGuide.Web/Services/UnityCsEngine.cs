using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Render;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>js/unity-ui.js(Unity 風の画面の、DOM を測る・ドラッグする部分)の呼び出し。</summary>
internal static partial class UnityUiJs
{
    [JSImport("elementExists", "unity-ui")]
    internal static partial bool ElementExists(string id);

    [JSImport("setDebug", "unity-ui")]
    internal static partial void SetDebug(string json);

    [JSImport("setDebugGeo", "unity-ui")]
    internal static partial void SetDebugGeo(string json);

    [JSImport("setDebugFrame", "unity-ui")]
    internal static partial void SetDebugFrame(string json);
}

/// <summary>
/// Unity 風の画面(unity.js)の C# 版(?engine=cs)。操作のロジック(<see cref="UnityEditorCore"/>)・描画の命令(<see cref="UnitySceneRenderer"/>)・gl.js とのやり取り・画面(Razor)に出す状態(<see cref="UnityHud"/>)を持つ。
/// 状態が変わったら <see cref="Changed"/> を出す(画面は描き直す)。Scene ビューの WebGL は、画面の DOM ができてから <see cref="StartGl"/> で始める。
/// </summary>
public sealed class UnityCsEngine : IUnityCs
{
    private static Task? importingUi;

    private readonly string rootId;
    private readonly string sceneViewId;
    private readonly string glHostId;
    private readonly bool debug;
    private readonly UnityEditorCore core;
    private readonly UnitySceneRenderer renderer;
    private float[] packed = new float[FramePacker.HeaderFloats + FramePacker.ItemFloats * 64];
    private (double, double) lastRect;
    private (double, double) lastClient;
    private bool dirty = true;
    private bool debugGeo;
    private bool glStarted;
    private bool disposed;
    private UnityHud hud = new();

    public UnityCsEngine(string rootId, string sceneViewId, string glHostId, bool wheelNeedsFocus, string[] instances, bool debug)
    {
        this.rootId = rootId;
        this.sceneViewId = sceneViewId;
        this.glHostId = glHostId;
        this.debug = debug;
        core = new UnityEditorCore(new UnityOptions
        {
            Instances = instances,
            WheelNeedsFocus = wheelNeedsFocus,
            Schedule = Schedule,
            Now = PracticeCsJs.Now,
        });
        renderer = new UnitySceneRenderer(core);
        core.Changed += () => dirty = true;
        // unity.js のコンストラクタは、最後に animate() を 1 回実行する(まだ大きさが分からない)
        core.Animate(PracticeCsJs.Now());
        Flush();
    }

    public UnityEditorCore Core => core;

    /// <summary>お題の進行(/practice/unity のページだけが付ける)。</summary>
    public UnityPracticeSession? Practice { get; private set; }

    public UnityHud Hud => hud;

    public bool GlStarted => glStarted;

    public bool IsDisposed => disposed;

    public event Action? Changed;

    /// <summary>エディターの操作のイベント(記事内チュートリアルが、手順の判定に使う)。</summary>
    public event Action<ViewEvent>? EventEmitted
    {
        add => core.EventEmitted += value;
        remove => core.EventEmitted -= value;
    }

    /// <summary>unity-ui.js を読み込む(1 回だけ)。</summary>
    public static Task ImportUiAsync() => importingUi ??= JSHost.ImportAsync("unity-ui", "../js/unity-ui.js");

    /// <inheritdoc />
    public void AttachPractice()
    {
        Practice = new UnityPracticeSession(core);
        Practice.Changed += () => dirty = true;
        Flush();
    }

    private IDisposable Schedule(int ms, Action action)
    {
        var cancelled = false;
        PracticeCsJsTutorial.SetTimer(ms, () => { if (!cancelled && !disposed) { action(); Flush(); } });
        return new Cancel(() => cancelled = true);
    }

    private sealed class Cancel(Action cancel) : IDisposable
    {
        public void Dispose() => cancel();
    }

    // ---------------------------------------------------------------- WebGL
    public bool StartGl()
    {
        if (glStarted) return true;
        glStarted = PracticeGl.Start(sceneViewId, OnFrame, OnPointerDown, OnPointerMove, OnPointerUp, OnWheel, OnKey, OnKeyUp, OnGlFocus, glHostId, rootId, "input, select, textarea");
        if (debug) PracticeCsJs.ExposeDebug(_ => { });
        return glStarted;
    }

    private void OnFrame(double width, double height, double client)
    {
        if (disposed) return;
        var rect = (width, height);
        var clientSize = (Math.Floor(client / 65536), client % 65536);
        if (rect != lastRect || clientSize != lastClient)
        {
            lastRect = rect;
            lastClient = clientSize;
            core.Resize(new ViewRect(0, 0, width, height), clientSize.Item1, clientSize.Item2);
        }
        core.Animate(PracticeCsJs.Now());
        var frame = renderer.Build();
        foreach (var buffer in frame.Uploads) PracticeGl.UploadBuffer(buffer.Id, (int)buffer.Layout, MemoryMarshal.AsBytes(buffer.Data.AsSpan()));
        foreach (var id in frame.Releases) PracticeGl.ReleaseBuffer(id);
        var length = FramePacker.LengthOf(frame);
        if (packed.Length < length) packed = new float[length * 2];
        FramePacker.Pack(frame, packed);
        PracticeGl.Draw(MemoryMarshal.AsBytes(packed.AsSpan(0, length)));
        Flush();
        if (debug)
        {
            UnityUiJs.SetDebug(DebugJson());   // カメラの位置は毎フレーム変わる
            UnityUiJs.SetDebugFrame(DebugFrameJson(frame));
            if (!debugGeo) { debugGeo = true; UnityUiJs.SetDebugGeo(DebugGeometryJson()); }
        }
    }

    private static Modifiers Mods(int bits) => new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);

    /// <summary>button の下位 4 ビットがボタン、その上が修飾キー(1 Shift・2 Ctrl・4 Alt・8 Meta)。</summary>
    private bool OnPointerDown(int buttonAndMods, double x, double y)
    {
        var prevented = core.ScenePointerDown(buttonAndMods & 15, Mods(buttonAndMods >> 4), x, y);
        Flush();
        return prevented;
    }

    private void OnPointerMove(double x, double y, int mods)
    {
        core.ScenePointerMove(x, y);
        Flush();
    }

    private void OnPointerUp(double x, double y)
    {
        core.ScenePointerUp(x, y);
        Flush();
    }

    private bool OnWheel(double deltaY)
    {
        var prevented = core.SceneWheel(deltaY);
        Flush();
        return prevented;
    }

    private bool OnKey(string code, string key, int mods)
    {
        var prevented = core.KeyDown(code, key, Mods(mods));
        Flush();
        return prevented;
    }

    private void OnKeyUp(int mods) { }

    private void OnGlFocus(bool on) { }

    // ---------------------------------------------------------------- 画面の操作(Razor から)
    public void ClickTool(string tool) { core.SetTool(tool); Flush(); }

    public void OpenMenu(string name, string selector) { core.OpenMenu(name, new UnityAnchor("below", Selector: selector)); Flush(); }

    public void MenuItem(string item)
    {
        if (item == "sdk-panel") core.OpenSdk();
        Flush();
    }

    public void ContextItem(string item) { core.ContextItem(item); Flush(); }

    public void AddItem(string type) { core.AddComponent(core.Selected, type); Flush(); }

    public void OpenAddComponent() { core.OpenAddComponent(); Flush(); }

    public void CloseSdk() { core.CloseSdk(); Flush(); }

    public void BuildSdk(string kind) { core.Build(kind); Flush(); }

    /// <summary>エディターの中の、どのボタンにも当たらないクリック: ポップアップを閉じる。</summary>
    public void ClickBackground() { core.ClosePopup(); Flush(); }

    public void SetActive(bool on)
    {
        if (core.Get(core.Selected) is { } n) core.SetActive(n.Id, on);
        Flush();
    }

    public void SetTag(string tag)
    {
        if (core.Get(core.Selected) is { } n) core.SetTag(n.Id, tag);
        Flush();
    }

    /// <summary>Inspector の Transform の欄(change イベント)。文字を parseFloat して、数でなければ無視する。</summary>
    public void SetTransform(string prop, int axis, string text)
    {
        if (core.Get(core.Selected) is { } n) core.SetTransformValue(n.Id, prop, axis, Js.ParseFloat(text), true);
        Flush();
    }

    public void SetAddSearch(string text) { core.SetAddSearch(text); Flush(); }

    public void AddSearchKey(string key)
    {
        if (key == "Enter") core.AddFirstFromSearch();
        else if (key == "Escape") core.ClosePopup();
        Flush();
    }

    public void ToggleFold(string id)
    {
        if (core.Get(id) is { } n) core.SetExpanded(n.Id, !n.Expanded);
        Flush();
    }

    public void SelectRow(string id) { core.Select(id); Flush(); }

    public void ContextAt(string id, double x, double y) { core.OpenContextMenu(id, new UnityAnchor("point", x, y)); Flush(); }

    public void DoubleClickRow(string id) { core.DoubleClickNode(id); Flush(); }

    public void ClearSelection() { core.ClearSelection(); Flush(); }

    public void ClickAsset(string asset) { core.ClickAsset(asset); Flush(); }

    public void DropNode(string id, string targetKind, string? targetId) { core.DropNode(id, targetKind, targetId); Flush(); }

    public void DropAsset(string asset, string targetKind, string? targetId, int slot, double x, double y) { core.DropAsset(asset, targetKind, targetId, slot, x, y); Flush(); }

    public void SetFocused(bool on) => core.Focused = on;

    public void Reset()
    {
        if (Practice is not null) Practice.Reset(); else core.ResetScene();
        Flush();
        PracticeCsJs.FocusRoot(rootId);
    }

    public void Refresh() => Flush();

    // ---------------------------------------------------------------- 画面に出す状態
    /// <summary>状態が変わっていたら、HUD を作り直して Changed を出す。</summary>
    private void Flush()
    {
        if (!dirty || disposed) return;
        dirty = false;
        hud = BuildHud();
        if (debug) UnityUiJs.SetDebug(DebugJson());
        Changed?.Invoke();
    }

    /// <summary>確認用の状態(視点・カメラの位置・ノードの位置/回転/拡大率・選択・ツール)。JS 版の window.__unity から同じ形で読めるもの。</summary>
    private string DebugJson()
    {
        static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        static string V(IEnumerable<double> v) => "[" + string.Join(",", v.Select(F)) + "]";
        static string Q(string? s) => s is null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        string Path(UnityNode n) => string.Join("/", core.Ancestors(n).AsEnumerable().Reverse().Select(a => a.Name).Append(n.Name));
        var nodes = string.Join(",", core.Nodes.Select(n => $"{{\"path\":{Q(Path(n))},\"p\":{V(n.P)},\"r\":{V(n.R)},\"s\":{V(n.S)},\"active\":{(n.Active ? "true" : "false")},\"tag\":{Q(n.Tag)}}}"));
        var v = core.View;
        var cp = core.Camera.Position;
        var sel = core.Get(core.Selected);
        return $"{{\"view\":{{\"target\":{V([v.Target.X, v.Target.Y, v.Target.Z])},\"dist\":{F(v.Dist)},\"yaw\":{F(v.Yaw)},\"pitch\":{F(v.Pitch)}}},\"camera\":{V([cp.X, cp.Y, cp.Z])},\"selected\":{Q(sel is null ? null : Path(sel))},\"tool\":{Q(core.Tool)},\"nodes\":[{nodes}]}}";
    }

    /// <summary>確認用: GPU に渡す行列(射影と、描画ごとの ModelView)。three.js の同じものと、ビットまで同じか確かめる。</summary>
    private static string DebugFrameJson(Frame frame)
    {
        static string F(float d) => d.ToString("R", CultureInfo.InvariantCulture);
        var proj = string.Join(",", frame.Projection.Select(F));
        var items = string.Join(",", frame.Items.Select(i => "[" + string.Join(",", Mat4.Multiply(frame.View, i.World).E.Select(e => F((float)e))) + "]"));
        return $"{{\"proj\":[{proj}],\"mv\":[{items}],\"labels\":[{string.Join(",", frame.Items.Select(i => "\"" + i.Label + "\""))}]}}";
    }

    /// <summary>確認用: 部品のジオメトリの位置・法線のビット列のハッシュ(three.js の同じものと、ビットまで同じか確かめる)。</summary>
    private string DebugGeometryJson()
    {
        static uint Hash(float[] a) { uint h = 2166136261; foreach (var f in a) { h ^= (uint)BitConverter.SingleToInt32Bits(f); h *= 16777619; } return h; }
        var list = new List<string>();
        foreach (var n in core.Nodes)
            foreach (var p in n.Parts ?? [])
                list.Add($"[\"{n.Name}:{p.Shape}\",{Hash(p.Geometry.Positions)},{Hash(p.Geometry.Normals)}]");
        return "[" + string.Join(",", list) + "]";
    }

    private UnityHud BuildHud()
    {
        var rows = core.Rows();
        var inspector = core.Inspector();
        var assets = UnityData.Assets.Where(a => !a.Hidden).Select(a => new UnityHudAsset(a.Key, a.Label, a.Type, a.Color is { } c ? $"#{c:x6}" : null, a.Note ?? a.Shader ?? "")).ToList();
        UnityHudSdk? sdk = null;
        if (core.SdkOpen)
        {
            var m = core.SdkModel();
            sdk = m is null ? new UnityHudSdk(false, "", [], 0, "", []) : new UnityHudSdk(true, m.AvatarName, m.Notes, m.Overall, UnityData.Ranks[m.Overall], [.. m.Rows.Select(r => new UnityHudSdkRow(r.Label, r.Value, r.Rank, UnityData.Ranks[r.Rank]))]);
        }
        var popup = BuildPopup();
        return new UnityHud
        {
            Tool = core.Tool,
            Status = core.Status,
            Info = core.InfoText,
            Rows = [.. rows.Select(r => new UnityHudRow(r.Id, r.Name, r.Depth, r.HasKids, r.Expanded, r.Selected, r.Inactive, r.Prefab, r.Icon, r.EditorOnly))],
            Inspector = inspector is null ? null : new UnityHudInspector(inspector.NodeId, inspector.Name, inspector.Active, inspector.Tag, inspector.P, inspector.R, inspector.S,
                [.. inspector.Components.Select(c => new UnityHudComp(c.Type, c.Label, [.. c.Fields.Select(f => new UnityHudField(f.Kind, f.Label, f.Text, f.Icon, f.Color, f.Slot, f.Hl))]))]),
            Assets = assets,
            Preview = core.Preview is { } p ? new UnityHudPreview(p.Text, p.Busy) : null,
            SdkOpen = core.SdkOpen,
            Sdk = sdk,
            Popup = popup,
            Highlight = ResolveHighlight(rows, inspector, assets, sdk, popup),
            TaskTexts = Practice is null ? [] : [.. UnityPracticeSession.Tasks.Select(t => t.Text)],
            TaskIndex = Practice?.Index ?? 0,
        };
    }

    private UnityHudPopup? BuildPopup()
    {
        var p = core.Popup;
        if (p is null) return null;
        var a = p.Anchor;
        var key = $"{p.Kind}|{p.NodeId}|{p.MenuName}|{a.Kind}|{a.X.ToString(CultureInfo.InvariantCulture)}|{a.Y.ToString(CultureInfo.InvariantCulture)}|{a.Selector}";
        var items = p.Kind == "add"
            ? [.. core.AddList().Select(c => new UnityHudAddItem(c.Type, c.Label, c.Path, core.Highlight.Popup == $"add-{c.Type}"))]
            : (IReadOnlyList<UnityHudAddItem>)[];
        return new UnityHudPopup(p.Kind, key, a.Kind, a.X, a.Y, a.Kind == "row" ? a.Selector : a.Selector, p.NodeId is null ? null : core.Get(p.NodeId)?.Name, p.MenuName, p.Search, items);
    }

    /// <summary>案内中の場所が、いま画面にあるか(unity.js の applyHighlight は、最初に当てはまる 1 種類だけを調べ、見つからない・隠れているなら光らせない)。</summary>
    private UnityHudHighlight? ResolveHighlight(IReadOnlyList<UnityRow> rows, UnityInspector? inspector, IReadOnlyList<UnityHudAsset> assets, UnityHudSdk? sdk, UnityHudPopup? popup)
    {
        var h = core.Highlight;
        if (!string.IsNullOrEmpty(h.Hierarchy))
        {
            var row = rows.FirstOrDefault(r => r.Name == h.Hierarchy);
            return row is null ? null : new UnityHudHighlight("hierarchy", row.Id, h.Label);
        }
        if (!string.IsNullOrEmpty(h.Project))
            return assets.Any(a => a.Key == h.Project) ? new UnityHudHighlight("project", h.Project, h.Label) : null;
        if (!string.IsNullOrEmpty(h.Inspector))
        {
            if (inspector is null) return null;
            var key = h.Inspector;
            var exists = key is "active" or "tag" or "add-component"
                || (key.StartsWith("material-slot-", StringComparison.Ordinal) && inspector.Components.Any(c => c.Fields.Any(f => f.Hl == key)))
                || (key.Length == 3 && key[1] == '-' && key[0] is 'p' or 'r' or 's' && key[2] is '0' or '1' or '2');
            return exists ? new UnityHudHighlight("inspector", key, h.Label) : null;
        }
        if (!string.IsNullOrEmpty(h.Menu))
            return Menus.Contains(h.Menu) ? new UnityHudHighlight("menu", h.Menu, h.Label) : null;
        if (!string.IsNullOrEmpty(h.Sdk))
            return sdk is { HasAvatar: true } && h.Sdk is "build-test" or "build-upload" ? new UnityHudHighlight("sdk", h.Sdk, h.Label) : null;
        if (!string.IsNullOrEmpty(h.Tool))
            return h.Tool is "hand" or "move" or "rotate" or "scale" ? new UnityHudHighlight("tool", h.Tool, h.Label) : null;
        if (!string.IsNullOrEmpty(h.Popup))
        {
            if (popup is null) return null;
            var exists = (popup.Kind == "context" && h.Popup == "setup-outfit")
                || (popup.Kind == "menu" && popup.MenuName == "VRChat SDK" && h.Popup == "sdk-panel")
                || (popup.Kind == "add" && popup.Items.Any(i => $"add-{i.Type}" == h.Popup));
            return exists ? new UnityHudHighlight("popup", h.Popup, h.Label) : null;
        }
        if (h.Scene) return new UnityHudHighlight("scene", "", h.Label);
        return null;
    }

    public static readonly string[] Menus = ["File", "Edit", "Assets", "GameObject", "Component", "VRChat SDK", "Tools", "Window"];

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (glStarted) PracticeGl.Dispose();
        glStarted = false;
        core.StopPreview();
    }
}
