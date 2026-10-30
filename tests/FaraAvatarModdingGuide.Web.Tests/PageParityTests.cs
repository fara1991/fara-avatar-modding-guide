using Microsoft.Extensions.DependencyInjection;
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
        Assert.Contains("fbc-page-column--wide", cut.Markup);                       // PC で横幅 72rem（長文のページ）
    }

    private const string PrivacyEnacted = "制定:2026年10月22日";
    private const string PrivacyRevised = "最終改定:2026年10月29日";

    [Fact]
    public void Privacy_states_what_is_sent_stored_and_for_how_long()
    {
        var cut = Render<Privacy>();
        var text = TestData.TextOf(cut.Markup);
        Assert.Contains(PrivacyEnacted, text);
        Assert.Contains(PrivacyRevised, text);
        Assert.Contains("利用目的", text);
        Assert.Contains("暗号化せずそのまま保存", text);                      // 作成者の端末のパスワードは平文
        Assert.Contains("パスワードの有無", text);                            // 一覧に出る項目
        Assert.Contains("保存の期間", text);
        Assert.Contains("次に間違いが記録されるときに消えます", text);          // 10 分後の削除はタイマーではない
        Assert.NotEmpty(cut.FindAll("table tbody tr"));                      // 外部のサービスの表
        Assert.Equal(4, cut.FindAll("table thead th").Count);
        Assert.DoesNotContain("サーバーには送りません）", text);              // 見出しの言い過ぎを戻さない
        Assert.DoesNotContain("匿名", text);
        Assert.DoesNotContain("共同練習", text);                              // 「みんなで練習」に統一
        Assert.DoesNotContain("掃除", text);
        Assert.DoesNotContain("SHA-256", text);
        Assert.DoesNotContain("AdSense", text);
        Assert.DoesNotContain("avatar-modding-guide.", text);                // 保存キー名は書かない
        // 閲覧者に要らない実装の用語は出さない（「ブラウザの保存領域」「元に戻せない形」など平易に書く）
        foreach (var jargon in new[] { "ハッシュ", "トークン", "localStorage", "sessionStorage", "秘密の鍵", "秘密の乱数", "部屋の ID", "ID の目印" })
            Assert.DoesNotContain(jargon, text);
        Assert.Equal(6, cut.FindAll("table tbody th").Count);                // 狭い幅のカード表示の見出しになる行頭のセル
        Assert.All(cut.FindAll("table tbody td"), td => Assert.False(string.IsNullOrEmpty(td.GetAttribute("data-label"))));
    }

    [Fact]
    public void Home_has_the_same_structure_classes_as_the_static_page()
    {
        var cut = Render<Home>();
        foreach (var cls in new[] { "home-header", "home-title", "home-go", "home-search", "home-chips", "home-stat", "home-read", "home-group", "home-grid", "home-card", "home-badge", "home-bar", "home-links" })
        {
            Assert.Contains(cls, cut.Markup);
            Assert.Contains(cls, StaticMain("/"));
        }
        var themes = TestData.Content.Categories.Count(c => TestData.Content.ArticlesOf(c.Id).Count > 0);
        Assert.Equal(themes, cut.FindAll(".home-card").Count);
        Assert.Equal(themes, cut.FindAll(".home-card [role=progressbar]").Count);
        Assert.Equal(4, cut.FindAll(".home-links a").Count);
    }

    [Fact]
    public void Home_card_reflects_the_read_articles_with_text_and_progressbar()
    {
        var store = Services.GetRequiredService<FaraAvatarModdingGuide.Web.Services.ProgressStore>();
        var category = TestData.Content.Categories.First(c => TestData.Content.ArticlesOf(c.Id).Count >= 2);
        var articles = TestData.Content.ArticlesOf(category.Id);
        store.Set(articles[0].Id, true);
        var cut = Render<Home>();
        var card = cut.FindAll(".home-card").Single(c => c.GetAttribute("href") == "/learn/" + category.Id);
        Assert.Contains("読んでいる途中", card.TextContent);
        Assert.Contains($"読んだ 1/{articles.Count}", card.TextContent);
        var bar = card.QuerySelector("[role=progressbar]")!;
        Assert.Equal("1", bar.GetAttribute("aria-valuenow"));
        Assert.Equal(articles.Count.ToString(), bar.GetAttribute("aria-valuemax"));
        Assert.False(cut.Find(".home-read").HasAttribute("hidden"));                   // 1 件でも読んだら「読んだ記事: …」を出す
        foreach (var a in articles) store.Set(a.Id, true);
        cut = Render<Home>();
        Assert.Contains("全部読んだ", cut.FindAll(".home-card").Single(c => c.GetAttribute("href") == "/learn/" + category.Id).TextContent);
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
