namespace FaraAvatarModdingGuide.Practice;

/// <summary>ビューポートが出すイベント(viewport.js の emit)。Detail は JavaScript のイベントオブジェクトと同じ名前の値。</summary>
public sealed record ViewEvent(string Type, IReadOnlyDictionary<string, object?> Detail);

/// <summary>ポインターやキーの修飾キー。</summary>
public readonly record struct Modifiers(bool Shift = false, bool Ctrl = false, bool Alt = false, bool Meta = false);

/// <summary>3D ビューが画面のどこにあるか(getBoundingClientRect と同じ。ページ上の座標)。</summary>
public readonly record struct ViewRect(double Left, double Top, double Width, double Height);

/// <summary>
/// オブジェクトモードの 3D ビューの操作(viewport.js の bind / onKey / onDrag / finishBox / select / startOp / confirmOp / cancelOp と、そこから呼ぶもの)。
/// 描画も DOM も持たない。入力(ポインター・ホイール・キー)を受けて、状態(オブジェクト・選択・視点・操作中のようす)と、表示用の文字・イベントを更新する。
/// 現行の JavaScript と同じ入力で同じ結果になることを、tests/FaraAvatarModdingGuide.Practice.Tests が確かめる。
///
/// この段階で対応するのは、クリック・Shift+クリック・ボックスでの選択、中ボタン(Alt+左)での回転・移動・ズーム、ホイール、
/// G / R / S(軸・数値・Ctrl の刻み・Shift の細かい操作・確定・キャンセル)、テンキー(「テンキーを模倣」を含む)の視点、Home・A・Alt+A・Ctrl+I。
/// 追加(Shift+A)・複製・削除・取り消し・編集モード・メニュー・N パネル・ループカットは後の段階(<see cref="KeyDown"/> は false を返すので、呼び側が JS に任せられる)。
/// </summary>
public sealed class ViewportCore
{
    private enum DragKind { Orbit, Pan, Zoom, Box }

    private sealed class Drag
    {
        public DragKind Kind;
        public double LastX, LastY, StartX, StartY;
        public double Distance;
        public bool Shift, Moved;
    }

    private readonly List<ViewEvent> events = [];
    private Drag? drag;
    private double mouseNdcX, mouseNdcY, mouseClientX, mouseClientY;
    private bool keyShift, keyCtrl;

    public ViewportCore(ViewRect rect, Func<bool>? emulateNumpad = null)
    {
        Rect = rect;
        EmulateNumpad = emulateNumpad ?? (() => false);
        View.Aspect = rect.Width / rect.Height;
        View.HeightPx = rect.Height;
        mouseClientX = rect.Left + rect.Width / 2;
        mouseClientY = rect.Top + rect.Height / 2;
    }

    public ViewRect Rect { get; private set; }
    public Func<bool> EmulateNumpad { get; set; }

    public OrbitView View { get; } = new();
    public List<SceneObject> Objects { get; } = [];
    public List<SceneObject> Selected { get; private set; } = [];
    public SceneObject? Active { get; private set; }
    public TransformOp? Op { get; private set; }
    public OpResult? LastOp { get; private set; }

