using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Web.Pages;
using Bunit;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// WebAssembly の画面（Razor）と、Build が書く静的 HTML で、本文のテキストが同じことを確かめる
/// （起動の前後で内容が変わらない。計画書 4.10）。構造・class の細部は、フェーズ 4 のスクリーンショット差で見る。
/// </summary>
public class PageParityTests : WebTestContext
{
    private static string StaticMain(string path) => PageCatalog.Build(TestData.Content, TestData.Settings).Single(p => p.Path == path).Main;

    [Fact]
    public void Home_text_matches_the_static_page()
    {
        var cut = Render<Home>();
        Assert.Equal(TestData.TextOf(StaticMain("/")), TestData.TextOf(cut.Markup));
    }

    [Fact]
    public void Links_text_matches_the_static_page()
    {
        var cut = Render<Links>();
        Assert.Equal(TestData.TextOf(StaticMain("/links")), TestData.TextOf(cut.Markup));
    }

    [Fact]
    public void Changelog_text_matches_the_static_page()
    {
        var cut = Render<Changelog>();
        Assert.Equal(TestData.TextOf(StaticMain("/changelog")), TestData.TextOf(cut.Markup));
    }

    [Fact]
    public void Privacy_text_matches_the_static_page()
    {
        var cut = Render<Privacy>();
        Assert.Equal(TestData.TextOf(StaticMain("/privacy")), TestData.TextOf(cut.Markup));
        Assert.Contains("fsk-page-column--wide", cut.Markup);                       // PC で横幅 72rem（長文のページ）
    }

    [Fact]
    public void Home_cards_are_the_same_as_the_static_cards()
    {
        Assert.Equal(PageBodies.HomeCards.Select(c => (c.Href, c.Icon, c.Title, c.Text)).ToArray(), Home.Cards);
        Assert.Equal(PageBodies.RecentChangelogCount, Home.RecentCount);
    }

    [Fact]
    public void Home_roadmap_has_the_same_structure_classes_as_the_static_page()
    {
        var cut = Render<Home>();
        foreach (var cls in new[] { "home-header", "home-title", "roadmap-section", "roadmap-step", "roadmap-marker", "roadmap-articles", "changelog-recent" })
        {
            Assert.Contains(cls, cut.Markup);
            Assert.Contains(cls, StaticMain("/"));
        }
        Assert.Equal(TestData.Content.Articles.Count, cut.FindAll(".roadmap-articles li").Count);
    }
}

/// <summary>データが読めなかったとき（content.json が無い）は、案内を出して落ちない。</summary>
public class MissingDataTests : WebTestContext
{
    public MissingDataTests() : base(contentLoaded: false) { }

    [Fact]
    public void Pages_show_a_notice_instead_of_failing()
    {
        Assert.Contains("読み込めませんでした", Render<Home>().Markup);
        Assert.Contains("読み込めませんでした", Render<Links>().Markup);
        Assert.Contains("読み込めませんでした", Render<Changelog>().Markup);
        Assert.Contains("プライバシーポリシー", Render<Privacy>().Markup);   // データ無しでも読める
    }
}
