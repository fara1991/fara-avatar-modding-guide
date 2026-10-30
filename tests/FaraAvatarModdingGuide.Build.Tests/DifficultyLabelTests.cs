using FaraAvatarModdingGuide.Models;
using Xunit;

namespace FaraAvatarModdingGuide.Build.Tests;

/// <summary>むずかしさの表示名（front matter の初級・中級・上級は変えず、画面にはやさしい言葉で出す）。</summary>
public class DifficultyLabelTests
{
    [Theory]
    [InlineData("初級", "やさしい")]
    [InlineData("中級", "ふつう")]
    [InlineData("上級", "むずかしい")]
    [InlineData(null, "")]
    public void Difficulty_is_shown_in_gentle_words(string? front, string expected) =>
        Assert.Equal(expected, SiteLookups.DifficultyLabel(front));

    [Fact]
    public void Static_badge_uses_the_gentle_label_and_keeps_the_color_by_front_matter_value()
    {
        var html = Fragments.DifficultyBadge("初級");
        Assert.Contains(">やさしい</span>", html);
        Assert.Contains("bg-success", html);
    }
}