    /// <summary>左下の状態表示(setStatus)。</summary>
    public string Status { get; private set; } = "";
    /// <summary>数値入力の表示("入力: 2")。</summary>
    public string NumericHud { get; private set; } = "";
    /// <summary>軸固定の線(操作中だけ表示)。</summary>
    public AxisLine AxisLine { get; private set; } = new(false, Vec3.Zero, 0);
    /// <summary>取り消しに積んだ数(取り消し本体は後の段階。キャンセルで戻す操作の数合わせ)。</summary>
    public int UndoDepth { get; private set; }

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
        View.Aspect = rect.Width / rect.Height;
        View.HeightPx = rect.Height;
    }

    public void SetSelection(IEnumerable<SceneObject> selected)
    {
        Selected = [.. selected];
        Active = Selected.Count > 0 ? Selected[^1] : null;
    }

    private void Emit(string type, params (string Key, object? Value)[] detail) =>
        events.Add(new ViewEvent(type, detail.ToDictionary(d => d.Key, d => d.Value)));

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

    private SceneObject? PickAt(double clientX, double clientY)
    {
        var (nx, ny) = Ndc(clientX, clientY);
        return Picking.Pick(View.RayFromNdc(nx, ny), Objects);
    }

    // ---------------------------------------------------------------- ポインター
    public void PointerMove(double clientX, double clientY, Modifiers m)
    {
        var (nx, ny) = Ndc(clientX, clientY);
        mouseNdcX = nx; mouseNdcY = ny; mouseClientX = clientX; mouseClientY = clientY;
        keyShift = m.Shift; keyCtrl = m.Ctrl;
        if (Op is not null) { ApplyOp(); return; }
        if (drag is not null) OnDrag(clientX, clientY);
    }

    /// <summary>button: 0 左・1 中・2 右。</summary>
    public void PointerDown(int button, double clientX, double clientY, Modifiers m)
    {
        if (Op is not null)
        {
            if (button == 0) ConfirmOp();
            else if (button == 2) CancelOp();
            return;
        }
        var (px, py) = Pixel(clientX, clientY);
        if (button == 1 || (button == 0 && m.Alt))
            drag = new Drag { Kind = m.Shift ? DragKind.Pan : m.Ctrl ? DragKind.Zoom : DragKind.Orbit, LastX = px, LastY = py };
        else if (button == 0)
            drag = new Drag { Kind = DragKind.Box, StartX = px, StartY = py, Shift = m.Shift };
    }

    public void PointerUp(double clientX, double clientY)
    {
        var d = drag;
        drag = null;
        if (d is null) return;
        if (d.Kind == DragKind.Box)
        {
            if (d.Moved) FinishBox(d, clientX, clientY);
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

    public void Wheel(double deltaY)
    {
        View.ZoomWheel(deltaY);
        EmitView("zoom", ("delta", (double)Js.Sign(deltaY)));
    }

    private void OnDrag(double clientX, double clientY)
    {
        var d = drag!;
        var (px, py) = Pixel(clientX, clientY);
        if (d.Kind != DragKind.Box) d.Distance += Math.Sqrt((px - d.LastX) * (px - d.LastX) + (py - d.LastY) * (py - d.LastY));
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
                if (!d.Moved && Math.Sqrt((px - d.StartX) * (px - d.StartX) + (py - d.StartY) * (py - d.StartY)) < 4) return;
                d.Moved = true;
                return;
        }
        d.LastX = px; d.LastY = py;
    }

    private void FinishBox(Drag d, double clientX, double clientY)
    {
        var (px, py) = Pixel(clientX, clientY);
        double x0 = Math.Min(px, d.StartX), x1 = Math.Max(px, d.StartX), y0 = Math.Min(py, d.StartY), y1 = Math.Max(py, d.StartY);
        if (!d.Shift) Selected = [];
        foreach (var o in Objects)
        {
            var (sx, sy) = View.ToScreen(o.Position, Rect.Width, Rect.Height);
            if (sx >= x0 && sx <= x1 && sy >= y0 && sy <= y1 && !Selected.Contains(o)) { Selected.Add(o); Active = o; }
        }
        if (Selected.Count == 0) Active = null;
        Status = $"{Selected.Count} 個を選択";
        EmitSelect("box");
    }

    // ---------------------------------------------------------------- 選択
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
    }

    private void EmitSelect(string source) =>
        Emit("select", ("source", source), ("selected", Selected.Select(o => o.Name).ToArray()), ("active", Active?.Name));

    private void SelectAll(bool all)
    {
        if (all)
        {
            Selected = [.. Objects];
            Active = Selected.Count > 0 ? Selected[^1] : null;
        }
        else
        {
            Selected = [];
            Active = null;
        }
        Status = all ? "全選択" : "選択解除";
        EmitSelect(all ? "all" : "none");
    }

    private void InvertSelection()
    {
        Selected = [.. Objects.Where(o => !Selected.Contains(o))];
        Active = Selected.Count > 0 ? Selected[^1] : null;
        Status = "選択を反転";
        EmitSelect("invert");
    }

    // ---------------------------------------------------------------- 操作(G / R / S)
    private void StartOp(TransformKind kind)
    {
        var name = kind.ToString();
        if (Selected.Count == 0)
        {
            Status = "先にオブジェクトを選択してください";
            Emit("opfail", ("kind", name), ("reason", "noselection"));
            return;
        }
        UndoDepth++;
        var (px, py) = MousePx();
        Op = new TransformOp(kind, Selected, mouseNdcX, mouseNdcY, px, py);
        ApplyOp();
        Emit("opstart", ("kind", name), ("edit", false), ("extrude", false));
    }

    private void ApplyOp()
    {
        var op = Op;
        if (op is null) return;
        var (px, py) = MousePx();
        var result = op.Apply(new OpInput(View, mouseNdcX, mouseNdcY, px, py, Rect.Width, Rect.Height, keyShift, keyCtrl));
        LastOp = result;
        AxisLine = result.Line;
        Status = result.Status;
        NumericHud = result.NumericHud;
    }

    private void EndOp()
    {
        Op = null;
        NumericHud = "";
        AxisLine = new AxisLine(false, Vec3.Zero, 0);
    }

    private void ConfirmOp()
    {
        var op = Op;
        if (op is null) return;
        EndOp();
        Status = "確定";
        Emit("confirm", ("kind", op.Kind.ToString()), ("axis", op.Axis), ("exclude", op.Exclude), ("value", op.Numeric), ("edit", false), ("extrude", op.FromExtrude), ("centered", false));
    }

    private void CancelOp()
    {
        var op = Op;
        if (op is null) return;
        op.Restore();
        if (!op.FromDuplicate && UndoDepth > 0) UndoDepth--;
        EndOp();
        Status = op.FromExtrude ? "キャンセル（押し出した面は同じ位置に残っています。Ctrl+Z で取り消し）" : "キャンセル";
        Emit("cancel", ("kind", op.Kind.ToString()), ("edit", false), ("extrude", op.FromExtrude));
    }

    private void SetAxis(string axis)
    {
        var op = Op;
        if (op is null) return;
        op.SetAxis(axis, keyShift);
        ApplyOp();
        Emit("axis", ("kind", op.Kind.ToString()), ("axis", op.Axis), ("exclude", op.Exclude));
    }

    private void TypeNumeric(string text)
    {
        var op = Op;
        if (op is null) return;
        op.TypeNumeric(text);
        ApplyOp();
        Emit("numeric", ("kind", op.Kind.ToString()), ("value", op.Numeric));
    }

    // ---------------------------------------------------------------- キー
    public void KeyUp(Modifiers m)
    {
        keyShift = m.Shift; keyCtrl = m.Ctrl;
        if (Op is not null) ApplyOp();
    }

    /// <summary>
    /// キーが押された(KeyboardEvent の code と key)。この段階で扱えるキーなら処理して true。
    /// 扱えないキー(追加・複製・削除・取り消し・編集モードなど)は false を返し、状態は keydown のイベント以外変えない(呼び側が現行の JS に任せる)。
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
            if (numpad == "Decimal")
            {
                View.Frame(Selected.Count > 0 ? Selected : Objects);
                EmitView("frame");
            }
            else if (View.NumpadAxis(numpad, m.Ctrl))
            {
                EmitView(numpad == "5" ? "ortho" : "axis");
            }
            return true;
        }
        if (code == "Home")
        {
            View.Frame(Objects);
            EmitView("home");
            return true;
        }

        if (mod)
        {
            if (code == "KeyI") { InvertSelection(); return true; }
            return code != "KeyZ"; // Ctrl+Z(取り消し)は後の段階。それ以外の Ctrl の組み合わせは、現行でも何もしない
        }

        switch (code)
        {
            case "KeyG": StartOp(TransformKind.G); return true;
            case "KeyR": StartOp(TransformKind.R); return true;
            case "KeyS": StartOp(TransformKind.S); return true;
            case "KeyA":
                if (m.Shift) return false; // 追加メニュー
                SelectAll(!m.Alt);
                return true;
            case "KeyB": Status = "ボックス選択: ドラッグで囲む"; return true;
            case "Escape": return true;
            default: return false;
        }
    }

    /// <summary>"Digit0"〜"Digit9" と "Numpad0"〜"Numpad9"。</summary>
    private static bool IsDigitCode(string code) =>
        code.Length > 0 && char.IsAsciiDigit(code[^1]) && (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) || code.Length == 7 && code.StartsWith("Numpad", StringComparison.Ordinal));
}
