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
        Assert.Matches(@"\.av-drawer-theme \.mud-button-root,[^{]*\.fbc-menu-toggle,[^{]*\.fbc-search-toggle,[^{]*\.btn\s*\{\s*min-height:\s*44px;", phone);
        Assert.Matches(@"\.av-side-link\s*\{[^}]*min-height:\s*44px;", AppCss);                          // サイドバーのリンク（PC・スマホ共通）
        Assert.DoesNotMatch(@"\.av-side-sublink\s*\{[^}]*min-height", AppCss);                          // 区切りの中のカテゴリも、同じ 44px（下げない）
        Assert.Matches(@"\.fbc-menu-toggle,\s*\.fbc-search-toggle\s*\{\s*min-width:\s*44px;", phone);
    }

    [Fact]
    public void Article_links_wrap_so_a_long_url_does_not_widen_the_page()
    {
        // 本文のリンクに長い URL がそのまま文字として出ていても、ページ全体が横に動かない（360px で 455px まで広がった）
        Assert.Matches(@"\.article-body a:not\(\.btn\)\s*\{\s*overflow-wrap:\s*anywhere;\s*\}", AppCss);
    }

    [Fact]
    public void Linked_article_images_keep_their_caption_style_and_the_progress_button_is_44px()
    {
        // 画像を、元の大きさで開くリンクで包んでも、直後の斜体 1 行がキャプションの見た目になる
        Assert.Matches(@"\.article-body p > a:has\(> img\) \+ em\s*,", AppCss);
        // 「読んだ」にするボタンは、PC でも 44px
        Assert.Matches(@"\.btn\.btn-progress\s*\{\s*min-height:\s*44px;\s*\}", AppCss);
        // コードブロックは、折り返さず箱の中で横に送る（木構造の図の字下げが崩れないように。狭い幅でも pre-wrap にしない）
        Assert.DoesNotMatch(@"\.article-body pre\s*\{[^}]*white-space:\s*pre-wrap", AppCss);
    }

    [Fact]
    public void Header_menu_toggle_is_44px_on_pc_too()
    {
        // サイドバーの開閉ボタンは、PC でもヘッダーの常設の操作（768〜1024px は別の規則で押せる範囲を広げている）
        Assert.Matches(@"@media \(min-width: 1025px\) \{\s*\.fbc-header \.fbc-menu-toggle\s*\{\s*min-width:\s*44px;\s*min-height:\s*44px;", AppCss);
    }

    [Fact]
    public void Footer_stays_in_two_rows_at_360px()
    {
        // 幅 360px では、名前・問い合わせ・プライバシーポリシーの 1 段目に入るよう、項目の間と左右の余白を詰める（kit の既定のままだと SNS が 3 段目に落ちる）
        var narrow = MobileBlocks("379.98px");
        Assert.Matches(@"\.fbc-footer-line\s*\{\s*column-gap:\s*0\.5rem;\s*\}", narrow);
        Assert.Matches(@"\.fbc-footer\s*\{\s*padding-left:\s*0\.75rem;\s*padding-right:\s*0\.75rem;\s*\}", narrow);
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
