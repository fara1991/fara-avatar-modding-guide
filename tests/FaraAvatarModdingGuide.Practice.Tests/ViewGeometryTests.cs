using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// S4: ビューの大きさの扱い(枠のある要素)とマウス位置の既定。viewport.js は、縦横比・mousePx の範囲は clientWidth / clientHeight(枠を除いた整数)、
/// 座標の変換(ndc・toScreen)は getBoundingClientRect(枠を含む小数)で行う。記事内チュートリアルのビューは 1px の枠があり、この 2 つが違う(画素の比較で 1px のずれになった)。
/// </summary>
public class ViewGeometryTests
{
    [Fact]
    public void Aspect_uses_the_client_size_not_the_bounding_rect()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540));
        core.Resize(new ViewRect(0, 0, 711.5, 400.2), 710, 398);
        Assert.Equal(710.0 / 398, core.View.Aspect, 12);
        Assert.Equal(398, core.View.HeightPx);
    }

    [Fact]
    public void Resize_with_only_a_rect_rounds_it_to_the_client_size()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540));
        core.Resize(new ViewRect(0, 0, 709.5, 398.2));
        Assert.Equal(Math.Round(709.5) / Math.Round(398.2), core.View.Aspect, 12);
    }

    [Fact]
    public void Menu_opens_at_the_client_center_until_the_mouse_has_entered()
    {
        // 作ったときの大きさ(仮の 960 x 540)ではなく、いまの clientWidth / 2・clientHeight / 2
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540));
        core.Resize(new ViewRect(0, 0, 711.5, 400.2), 710, 398);
        core.KeyDown("KeyA", "A", new Modifiers(true, false, false, false));
        Assert.NotNull(core.Menu);
        Assert.Equal(355, core.MenuPosition.X);
        Assert.Equal(199, core.MenuPosition.Y);
    }

    [Fact]
    public void Menu_opens_at_the_mouse_once_it_has_moved()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540));
        core.Resize(new ViewRect(0, 0, 711.5, 400.2), 710, 398);
        core.PointerMove(120.5, 80.25, new Modifiers(false, false, false, false));
        core.KeyDown("KeyA", "A", new Modifiers(true, false, false, false));
        Assert.Equal(120.5, core.MenuPosition.X);
        Assert.Equal(80.25, core.MenuPosition.Y);
    }
}
