namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// 3D ビューの操作(viewport.js の Viewport から、描画と DOM を除いた部分)。
/// オブジェクトモードと編集モードの入力(ポインター・ホイール・キー)を受けて、状態(オブジェクト・選択・視点・操作中のようす・取り消し)と、
/// 画面に出す文字・イベントを更新する。描画も DOM も持たない: 画面に出すものは状態(Status・NumericHud・Tools・NPanel・Menu など)として読む。
/// 現行の JavaScript と同じ入力で同じ結果になることを、tests/FaraAvatarModdingGuide.Practice.Tests が確かめる。
///
/// 現行の関数との対応: bind() のハンドラー → PointerMove / PointerDown / PointerUp / Wheel / KeyUp / ClickTool / ChangeNPanel、onKey → KeyDown、
/// resetScene・addObject・removeObject・select*・setMode・undo / redo・room 系 → ViewportCore.Scene.cs、編集モードの操作・メニュー → ViewportCore.Edit.cs。
/// ポインターとキーの関数は、戻り値で「ブラウザの既定動作を止めたか」(preventDefault を呼んだか)を返す。
/// </summary>
public sealed partial class ViewportCore
{
    /// <summary>取り消しに積める数(viewport.js の pushUndo の上限)。</summary>
    public const int MaxUndo = 50;

    private enum DragKind { Orbit, Pan, Zoom, Box, AltClick }

    private sealed class Drag
    {
        public DragKind Kind;
        public double LastX, LastY, StartX, StartY;
        public double Distance;
        public bool Shift, Moved, Ring;
    }

    private readonly List<ViewEvent> events = [];
    private Drag? drag;
    private double mouseNdcX, mouseNdcY, mouseClientX, mouseClientY;
    private bool keyShift, keyCtrl;
    private bool silent;

    public ViewportCore(ViewRect rect, ViewportOptions? options = null)
    {
        Options = options ?? new ViewportOptions();
        NewId = Options.NewId ?? RandomId;
        Rect = rect;
        ApplySize(rect);
        mouseClientX = rect.Left + rect.Width / 2;
        mouseClientY = rect.Top + rect.Height / 2;
        ResetScene();
    }

    public ViewportOptions Options { get; }
    public ViewRect Rect { get; private set; }

    /// <summary>true なら数字キーをテンキーとして扱う(テンキーを模倣)。</summary>
    public Func<bool> EmulateNumpad => Options.EmulateNumpad ?? (() => false);

    public OrbitView View { get; } = new();

    /// <summary>ここまでに出たイベントを、出た順に呼び出す(viewport.js の on("*") と同じ。出た瞬間の状態で呼ぶ)。</summary>
    public event Action<ViewEvent>? EventEmitted;

    /// <summary>ビューにフォーカスがあるか(wheelNeedsFocus の判定。ポインターを押すと得る)。</summary>
    public bool Focused { get; private set; }

    public void Focus() => Focused = true;
    public void Blur() => Focused = false;

    /// <summary>ここまでに出たイベントを取り出す(空にする)。</summary>
    public IReadOnlyList<ViewEvent> TakeEvents()
    {
        var list = events.ToArray();
        events.Clear();
        return list;
    }

    /// <summary>ビューの大きさが変わったとき(resize())。</summary>
    public void Resize(ViewRect rect)
    {
        Rect = rect;
        ApplySize(rect);
    }

