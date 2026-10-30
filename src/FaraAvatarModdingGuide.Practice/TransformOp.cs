namespace FaraAvatarModdingGuide.Practice;

/// <summary>操作の種類。G(移動)・R(回転)・S(スケール)・I(差し込み)・B(ベベル)・Slide(ループカットの位置)。イベントの kind は <see cref="TransformKindExtensions.Name"/>。</summary>
public enum TransformKind { G, R, S, I, B, Slide }

public static class TransformKindExtensions
{
    /// <summary>viewport.js の op.kind の文字("G" / "R" / "S" / "I" / "B" / "slide")。</summary>
    public static string Name(this TransformKind kind) => kind == TransformKind.Slide ? "slide" : kind.ToString();

    /// <summary>軸を固定できる操作(G / R / S)。</summary>
    public static bool IsTransform(this TransformKind kind) => kind is TransformKind.G or TransformKind.R or TransformKind.S;
}

/// <summary>操作を始めた時点のオブジェクトの状態(キャンセルで戻す)。</summary>
public sealed record StartState(SceneObject Object, Vec3 Position, Quat Quaternion, Vec3 Scale);

/// <summary>編集モードで操作を始めた時点の頂点と、そのワールド座標(キャンセルで戻す)。</summary>
public sealed record EditStart(EmVert V, Vec3 W);

/// <summary>1 回の計算(applyOp)の入力: マウスと修飾キーと、視点。</summary>
public sealed record OpInput(OrbitView View, double MouseNdcX, double MouseNdcY, double MousePxX, double MousePxY, double ViewWidthPx, double ViewHeightPx, bool Shift, bool Ctrl);

/// <summary>軸固定の線の表示(操作の中心に、その軸の向きで出す)。Direction は軸の向き(法線のときは法線)。</summary>
public sealed record AxisLine(bool Visible, Vec3 Direction, int Color);

/// <summary>操作の結果(表示用の文字と軸線)。</summary>
public sealed record OpResult(string Status, string NumericHud, AxisLine Line, Vec3 Delta, Quat Rotation, Vec3 Factor, Vec3 Pivot, TransformKind Kind);

/// <summary>
/// G / R / S / I / B / slide の操作の状態(viewport.js の op と startOp / startMoveOp / setAxis / typeNumeric / applyOp)。
/// 開始(選択の中心を決める)→ 軸(X / Y / Z。Shift で「それ以外」。もう一度で解除)→ 数値入力 → マウスで動かす(Ctrl で刻み、Shift で細かく)→ 確定 / キャンセル。
/// オブジェクトモードではオブジェクトの位置・回転・スケールを、編集モードでは選んだ頂点(ワールド座標で動かしてローカルへ戻す)を動かす。
/// 式・順序・表示の文字は現行の JavaScript と同じ。
/// </summary>
public sealed class TransformOp
{
    public static readonly string[] Axes = ["x", "y", "z"];

    /// <summary>軸の色(AXIS_COLOR)。x / y / z / n(法線)。</summary>
    public static int AxisColor(string axis) => axis switch { "x" => 0xff3352, "y" => 0x8bdc00, "z" => 0x2890ff, _ => 0xffd84a };

    /// <summary>オブジェクトモードの G / R / S。</summary>
    public TransformOp(TransformKind kind, IReadOnlyList<SceneObject> selected, double startNdcX, double startNdcY, double startPxX, double startPxY, Vec3? normal = null, bool fromExtrude = false, bool fromDuplicate = false)
    {
        Kind = kind;
        Start = [.. selected.Select(o => new StartState(o, o.Position, o.Quaternion, o.Scale))];
        var sum = Vec3.Zero;
        foreach (var o in selected) sum += o.Position;
        Pivot = sum.DivideScalar(selected.Count);
        Normal = normal;
        Axis = normal is null ? null : "n";
        FromExtrude = fromExtrude;
        FromDuplicate = fromDuplicate;
        StartNdcX = startNdcX; StartNdcY = startNdcY; StartPxX = startPxX; StartPxY = startPxY;
    }

    private TransformOp(TransformKind kind, SceneObject editObject, Vec3 pivot, double startNdcX, double startNdcY, double startPxX, double startPxY)
    {
        Kind = kind;
        Start = [];
        Edit = true;
        EditObject = editObject;
        Pivot = pivot;
        StartNdcX = startNdcX; StartNdcY = startNdcY; StartPxX = startPxX; StartPxY = startPxY;
    }

