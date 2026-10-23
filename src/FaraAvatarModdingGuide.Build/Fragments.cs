using FaraAvatarModdingGuide.Models;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FaraAvatarModdingGuide.Build;

/// <summary>
/// ページの本文に共通する部品（難易度のバッジ・パンくず・TIPS のタグなど）。
/// 旧 SSR 版（Phase 5 で削除。Git の履歴にある）（Components/Shared/**）と同じ HTML・同じ class にする（CSS は Web の wwwroot/css/base.css・app.css。
/// base.css は Bootstrap 5.3.3 から使っているクラスだけを取り出したもの）。アイコンだけは Bootstrap Icons をやめて、Material の SVG にする。
/// </summary>
internal static class Fragments
{
    private static string E(string? s) => Html.E(s);

    /// <summary>Material のアイコン（装飾なので読み上げには出さない）。名前は SiteIcons の Material 側のメンバー名か bi-*。</summary>
    public static string Icon(string name, string extraClass = "") => SiteIcons.Svg(name, string.IsNullOrEmpty(extraClass) ? "av-icon" : "av-icon " + extraClass);

    public static string DifficultyBadge(string? difficulty)
    {
        if (string.IsNullOrEmpty(difficulty)) return "";
        return $"<span class=\"badge {SiteLookups.DifficultyClass(difficulty)}\">{E(difficulty)}</span>";
    }

    /// <summary>パンくず。最後の項目だけリンクにしない。</summary>
    public static string Breadcrumb(IReadOnlyList<(string? Href, string Text)> items)
    {
        var sb = new StringBuilder();
        sb.Line("<nav aria-label=\"パンくず\">");
        sb.Line("<ol class=\"breadcrumb\">");
        for (var i = 0; i < items.Count; i++)
        {
            var (href, text) = items[i];
            if (i == items.Count - 1 || href is null)
                sb.Line($"<li class=\"breadcrumb-item active\" aria-current=\"page\">{E(text)}</li>");
            else
                sb.Line($"<li class=\"breadcrumb-item\"><a href=\"{E(href)}\">{E(text)}</a></li>");
        }
        return sb.Line("</ol>").Line("</nav>").ToString();
    }

    /// <summary>項目のタグと出どころ（TipTags.razor）。</summary>
    public static string Tags(IReadOnlyList<string> tags, string source)
    {
        var sb = new StringBuilder("<div class=\"tip-tags\">");
        foreach (var tag in tags) sb.Append($"<span class=\"tip-tag\">{E(tag)}</span>");
        if (source == "discord") sb.Append($"<span class=\"tip-tag tip-source-discord\" title=\"Discord での質問・会話をもとにした項目\">{Icon("Forum")} Discord より</span>");
        return sb.Append("</div>").ToString();
    }

    /// <summary>「詳しく」（TipBody.razor）。</summary>
    public static string Body(string? html, string label = "詳しく") =>
        string.IsNullOrWhiteSpace(html) ? "" : $"<details class=\"tip-body mb-2\"><summary>{E(label)}</summary><div class=\"tip-body-content\">{html}</div></details>";

    /// <summary>用語集・何て名前？・TIPS・Q&amp;A の各項目の「キー / 記事 / 関連 / 関連語」（TipRefs.razor）。</summary>
    public static string Refs(SiteContent c, IReadOnlyList<string> shortcuts, IReadOnlyList<string> articles, IReadOnlyList<string> related, IReadOnlyList<string> keywords)
    {
        if (shortcuts.Count == 0 && articles.Count == 0 && related.Count == 0 && keywords.Count == 0) return "";
        var sb = new StringBuilder("<div class=\"tip-refs\">");
        if (shortcuts.Count > 0)
        {
            sb.Append("<div><span class=\"tip-refs-label\">キー</span>");
            foreach (var key in shortcuts)
            {
                var s = c.Shortcuts.FirstOrDefault(x => x.Key == key);
                sb.Append(s is null
                    ? $"<kbd>{E(key)}</kbd>"
                    : $"<a href=\"/shortcuts?category={Uri.EscapeDataString(s.Category)}\" class=\"text-decoration-none\" title=\"{E(s.Function)}\"><kbd>{E(key)}</kbd></a>");
            }
            sb.Append("</div>");
        }
        var linked = articles.Select(id => c.Articles.FirstOrDefault(a => a.Id == id)).Where(a => a is not null).ToList();
        if (articles.Count > 0)
            sb.Append("<div><span class=\"tip-refs-label\">記事</span>" + string.Join("<span class=\"text-body-secondary\"> / </span>", linked.Select(a => $"<a href=\"{E(a!.Url)}\">{E(a.Title)}</a>")) + "</div>");
        var terms = related.Select(id => c.Glossary.FirstOrDefault(g => g.Id == id)).Where(g => g is not null).ToList();
        if (related.Count > 0)
            sb.Append("<div><span class=\"tip-refs-label\">関連</span>" + string.Join("<span class=\"text-body-secondary\"> / </span>", terms.Select(g => $"<a href=\"/glossary#{E(g!.Id)}\">{E(g.Term)}</a>")) + "</div>");
        if (keywords.Count > 0)
            sb.Append($"<div><span class=\"tip-refs-label\">関連語</span><span class=\"tip-keywords\">{E(string.Join(" / ", keywords))}</span></div>");
        return sb.Append("</div>").ToString();
    }

