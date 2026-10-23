using FaraAvatarModdingGuide.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>Content/ の読み込み（ASP.NET なし）。移行前後で記事の数・URL・画像の参照先が変わらないことを確かめる。</summary>
public class ContentLoadTests
{
    private static readonly string ContentDir = FindContentDir();

    private static string FindContentDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Content", "learn", "categories.yml"))) return Path.Combine(dir.FullName, "Content");
        }
        throw new DirectoryNotFoundException("Content/ が見つかりません");
    }

    private static ContentService Load() => new(ContentDir, NullLogger<ContentService>.Instance);

    [Fact]
    public void All_articles_in_the_content_folder_are_loaded_with_their_urls()
    {
        var content = Load();
        var files = Directory.EnumerateFiles(Path.Combine(ContentDir, "learn"), "*.md", SearchOption.AllDirectories).ToList();
        Assert.Equal(50, files.Count);                      // 計画書 1.1: 記事は 50 件
        Assert.Equal(files.Count, content.Articles.Count);
        Assert.Equal(10, content.Categories.Count);
        Assert.Equal(content.Articles.Count, content.Articles.Select(a => a.Url).Distinct().Count());
        Assert.All(content.Articles, a => Assert.Matches(@"^/learn/[a-z0-9-]+/[a-z0-9-]+$", a.Url));
        Assert.All(content.Articles, a => Assert.False(string.IsNullOrWhiteSpace(a.Html)));
    }

    [Fact]
    public void Relative_images_in_articles_resolve_to_learn_urls_and_exist()
    {
        var content = Load();
        var found = 0;
        foreach (var article in content.Articles)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(article.Html, "<img[^>]*src=\"([^\"]+)\""))
            {
                var src = m.Groups[1].Value;
                if (src.StartsWith("/images/", StringComparison.Ordinal)) continue;   // wwwroot の画像（最初から絶対パス）
                Assert.StartsWith($"/learn/{article.Category}/", src);   // 相対パスは /learn/<カテゴリ>/ から始まる絶対パスになる
                Assert.True(File.Exists(Path.Combine(ContentDir, "learn", src["/learn/".Length..].Replace('/', Path.DirectorySeparatorChar))), $"{article.Url}: {src} が無い");
                found++;
            }
        }
        Assert.True(found > 0);
    }

    [Fact]
    public void Search_index_has_every_article_and_page()
    {
        var content = Load();
        var index = content.BuildSearchIndex();
        Assert.Equal(content.Articles.Count, index.Count(e => e.Type == "article"));
        Assert.Contains(index, e => e.Type == "page" && e.Url == "/privacy");
        Assert.Equal(index.Count, index.Select(e => (e.Type, e.Url, e.Title)).Distinct().Count());
    }

    [Fact]
    public void Constructor_rejects_a_blank_directory()
    {
        Assert.Throws<ArgumentException>(() => new ContentService(" "));
    }
}
