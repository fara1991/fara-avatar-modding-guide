using FaraAvatarModdingGuide.Models;
using System.Text;

namespace FaraAvatarModdingGuide.Build;

/// <summary>1 ページ分の計画。Main は main の中身、Wasm は WebAssembly が起動して引き継ぐページか。</summary>
public sealed record PagePlan(string Path, string Title, string Description, string Main, bool Wasm, bool NoIndex = false);

/// <summary>
/// 公開する全ページの一覧（固定ページ 16 + カテゴリ 10 + 記事 50 = 76 本。計画書 1.1）。
/// Wasm が true のページだけ WebAssembly を読み込む（フェーズ 3 で、すべてのページになった: 記事・カテゴリ・目次・検索・一覧・ホーム・リンク集・練習の 3 ページなど）。
/// Web プロジェクトの @page と、この一覧の Wasm が一致することを Web.Tests が確かめる。
/// </summary>
public static class PageCatalog
{
    /// <summary>WebAssembly 側に @page がある固定のパス。フェーズが進んだらここを増やす（記事・カテゴリは content.json にあるものすべて: IsWasm）。</summary>
    public static readonly IReadOnlyList<string> WasmPaths =
    [
        "/", "/learn", "/shortcuts", "/addons", "/glossary", "/howto", "/tips", "/qa", "/links", "/changelog", "/report", "/privacy", "/search",
        "/practice", "/practice/unity", "/practice/room",
    ];

    /// <summary>
    /// フェーズ 4 までは、検索エンジンに載せないページ（フェーズ 3 では維持。フェーズ 4 で見直す）。/search は検索の画面（JavaScript が無いと中身が出ない）、練習の 3 ページは 3D の本体が JavaScript で動くので、
    /// 静的 HTML だけでは中身が薄い。noindex にすると sitemap.xml からも外れる（kit は noindex のページを入れない）。
    /// </summary>
    public static readonly IReadOnlyList<string> ProvisionalNoIndex = ["/search", "/practice", "/practice/room", "/practice/unity"];

    /// <summary>このパスのページは、WebAssembly が引き継ぐか（固定のページ、または content.json にある記事・カテゴリ）。</summary>
    public static bool IsWasm(string path, SiteContent content) => WasmPaths.Contains(path) || content.IsContentPath(path);

    public static IReadOnlyList<PagePlan> Build(SiteContent content, SiteSettings settings)
    {
        var bodies = new PageBodies(content, settings);
        var site = settings.Title;
        var pages = new List<PagePlan>();

        PagePlan Page(string path, string title, string description, string main, bool noIndex = false) =>
            new(path, $"{title} - {site}", Html.Describe(description), main, IsWasm(path, content), noIndex || ProvisionalNoIndex.Contains(path));

        pages.Add(new("/", $"ホーム - {site}",
            Html.Describe("アバター改変のための Blender・Unity・VRChat の操作ガイドです。Blender でのモデリングから Unity での衣装・マテリアルの設定、VRChat へのアップロードまでをまとめています。"),
            bodies.Home(), true));
        pages.Add(Page("/learn", "目次", "Blender・Unity・VRChat の区切りごとに、上から順に読むと一通り追えるアバター改変の目次です。", bodies.LearnIndex()));
        pages.Add(Page("/shortcuts", "ショートカット一覧", "Blender でよく使うキーボードショートカットを機能別に整理した一覧です。", bodies.Shortcuts()));
        pages.Add(Page("/addons", "アドオン", "Blender の機能を拡張する便利なアドオンの紹介と、インストール方法です。", bodies.Addons()));
        pages.Add(Page("/practice", "操作の練習", "ブラウザ上の 3D ビューで、Blender と同じキー操作を試せる練習のページです。", bodies.Practice()));
        pages.Add(Page("/practice/room", "みんなで練習", "同じ 3D ビューを何人かで同時に操作できる、共同練習の部屋です。", bodies.PracticeRoom()));
        pages.Add(Page("/practice/unity", "Unity の練習", "Unity エディターに似た画面で、Modular Avatar を使った衣装の着せ替えの流れを試せる練習のページです。", bodies.PracticeUnity()));
        pages.Add(Page("/glossary", "用語集", "Blender・Unity・VRChat・Modular Avatar の用語を、読み・英語 UI の表記・短い説明と関連する記事でまとめた用語集です。", bodies.Glossary()));
        pages.Add(Page("/howto", "あの操作、何て名前？", "「頂点をくっつけたい」のようなやりたいことから、Blender・Unity・VRChat の操作の名前・メニューの場所・ショートカットを引けます。", bodies.Howto()));
        pages.Add(Page("/tips", "TIPS", "スケールの未適用、法線の裏返り、Unity でピンクになる、Modular Avatar で服がついてこないなど、アバターづくりでよくあるつまずきと直し方です。", bodies.Tips()));
        pages.Add(Page("/qa", "改変 Q&A", "非対応衣装の着せ替え、ウェイト、首すげ替え、テクスチャの色変え、表情・アニメーター、PhysBone、VRM など、アバター改変でよく出る質問と答え・手順です。", bodies.Qa()));
        pages.Add(Page("/links", "リンク集", "アバター改変で使う Unity のツール、VRChat の資料、参考になる記事・動画、話題になった BOOTH のアバター・衣装のリンク集です。", bodies.Links()));
        pages.Add(Page("/changelog", "更新履歴", $"{site} の更新履歴です。記事の追加・見た目や機能の変更を新しい順に載せています。", bodies.Changelog()));
        pages.Add(Page("/report", "問い合わせ", $"{site} の不具合・要望・ご意見を、作者に直接送るフォームです。", bodies.Report(), noIndex: true));
        pages.Add(Page("/privacy", "プライバシーポリシー", $"{site} で、どんな情報を何のために扱うかのまとめです。", bodies.Privacy()));
        pages.Add(Page("/search", "検索", "記事・用語集・何て名前？・TIPS・改変 Q&A・ショートカット・アドオン・リンク集をまとめて検索します。", bodies.Search()));

        foreach (var category in content.Categories)
        {
            pages.Add(Page($"/learn/{category.Id}", category.Title, category.Description, bodies.Category(category)));
        }
        foreach (var article in content.Articles)
        {
            var category = content.Categories.First(c => c.Id == article.Category);
            var description = string.IsNullOrWhiteSpace(article.Description) ? category.Description : article.Description;
            pages.Add(Page(article.Url, article.Title, description, bodies.Article(article)));
        }
        return pages;
    }

