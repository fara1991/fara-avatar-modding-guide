using System.Net;
using System.Text.RegularExpressions;

namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// content.json（SiteContent）の引き当てと、表示の規則。Build（静的 HTML）と Web（WebAssembly の画面）の両方が使う。
/// 同じ規則を 2 か所に書かない（静的 HTML と WebAssembly の画面で、並び・目次・前後の記事が食い違わないように）。
/// </summary>
public static class SiteLookups
{
    public static CategoryDto? CategoryById(this SiteContent c, string id) => c.Categories.FirstOrDefault(x => x.Id == id);

    public static ArticleDto? ArticleOf(this SiteContent c, string category, string slug) =>
        c.Articles.FirstOrDefault(a => a.Category == category && a.Slug == slug);

    /// <summary>記事の URL（/learn/カテゴリ/スラッグ）から探す。</summary>
    public static ArticleDto? ArticleByUrl(this SiteContent c, string url) => c.Articles.FirstOrDefault(a => a.Url == url);

    /// <summary>前後の記事（読む順。区切り → カテゴリ → 記事。SSR 版の ContentService.GetNeighbors と同じ）。</summary>
    public static (ArticleDto? Prev, ArticleDto? Next) Neighbors(this SiteContent c, ArticleDto article)
    {
        var index = -1;
        for (var i = 0; i < c.Articles.Count; i++)
        {
            if (c.Articles[i].Url == article.Url) { index = i; break; }
        }
        var prev = index > 0 ? c.Articles[index - 1] : null;
        var next = index >= 0 && index < c.Articles.Count - 1 ? c.Articles[index + 1] : null;
        return (prev, next);
    }

    /// <summary>ショートカットのカテゴリ（出てくる順）。</summary>
    public static IReadOnlyList<string> ShortcutCategories(this SiteContent c) => c.Shortcuts.Select(x => x.Category).Distinct().ToList();

    /// <summary>進捗の記録に使う記事の id（カテゴリ/スラッグ）を、読む順に。</summary>
    public static IReadOnlyList<string> ArticleIds(this SiteContent c) => c.Articles.Select(a => a.Id).ToList();

    /// <summary>
    /// サイト内のパス（/learn/… の記事・カテゴリ、固定ページ）を、WebAssembly のページとして扱うか。Build の PageCatalog と Web の PageRoutes が同じ規則を使う。
    /// 記事・カテゴリの URL は、content.json にあるものだけ（知らない URL は、静的 HTML の 404.html を出すため、再読み込みにする）。
    /// </summary>
    public static bool IsContentPath(this SiteContent c, string path)
    {
        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        return c.Categories.Any(x => "/learn/" + x.Id == normalized) || c.Articles.Any(a => a.Url == normalized);
    }

    /// <summary>ショートカットのカテゴリのバッジの色（Bootstrap のクラス）。</summary>
    public static string ShortcutBadgeClass(string category) => category switch
    {
        "一般" => "bg-primary",
        "モデリング" => "bg-success",
        "編集" => "bg-warning text-dark",
        "表示" => "bg-info text-dark",
        "選択" => "bg-secondary",
        _ => "bg-dark",
    };

    /// <summary>アドオンの種類のバッジの色。</summary>
    public static string AddonBadgeClass(string type) => type switch
    {
        "モデリング" => "bg-success",
        "アニメーション" => "bg-primary",
        "レンダリング" => "bg-warning text-dark",
        "ユーティリティ" => "bg-info text-dark",
        "ゲーム開発" => "bg-secondary",
        _ => "bg-dark",
    };

    /// <summary>むずかしさの表示名（front matter の初級・中級・上級を、やさしい言葉で出す）。Content の検索の副題（ContentService.DifficultyLabel）と同じ対応。</summary>
    public static string DifficultyLabel(string? difficulty) => difficulty switch
    {
        "初級" => "やさしい",
        "中級" => "ふつう",
        "上級" => "むずかしい",
        _ => difficulty ?? "",
    };

    /// <summary>むずかしさのバッジの色。</summary>
    public static string DifficultyClass(string? difficulty) => difficulty switch
    {
        "初級" => "bg-success",
        "中級" => "bg-warning text-dark",
        "上級" => "bg-danger",
        _ => "bg-secondary",
    };

    /// <summary>TIPS・用語集のタグ（よく使うものから）。SSR 版の ContentService.TipTags と同じ並び。</summary>
    public static IReadOnlyList<string> TipTags(IEnumerable<IReadOnlyList<string>> tagLists) =>
        tagLists.SelectMany(t => t).GroupBy(t => t).OrderBy(g => TagOrder(g.Key)).ThenByDescending(g => g.Count()).Select(g => g.Key).ToList();

    private static int TagOrder(string tag) => tag switch { "Blender" => 0, "Unity" => 1, "VRChat" => 2, "Modular Avatar" => 3, _ => 10 };

    private static readonly Regex H2Pattern = new("<h2 id=\"([^\"]+)\"[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>本文 HTML の h2（Markdig が id を付けたもの）を拾う。2 個以下なら目次は出さない（SSR 版の ArticlePage と同じ規則）。</summary>
    public static IReadOnlyList<(string Id, string Text)> Headings(string html)
    {
        var list = H2Pattern.Matches(html)
            .Select(m => (Id: WebUtility.HtmlDecode(m.Groups[1].Value), Text: WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", "")).Trim()))
            .Where(h => h.Text.Length > 0)
            .ToList();
        return list.Count > 2 ? list : [];
    }

    /// <summary>報告のきっかけのページ（?from=）のように、検索語やキーを URL に入れる前のエスケープ（Uri.EscapeDataString）。</summary>
    public static string Q(string value) => Uri.EscapeDataString(value);
}
