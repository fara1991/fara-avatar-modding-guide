using System.Numerics;

namespace Csharp3d.Shared;

/// <summary>
/// G（移動）の操作。viewport.js の startOp / applyOp（G の部分）と同じ流れ:
/// 開始（選択の位置を記録し、中心を決める）→ 軸（X/Y/Z）→ 数値入力 → マウスで動かす → Enter で確定 / Esc・右クリックで取り消し。
/// </summary>
public sealed class MoveOp
{
    readonly SceneObject[] objects;
    readonly Vector3[] start;
    public readonly Vector3 Pivot;
    public string? Axis { get; private set; }
    public string Numeric { get; private set; } = "";
    public Vector3 Delta { get; private set; }
    readonly Picking.Ray startRay;
    readonly Vector3 viewNormal;

    public MoveOp(IReadOnlyList<SceneObject> selected, Picking.Ray startRay, Vector3 viewNormal)
    {
        objects = selected.ToArray();
        start = objects.Select(o => o.Position).ToArray();
        Pivot = objects.Aggregate(Vector3.Zero, (acc, o) => acc + o.Position) / objects.Length;
        this.startRay = startRay;
        this.viewNormal = viewNormal;
    }

    public void SetAxis(string axis)
    {
        Axis = Axis == axis ? null : axis; // 同じ軸をもう一度押すと解除
    }

    public void Type(string ch)
    {
        if (ch == "Backspace") { if (Numeric.Length > 0) Numeric = Numeric[..^1]; }
        else if (ch == "-" && Numeric.Length == 0) Numeric = "-";
        else if (ch == "." && !Numeric.Contains('.')) Numeric += ".";
        else if (char.IsDigit(ch[0])) Numeric += ch;
    }

    /// <summary>数値入力が空、または "-" や "." だけなら null（マウスの動きを使う）</summary>
    float? NumericValue()
    {
        if (Numeric is "" or "-" or ".") return null;
        return float.TryParse(Numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>現在のマウスのレイから、移動量を計算し、オブジェクトへ反映する</summary>
    public void Recalc(Picking.Ray? cur)
    {
        var now = cur ?? startRay;
        var p0 = Picking.RayPlane(startRay, Pivot, viewNormal);
        var p1 = Picking.RayPlane(now, Pivot, viewNormal);
        var delta = p1 - p0;
        if (Axis is { } ax)
        {
            var a = AxisVec(ax);
            delta = a * Vector3.Dot(delta, a);
        }
        if (NumericValue() is { } n)
        {
            delta = AxisVec(Axis ?? "x") * n;
        }
        Delta = delta;
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].Position = start[i] + delta;
        }
    }

    public static Vector3 AxisVec(string axis) => axis switch
    {
        "x" => Vector3.UnitX,
        "y" => Vector3.UnitY,
        _ => Vector3.UnitZ,
    };

    public void Restore()
    {
        for (int i = 0; i < objects.Length; i++) objects[i].Position = start[i];
    }

    public string Label => Axis is { } a
        ? $"移動  {a.ToUpperInvariant()} 方向  距離: {Delta.Length():0.000} m"
        : $"移動  （X / Y / Z で軸固定）  {Delta.Length():0.000} m";
}

/// <summary>練習のお題（practice.js の TASKS の一部。2 つ分）。Check は現在の状態を見て達成かどうか</summary>
public sealed class PracticeTask
{
    public required string Text { get; init; }
    public required Func<Practice3D, bool> Check { get; init; }
}

public readonly record struct DrawCall(Geometry.Mesh Mesh, Matrix4x4 World);

/// <summary>
/// 練習ビューの状態と操作（viewport.js の一部 + practice.js の判定）。描画は持たない。
/// 描画側（KNI / WebGL）は Draws を順に描き、入力は Pointer* / KeyDown などを呼ぶ。
/// </summary>
public sealed class Practice3D
{
    public Scene Scene { get; } = new();
    public OrbitCamera Cam { get; } = new();
    public MoveOp? Op { get; private set; }
    public string Status { get; private set; } = "";
    public float Width { get; private set; } = 1;
    public float Height { get; private set; } = 1;
    public event Action? Changed;

    public static readonly PracticeTask[] Tasks =
    {
        new() { Text = "立方体をクリックして選択する", Check = s => s.Scene.Objects.Any(o => o.Kind == ObjectKind.Cube && o.Selected) },
        new() { Text = "G → Z → 2 → Enter で、立方体を上に 2 m 動かす", Check = s => s.Scene.Objects.Any(o => o.Kind == ObjectKind.Cube && Near(o.Position.Z, 2)) },
    };
    public int TaskIndex { get; private set; }
    public bool[] TaskDone { get; } = new bool[Tasks.Length];

    public Practice3D()
    {
        Scene.AddCube();
        Status = "立方体をクリックして選択してください";
    }

    static bool Near(float a, float b, float eps = 0.05f) => MathF.Abs(a - b) <= eps;

    public void Resize(float w, float h)
    {
        if (w <= 0 || h <= 0) return;
        Width = w;
        Height = h;
    }

    public Matrix4x4 View => Cam.View();
    public Matrix4x4 Proj => OrbitCamera.Projection(Width / Height);

    /// <summary>描画する順番の一覧（床・軸・オブジェクト・選択の枠）</summary>
    public IEnumerable<DrawCall> Draws()
    {
        yield return new DrawCall(Geometry.Mesh.Grid, Matrix4x4.Identity);
        yield return new DrawCall(Geometry.Mesh.AxisX, Matrix4x4.Identity);
        yield return new DrawCall(Geometry.Mesh.AxisY, Matrix4x4.Identity);
        foreach (var o in Scene.Objects)
        {
            yield return new DrawCall(o.Mesh, o.World());
        }
        foreach (var o in Scene.Objects.Where(o => o.Selected))
        {
            yield return new DrawCall(Geometry.Mesh.Outline, o.World());
        }
    }

