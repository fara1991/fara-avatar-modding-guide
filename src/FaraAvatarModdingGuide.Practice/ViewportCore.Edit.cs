namespace FaraAvatarModdingGuide.Practice;

// 編集モード: 要素の選択(クリック・ボックス・ループ・リング・つながり・拡大/縮小)、押し出し・差し込み・ベベル・ループカット・削除・マージ・面を張る・分離、メニュー。
// viewport.js の対応する関数を写したもの(メッシュ自体の操作は <see cref="EditMesh"/>)。
public sealed partial class ViewportCore
{
    // ---------------------------------------------------------------- 見えるか・ピック
    /// <summary>p(ワールド座標)が手前の面に隠れていなければ true。透過表示中は常に true。</summary>
    private bool IsVisible(Vec3 p, EmFace? faceHint = null)
    {
        if (Xray) return true;
        var (nx, ny) = View.ProjectToNdc(p);
        var ray = View.RayFromNdc(nx, ny);
        var hits = Picking.Raycast(ray, Objects);
        if (hits.Count == 0) return true;
        var d = ray.Origin.DistanceTo(p);
        if (faceHint is not null && hits[0].Object == EditObj && EditObj!.TriFace[hits[0].FaceIndex] == faceHint) return true;
        return hits[0].Distance >= d - Math.Max(0.002, d * 0.002);
    }

    private EmVert? PickVert(double pxX, double pxY, double radius = 14)
    {
        var obj = EditObj!;
        EmVert? best = null;
        var bestD = radius;
        foreach (var v in obj.Em.Verts)
        {
            var w = WorldOf(v, obj);
            var s = ToScreen(w);
            var d = Dist2(s.X, s.Y, pxX, pxY);
            if (d < bestD && IsVisible(w)) { best = v; bestD = d; }
        }
        return best;
    }

    private EmEdge? PickEdge(double pxX, double pxY, double radius = 12)
    {
        var obj = EditObj!;
        EmEdge? best = null;
        var bestD = radius;
        foreach (var e in obj.Em.Edges())
        {
            var wa = WorldOf(e.A, obj);
            var wb = WorldOf(e.B, obj);
            var sa = ToScreen(wa);
            var sb = ToScreen(wb);
            double abx = sb.X - sa.X, aby = sb.Y - sa.Y;
            var len2 = abx * abx + aby * aby;
            var t = len2 != 0 ? Math.Max(0, Math.Min(1, ((pxX - sa.X) * abx + (pxY - sa.Y) * aby) / len2)) : 0;
            var d = Dist2(sa.X + abx * t, sa.Y + aby * t, pxX, pxY);
            if (d < bestD && IsVisible(wa.Lerp(wb, Math.Max(0.1, Math.Min(0.9, t))))) { best = e; bestD = d; }
        }
        return best;
    }

    private EmFace? PickFace(double clientX, double clientY)
    {
        var hits = Picking.HitsOn(RayAt(clientX, clientY), EditObj!);
        if (hits.Count == 0) return null;
        var first = hits.OrderBy(h => h.Distance).First();
        return EditObj!.TriFace[first.FaceIndex];
    }

    // ---------------------------------------------------------------- 選択(編集モード)
    /// <summary>クリックでの選択(頂点・辺・面のモードに合わせる)。</summary>
    private void EditClick(double clientX, double clientY, bool extend)
    {
        var em = EditObj!.Em;
        var (px, py) = Pixel(clientX, clientY);
        object? item = SelectMode == "vert" ? PickVert(px, py) : SelectMode == "edge" ? PickEdge(px, py)?.Key : PickFace(clientX, clientY);
        if (item is null)
        {
            if (!extend) em.SelectAll(SelectMode, false);
        }
        else if (extend)
        {
            em.SetSelected(SelectMode, item);
        }
        else
        {
            em.SelectAll(SelectMode, false);
            em.SetSelected(SelectMode, item, true);
        }
        EditChanged();
        var c = em.Counts();
        Status = item is not null ? $"選択: 頂点 {c.Verts} / 辺 {c.Edges} / 面 {c.Faces}" : "選択解除";
        EmitSelect(extend ? "shift-click" : "click");
    }

