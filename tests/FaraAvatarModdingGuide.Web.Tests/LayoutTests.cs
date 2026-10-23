using Bunit;
using FaraAvatarModdingGuide.Web.Layout;
using FaraSiteKit.Core;
using FaraSiteKit.Razor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>共通のレイアウト（kit の SiteLayout）に、このサイトの中身（サイドバー・お知らせ・フッター）が入っているか。</summary>
public class LayoutTests : WebTestContext
{
    private IRenderedComponent<MainLayout> RenderLayout(MaintenanceInfo maintenance)
    {
        Services.AddSingleton(maintenance);
        return Render<MainLayout>(p => p.Add(x => x.Body, (RenderFragment)(b => b.AddContent(0, "本文のテスト"))));
    }

    [Fact]
    public void Notice_state_shows_the_banner_with_the_message_above_the_page()
    {
        var cut = RenderLayout(new MaintenanceInfo(MaintenanceState.Notice, "10月25日（土）20:00ごろ、サイトを更新します"));
        var banner = cut.Find(".fsk-notice");
        Assert.Equal("status", banner.GetAttribute("role"));
        Assert.Contains("【お知らせ】", banner.TextContent);
        Assert.Contains("10月25日（土）20:00ごろ、サイトを更新します", banner.TextContent);
        Assert.Contains("本文のテスト", cut.Markup);
    }

    [Fact]
    public void Notice_without_a_message_uses_the_default_text()
    {
        var cut = RenderLayout(new MaintenanceInfo(MaintenanceState.Notice, ""));
        Assert.Contains("まもなくメンテナンスを行います。", cut.Find(".fsk-notice").TextContent);
    }

    [Fact]
    public void Normal_state_shows_no_banner()
    {
        var cut = RenderLayout(MaintenanceInfo.Off);
        Assert.Empty(cut.FindAll(".fsk-notice"));
    }

    [Fact]
    public void Layout_has_the_site_header_sidebar_fixed_footer_and_scroll_progress_line()
    {
        var cut = RenderLayout(MaintenanceInfo.Off);
        Assert.Contains("Avatar Modding Guide", cut.Find(".fsk-brand").TextContent);
        Assert.NotEmpty(cut.FindAll("#fsk-sidebar"));                                   // サイドバー（Mini ドロワー）
        Assert.NotEmpty(cut.FindAll(".fsk-nav-section"));                               // 区切りの見出し
        Assert.Contains(cut.FindAll(".fsk-sidebar a"), a => a.GetAttribute("href") == "privacy");
        var footer = cut.Find("footer.fsk-footer");                                     // 画面の下に固定のフッター（CSS は kit）
        Assert.Contains("問い合わせ", footer.TextContent);
        Assert.Contains("プライバシーポリシー", footer.TextContent);
        Assert.Contains("© 2026", footer.TextContent);
        Assert.Equal(4, footer.QuerySelectorAll(".fsk-footer-socials a").Length);        // GitHub・Twitter・Twitch・Discord
        // スクロールの進み具合の線（読み上げには出さない）。SSR 版の ScrollProgressTests の置き換え
        Assert.Equal("true", cut.Find(".fsk-scroll-progress").GetAttribute("aria-hidden"));
        Assert.NotEmpty(cut.FindAll(".av-drawer-theme"));                               // スマホのメニューの中のテーマのボタン
    }

    [Fact]
    public void Scroll_progress_script_is_loaded_by_the_template_and_styled_by_the_kit_css()
    {
        var html = File.ReadAllText(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "index.html"));
        Assert.Contains("_content/FaraSiteKit.Razor/js/scroll-progress.js", html);
        var css = File.ReadAllText(Path.Combine(TestData.Root, "external", "fara-site-kit", "src", "FaraSiteKit.Razor", "wwwroot", "css", "fara-site-kit.css"));
        Assert.Contains(".fsk-scroll-progress::before", css);
        Assert.Contains("--scroll-progress", css);
    }
}
