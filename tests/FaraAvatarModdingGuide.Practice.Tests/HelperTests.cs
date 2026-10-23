using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>JavaScript に無い補助(JavaScript の数の丸め・書き方の再現)と、練習の部品の単体テスト。</summary>
public class HelperTests
{
    [Theory]
    [InlineData(0.0, 3, "0.000")]
    [InlineData(-0.0004, 3, "-0.000")]      // 負の値は、丸めて 0 になっても符号が残る
    [InlineData(1.0005, 3, "1.000")]        // 2 進数での値 1.000499999999999989... を正確に丸める
    [InlineData(0.0625, 3, "0.063")]        // ちょうど半分は、0 から遠いほうへ
    [InlineData(-0.0625, 3, "-0.063")]
    [InlineData(2, 2, "2.00")]
    [InlineData(-45.126, 2, "-45.13")]
    public void ToFixed_matches_javascript(double value, int digits, string expected) => Assert.Equal(expected, Js.ToFixed(value, digits));

    [Fact]
    public void ToFixed_drops_the_sign_of_negative_zero() => Assert.Equal("0.000", Js.ToFixed(-0.0, 3));   // JavaScript の (-0).toFixed(3) は "0.000"

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -2)]                  // Math.round は 0.5 を正の無限大のほうへ
    [InlineData(-0.4, 0)]
    [InlineData(0.5, 1)]
    [InlineData(1.4999999999999998, 1)]
    public void Round_matches_math_round(double value, double expected) => Assert.Equal(expected, Js.Round(value));

    [Theory]
    [InlineData("2", 2.0)]
    [InlineData("2.", 2.0)]
    [InlineData(".5", 0.5)]
    [InlineData("-.5", -0.5)]
    [InlineData("-12.25", -12.25)]
    [InlineData("007", 7.0)]
    public void ParseNumeric_reads_what_the_numeric_input_can_build(string text, double expected) => Assert.Equal(expected, Js.ParseNumeric(text));

    [Theory]
    [InlineData("-.")]   // 現行の JavaScript は NaN になり、オブジェクトが消える。C# は数値入力なしとして扱う(意図した差。ParityTests の入力には含めない)
    [InlineData("")]
    public void ParseNumeric_returns_null_when_there_is_no_number(string text) => Assert.Null(Js.ParseNumeric(text));

    [Fact]
    public void Numeric_input_follows_the_viewport_rules()
    {
        var op = new TransformOp(TransformKind.G, [SceneObject.Create("cube")], 0, 0, 0, 0);
        foreach (var t in new[] { "1", ".", "5", ".", "-" }) op.TypeNumeric(t);
        Assert.Equal("-1.5", op.Numeric);   // 2 つ目の "." は無視、"-" は先頭の符号の入れ替え
        op.TypeNumeric("-");
        Assert.Equal("1.5", op.Numeric);
        foreach (var _ in new[] { 1, 2, 3, 4 }) op.TypeNumeric("Backspace");
        Assert.Equal("", op.Numeric);
    }

    [Fact]
    public void Axis_pressed_twice_is_released_and_extrude_falls_back_to_the_normal()
    {
        var o = SceneObject.Create("cube");
        var op = new TransformOp(TransformKind.G, [o], 0, 0, 0, 0);
        op.SetAxis("x", false); Assert.Equal(("x", false), (op.Axis, op.Exclude));
        op.SetAxis("x", true); Assert.Equal(("x", true), (op.Axis, op.Exclude));   // Shift を付けると別の指定(それ以外)
        op.SetAxis("x", true); Assert.Null(op.Axis);
        var extrude = new TransformOp(TransformKind.G, [o], 0, 0, 0, 0, normal: Vec3.UnitZ, fromExtrude: true);
        Assert.Equal("n", extrude.Axis);
        extrude.SetAxis("z", false); extrude.SetAxis("z", false);
        Assert.Equal("n", extrude.Axis);
    }

    [Fact]
    public void Euler_roundtrip_gives_back_the_angles_three_js_would_report()
    {
        foreach (var (x, y, z) in new[] { (0.1, 0.2, 0.3), (-1.0, 0.5, 2.5), (0.0, 0.0, Math.PI / 4), (3.0, -1.2, -2.9) })
        {
            var e = Quat.FromEulerXYZ(x, y, z).ToEulerXYZ();
            Assert.True(Math.Abs(e.X - x) < 1e-12 && Math.Abs(e.Y - y) < 1e-12 && Math.Abs(e.Z - z) < 1e-12, $"({x}, {y}, {z}) → {e}");
        }
    }

    [Fact]
    public void Wheel_zoom_is_clamped()
    {
        var view = new OrbitView();
        for (var i = 0; i < 100; i++) view.ZoomWheel(120);
        Assert.Equal(OrbitView.MaxDist, view.Dist);
        for (var i = 0; i < 200; i++) view.ZoomWheel(-120);
        Assert.Equal(OrbitView.MinDist, view.Dist);
    }

    [Fact]
    public void Unsupported_keys_return_false_without_changing_the_scene()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 800, 600));
        core.Objects.Add(SceneObject.Create("cube"));
        core.SetSelection(core.Objects);
        foreach (var (code, mods) in new[] { ("KeyA", new Modifiers(Shift: true)), ("KeyX", default(Modifiers)), ("Delete", default), ("Tab", default), ("KeyZ", new Modifiers(Ctrl: true)), ("KeyD", new Modifiers(Shift: true)) })
            Assert.False(core.KeyDown(code, "", mods), code);
        Assert.Single(core.Objects);
        Assert.Null(core.Op);
    }

    [Fact]
    public void Zero_size_view_keeps_the_previous_aspect_like_resize_in_viewport_js()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 800, 400));
        core.Resize(new ViewRect(0, 0, 0, 0));
        Assert.Equal(2, core.View.Aspect);
        var zero = new ViewportCore(new ViewRect(0, 0, 0, 0));
        Assert.Equal(1, zero.View.Aspect);   // 既定は 1(viewport.js の aspect || 1)
        Assert.True(double.IsFinite(zero.View.RayFromNdc(0.3, 0.2).Direction.X));
    }
}
