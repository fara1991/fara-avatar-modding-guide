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

    private static IEnumerable<SideItem> AllItems() =>
        SideNav.Build(TestData.Content).SelectMany(g => g.Items).SelectMany(i => i.Children is null ? [i] : new[] { i }.Concat(i.Children));

    [Fact]
    public void Every_sidebar_link_points_to_an_existing_page()
    {
        var paths = PageCatalog.Build(TestData.Content, TestData.Settings).Select(p => p.Path).ToHashSet();
        var links = AllItems().Where(i => i.Href is not null).ToList();
        foreach (var link in links) Assert.Contains(link.Href!, paths);
        Assert.Equal(links.Count, links.Select(l => l.Href).Distinct().Count());
        Assert.All(AllItems(), i => Assert.False(string.IsNullOrEmpty(IconMap.Get(i.Icon)), i.Label));
        Assert.All(AllItems().Where(i => i.IsFold), i => Assert.Null(i.Href));
    }

    [Fact]
    public void Every_page_the_old_sidebar_and_header_listed_is_reachable_from_the_sidebar()
    {
        var reachable = AllItems().Select(i => i.Href).ToHashSet();
        foreach (var path in new[] { "/", "/learn", "/learn/getting-started", "/howto", "/practice", "/practice/unity", "/practice/room", "/glossary", "/shortcuts", "/addons", "/links", "/changelog", "/report", "/privacy" })
            Assert.Contains(path, reachable);
        // サイドバーに無い /tips・/qa は、「困ったとき」の先のページ本文のリンクから行ける
        var bodies = new PageBodies(TestData.Content, TestData.Settings);
        Assert.Contains("href=\"/tips\"", bodies.Howto());
        Assert.Contains("href=\"/qa\"", bodies.Howto());
        // 記事のあるカテゴリは、区切りの開閉の中（はじめかたは独立した項目）に全部ある
        foreach (var category in TestData.Content.Categories.Where(c => TestData.Content.ArticlesOf(c.Id).Count > 0))
            Assert.Contains($"/learn/{category.Id}", reachable);
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
/// サイドバーの「いまのページ」の強調（aria-current="page"）。ちょうど 1 つだけ強調する。
/// いまのページが入っている区切り（開閉）は、開いた状態で出す。
/// </summary>
public class SideActiveTests : WebTestContext
{
    private IRenderedComponent<AvSideNav> RenderAt(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        return Render<AvSideNav>();
    }

    private IReadOnlyList<string> ActiveLinks(string path) =>
        RenderAt(path).FindAll("a[aria-current='page']").Select(a => a.GetAttribute("href")!).ToList();

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/learn", "/learn")]                                               // 目次のページだけ（カテゴリは強調しない）
    [InlineData("/learn/basics", "/learn/basics")]
    [InlineData("/learn/basics/transform", "/learn/basics")]
    [InlineData("/learn/basics/transform#x", "/learn/basics")]
    [InlineData("/learn/unity", "/learn/unity")]
    [InlineData("/learn/getting-started", "/learn/getting-started")]               // 「はじめかた」だけ
    [InlineData("/learn/getting-started/install", "/learn/getting-started")]
    [InlineData("/howto", "/howto")]
    [InlineData("/tips", "/howto")]                                                // 「困ったとき」は /howto・/tips・/qa
    [InlineData("/tips?tag=Unity", "/howto")]
    [InlineData("/qa", "/howto")]
    [InlineData("/practice", "/practice")]
    [InlineData("/practice/unity", "/practice/unity")]                              // 「操作の練習」は強調しない
    [InlineData("/practice/room", "/practice/room")]
    [InlineData("/glossary", "/glossary")]
    [InlineData("/shortcuts", "/shortcuts")]
    [InlineData("/addons", "/addons")]
    [InlineData("/links", "/links")]
    [InlineData("/changelog", "/changelog")]
    [InlineData("/report", "/report")]
    [InlineData("/privacy", "/privacy")]
    public void Exactly_one_link_is_active(string path, string expectedHref)
    {
        Assert.Equal([expectedHref], ActiveLinks(path));
    }

    [Fact]
    public void Only_the_fold_with_the_current_page_is_open()
    {
        var cut = RenderAt("/learn/basics/transform");
        var open = cut.FindAll("details.av-side-fold[open]");
        Assert.Single(open);
        Assert.NotNull(open[0].QuerySelector("a[aria-current='page']"));
        Assert.True(cut.FindAll("details.av-side-fold").Count > 1);
        Assert.Empty(RenderAt("/").FindAll("details.av-side-fold[open]"));
    }

    [Fact]
    public void Search_page_highlights_nothing()
    {
        Assert.Empty(ActiveLinks("/search"));
    }
}
