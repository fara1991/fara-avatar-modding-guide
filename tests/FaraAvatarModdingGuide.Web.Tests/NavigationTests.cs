using System.Reflection;
using Bunit;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Layout;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>WebAssembly のページの一覧・サイドバーの項目・静的 HTML へ移るときの判定。</summary>
public class NavigationTests
{
    private static readonly string[] RouteTemplates = typeof(PageRoutes).Assembly.GetTypes()
        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
        .Select(r => r.Template)
        .Order()
        .ToArray();

    /// <summary>@page のルートの書き方(/learn/{CategoryId}/{Slug})に、このパスが合うか。</summary>
    private static bool MatchesRoute(string template, string path)
    {
        var t = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var p = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return t.Length == p.Length && t.Zip(p).All(x => x.First.StartsWith('{') || x.First == x.Second);
    }

    [Fact]
    public void Fixed_wasm_routes_are_the_same_in_web_and_build()
    {
        Assert.Equal(PageCatalog.WasmPaths.Order().ToArray(), PageRoutes.Wasm.Order().ToArray());
    }

    [Fact]
    public void Every_page_the_build_hands_to_wasm_is_a_web_route_and_every_other_is_not()
    {
        var pages = PageCatalog.Build(TestData.Content, TestData.Settings);
        Assert.Equal(76, pages.Count);
        foreach (var page in pages)
        {
            Assert.Equal(page.Wasm, PageRoutes.IsWasmPage(page.Path, TestData.Content));                  // Web の判定 = Build の判定
            Assert.Equal(page.Wasm, RouteTemplates.Any(t => MatchesRoute(t, page.Path)));                  // WebAssembly のページは、どれかの @page に合う
        }
        // フェーズ 3 で、練習の 3 ページも WebAssembly になった。静的 HTML のままのページは無い
        Assert.DoesNotContain(pages, p => !p.Wasm);
        Assert.Equal(50, pages.Count(p => p.Path.Count(c => c == '/') == 3 && p.Wasm));                    // 記事 50 件
    }

    [Theory]
    [InlineData("http://localhost/learn/basics", true)]                 // content.json にあるカテゴリ → Blazor
    [InlineData("http://localhost/learn/basics/", true)]
    [InlineData("http://localhost/learn/basics/transform", true)]       // 記事 → Blazor
    [InlineData("http://localhost/learn/basics/transform#section", true)]
    [InlineData("http://localhost/learn", true)]
    [InlineData("http://localhost/tips?tag=Unity", true)]
    [InlineData("http://localhost/search?q=blender", true)]
    [InlineData("http://localhost/links", true)]
    [InlineData("http://localhost/report?from=%2Flearn", true)]
    [InlineData("http://localhost/", true)]
    [InlineData("http://localhost/practice", true)]                     // 練習(フェーズ 3 で WebAssembly) → Blazor
    [InlineData("http://localhost/practice/unity#x", true)]
    [InlineData("http://localhost/practice/room?room=abcdef123456", true)]
    [InlineData("http://localhost/learn/basics/no-such-article", false)]   // 知らない記事 → 読み直して 404.html
    [InlineData("http://localhost/learn/no-such-category", false)]
    [InlineData("http://localhost/no-such-page", false)]
    public void Wasm_pages_are_navigated_inside_the_app_and_the_rest_reload(string target, bool inApp)
    {
        Assert.Equal(!inApp, PageRoutes.ShouldReload(target, "http://localhost/", TestData.Content));
    }

    [Theory]
    [InlineData("https://github.com/fara1991")]                         // 外部のサイトはブラウザに任せる
    [InlineData("http://localhost:5000/learn")]
    [InlineData("mailto:a@example.com")]
    public void External_links_are_left_to_the_browser(string target)
    {
        Assert.False(PageRoutes.ShouldReload(target, "http://localhost/", TestData.Content));
    }

    [Fact]
    public void Without_content_articles_are_treated_as_static_pages()
    {
        Assert.True(PageRoutes.ShouldReload("http://localhost/learn/basics/transform", "http://localhost/", null));
        Assert.False(PageRoutes.ShouldReload("http://localhost/links", "http://localhost/", null));
    }

    [Fact]
    public void Every_header_link_points_to_an_existing_page()
    {
        var paths = PageCatalog.Build(TestData.Content, TestData.Settings).Select(p => p.Path).ToHashSet();
        var links = HeaderNav.Main.Concat(HeaderNav.More).ToList();
        foreach (var link in links) Assert.Contains(link.Href, paths);
        Assert.Equal(links.Count, links.Select(l => l.Href).Distinct().Count());
        Assert.All(HeaderNav.More, l => Assert.False(string.IsNullOrEmpty(IconMap.Get(l.Icon))));
    }

