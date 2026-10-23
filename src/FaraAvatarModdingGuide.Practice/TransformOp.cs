namespace FaraAvatarModdingGuide.Practice;

/// <summary>G(移動)・R(回転)・S(スケール)。差し込み(I)・ベベル(B)・ループカットの位置(slide)は編集モードの段階で足す。</summary>
public enum TransformKind { G, R, S }

/// <summary>操作を始めた時点のオブジェクトの状態(キャンセルで戻す)。</summary>
public sealed record StartState(SceneObject Object, Vec3 Position, Quat Quaternion, Vec3 Scale);

/// <summary>1 回の計算(applyOp)の入力: マウスと修飾キーと、視点。</summary>
public sealed record OpInput(OrbitView View, double MouseNdcX, double MouseNdcY, double MousePxX, double MousePxY, double ViewWidthPx, double ViewHeightPx, bool Shift, bool Ctrl);

/// <summary>軸固定の線の表示(操作の中心に、その軸の向きで出す)。Direction は軸の向き(法線のときは法線)。</summary>
public sealed record AxisLine(bool Visible, Vec3 Direction, int Color);

/// <summary>操作の結果。編集モードでは、選択した頂点のワールド座標を <see cref="MapPoint"/> で動かす。</summary>
public sealed record OpResult(string Status, string NumericHud, AxisLine Line, Vec3 Delta, Quat Rotation, Vec3 Factor, Vec3 Pivot, TransformKind Kind)
{
    /// <summary>ワールド座標の点に、この操作を当てた結果(編集モードの頂点用)。</summary>
    public Vec3 MapPoint(Vec3 w) => Kind switch
    {
        TransformKind.G => w + Delta,
        TransformKind.R => Rotation.Rotate(w - Pivot) + Pivot,
        _ => (w - Pivot) * Factor + Pivot,
    };
}

/// <summary>
/// G / R / S の操作の状態(viewport.js の op と startOp / setAxis / typeNumeric / applyOp)。
/// 開始(選択の中心を決める)→ 軸(X / Y / Z。Shift で「それ以外」。もう一度で解除)→ 数値入力 → マウスで動かす(Ctrl で刻み、Shift で細かく)→ 確定 / キャンセル。
/// 式・順序・表示の文字は現行の JavaScript と同じ。
/// </summary>
public sealed class TransformOp
{
    public static readonly string[] Axes = ["x", "y", "z"];

    /// <summary>軸の色(AXIS_COLOR)。x / y / z / n(法線)。</summary>
    public static int AxisColor(string axis) => axis switch { "x" => 0xff3352, "y" => 0x8bdc00, "z" => 0x2890ff, _ => 0xffd84a };

    public TransformOp(TransformKind kind, IReadOnlyList<SceneObject> selected, double startNdcX, double startNdcY, double startPxX, double startPxY, Vec3? normal = null, bool fromExtrude = false, bool fromDuplicate = false)
    {
        Kind = kind;
        Start = [.. selected.Select(o => new StartState(o, o.Position, o.Quaternion, o.Scale))];
        var sum = Vec3.Zero;
        foreach (var o in selected) sum += o.Position;
        Pivot = sum * (1.0 / selected.Count);
        Normal = normal;
        Axis = normal is null ? null : "n";
        FromExtrude = fromExtrude;
        FromDuplicate = fromDuplicate;
        StartNdcX = startNdcX; StartNdcY = startNdcY; StartPxX = startPxX; StartPxY = startPxY;
    }

    public TransformKind Kind { get; }
    public IReadOnlyList<StartState> Start { get; }
    public Vec3 Pivot { get; }
    public Vec3? Normal { get; }
    public bool FromExtrude { get; }
    public bool FromDuplicate { get; }

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

    /// <summary>入力が空・"-"・"." だけのとき、または数値にならないときは null(マウスの動きを使う)。</summary>
    private double? NumericValue() => Numeric is "" or "-" or "." ? null : Js.ParseNumeric(Numeric);

    private Vec3 AxisVec(string a) => a == "n" ? Normal ?? Vec3.Zero : a switch { "x" => Vec3.UnitX, "y" => Vec3.UnitY, _ => Vec3.UnitZ };

    private static string AxisName(string a) => a == "n" ? "法線" : a.ToUpperInvariant();

    private static string FmtVec(Vec3 v, int digits = 3, string unit = " m") =>
        $"X {Js.ToFixed(v.X, digits)}{unit}  Y {Js.ToFixed(v.Y, digits)}{unit}  Z {Js.ToFixed(v.Z, digits)}{unit}";

    /// <summary>キャンセル: 選択物を操作の前に戻す。</summary>
    public void Restore()
    {
        foreach (var s in Start)
        {
            s.Object.Position = s.Position;
            s.Object.Quaternion = s.Quaternion;
            s.Object.Scale = s.Scale;
        }
    }

    /// <summary>マウスの位置・軸固定・数値入力から、選択物のトランスフォームを更新して、表示用の結果を返す(オブジェクトモード)。</summary>
    public OpResult Apply(OpInput input)
    {
        var view = input.View;
        bool snap = input.Ctrl, fine = input.Shift;
        var numeric = NumericValue();
        var axisLineAxis = Axis;
        string text;
        var delta = Vec3.Zero;
        var rotation = Quat.Identity;
        var factor = Vec3.One;

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
            foreach (var s in Start) s.Object.Position = s.Position + delta;
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
            foreach (var s in Start)
            {
                s.Object.Quaternion = rotation * s.Quaternion;
                s.Object.Position = rotation.Rotate(s.Position - Pivot) + Pivot;
            }
            text = $"回転 {(Axis is not null && !Exclude ? AxisName(Axis) : "")}: {Js.ToFixed(angle / Js.Deg, 2)}°";
        }
        else
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
            foreach (var s in Start)
            {
                s.Object.Scale = s.Scale * factor;
                s.Object.Position = (s.Position - Pivot) * factor + Pivot;
            }
            text = $"スケール {(ax is not null ? (Exclude ? $"{ax.ToUpperInvariant()} 以外" : ax.ToUpperInvariant()) : "")}: {FmtVec(factor, 3, "")}";
        }

        // 軸固定中は、その軸の線を操作の中心に表示する
        var visible = Axis is not null && !Exclude && !(Kind == TransformKind.S && Axis == "n");
        var line = new AxisLine(visible, visible ? (axisLineAxis == "n" ? (Normal ?? Vec3.Zero) : AxisVec(axisLineAxis!)) : Vec3.Zero, visible ? AxisColor(Axis!) : 0);
        var hint = Axis is null ? "　（X / Y / Z で軸固定）" : "";
        return new OpResult(text + hint, Numeric.Length > 0 ? $"入力: {Numeric}" : "", line, delta, rotation, factor, Pivot, Kind);
    }
}
