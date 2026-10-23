using System.Text.Json;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>
/// ページに href として出す URL は、https だけにする。Razor（Blazor）は href の値を無害化しないので、javascript: や data: が入ると、そのままリンクになる。
/// 対象: リンク集（links.yml）・アドオン（addons.yml）・フッターの SNS・リポジトリ・作者のサイト（appsettings.json の Site）。
/// 記事本文のリンクは、https・サイト内の相対パス・#・mailto: だけ許す（Markdown に書いたもの）。
/// </summary>
public class ExternalUrlTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")) && Directory.Exists(Path.Combine(dir.FullName, "Content"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("リポジトリのルートが見つかりません");
    }

    private static ContentService Load() => new(Path.Combine(Root, "Content"), NullLogger<ContentService>.Instance);

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);

    [Fact]
    public void Link_page_urls_are_https()
    {
        var links = Load().Links;
        Assert.NotEmpty(links);
        foreach (var l in links) Assert.True(IsHttps(l.Url), $"links.yml {l.Id}: {l.Url}");
    }

    [Fact]
    public void Addon_urls_are_https_when_present()
    {
        var addons = Load().Addons;
        Assert.NotEmpty(addons);
        foreach (var a in addons.Where(a => !string.IsNullOrEmpty(a.Url))) Assert.True(IsHttps(a.Url), $"addons.yml {a.Name}: {a.Url}");
    }

    [Fact]
    public void Site_settings_urls_are_https()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "appsettings.json")));
        var site = doc.RootElement.GetProperty("Site");
        Assert.True(IsHttps(site.GetProperty("RepositoryUrl").GetString()), "RepositoryUrl");
        Assert.True(IsHttps(site.GetProperty("PortfolioUrl").GetString()), "PortfolioUrl");
        foreach (var s in site.GetProperty("Socials").EnumerateArray())
            Assert.True(IsHttps(s.GetProperty("Url").GetString()), "Socials " + s.GetProperty("Service").GetString());
    }

    [Fact]
    public void Article_links_and_images_use_safe_schemes()
    {
        var checkedCount = 0;
        foreach (var article in Load().Articles)
        {
            foreach (Match m in Regex.Matches(article.Html, @"<(?:a|img|source|video)\b[^>]*?\b(?:href|src)=""([^""]*)""", RegexOptions.IgnoreCase))
            {
                var url = m.Groups[1].Value.Trim();
                checkedCount++;
                var safe = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                           || url.StartsWith('/') || url.StartsWith('#') || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                           || !Regex.IsMatch(url, @"^[a-zA-Z][a-zA-Z0-9+.-]*:");      // スキームの無い相対パス
                Assert.True(safe, $"{article.Url}: {url}");
            }
        }
        Assert.True(checkedCount > 50, "検査した URL: " + checkedCount);
    }
}
