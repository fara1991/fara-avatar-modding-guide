using FaraAvatarModdingGuide.Models;
using System.Text;

namespace FaraAvatarModdingGuide.Build;

/// <summary>1 ページ分の計画。Main は main の中身、Wasm は WebAssembly が起動して引き継ぐページか、OgType は og:type（記事は article、それ以外は website）。</summary>
public sealed record PagePlan(string Path, string Title, string Description, string Main, bool Wasm, bool NoIndex = false, string OgType = PageCatalog.OgWebsite);

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

    /// <summary>og:type。記事だけ article、そのほか（ホーム・目次・一覧・練習など）は website。</summary>
    public const string OgWebsite = "website";
    public const string OgArticle = "article";

    /// <summary>
    /// 検索エンジンに載せないページ（noindex。kit は noindex のページを sitemap.xml に入れない）。
    /// /report は問い合わせフォーム（中身が無い）、/search は検索結果の画面（検索語ごとに中身が変わり、静的 HTML は枠だけで薄い）。
    /// 練習の 3 ページ（/practice・/practice/unity・/practice/room）は、フェーズ 4 で noindex をやめた（計画書 13.1）: 現行の SSR 版は
    /// noindex を付けておらず（Report 以外に noindex は無い）、noindex にすると、切り替えのときに検索結果から消える後退になる。
    /// 静的 HTML にも、操作の説明（キーの対応表・手順・共有の仕組みと制限）が 650〜1,200 文字あり、JavaScript が無くても読める。
    /// </summary>
    public static readonly IReadOnlyList<string> NoIndexPaths = ["/report", "/search"];

    /// <summary>このパスのページは、WebAssembly が引き継ぐか（固定のページ、または content.json にある記事・カテゴリ）。</summary>
    public static bool IsWasm(string path, SiteContent content) => WasmPaths.Contains(path) || content.IsContentPath(path);

    public static IReadOnlyList<PagePlan> Build(SiteContent content, SiteSettings settings)
    {
        var bodies = new PageBodies(content, settings);
        var site = settings.Title;
        var pages = new List<PagePlan>();

        PagePlan Page(string path, string title, string description, string main, string ogType = OgWebsite, string separator = "-") =>
            new(path, $"{title} {separator} {site}", Html.Describe(description), main, IsWasm(path, content), NoIndexPaths.Contains(path), ogType);

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
        pages.Add(Page("/report", "問い合わせ", $"{site} の不具合・要望・ご意見を、作者に直接送るフォームです。", bodies.Report(), separator: "|"));   // kit の ReportFormView の PageTitle(「問い合わせ | サイト名」)に合わせる(起動の後も、タブの題名が替わらない。kit の PageTitle は上書きできない)
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
            pages.Add(Page(article.Url, article.Title, description, bodies.Article(article), OgArticle));
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
    /// ページの本文を、ページ全体の静的な枠(ヘッダー・サイドバー・本文・フッター)で包む。枠は、WebAssembly が起動したあとの画面と同じ DOM 構造・同じ class にする(StaticFrame。計画書 4.10)。
    /// 先頭の要素に entry-static を付ける(読み込み表示が、WebAssembly が描くまで操作を止めるため。kit の bodyRootClass)。
    /// path が null のとき(404.html)は、サイドバーのどの項目も強調しない。
    /// </summary>
    public static string Wrap(string main, SiteSettings settings, SiteContent content, string? path = null)
    {
        var sb = new StringBuilder();
        sb.Line("<div class=\"entry-static av-static\">");
        sb.Append(StaticFrame.Header(settings));
        sb.Append(StaticFrame.Sidebar(content, path));
        sb.Line("<div class=\"mud-main-content\">");
        sb.Line("<main id=\"fsk-main\" tabindex=\"-1\"><article class=\"fsk-content\">");
        sb.Append(main.TrimEnd('\n')).Append('\n');
        sb.Line("</article></main>");
        sb.Append(StaticFrame.Footer(settings, path));
        sb.Line("</div>");
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
}
