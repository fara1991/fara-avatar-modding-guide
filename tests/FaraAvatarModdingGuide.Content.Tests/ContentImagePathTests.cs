using FaraAvatarModdingGuide.Data;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>記事の画像のパス（Content の外を指すものは、読まずにエラーにする）。</summary>
public class ContentImagePathTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "content-root-for-test");

    [Fact]
    public void A_path_inside_the_content_folder_resolves_to_the_file_under_the_category()
    {
        var full = ContentService.ResolveContentImage(Root, "basics", "images/move.png", "basics/01-move.md");
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "learn", "basics", "images", "move.png")), full);
    }

    [Theory]
    [InlineData("../../../secret.png")]
    [InlineData("../../../../etc/passwd")]
    public void A_path_that_leaves_the_content_folder_is_an_error_and_is_not_read(string url)
    {
        var ex = Assert.Throws<FormatException>(() => ContentService.ResolveContentImage(Root, "basics", url, "basics/01-move.md"));
        Assert.Contains("Content の外", ex.Message);
        Assert.Contains("basics/01-move.md", ex.Message);
    }

    [Fact]
    public void A_rooted_path_outside_the_content_folder_is_an_error()
    {
        var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "outside.png"));
        Assert.Throws<FormatException>(() => ContentService.ResolveContentImage(Root, "basics", outside, "basics/01-move.md"));
    }
}
