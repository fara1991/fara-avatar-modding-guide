namespace FaraAvatarModdingGuide.Practice;

// シーン(オブジェクトの追加・削除)・取り消し・モード・選択・HUD・共同練習の口。viewport.js の対応する関数を写したもの。
public sealed partial class ViewportCore
{
    /// <summary>追加できるメッシュ(Shift+A)。Blender の「メッシュ」メニューの順。</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Primitives =
    [
        ("plane", "平面"), ("cube", "立方体"), ("sphere", "UV球"), ("cylinder", "円柱"), ("cone", "円錐"), ("torus", "トーラス"),
    ];

    private static readonly Dictionary<string, string> Names = new()
    {
        ["plane"] = "Plane", ["cube"] = "Cube", ["sphere"] = "Sphere", ["cylinder"] = "Cylinder", ["cone"] = "Cone", ["torus"] = "Torus",
    };

    private static readonly Dictionary<string, string> SelectModeLabels = new() { ["vert"] = "頂点", ["edge"] = "辺", ["face"] = "面" };

    public List<SceneObject> Objects { get; } = [];
    public List<SceneObject> Selected { get; private set; } = [];
    public SceneObject? Active { get; private set; }
    public TransformOp? Op { get; private set; }
    /// <summary>ループカット(Ctrl+R)のプレビュー中の状態(中でなければ null)。</summary>
    public LoopCutState? LoopCut { get; private set; }
    /// <summary>"object" または "edit"。</summary>
    public string Mode { get; private set; } = "object";
    public SceneObject? EditObj { get; private set; }
    /// <summary>編集モードの選択モード: "vert" / "edge" / "face"。</summary>
    public string SelectMode { get; private set; } = "vert";
    public bool Xray { get; private set; }
    public bool ShowStats { get; private set; }
    public MenuState? Menu { get; private set; }

    // ---------------------------------------------------------------- 画面に出す文字(HUD)
    /// <summary>左下の状態表示(setStatus)。</summary>
    public string Status { get; private set; } = "";
    /// <summary>数値入力の表示("入力: 2")。</summary>
    public string NumericHud { get; private set; } = "";
    /// <summary>左上のモード表示。</summary>
    public string ModeLabel { get; private set; } = "オブジェクトモード";
    /// <summary>軸固定の線(操作中だけ表示)。</summary>
    public AxisLine AxisLine { get; private set; } = new(false, Vec3.Zero, 0);
    /// <summary>統計(オーバーレイ > 統計)を隠しているか。隠しているあいだ、StatsLines は前に出した文字のまま(現行と同じ)。</summary>
    public bool StatsHidden { get; private set; } = true;
    public IReadOnlyList<string> StatsLines { get; private set; } = [];
    public bool ToolsEditHidden { get; private set; } = true;
    public IReadOnlyDictionary<string, ToolState> Tools { get; private set; } = new Dictionary<string, ToolState>();
    public NPanelState NPanel => new(npanelHidden, npanelName, [.. npanelRows.Select(r => new NPanelRow(r.Prop, r.Axis, r.Value, r.Disabled))]);

    private bool npanelHidden = true;
    private string npanelName = "—";
    private readonly List<(string Prop, string Axis, string Value, bool Disabled)> npanelRows =
        [.. new[] { "position", "rotation", "scale" }.SelectMany(p => new[] { "x", "y", "z" }.Select(a => (p, a, "", false)))];

    // ---------------------------------------------------------------- 取り消し
    private sealed record ObjectSnapshot(string Kind, string Name, string Rid, Vec3 P, Vec3 R, Vec3 S, bool Sel, bool Act, MeshData Mesh, bool Mirror);

    private sealed record Snapshot(string Mode, string? Edit, List<ObjectSnapshot> Objects);

    private readonly List<Snapshot> undoStack = [];
    private List<Snapshot> redoStack = [];

    public int UndoDepth => undoStack.Count;
    public int RedoDepth => redoStack.Count;

    private Snapshot TakeSnapshot() => new(Mode, EditObj?.Name, [.. Objects.Select(o => new ObjectSnapshot(
        o.Kind, o.Name, o.Rid, o.Position, o.Rotation, o.Scale, Selected.Contains(o), Active == o, o.Em.ToData(), o.Mirror))]);

