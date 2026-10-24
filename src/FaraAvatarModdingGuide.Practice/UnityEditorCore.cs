namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// 案内中の場所(unity.js の highlight)。Hierarchy は名前、Project はアセット、Inspector は "active" | "tag" | "add-component" | "material-slot-0" | "p-0" など、
/// Menu は "VRChat SDK"、Sdk は "build-test"、Tool は "move"、Popup は "setup-outfit" | "sdk-panel"、Scene は Scene ビュー全体。Label は吹き出しの文字。
/// </summary>
public sealed record UnityHighlight(string? Hierarchy = null, string? Project = null, string? Inspector = null, string? Menu = null, string? Sdk = null, string? Tool = null, string? Popup = null, bool Scene = false, string? Label = null);

/// <summary>ポップアップをどこに出すか。Point はクライアント座標、Below は要素(Selector)の左下、Row は Hierarchy の行(Selector は data-node の値)の左 + 40 と下。画面側(JavaScript)が、そのときの実際の位置・大きさで決める。</summary>
public sealed record UnityAnchor(string Kind, double X = 0, double Y = 0, string? Selector = null);

/// <summary>開いているポップアップ(右クリックのメニュー・Add Component・上のメニュー)。</summary>
public sealed class UnityPopup
{
    /// <summary>"context" | "add" | "menu"。</summary>
    public required string Kind { get; init; }
    public string? NodeId { get; init; }
    public string? MenuName { get; init; }
    public required UnityAnchor Anchor { get; init; }
    /// <summary>Add Component の検索の文字。</summary>
    public string Search { get; set; } = "";
}

/// <summary>Build &amp; Test / Build &amp; Publish のあいだの表示(Scene ビューの上に出る)。</summary>
public sealed record UnityPreview(string Text, bool Busy);

/// <summary>VRChat SDK の集計(unity.js の stats())。</summary>
public sealed class UnityStats
{
    public required UnityNode Avatar { get; init; }
    public required IReadOnlyDictionary<string, int> Values { get; init; }
    public required IReadOnlyDictionary<string, int> Ranks { get; init; }
    public required int Overall { get; init; }
    public required IReadOnlyList<(string Level, string Text)> Notes { get; init; }
    public required HashSet<UnityNode> Merged { get; init; }
}

/// <summary>UnityEditorCore を作るときの設定(unity.js の options に当たる)。</summary>
public sealed class UnityOptions
{
    /// <summary>最初に置く Prefab(既定は Avatar 1 つ)。</summary>
    public IReadOnlyList<string>? Instances { get; init; }
    /// <summary>true ならフォーカス中だけホイールでズームする(記事内でページのスクロールを邪魔しない)。</summary>
    public bool WheelNeedsFocus { get; init; }
    /// <summary>ms ミリ秒後に action を呼ぶ(setTimeout)。戻り値を Dispose すると取り消す。省略するとタイマーは動かさない。</summary>
    public Func<int, Action, IDisposable>? Schedule { get; init; }
    /// <summary>いまの時刻(ミリ秒。performance.now)。腕を振るポーズに使う。</summary>
    public Func<double>? Now { get; init; }
}

/// <summary>
/// Unity エディターに似た練習用の画面(unity.js)の操作ロジック。描画(WebGL)も DOM も持たない: 画面に出すものは状態として読み(<see cref="Nodes"/>・<see cref="Selected"/>・
/// <see cref="Tool"/>・<see cref="Popup"/>・<see cref="Status"/>・<see cref="Preview"/>・SDK の集計など)、状態が変わると <see cref="Changed"/> が起きる。
/// 操作ごとのイベントは <see cref="EventEmitted"/>(viewport.js と同じ <see cref="ViewEvent"/>)。
/// 3D の部分は three.js の Object3D と同じ手順(<see cref="Obj3D"/>。ワールド行列を更新するのは「描いたとき」だけ)で持ち、レイ判定・Frame Selected・ギズモのドラッグが、現行と同じ値になるようにしてある。
/// 現行の関数との対応: メソッド名は unity.js の同名のメソッド(先頭が大文字)。ポインター・ホイール・キーは bind() のハンドラー。
/// </summary>
public sealed partial class UnityEditorCore
{
    private static int nextId = 1;
    private static string NewId() => $"n{Interlocked.Increment(ref nextId) - 1}";

    private readonly UnityOptions options;
    private readonly OrderedDictionary<string, UnityNode> nodes = [];
    private readonly List<string> roots = [];
    private readonly List<UnitySnapshot> undoStack = [];
    private IDisposable? previewTimer;

    public UnityEditorCore(UnityOptions? options = null)
    {
        this.options = options ?? new UnityOptions();
        View = new UnityView();
        Camera = new UnityCamera();
        ResetScene(this.options.Instances);
    }

    // ---------------------------------------------------------------- 公開する状態・イベント
    /// <summary>操作のイベント(select・expand・instantiate・reparent・delete・contextmenu・setupOutfit・active・tag・material・componentAdded・edit・tool・transform・view・menu・sdk・build・undo・key)。</summary>
    public event Action<ViewEvent>? EventEmitted;

    /// <summary>画面に出す状態が変わった(Razor が描き直す)。</summary>
    public event Action? Changed;

    /// <summary>true のあいだはイベントを出さない(チュートリアルの再生)。</summary>
    public bool Silent { get; set; }

