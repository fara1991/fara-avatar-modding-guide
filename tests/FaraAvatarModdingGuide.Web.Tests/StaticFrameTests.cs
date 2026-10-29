using System.Text.RegularExpressions;
using Bunit;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Layout;
using FaraBlazorComponent.Razor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 静的 HTML の枠(Build の StaticFrame)と、WebAssembly が起動したあとの画面が、同じ見た目・同じ項目になること(計画書 4.10。起動の前後で本文が動かない)。
/// ピクセル差・位置の差は tools/verify/boot-diff.mjs(ブラウザ)で測る。ここでは、ずれる原因になりやすい「2 か所に書いた値」の一致を確かめる。
/// </summary>
public class StaticFrameTests : WebTestContext
{
    private static string Frame(string path) => PageCatalog.Wrap("<p>x</p>", TestData.Settings, TestData.Content, path);

    private static readonly string AppCss = File.ReadAllText(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "css", "app.css"));

    private static string Var(string name)
    {
        var block = Regex.Match(AppCss, @"\.av-static \{(?<body>[^}]*--mud-appbar-height[^}]*)\}").Groups["body"].Value;
        var m = Regex.Match(block, Regex.Escape(name) + @":\s*([^;]+);");
        Assert.True(m.Success, name + " が app.css の .av-static にありません");
        return m.Groups[1].Value.Trim();
    }

    [Fact]
    public void Static_root_never_looks_rendered_to_the_kit_boot_script()
    {
        // kit の boot-indicator.js は、#app の中に .mud-layout / .fbc-maintenance が現れたら「描画された」とみなして、読み込み表示を外し、静的な本文の inert を解く。
        // 静的な枠に付けると、起動の前に外れてしまう(起動中の表示が出ず、操作できる状態になる)
        foreach (var path in new[] { "/", "/learn/basics/transform", "/search", null })
        {
            var html = Frame(path!);
            Assert.DoesNotContain("mud-layout", html);
            Assert.DoesNotContain("fbc-maintenance", html);
            Assert.DoesNotContain("fbc-page", html);
            Assert.StartsWith("<div class=\"entry-static av-static\">", html);
        }
    }

    [Fact]
    public void Css_variables_for_the_static_frame_equal_the_site_theme()
    {
        var layout = FaraAvatarModdingGuide.Web.Services.AvatarTheme.Create().LayoutProperties;
        Assert.Equal(layout.DefaultBorderRadius, Var("--mud-default-borderradius"));
        Assert.Equal(layout.AppbarHeight, Var("--mud-appbar-height"));
        Assert.Equal(layout.DrawerWidthLeft, Var("--mud-drawer-width-left"));
        Assert.Equal(layout.DrawerMiniWidthLeft, Var("--mud-drawer-width-mini-left"));
        var typography = FaraAvatarModdingGuide.Web.Services.AvatarTheme.Create().Typography;
        Assert.Equal(typography.Default.FontSize, Var("--mud-typography-default-size"));
        Assert.Equal(typography.Body2.FontSize, Var("--mud-typography-body2-size"));
        Assert.Equal(typography.Body2.LineHeight!.ToString(System.Globalization.CultureInfo.InvariantCulture), Var("--mud-typography-body2-lineheight"));
    }

    [Fact]
    public void Static_switch_and_active_colors_equal_the_site_palette()
    {
        var theme = FaraAvatarModdingGuide.Web.Services.AvatarTheme.Create();
        string Rgba(MudBlazor.Utilities.MudColor c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        Assert.Contains($"background-color: rgba(173, 173, 177, 0.06)", AppCss);
        // サイドバー（どちらのテーマでも濃い地）のいまのページの文字は、明るいアクセント #b4f0f7（MudBlazor の主色はライトでは地と近いため、CSS で直接決める）
        Assert.Equal(2, Regex.Matches(AppCss, @"\.av-static \.fbc-sidebar[^{}]*\.mud-nav-link\.active\s*{[^}]*color: #b4f0f7;").Count);
        Assert.Contains($"background: {Rgba(theme.PaletteDark.Primary)};", AppCss);
    }

    [Fact]
    public void Footer_social_icons_and_colors_equal_the_kit()
    {
        var razor = File.ReadAllText(Path.Combine(TestData.Root, "external", "fara-blazor-component", "src", "FaraBlazorComponent.Razor", "Components", "SocialIcon.razor"));
        var brands = Regex.Matches(razor, "\\[\"(?<name>\\w+)\"\\] = \"\"\"(?<path>.*?)\"\"\"", RegexOptions.Singleline).ToDictionary(m => m.Groups["name"].Value, m => m.Groups["path"].Value);
        Assert.Equal(4, brands.Count);
        foreach (var (name, path) in brands) Assert.Equal(path, StaticFrame.SocialPaths[name]);
        foreach (var service in SocialServices.All)
        {
            Assert.Equal((service.Label, service.Color), StaticFrame.SocialServices[service.Name]);
        }
        // 設定の SNS が、フッターに同じ順で出る(起動の後は SocialLinks が、設定の Social の URL が空でないものを出す)
        var footer = Frame("/");
        var hrefs = Regex.Matches(footer, "class=\"fbc-footer-social [^\"]*\" href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(TestData.Settings.Socials.Select(s => s.Url), hrefs);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/learn")]
    [InlineData("/learn/basics")]
    [InlineData("/learn/basics/transform")]
    [InlineData("/glossary")]
    [InlineData("/practice")]
    [InlineData("/practice/unity")]
    [InlineData("/report")]
    [InlineData("/privacy")]
    public void Sidebar_items_and_highlight_equal_the_rendered_sidebar(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvNavMenu>(p => p.Add(x => x.Groups, NavGroups.Build(TestData.Content)));
        var rendered = cut.FindAll("a.mud-nav-link").Select(a => (Href: a.GetAttribute("href")!, Text: a.TextContent.Trim(), Active: a.ClassList.Contains("active"))).ToList();
        var sections = cut.FindAll(".fbc-nav-section").Select(d => d.TextContent.Trim()).ToList();

        var html = StaticFrame.Sidebar(TestData.Content, path);
        var staticLinks = Regex.Matches(html, "<a href=\"/(?<href>[^\"]*)\" class=\"(?<cls>[^\"]*)\".*?<div class=\"mud-nav-link-text\">(?<text>.*?)(?:<span class=\"category-progress[^>]*></span>)?</div></a>", RegexOptions.Singleline)
            .Select(m => (Href: m.Groups["href"].Value, Text: System.Net.WebUtility.HtmlDecode(m.Groups["text"].Value).Trim(), Active: m.Groups["cls"].Value.Contains("active"))).ToList();
        var staticSections = Regex.Matches(html, "<div class=\"fbc-nav-section\">(.*?)</div>").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

        Assert.Equal(rendered, staticLinks);
        Assert.Equal(sections, staticSections);
        Assert.Single(staticLinks, l => l.Active);                       // ちょうど 1 つだけ強調
    }

    [Fact]
    public void Sidebar_highlights_nothing_when_there_is_no_page_like_the_404()
    {
        Assert.DoesNotContain("active", Regex.Replace(StaticFrame.Sidebar(TestData.Content, null), "category-progress|mud-drawer--[a-z]+", ""));
    }

    [Fact]
    public void Static_nav_icons_are_the_same_svg_as_the_rendered_ones()
    {
        // 静的 HTML のサイドバーのアイコンの path が、WebAssembly の IconMap(MudBlazor の定数)と同じ
        foreach (var entry in SiteNav.Build(TestData.Content).SelectMany(s => s.Items))
            Assert.Equal(SiteIcons.SvgMarkup(entry.Icon), FaraAvatarModdingGuide.Web.Services.IconMap.Get(entry.Icon));
    }

    [Fact]
    public void Static_active_highlight_selector_is_more_specific_than_mudblazor_s_without_important()
    {
        // MudBlazor: .mud-navmenu.mud-navmenu-default .mud-nav-link.active:not(.mud-nav-link-disabled) = (0,5,0)。静的な枠のルールは、これより詳細度が高いこと
        // (負けると、色の変数が無い起動前は灰色・背景透明になり、起動の瞬間に強調が現れる)。!important は使わない
        var rules = Regex.Matches(AppCss, @"(?<sel>[^{}]*\.av-static \.fbc-sidebar[^{}]*\.mud-nav-link\.active)\s*\{(?<body>[^}]*)\}").ToList();
        Assert.Equal(2, rules.Count);                                    // ダークとライト
        foreach (var rule in rules)
        {
            var sel = rule.Groups["sel"].Value;
            Assert.Contains(".mud-navmenu.mud-navmenu-default", sel);
            Assert.DoesNotContain("!important", rule.Groups["body"].Value);
        }
    }
}