    // ---------------------------------------------------------------- ポインター
    int dragButton = -1;
    bool dragMoved;
    float lastX, lastY, startX, startY;
    float mouseX, mouseY;

    public void PointerDown(int button, float x, float y, bool shift, bool alt, bool ctrl)
    {
        mouseX = x; mouseY = y;
        if (Op is not null)
        {
            if (button == 0) ConfirmOp();
            else if (button == 2) CancelOp();
            return;
        }
        if (button == 1 || (button == 0 && alt))
        {
            dragButton = 1;
            dragMode = shift ? DragMode.Pan : ctrl ? DragMode.Zoom : DragMode.Orbit;
        }
        else if (button == 0)
        {
            dragButton = 0;
            dragMode = DragMode.Box;
            shiftHeld = shift;
        }
        dragMoved = false;
        startX = lastX = x;
        startY = lastY = y;
    }

    enum DragMode { None, Orbit, Pan, Zoom, Box }
    DragMode dragMode = DragMode.None;
    bool shiftHeld;

    public void PointerMove(float x, float y)
    {
        mouseX = x; mouseY = y;
        if (Op is not null)
        {
            Op.Recalc(RayAt(x, y));
            Status = Op.Label;
            Changed?.Invoke();
            return;
        }
        if (dragButton < 0) return;
        float dx = x - lastX, dy = y - lastY;
        lastX = x; lastY = y;
        if (MathF.Abs(x - startX) + MathF.Abs(y - startY) > 3) dragMoved = true;
        switch (dragMode)
        {
            case DragMode.Orbit:
                Cam.Orbit(-dx * 0.01f, -dy * 0.01f);
                break;
            case DragMode.Pan:
                Cam.Pan(dx, dy, Height);
                break;
            case DragMode.Zoom:
                Cam.Zoom(MathF.Exp(dy * 0.01f));
                break;
        }
        if (dragMode != DragMode.Box) Changed?.Invoke();
    }

    public void PointerUp(int button, float x, float y)
    {
        var mode = dragMode;
        dragButton = -1;
        dragMode = DragMode.None;
        if (mode == DragMode.Box && button == 0 && Op is null && !dragMoved)
        {
            var hit = Scene.Pick(RayAt(x, y));
            Select(hit, shiftHeld);
            Status = hit is null ? "選択解除" : $"{hit.Name} を選択";
            Changed?.Invoke();
            CheckTasks();
        }
    }

    public void Wheel(float deltaY)
    {
        Cam.Zoom(deltaY > 0 ? 1.12f : 1 / 1.12f);
        Changed?.Invoke();
    }

    Picking.Ray RayAt(float x, float y) => Picking.ScreenRay(x, y, Width, Height, View, Proj);

    void Select(SceneObject? hit, bool extend)
    {
        if (!extend) foreach (var o in Scene.Objects) o.Selected = false;
        if (hit is not null) hit.Selected = extend ? !hit.Selected : true;
    }

    // ---------------------------------------------------------------- キー
    /// <summary>code は KeyboardEvent.code と同じ形（"KeyG", "Digit2", "Numpad2", "Enter", "Escape"...）</summary>
    public void KeyDown(string code)
    {
        if (Op is not null)
        {
            switch (code)
            {
                case "Escape": CancelOp(); return;
                case "Enter" or "NumpadEnter": ConfirmOp(); return;
                case "KeyX": Op.SetAxis("x"); break;
                case "KeyY": Op.SetAxis("y"); break;
                case "KeyZ": Op.SetAxis("z"); break;
                case "Backspace": Op.Type("Backspace"); break;
                case "Period" or "NumpadDecimal": Op.Type("."); break;
                case "Minus" or "NumpadSubtract": Op.Type("-"); break;
                default:
                    if (TryDigit(code, out var d)) Op.Type(d);
                    break;
            }
            Op.Recalc(RayAt(mouseX, mouseY));
            Status = Op.Label;
            Changed?.Invoke();
            return;
        }
        if (code == "KeyG") StartMove();
    }

    static bool TryDigit(string code, out string d)
    {
        d = "";
        if (code.StartsWith("Digit") || code.StartsWith("Numpad"))
        {
            var last = code[^1];
            if (char.IsDigit(last)) { d = last.ToString(); return true; }
        }
        return false;
    }

    void StartMove()
    {
        var selected = Scene.Objects.Where(o => o.Selected).ToList();
        if (selected.Count == 0)
        {
            Status = "先にオブジェクトを選択してください";
            Changed?.Invoke();
            return;
        }
        var eye = Cam.Eye;
        var normal = Vector3.Normalize(Cam.Target - eye);
        Op = new MoveOp(selected, RayAt(mouseX, mouseY), normal);
        Status = Op.Label;
        Changed?.Invoke();
    }

    void ConfirmOp()
    {
        Op = null;
        Status = "移動を確定しました";
        Changed?.Invoke();
        CheckTasks();
    }

    void CancelOp()
    {
        Op?.Restore();
        Op = null;
        Status = "移動をやめました";
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- お題
    public void CheckTasks()
    {
        bool changed = false;
        while (TaskIndex < Tasks.Length && Tasks[TaskIndex].Check(this))
        {
            TaskDone[TaskIndex] = true;
            TaskIndex++;
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }
}