    private void Restore(Snapshot snap)
    {
        SetMode("object", true);
        foreach (var o in Objects.ToList()) RemoveObject(o);
        foreach (var d in snap.Objects)
        {
            var m = AddObject(d.Kind, null, d.Mesh);
            m.Name = d.Name;
            m.Position = d.P;
            m.SetRotation(d.R.X, d.R.Y, d.R.Z);
            m.Scale = d.S;
            m.Mirror = d.Mirror;
            if (d.Rid.Length > 0) m.Rid = d.Rid;
            if (d.Sel) Selected.Add(m);
            if (d.Act) Active = m;
        }
        SetXray(Xray, true);
        if (snap.Mode == "edit" && snap.Edit is not null)
        {
            Active = Find(snap.Edit) ?? Active;
            SetMode("edit", true);
        }
        RefreshSelection();
        UpdateTools();
    }

    private void PushUndo()
    {
        undoStack.Add(TakeSnapshot());
        if (undoStack.Count > MaxUndo) undoStack.RemoveAt(0);
        redoStack = [];
    }

    private Snapshot? PopUndo()
    {
        if (undoStack.Count == 0) return null;
        var s = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        return s;
    }

    private void Undo()
    {
        var snap = PopUndo();
        if (snap is null) { Status = "これ以上元に戻せません"; return; }
        redoStack.Add(TakeSnapshot());
        Restore(snap);
        Status = "元に戻す";
        Emit("undo");
    }

    private void Redo()
    {
        if (redoStack.Count == 0) return;
        var snap = redoStack[^1];
        redoStack.RemoveAt(redoStack.Count - 1);
        undoStack.Add(TakeSnapshot());
        Restore(snap);
        Status = "やり直し";
        Emit("redo");
    }

    // ---------------------------------------------------------------- シーン
    /// <summary>最初の状態に戻す。scene を省略するとコンストラクタで渡したもの(それも省略すると立方体 1 つ)。</summary>
    public void ResetScene(IReadOnlyList<SceneSpec>? scene = null)
    {
        scene ??= Options.Scene ?? [new SceneSpec("cube")];
        CloseMenu();
        Op = null;
        EndLoopCut();
        SetMode("object", true);
        foreach (var o in Objects.ToList()) RemoveObject(o);
        foreach (var d in scene)
        {
            var m = AddObject(d.Kind);
            if (!string.IsNullOrEmpty(d.Name)) m.Name = d.Name;
            if (d.P is not null) m.Position = new Vec3(d.P[0], d.P[1], d.P[2]);
            if (d.R is not null) m.SetRotationDegrees(d.R[0], d.R[1], d.R[2]);
            if (d.S is not null) m.Scale = new Vec3(d.S[0], d.S[1], d.S[2]);
        }
        Selected = [];
        Active = null;
        undoStack.Clear();
        redoStack = [];
        View.Reset(Options.ViewDist is { } dist && dist != 0 ? dist : null, Options.ViewTarget is { } t ? new Vec3(t[0], t[1], t[2]) : null);
        SelectMode = "vert";
        SetXray(false, true);
        SetStats(false, true);
        npanelHidden = true;
        NumericHud = "";
        AxisLine = new AxisLine(false, Vec3.Zero, 0);
        RefreshSelection();
        UpdateTools();
        Status = "クリックして選択。G / R / S で動かす";
    }

    private string UniqueName(string baseName)
    {
        if (!Objects.Any(o => o.Name == baseName)) return baseName;
        for (var i = 1; ; i++)
        {
            var n = $"{baseName}.{i:000}";
            if (!Objects.Any(o => o.Name == n)) return n;
        }
    }

    private SceneObject MakeObject(string kind, MeshData? data)
    {
        var fresh = data is null;
        var em = EditMesh.FromData(data ?? MeshShapes.Create(kind));
        if (fresh) em.SelectAll("vert"); // 追加したばかりのメッシュは、編集モードで全体が選択されている(Blender と同じ)
        var mesh = new SceneObject(kind, "", em) { Mirror = false, Rid = NewId() };
        mesh.Name = UniqueName(Names.TryGetValue(kind, out var n) ? n : kind);
        Rebuild(mesh);
        return mesh;
    }

    /// <summary>オブジェクトを置く。at を省略すると 3D カーソル(原点)。data があればその形(取り消し・複製・分離)、なければ基本の形。</summary>
    private SceneObject AddObject(string kind, Vec3? at = null, MeshData? data = null)
    {
        var mesh = MakeObject(kind, data);
        mesh.Position = at ?? Vec3.Zero;
        Objects.Add(mesh);
        return mesh;
    }

