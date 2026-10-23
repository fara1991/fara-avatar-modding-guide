using System.Reflection;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Web.Layout;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>WebAssembly のページの一覧・サイドバーの項目・静的 HTML へ移るときの判定。</summary>
public class NavigationTests
{
    [Fact]
    public void Wasm_routes_are_the_same_in_web_build_and_the_router()
    {
        Assert.Equal(PageCatalog.WasmPaths.Order().ToArray(), PageRoutes.Wasm.Order().ToArray());   // Web と Build の一覧
        // 実際の @page（Route 属性）
        var routes = typeof(PageRoutes).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(r => r.Template)
            .Order()
            .ToArray();
        Assert.Equal(PageRoutes.Wasm.Order().ToArray(), routes);
    }

    [Theory]
    [InlineData("http://localhost/learn/basics", "http://localhost/", true)]       // 静的 HTML のページ → 読み直す
    [InlineData("http://localhost/learn/basics/", "http://localhost/", true)]
    [InlineData("http://localhost/practice#x", "http://localhost/", true)]
    [InlineData("http://localhost/no-such-page", "http://localhost/", true)]        // 存在しないページも、読み直して 404.html を出す
    [InlineData("http://localhost/links", "http://localhost/", false)]              // WebAssembly のページ → Blazor の移動
    [InlineData("http://localhost/links/", "http://localhost/", false)]
    [InlineData("http://localhost/report?from=%2Flearn", "http://localhost/", false)]
    [InlineData("http://localhost/#top", "http://localhost/", false)]
    [InlineData("https://github.com/fara1991", "http://localhost/", false)]          // 外部のサイトはブラウザに任せる
    [InlineData("http://localhost:5000/learn", "http://localhost/", false)]
    public void ShouldReload_only_for_static_pages_of_this_site(string target, string baseUri, bool expected)
    {
        Assert.Equal(expected, PageRoutes.ShouldReload(target, baseUri));
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
