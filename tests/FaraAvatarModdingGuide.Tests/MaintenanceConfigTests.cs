using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// SSR 版（Phase 5 の次の PR で消す）のお知らせの帯の CSS。WebAssembly 版の帯は kit の MaintenanceBanner（Web.Tests の MaintenanceTests）。
/// 公開の設定（wrangler.toml・Worker・workflows）の検査は、Web.Tests の DeployWorkflowTests に移した。
/// </summary>
public class MaintenanceConfigTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void Notice_banner_has_a_style_in_the_site_css()
    {
        // バナーは class だけで、インラインの style は付けない（CSP のため）。見た目は CSS にある
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Contains(".maintenance-notice {", css);
    }

    [Fact]
    public void Ssr_notice_banner_puts_the_heading_and_the_text_on_their_own_lines_in_bold()
    {
        // SSR 版（フェーズ 5 で消す）の CSS。WebAssembly 版の帯は kit の MaintenanceBanner（Web.Tests の MaintenanceTests が確かめる）
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Matches(@"\.maintenance-notice-title,\s*\.maintenance-notice-text\s*\{\s*display:\s*block;\s*\}", css);
        // 太字は、帯（.maintenance-notice）の font-weight: 700 のまま
        Assert.Matches(@"\.maintenance-notice \{[^}]*font-weight:\s*700;", css);
    }

    [Fact]
    public void Notice_band_height_is_estimated_for_two_lines()
    {
        // 見出し 1 行 + 本文 1 行。PC の目次・見出しへのジャンプの位置に使う（帯の高さの見積もり）
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Matches(@"html:has\(\.maintenance-notice\)\s*\{\s*--notice-band:\s*3\.75rem;", css);
    }

    private static string RuleBody(string css, string selector)
    {
        var match = Regex.Match(css, @"^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}", RegexOptions.Multiline);
        Assert.True(match.Success, $"{selector} の書き方が見つからない");
        return match.Groups[1].Value;
    }

    [Fact]
    public void Notice_banner_is_sticky_at_the_top_and_stays_above_the_content_but_below_the_header()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        var rule = RuleBody(css, ".maintenance-notice");

        // スクロールしても消えないよう、貼り付ける（スマホは画面の最上部）
        Assert.Matches(@"position:\s*sticky;", rule);
        Assert.Matches(@"top:\s*0;", rule);

        // 重なり順: 本文・サイドバーの上（ほかの最大は 30）、固定ヘッダー（1040）より下
        var z = int.Parse(Regex.Match(rule, @"z-index:\s*(\d+);").Groups[1].Value);
        Assert.InRange(z, 31, 1039);

        // PC（641px 以上）: 固定ヘッダーの真下に貼り付く。ヘッダーの高さ 3.5rem は、Bootstrap の box-sizing: border-box で下線を含む（App.razor で読み込み）
        Assert.Matches(@"\.app-header\s*\{[^}]*?height:\s*3\.5rem;", css);
        Assert.Contains("bootstrap@5", File.ReadAllText(RepoFile("Components/App.razor")));
        Assert.Matches(@"@media \(min-width: 641px\) \{\s*\.maintenance-notice \{\s*top:\s*3\.5rem;", css);
    }

    [Fact]
    public void Notice_banner_does_not_hide_anchors_the_article_toc_or_the_mobile_search_bar()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));

        // 見出しへのジャンプ（scroll-padding-top）と、記事右の目次（top）は、帯の下に合わせる（帯が出ているときだけ）
        Assert.Matches(@"html:has\(\.maintenance-notice\)\s*\{[^}]*scroll-padding-top:\s*calc\(var\(--notice-band\) \+ 1rem\);", css);
        Assert.Matches(@"@media \(min-width: 641px\) \{[^@]*html:has\(\.maintenance-notice\)\s*\{\s*scroll-padding-top:\s*calc\(3\.5rem \+ var\(--notice-band\) \+ 1rem\);", css);

        // 記事右の目次は、.article-aside が貼り付く幅（992px 以上）の中だけ。狭い幅では流れたまま（元の static の指定）
        Assert.Matches(@"@media \(max-width: 991\.98px\) \{\s*\.article-aside \{\s*position:\s*static;", css);
        Assert.Matches(@"@media \(min-width: 992px\) \{\s*main:has\(> \.maintenance-notice\) \.article-aside\s*\{\s*top:\s*calc\(3\.5rem \+ var\(--notice-band\) \+ 1\.25rem\);\s*max-height:\s*calc\(100vh - 5\.5rem - var\(--notice-band\)\);", css);
        Assert.DoesNotMatch(@"^main:has\(> \.maintenance-notice\) \.article-aside", css);

        // スマホの検索欄は、帯と重ならないよう貼り付けない（帯の下に流す）
        Assert.Matches(@"main > \.maintenance-notice \+ \.site-header\s*\{\s*position:\s*static;", css);
    }
}