    private void RemoveObject(SceneObject mesh)
    {
        if (mesh == EditObj) SetMode("object", true);
        Objects.Remove(mesh);
        Selected = [.. Selected.Where(o => o != mesh)];
        if (Active == mesh) Active = Selected.Count > 0 ? Selected[^1] : null;
    }

    /// <summary>テストの準備用: 選択を直接置く(アクティブは最後)。</summary>
    public void SetSelection(IEnumerable<SceneObject> selected)
    {
        Selected = [.. selected];
        Active = Selected.Count > 0 ? Selected[^1] : null;
        RefreshSelection();
    }

    /// <summary>テストの準備用: オブジェクトのメッシュの中身を差し替える(小さなメッシュで試すため)。</summary>
    public void ReplaceMesh(SceneObject obj, MeshData mesh)
    {
        obj.Em = EditMesh.FromData(mesh);
        Rebuild(obj);
    }

    /// <summary>形(メッシュの中身)を作り直す: 表示・レイ判定用のジオメトリと統計(viewport.js の rebuild)。編集モードの点・線の描画データは描画層が em から作る。</summary>
    private void Rebuild(SceneObject obj)
    {
        obj.Rebuild();
        if (obj == EditObj) OverlayRebuilt?.Invoke(obj);
        UpdateStats();
    }

    /// <summary>
    /// 編集モードの点・線・選んだ面の描画データを作り直すタイミング(viewport.js が rebuildOverlay を呼ぶ所: 編集中のオブジェクトの rebuild・setXray・
    /// 編集モードに入ったとき・setSelectMode)。現行は、それ以外のときに選択が変わっても描画データを作り直さない(古い状態が出うる)ので、
    /// 描画層はこの通知を受けたときだけ作り直す。通知を受けた時点の <see cref="Xray"/> と <see cref="SelectMode"/> も、その時点のものを使う。
    /// </summary>
    public event Action<SceneObject>? OverlayRebuilt;

    /// <summary>編集モードを出て、点・線・面の描画データを捨てるタイミング(removeOverlay)。</summary>
    public event Action<SceneObject>? OverlayRemoved;

    /// <summary>編集中のメッシュを変えたあとに呼ぶ(表示の更新)。</summary>
    private void EditChanged()
    {
        if (EditObj is null) return;
        Rebuild(EditObj);
        UpdateStats();
    }

    // ---------------------------------------------------------------- 統計・ボタン
    private void UpdateStats()
    {
        StatsHidden = !ShowStats;
        if (!ShowStats) return;
        if (Mode == "edit" && EditObj is not null)
        {
            var em = EditObj.Em;
            var s = em.Stats();
            var c = em.Counts();
            StatsLines = [EditObj.Name, $"頂点 {c.Verts}/{s.Verts}", $"辺 {c.Edges}/{s.Edges}", $"面 {c.Faces}/{s.Faces}", $"三角形 {s.Tris}"];
        }
        else
        {
            int verts = 0, edges = 0, faces = 0, tris = 0;
            foreach (var o in Objects)
            {
                var s = o.Em.Stats();
                verts += s.Verts; edges += s.Edges; faces += s.Faces; tris += s.Tris;
            }
            StatsLines = [$"オブジェクト {Selected.Count}/{Objects.Count}", $"頂点 {verts}", $"辺 {edges}", $"面 {faces}", $"三角形 {tris}"];
        }
    }

    private void UpdateTools()
    {
        ToolsEditHidden = Mode != "edit";
        var tools = new Dictionary<string, ToolState>();
        foreach (var k in new[] { "vert", "edge", "face", "xray", "stats", "mirror" })
        {
            var on = k == SelectMode ? Mode == "edit" : k == "xray" ? Xray : k == "stats" ? ShowStats : k == "mirror" ? Active?.Mirror == true : false;
            tools[k] = new ToolState(on, k == "mirror" && Active is null);
        }
        Tools = tools;
    }

    /// <summary>透過表示(Alt+Z)。quiet ならステータスとイベントを出さない。</summary>
    public void SetXray(bool? on = null, bool quiet = false)
    {
        var value = on ?? !Xray;
        Xray = value;
        foreach (var o in Objects) o.XrayApplied = value;
        if (EditObj is not null) OverlayRebuilt?.Invoke(EditObj);
        UpdateTools();
        if (!quiet)
        {
            Status = value ? "透過表示: オン（裏側も選択できる）" : "透過表示: オフ";
            Emit("xray", ("on", value));
        }
    }

