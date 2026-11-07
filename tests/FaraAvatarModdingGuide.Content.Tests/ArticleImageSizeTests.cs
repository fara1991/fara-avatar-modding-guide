using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>
/// 記事の画像に寸法（width・height）が付いているか。寸法が無いと、読み込み前後で本文が動く（レイアウトのずれ）。
/// 寸法は画像ファイル（PNG の IHDR）から読む。画像を差し替えたら、ここの数字も自動で追う。
/// </summary>
public class ArticleImageSizeTests
{
    private static readonly Regex ImgTag = new(@"<img\b[^>]*>");

    private static ContentService Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Content", "learn", "categories.yml"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return new ContentService(Path.Combine(dir!.FullName, "Content"), NullLogger<ContentService>.Instance);
    }

    [Fact]
    public void Select_article_image_carries_the_pixel_size_of_the_png()
    {
        var article = Load().GetArticles("basics").Single(a => a.Slug == "select");
        var img = ImgTag.Matches(article.Html).Select(m => m.Value).Single(t => t.Contains("select-objects.png"));
        Assert.Contains("width=\"1321\"", img);
        Assert.Contains("height=\"785\"", img);
    }

    [Fact]
    public void Every_article_image_has_width_and_height()
    {
        var content = Load();
        var missing = content.Categories
            .SelectMany(c => content.GetArticles(c.Id))
            .SelectMany(a => ImgTag.Matches(a.Html).Select(m => (Article: a.Category + "/" + a.Slug, Tag: m.Value)))
            .Where(x => !x.Tag.Contains(" width=\"") || !x.Tag.Contains(" height=\""))
            .Select(x => x.Article + " " + x.Tag)
            .ToList();
        Assert.True(missing.Count == 0, "寸法（width・height）の無い画像: " + string.Join(" / ", missing));
    }
}