    /// <summary>絞り込み欄（TipsFilter.razor）。静的 HTML では動かない（動きは Phase 2 で WebAssembly 側に移す）。</summary>
    public static string TipsFilter(IReadOnlyList<string> tags, bool hasDiscord, int count, string placeholder)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"tips-filter mb-4\">");
        sb.Append($"<input type=\"search\" class=\"form-control mb-2\" placeholder=\"{E(placeholder)}\" aria-label=\"この一覧を絞り込む\" autocomplete=\"off\" />");
        sb.Append("<div class=\"d-flex flex-wrap gap-2\" role=\"group\" aria-label=\"タグで絞り込む\">");
        sb.Append("<button type=\"button\" class=\"btn btn-sm btn-primary\" data-tag=\"\" aria-pressed=\"true\">すべて</button>");
        foreach (var tag in tags) sb.Append($"<button type=\"button\" class=\"btn btn-sm btn-outline-primary\" data-tag=\"{E(tag)}\" aria-pressed=\"false\">{E(tag)}</button>");
        if (hasDiscord) sb.Append("<button type=\"button\" class=\"btn btn-sm btn-outline-primary\" data-tag=\"source:discord\" aria-pressed=\"false\">Discord より</button>");
        sb.Append($"</div><p class=\"small text-body-secondary mt-2 mb-0\" aria-live=\"polite\">{count} 件</p></div>");
        return sb.ToString();
    }

    /// <summary>TIPS・用語集のタグ（よく使うものから）。Web の画面と同じ並び（SiteLookups.TipTags）。</summary>
    public static IReadOnlyList<string> TipTags(IEnumerable<IReadOnlyList<string>> tagLists) => SiteLookups.TipTags(tagLists);

    /// <summary>本文 HTML の h2 を拾う。2 個以下なら目次は出さない（Web の画面と同じ規則: SiteLookups.Headings）。</summary>
    public static IReadOnlyList<(string Id, string Text)> ExtractHeadings(string html) => SiteLookups.Headings(html);

    /// <summary>
    /// カテゴリの進捗の印（「3/5」。ブラウザで完了の記録があるときだけ、WebAssembly が中身を入れて見せる）。Web の CategoryProgress.razor と同じ構造。
    /// </summary>
    public static string CategoryProgress(string extraClass = "") =>
        $"<span class=\"category-progress{(extraClass.Length == 0 ? "" : " " + extraClass)}\" hidden></span>";

    /// <summary>目次の記事の横の「完了」のチェック（完了の記録があるときだけ、WebAssembly が見せる）。</summary>
    public static string DoneMark() => $"<span class=\"done-mark\" title=\"完了\" aria-label=\"完了\" role=\"img\">{Icon("CheckCircle")}</span>";

    /// <summary>「完了」の印（記事の見出しの横・記事カードの右上。完了の記録があるときだけ、WebAssembly が見せる）。</summary>
    public static string DoneBadge(string extraClass = "") =>
        $"<span class=\"done-mark badge-done{(extraClass.Length == 0 ? "" : " " + extraClass)}\">{Icon("CheckCircle")} 完了</span>";

    /// <summary>
    /// 完了したページの件数と「次に読む」リンク（Web の ProgressSummary.razor と同じ構造）。
    /// まだ 1 ページも完了していなければ表示しない（hideWhenEmpty）ときは hidden を付けておく（WebAssembly が、記録があれば外す）。
    /// </summary>
    public static string ProgressSummary(IReadOnlyList<ArticleDto> articles, bool hideWhenEmpty, bool showReset, bool showNote)
    {
        var sb = new StringBuilder();
        sb.Line($"<div class=\"progress-summary card mb-4\"{(hideWhenEmpty ? " hidden" : "")}>");
        sb.Line("<div class=\"card-body d-flex flex-wrap align-items-center gap-3\">");
        sb.Line("<div class=\"flex-grow-1\">");
        sb.Line($"<div class=\"fw-semibold\">{Icon("TaskAlt", "me-1")}<span>{articles.Count} ページ中 0 ページ完了</span></div>");
        sb.Line("<div class=\"progress-track mt-2\" aria-hidden=\"true\"><span style=\"width: 0%\"></span></div>");
        sb.Line("</div>");
        // 何も完了していないときの「次に読む」は、最初の記事（完了の記録があれば、WebAssembly が、まだ読んでいない最初の記事に替える）
        if (articles.Count > 0) sb.Line($"<a class=\"btn btn-primary\" href=\"/learn/{E(articles[0].Id)}\">次に読む: <span>{E(articles[0].Title)}</span> {Icon("ArrowForward")}</a>");
        if (showReset) sb.Line($"<button type=\"button\" class=\"btn btn-sm btn-outline-secondary\">{Icon("RestartAlt")} 記録を消す</button>");
        sb.Line("</div>");
        if (showNote) sb.Line($"<div class=\"card-footer small text-body-secondary\">{ProgressNote}</div>");
        sb.Line("</div>");
        return sb.ToString();
    }

    /// <summary>学習の進み具合の説明（/learn の ProgressSummary の下）。</summary>
    public const string ProgressNote = "各ページの最後にある「このページを完了にする」で記録できます（ページ内の「やってみよう」を最後まで進めても完了になります）。記録はこのブラウザにだけ保存され、ログインは不要です。別のブラウザや端末には引き継がれません。";
}
