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
    public void Every_sidebar_item_points_to_an_existing_page()
    {
        var paths = PageCatalog.Build(TestData.Content, TestData.Settings).Select(p => p.Path).ToHashSet();
        var groups = NavGroups.Build(TestData.Content);
        var items = groups.SelectMany(g => g.Items).ToList();
        Assert.True(items.Count >= 25);
        foreach (var item in items) Assert.Contains("/" + item.Href.TrimStart('/'), paths);
        Assert.Equal(items.Count, items.Select(i => i.Href).Distinct().Count());
    }

    [Fact]
    public void Sidebar_lists_every_section_with_its_categories_in_reading_order()
    {
        var groups = NavGroups.Build(TestData.Content);
        foreach (var section in TestData.Content.Sections.Where(s => TestData.Content.CategoriesIn(s.Id).Count > 0))
        {
            var group = groups.Single(g => g.Title == section.Title);
            Assert.Equal(TestData.Content.CategoriesIn(section.Id).Select(c => "learn/" + c.Id), group.Items.Select(i => i.Href));
            Assert.All(group.Items, i => Assert.False(string.IsNullOrEmpty(i.Icon)));   // アイコンが引ける
            Assert.All(group.Items, i => Assert.NotEmpty(i.ProgressIds!));              // カテゴリの進捗(3/5)に使う記事の id
        }
        // データが読めなくても、固定の項目は出る
        var fallback = NavGroups.Build(null);
        Assert.Contains(fallback.SelectMany(g => g.Items), i => i.Href == "privacy");
        Assert.DoesNotContain(fallback, g => g.Title == "Blender");
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
/// サイドバーの「いまのページ」の強調。kit の SiteNavMenu は、ホーム以外を前方一致にするので、/learn が記事のページでも強調され、カテゴリと 2 つ強調されてしまう。
/// このサイトのサイドバー(AvNavMenu)は、SSR 版と同じに、ちょうど 1 つだけ強調する。
/// </summary>
public class SidebarActiveTests : WebTestContext
{
    private IReadOnlyList<string> ActiveLinks(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvNavMenu>(p => p.Add(x => x.Groups, NavGroups.Build(TestData.Content)));
        return cut.FindAll("a.mud-nav-link.active").Select(a => a.GetAttribute("href")!).ToList();
    }

    [Theory]
    [InlineData("/", "")]
    [InlineData("/learn", "learn")]                                       // 目次だけ(カテゴリは強調しない)
    [InlineData("/learn/basics", "learn/basics")]                         // カテゴリだけ(目次は強調しない)
    [InlineData("/learn/basics/transform", "learn/basics")]               // 記事のページ: そのカテゴリだけ
    [InlineData("/learn/basics/transform#x", "learn/basics")]
    [InlineData("/tips", "tips")]
    [InlineData("/tips?tag=Unity", "tips")]
    [InlineData("/shortcuts", "shortcuts")]
    [InlineData("/practice", "practice")]
    [InlineData("/practice/unity", "practice/unity")]                     // 「操作の練習」は強調しない
    [InlineData("/practice/room", "practice/room")]
    [InlineData("/privacy", "privacy")]
    public void Exactly_one_item_is_active(string path, string expectedHref)
    {
        Assert.Equal([expectedHref], ActiveLinks(path));
    }
}
