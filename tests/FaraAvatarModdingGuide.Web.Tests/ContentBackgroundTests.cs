using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 背景の図形（js/bgscene.js の canvas）が本文の後ろで見えるための約束。本文の地（.fbc-content）は半透明にとどめる。
/// 不透明に近いと図形がほとんど見えなくなる（ライトで特に）。文字のコントラストは別に保つ（ここでは地の透明度だけを見る）。
/// </summary>
public class ContentBackgroundTests
{
    private static readonly string AppCss = File.ReadAllText(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "css", "app.css"));

    /// <summary>本文の地の透明度の上限。これより濃いと、背景の図形がほとんど見えなくなる。</summary>
    private const double MaxContentAlpha = 0.5;

    [Theory]
    [InlineData(@"(?m)^\.fbc-content\s*\{\s*background:\s*rgb\(12 8 28 / ([0-9.]+)\);")]
    [InlineData(@"(?m)^\[data-bs-theme=""light""\] \.fbc-content\s*\{\s*background:\s*rgb\(247 244 253 / ([0-9.]+)\);")]
    public void The_content_background_stays_translucent_so_the_scene_shows_through(string pattern)
    {
        var match = Regex.Match(AppCss, pattern);
        Assert.True(match.Success, "本文の地の規則が見つからない");
        var alpha = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(alpha <= MaxContentAlpha, $"本文の地の透明度が濃すぎる（{alpha}）。図形が見えなくなる");
    }
}
