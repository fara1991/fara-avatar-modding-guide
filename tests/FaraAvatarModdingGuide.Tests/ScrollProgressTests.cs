using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// スクロールの進み具合の線（.scroll-progress）。レイアウト（MainLayout.razor）・見た目（site.css）・値を入れる JS（js/scrollprogress.js）がつながっているかを確かめる。
/// 実際の動きは Playwright で確認する（ここでは、ファイルの中身の要点だけを見る）。
/// </summary>
public class ScrollProgressTests
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

    private static string RuleBody(string css, string selector)
    {
        var match = Regex.Match(css, @"^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}", RegexOptions.Multiline);
        Assert.True(match.Success, $"{selector} の書き方が見つからない");
        return match.Groups[1].Value;
    }

    [Fact]
    public void Layout_puts_a_decorative_progress_line_at_the_top_of_the_page()
    {
        var razor = File.ReadAllText(RepoFile("Components/Layout/MainLayout.razor"));
        Assert.Contains("<div class=\"scroll-progress\" aria-hidden=\"true\"></div>", razor);
        // 読み上げに出さない飾りなので、フォーカスや内容は持たせない。インラインの script / style も使わない
        Assert.DoesNotContain("<script", razor);
        Assert.DoesNotContain("<style", razor);
    }

    [Fact]
    public void App_loads_the_progress_script_with_defer_like_the_other_site_scripts()
    {
        var app = File.ReadAllText(RepoFile("Components/App.razor"));
        Assert.Contains("<script src=\"js/scrollprogress.js\" defer></script>", app);
    }

    [Fact]
    public void Progress_line_is_fixed_to_the_top_above_the_header_and_has_no_transition()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        var rule = RuleBody(css, ".scroll-progress");

        Assert.Matches(@"position:\s*fixed;", rule);
        Assert.Matches(@"top:\s*0;", rule);
        Assert.Matches(@"height:\s*2px;", rule);
        Assert.Matches(@"pointer-events:\s*none;", rule);
        Assert.Matches(@"transform:\s*scaleX\(var\(--scroll-progress, 0\)\);", rule);
        Assert.Matches(@"background:\s*var\(--grad\);", rule);
        Assert.DoesNotContain("transition", rule);

        // 固定ヘッダー（.app-header、z-index 1040）と、隠したサイドバー（1045）の上に出る
        var z = Regex.Match(rule, @"z-index:\s*(\d+);");
        Assert.True(z.Success);
        Assert.True(int.Parse(z.Groups[1].Value) > 1045);
    }

    [Fact]
    public void On_pc_the_line_sits_on_the_header_bottom_edge_in_place_of_the_old_border()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        // ヘッダーの下の線は、進み具合の線が兼ねる（ヘッダーの下端の 2px の枠線は無くす）
        Assert.DoesNotContain("border-bottom: 2px solid #8b5cf6", css);
        Assert.Contains("top: calc(3.5rem - 2px);", css);
    }

    [Fact]
    public void Progress_script_sets_a_clamped_value_and_does_nothing_when_the_page_cannot_scroll()
    {
        var js = File.ReadAllText(RepoFile("wwwroot/js/scrollprogress.js"));
        Assert.Contains("root.style.setProperty(\"--scroll-progress\"", js);
        Assert.Contains("max > 0 ? window.scrollY / max : 0", js);
        Assert.Contains("Math.min(1, Math.max(0, p))", js);
        // 画面の切り替え（Blazor）で高さが変わったときも、合わせ直す
        Assert.Contains("new MutationObserver(schedule).observe(document.body", js);
        Assert.Contains("window.addEventListener(\"scroll\", schedule, { passive: true });", js);
    }
}