    /// <summary>縦横比と高さ。幅か高さが 0 のときは変えない(viewport.js の resize は早期 return。縦横比は `aspect || 1`)。</summary>
    private void ApplySize(ViewRect rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            View.Aspect = rect.Width / rect.Height;
            View.HeightPx = rect.Height;
        }
    }

    private void Emit(string type, params (string Key, object? Value)[] detail)
    {
        if (silent) return;
        // 現行は { type, ...detail } でイベントを作るので、detail に type があると、イベントの種類がそれで上書きされる
        // (押し出しの extrude イベントは type が "region" / "edge" / "vert" になり、"extrude" としては届かない。現行の不具合だが、挙動のとおりに写す)
        var overridden = detail.FirstOrDefault(d => d.Key == "type").Value as string;
        var e = new ViewEvent(overridden ?? type, detail.ToDictionary(d => d.Key, d => d.Value));
        events.Add(e);
        EventEmitted?.Invoke(e);
    }

    private void EmitView(string action, params (string Key, object? Value)[] extra) =>
        Emit("view", [("action", action), ("name", View.Name), ("ortho", View.Ortho), .. extra]);

    // ---------------------------------------------------------------- 座標
    private (double X, double Y) Ndc(double clientX, double clientY) =>
        ((clientX - Rect.Left) / Rect.Width * 2 - 1, -((clientY - Rect.Top) / Rect.Height) * 2 + 1);

    private (double X, double Y) Pixel(double clientX, double clientY) => (clientX - Rect.Left, clientY - Rect.Top);

    /// <summary>最後にマウスがあった位置(ビューポート内のピクセル)。まだ入っていなければ中央。</summary>
    private (double X, double Y) MousePx()
    {
        var (x, y) = Pixel(mouseClientX, mouseClientY);
        var inside = double.IsFinite(x) && double.IsFinite(y) && x >= 0 && y >= 0 && x <= Rect.Width && y <= Rect.Height;
        return inside ? (x, y) : (Rect.Width / 2, Rect.Height / 2);
    }

    private (double X, double Y) ToScreen(Vec3 world) => View.ToScreen(world, Rect.Width, Rect.Height);

    private Ray RayAt(double clientX, double clientY)
    {
        var (nx, ny) = Ndc(clientX, clientY);
        return View.RayFromNdc(nx, ny);
    }

    private SceneObject? PickAt(double clientX, double clientY) => Picking.Pick(RayAt(clientX, clientY), Objects);

    private static double Dist2(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    /// <summary>マウスの下のワールド座標(オブジェクトの表面か、床 Z = 0)。どちらにも当たらなければ null(共同練習のカーソル用)。</summary>
    public Vec3? PointAt(double clientX, double clientY)
    {
        var ray = RayAt(clientX, clientY);
        var hits = Picking.Raycast(ray, Objects);
        if (hits.Count > 0) return hits[0].Point;
        // Ray.intersectPlane(Plane((0,0,1), 0))
        double t;
        var denominator = ray.Direction.Z;
        if (denominator == 0)
        {
            if (ray.Origin.Z != 0) return null;
            t = 0;
        }
        else
        {
            t = -ray.Origin.Z / denominator;
            if (!(t >= 0)) return null;
        }
        var p = ray.At(t);
        return p.Length < 500 ? p : null;
    }

    // ---------------------------------------------------------------- ポインター
    public void PointerMove(double clientX, double clientY, Modifiers m)
    {
        var (nx, ny) = Ndc(clientX, clientY);
        mouseNdcX = nx; mouseNdcY = ny; mouseClientX = clientX; mouseClientY = clientY;
        keyShift = m.Shift; keyCtrl = m.Ctrl;
        if (LoopCut is not null) { UpdateLoopPreview(); return; }
        if (Op is not null) { ApplyOp(); return; }
        if (drag is not null) OnDrag(clientX, clientY);
    }

    /// <summary>button: 0 左・1 中・2 右。overUi は、サイドバー(入力欄)やメニューのボタンの上で押したとき(何もしない)。戻り値は preventDefault を呼んだか。</summary>
    public bool PointerDown(int button, double clientX, double clientY, Modifiers m, bool overUi = false)
    {
        if (overUi) return false;
        Focused = true;
        CloseMenu();
        if (LoopCut is not null)
        {
            if (button == 0) { UpdateLoopPreview(); CommitLoopCut(); }
            else if (button == 2) { EndLoopCut(); Status = "ループカットをやめました"; Emit("cancel", ("kind", "loopcut")); }
            return false;
        }
        if (Op is not null)
        {
            if (button == 0) ConfirmOp();
            else if (button == 2) CancelOp();
            return false;
        }
        var (px, py) = Pixel(clientX, clientY);
        if (button == 0 && m.Alt && Mode == "edit")
        {
            // Alt+クリックはループ選択。動かしたら視点を回す
            drag = new Drag { Kind = DragKind.AltClick, StartX = px, StartY = py, LastX = px, LastY = py, Ring = m.Ctrl, Shift = m.Shift };
            return true;
        }
        if (button == 1 || (button == 0 && m.Alt))
        {
            drag = new Drag { Kind = m.Shift ? DragKind.Pan : m.Ctrl ? DragKind.Zoom : DragKind.Orbit, LastX = px, LastY = py };
            return true;
        }
        if (button == 0)
            drag = new Drag { Kind = DragKind.Box, StartX = px, StartY = py, Shift = m.Shift };
        return false;
    }

    public void PointerUp(double clientX, double clientY)
    {
        var d = drag;
        drag = null;
        if (d is null) return;
        if (d.Kind == DragKind.AltClick)
        {
            EditLoop(clientX, clientY, d.Ring, d.Shift);
        }
        else if (d.Kind == DragKind.Box)
        {
            if (d.Moved) FinishBox(d, clientX, clientY);
            else if (Mode == "edit") EditClick(clientX, clientY, d.Shift);
            else
            {
                var hit = PickAt(clientX, clientY);
                Select(hit, d.Shift);
                Status = hit is not null ? $"{hit.Name} を選択" : "選択解除";
                EmitSelect(d.Shift ? "shift-click" : "click");
            }
        }
        else if (d.Distance > 0)
        {
            EmitView(d.Kind switch { DragKind.Pan => "pan", DragKind.Zoom => "zoom", _ => "orbit" }, ("distance", d.Distance));
        }
    }

    /// <summary>ホイール。wheelNeedsFocus でフォーカスが無いときは何もしない(ページのスクロールを優先)。戻り値は preventDefault を呼んだか。</summary>
    public bool Wheel(double deltaY)
    {
        if (Options.WheelNeedsFocus && !Focused) return false;
        if (LoopCut is not null) { SetCuts(LoopCut.Cuts + (deltaY < 0 ? 1 : -1)); return true; }
        if (Op?.Kind == TransformKind.B) { Status = "このビューのベベルは分割数 1 だけです（Blender ではホイールで分割数が増えます）"; return true; }
        View.ZoomWheel(deltaY);
        EmitView("zoom", ("delta", (double)Js.Sign(deltaY)));
        return true;
    }

    private void OnDrag(double clientX, double clientY)
    {
        var d = drag!;
        var (px, py) = Pixel(clientX, clientY);
        if (d.Kind == DragKind.AltClick)
        {
            if (Dist2(px, py, d.StartX, d.StartY) < 5) return;
            d.Kind = DragKind.Orbit; // 動かしたので視点の回転にする
        }
        if (d.Kind != DragKind.Box) d.Distance += Dist2(px, py, d.LastX, d.LastY);
        switch (d.Kind)
        {
            case DragKind.Orbit:
                // 平行投影のまま回すと Blender では透視に戻る(自動透視)
                View.OrbitBy(-(px - d.LastX) * 0.008, -(py - d.LastY) * 0.008);
                break;
            case DragKind.Pan:
                View.Pan(px - d.LastX, py - d.LastY);
                break;
            case DragKind.Zoom:
                View.ZoomDrag(py - d.LastY);
                break;
            case DragKind.Box:
                if (!d.Moved && Dist2(px, py, d.StartX, d.StartY) < 4) return;
                d.Moved = true;
                return;
        }
        d.LastX = px; d.LastY = py;
    }

    private void FinishBox(Drag d, double clientX, double clientY)
    {
        var (px, py) = Pixel(clientX, clientY);
        double x0 = Math.Min(px, d.StartX), x1 = Math.Max(px, d.StartX), y0 = Math.Min(py, d.StartY), y1 = Math.Max(py, d.StartY);
        if (Mode == "edit") { EditBox(x0, x1, y0, y1, d.Shift); return; }
        if (!d.Shift) Selected = [];
        foreach (var o in Objects)
        {
            var (sx, sy) = ToScreen(o.Position);
            if (sx >= x0 && sx <= x1 && sy >= y0 && sy <= y1 && !Selected.Contains(o)) { Selected.Add(o); Active = o; }
        }
        if (Selected.Count == 0) Active = null;
        RefreshSelection();
        Status = $"{Selected.Count} 個を選択";
        EmitSelect("box");
    }

    // ---------------------------------------------------------------- 操作(G / R / S / I / B / slide)
    /// <summary>
    /// G / R / S を始める。noPush: 取り消しの記録をしない(押し出しなど、直前に記録済みのとき)。normal: 最初から固定する方向(押し出しの法線。ワールド座標)。
    /// fromExtrude / fromDuplicate: キャンセルしても押し出し・複製したものは残す。
    /// </summary>
    private void StartOp(TransformKind kind, bool noPush = false, Vec3? normal = null, bool fromExtrude = false, bool fromDuplicate = false)
    {
        var name = kind.Name();
        if (Mode == "edit")
        {
            var obj = EditObj!;
            var em = obj.Em;
            if (em.Vsel.Count == 0)
            {
                Status = "先に頂点・辺・面を選択してください";
                Emit("opfail", ("kind", name), ("reason", "noselection"));
                return;
            }
            var start = em.Vsel.Select(v => new EditStart(v, WorldOf(v, obj))).ToList();
            var sum = Vec3.Zero;
            foreach (var s in start) sum += s.W;
            var pivot = sum.DivideScalar(start.Count);
            if (!noPush) PushUndo();
            var (px, py) = MousePx();
            Op = TransformOp.ForEdit(kind, obj, start, pivot, mouseNdcX, mouseNdcY, px, py, normal, fromExtrude);
        }
        else
        {
            if (Selected.Count == 0)
            {
                Status = "先にオブジェクトを選択してください";
                Emit("opfail", ("kind", name), ("reason", "noselection"));
                return;
            }
            // 共同練習: ほかの人が操作中のオブジェクトは動かせない
            var blocked = Options.CanTransform?.Invoke(Selected);
            if (!string.IsNullOrEmpty(blocked))
            {
                Status = blocked;
                Emit("opfail", ("kind", name), ("reason", blocked));
                return;
            }
            if (!noPush) PushUndo();
            var (px, py) = MousePx();
            Op = new TransformOp(kind, Selected, mouseNdcX, mouseNdcY, px, py, normal, fromExtrude, fromDuplicate);
        }
        ApplyOp();
        Emit("opstart", ("kind", name), ("edit", Op!.Edit), ("extrude", Op.FromExtrude));
    }

    /// <summary>差し込み(I)・ベベル(B)・ループカットの位置(slide)。moves の頂点を動かす操作。</summary>
    private void StartMoveOp(TransformKind kind, IReadOnlyList<MoveVert>? moves, IReadOnlyList<SlideVert>? slide, int cuts, double limit)
    {
        var obj = EditObj!;
        var (px, py) = MousePx();
        Op = TransformOp.ForMoves(kind, obj, moves, slide, cuts, limit, mouseNdcX, mouseNdcY, px, py);
        ApplyOp();
        Emit("opstart", ("kind", kind.Name()), ("edit", true));
    }

    private void EndOp()
    {
        Op = null;
        NumericHud = "";
        AxisLine = new AxisLine(false, Vec3.Zero, 0);
        UpdateNPanel();
    }

    private void CancelOp()
    {
        var op = Op;
        if (op is null) return;
        if (op.Kind == TransformKind.Slide)
        {
            // ループカットの位置調整をやめると、ちょうど真ん中に入る(Blender と同じ)
            op.SetNumeric("0");
            ApplyOp();
            EndOp();
            Status = "ループカット（中央）";
            Emit("confirm", ("kind", "slide"), ("value", "0"), ("edit", true), ("centered", true));
            return;
        }
        if (op.Kind is TransformKind.I or TransformKind.B)
        {
            EndOp();
            Restore(PopUndo()!);
        }
        else if (op.Edit)
        {
            op.Restore();
            if (!op.FromExtrude) PopUndo();
            EndOp();
            EditChanged();
        }
        else
        {
            op.Restore();
            if (!op.FromDuplicate) PopUndo();
            EndOp();
        }
        Status = op.FromExtrude ? "キャンセル（押し出した面は同じ位置に残っています。Ctrl+Z で取り消し）" : "キャンセル";
        Emit("cancel", ("kind", op.Kind.Name()), ("edit", op.Edit), ("extrude", ExtrudeFlag(op)));
    }

    /// <summary>JS の op.fromExtrude(差し込み・ベベル・位置調整の op には無い = undefined なので、イベントにも入らない)。</summary>
    private static bool? ExtrudeFlag(TransformOp op) => op.Kind.IsTransform() ? op.FromExtrude : null;

    private void ConfirmOp()
    {
        var op = Op;
        if (op is null) return;
        EndOp();
        if (op.Edit) EditChanged();
        Status = "確定";
        Emit("confirm", ("kind", op.Kind.Name()), ("axis", op.Axis), ("exclude", op.Exclude), ("value", op.Numeric), ("edit", op.Edit), ("extrude", ExtrudeFlag(op)), ("centered", op.Kind == TransformKind.Slide && op.Numeric == "0"));
    }

    private void SetAxis(string axis)
    {
        var op = Op;
        if (op is null || !op.Kind.IsTransform()) return;
        op.SetAxis(axis, keyShift);
        ApplyOp();
        Emit("axis", ("kind", op.Kind.Name()), ("axis", op.Axis), ("exclude", op.Exclude));
    }

    private void TypeNumeric(string text)
    {
        var op = Op;
        if (op is null) return;
        op.TypeNumeric(text);
        ApplyOp();
        Emit("numeric", ("kind", op.Kind.Name()), ("value", op.Numeric));
    }

    /// <summary>マウスの位置・軸固定・数値入力から、選択物のトランスフォームを更新する。</summary>
    private void ApplyOp()
    {
        var op = Op;
        if (op is null) return;
        var (px, py) = MousePx();
        var result = op.Apply(new OpInput(View, mouseNdcX, mouseNdcY, px, py, Rect.Width, Rect.Height, keyShift, keyCtrl));
        if (op.Edit) Rebuild(op.EditObject!);
        AxisLine = result.Line;
        Status = result.Status;
        NumericHud = result.NumericHud;
        UpdateNPanel();
    }

    // ---------------------------------------------------------------- キー
    public void KeyUp(Modifiers m)
    {
        keyShift = m.Shift; keyCtrl = m.Ctrl;
        if (Op is not null) ApplyOp();
    }

    /// <summary>
    /// キーが押された(KeyboardEvent の code と key)。現行の onKey と同じ順に判定して処理する。
    /// 戻り値は、現行が preventDefault を呼ぶ場合に true(ブラウザの既定動作を止める。呼ばないキーは false)。
    /// </summary>
    public bool KeyDown(string code, string key, Modifiers m)
    {
        keyShift = m.Shift; keyCtrl = m.Ctrl;
        Emit("key", ("code", code), ("key", key), ("shift", m.Shift), ("ctrl", m.Ctrl || m.Meta), ("alt", m.Alt));
        var emulate = EmulateNumpad();
        string? numpad = code.StartsWith("Numpad", StringComparison.Ordinal) ? code[6..]
            : emulate && code.StartsWith("Digit", StringComparison.Ordinal) ? code[5..]
            : emulate && key == "." ? "Decimal" : null;
        var mod = m.Ctrl || m.Meta;

        // ループカットのプレビュー中
        if (LoopCut is not null)
        {
            if (code == "Escape") { EndLoopCut(); Status = "ループカットをやめました"; Emit("cancel", ("kind", "loopcut")); }
            else if (code is "Enter" or "NumpadEnter") CommitLoopCut();
            return true;
        }

        // 操作中のキー
        if (Op is not null)
        {
            if (code == "Escape") CancelOp();
            else if (code is "Enter" or "NumpadEnter") ConfirmOp();
            else if (code is "KeyX" or "KeyY" or "KeyZ") SetAxis(code[3..].ToLowerInvariant());
            else if (IsDigitCode(code)) TypeNumeric(code[^1..]);
            else if (key == "." || code == "NumpadDecimal") TypeNumeric(".");
            else if (key == "-" || code == "NumpadSubtract") TypeNumeric("-");
            else if (code == "Backspace") TypeNumeric("Backspace");
            else if (code is "ShiftLeft" or "ShiftRight" or "ControlLeft" or "ControlRight") ApplyOp();
            return true;
        }

        // 視点
        if (numpad is not null)
        {
            if (Mode == "edit" && mod && (numpad == "Add" || numpad == "Subtract")) { GrowShrink(numpad == "Add"); return true; }
            return NumpadView(numpad, m.Ctrl);
        }
        if (code == "Home")
        {
            Frame(Objects);
            EmitView("home");
            return true;
        }

        if (mod)
        {
            var handled = true;
            if (code == "KeyZ") { if (m.Shift) Redo(); else Undo(); }
            else if (code == "KeyI") InvertSelection();
            else if (code == "KeyR" && Mode == "edit") StartLoopCut();
            else if (code == "KeyB" && Mode == "edit") Bevel();
            else if (code == "KeyL" && Mode == "edit") SelectLinked(false);
            else handled = false;
            return handled;
        }

        if (code == "Tab") { ToggleEditMode(); return true; }
        if (code == "KeyZ" && m.Alt) { SetXray(); return true; }

        if (Mode == "edit")
        {
            if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) && code[5] is >= '1' and <= '3')
            {
                SetSelectMode(code[5] switch { '1' => "vert", '2' => "edge", _ => "face" });
                return true;
            }
            switch (code)
            {
                case "KeyE": Extrude(); break;
                case "KeyI": Inset(); break;
                case "KeyF": FillEdit(); break;
                case "KeyM": ShowMergeMenu(); break;
                case "KeyP": ShowSeparateMenu(); break;
                case "KeyL": SelectLinked(true); break;
                case "KeyB": Status = "ボックス選択: ドラッグで囲む（Shift+ドラッグで追加）"; break;
                case "KeyD":
                    if (m.Shift) { Status = "編集モードの複製（Shift+D）には対応していません"; break; }
                    return false;
                case "KeyK": Status = "ナイフ（K）には対応していません"; break;
                case "KeyH": Status = "隠す（H）には対応していません"; break;
                default: return CommonKey(code, m);
            }
            return true;
        }
        return CommonKey(code, m);
    }

    /// <summary>オブジェクトモードと編集モードで共通のキー。</summary>
    private bool CommonKey(string code, Modifiers m)
    {
        switch (code)
        {
            case "KeyG": StartOp(TransformKind.G); break;
            case "KeyR": StartOp(TransformKind.R); break;
            case "KeyS": StartOp(TransformKind.S); break;
            case "KeyA":
                if (m.Shift) ShowAddMenu();
                else SelectAll(!m.Alt);
                break;
            case "KeyD":
                if (m.Shift) Duplicate();
                break;
            case "KeyX": ShowDeleteMenu(); break;
            case "Delete": if (Mode == "edit") ShowDeleteMenu(); else DeleteSelected(); break;
            case "KeyN": ToggleNPanel(); break;
            case "KeyB": Status = "ボックス選択: ドラッグで囲む"; break;
            case "Escape": CloseMenu(); break;
            default: return false;
        }
        return true;
    }

    /// <summary>"Digit0"〜"Digit9" と "Numpad0"〜"Numpad9"。</summary>
    private static bool IsDigitCode(string code) =>
        code.Length > 0 && char.IsAsciiDigit(code[^1]) && (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) || code.Length == 7 && code.StartsWith("Numpad", StringComparison.Ordinal));

    // ---------------------------------------------------------------- 視点
    /// <summary>テンキーの視点切り替え。key は "1" / "3" / "7" / "5" / "Decimal"。切り替えたら true(preventDefault)。</summary>
    private bool NumpadView(string key, bool ctrl)
    {
        if (key == "Decimal")
        {
            Frame(FrameTargets());
            EmitView("frame");
            return true;
        }
        if (View.NumpadAxis(key, ctrl))
        {
            EmitView(key == "5" ? "ortho" : "axis");
            return true;
        }
        return false;
    }

    /// <summary>フレーミングの対象: 編集モードで頂点を選んでいればその点、なければ選択したオブジェクト(なければ全部)。</summary>
    private IReadOnlyList<object> FrameTargets()
    {
        if (Mode == "edit" && EditObj!.Em.Vsel.Count > 0)
            return [.. EditObj.Em.Vsel.Select(v => (object)WorldOf(v, EditObj))];
        return [.. (Selected.Count > 0 ? Selected : Objects)];
    }

    private void Frame(IReadOnlyList<object> targets)
    {
        if (targets.Count == 0) return;
        if (targets[0] is Vec3) View.Frame([.. targets.Cast<Vec3>()]);
        else View.Frame([.. targets.Cast<SceneObject>()], EditObj);
    }

    private void Frame(IReadOnlyList<SceneObject> items) => View.Frame(items, EditObj);

    /// <summary>視点を回す(ラジアン)。チュートリアルのスキップ用。</summary>
    public void OrbitBy(double dTheta, double dPhi = 0) => View.OrbitBy(dTheta, dPhi);

    public ViewState GetView() => new(View.Target.ToArray(), View.Dist, View.Theta, View.Phi, View.Ortho, View.Name);

    public void SetView(ViewState v)
    {
        View.Target = new Vec3(v.Target[0], v.Target[1], v.Target[2]);
        View.Dist = v.Dist;
        View.Theta = v.Theta;
        View.Phi = v.Phi;
        View.Name = string.IsNullOrEmpty(v.Name) ? "ユーザー" : v.Name;
        View.Ortho = v.Ortho;
    }

    /// <summary>"x" / "y" / "z" のオブジェクト名で選択(チュートリアルのスキップ用)ではなく、名前でオブジェクトを探す。</summary>
    public SceneObject? Find(string name) => Objects.FirstOrDefault(o => o.Name == name);

    private static string RandomId()
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        Span<byte> bytes = stackalloc byte[8];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return string.Create(8, bytes.ToArray(), (span, b) => { for (var i = 0; i < 8; i++) span[i] = digits[b[i] % 36]; });
    }

    private Func<string> NewId { get; }
}