    private void EditBox(double x0, double x1, double y0, double y1, bool extend)
    {
        var obj = EditObj!;
        var em = obj.Em;
        bool Inside(Vec3 w)
        {
            var s = ToScreen(w);
            return s.X >= x0 && s.X <= x1 && s.Y >= y0 && s.Y <= y1;
        }
        if (!extend) em.SelectAll(SelectMode, false);
        if (SelectMode == "vert")
        {
            foreach (var v in em.Verts)
            {
                var w = WorldOf(v, obj);
                if (Inside(w) && IsVisible(w)) em.Vsel.Add(v);
            }
        }
        else if (SelectMode == "edge")
        {
            foreach (var e in em.Edges())
            {
                var wa = WorldOf(e.A, obj);
                var wb = WorldOf(e.B, obj);
                if (Inside(wa) && Inside(wb) && IsVisible(wa.Lerp(wb, 0.5))) em.Esel.Add(e.Key);
            }
        }
        else
        {
            foreach (var f in em.Faces)
            {
                var w = obj.MatrixWorld.ApplyToPoint(EditMesh.FaceCenter(f));
                if (Inside(w) && IsVisible(w, f)) em.Fsel.Add(f);
            }
        }
        em.Flush(SelectMode);
        EditChanged();
        var c = em.Counts();
        Status = $"選択: 頂点 {c.Verts} / 辺 {c.Edges} / 面 {c.Faces}";
        EmitSelect("box");
    }

    /// <summary>Alt+クリック(ループ)/ Ctrl+Alt+クリック(リング)。</summary>
    private void EditLoop(double clientX, double clientY, bool ring, bool extend)
    {
        var (px, py) = Pixel(clientX, clientY);
        var edge = PickEdge(px, py, 25);
        if (edge is null) { Status = "辺の近くで Alt+クリックしてください"; return; }
        SelectLoop(edge, ring, extend);
    }

    private void SelectLoop(EmEdge edge, bool ring, bool extend)
    {
        var em = EditObj!.Em;
        if (!extend) em.SelectAll(SelectMode, false);
        if (SelectMode == "face")
        {
            foreach (var f in em.EdgeRing(edge).Faces) em.Fsel.Add(f);
            em.Flush("face");
        }
        else
        {
            var edges = ring ? em.EdgeRing(edge).Ring.Select(r => r.E).ToList() : em.EdgeLoop(edge);
            if (SelectMode == "edge")
            {
                foreach (var x in edges) em.Esel.Add(x.Key);
                em.Flush("edge");
            }
            else if (ring)
            {
                foreach (var x in edges) { em.Vsel.Add(x.A); em.Vsel.Add(x.B); }
                em.Flush("vert");
            }
            else em.SelectVerts("vert", edges.SelectMany(x => new[] { x.A, x.B }), true);
        }
        EditChanged();
        var c = em.Counts();
        Status = $"{(ring ? "辺リング" : "ループ")}を選択: 頂点 {c.Verts} / 辺 {c.Edges} / 面 {c.Faces}";
        EmitSelect(ring ? "ring" : "loop");
    }

    /// <summary>L(マウスの下)/ Ctrl+L(今の選択から)でつながった部分を選ぶ。</summary>
    private void SelectLinked(bool underMouse)
    {
        var em = EditObj!.Em;
        List<EmVert> seeds;
        if (underMouse)
        {
            var (px, py) = MousePx();
            var v = PickVert(px, py, 30);
            var e = v is not null ? null : PickEdge(px, py, 30);
            var f = v is not null || e is not null ? null : PickFace(mouseClientX, mouseClientY);
            seeds = v is not null ? [v] : e is not null ? [e.A, e.B] : f is not null ? [.. f.V] : [];
            if (seeds.Count == 0) { Status = "メッシュの上にマウスを置いて L を押してください"; return; }
        }
        else
        {
            seeds = em.Vsel.ToList();
            if (seeds.Count == 0) { Status = "先に一部を選択してください"; return; }
        }
        em.SelectVerts(SelectMode, em.Linked(seeds), true);
        EditChanged();
        var c = em.Counts();
        Status = $"つながった部分を選択: 頂点 {c.Verts}";
        EmitSelect("linked");
    }

    private void GrowShrink(bool grow)
    {
        var em = EditObj!.Em;
        if (grow) em.Grow(SelectMode); else em.Shrink(SelectMode);
        EditChanged();
        EmitSelect(grow ? "grow" : "shrink");
    }

    // ---------------------------------------------------------------- 編集モードの操作
    private bool RequireEdit(string what)
    {
        if (Mode == "edit") return true;
        Status = $"{what}は編集モードの操作です（Tab で編集モードへ）";
        Emit("opfail", ("kind", what), ("reason", "objectmode"));
        return false;
    }

