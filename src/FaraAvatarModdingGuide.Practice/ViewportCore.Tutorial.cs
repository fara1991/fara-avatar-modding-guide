namespace FaraAvatarModdingGuide.Practice;

/// <summary>記事内チュートリアルの案内(viewport.js の setHighlight の引数)。Object はオブジェクト名(矢印を出す)、At はワールド座標の点、Axis は点線で示す軸、Hud は "view"、Tool はボタンの名前。</summary>
public sealed record TutorialHighlight(string? Object = null, string? Label = null, string? Axis = null, string? Hud = null, double[]? At = null, string? Tool = null);

/// <summary>辺を探す条件に渡す、ワールド座標の中点 C・向き D(単位ベクトル)・両端 A / B(viewport.js の findEdge が fn に渡す { c, d, a, b })。</summary>
public readonly record struct EdgeProbe(Vec3 C, Vec3 D, Vec3 A, Vec3 B);

/// <summary>編集モードの要素を探す条件に渡す値。面: Center / Normal、辺: C / D、頂点: Co(いずれもワールド座標。使わないものは 0)。</summary>
public readonly record struct ElementProbe(Vec3 Center, Vec3 Normal, Vec3 C, Vec3 D, Vec3 Co);

/// <summary>
/// チュートリアルの手順が使う操作(viewport.js の、チュートリアル用のメソッド)。手順の「スキップ」(その手順を終えた状態にする)・再開・画像づくりが、イベント無しで状態を作るために呼ぶ。
/// </summary>
public sealed partial class ViewportCore
{
    /// <summary>true のあいだ、イベントを出さない(viewport.js の silent。手順の再生のため)。</summary>
    public bool Silent
    {
        get => silent;
        set => silent = value;
    }

    /// <summary>左下のステータス表示を決める(setStatus)。</summary>
    public void SetStatusText(string text) => Status = text;

    // ---------------------------------------------------------------- 案内(矢印・点線・ボタンの強調)
    public TutorialHighlight Highlight { get; private set; } = new();

    /// <summary>案内を変える(setHighlight)。矢印のラベルは label か object の名前(どちらも無ければ「ここ」)。</summary>
    public void SetHighlight(TutorialHighlight? h) => Highlight = h ?? new TutorialHighlight();

    /// <summary>矢印の文字。</summary>
    public string MarkerLabel => !string.IsNullOrEmpty(Highlight.Label) ? Highlight.Label! : Highlight.Object ?? "ここ";

    /// <summary>矢印をビューポート内のピクセルで置く位置(updateMarker)。対象の無いとき・オブジェクトが見つからないとき null。オブジェクトのときは、位置の少し上(1.1 × スケール Z)。</summary>
    public (double X, double Y)? MarkerScreen
    {
        get
        {
            var h = Highlight;
            Vec3 top;
            if (h.At is { Length: 3 } at) top = new Vec3(at[0], at[1], at[2]);
            else if (h.Object is { } name)
            {
                var o = Find(name);
                if (o is null) return null;
                top = o.Position with { Z = o.Position.Z + 1.1 * o.Scale.Z };
            }
            else return null;
            return View.ToScreen(top, Rect.Width, Rect.Height);
        }
    }

    /// <summary>案内の軸の点線を出すか(updateGuide)。軸固定中はその線(実線)が出るので、点線は出さない。出すときの位置(対象のオブジェクトか、アクティブ、なければ原点)と軸。</summary>
    public (Vec3 Position, string Axis)? GuideLine
    {
        get
        {
            var axis = Highlight.Axis;
            if (axis is null) return null;
            if (Op is { } op && op.Axis == axis && !op.Exclude) return null;
            var target = (Highlight.Object is { } name ? Find(name) : null) ?? Active;
            return (target?.Position ?? Vec3.Zero, axis);
        }
    }

    // ---------------------------------------------------------------- 選択
    /// <summary>名前でオブジェクトを選ぶ(selectByName)。</summary>
    public void SelectByName(string name, bool extend = false)
    {
        Select(Find(name), extend);
        EmitSelect(extend ? "shift-click" : "click");
    }

    /// <summary>すべて選択 / 解除(A / Alt+A と同じ)。</summary>
    public void SelectAllItems(bool all = true) => SelectAll(all);

    /// <summary>編集モードに入る / 出る(toggleEditMode)。</summary>
    public bool ToggleEditMode(bool on) => SetMode(on ? "edit" : "object");

    /// <summary>条件に合う辺(ワールド座標の中点・向きで判定)。編集中のメッシュの最初の 1 本。</summary>
    public EmEdge? FindEdge(Func<EdgeProbe, bool> fn)
    {
        var obj = EditObj!;
        return obj.Em.Edges().FirstOrDefault(e =>
        {
            var a = WorldOf(e.A, obj);
            var b = WorldOf(e.B, obj);
            return fn(new EdgeProbe(a.Lerp(b, 0.5), (b - a).Normalized(), a, b));
        });
    }

