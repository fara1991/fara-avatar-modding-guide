using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FaraAvatarModdingGuide.Build;

/// <summary>ページの本文に共通する部品（難易度のバッジ・パンくず・TIPS のタグなど）。クラス名は Web の app.css と同じ。</summary>
internal static class Fragments
{
    private static string E(string? s) => Html.E(s);

    public static string Icon(string name) => SiteIcons.Svg(name);

    public static string DifficultyBadge(string? difficulty)
    {
        if (string.IsNullOrEmpty(difficulty)) return "";
        var css = difficulty switch { "初級" => "av-badge-success", "中級" => "av-badge-warning", "上級" => "av-badge-danger", _ => "av-badge-secondary" };
        return $"<span class=\"av-badge {css}\">{E(difficulty)}</span>";
    }

    /// <summary>パンくず。最後の項目だけリンクにしない。</summary>
    public static string Breadcrumb(IReadOnlyList<(string? Href, string Text)> items)
    {
        var sb = new StringBuilder();
        sb.Line("<nav aria-label=\"パンくず\"><ol class=\"av-breadcrumb\">");
        for (var i = 0; i < items.Count; i++)
        {
            var (href, text) = items[i];
            if (i == items.Count - 1 || href is null)
                sb.Line($"<li class=\"av-breadcrumb-item active\" aria-current=\"page\">{E(text)}</li>");
            else
                sb.Line($"<li class=\"av-breadcrumb-item\"><a href=\"{E(href)}\">{E(text)}</a></li>");
        }
        return sb.Line("</ol></nav>").ToString();
    }

    public static string Tags(IReadOnlyList<string> tags, string source)
    {
        var sb = new StringBuilder("<div class=\"tip-tags\">");
        foreach (var tag in tags) sb.Append($"<span class=\"tip-tag\">{E(tag)}</span>");
        if (source == "discord") sb.Append($"<span class=\"tip-tag tip-source-discord\" title=\"Discord での質問・会話をもとにした項目\">{Icon("Forum")} Discord より</span>");
        return sb.Append("</div>").ToString();
    }

    public static string Body(string? html, string label = "詳しく") =>
        string.IsNullOrWhiteSpace(html) ? "" : $"<details class=\"tip-body\"><summary>{E(label)}</summary><div class=\"tip-body-content\">{html}</div></details>";

    /// <summary>用語集・何て名前？・TIPS・Q&amp;A の各項目の「キー / 記事 / 関連 / 関連語」。</summary>
    public static string Refs(SiteContent c, IReadOnlyList<string> shortcuts, IReadOnlyList<string> articles, IReadOnlyList<string> related, IReadOnlyList<string> keywords)
    {
        if (shortcuts.Count == 0 && articles.Count == 0 && related.Count == 0 && keywords.Count == 0) return "";
        var sb = new StringBuilder("<div class=\"tip-refs\">");
        if (shortcuts.Count > 0)
        {
            sb.Append("<div><span class=\"tip-refs-label\">キー</span> ");
            foreach (var key in shortcuts)
            {
                var s = c.Shortcuts.FirstOrDefault(x => x.Key == key);
                sb.Append(s is null
                    ? $"<kbd>{E(key)}</kbd> "
                    : $"<a href=\"/shortcuts?category={Uri.EscapeDataString(s.Category)}\" title=\"{E(s.Function)}\"><kbd>{E(key)}</kbd></a> ");
            }
            sb.Append("</div>");
        }
        var linked = articles.Select(id => c.Articles.FirstOrDefault(a => a.Id == id)).Where(a => a is not null).ToList();
        if (articles.Count > 0 && linked.Count > 0)
            sb.Append("<div><span class=\"tip-refs-label\">記事</span> " + string.Join("<span class=\"av-muted\"> / </span>", linked.Select(a => $"<a href=\"{E(a!.Url)}\">{E(a.Title)}</a>")) + "</div>");
        var terms = related.Select(id => c.Glossary.FirstOrDefault(g => g.Id == id)).Where(g => g is not null).ToList();
        if (related.Count > 0 && terms.Count > 0)
            sb.Append("<div><span class=\"tip-refs-label\">関連</span> " + string.Join("<span class=\"av-muted\"> / </span>", terms.Select(g => $"<a href=\"/glossary#{E(g!.Id)}\">{E(g.Term)}</a>")) + "</div>");
        if (keywords.Count > 0)
            sb.Append($"<div><span class=\"tip-refs-label\">関連語</span> <span class=\"tip-keywords\">{E(string.Join(" / ", keywords))}</span></div>");
        return sb.Append("</div>").ToString();
    }

    /// <summary>絞り込み欄（動作は Phase 2 で WebAssembly 側に移す。静的 HTML ではタグの一覧と件数が読める）。</summary>
    public static string TipsFilter(IReadOnlyList<string> tags, bool hasDiscord, int count, string placeholder)
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"tips-filter\"><input type=\"search\" class=\"av-input\" placeholder=\"{E(placeholder)}\" aria-label=\"この一覧を絞り込む\" autocomplete=\"off\" disabled />");
        sb.Append("<div class=\"av-flex av-flex-wrap av-gap-2\" role=\"group\" aria-label=\"タグ\">");
        sb.Append("<span class=\"av-btn av-btn-sm av-btn-primary\">すべて</span>");
        foreach (var tag in tags) sb.Append($"<span class=\"av-btn av-btn-sm av-btn-outline\">{E(tag)}</span>");
        if (hasDiscord) sb.Append("<span class=\"av-btn av-btn-sm av-btn-outline\">Discord より</span>");
        sb.Append($"</div><p class=\"av-small av-muted\">{count} 件</p></div>");
        return sb.ToString();
    }

    /// <summary>TIPS・用語集のタグ（よく使うものから）。ContentService.TipTags と同じ並び。</summary>
    public static IReadOnlyList<string> TipTags(IEnumerable<IReadOnlyList<string>> tagLists) =>
        tagLists.SelectMany(t => t).GroupBy(t => t).OrderBy(g => TagOrder(g.Key)).ThenByDescending(g => g.Count()).Select(g => g.Key).ToList();

    private static int TagOrder(string tag) => tag switch { "Blender" => 0, "Unity" => 1, "VRChat" => 2, "Modular Avatar" => 3, _ => 10 };

    private static readonly Regex H2Pattern = new("<h2 id=\"([^\"]+)\"[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>本文 HTML の h2（Markdig が id を付けたもの）を拾う。2 個以下なら目次は出さない（SSR 版の ArticlePage と同じ規則）。</summary>
    public static IReadOnlyList<(string Id, string Text)> ExtractHeadings(string html)
    {
        var list = H2Pattern.Matches(html)
            .Select(m => (Id: WebUtility.HtmlDecode(m.Groups[1].Value), Text: WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", "")).Trim()))
            .Where(h => h.Text.Length > 0)
            .ToList();
        return list.Count > 2 ? list : [];
    }
}
