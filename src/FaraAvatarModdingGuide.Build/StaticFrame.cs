using System.Text;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Build;

/// <summary>
/// 静的 HTML の枠(ヘッダー・フッター)。WebAssembly が起動したあとの画面(kit の SiteLayout + サイトの AvTopNav・AvSearchBox)と
/// 同じ DOM 構造・同じ class にして、起動の前後で本文が動かない・見た目が変わらないようにする(計画書 4.10)。
/// 起動すると Blazor が #app の中身を置き換える(二重にはならない)。枠の中の操作(検索・テーマ。上のリンクと「もっと見る」・「メニュー」は、起動の前から使える)は、起動するまで効かない(装飾)。
/// ヘッダーの項目は Shared の HeaderNav(Web の AvTopNav と同じ元)。MudBlazor の CSS の変数(--mud-*)は、起動前は無い(MudThemeProvider が起動後に入れる)ので、
/// app.css の .av-static が持つ(値は SiteTheme と同じ。Web.Tests が確かめる)。
/// </summary>
internal static class StaticFrame
{
    private static string E(string? s) => Html.E(s);

    /// <summary>SNS のアイコンの path(kit の SocialIcon と同じ。Web.Tests が kit の SocialIcon.razor と一致することを確かめる)。</summary>
    public static readonly IReadOnlyDictionary<string, string> SocialPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["discord"] = "<path d=\"M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z\"/>",
        ["github"] = "<path d=\"M12 .3a12 12 0 0 0-3.8 23.38c.6.12.83-.26.83-.57L9 21.07c-3.34.72-4.04-1.61-4.04-1.61-.55-1.39-1.34-1.76-1.34-1.76-1.08-.74.09-.73.09-.73 1.2.09 1.84 1.24 1.84 1.24 1.07 1.83 2.8 1.3 3.49 1 .1-.78.42-1.31.76-1.61-2.67-.3-5.47-1.33-5.47-5.93 0-1.31.47-2.38 1.24-3.22-.14-.3-.54-1.52.1-3.18 0 0 1-.32 3.3 1.23a11.5 11.5 0 0 1 6 0c2.28-1.55 3.29-1.23 3.29-1.23.64 1.66.24 2.88.12 3.18a4.65 4.65 0 0 1 1.23 3.22c0 4.61-2.8 5.63-5.48 5.92.42.36.81 1.1.81 2.22l-.01 3.29c0 .31.2.69.82.57A12 12 0 0 0 12 .3\"/>",
        ["twitter"] = "<path d=\"M23.95 4.57a10 10 0 0 1-2.82.77 4.96 4.96 0 0 0 2.16-2.72c-.95.55-2 .96-3.12 1.19a4.92 4.92 0 0 0-8.39 4.48A13.96 13.96 0 0 1 1.64 3.16a4.82 4.82 0 0 0-.67 2.48c0 1.71.87 3.21 2.19 4.1a4.9 4.9 0 0 1-2.23-.62v.06a4.92 4.92 0 0 0 3.95 4.83 5 5 0 0 1-2.21.08 4.94 4.94 0 0 0 4.6 3.42A9.87 9.87 0 0 1 0 19.54a14 14 0 0 0 7.56 2.21c9.05 0 14-7.5 14-13.98l-.02-.64A9.94 9.94 0 0 0 24 4.59z\"/>",
        ["twitch"] = "<path d=\"M11.57 4.71h1.72v5.15h-1.72zm4.72 0H18v5.15h-1.71zM6 0 1.71 4.29v15.42h5.15V24l4.28-4.29h3.43L22.29 12V0zm14.57 11.14-3.43 3.43h-3.43l-3 3v-3H6.86V1.71h13.71z\"/>",
    };

    /// <summary>SNS の色(kit の SocialServices と同じ。Web.Tests が確かめる)。</summary>
    public static readonly IReadOnlyDictionary<string, (string Label, string Color)> SocialServices = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["github"] = ("GitHub", "#6e40c9"),
        ["twitter"] = ("Twitter", "#1d9bf0"),
        ["twitch"] = ("Twitch", "#9146ff"),
        ["discord"] = ("Discord", "#5865f2"),
    };

    private const string IconClasses = "mud-icon-root mud-svg-icon mud-icon-size-medium";

    /// <summary>MudBlazor の Material アイコン(kit の MudIcon と同じ形。path は MudBlazor の定数)。</summary>
    private static string MudSvg(string materialName, string extraClasses = "") =>
        $"<svg class=\"{IconClasses}{extraClasses}\" focusable=\"false\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" role=\"img\">{SiteIcons.SvgMarkup(materialName)}</svg>";

    /// <summary>ヘッダー(PC: 名前・各リンク・検索欄・ライト/ダークのスイッチ。スマホ: ハンバーガー・名前・検索ボタン)。PC とスマホの出し分けは CSS(.av-pc / .av-sp)。</summary>
    public static string Header(SiteSettings settings, string? path)
    {
        var sb = new StringBuilder();
        sb.Line("<header class=\"mud-appbar mud-appbar-fixed-top mud-elevation-0 fbc-header\">");
        sb.Line("<div role=\"toolbar\" class=\"mud-toolbar mud-toolbar-appbar\">");
        sb.Line("<div class=\"fbc-scroll-progress\" aria-hidden=\"true\" style=\"--scroll-progress: 0.0000;\"></div>");
        sb.Line($"<span class=\"mud-button-root mud-icon-button mud-inherit-text mud-ripple mud-ripple-icon fbc-menu-toggle av-sp\" aria-hidden=\"true\"><span class=\"mud-icon-button-label\">{MudSvg("Menu")}</span></span>");
        sb.Line($"<a class=\"fbc-brand\" href=\"/\" title=\"ホームへ\">{E(settings.Title)}</a>");
        // 各リンク(PC)。Web の AvTopNav と同じ構造。いまのページには aria-current を付ける
        sb.Append(TopNav(path));
        // 検索欄(PC)。Web の AvSearchBox と同じ構造
        sb.Line("<div class=\"av-search av-pc\" role=\"search\" aria-hidden=\"true\">");
        sb.Line("<svg class=\"av-search-icon\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\"><circle cx=\"11\" cy=\"11\" r=\"7\"></circle><path d=\"M20 20l-3.5-3.5\"></path></svg>");
        sb.Line("<input type=\"search\" class=\"av-search-input\" placeholder=\"知りたいことを入力（例: ウェイト、ループカット）\" autocomplete=\"off\" spellcheck=\"false\" tabindex=\"-1\" readonly=\"readonly\" aria-hidden=\"true\">");
        sb.Line("<span class=\"av-search-hint\" aria-hidden=\"true\"><kbd>Ctrl</kbd><kbd>K</kbd></span>");
        sb.Line("</div>");
        // ライト／ダークのスイッチ(PC)。MudSwitch の見た目を、色の変数(--mud-palette-*。起動前は無い)に頼らず CSS(.av-sw)で描く。ダーク／ライトは html の data-bs-theme で切り替える
        sb.Line("<div class=\"av-sw av-pc\" aria-hidden=\"true\"><span class=\"av-sw-track\"></span><span class=\"av-sw-thumb\">");
        sb.Line($"<span class=\"av-sw-moon\">{MudSvg("DarkMode")}</span><span class=\"av-sw-sun\">{MudSvg("LightMode")}</span>");
        sb.Line("</span></div>");
        // 検索ボタン(スマホ)
        sb.Line($"<span class=\"mud-button-root mud-icon-button mud-inherit-text mud-ripple mud-ripple-icon fbc-search-toggle av-sp\" aria-hidden=\"true\"><span class=\"mud-icon-button-label\">{MudSvg("Search")}</span></span>");
        sb.Line("</div>");
        sb.Line("</header>");
        return sb.ToString();
    }

    /// <summary>上のヘッダーの 1 つのリンク。Web の AvNavLink と同じ構造。pop は「もっと見る」・メニューの中の項目(アイコン付き)。</summary>
    internal static string NavLink(HeaderLink link, string? path, bool pop)
    {
        var active = HeaderNav.IsActive(link, path) ? " aria-current=\"page\"" : "";
        var icon = pop && link.Icon is not null ? SiteIcons.Svg(link.Icon) : "";
        var cls = pop ? "av-pop-link" : "fbc-topnav-link av-nav-link";
        return $"<a class=\"{cls}\" href=\"{E(link.Href)}\"{active}>{icon}{E(link.Label)}</a>";
    }

    private static string Flat(string? path)
    {
        var sb = new StringBuilder();
        foreach (var link in HeaderNav.Main) sb.Append(NavLink(link, path, true));
        sb.Append("<div class=\"av-nav-label\">そのほか</div>");
        foreach (var link in HeaderNav.More) sb.Append(NavLink(link, path, true));
        return sb.ToString();
    }

    /// <summary>
    /// 上のヘッダーの各リンク(Web の AvTopNav と同じ構造・同じ class。項目・並び・強調は Shared の HeaderNav)。
    /// 幅ごとの見せ方は CSS: 1181px 以上は横に並べて「もっと見る」、768〜1180px は「メニュー」1 つ。768px 未満の縦のメニュー(.av-nav-flat)は、
    /// 起動の後はハンバーガーの中に出る。静的 HTML では出さず、JavaScript が無いスマホだけ、本文の後ろに出す(index.html の noscript)。path が null(404.html)は、どれも強調しない。
    /// </summary>
    public static string TopNav(string? path)
    {
        var sb = new StringBuilder();
        sb.Append("<nav class=\"av-topnav\" aria-label=\"メインメニュー\"><div class=\"av-topnav-links\">");
        foreach (var link in HeaderNav.Main) sb.Append(NavLink(link, path, false));
        var moreActive = HeaderNav.IsMoreActive(path) ? " data-active=\"true\"" : "";
        sb.Append($"<details class=\"av-more\"{moreActive}><summary class=\"fbc-topnav-link av-more-summary\">もっと見る{SiteIcons.Svg("ExpandMore")}</summary><div class=\"av-pop\">");
        foreach (var link in HeaderNav.More) sb.Append(NavLink(link, path, true));
        sb.Append("</div></details></div>");
        sb.Append($"<details class=\"av-menu\"><summary class=\"fbc-topnav-link av-menu-summary\">{SiteIcons.Svg("Menu")}メニュー</summary><div class=\"av-pop\">{Flat(path)}</div></details>");
        sb.Append("</nav>");
        return sb.ToString();
    }

    /// <summary>JavaScript が無いスマホ用の縦のメニュー(本文の後ろ。通常は出さない。index.html の noscript の CSS が出す)。</summary>
    public static string NoScriptMenu(string? path) =>
        $"<nav class=\"av-nav-flat av-nav-noscript\" aria-label=\"サイトメニュー\">{Flat(path)}</nav>\n";

    /// <summary>フッター(サイト名 | 問い合わせ | プライバシーポリシー | © 公開年 作者 と、SNS のアイコン)。kit の SiteFooter と同じ構造。</summary>
    public static string Footer(SiteSettings settings, string? path)
    {
        var sb = new StringBuilder();
        sb.Line("<footer class=\"fbc-footer\"><div class=\"fbc-footer-line\">");
        sb.Line($"<span class=\"fbc-footer-name\">{E(settings.Title)}</span><span class=\"fbc-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line($"<a href=\"{PageCatalog.ReportHref(path)}\">問い合わせ</a><span class=\"fbc-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line("<a href=\"/privacy\">プライバシーポリシー</a><span class=\"fbc-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line($"<span>© {settings.PublishedYear} <a href=\"{E(settings.PortfolioUrl)}\">Fara</a></span>");
        var socials = settings.Socials.Where(s => IsSafeUrl(s.Url)).ToList();
        if (socials.Count > 0)
        {
            sb.Append("<span class=\"fbc-footer-socials\">");
            foreach (var social in socials)
            {
                var (label, color) = SocialServices.TryGetValue(social.Service, out var known) ? known : (social.Service, "#7c3aed");
                var iconPath = SocialPaths.TryGetValue(social.Service, out var p) ? p : SocialPaths["github"];
                sb.Append($"<a class=\"fbc-footer-social fbc-service-{E(social.Service)}\" href=\"{E(social.Url)}\" target=\"_blank\" rel=\"noopener noreferrer\" aria-label=\"{E(label)}\" title=\"{E(label)}\" style=\"--brand: {color}\">" +
                          $"<svg class=\"icon \" viewBox=\"0 0 24 24\" width=\"13\" height=\"13\" aria-hidden=\"true\" focusable=\"false\" fill=\"currentColor\" stroke=\"none\">{iconPath}</svg></a>");
            }
            sb.Line("</span>");
        }
        sb.Line("</div></footer>");
        return sb.ToString();
    }

    /// <summary>https:// の絶対 URL だけ(kit の SocialLinks.IsSafeUrl と同じ)。</summary>
    internal static bool IsSafeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);
}