    public void SetStats(bool? on = null, bool quiet = false)
    {
        var value = on ?? !ShowStats;
        ShowStats = value;
        UpdateStats();
        UpdateTools();
        if (!quiet) Emit("stats", ("on", value));
    }

    /// <summary>アクティブなオブジェクトに X 方向のミラーを付ける / 外す(ミラー モディファイアーの代わり)。</summary>
    public void SetMirror(bool? on = null, SceneObject? obj = null)
    {
        obj ??= Active;
        if (obj is null) { Status = "先にオブジェクトを選択してください"; return; }
        var value = on ?? !obj.Mirror;
        obj.Mirror = value;
        UpdateTools();
        Status = value ? $"{obj.Name} にミラーを追加（X 方向）" : "ミラーを外しました";
        Emit("mirror", ("on", value), ("object", obj.Name));
    }

    /// <summary>画面上部のボタン(data-tool)を押した。</summary>
    public void ClickTool(string tool)
    {
        if (Op is not null || LoopCut is not null) return;
        if (tool is "vert" or "edge" or "face") SetSelectMode(tool);
        else if (tool == "xray") SetXray();
        else if (tool == "stats") SetStats();
        else if (tool == "mirror") SetMirror();
        else return;
        Focused = true;
    }

    // ---------------------------------------------------------------- モード
    /// <summary>"object" / "edit"。quiet ならイベントを出さない(内部の切り替え用)。</summary>
    public bool SetMode(string mode, bool quiet = false)
    {
        if (mode == Mode) return true;
        if (mode == "edit")
        {
            var obj = Active;
            if (obj is null)
            {
                if (!quiet)
                {
                    Status = "先にオブジェクトを選択してください";
                    Emit("opfail", ("kind", "Tab"), ("reason", "noselection"));
                }
                return false;
            }
            Mode = "edit";
            EditObj = obj;
            if (!Selected.Contains(obj)) Selected.Add(obj);
            obj.Em.Flush(SelectMode);
            OverlayRebuilt?.Invoke(obj);
        }
        else
        {
            if (EditObj is not null) OverlayRemoved?.Invoke(EditObj);
            Mode = "object";
            EditObj = null;
        }
        ModeLabel = Mode == "edit" ? "編集モード" : "オブジェクトモード";
        RefreshSelection();
        UpdateTools();
        UpdateStats();
        if (!quiet)
        {
            Status = Mode == "edit" ? $"編集モード（{SelectModeLabels[SelectMode]}選択）。1 / 2 / 3 で頂点・辺・面" : "オブジェクトモード";
            Emit("mode", ("mode", Mode), ("object", (EditObj ?? Active)?.Name));
        }
        return true;
    }

    private bool ToggleEditMode() => SetMode(Mode != "edit" ? "edit" : "object");

    public void SetSelectMode(string mode)
    {
        if (Mode != "edit") return;
        SelectMode = mode;
        EditObj!.Em.Flush(mode);
        OverlayRebuilt?.Invoke(EditObj);
        UpdateTools();
        UpdateStats();
        Status = $"{SelectModeLabels[mode]}選択モード";
        Emit("selectmode", ("mode", mode));
    }

    // ---------------------------------------------------------------- 選択(オブジェクト)
    /// <summary>アウトライン(選択したオブジェクトの縁取り)を出すか。編集中のオブジェクトは出さない。</summary>
    public bool OutlineVisible(SceneObject o) => Selected.Contains(o) && o != EditObj;

    /// <summary>アクティブなオブジェクトの縁取りは明るい色。</summary>
    public bool OutlineIsActive(SceneObject o) => o == Active;

    private void RefreshSelection()
    {
        UpdateNPanel();
        UpdateStats();
        UpdateTools();
    }

    private void Select(SceneObject? obj, bool extend)
    {
        if (!extend) Selected = [];
        if (obj is not null)
        {
            if (extend && Selected.Contains(obj))
            {
                Selected = [.. Selected.Where(o => o != obj)];
                Active = Selected.Count > 0 ? Selected[^1] : null;
            }
            else
            {
                Selected.Add(obj);
                Active = obj;
            }
        }
        else if (!extend)
        {
            Active = null;
        }
        RefreshSelection();
    }

