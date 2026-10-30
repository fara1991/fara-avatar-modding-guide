namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// 上のヘッダーの 1 項目。Href は / から始まる。Prefixes は「このページのとき強調する」パス（そのページと、その下のページ）、Exact はそのページだけ、
/// Excepts は強調から外すパス（「記事をよむ」から「はじめかた」の記事を外す、など）。Icon は「もっと見る」の中の項目だけに付く（Material のメンバー名）。
/// </summary>
public sealed record HeaderLink(string Href, string Label, IReadOnlyList<string> Prefixes, bool Exact = false, IReadOnlyList<string>? Excepts = null, string? Icon = null);

/// <summary>
/// 上のヘッダーの項目（決定したデザイン B 案）。静的 HTML（Build の StaticFrame）と WebAssembly の画面（Web の AvTopNav）が、この 1 か所から作る
/// （2 か所に書くと、起動の前後で項目・強調が食い違う）。サイドバーは無い。サイドバーにあった導線は、次のページが引き受ける:
/// カテゴリ別の記事一覧は「記事をよむ」（/learn）とホーム、リファレンスは「もっと見る」と「困ったとき」、練習は「練習」「みんなで練習」。
/// </summary>
public static class HeaderNav
{
    /// <summary>横に並ぶ項目。</summary>
    public static IReadOnlyList<HeaderLink> Main { get; } =
    [
        new("/", "ホーム", ["/"], Exact: true),
        new("/learn/getting-started", "はじめかた", ["/learn/getting-started"]),
        new("/learn", "記事をよむ", ["/learn"], Excepts: ["/learn/getting-started"]),
        new("/howto", "困ったとき", ["/howto", "/tips", "/qa"]),
        new("/practice", "練習", ["/practice", "/practice/unity"], Exact: true),
        new("/practice/room", "みんなで練習", ["/practice/room"]),
        new("/glossary", "言葉の意味", ["/glossary"]),
    ];

    /// <summary>「もっと見る」の中の項目。</summary>
    public static IReadOnlyList<HeaderLink> More { get; } =
    [
        new("/shortcuts", "ショートカットキー", ["/shortcuts"], Icon: "Keyboard"),
        new("/addons", "便利なアドオン", ["/addons"], Icon: "Extension"),
        new("/links", "リンク", ["/links"], Icon: "Link"),
        new("/changelog", "更新のお知らせ", ["/changelog"], Icon: "History"),
        new("/report", "問い合わせ", ["/report"], Icon: "MailOutline"),
        new("/privacy", "プライバシーポリシー", ["/privacy"], Icon: "PrivacyTip"),
    ];

    /// <summary>見ているページ（/ から始まるパス。クエリ・# は除く）で、この項目を「いまのページ」にするか。末尾の / は無視する。</summary>
    public static bool IsActive(HeaderLink link, string? path)
    {
        if (path is null) return false;
        var current = path.Length > 1 ? path.TrimEnd('/') : path;
        if (link.Excepts is not null && link.Excepts.Any(x => Under(current, x))) return false;
        if (link.Exact)
            return link.Prefixes.Any(p => current == p);
        return link.Prefixes.Any(p => Under(current, p));
    }

    /// <summary>「もっと見る」の中のどれかが、いまのページか（折りたたんだ「もっと見る」の見出しに印を付ける）。</summary>
    public static bool IsMoreActive(string? path) => More.Any(l => IsActive(l, path));

    private static bool Under(string current, string prefix) => current == prefix || current.StartsWith(prefix + "/", StringComparison.Ordinal);
}
