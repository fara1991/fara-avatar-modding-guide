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
    public void Static_switch_colors_equal_the_site_palette()
    {
        var theme = FaraAvatarModdingGuide.Web.Services.AvatarTheme.Create();
        string Rgba(MudBlazor.Utilities.MudColor c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        Assert.Contains($"background: {Rgba(theme.PaletteDark.Primary)};", AppCss);
    }

    [Fact]
    public void Home_header_does_not_rely_on_margin_collapsing_between_static_and_booted_layouts()
    {
        // 静的 HTML の本文は display: block（隣り合う余白が重なる）、起動後は flex の縦並び（足し算）。ホームの見出し部の下の余白に頼ると、起動の前後で位置が 32px ずれる
        Assert.Matches(@"\.home-header \{\s*margin-bottom: 0;\s*\}", AppCss);
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
    [InlineData("/learn/getting-started")]
    [InlineData("/learn/getting-started/install")]
    [InlineData("/learn/basics")]
    [InlineData("/learn/basics/transform")]
    [InlineData("/glossary")]
    [InlineData("/tips")]
    [InlineData("/practice")]
    [InlineData("/practice/unity")]
    [InlineData("/practice/room")]
    [InlineData("/shortcuts")]
    [InlineData("/report")]
    [InlineData("/privacy")]
    public void Sidebar_links_highlight_and_open_folds_equal_the_rendered_sidebar(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvSideNav>();
        var rendered = cut.FindAll("a").Select(a => (Text: a.TextContent.Trim(), Href: a.GetAttribute("href"), Current: a.GetAttribute("aria-current"))).ToList();
        var renderedOpen = cut.FindAll("details.av-side-fold").Select(d => d.HasAttribute("open")).ToList();
        var renderedTitles = cut.FindAll(".av-side-title").Select(t => t.TextContent.Trim()).ToList();

        (string Text, string? Href, string? Current) Parse(Match m) =>
            (System.Net.WebUtility.HtmlDecode(m.Groups["text"].Value).Trim(), m.Groups["href"].Value, m.Groups["cur"].Success ? "page" : null);
        const string Link = "<a class=\"av-side-link[^\"]*\" href=\"(?<href>[^\"]*)\"(?<cur> aria-current=\"page\")? title=\"[^\"]*\">(?:<svg.*?</svg>)?<span class=\"av-side-label\">(?<text>[^<]*)</span>";

        var html = StaticFrame.SideNavMarkup(TestData.Content, path);
        var staticLinks = Regex.Matches(html, Link, RegexOptions.Singleline).Select(Parse).ToList();
        Assert.Equal(rendered, staticLinks.Select(l => (l.Text, (string?)l.Href, l.Current)).ToList());
        Assert.Equal(renderedOpen, Regex.Matches(html, "<details class=\"av-side-fold\"( open)?>").Select(m => m.Groups[1].Success).ToList());
        Assert.Equal(renderedTitles, Regex.Matches(html, "<div class=\"av-side-title\">([^<]*)</div>").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList());
        Assert.True(staticLinks.Count(l => l.Current == "page") <= 1);   // 強調は多くても 1 つ
    }

    [Fact]
    public void Sidebar_highlights_nothing_and_opens_nothing_when_there_is_no_page_like_the_404()
    {
        var html = StaticFrame.SideNavMarkup(TestData.Content, null);
        Assert.DoesNotContain("aria-current", html);
        Assert.DoesNotContain("<details class=\"av-side-fold\" open>", html);
    }

    [Fact]
    public void Static_sidebar_is_a_drawer_with_the_same_id_and_class_as_the_kit_layout()
    {
        var html = StaticFrame.Sidebar(TestData.Content, "/");
        Assert.Contains("id=\"fbc-sidebar\"", html);
        Assert.Contains("fbc-sidebar", html);
        Assert.Contains("mud-drawer-mini", html);
    }

    [Fact]
    public void Static_nav_icons_are_the_same_svg_as_the_rendered_ones()
    {
        // 静的 HTML のサイドバーのアイコンの path が、WebAssembly の IconMap(MudBlazor の定数)と同じ
        var items = SideNav.Build(TestData.Content).SelectMany(g => g.Items).SelectMany(i => i.Children is null ? [i] : new[] { i }.Concat(i.Children));
        foreach (var item in items.Concat([new SideItem("開閉の印", "ExpandMore")]))
            Assert.Equal(SiteIcons.SvgMarkup(item.Icon), FaraAvatarModdingGuide.Web.Services.IconMap.Get(item.Icon));
    }
}