    /// <summary>条件に合う辺で、ループ(ring なら辺リング)を選ぶ。</summary>
    public void SelectLoopWhere(Func<EdgeProbe, bool> fn, bool ring = false)
    {
        var edge = FindEdge(fn);
        if (edge is not null) SelectLoop(edge, ring, false);
    }

    /// <summary>条件に合う要素を選ぶ(現在の選択モードの要素。extend なら足す)。</summary>
    public void EditSelectWhere(Func<ElementProbe, bool> fn, bool extend = false)
    {
        var obj = EditObj!;
        var em = obj.Em;
        var m = obj.MatrixWorld;
        if (!extend) em.SelectAll(SelectMode, false);
        if (SelectMode == "face")
        {
            foreach (var f in em.Faces)
                if (fn(new ElementProbe(m.ApplyToPoint(EditMesh.FaceCenter(f)), m.ApplyNormalMatrix(EditMesh.FaceNormal(f)).Normalized(), Vec3.Zero, Vec3.Zero, Vec3.Zero))) em.Fsel.Add(f);
        }
        else if (SelectMode == "edge")
        {
            foreach (var e in em.Edges())
            {
                var a = WorldOf(e.A, obj);
                var b = WorldOf(e.B, obj);
                if (fn(new ElementProbe(Vec3.Zero, Vec3.Zero, a.Lerp(b, 0.5), (b - a).Normalized(), Vec3.Zero))) em.Esel.Add(e.Key);
            }
        }
        else
        {
            foreach (var v in em.Verts)
                if (fn(new ElementProbe(Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, WorldOf(v, obj)))) em.Vsel.Add(v);
        }
        em.Flush(SelectMode);
        EditChanged();
        EmitSelect("click");
    }

    // ---------------------------------------------------------------- 操作(G / R / S・数値・確定)
    /// <summary>G / R / S を始める(startOp)。</summary>
    public void StartOperation(TransformKind kind) => StartOp(kind);

    /// <summary>軸を固定する(setAxis(axis, exclude)。キー入力の Shift ではなく、引数で「それ以外」を決める)。</summary>
    public void SetOpAxis(string axis, bool exclude)
    {
        var op = Op;
        if (op is null || !op.Kind.IsTransform()) return;
        op.SetAxis(axis, exclude);
        ApplyOp();
        Emit("axis", ("kind", op.Kind.Name()), ("axis", op.Axis), ("exclude", op.Exclude));
    }

    public void TypeOpNumeric(string text) => TypeNumeric(text);

    public void ConfirmOperation() => ConfirmOp();

    public void CancelOperation() => CancelOp();

    // ---------------------------------------------------------------- メニュー・編集の操作
    public void ShowAddMenuNow() => ShowAddMenu();

    public void ShowDeleteMenuNow() => ShowDeleteMenu();

    public void ShowMergeMenuNow() => ShowMergeMenu();

    public void CloseMenuNow() => CloseMenu();

    public void AddPrimitiveNow(string kind) => AddPrimitive(kind);

    public void DeleteSelectedObjects() => DeleteSelected();

    public void DuplicateSelected() => Duplicate();

    public void UndoNow() => Undo();

    public void ExtrudeNow() => Extrude();

    public void InsetNow() => Inset();

    public void BevelNow() => Bevel();

    public void FillNow() => FillEdit();

    public void DeleteEditNow(string what) => DeleteEdit(what);

    public void MergeEditNow(string how) => MergeEdit(how);

    public void ToggleSidebar(bool? open = null) => ToggleNPanel(open);

    // ---------------------------------------------------------------- 視点
    public bool NumpadViewNow(string key, bool ctrl = false) => NumpadView(key, ctrl);

    /// <summary>オブジェクトを全部 / 選択に合わせて視点を寄せる(frame(vp.objects))。</summary>
    public void FrameAll() => Frame(Objects);

    // ---------------------------------------------------------------- ループカット
    /// <summary>ループカットのプレビューを、条件に合う辺で出す(previewLoopCut)。</summary>
    public void PreviewLoopCut(Func<EdgeProbe, bool> fn)
    {
        if (LoopCut is null) StartLoopCut();
        if (LoopCut is not null) UpdateLoopPreview(FindEdge(fn), false);
    }

    /// <summary>本数を決める(setCuts)。</summary>
    public void SetLoopCuts(int n) => SetCuts(n);

    /// <summary>辺を指定して確定する(commitLoopCut(edge))。edge が null ならやめる。</summary>
    public void CommitLoopCutAt(EmEdge? edge)
    {
        if (LoopCut is not null) LoopCut.Edge = edge;
        else
        {
            // プレビュー中でないとき(スキップの再生): 本数 1 で、その辺に入れる
            LoopCut = new LoopCutState { Edge = edge };
        }
        CommitLoopCut();
    }
}
