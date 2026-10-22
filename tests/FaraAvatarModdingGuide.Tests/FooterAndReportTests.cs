using FaraAvatarModdingGuide.Components.Layout;
using FaraAvatarModdingGuide.Components.Pages;
using FaraAvatarModdingGuide.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>3 サイト共通の形のフッター（サイト名 | 問い合わせ | プライバシーポリシー | © 公開年 Fara）と、問い合わせ・プライバシーポリシーのページ。</summary>
public class FooterAndReportTests
{
    private sealed class TestNav : NavigationManager
    {
        public TestNav(string uri) => Initialize("https://avatar.example/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private static async Task<string> RenderAsync<T>(string uri = "https://avatar.example/learn/basics", SiteOptions? options = null) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(new TestNav(uri));
        services.AddSingleton<IOptions<SiteOptions>>(Options.Create(options ?? new SiteOptions { Title = "Avatar Modding Guide", PublishedYear = 2026, PortfolioUrl = "https://fara-labs.com/" }));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>();
            return output.ToHtmlString();
        });
    }

    private static string Text(string html) => System.Text.RegularExpressions.Regex.Replace(System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    [Fact]
    public async Task Footer_has_the_agreed_parts_in_order()
    {
        var html = await RenderAsync<SiteFooter>();
        Assert.Equal("Avatar Modding Guide | 問い合わせ | プライバシーポリシー | © 2026 Fara", Text(html));
    }

    [Fact]
    public async Task Footer_links_go_to_the_report_page_the_privacy_page_and_the_author_homepage()
    {
        var html = await RenderAsync<SiteFooter>();
        Assert.Contains("href=\"/report?from=%2Flearn%2Fbasics\"", html);
        Assert.Contains("href=\"/privacy\"", html);
        Assert.Contains("href=\"https://fara-labs.com/\"", html);
        Assert.Contains(">Fara</a>", html);
    }

    [Fact]
    public async Task Footer_follows_the_site_options()
    {
        var html = await RenderAsync<SiteFooter>(options: new SiteOptions { Title = "別の名前", PublishedYear = 2030, PortfolioUrl = "https://example.org/" });
        Assert.Equal("別の名前 | 問い合わせ | プライバシーポリシー | © 2030 Fara", Text(html));
        Assert.Contains("href=\"https://example.org/\"", html);
    }

    [Theory]
    [InlineData("/learn/basics", "/report?from=%2Flearn%2Fbasics")]
    [InlineData("/", "/report?from=%2F")]
    [InlineData("/a/b_c-d.e", "/report?from=%2Fa%2Fb_c-d.e")]
    [InlineData("/report", "/report")]            // 報告ページ・プライバシーポリシー自体からは付けない
    [InlineData("/privacy", "/report")]
    [InlineData("/x y", "/report")]               // 英数字・- _ / . 以外を含むパス
    [InlineData("//evil.example", "/report")]
    [InlineData("/<script>", "/report")]
    [InlineData("", "/report")]
    public void Report_link_carries_the_current_page_only_when_it_is_safe(string path, string expected) =>
        Assert.Equal(expected, SiteFooterLinks.ReportHref(path));

    [Fact]
    public void A_too_long_path_is_not_carried() => Assert.Equal("/report", SiteFooterLinks.ReportHref("/" + new string('a', 300)));

    [Fact]
    public async Task Report_page_has_the_form_the_script_expects()
    {
        var html = await RenderAsync<Report>();
        foreach (var id in new[] { "report-form", "report-category", "report-message", "report-contact", "report-website", "report-count", "report-status", "report-submit", "report-turnstile", "report-done", "report-again" })
            Assert.Contains($"id=\"{id}\"", html);
        // 種類の ID は、サーバー（worker/report.js の CATEGORIES）と同じ
        Assert.Contains("value=\"bug\"", html);
        Assert.Contains("value=\"request\"", html);
        Assert.Contains("value=\"other\"", html);
        Assert.Contains("src=\"/js/report.js\"", html);
        // 送信後のお礼は、最初は隠れていて、フォームの外にある（report.js が入力欄と入れ替える）
        Assert.Matches("id=\"report-done\"[^>]*hidden", html);
        Assert.True(html.IndexOf("</form>", StringComparison.Ordinal) < html.IndexOf("id=\"report-done\"", StringComparison.Ordinal));
        Assert.Contains("maxlength=\"2000\"", html);
        // 罠の欄は、人には見えない（画面の外）。入力の候補にも出さない
        Assert.Contains("report-trap", html);
        Assert.Contains("tabindex=\"-1\"", html);
        Assert.Contains("autocomplete=\"off\"", html);
        Assert.Contains("href=\"/privacy\"", html);
    }

    [Fact]
    public async Task Privacy_page_covers_the_topics()
    {
        var text = Text(await RenderAsync<Privacy>());
        foreach (var topic in new[] { "運営者", "ブラウザに保存する情報", "アクセス解析・広告", "問い合わせで送る情報", "Turnstile", "Discord", "外部のサービス", "第三者への提供", "Google Fonts", "jsDelivr" })
            Assert.Contains(topic, text);
    }

    [Fact]
    public void Site_options_default_to_the_agreed_year_and_homepage()
    {
        var options = new SiteOptions();
        Assert.Equal(2026, options.PublishedYear);
        Assert.Equal("https://fara-labs.com/", options.PortfolioUrl);
    }

    [Fact]
    public void Shipped_settings_use_the_agreed_year_and_homepage()
    {
        var path = FindUp("appsettings.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var site = doc.RootElement.GetProperty("Site");
        Assert.Equal(2026, site.GetProperty("PublishedYear").GetInt32());
        Assert.Equal("https://fara-labs.com/", site.GetProperty("PortfolioUrl").GetString());
    }

    private static readonly SocialLink[] PortfolioSocials =
    {
        new() { Service = "github", Label = "GitHub", Url = "https://github.com/fara1991" },
        new() { Service = "twitter", Label = "Twitter", Url = "https://twitter.com/game_fara" },
        new() { Service = "twitch", Label = "Twitch", Url = "https://twitch.tv/game_fara" },
        new() { Service = "discord", Label = "Discord", Url = "https://discord.com/invite/B38eCSYyk5" },
    };

    private static SiteOptions OptionsWith(params SocialLink[] socials) =>
        new() { Title = "Avatar Modding Guide", PublishedYear = 2026, PortfolioUrl = "https://fara-labs.com/", Socials = socials.ToList() };

    [Fact]
    public async Task Footer_shows_icon_links_after_the_copyright_with_the_portfolio_urls()
    {
        var html = await RenderAsync<SiteFooter>(options: OptionsWith(PortfolioSocials));
        Assert.Contains("<a class=\"service-github\" href=\"https://github.com/fara1991\" target=\"_blank\" rel=\"noopener noreferrer\" aria-label=\"GitHub\" title=\"GitHub\">", html);
        Assert.Contains("<a class=\"service-twitter\" href=\"https://twitter.com/game_fara\" target=\"_blank\" rel=\"noopener noreferrer\" aria-label=\"Twitter\" title=\"Twitter\">", html);
        Assert.Contains("<a class=\"service-twitch\" href=\"https://twitch.tv/game_fara\" target=\"_blank\" rel=\"noopener noreferrer\" aria-label=\"Twitch\" title=\"Twitch\">", html);
        Assert.Contains("<a class=\"service-discord\" href=\"https://discord.com/invite/B38eCSYyk5\" target=\"_blank\" rel=\"noopener noreferrer\" aria-label=\"Discord\" title=\"Discord\">", html);
        Assert.True(html.IndexOf("© 2026", StringComparison.Ordinal) < html.IndexOf("footer-socials", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Footer_icons_use_the_same_svg_paths_as_the_portfolio_site()
    {
        // Components/Icon.razor（ポートフォリオ）の github / twitter / twitch / discord と同じ path の先頭
        var html = await RenderAsync<SiteFooter>(options: OptionsWith(PortfolioSocials));
        Assert.Contains("M12 .3a12 12 0 0 0-3.8 23.38", html);
        Assert.Contains("M23.95 4.57a10 10 0 0 1-2.82.77", html);
        Assert.Contains("M11.57 4.71h1.72v5.15h-1.72z", html);
        Assert.Contains("M20.317 4.3698", html);
    }

    [Fact]
    public async Task Discord_is_hidden_when_its_url_is_empty()
    {
        var html = await RenderAsync<SiteFooter>(options: OptionsWith(PortfolioSocials.Select(s => s.Service == "discord" ? new SocialLink { Service = "discord", Label = "Discord", Url = "" } : s).ToArray()));
        Assert.DoesNotContain("service-discord", html);
        Assert.Contains("service-github", html);
    }

    [Fact]
    public async Task Footer_has_no_icon_row_when_no_social_has_a_url()
    {
        var html = await RenderAsync<SiteFooter>();
        Assert.DoesNotContain("footer-socials", html);
    }

    [Fact]
    public void Shipped_socials_match_the_portfolio_site()
    {
        // ポートフォリオの wwwroot/data/profile.json の url と同じ
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FindUp("appsettings.json")));
        var socials = doc.RootElement.GetProperty("Site").GetProperty("Socials").EnumerateArray()
            .ToDictionary(s => s.GetProperty("Service").GetString()!, s => s.GetProperty("Url").GetString()!);
        Assert.Equal("https://github.com/fara1991", socials["github"]);
        Assert.Equal("https://twitter.com/game_fara", socials["twitter"]);
        Assert.Equal("https://twitch.tv/game_fara", socials["twitch"]);
        Assert.Equal("https://discord.com/invite/B38eCSYyk5", socials["discord"]);
    }

    [Fact]
    public void Side_menu_links_to_the_contact_and_privacy_pages()
    {
        var nav = File.ReadAllText(FindUp(Path.Combine("Components", "Layout", "NavMenu.razor")));
        Assert.Contains("<NavLink class=\"nav-link\" href=\"/report\">", nav);
        Assert.Matches("問い合わせ\\s*</NavLink>", nav);
        Assert.Contains("<NavLink class=\"nav-link\" href=\"/privacy\">", nav);
        Assert.Contains("プライバシーポリシー", nav);
    }

    [Fact]
    public async Task Contact_page_uses_the_new_name_and_not_the_old_one()
    {
        var html = await RenderAsync<Report>();
        Assert.Contains("<h1>問い合わせ</h1>", html);
        Assert.DoesNotContain("作者へ報告", html);
    }

    private static string FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }
}