    private void EmitSelect(string source)
    {
        var detail = new List<(string, object?)> { ("source", source), ("selected", Selected.Select(o => o.Name).ToArray()), ("active", Active?.Name) };
        if (Mode == "edit")
        {
            var c = EditObj!.Em.Counts();
            detail.Add(("edit", true));
            detail.Add(("counts", new Dictionary<string, object?> { ["verts"] = c.Verts, ["edges"] = c.Edges, ["faces"] = c.Faces }));
            detail.Add(("selectMode", SelectMode));
        }
        Emit("select", [.. detail]);
    }

    private void SelectAll(bool all)
    {
        if (Mode == "edit")
        {
            EditObj!.Em.SelectAll(SelectMode, all);
            EditChanged();
        }
        else if (all)
        {
            Selected = [.. Objects];
            Active = Selected.Count > 0 ? Selected[^1] : null;
        }
        else
        {
            Selected = [];
            Active = null;
        }
        RefreshSelection();
        Status = all ? "全選択" : "選択解除";
        EmitSelect(all ? "all" : "none");
    }

    private void InvertSelection()
    {
        if (Mode == "edit")
        {
            EditObj!.Em.Invert(SelectMode);
            EditChanged();
        }
        else
        {
            Selected = [.. Objects.Where(o => !Selected.Contains(o))];
            Active = Selected.Count > 0 ? Selected[^1] : null;
            RefreshSelection();
        }
        Status = "選択を反転";
        EmitSelect("invert");
    }

    // ---------------------------------------------------------------- 追加・複製・削除
    private void AddPrimitive(string kind)
    {
        var p = Primitives.First(x => x.Key == kind);
        PushUndo();
        var m = AddObject(kind);
        SetXray(Xray, true);
        Select(m, false);
        Status = $"{p.Label}を追加";
        Emit("add", ("kind", kind), ("name", m.Name));
    }

    private void Duplicate()
    {
        if (Selected.Count == 0) return;
        PushUndo();
        var copies = Selected.ToList().Select(o =>
        {
            var m = AddObject(o.Kind, o.Position, o.Em.ToData());
            m.Quaternion = o.Quaternion;
            m.Scale = o.Scale;
            m.Mirror = o.Mirror;
            Rebuild(m);
            return m;
        }).ToList();
        SetXray(Xray, true);
        Selected = copies;
        Active = copies[^1];
        RefreshSelection();
        Emit("duplicate", ("names", copies.Select(o => o.Name).ToArray()));
        StartOp(TransformKind.G);
        // 複製と移動を 1 回の取り消しにまとめる。キャンセルしても複製は元の位置に残る(Blender と同じ)。
        // 現行は、StartOp が動かせない理由(共同練習のロック)で始められないとここで例外になる(複製は選択し直した新しいオブジェクトなので、実際には起きない)。C# は例外にしない
        if (Op is null) return;
        PopUndo();
        Op.FromDuplicate = true;
        Status = "複製。マウスで位置を決めてクリック";
    }

    private void DeleteSelected()
    {
        if (Selected.Count == 0) return;
        PushUndo();
        var names = Selected.Select(o => o.Name).ToArray();
        foreach (var o in Selected.ToList()) RemoveObject(o);
        RefreshSelection();
        Status = "削除";
        Emit("delete", ("names", names));
    }

    // ---------------------------------------------------------------- N パネル
    private void ToggleNPanel(bool? open = null)
    {
        var value = open ?? npanelHidden;
        npanelHidden = !value;
        UpdateNPanel();
        Emit("npanel", ("open", value));
    }

    private void UpdateNPanel()
    {
        if (npanelHidden) return;
        var o = Active;
        npanelName = o is not null ? o.Name : "—";
        for (var i = 0; i < npanelRows.Count; i++)
        {
            var (prop, axis, _, _) = npanelRows[i];
            if (o is null) { npanelRows[i] = (prop, axis, "", true); continue; }
            var v = prop switch { "position" => o.Position.Get(axis), "rotation" => o.Rotation.Get(axis), _ => o.Scale.Get(axis) };
            npanelRows[i] = (prop, axis, prop == "rotation" ? Js.ToFixed(v / Js.Deg, 1) : Js.ToFixed(v, 3), false);
        }
    }

