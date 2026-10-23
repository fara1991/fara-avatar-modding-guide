using FaraAvatarModdingGuide.Models;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Data;
using Xunit;

namespace FaraAvatarModdingGuide.Build.Tests;

/// <summary>ページ一覧（PageCatalog）・アイコンの写し・検索の正規化・説明文の整形。Node は使わない。</summary>
public class PageCatalogTests
{
    private static readonly ContentService Content = new(Path.Combine(Repo.Root, "Content"));
    private static readonly SiteContent Site = SiteContentFactory.From(Content);
    private static readonly SiteSettings Settings = SiteSettingsLoader.Load(Path.Combine(Repo.Root, "appsettings.json"), "avatar-dev.fara-labs.com");
    private static readonly IReadOnlyList<PagePlan> Pages = PageCatalog.Build(Site, Settings);

    [Fact]
    public void There_are_76_pages_with_unique_kit_compatible_paths()
    {
        Assert.Equal(76, Pages.Count);                                   // 固定 16 + カテゴリ 10 + 記事 50
        Assert.Equal(Pages.Count, Pages.Select(p => p.Path).Distinct().Count());
        Assert.All(Pages, p => Assert.Matches("^/(?:[a-z0-9][a-z0-9-]*(?:/[a-z0-9][a-z0-9-]*)*)?$", p.Path));   // kit の path の規則
        Assert.Equal(50, Pages.Count(p => Regex.IsMatch(p.Path, "^/learn/[^/]+/[^/]+$")));
        Assert.Equal(10, Pages.Count(p => Regex.IsMatch(p.Path, "^/learn/[^/]+$")));
    }

    [Fact]
    public void Article_urls_are_the_same_as_the_ssr_site()
    {
        var urls = Pages.Select(p => p.Path).ToHashSet();
        foreach (var article in Content.Articles) Assert.Contains(article.Url, urls);
        foreach (var category in Content.Categories) Assert.Contains($"/learn/{category.Id}", urls);
        // 現行の StaticSiteExporter が書き出す固定ページ（/ と /learn を含む 16 本）
        foreach (var path in new[] { "/", "/learn", "/shortcuts", "/addons", "/practice", "/practice/room", "/practice/unity", "/glossary", "/howto", "/tips", "/qa", "/links", "/changelog", "/report", "/privacy", "/search" })
            Assert.Contains(path, urls);
    }