    /// <summary>編集モードの G / R / S(選んだ頂点を動かす)。pivot は選んだ頂点のワールド座標の平均。</summary>
    public static TransformOp ForEdit(TransformKind kind, SceneObject editObject, IReadOnlyList<EditStart> start, Vec3 pivot, double startNdcX, double startNdcY, double startPxX, double startPxY, Vec3? normal = null, bool fromExtrude = false)
    {
        var op = new TransformOp(kind, editObject, pivot, startNdcX, startNdcY, startPxX, startPxY)
        {
            EditStarts = start,
            Inverse = editObject.MatrixWorld.Inverted(),
            Normal = normal,
            Axis = normal is null ? null : "n",
            FromExtrude = fromExtrude,
        };
        return op;
    }

    /// <summary>差し込み(I)・ベベル(B)・ループカットの位置(slide)。頂点を base + dir × t(slide は a → b の補間)で動かす。pivot は動かす点のワールド座標の平均。</summary>
    public static TransformOp ForMoves(TransformKind kind, SceneObject editObject, IReadOnlyList<MoveVert>? moves, IReadOnlyList<SlideVert>? slide, int cuts, double limit, double startNdcX, double startNdcY, double startPxX, double startPxY)
    {
        var m = editObject.MatrixWorld;
        var pivot = Vec3.Zero;
        var count = moves?.Count ?? slide!.Count;
        if (moves is not null) foreach (var mv in moves) pivot += m.ApplyToPoint(mv.Base);
        else foreach (var s in slide!) pivot += m.ApplyToPoint(s.V.Co);
        pivot = pivot.DivideScalar(Math.Max(1, count));
        return new TransformOp(kind, editObject, pivot, startNdcX, startNdcY, startPxX, startPxY) { Moves = moves, Slide = slide, Cuts = cuts, Limit = limit };
    }

    public TransformKind Kind { get; }
    public IReadOnlyList<StartState> Start { get; }
    public Vec3 Pivot { get; }
    public Vec3? Normal { get; private set; }
    public bool FromExtrude { get; private set; }
    public bool FromDuplicate { get; set; }
    /// <summary>編集モードの操作か。</summary>
    public bool Edit { get; }
    public SceneObject? EditObject { get; }
    public IReadOnlyList<EditStart> EditStarts { get; private set; } = [];
    /// <summary>編集中のオブジェクトの matrixWorld の逆行列(操作を始めたとき)。</summary>
    public Mat4 Inverse { get; private set; }
    public IReadOnlyList<MoveVert>? Moves { get; private set; }
    public IReadOnlyList<SlideVert>? Slide { get; private set; }
    public int Cuts { get; private set; }
    public double Limit { get; private set; }

    /// <summary>"x" / "y" / "z" / "n"(法線)/ null(固定なし)。</summary>
    public string? Axis { get; private set; }
    /// <summary>true なら「その軸以外」(Shift+軸)。</summary>
    public bool Exclude { get; private set; }
    /// <summary>入力中の数値の文字("-"・"." の途中も含む)。</summary>
    public string Numeric { get; private set; } = "";

    public double StartNdcX { get; }
    public double StartNdcY { get; }
    public double StartPxX { get; }
    public double StartPxY { get; }

    /// <summary>ループカットの位置の調整をやめたとき(右クリック・Esc)は、ちょうど真ん中("0")にする。</summary>
    public void SetNumeric(string value) => Numeric = value;

    /// <summary>X / Y / Z で軸を固定する。同じ軸(と同じ Shift)をもう一度押すと解除(押し出しは法線に戻る)。</summary>
    public void SetAxis(string axis, bool exclude)
    {
        if (Axis == axis && Exclude == exclude)
        {
            Axis = Normal is not null ? "n" : null;
            Exclude = false;
        }
        else
        {
            Axis = axis;
            Exclude = exclude;
        }
    }

    /// <summary>数値入力。text は 1 文字(数字・"."・"-")か "Backspace"。</summary>
    public void TypeNumeric(string text)
    {
        if (text == "Backspace") Numeric = Numeric.Length > 0 ? Numeric[..^1] : "";
        else if (text == "-") Numeric = Numeric.StartsWith('-') ? Numeric[1..] : "-" + Numeric;
        else if (text == ".") { if (!Numeric.Contains('.')) Numeric += "."; }
        else Numeric += text;
    }

    /// <summary>数値にならないあいだ(空・"-"・"."・"-.")は null(マウスの動きを使う)。</summary>
    private double? NumericValue() => Js.ParseNumeric(Numeric);

    private Vec3 AxisVec(string a) => a == "n" ? Normal ?? Vec3.Zero : a switch { "x" => Vec3.UnitX, "y" => Vec3.UnitY, _ => Vec3.UnitZ };