    /// <summary>N パネルの入力欄を書き換えて確定した(change)。value は入力欄の文字。数にならなければ何もしない。</summary>
    public void ChangeNPanel(string prop, string axis, string value)
    {
        var index = npanelRows.FindIndex(r => r.Prop == prop && r.Axis == axis);
        if (index >= 0) npanelRows[index] = (prop, axis, value, npanelRows[index].Disabled);
        var o = Active;
        if (o is null) return;
        var v = Js.ParseFloat(value);
        if (double.IsNaN(v)) return;
        PushUndo();
        if (prop == "position") o.Position = o.Position.With(axis, v);
        else if (prop == "scale") o.Scale = o.Scale.With(axis, v);
        else
        {
            var e = o.Rotation.With(axis, v * Js.Deg);
            o.SetRotation(e.X, e.Y, e.Z);
        }
        Emit("edit", ("prop", prop), ("axis", axis), ("value", v));
    }

    // ---------------------------------------------------------------- 共同練習(room.js)の口
    /// <summary>部屋で共有する形。編集モードで変えたメッシュの形は含まない。回転はラジアン、値は小数 4 桁に丸める。</summary>
    public IReadOnlyList<RoomObject> RoomState()
    {
        static double R(double n) => Js.Round(n * 10000) / 10000;
        static double[] A(Vec3 v) => [R(v.X), R(v.Y), R(v.Z)];
        return [.. Objects.Select(o => new RoomObject(o.Rid, o.Kind, o.Name, A(o.Position), A(o.Rotation), A(o.Scale)))];
    }

    /// <summary>ほかの人の変更を反映する(イベントは出さない)。upsert は追加か変形、delete は削除。取り消しの記録は、ほかの人の変更と食い違うので消す。</summary>
    public void ApplyRemote(IReadOnlyList<RoomObject> upsert, IReadOnlyList<string>? delete = null, bool keepUndo = false)
    {
        delete ??= [];
        var wasSilent = silent;
        silent = true;
        foreach (var d in upsert)
        {
            var o = Objects.FirstOrDefault(x => x.Rid == d.Id);
            if (o is null)
            {
                o = AddObject(d.Kind);
                o.Rid = d.Id;
            }
            o.Name = d.Name;
            o.Position = new Vec3(d.P[0], d.P[1], d.P[2]);
            o.SetRotation(d.R[0], d.R[1], d.R[2]);
            o.Scale = new Vec3(d.S[0], d.S[1], d.S[2]);
        }
        foreach (var id in delete)
        {
            var o = Objects.FirstOrDefault(x => x.Rid == id);
            if (o is not null) RemoveObject(o);
        }
        if (upsert.Count > 0) SetXray(Xray, true);
        if (!keepUndo && (upsert.Count > 0 || delete.Count > 0)) { undoStack.Clear(); redoStack = []; }
        RefreshSelection();
        silent = wasSilent;
    }

    /// <summary>部屋に入ったとき: シーンを丸ごと置き換える。</summary>
    public void LoadRoomScene(IReadOnlyList<RoomObject> objects)
    {
        var wasSilent = silent;
        silent = true;
        ResetScene([]);
        ApplyRemote(objects);
        silent = wasSilent;
    }

    // ---------------------------------------------------------------- 編集中のメッシュの情報
    /// <summary>
    /// チュートリアルとお題の判定用: 編集中(なければアクティブ)のメッシュの、選んだ面(ワールド座標の中心と法線)と、全頂点の位置と、数。
    /// </summary>
    public EditInfo GetEditInfo()
    {
        var obj = EditObj ?? Active;
        if (obj is null) return new EditInfo([], [], null);
        var em = obj.Em;
        var m = obj.MatrixWorld;
        return new EditInfo(
            [.. em.Fsel.Select(f => new EditFace(m.ApplyToPoint(EditMesh.FaceCenter(f)), m.ApplyNormalMatrix(EditMesh.FaceNormal(f)).Normalized(), f.V.Count, EditMesh.FaceArea(f)))],
            [.. em.Verts.Select(v => WorldOf(v, obj))],
            em.Stats());
    }

    /// <summary>お題の判定に渡す、現在の状態(practice.js の checkTasks が作る ctx)。</summary>
    public TaskContext GetTaskContext(TaskFlags flags) => new(
        Objects, Selected, View.Name, View.Ortho, Mode, SelectMode,
        Mode == "edit" ? [.. GetEditInfo().Faces.Select(f => f.Normal)] : [], flags);

    private Vec3 WorldOf(EmVert v, SceneObject obj) => obj.MatrixWorld.ApplyToPoint(v.Co);
}
