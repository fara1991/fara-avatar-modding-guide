using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// スマートフォンの見た目を決める app.css の規則（44px の操作・記事の表）。ブラウザでの見え方は tools/verify（Playwright）で確かめる。
/// ここでは、規則が消えたり、別の幅の @media に移ったりしていないかを見る。
/// </summary>
public class MobileCssTests
{
    private static readonly string AppCss = File.ReadAllText(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "css", "app.css"));

    /// <summary>指定の幅の @media (max-width: …) の中身（中の規則を含む）を全部返す。</summary>
    private static string MobileBlocks(string maxWidth)
    {
        var pattern = @"@media \(max-width: " + Regex.Escape(maxWidth) + @"\) \{(?<body>(?:[^{}]|\{[^{}]*\})*)\}";
        return string.Join("\n", Regex.Matches(AppCss, pattern).Select(m => m.Groups["body"].Value));
    }

    [Fact]
    public void Drawer_links_and_header_buttons_are_at_least_44px_tall_on_phones()
    {
        var phone = MobileBlocks("767.98px");
        Assert.Matches(@"\.mud-drawer \.mud-nav-link,[^{]*\.fsk-menu-toggle,[^{]*\.fsk-search-toggle,[^{]*\.btn\s*\{\s*min-height:\s*44px;", phone);
        Assert.Matches(@"\.av-drawer-theme \.mud-button-root,", phone);
        Assert.Matches(@"\.fsk-menu-toggle,\s*\.fsk-search-toggle\s*\{\s*min-width:\s*44px;", phone);
    }

    [Fact]
    public void Article_cells_wrap_so_tables_fit_the_column_and_only_wide_tables_scroll_on_small_phones()
    {
        // 見出し（th）は keep-all のまま字の途中で折らない（PR #142）。本文のセル（td）は、語が列に入らないときだけ字の途中で折る（keep-all のままだと列より広くなり、右端が切れる）
        Assert.Matches(@"\.article-body th,\s*\.article-body td\s*\{[^}]*word-break:\s*keep-all;\s*\}", AppCss);
        Assert.Matches(@"\.article-body td\s*\{\s*overflow-wrap:\s*anywhere;\s*\}", AppCss);
        // 480px 未満では、折り返しても収まらない表だけ、表の箱の中で横に送れる（tip の表と同じ形）
        var small = MobileBlocks("479.98px");
        Assert.Matches(@"\.article-body table\s*\{[^}]*display:\s*block;[^}]*overflow-x:\s*auto;", small);
    }

    [Fact]
    public void Article_code_blocks_scroll_inside_themselves_instead_of_widening_the_page()
    {
        Assert.Matches(@"\.article-body pre\s*\{[^}]*max-width:\s*100%;[^}]*overflow-x:\s*auto;", AppCss);
    }
}