    public IReadOnlyCollection<UnityNode> Nodes => nodes.Values;
    public IReadOnlyList<string> Roots => roots;
    public string? Selected { get; private set; }
    public string Tool { get; private set; } = "move";
    public string Status { get; private set; } = "";
    public UnityHighlight Highlight { get; private set; } = new();
    public UnityPopup? Popup { get; private set; }
    public UnityPreview? Preview { get; private set; }
    public bool SdkOpen { get; private set; }
    public UnityView View { get; }
    public UnityCamera Camera { get; }
    /// <summary>エディター(Hierarchy・Inspector など全体)の中にフォーカスがあるか。画面側が focusin / focusout で決める(wheelNeedsFocus に使う)。</summary>
    public bool Focused { get; set; }
    /// <summary>Scene ビューの上の「Move Tool」などの文字(毎フレームの <see cref="Animate"/> で決まる)。</summary>
    public string InfoText { get; private set; } = "";
    public bool Posing => pose is not null;

    private void Touch() => Changed?.Invoke();

    private void Emit(string type, params (string Key, object? Value)[] detail)
    {
        if (Silent) return;
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in detail) d[k] = v;
        EventEmitted?.Invoke(new ViewEvent(type, d));
    }

    public void SetStatus(string text)
    {
        Status = text;
        Touch();
    }

    // ---------------------------------------------------------------- シーン(ノード)
    private sealed record UnitySnapshot(List<UnityNode> Nodes, List<string> Roots, string? Selected);

    /// <summary>unity.js の resetScene。</summary>
    public void ResetScene(IReadOnlyList<string>? instances = null)
    {
        instances ??= options.Instances ?? ["Avatar"];
        ClosePopup();
        CloseSdk(true);
        StopPreview();
        nodes.Clear();
        roots.Clear();
        foreach (var n in DefaultSceneNodes()) AddNode(n);
        foreach (var name in instances) AddNodes(Prefab(name)!);
        Selected = null;
        undoStack.Clear();
        Tool = "move";
        View.Reset();
        Rebuild();
        Render();
        SetStatus("Hierarchy や Scene でクリックして選択。Project からドラッグして追加");
    }

    private static UnityNode MakeNode(string name, string? parent, Action<UnityNode>? extra = null)
    {
        var n = new UnityNode { Id = NewId(), Name = name, Parent = parent };
        extra?.Invoke(n);
        return n;
    }

    private static UnityNode ArmatureNodes(List<UnityNode> list, string parentId, IEnumerable<string> boneNames)
    {
        var arm = MakeNode("Armature", parentId);
        list.Add(arm);
        var byName = new Dictionary<string, UnityNode>();
        var allowed = boneNames.ToHashSet();
        foreach (var (name, parent, p) in UnityData.Bones)
        {
            if (!allowed.Contains(name)) continue;
            var n = MakeNode(name, parent is null ? arm.Id : byName[parent].Id, x => { x.P = [.. p]; x.Bone = true; });
            byName[name] = n;
            list.Add(n);
        }
        return arm;
    }

    private static UnityComponent Smr(string[] materials, string[] blendshapes, int tris) => new() { Type = "SkinnedMeshRenderer", Materials = [.. materials], Blendshapes = [.. blendshapes], Tris = tris };

    /// <summary>Prefab(Avatar・Hoodie)のノードの一覧(先頭がルート)。新しい ID を振る。</summary>
    public static List<UnityNode>? Prefab(string name)
    {
        var list = new List<UnityNode>();
        if (name == "Avatar")
        {
            var root = MakeNode("Avatar", null, n =>
            {
                n.Prefab = "Avatar";
                n.Expanded = false;
                n.Components =
                [
                    new() { Type = "Animator", Avatar = "AvatarAvatar（Humanoid）" },
                    new() { Type = "VRCAvatarDescriptor", View = [0, 1.52, 0.07] },
                    new() { Type = "PipelineManager", BlueprintId = "" },
                ];
            });
            list.Add(root);
            ArmatureNodes(list, root.Id, UnityData.Bones.Select(b => b.Name));
            list.Add(MakeNode("Body", root.Id, n => { n.Mesh = "body"; n.Components = [Smr(["Body_lilToon", "Face_lilToon"], ["Breast_big", "Shrink_Upper"], 22000)]; }));
            list.Add(MakeNode("Shirt_Default", root.Id, n => { n.Mesh = "shirt"; n.Components = [Smr(["Shirt_Default"], [], 4800)]; }));
            return list;
        }
        if (name == "Hoodie")
        {
            var root = MakeNode("Hoodie", null, n => n.Prefab = "Hoodie");
            list.Add(root);
            ArmatureNodes(list, root.Id, UnityData.OutfitBones);
            list.Add(MakeNode("Hoodie_Mesh", root.Id, n => { n.Mesh = "hoodie"; n.Components = [Smr(["Hoodie_Standard"], ["Breast_big"], 9600)]; }));
            return list;
        }
        return null;
    }

    /// <summary>Unity の新しいシーンと同じく、カメラとライトを置く。</summary>
    private static List<UnityNode> DefaultSceneNodes() =>
    [
        MakeNode("Main Camera", null, n => { n.P = [0, 1, -10]; n.Icon = "camera"; n.Components = [new() { Type = "Camera" }]; }),
        MakeNode("Directional Light", null, n => { n.P = [0, 3, 0]; n.R = [50, -30, 0]; n.Icon = "light"; n.Components = [new() { Type = "Light" }]; }),
    ];

    private void AddNode(UnityNode n)
    {
        nodes[n.Id] = n;
        if (n.Parent is not null) nodes[n.Parent].Children.Add(n.Id);
        else roots.Add(n.Id);
    }

    private UnityNode AddNodes(List<UnityNode> list)
    {
        foreach (var n in list) AddNode(n);
        return list[0];
    }

    public UnityNode? Get(string? id) => id is not null && nodes.TryGetValue(id, out var n) ? n : null;

    /// <summary>名前で探す(上から順に最初に見つかったもの)。"Hoodie/Armature" のように親の名前で絞れる。</summary>
    public UnityNode? FindByName(string path)
    {
        var parts = path.Split('/');
        UnityNode? Walk(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                var n = Get(id)!;
                if (n.Name == parts[^1] && PathMatches(n, parts)) return n;
                var c = Walk(n.Children);
                if (c is not null) return c;
            }
            return null;
        }
        return Walk(roots);
    }

    private bool PathMatches(UnityNode n, string[] parts)
    {
        var p = Get(n.Parent);
        for (var i = parts.Length - 2; i >= 0; i--, p = p is null ? null : Get(p.Parent))
            if (p is null || p.Name != parts[i]) return false;
        return true;
    }

    public List<UnityNode> Ancestors(UnityNode n)
    {
        var list = new List<UnityNode>();
        for (var p = Get(n.Parent); p is not null; p = Get(p.Parent)) list.Add(p);
        return list;
    }

    /// <summary>アバターのルート(VRC Avatar Descriptor を持つもの)。</summary>
    public UnityNode? AvatarRootOf(UnityNode n) => new[] { n }.Concat(Ancestors(n)).FirstOrDefault(x => x.Components.Any(c => c.Type == "VRCAvatarDescriptor"));

    public bool IsEffectivelyActive(UnityNode n) => n.Active && Ancestors(n).All(a => a.Active);

    private UnitySnapshot Snapshot() => new([.. nodes.Values.Select(n => n.Clone())], [.. roots], Selected);

    private void PushUndo()
    {
        undoStack.Add(Snapshot());
        if (undoStack.Count > 50) undoStack.RemoveAt(0);
    }

    public int UndoDepth => undoStack.Count;

    public void Undo()
    {
        if (undoStack.Count == 0) { SetStatus("これ以上元に戻せません"); return; }
        var s = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        nodes.Clear();
        foreach (var n in s.Nodes) nodes[n.Id] = n;
        roots.Clear();
        roots.AddRange(s.Roots);
        Selected = s.Selected is not null && nodes.ContainsKey(s.Selected) ? s.Selected : null;
        Rebuild();
        Render();
        SetStatus("元に戻す（Undo）");
        Emit("undo");
    }

    // ---------------------------------------------------------------- 3D(ノードから作り直す)
    /// <summary>3D の世界(three.js の world グループ)。</summary>
    public Obj3D World { get; } = new("world");

    /// <summary>unity.js の rebuild()。</summary>
    private void Rebuild()
    {
        World.Clear();
        void Build(string id, Obj3D parentObj)
        {
            var n = Get(id)!;
            var g = new Obj3D(n.Name);
            ApplyTransform(n, g);
            parentObj.Add(g);
            n.Obj = g;
            foreach (var c in n.Children) Build(c, g);
        }
        foreach (var id in roots) Build(id, World);
        foreach (var n in nodes.Values)
        {
            if (n.Mesh is null) continue;
            var comp = n.Smr;
            var color = UnityData.Asset(comp?.Materials is { Count: > 0 } m ? m[0] : null)?.Color ?? 0xff00ff;
            n.Parts = [];
            foreach (var part in UnityData.MeshParts[n.Mesh])
            {
                var b = BoneFor(n, part.Bone);
                if (b is null) continue;
                var mesh = new Obj3D(part.Shape);
                mesh.Position = new Vec3(part.Pos[0], part.Pos[1], part.Pos[2]);
                if (part.Axis == "x") mesh.SetRotationZ(Math.PI / 2);
                b.Obj!.Add(mesh);
                n.Parts.Add(new UnityPart
                {
                    Mesh = mesh,
                    Geometry = UnityData.PartMesh(part.Shape, part.Size),
                    Shape = part.Shape,
                    NodeId = n.Id,
                    Color = part.Shape == "eye" ? 0x2b2b3a : part.Shape == "hair" ? 0x5b3b2b : color,
                    DoubleSided = part.Shape is "hood" or "hair",
                });
            }
        }
        UpdateVisibility();
        UpdateGizmo();
    }

    /// <summary>メッシュが使うボーン: 同じ親の下の Armature から名前で探す。</summary>
    private UnityNode? BoneFor(UnityNode meshNode, string boneName)
    {
        var parent = Get(meshNode.Parent);
        var armId = parent?.Children.FirstOrDefault(id => Get(id)!.Name == "Armature");
        if (armId is null) return null;
        UnityNode? Walk(string id)
        {
            var n = Get(id)!;
            if (n.Name == boneName && n.Bone) return n;
            foreach (var c in n.Children) { var r = Walk(c); if (r is not null) return r; }
            return null;
        }
        return Walk(armId);
    }

    private static void ApplyTransform(UnityNode n, Obj3D obj)
    {
        obj.Position = new Vec3(n.P[0], n.P[1], n.P[2]);
        obj.SetRotation(n.R[0] * Js.Deg, n.R[1] * Js.Deg, n.R[2] * Js.Deg, EulerOrder.YXZ);
        obj.Scale = new Vec3(n.S[0], n.S[1], n.S[2]);
    }

    private static void ReadTransform(UnityNode n)
    {
        static double R(double v) => Js.Round(v * 1000) / 1000;
        var o = n.Obj!;
        n.P = [R(o.Position.X), R(o.Position.Y), R(o.Position.Z)];
        var e = UnityMath.EulerFromQuat(o.Quaternion, EulerOrder.YXZ);
        n.R = [Js.Round(e.X / Js.Deg * 100) / 100, Js.Round(e.Y / Js.Deg * 100) / 100, Js.Round(e.Z / Js.Deg * 100) / 100];
        n.S = [R(o.Scale.X), R(o.Scale.Y), R(o.Scale.Z)];
    }

    private void UpdateVisibility()
    {
        foreach (var n in nodes.Values)
        {
            if (n.Parts is null) continue;
            var on = IsEffectivelyActive(n);
            foreach (var p in n.Parts) p.Mesh.Visible = on;
        }
    }

    // ---------------------------------------------------------------- 操作(チュートリアルのスキップからも呼ぶ)
    public void Select(string? id, string source = "hierarchy")
    {
        Selected = id;
        var n = Get(id);
        if (n is not null) foreach (var a in Ancestors(n)) a.Expanded = true;
        ClosePopup();
        Render();
        UpdateGizmo();
        Emit("select", ("id", id), ("name", n?.Name), ("source", source));
    }

    public void SelectByName(string path)
    {
        var n = FindByName(path);
        if (n is not null) Select(n.Id);
    }

    public void SetExpanded(string id, bool expanded)
    {
        var n = Get(id);
        if (n is null || n.Children.Count == 0) return;
        n.Expanded = expanded;
        Touch();
        Emit("expand", ("id", id), ("name", n.Name), ("expanded", expanded));
    }

    public void ExpandByName(string path, bool expanded = true)
    {
        var n = FindByName(path);
        if (n is not null) SetExpanded(n.Id, expanded);
    }

    /// <summary>Prefab を置く(parentId を省くとルート)。</summary>
    public UnityNode? Instantiate(string asset, string? parentId = null)
    {
        var list = Prefab(asset);
        if (list is null) return null;
        PushUndo();
        var root = list[0];
        root.Parent = parentId;
        var siblings = (parentId is not null ? Get(parentId)!.Children : roots).Select(i => Get(i)!.Name).ToList();
        if (siblings.Contains(root.Name)) root.Name = $"{root.Name} ({siblings.Count(s => s.StartsWith(root.Name, StringComparison.Ordinal))})";
        AddNodes(list);
        if (parentId is not null) Get(parentId)!.Expanded = true;
        Rebuild();
        Select(root.Id);
        var parent = Get(parentId);
        SetStatus($"{root.Name} を{(parent is not null ? $"「{parent.Name}」の中に" : "")}追加しました");
        Emit("instantiate", ("asset", asset), ("id", root.Id), ("name", root.Name), ("parent", parentId), ("parentName", parent?.Name));
        return root;
    }

    /// <summary>親を変える(ワールドでの位置はそのまま。Unity と同じ)。</summary>
    public void Reparent(string id, string? parentId)
    {
        var n = Get(id);
        if (n is null || n.Parent == parentId || id == parentId) return;
        if (parentId is not null && new[] { Get(parentId)! }.Concat(Ancestors(Get(parentId)!)).Any(a => a.Id == id))
        {
            SetStatus("自分の子の中には入れられません");
            return;
        }
        PushUndo();
        var list = n.Parent is not null ? Get(n.Parent)!.Children : roots;
        list.Remove(id);
        n.Parent = parentId;
        (parentId is not null ? Get(parentId)!.Children : roots).Add(id);
        (parentId is not null ? Get(parentId)!.Obj! : World).Attach(n.Obj!);
        ReadTransform(n);
        if (parentId is not null) Get(parentId)!.Expanded = true;
        Rebuild();
        Render();
        var parent = Get(parentId);
        SetStatus($"{n.Name} を{(parent is not null ? $"「{parent.Name}」の子に" : "ルートに")}移しました");
        Emit("reparent", ("id", id), ("name", n.Name), ("parent", parentId), ("parentName", parent?.Name));
    }

    public void DeleteNode(string id)
    {
        var n = Get(id);
        if (n is null) return;
        PushUndo();
        void Drop(string x) { foreach (var c in Get(x)!.Children) Drop(c); nodes.Remove(x); }
        var list = n.Parent is not null ? Get(n.Parent)!.Children : roots;
        list.Remove(id);
        Drop(id);
        if (Selected is null || !nodes.ContainsKey(Selected)) Selected = null;
        Rebuild();
        Render();
        SetStatus($"{n.Name} を削除しました");
        Emit("delete", ("name", n.Name));
    }

    public void SetActive(string id, bool active)
    {
        var n = Get(id);
        if (n is null || n.Active == active) return;
        PushUndo();
        n.Active = active;
        UpdateVisibility();
        Render();
        SetStatus($"{n.Name} を{(active ? "有効" : "無効")}にしました");
        Emit("active", ("id", id), ("name", n.Name), ("active", active));
    }

    public void SetTag(string id, string tag)
    {
        var n = Get(id);
        if (n is null || n.Tag == tag) return;
        PushUndo();
        n.Tag = tag;
        Render();
        SetStatus(tag == "EditorOnly" ? $"{n.Name} はアップロードに含まれなくなります（EditorOnly）" : $"Tag: {tag}");
        Emit("tag", ("id", id), ("name", n.Name), ("tag", tag));
    }

    public void AssignMaterial(string? id, int slot, string material)
    {
        var n = Get(id);
        var comp = n?.Smr;
        if (n is null || comp is null || UnityData.Asset(material)?.Type != "material") return;
        PushUndo();
        // JavaScript の配列は、範囲の外の番号に代入すると広がる(unity.js のとおり)
        while (comp.Materials!.Count <= slot) comp.Materials.Add("");
        comp.Materials[slot] = material;
        Rebuild();
        Render();
        SetStatus($"{n.Name} の Element {slot} に {material} を設定しました");
        Emit("material", ("id", id), ("name", n.Name), ("slot", slot), ("material", material));
    }

    public void AddComponent(string? id, string type)
    {
        var n = Get(id);
        if (n is null) return;
        if (n.Components.Any(c => c.Type == type)) { SetStatus($"{UnityData.ComponentLabel(type)} はもう付いています"); return; }
        PushUndo();
        var comp = new UnityComponent { Type = type };
        if (type == "MABlendshapeSync")
        {
            // 同じ名前のブレンドシェイプを、アバターの Body から同期する
            var own = n.Smr?.Blendshapes ?? [];
            var avatar = AvatarRootOf(n);
            var body = avatar is not null ? FindByName($"{avatar.Name}/Body") : null;
            var src = body?.Smr?.Blendshapes ?? [];
            comp.Bindings = [.. own.Where(b => src.Contains(b)).Select(b => ("Body", b))];
        }
        if (type == "VRCPhysBone") { comp.Root = n.Name; comp.Pull = 0.2; comp.Spring = 0.2; }
        n.Components.Add(comp);
        ClosePopup();
        Render();
        SetStatus($"{n.Name} に {UnityData.ComponentLabel(type)} を追加しました");
        Emit("componentAdded", ("id", id), ("name", n.Name), ("component", type));
    }

    /// <summary>Modular Avatar &gt; Setup Outfit(衣装のアーマチュアに MA Merge Armature を付ける)。</summary>
    public void SetupOutfit(string? id)
    {
        var n = Get(id);
        ClosePopup();
        if (n is null) return;
        void Fail(string reason) { SetStatus(reason); Emit("setupOutfit", ("id", id), ("name", n.Name), ("ok", false), ("reason", reason)); }
        var avatar = AvatarRootOf(Get(n.Parent) ?? n);
        if (avatar is null || avatar.Id == n.Id) { Fail("衣装をアバターの中（子）に入れてから Setup Outfit を実行してください"); return; }
        var armId = n.Children.FirstOrDefault(c => Get(c)!.Name == "Armature");
        if (armId is null) { Fail($"{n.Name} の中に Armature が見つかりません（衣装の Prefab のルートで実行してください）"); return; }
        var target = FindByName($"{avatar.Name}/Armature");
        PushUndo();
        var arm = Get(armId)!;
        if (!arm.Components.Any(c => c.Type == "MAMergeArmature")) arm.Components.Add(new UnityComponent { Type = "MAMergeArmature", Target = $"{avatar.Name}/Armature", TargetId = target?.Id, Prefix = "", Suffix = "" });
        if (!n.Components.Any(c => c.Type == "MAMeshSettings")) n.Components.Add(new UnityComponent { Type = "MAMeshSettings" });
        Render();
        SetStatus($"Setup Outfit: {n.Name}/Armature に MA Merge Armature を追加しました");
        Emit("setupOutfit", ("id", id), ("name", n.Name), ("ok", true));
    }

    public void SetTool(string tool)
    {
        Tool = tool;
        UpdateGizmo();
        Touch();
        Emit("tool", ("tool", tool));
    }

    /// <summary>Inspector の Transform の値を変える(prop は "p" | "r" | "s"、axis は 0〜2)。fromInput は Inspector の欄からの入力。</summary>
    public void SetTransformValue(string? id, string prop, int axis, double value, bool fromInput = false)
    {
        var n = Get(id);
        if (n is null || !double.IsFinite(value)) return;
        PushUndo();
        var arr = prop switch { "p" => n.P, "r" => n.R, _ => n.S };
        arr[axis] = value;
        ApplyTransform(n, n.Obj!);
        UpdateGizmo();
        Touch();
        Emit("edit", ("id", id), ("name", n.Name), ("prop", prop), ("axis", axis), ("value", value));
    }

    public void FrameSelected()
    {
        var n = Get(Selected);
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        if (n?.Parts is { Count: > 0 } parts)
        {
            foreach (var p in parts) p.Mesh.ExpandBox(ref min, ref max, o => o == p.Mesh ? p.Geometry : null);
        }
        else if (n is not null)
        {
            n.Obj!.ExpandBox(ref min, ref max, o => PartGeometry(o));
        }
        var empty = max.X < min.X || max.Y < min.Y || max.Z < min.Z;
        if (empty)
        {
            if (n is null) { SetStatus("先に Hierarchy で選択してください"); return; }
            var c = n.Obj!.GetWorldPosition();
            min = c - new Vec3(0.25, 0.25, 0.25);
            max = c + new Vec3(0.25, 0.25, 0.25);
        }
        View.Target = (min + max) * 0.5;
        View.Dist = Math.Max(0.6, (max - min).Length * 1.4);
        SetStatus($"{n!.Name} を表示（Frame Selected）");
        Emit("view", ("action", "frame"));
    }

    private UnityMesh? PartGeometry(Obj3D o)
    {
        foreach (var n in nodes.Values)
            if (n.Parts is not null)
                foreach (var p in n.Parts)
                    if (p.Mesh == o) return p.Geometry;
        return null;
    }

    public void OrbitBy(double dyaw, double dpitch = 0)
    {
        View.Yaw += dyaw;
        View.Pitch = Math.Clamp(View.Pitch + dpitch, -1.4, 1.4);
    }

    // ---------------------------------------------------------------- VRChat SDK(シミュレーション)
    public void OpenSdk()
    {
        ClosePopup();
        SdkOpen = true;
        Touch();
        SetStatus("VRChat SDK > Show Control Panel");
        Emit("sdk", ("open", true));
    }

    public void CloseSdk(bool quiet = false)
    {
        if (!SdkOpen) return;
        SdkOpen = false;
        Touch();
        if (!quiet) Emit("sdk", ("open", false));
    }

    /// <summary>アップロードされるアバターの集計(EditorOnly は除く。無効なだけのものは含む = VRChat と同じ)。</summary>
    public UnityStats? Stats()
    {
        var avatar = roots.Select(id => Get(id)!).FirstOrDefault(n => n.Components.Any(c => c.Type == "VRCAvatarDescriptor"));
        if (avatar is null) return null;
        var included = new List<UnityNode>();
        void Walk(string id, bool editorOnly)
        {
            var n = Get(id)!;
            var eo = editorOnly || n.Tag == "EditorOnly";
            if (!eo) included.Add(n);
            foreach (var c in n.Children) Walk(c, eo);
        }
        Walk(avatar.Id, false);
        var smrs = included.Select(n => n.Smr).Where(c => c is not null).Select(c => c!).ToList();
        var merged = OutfitMerged(avatar);
        // 統合される衣装のボーンは、同じ名前のアバターのボーンにまとまるので数えない
        var bones = included.Count(n => n.Bone && !(ArmatureOf(n) is { } arm && merged.Contains(arm) && FindByName($"{avatar.Name}/Armature") is not null && AvatarBoneNamed(avatar, n.Name) is not null));
        var values = new Dictionary<string, int>
        {
            ["tris"] = smrs.Sum(c => c.Tris),
            ["materials"] = smrs.Sum(c => c.Materials!.Count),
            ["skinned"] = smrs.Count,
            ["bones"] = bones,
            ["physbones"] = included.Sum(n => n.Components.Count(c => c.Type == "VRCPhysBone")),
        };
        int RankOf(string k, int v) { var i = Array.FindIndex(UnityData.Limits[k], lim => v <= lim); return i < 0 ? 4 : i; }
        var ranks = values.ToDictionary(kv => kv.Key, kv => RankOf(kv.Key, kv.Value));
        var overall = ranks.Values.Max();
        var notes = new List<(string Level, string Text)>();
        foreach (var n in included)
        {
            if (n.Smr is not null && !IsEffectivelyActive(n)) notes.Add(("info", $"{n.Name} は無効ですが、アップロードに含まれ、ランクの計算にも入ります。外すなら Tag を EditorOnly にしてください。"));
            var mat = n.Smr?.Materials ?? [];
            if (mat.Any(m => UnityData.Asset(m)?.Shader == "Standard")) notes.Add(("info", $"{n.Name} のマテリアルが Standard シェーダーです（VRChat では lilToon などのトゥーンシェーダーがよく使われます）。"));
        }
        foreach (var outfit in OutfitsIn(avatar))
        {
            var armId = outfit.Children.FirstOrDefault(c => Get(c)!.Name == "Armature");
            if (!merged.Contains(Get(armId)!)) notes.Add(("warn", $"{outfit.Name} のボーンがアバターに統合されません。衣装を右クリック > Modular Avatar > Setup Outfit を実行してください。"));
        }
        return new UnityStats { Avatar = avatar, Values = values, Ranks = ranks, Overall = overall, Notes = notes, Merged = merged };
    }

    /// <summary>アバターの中の衣装(Armature を持つ、アバター以外の Prefab)。</summary>
    public List<UnityNode> OutfitsIn(UnityNode avatar)
    {
        var list = new List<UnityNode>();
        void Walk(string id)
        {
            var n = Get(id)!;
            if (n.Id != avatar.Id && n.Children.Any(c => Get(c)!.Name == "Armature")) list.Add(n);
            foreach (var c in n.Children) Walk(c);
        }
        Walk(avatar.Id);
        return list;
    }

    /// <summary>MA Merge Armature でアバターに統合される衣装のアーマチュア。</summary>
    public HashSet<UnityNode> OutfitMerged(UnityNode avatar)
    {
        var set = new HashSet<UnityNode>();
        foreach (var outfit in OutfitsIn(avatar))
        {
            var armId = outfit.Children.FirstOrDefault(c => Get(c)!.Name == "Armature");
            var arm = Get(armId);
            if (arm is not null && arm.Components.Any(c => c.Type == "MAMergeArmature") && IsEffectivelyActive(outfit)) set.Add(arm);
        }
        return set;
    }

    public UnityNode? ArmatureOf(UnityNode bone) => Ancestors(bone).FirstOrDefault(a => a.Name == "Armature");

    public UnityNode? AvatarBoneNamed(UnityNode avatar, string name)
    {
        var arm = FindByName($"{avatar.Name}/Armature");
        if (arm is null) return null;
        UnityNode? Walk(string id)
        {
            var n = Get(id)!;
            if (n.Bone && n.Name == name) return n;
            foreach (var c in n.Children) { var r = Walk(c); if (r is not null) return r; }
            return null;
        }
        return Walk(arm.Id);
    }

    /// <summary>Build &amp; Test / Build &amp; Publish(実際には何も送らない)。</summary>
    public void Build(string kind)
    {
        var st = Stats();
        if (st is null) { SetStatus("VRC Avatar Descriptor の付いたアバターがありません"); return; }
        CloseSdk(true);
        var label = kind == "test" ? "Build & Test" : "Build & Publish";
        ShowPreview($"{label}: ビルドしています…", true);
        previewTimer?.Dispose();
        previewTimer = options.Schedule?.Invoke(900, () =>
        {
            StartPose(st);
            var followText = st.Merged.Count > 0 ? "衣装はアバターと一緒に動いています（Modular Avatar で統合済み）" : "衣装が体についてきていません（Setup Outfit をしていない）";
            ShowPreview(kind == "test"
                ? $"テスト用に読み込みました（VRChat のアバターのメニューの SDK Test Avatars）。ランク: {UnityData.Ranks[st.Overall]}。{followText}"
                : $"アップロードしました（練習なので実際には送信していません）。ランク: {UnityData.Ranks[st.Overall]}");
            SetStatus($"{label} が完了しました（ランク: {UnityData.Ranks[st.Overall]}）");
            Emit("build", ("kind", kind), ("merged", st.Merged.Count > 0), ("rank", UnityData.Ranks[st.Overall]));
            previewTimer = options.Schedule?.Invoke(4500, StopPreview);
        });
    }

    private void ShowPreview(string text, bool busy = false)
    {
        Preview = new UnityPreview(text, busy);
        Touch();
    }

    private sealed record UnityPose(double Start, UnityNode Avatar, HashSet<UnityNode> Merged);
    private UnityPose? pose;

    /// <summary>腕を振るポーズ。統合された衣装のボーンはアバターのボーンについていく。</summary>
    private void StartPose(UnityStats st)
    {
        pose = new UnityPose(options.Now?.Invoke() ?? 0, st.Avatar, st.Merged);
        UpdateGizmo();
    }

    public void StopPreview()
    {
        previewTimer?.Dispose();
        previewTimer = null;
        if (Preview is not null) { Preview = null; Touch(); }
        if (pose is not null) { pose = null; Rebuild(); }
    }

    private void UpdatePose(double now)
    {
        var p = pose;
        if (p is null) return;
        var t = (now - p.Start) / 1000;
        var swing = Math.Sin(t * 3) * 0.6 + 0.5;
        void Set(string name, double z) { var b = AvatarBoneNamed(p.Avatar, name); b?.Obj!.SetRotationZ(z); }
        Set("LeftUpperArm", swing);
        Set("RightUpperArm", -swing);
        Set("LeftLowerArm", swing * 0.5);
        Set("RightLowerArm", -swing * 0.5);
        // MA Merge Armature: 衣装のボーンを、同じ名前のアバターのボーンのワールド変換に合わせる
        World.UpdateMatrixWorld(true);
        foreach (var arm in p.Merged)
        {
            void Walk(string id)
            {
                var n = Get(id)!;
                if (n.Bone)
                {
                    var src = AvatarBoneNamed(p.Avatar, n.Name);
                    if (src is not null)
                    {
                        var parentInv = n.Obj!.Parent!.MatrixWorld.Inverted();
                        var m = Mat4.Multiply(parentInv, src.Obj!.MatrixWorld);
                        var (pos, q, s) = UnityMath.Decompose(m);
                        n.Obj.Position = pos;
                        n.Obj.SetQuaternion(q);
                        n.Obj.Scale = s;
                        n.Obj.UpdateMatrixWorld(true);
                    }
                }
                foreach (var c in n.Children) Walk(c);
            }
            Walk(arm.Id);
        }
    }

    // ---------------------------------------------------------------- チュートリアル用のハイライト
    public void SetHighlight(UnityHighlight? h)
    {
        Highlight = h ?? new UnityHighlight();
        Touch();
    }

    // ---------------------------------------------------------------- 画面を作り直した合図(状態は変わらない)
    private void Render() => Touch();

    // ---------------------------------------------------------------- ポップアップ(メニュー・右クリック・Add Component)
    public void ClosePopup()
    {
        if (Popup is null) return;
        Popup = null;
        Touch();
    }

    public void OpenContextMenu(string id, UnityAnchor anchor)
    {
        var n = Get(id);
        if (n is null) return;
        Selected = id;
        Render();
        Popup = new UnityPopup { Kind = "context", NodeId = id, Anchor = anchor };
        Touch();
        Emit("contextmenu", ("id", id), ("name", n.Name));
    }

    /// <summary>チュートリアルのスキップ用: 名前のオブジェクトで右クリックメニューを開く。</summary>
    public void ContextMenuByName(string path)
    {
        var n = FindByName(path);
        if (n is null) return;
        OpenContextMenu(n.Id, new UnityAnchor("row", Selector: n.Id));
    }

    public void OpenAddComponent()
    {
        var n = Get(Selected);
        if (n is null) return;
        Popup = new UnityPopup { Kind = "add", Anchor = new UnityAnchor("below", Selector: ".ue-addcomp") };
        Touch();
        Emit("menu", ("menu", "add-component"));
    }

    public void OpenMenu(string name, UnityAnchor anchor)
    {
        Popup = new UnityPopup { Kind = "menu", MenuName = name, Anchor = anchor };
        Touch();
        Emit("menu", ("menu", name));
    }

    /// <summary>Add Component の検索の文字を変えた(一覧を絞り込む)。</summary>
    public void SetAddSearch(string text)
    {
        if (Popup is not { Kind: "add" } p) return;
        p.Search = text;
        Touch();
    }

    /// <summary>絞り込んだ Add Component の一覧。</summary>
    public IReadOnlyList<UnityComponentInfo> AddList()
    {
        var q = (Popup?.Search ?? "").Trim().ToLowerInvariant();
        return [.. UnityData.Components.Where(c => q.Length == 0 || c.Label.ToLowerInvariant().Contains(q) || c.Path.ToLowerInvariant().Contains(q))];
    }

    /// <summary>検索欄で Enter: 絞り込んだ先頭を追加する。</summary>
    public void AddFirstFromSearch()
    {
        var first = AddList().FirstOrDefault();
        if (first is not null) AddComponent(Selected, first.Type);
    }

    /// <summary>右クリックのメニューの項目。</summary>
    public void ContextItem(string item)
    {
        var id = Popup?.NodeId;
        if (item == "setup-outfit") SetupOutfit(id);
        else if (item == "delete") { ClosePopup(); if (id is not null) DeleteNode(id); }
    }

    // ---------------------------------------------------------------- ドラッグ(Hierarchy・Project → Hierarchy / Inspector / Scene)
    /// <summary>ドラッグせずに離した = クリック。</summary>
    public void ClickAsset(string asset)
    {
        var a = UnityData.Asset(asset)!;
        SetStatus($"{a.Label}: {a.Note ?? a.Shader ?? ""}（ドラッグして使う）");
    }

    /// <summary>ドロップ先: "node"(id)・"root"・"slot"(番号)・"inspector"・"scene"。x・y は Scene ビューの左上からのピクセル(scene のとき、マテリアルのメッシュ判定に使う)。</summary>
    public void DropNode(string payloadId, string targetKind, string? targetId)
    {
        if (targetKind == "node") Reparent(payloadId, targetId);
        else if (targetKind == "root") Reparent(payloadId, null);
    }

    public void DropAsset(string payloadAsset, string targetKind, string? targetId, int slot, double x, double y)
    {
        var asset = UnityData.Asset(payloadAsset)!;
        if (asset.Type == "prefab")
        {
            if (targetKind == "node") Instantiate(payloadAsset, targetId);
            else if (targetKind is "root" or "scene") Instantiate(payloadAsset, null);
            return;
        }
        if (asset.Type == "material")
        {
            if (targetKind == "slot") AssignMaterial(Selected, slot, payloadAsset);
            else if (targetKind == "scene")
            {
                var hit = PickMesh(x, y);
                if (hit is not null) AssignMaterial(hit, 0, payloadAsset);
                else SetStatus("マテリアルはメッシュの上か、Inspector の Materials の欄で離してください");
            }
            else SetStatus("マテリアルは Inspector の Materials の欄（Element 0）にドラッグしてください");
            return;
        }
        SetStatus(asset.Type == "model" ? "FBX ではなく、衣装の Prefab（Hoodie）を使ってください" : "テクスチャはマテリアルに設定して使います（この練習では省略）");
    }

    /// <summary>Hierarchy の何もない所をクリックした(選択を外す)。</summary>
    public void ClearSelection()
    {
        Selected = null;
        Render();
        UpdateGizmo();
    }

    /// <summary>Hierarchy の行をダブルクリック(選んで、表示する)。</summary>
    public void DoubleClickNode(string id)
    {
        Select(id);
        FrameSelected();
    }

    // ---------------------------------------------------------------- キー
    /// <summary>viewport のキー。入力欄・選択欄の上のキーは、呼ぶ前に除く。preventDefault するなら true。</summary>
    public bool KeyDown(string code, string key, Modifiers m)
    {
        Emit("key", ("code", code), ("key", key), ("shift", m.Shift), ("ctrl", m.Ctrl || m.Meta), ("alt", m.Alt));
        var mod = m.Ctrl || m.Meta;
        if (mod && code == "KeyZ") { Undo(); return true; }
        if (mod) return false;
        switch (code)
        {
            case "KeyQ": SetTool("hand"); break;
            case "KeyW": SetTool("move"); break;
            case "KeyE": SetTool("rotate"); break;
            case "KeyR": SetTool("scale"); break;
            case "KeyF": FrameSelected(); break;
            case "Delete": if (Selected is not null) DeleteNode(Selected); break;
            case "Escape": ClosePopup(); CloseSdk(); break;
            default: return false;
        }
        return true;
    }
}
