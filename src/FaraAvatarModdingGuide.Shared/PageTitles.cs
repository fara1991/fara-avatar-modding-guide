namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// ページの題名（&lt;title&gt;・og:title）の書き方。「ページ名 | サイト名」。静的な入口ページ（Build の PageCatalog）と
/// WebAssembly の PageTitle（Razor）が、この 1 か所から作る（起動の前後でタブの題名が変わらない。kit の問い合わせ・404 も同じ形）。
/// </summary>
public static class PageTitles
{
    /// <summary>題名とサイト名の区切り。</summary>
    public const string Separator = " | ";

    /// <summary>「ページ名 | サイト名」の形にする。</summary>
    public static string Compose(string pageTitle, string siteTitle) => pageTitle + Separator + siteTitle;
}