    [Fact]
    public void Pages_the_sidebar_used_to_list_are_still_reachable()
    {
        // 旧サイドバーの項目: カテゴリ別の一覧・リファレンス・練習・サポート
        var reachable = HeaderNav.Main.Concat(HeaderNav.More).Select(l => l.Href).ToHashSet();
        foreach (var path in new[] { "/learn", "/shortcuts", "/glossary", "/howto", "/addons", "/links", "/changelog", "/practice", "/practice/room", "/report", "/privacy" })
            Assert.Contains(path, reachable);
        // ヘッダーに無い /tips・/qa・/practice/unity は、「困ったとき」「練習」の先のページ本文のリンクから行ける
        var bodies = new PageBodies(TestData.Content, TestData.Settings);
        Assert.Contains("href=\"/tips\"", bodies.Howto());
        Assert.Contains("href=\"/qa\"", bodies.Howto());
        Assert.Contains("href=\"/practice/unity\"", bodies.Practice());
        // カテゴリ別の記事一覧は、「記事をよむ」（記事一覧のページ）に全部ある
        foreach (var section in TestData.Content.Sections)
            foreach (var category in TestData.Content.CategoriesIn(section.Id))
                Assert.Contains($"href=\"/learn/{category.Id}\"", bodies.LearnIndex());
    }

    [Fact]
    public void Icon_map_has_every_icon_the_build_can_emit()
    {
        foreach (var name in SiteIcons.MaterialNames)
        {
            Assert.False(string.IsNullOrEmpty(IconMap.Get(name)), name + " が IconMap にありません");
            Assert.Equal(SiteIcons.SvgMarkup(name), IconMap.Get(name));                // 静的 HTML と同じ SVG
        }
        Assert.Equal("", IconMap.Get("NoSuchIcon"));
        Assert.Equal("", IconMap.Get(null));
    }
}

/// <summary>
/// 上のヘッダーの「いまのページ」の強調（aria-current="page"）。ちょうど 1 つだけ強調する
/// （「もっと見る」の中のページは、中の項目だけが強調され、見出しに data-active が付く）。
/// </summary>
public class HeaderActiveTests : WebTestContext
{
    private IReadOnlyList<string> ActiveLinks(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvTopNav>();
        return cut.FindAll(".av-topnav-links > a[aria-current='page']").Select(a => a.GetAttribute("href")!).ToList();
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/learn", "/learn")]
    [InlineData("/learn/basics", "/learn")]
    [InlineData("/learn/basics/transform", "/learn")]
    [InlineData("/learn/basics/transform#x", "/learn")]
    [InlineData("/learn/getting-started", "/learn/getting-started")]               // 「はじめかた」だけ（「記事をよむ」は強調しない）
    [InlineData("/learn/getting-started/install", "/learn/getting-started")]
    [InlineData("/howto", "/howto")]
    [InlineData("/tips", "/howto")]                                                // 「困ったとき」は /howto・/tips・/qa
    [InlineData("/tips?tag=Unity", "/howto")]
    [InlineData("/qa", "/howto")]
    [InlineData("/practice", "/practice")]
    [InlineData("/practice/unity", "/practice")]
    [InlineData("/practice/room", "/practice/room")]                               // 「練習」は強調しない
    [InlineData("/glossary", "/glossary")]
    public void Exactly_one_main_link_is_active(string path, string expectedHref)
    {
        Assert.Equal([expectedHref], ActiveLinks(path));
    }

    [Theory]
    [InlineData("/shortcuts")]
    [InlineData("/addons")]
    [InlineData("/links")]
    [InlineData("/changelog")]
    [InlineData("/report")]
    [InlineData("/privacy")]
    public void A_page_under_more_marks_the_more_button_and_its_own_item(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvTopNav>();
        Assert.Empty(cut.FindAll(".av-topnav-links > a[aria-current='page']"));
        Assert.Equal("true", cut.Find("details.av-more").GetAttribute("data-active"));
        Assert.Equal([path], cut.FindAll("details.av-more a[aria-current='page']").Select(a => a.GetAttribute("href")!).ToList());
    }

    [Fact]
    public void Search_page_highlights_nothing()
    {
        Assert.Empty(ActiveLinks("/search"));
    }
}