    /// <summary>E: 押し出してから移動。</summary>
    private void Extrude()
    {
        if (!RequireEdit("押し出し")) return;
        var obj = EditObj!;
        var em = obj.Em;
        if (em.Vsel.Count == 0)
        {
            Status = "先に頂点・辺・面を選択してください";
            Emit("opfail", ("kind", "E"), ("reason", "noselection"));
            return;
        }
        PushUndo();
        var res = em.Extrude(SelectMode);
        if (res is null) { PopUndo(); return; }
        EditChanged();
        Emit("extrude", ("type", res.Type));
        Vec3? normal = res.Normal is { } n ? obj.MatrixWorld.ApplyNormalMatrix(n).Normalized() : null;
        StartOp(TransformKind.G, noPush: true, normal: normal, fromExtrude: true);
    }

    /// <summary>I: 面の差し込み。</summary>
    private void Inset()
    {
        if (!RequireEdit("差し込み")) return;
        var em = EditObj!.Em;
        if (em.Fsel.Count == 0)
        {
            Status = "差し込む面を選択してください（3 で面選択モード）";
            Emit("opfail", ("kind", "I"), ("reason", "noselection"));
            return;
        }
        PushUndo();
        var res = em.Inset(SelectMode)!;
        EditChanged();
        Emit("inset");
        StartMoveOp(TransformKind.I, res.Moves, null, 0, res.Limit);
    }

    /// <summary>Ctrl+B: 辺のベベル(分割数 1)。</summary>
    private void Bevel()
    {
        if (!RequireEdit("ベベル")) return;
        var em = EditObj!.Em;
        PushUndo();
        var res = em.Bevel(SelectMode);
        if (res.Error is { } error)
        {
            Restore(PopUndo()!);
            Status = error;
            Emit("opfail", ("kind", "B"), ("reason", error));
            return;
        }
        EditChanged();
        Emit("bevel");
        StartMoveOp(TransformKind.B, res.Moves, null, 0, res.Limit);
    }

    /// <summary>Ctrl+R: ループカット。マウスを辺に重ねると黄色い線、ホイールで本数、クリックで確定して位置調整へ。</summary>
    private void StartLoopCut()
    {
        if (!RequireEdit("ループカット")) return;
        LoopCut = new LoopCutState();
        Status = "ループカット: 辺の上にマウスを置く（ホイールで本数、クリックで確定、右クリックでやめる）";
        UpdateLoopPreview();
        Emit("opstart", ("kind", "loopcut"), ("edit", true));
    }

    private void EndLoopCut() => LoopCut = null;

    /// <summary>プレビューを作り直す。pickFromMouse なら、マウスの下の辺(40 ピクセル以内)を使う。edge を渡すとその辺(チュートリアルの再現用)。</summary>
    private void UpdateLoopPreview(EmEdge? edge = null, bool pickFromMouse = true)
    {
        var lc = LoopCut;
        if (lc is null) return;
        if (pickFromMouse)
        {
            var (px, py) = MousePx();
            edge = PickEdge(px, py, 40);
        }
        lc.Edge = edge;
        lc.Preview = null;
        if (edge is null) return;
        var em = EditObj!.Em;
        var ring = em.EdgeRing(edge);
        if (ring.Faces.Count == 0)
        {
            lc.Edge = null;
            Status = "ここにはループカットを入れられません（四角形の面がありません）";
            return;
        }
        var pts = new List<Vec3>();
        for (var j = 0; j < ring.Faces.Count; j++)
        {
            var r0 = ring.Ring[j];
            var r1 = ring.Ring[(j + 1) % ring.Ring.Count];
            for (var m = 1; m <= lc.Cuts; m++)
            {
                var t = (double)m / (lc.Cuts + 1);
                pts.Add(r0.A.Co.Lerp(r0.B.Co, t));
                pts.Add(r1.A.Co.Lerp(r1.B.Co, t));
            }
        }
        lc.Preview = pts;
        Status = $"ループカット: {lc.Cuts} 本（ホイールで本数、クリックで確定）";
    }

    private void SetCuts(int n)
    {
        if (LoopCut is null) return;
        LoopCut.Cuts = Math.Max(1, Math.Min(20, n));
        if (LoopCut.Edge is not null) UpdateLoopPreview(LoopCut.Edge, false);
        else UpdateLoopPreview();
        Emit("cuts", ("cuts", LoopCut.Cuts));
    }

    /// <summary>プレビュー中の辺でカットし、位置調整(slide)を始める。</summary>
    private void CommitLoopCut()
    {
        var cuts = LoopCut?.Cuts ?? 1;
        var edge = LoopCut?.Edge;
        EndLoopCut();
        if (edge is null)
        {
            Status = "ループカットをやめました";
            Emit("cancel", ("kind", "loopcut"));
            return;
        }
        PushUndo();
        var res = EditObj!.Em.LoopCut(edge, cuts, SelectMode);
        if (res is null) { PopUndo(); return; }
        EditChanged();
        Emit("loopcut", ("cuts", cuts));
        StartMoveOp(TransformKind.Slide, null, res.Slide, cuts, 0);
    }