    private static string AxisName(string a) => a == "n" ? "法線" : a.ToUpperInvariant();

    private static string FmtVec(Vec3 v, int digits = 3, string unit = " m") =>
        $"X {Js.ToFixed(v.X, digits)}{unit}  Y {Js.ToFixed(v.Y, digits)}{unit}  Z {Js.ToFixed(v.Z, digits)}{unit}";

    /// <summary>キャンセル(G / R / S): 選択物(頂点)を操作の前に戻す。</summary>
    public void Restore()
    {
        if (Edit)
        {
            foreach (var s in EditStarts) s.V.Co = Inverse.ApplyToPoint(s.W);
            EditObject!.Em.Changed();
            return;
        }
        foreach (var s in Start)
        {
            s.Object.Position = s.Position;
            s.Object.Quaternion = s.Quaternion;
            s.Object.Scale = s.Scale;
        }
    }

    /// <summary>
    /// マウスの位置・軸固定・数値入力から、選択物のトランスフォーム(編集モードでは頂点)を更新して、表示用の結果を返す。
    /// 編集モードでは呼び側が、あとで形(<see cref="SceneObject.Rebuild"/>)を作り直す。
    /// </summary>
    public OpResult Apply(OpInput input)
    {
        var view = input.View;
        bool snap = input.Ctrl, fine = input.Shift;
        var numeric = NumericValue();
        string text;
        var delta = Vec3.Zero;
        var rotation = Quat.Identity;
        var factor = Vec3.One;
        Func<Vec3, Vec3>? move = null; // ワールド座標の点を動かす関数(編集モード用)

        if (Kind == TransformKind.G)
        {
            delta = view.UnprojectOnPlane(input.MouseNdcX, input.MouseNdcY, Pivot) - view.UnprojectOnPlane(StartNdcX, StartNdcY, Pivot);
            if (fine) delta *= 0.1;
            if (Axis is not null && !Exclude)
            {
                var a = AxisVec(Axis);
                delta = a * delta.Dot(a);
            }
            else if (Axis is not null && Exclude && Axis != "n")
            {
                delta = delta.With(Axis, 0);
            }
            if (numeric is { } n)
            {
                var a = Axis is not null && !Exclude ? Axis : "x";
                delta = AxisVec(a) * n;
            }
            else if (snap)
            {
                var step = fine ? 0.1 : 1;
                if (Axis == "n" && Normal is { } normal) delta = normal * (Js.Round(delta.Dot(normal) / step) * step);
                else delta = new Vec3(Js.Round(delta.X / step) * step, Js.Round(delta.Y / step) * step, Js.Round(delta.Z / step) * step);
            }
            var d = delta;
            if (Edit) move = w => w + d;
            else foreach (var s in Start) s.Object.Position = s.Position + delta;
            var label = Axis is not null ? (Exclude ? $"{Axis.ToUpperInvariant()} 以外" : AxisName(Axis)) : "";
            text = $"{(FromExtrude ? "押し出し" : "移動")} {label}: {(Axis == "n" && Normal is { } nn ? $"{Js.ToFixed(delta.Dot(nn), 3)} m" : FmtVec(delta))}";
        }
        else if (Kind == TransformKind.R)
        {
            var (px, py) = view.ToScreen(Pivot, input.ViewWidthPx, input.ViewHeightPx);
            var a0 = Math.Atan2(StartPxY - py, StartPxX - px);
            var a1 = Math.Atan2(input.MousePxY - py, input.MousePxX - px);
            var angle = -(a1 - a0);
            if (fine) angle *= 0.1;
            var axis = Axis is not null && !Exclude ? AxisVec(Axis) : -view.Forward;
            if (numeric is { } n) angle = n * Js.Deg;
            else if (snap) angle = Js.Round(angle / (5 * Js.Deg)) * 5 * Js.Deg;
            rotation = Quat.FromAxisAngle(axis, angle);
            var q = rotation;
            if (Edit) move = w => q.Rotate(w - Pivot) + Pivot;
            else foreach (var s in Start)
            {
                s.Object.Quaternion = rotation * s.Quaternion;
                s.Object.Position = rotation.Rotate(s.Position - Pivot) + Pivot;
            }
            text = $"回転 {(Axis is not null && !Exclude ? AxisName(Axis) : "")}: {Js.ToFixed(angle / Js.Deg, 2)}°";
        }
        else if (Kind == TransformKind.S)
        {
            var (px, py) = view.ToScreen(Pivot, input.ViewWidthPx, input.ViewHeightPx);
            var d0 = Math.Max(1, Math.Sqrt((StartPxX - px) * (StartPxX - px) + (StartPxY - py) * (StartPxY - py)));
            var d1 = Math.Sqrt((input.MousePxX - px) * (input.MousePxX - px) + (input.MousePxY - py) * (input.MousePxY - py));
            var f = d1 / d0;
            if (fine) f = 1 + (f - 1) * 0.1;
            if (numeric is { } n) f = n;
            else if (snap) f = Js.Round(f * 10) / 10;
            var ax = Axis == "n" ? null : Axis;
            if (ax is not null && !Exclude) factor = Vec3.One.With(ax, f);
            else if (ax is not null && Exclude) factor = new Vec3(f, f, f).With(ax, 1);
            else factor = new Vec3(f, f, f);
            var fac = factor;
            if (Edit) move = w => (w - Pivot) * fac + Pivot;
            else foreach (var s in Start)
            {
                s.Object.Scale = s.Scale * factor;
                s.Object.Position = (s.Position - Pivot) * factor + Pivot;
            }
            text = $"スケール {(ax is not null ? (Exclude ? $"{ax.ToUpperInvariant()} 以外" : ax.ToUpperInvariant()) : "")}: {FmtVec(factor, 3, "")}";
        }
        else if (Kind is TransformKind.I or TransformKind.B)
        {
            // 差し込み: マウスを中心に近づけるほど厚く / ベベル: 中心から離すほど幅が広い
            var (px, py) = view.ToScreen(Pivot, input.ViewWidthPx, input.ViewHeightPx);
            var d0 = Math.Sqrt((StartPxX - px) * (StartPxX - px) + (StartPxY - py) * (StartPxY - py));
            var d1 = Math.Sqrt((input.MousePxX - px) * (input.MousePxX - px) + (input.MousePxY - py) * (input.MousePxY - py));
            var wpp = view.WorldPerPixel(Pivot);
            var t = numeric ?? Math.Max(0, (Kind == TransformKind.I ? d0 - d1 : d1 - d0) * wpp * (fine ? 0.1 : 1));
            if (numeric is null && snap) t = Js.Round(t * 10) / 10;
            t = Math.Max(0, Math.Min(Limit, t)); // THREE.MathUtils.clamp(t, 0, limit)
            foreach (var m in Moves!) m.V.Co = m.Base.AddScaled(m.Dir, t);
            EditObject!.Em.Changed();
            text = Kind == TransformKind.I ? $"差し込み  厚さ: {Js.ToFixed(t, 3)} m" : $"ベベル  幅: {Js.ToFixed(t, 3)} m  セグメント: 1";
        }
        else
        {
            // ループカットの位置。辺に沿ったマウスの動きで、-1(片側)〜 1(反対側)
            var first = Slide![0];
            var m = EditObject!.MatrixWorld;
            var sa = view.ToScreen(m.ApplyToPoint(first.A.Co), input.ViewWidthPx, input.ViewHeightPx);
            var sb = view.ToScreen(m.ApplyToPoint(first.B.Co), input.ViewWidthPx, input.ViewHeightPx);
            double dx = sb.X - sa.X, dy = sb.Y - sa.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var len = Math.Max(1, length);
            var span = 1.0 / (Cuts + 1);
            var inv = 1 / (length == 0 ? 1 : length);
            double ndx = dx * inv, ndy = dy * inv; // dir.normalize()
            var s = numeric ?? ((input.MousePxX - StartPxX) * ndx + (input.MousePxY - StartPxY) * ndy) / len / span;
            s = Math.Max(-0.98, Math.Min(0.98, s));
            foreach (var it in Slide) it.V.Co = it.A.Co.Lerp(it.B.Co, it.T + s * span);
            EditObject.Em.Changed();
            text = $"辺をスライド: {Js.ToFixed(s, 3)}（右クリックで中央）";
        }
        if (move is not null)
        {
            foreach (var s in EditStarts) s.V.Co = Inverse.ApplyToPoint(move(s.W));
            EditObject!.Em.Changed();
        }

        // 軸固定中は、その軸の線を操作の中心に表示する
        var visible = Kind.IsTransform() && Axis is not null && !Exclude && !(Kind == TransformKind.S && Axis == "n");
        var line = new AxisLine(visible, visible ? (Axis == "n" ? (Normal ?? Vec3.Zero) : AxisVec(Axis!)) : Vec3.Zero, visible ? AxisColor(Axis!) : 0);
        var hint = Kind.IsTransform() && Axis is null ? "　（X / Y / Z で軸固定）" : "";
        return new OpResult(text + hint, Numeric.Length > 0 ? $"入力: {Numeric}" : "", line, delta, rotation, factor, Pivot, Kind);
    }
}