    /// <summary>一覧(用語集・何て名前？・TIPS・Q&amp;A)の絞り込み用の文字(data/filter.json の中身)。</summary>
    public static IReadOnlyDictionary<string, Dictionary<string, string>> FilterTexts(SiteContent content, SiteSettings settings)
    {
        var bodies = new PageBodies(content, settings);
        _ = bodies.Glossary();
        _ = bodies.Howto();
        _ = bodies.Tips();
        _ = bodies.Qa();
        return bodies.FilterTexts;
    }

    /// <summary>404.html の題名（kit の既定の形に合わせる）。</summary>
    public static string NotFoundTitle(SiteSettings settings) => $"ページが見つかりません | {settings.Title}";

    /// <summary>
    /// ページの本文を、ページ全体の静的な枠（ヘッダーの名前とリンク・本文・フッター）で包む。
    /// 先頭の要素に entry-static を付ける（読み込み表示が、WebAssembly が描くまで操作を止めるため。kit の bodyRootClass）。
    /// </summary>
    public static string Wrap(string main, SiteSettings settings, string? path = null)
    {
        var sb = new StringBuilder();
        sb.Line("<div class=\"entry-static av-static\">");
        sb.Line($"<header class=\"av-static-header\"><a class=\"fsk-brand\" href=\"/\">{Html.E(settings.Title)}</a>");
        sb.Line("<nav aria-label=\"サイト内\" class=\"av-static-nav\">");
        foreach (var (href, text) in StaticNav) sb.Line($"<a href=\"{href}\">{text}</a>");
        sb.Line("</nav></header>");
        sb.Line("<main id=\"fsk-main\" class=\"av-static-main\" tabindex=\"-1\"><article class=\"fsk-content\">");
        sb.Append(main.TrimEnd('\n')).Append('\n');
        sb.Line("</article></main>");
        sb.Line("<footer class=\"fsk-footer\"><div class=\"fsk-footer-line\">");
        sb.Line($"<span class=\"fsk-footer-name\">{Html.E(settings.Title)}</span><span class=\"fsk-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line($"<a href=\"{ReportHref(path)}\">問い合わせ</a><span class=\"fsk-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line("<a href=\"/privacy\">プライバシーポリシー</a><span class=\"fsk-footer-sep\" aria-hidden=\"true\">|</span>");
        sb.Line($"<span>© {settings.PublishedYear} <a href=\"{Html.E(settings.PortfolioUrl)}\">Fara</a></span>");
        sb.Line("</div></footer>");
        sb.Line("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// フッターの「問い合わせ」のリンク。見ているページのパスを ?from= に付ける（報告に、どのページの話かが分かるように。kit の SiteFooter・SSR 版の SiteFooterLinks と同じ規則）。
    /// 問い合わせ・プライバシーのページ、安全でないパス（英数字と - _ / . 以外を含む）、パスが無いとき（404.html）は付けない。
    /// </summary>
    public static string ReportHref(string? path)
    {
        if (path is null || path is "/report" or "/privacy" || path.Length == 0 || path.Length > 300 || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal)) return "/report";
        return path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')
            ? "/report?from=" + Uri.EscapeDataString(path)
            : "/report";
    }

    private static readonly (string Href, string Text)[] StaticNav =
    [
        ("/", "ホーム"), ("/learn", "目次"), ("/shortcuts", "ショートカット"), ("/glossary", "用語集"), ("/howto", "何て名前？"),
        ("/tips", "TIPS"), ("/qa", "改変 Q&amp;A"), ("/addons", "アドオン"), ("/links", "リンク集"), ("/changelog", "更新履歴"), ("/practice", "練習"),
    ];
}