    private void DeleteEdit(string what)
    {
        var em = EditObj!.Em;
        if (em.Vsel.Count == 0) return;
        PushUndo();
        em.Remove(what);
        EditChanged();
        var label = what switch { "vert" => "頂点", "edge" => "辺", "face" => "面", _ => "辺を溶解" };
        Status = $"{label}{(what == "dissolve" ? "" : "を削除")}";
        Emit("delete", ("edit", true), ("what", what), ("names", Array.Empty<string>()));
    }

    private void MergeEdit(string how)
    {
        var em = EditObj!.Em;
        PushUndo();
        var cursor = EditObj.MatrixWorld.Inverted().ApplyToPoint(Vec3.Zero);
        var removed = em.Merge(how, cursor);
        if (removed == 0) PopUndo();
        EditChanged();
        Status = $"{removed} 個の頂点を削除";
        Emit("merge", ("how", how), ("removed", removed));
    }

    private void FillEdit()
    {
        if (!RequireEdit("面を張る")) return;
        var em = EditObj!.Em;
        PushUndo();
        var made = em.Fill(SelectMode);
        if (made is null)
        {
            PopUndo();
            Status = "面を張れません（頂点を 2 つ以上選んでください）";
            return;
        }
        EditChanged();
        Status = made == "face" ? "面を張りました" : "辺を作りました";
        Emit("fill", ("made", made));
    }

    private void SeparateEdit()
    {
        var em = EditObj!.Em;
        if (em.Fsel.Count == 0) { Status = "分離する面を選択してください"; return; }
        PushUndo();
        var src = EditObj;
        var part = em.Separate()!;
        EditChanged();
        var m = AddObject(src.Kind, src.Position, part.ToData());
        m.Quaternion = src.Quaternion;
        m.Scale = src.Scale;
        SetXray(Xray, true);
        Status = $"{m.Name} に分離しました";
        Emit("separate", ("name", m.Name));
    }

    // ---------------------------------------------------------------- メニュー
    private void ShowMenu(string title, IReadOnlyList<MenuItem> items)
    {
        CloseMenu();
        Menu = new MenuState(title, items);
    }

    private void CloseMenu() => Menu = null;

    /// <summary>メニューの index 番目の項目を選ぶ(メニューを閉じて実行し、ビューにフォーカスを戻す)。</summary>
    public void ChooseMenu(int index)
    {
        var menu = Menu;
        if (menu is null || index < 0 || index >= menu.Items.Count) return;
        CloseMenu();
        menu.Items[index].Run();
        Focused = true;
    }

    private void ShowAddMenu()
    {
        if (Mode == "edit") { Status = "編集モードでは追加できません（Tab でオブジェクトモードへ）"; return; }
        ShowMenu("追加 > メッシュ", [.. Primitives.Select(p => new MenuItem(p.Label, () => AddPrimitive(p.Key)))]);
        Emit("menu", ("menu", "add"));
    }

    private void ShowDeleteMenu()
    {
        if (Mode == "edit")
        {
            if (EditObj!.Em.Vsel.Count == 0) { Status = "先に頂点・辺・面を選択してください"; return; }
            ShowMenu("削除",
            [
                new MenuItem("頂点", () => DeleteEdit("vert")),
                new MenuItem("辺", () => DeleteEdit("edge")),
                new MenuItem("面", () => DeleteEdit("face")),
                new MenuItem("辺を溶解", () => DeleteEdit("dissolve")),
            ]);
            Emit("menu", ("menu", "delete"), ("edit", true));
            return;
        }
        if (Selected.Count == 0) return;
        ShowMenu("削除", [new MenuItem("削除", DeleteSelected)]);
        Emit("menu", ("menu", "delete"));
    }

    private void ShowMergeMenu()
    {
        if (!RequireEdit("マージ")) return;
        if (EditObj!.Em.Vsel.Count == 0) { Status = "先に頂点を選択してください"; return; }
        ShowMenu("マージ",
        [
            new MenuItem("中心に", () => MergeEdit("center")),
            new MenuItem("カーソル位置に", () => MergeEdit("cursor")),
            new MenuItem("距離で", () => MergeEdit("distance")),
        ]);
        Emit("menu", ("menu", "merge"));
    }

    private void ShowSeparateMenu()
    {
        if (!RequireEdit("分離")) return;
        ShowMenu("分離", [new MenuItem("選択", SeparateEdit)]);
        Emit("menu", ("menu", "separate"));
    }
}