    [Fact]
    public void Titles_and_descriptions_fit_what_kit_accepts()
    {
        Assert.All(Pages, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Title));
            Assert.EndsWith(" - Avatar Modding Guide", p.Title);
            Assert.False(string.IsNullOrWhiteSpace(p.Description), p.Path);
            Assert.True(p.Description.Length <= 160, p.Path + " の説明が長い");
            Assert.False(string.IsNullOrWhiteSpace(p.Main), p.Path);
        });
        Assert.Equal(Pages.Count, Pages.Select(p => p.Title).Distinct().Count());   // 題名は重複させない
    }

    [Fact]
    public void Noindex_pages_are_the_report_and_the_provisional_ones_and_wasm_pages_are_all_but_practice()
    {
        // /report と、フェーズ 4 までの暫定(検索の画面・練習の 3 ページ。sitemap にも載せない)
        Assert.Equal(new[] { "/report" }.Concat(PageCatalog.ProvisionalNoIndex).Order().ToArray(), Pages.Where(p => p.NoIndex).Select(p => p.Path).Order().ToArray());
        Assert.Equal(PageCatalog.WasmPaths.Concat(Pages.Where(p => p.Path.StartsWith("/learn/", StringComparison.Ordinal)).Select(p => p.Path)).Order().ToArray(),
            Pages.Where(p => p.Wasm).Select(p => p.Path).Order().ToArray());
        Assert.Equal(["/practice", "/practice/room", "/practice/unity"], Pages.Where(p => !p.Wasm).Select(p => p.Path).Order().ToArray());
    }

    [Fact]
    public void Every_bootstrap_icon_used_in_the_content_maps_to_a_material_icon()
    {
        foreach (var name in SiteIcons.BootstrapNames)
        {
            var material = SiteIcons.ToMaterial(name);
            Assert.Contains("<path", SiteIcons.SvgMarkup(material));  // MudBlazor の定数が実在する
        }
        foreach (var section in Content.Sections) Assert.Contains(section.Icon, SiteIcons.BootstrapNames);
        foreach (var category in Content.Categories) Assert.Contains(category.Icon, SiteIcons.BootstrapNames);
        foreach (var link in ContentService.LinkCategories) Assert.Contains(link.Icon, SiteIcons.BootstrapNames);
        Assert.Throws<KeyNotFoundException>(() => SiteIcons.ToMaterial("bi-nonexistent"));
    }

    [Fact]
    public void Pages_use_material_svg_icons_and_never_the_bootstrap_icons_font()
    {
        foreach (var page in Pages)
        {
            var html = PageCatalog.Wrap(page.Main, Settings);
            Assert.DoesNotContain("class=\"bi ", html);
            Assert.DoesNotContain("bi-", Regex.Replace(html, "<svg.*?</svg>", "", RegexOptions.Singleline), StringComparison.Ordinal);   // アイコンの名前の取りこぼし
        }
        Assert.Contains("<svg class=\"av-icon", Pages.Single(p => p.Path == "/").Main);
    }

    [Theory]
    [InlineData("/learn/basics/select", "/report?from=%2Flearn%2Fbasics%2Fselect")]
    [InlineData("/", "/report?from=%2F")]
    [InlineData("/report", "/report")]
    [InlineData("/privacy", "/report")]
    [InlineData(null, "/report")]
    [InlineData("/a b", "/report")]
    public void Footer_report_link_carries_the_current_page(string? path, string expected)
    {
        Assert.Equal(expected, PageCatalog.ReportHref(path));
        Assert.Contains($"<a href=\"{expected}\">問い合わせ</a>", PageCatalog.Wrap("<p>x</p>", Settings, path));
    }

    [Fact]
    public void Wrapped_page_starts_with_the_entry_static_root_that_kit_requires()
    {
        var html = PageCatalog.Wrap("<p>x</p>", Settings);
        Assert.StartsWith("<div class=\"entry-static", html);
        Assert.Contains("<main id=\"fsk-main\"", html);
        Assert.Contains("<footer class=\"fsk-footer\">", html);
    }

    [Fact]
    public void Article_pages_show_the_toc_only_with_three_or_more_h2()
    {
        Assert.Empty(Fragments.ExtractHeadings("<h2 id=\"a\">A</h2><h2 id=\"b\">B</h2>"));
        Assert.Equal(3, Fragments.ExtractHeadings("<h2 id=\"a\">A</h2><h2 id=\"b\">B &amp; b</h2><h2 id=\"c\"><code>C</code></h2>").Count);
        Assert.Equal("B & b", Fragments.ExtractHeadings("<h2 id=\"a\">A</h2><h2 id=\"b\">B &amp; b</h2><h2 id=\"c\">C</h2>")[1].Text);
    }

    [Fact]
    public void Home_lists_every_section_and_the_three_latest_changelog_entries()
    {
        var home = Pages.Single(p => p.Path == "/").Main;
        foreach (var section in Site.Sections) Assert.Contains($"id=\"section-{section.Id}\"", home);
        foreach (var entry in Site.Changelog.Take(PageBodies.RecentChangelogCount)) Assert.Contains(System.Net.WebUtility.HtmlEncode(entry.Title), home);
        Assert.Equal(8, Regex.Matches(home, "class=\"card h-100 text-decoration-none article-card\"").Count);
    }

    [Theory]
    [InlineData("ＡＢＣ　ｱｲｳ", "abc あいう")]       // 全角英数・全角スペース・半角カナ → NFKC、カタカナ → ひらがな
    [InlineData("ヴァイオリン", "ゔぁいおりん")]      // カタカナ → ひらがな
    [InlineData("A~B", "a〜b")]                       // ~ → 〜
    [InlineData("", "")]
    public void Search_normalisation_matches_searchcore_js(string input, string expected)
    {
        // 期待値は searchcore.js の norm() と同じ規則（tests/js/search-norm.test.mjs が JS 側と突き合わせる）
        Assert.Equal(expected, SearchIndexJson.Normalize(input));
    }

    [Fact]
    public void Describe_squashes_whitespace_and_truncates_with_an_ellipsis()
    {
        Assert.Equal("a b", Html.Describe("  a \n  b "));
        var long150 = new string('あ', 200);
        var described = Html.Describe(long150);
        Assert.Equal(150, described.Length);
        Assert.EndsWith("…", described);
    }
}
