using System.Text;

namespace FaraAvatarModdingGuide.Build;

/// <summary>
/// 各ページの本文（main の中身）の静的 HTML。現行の SSR 版（Components/Pages/**）と同じ文言・並び・class で出す
/// （CSS は Web の wwwroot/css/base.css = Bootstrap 5.3.3 から使っているクラスだけを取り出したもの、と app.css）。
/// アイコンは Bootstrap Icons ではなく Material の SVG（Fragments.Icon）。
/// ブラウザで動く部分（絞り込み・検索・進捗・3D）は含めない。それらは WebAssembly 側（フェーズ 2〜3）で動く。
/// </summary>
internal sealed class PageBodies(SiteContent c, SiteSettings s)
{
    private static string E(string? text) => Html.E(text);
    private static string Icon(string name, string extra = "") => Fragments.Icon(name, extra);

    /// <summary>見出し(h1)とリード文。lead は HTML（リンクを含められる）。</summary>
    private static string Heading(string title, string lead) => $"<h1>{E(title)}</h1>\n<p class=\"lead\">{lead}</p>\n";

    // ------------------------------------------------------------ ホーム・目次

    public string Home()
    {
        var sb = new StringBuilder();
        sb.Line("<div class=\"home-header\">");
        sb.Line($"<h1 class=\"home-title\">{E(s.Title)}</h1>");
        sb.Line("<p class=\"home-lead mb-3\">");
        sb.Line("アバター改変のための Blender・Unity・VRChat の操作ガイドです。Blender でのモデリングから Unity での衣装・マテリアルの設定、VRChat へのアップロードまでをまとめています。<br />");
        sb.Line("初めての方は下の目次を上から順に読んでください。");
        sb.Line("</p>");
        sb.Append($"<p class=\"small text-body-secondary mb-3\">対象バージョン: Blender {E(s.BlenderVersion)} / Unity {E(s.UnityVersion)}（VRChat SDK3）");
        if (!string.IsNullOrEmpty(s.RepositoryUrl)) sb.Append($"<span> · </span><a href=\"{E(s.RepositoryUrl)}\" target=\"_blank\" rel=\"noopener\">GitHub で修正を提案する</a>");
        sb.Line("</p>");
        sb.Line($"<a href=\"/learn\" class=\"btn btn-primary btn-lg\">{Icon("MenuBook")} はじめから読む</a>");
        sb.Line("</div>");

        sb.Line("<section class=\"mb-5\">");
        sb.Line("<h2 class=\"h3 mb-3\">目次</h2>");
        sb.Append(Roadmap());
        sb.Line("</section>");

        sb.Line("<section class=\"mb-5\">");
        sb.Line("<h2 class=\"h3 mb-3\">困ったときに引く</h2>");
        sb.Line("<div class=\"row g-4\">");
        foreach (var (href, icon, title, text) in HomeCards)
        {
            sb.Line("<div class=\"col-md-4\">");
            sb.Line($"<a href=\"{href}\" class=\"card h-100 text-decoration-none article-card\"><div class=\"card-body\"><h3 class=\"h5 card-title\">{Icon(icon, "me-2")}{E(title)}</h3><p class=\"card-text text-body-secondary\">{E(text)}</p></div></a>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        sb.Line("</section>");

        sb.Line("<section class=\"mb-4\">");
        sb.Line("<h2 class=\"h3 mb-3\">更新履歴</h2>");
        sb.Line("<ul class=\"changelog-recent list-unstyled mb-2\">");
        foreach (var e in c.Changelog.Take(RecentChangelogCount))
        {
            sb.Line($"<li><span class=\"changelog-date\">{E(e.Date[5..].Replace('-', '/'))}</span> {E(e.Title)}</li>");
        }
        sb.Line("</ul>");
        sb.Line("<a href=\"/changelog\" class=\"btn btn-sm btn-outline-primary\">すべて見る</a>");
        sb.Line("</section>");
        return sb.ToString();
    }

    /// <summary>ホームに出す更新履歴の件数（細かい内容は /changelog）。</summary>
    public const int RecentChangelogCount = 3;

    /// <summary>ホームの「困ったときに引く」（SSR 版 Home.razor と同じ文言）。Icon は Material のメンバー名。</summary>
    public static readonly IReadOnlyList<(string Href, string Icon, string Title, string Text)> HomeCards =
    [
        ("/howto", "HelpOutline", "あの操作、何て名前？", "「頂点をくっつけたい」から、操作の名前・メニュー・キーを引けます。"),
        ("/tips", "Lightbulb", "TIPS", "Unity でピンクになる、服がついてこないなど、よくあるつまずきと直し方。"),
        ("/qa", "Chat", "改変 Q&A", "非対応衣装・ウェイト・首すげ替え・色変えなど、改変の相談でよく出た質問と手順。"),
        ("/glossary", "Article", "用語集", "Blender・Unity・VRChat の言葉を、英語 UI の表記と一緒に。"),
        ("/shortcuts", "Keyboard", "ショートカット一覧", "キーから機能を逆引きできます。"),
        ("/addons", "Extension", "アドオン", "作業を楽にするアドオンの紹介。"),
        ("/links", "Link", "リンク集", "Unity のツール、参考になる記事、話題になった BOOTH のアバター・衣装。"),
        ("/practice", "SportsEsports", "操作の練習", "ブラウザ上で、Blender と同じキー操作を試せます。"),
    ];

    /// <summary>目次（Roadmap.razor）。区切りごとに、カテゴリを読む順に並べる。</summary>
    public string Roadmap()
    {
        var sb = new StringBuilder();
        foreach (var section in c.Sections)
        {
            var categories = c.CategoriesIn(section.Id);
            sb.Line($"<section class=\"roadmap-section\" id=\"section-{E(section.Id)}\" aria-labelledby=\"section-title-{E(section.Id)}\">");
            sb.Line($"<h3 class=\"roadmap-section-title\" id=\"section-title-{E(section.Id)}\">{Icon(section.Icon)} {E(section.Title)}</h3>");
            sb.Line($"<p class=\"roadmap-section-desc text-body-secondary\">{E(section.Description)}</p>");
            if (categories.Count == 0)
            {
                sb.Line("<p><span class=\"badge bg-secondary\">準備中</span></p>");
            }
            else
            {
                sb.Line("<ol class=\"roadmap list-unstyled\">");
                for (var i = 0; i < categories.Count; i++)
                {
                    var category = categories[i];
                    var articles = c.ArticlesOf(category.Id);
                    sb.Line("<li class=\"roadmap-step\">");
                    sb.Line($"<div class=\"roadmap-marker\">{i + 1}</div>");
                    sb.Line("<div class=\"roadmap-body\">");
                    sb.Line($"<h4 class=\"h5 mb-1\">{Icon(category.Icon, "me-1")}<a href=\"/learn/{E(category.Id)}\" class=\"text-decoration-none\">{E(category.Title)}</a></h4>");
                    sb.Line($"<p class=\"text-body-secondary mb-2\">{E(category.Description)}</p>");
                    if (articles.Count == 0)
                    {
                        sb.Line("<span class=\"badge bg-secondary\">準備中</span>");
                    }
                    else
                    {
                        sb.Line("<ol class=\"roadmap-articles\">");
                        foreach (var a in articles) sb.Line($"<li data-article=\"{E(a.Id)}\"><a href=\"{E(a.Url)}\">{E(a.Title)}</a> {Fragments.DifficultyBadge(a.Difficulty)}</li>");
                        sb.Line("</ol>");
                    }
                    sb.Line("</div>");
                    sb.Line("</li>");
                }
                sb.Line("</ol>");
            }
            sb.Line("</section>");
        }
        return sb.ToString();
    }

    public string LearnIndex() =>
        Heading("目次", "Blender・Unity・VRChat の区切りごとに、上から順に読むと一通り追えます。最後の「通しで作る」では、衣装づくりを最初から最後まで行います。") + Roadmap();

    // ------------------------------------------------------------ カテゴリ・記事

    public string Category(CategoryDto category)
    {
        var sb = new StringBuilder();
        var crumbs = new List<(string?, string)> { ("/learn", "目次") };
        if (c.SectionOf(category) is { } section) crumbs.Add(($"/learn#section-{section.Id}", section.Title));
        crumbs.Add((null, category.Title));
        sb.Append(Fragments.Breadcrumb(crumbs));
        sb.Line($"<h1>{Icon(category.Icon, "me-2")}{E(category.Title)}</h1>");
        sb.Line($"<p class=\"lead\">{E(category.Description)}</p>");
        var articles = c.ArticlesOf(category.Id);
        if (articles.Count == 0)
        {
            sb.Line("<div class=\"alert alert-secondary\">このカテゴリの記事は準備中です。</div>");
        }
        else
        {
            sb.Line("<div class=\"row g-4\">");
            foreach (var a in articles)
            {
                sb.Line("<div class=\"col-md-6 col-xl-4\">");
                sb.Line($"<a href=\"{E(a.Url)}\" class=\"article-card card h-100 text-decoration-none\" data-article=\"{E(a.Id)}\">");
                sb.Line("<div class=\"card-body\">");
                sb.Line("<div class=\"d-flex justify-content-between align-items-start gap-2 mb-2\">");
                sb.Line($"<h5 class=\"card-title mb-0\"><span class=\"badge bg-primary me-2\">{a.Order}</span>{E(a.Title)}</h5>");
                sb.Line($"<div class=\"d-flex flex-column align-items-end gap-1\">{Fragments.DifficultyBadge(a.Difficulty)}</div>");
                sb.Line("</div>");
                sb.Line($"<p class=\"card-text text-body-secondary mb-0\">{E(a.Description)}</p>");
                sb.Line("</div>");
                sb.Line("</a>");
                sb.Line("</div>");
            }
            sb.Line("</div>");
        }
        return sb.ToString();
    }

    public string Article(ArticleDto article)
    {
        var category = c.Categories.First(x => x.Id == article.Category);
        var sb = new StringBuilder();
        var crumbs = new List<(string?, string)> { ("/learn", "目次") };
        if (c.SectionOf(category) is { } section) crumbs.Add(($"/learn#section-{section.Id}", section.Title));
        crumbs.Add(($"/learn/{category.Id}", category.Title));
        crumbs.Add((null, article.Title));
        sb.Append(Fragments.Breadcrumb(crumbs));

        sb.Line("<header class=\"article-header mb-4\">");
        sb.Line($"<h1>{E(article.Title)}</h1>");
        sb.Line("<div class=\"d-flex flex-wrap align-items-center gap-2 text-body-secondary small\">");
        sb.Line(Fragments.DifficultyBadge(article.Difficulty));
        // 対象のソフトとバージョン。Unity / VRChat の区切りの記事は Unity のバージョン
        if (category.Section is "unity" or "vrchat") sb.Line($"<span>{Icon("GridView")} Unity {E(s.UnityVersion)}</span>");
        else sb.Line($"<span>{Icon("ViewInAr")} Blender {E(article.BlenderVersion ?? s.BlenderVersion)}</span>");
        if (article.Updated is { } updated) sb.Line($"<span>{Icon("History")} 更新: {E(updated)}</span>");
        sb.Line("</div>");
        if (!string.IsNullOrEmpty(article.Description)) sb.Line($"<p class=\"lead mt-2 mb-0\">{E(article.Description)}</p>");
        sb.Line("</header>");

        var headings = Fragments.ExtractHeadings(article.Html);
        if (headings.Count > 0)
        {
            sb.Line("<details class=\"article-toc-mobile d-lg-none mb-4\">");
            sb.Line($"<summary>この記事の目次（{headings.Count}）</summary>");
            sb.Line("<ol class=\"toc-list\">");
            foreach (var (id, text) in headings) sb.Line($"<li><a href=\"{E(article.Url)}#{E(id)}\">{E(text)}</a></li>");
            sb.Line("</ol>");
            sb.Line("</details>");
        }

        var hasAside = headings.Count > 0 || article.Shortcuts.Count > 0;
        sb.Line("<div class=\"row g-4\">");
        sb.Line($"<div class=\"{(hasAside ? "col-lg-9" : "col-12")}\">");
        sb.Line("<div class=\"article-body\">");
        sb.Line(article.Html.TrimEnd('\n'));
        sb.Line("</div>");
        sb.Line("</div>");
        if (hasAside)
        {
            sb.Line("<aside class=\"col-lg-3\" aria-label=\"この記事の補足\">");
            sb.Line("<div class=\"article-aside\">");
            if (headings.Count > 0)
            {
                sb.Line("<nav class=\"article-toc d-none d-lg-block\" aria-labelledby=\"article-toc-title\">");
                sb.Line("<div class=\"article-toc-title\" id=\"article-toc-title\">この記事の目次</div>");
                sb.Line("<ol class=\"toc-list\">");
                foreach (var (id, text) in headings) sb.Line($"<li><a href=\"{E(article.Url)}#{E(id)}\">{E(text)}</a></li>");
                sb.Line("</ol>");
                sb.Line("</nav>");
            }
            if (article.Shortcuts.Count > 0)
            {
                sb.Line("<div class=\"card article-keys\">");
                sb.Line($"<div class=\"card-header\">{Icon("Keyboard", "me-1")}この記事で使うキー</div>");
                sb.Line("<ul class=\"list-group list-group-flush\">");
                foreach (var key in article.Shortcuts)
                {
                    var shortcut = c.Shortcuts.FirstOrDefault(x => x.Key == key);
                    sb.Append($"<li class=\"list-group-item d-flex justify-content-between align-items-center gap-2\"><kbd>{E(key)}</kbd>");
                    if (shortcut is not null) sb.Append($"<a href=\"/shortcuts?category={Uri.EscapeDataString(shortcut.Category)}\" class=\"small text-decoration-none\">{E(shortcut.Function)}</a>");
                    sb.Line("</li>");
                }
                sb.Line("</ul>");
                sb.Line("</div>");
            }
            sb.Line("</div>");
            sb.Line("</aside>");
        }
        sb.Line("</div>");

        var index = c.Articles.ToList().FindIndex(a => a.Url == article.Url);
        var prev = index > 0 ? c.Articles[index - 1] : null;
        var next = index >= 0 && index < c.Articles.Count - 1 ? c.Articles[index + 1] : null;
        sb.Line("<nav class=\"article-nav d-flex justify-content-between gap-3 mt-4 pt-4 border-top\" aria-label=\"前後の記事\">");
        sb.Line(prev is null ? "<span></span>" : $"<a href=\"{E(prev.Url)}\" class=\"btn btn-outline-secondary text-start\"><small class=\"d-block text-body-secondary\">← 前へ</small>{E(prev.Title)}</a>");
        if (next is not null) sb.Line($"<a href=\"{E(next.Url)}\" class=\"btn btn-primary text-end\"><small class=\"d-block\">次へ →</small>{E(next.Title)}</a>");
        sb.Line("</nav>");
        return sb.ToString();
    }

    // ------------------------------------------------------------ リファレンス

    public string Shortcuts()
    {
        var categories = c.Shortcuts.Select(x => x.Category).Distinct().ToList();
        var sb = new StringBuilder();
        sb.Append(Heading("ショートカット一覧", "Blender でよく使うキーボードショートカットを機能別に整理しました。"));
        sb.Line("<div id=\"shortcut-filters\" class=\"mb-3 d-flex flex-wrap gap-2\">");
        sb.Line("<a href=\"/shortcuts\" class=\"btn btn-sm btn-primary\" data-category=\"\">すべて</a>");
        foreach (var cat in categories) sb.Line($"<a href=\"/shortcuts?category={Uri.EscapeDataString(cat)}\" class=\"btn btn-sm btn-outline-primary\" data-category=\"{E(cat)}\">{E(cat)}</a>");
        sb.Line("</div>");
        sb.Line("<div class=\"table-responsive\">");
        sb.Line("<table class=\"table table-striped table-hover align-middle\">");
        sb.Line("<thead class=\"table-dark\"><tr><th>ショートカット</th><th>機能</th><th>カテゴリ</th><th>説明</th></tr></thead>");
        sb.Line("<tbody id=\"shortcut-rows\">");
        foreach (var x in c.Shortcuts)
        {
            sb.Line($"<tr data-category=\"{E(x.Category)}\"><td><kbd>{E(x.Key)}</kbd></td><td>{E(x.Function)}</td><td><span class=\"badge {ShortcutBadge(x.Category)}\">{E(x.Category)}</span></td><td>{E(x.Description)}</td></tr>");
        }
        sb.Line("</tbody>");
        sb.Line("</table>");
        sb.Line("</div>");
        return sb.ToString();
    }

    public static string ShortcutBadge(string category) => category switch
    {
        "一般" => "bg-primary",
        "モデリング" => "bg-success",
        "編集" => "bg-warning text-dark",
        "表示" => "bg-info text-dark",
        "選択" => "bg-secondary",
        _ => "bg-dark",
    };

    public string Addons()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("おすすめアドオン", "Blender の機能を拡張する便利なアドオンを紹介します。Unity のツールや参考になる記事は <a href=\"/links\">リンク集</a> へ。"));
        sb.Line("<div class=\"row g-4\">");
        foreach (var a in c.Addons)
        {
            sb.Line("<div class=\"col-lg-6\">");
            sb.Line("<div class=\"card h-100\">");
            sb.Line("<div class=\"card-body\">");
            sb.Line("<div class=\"d-flex justify-content-between align-items-start gap-2 mb-2\">");
            sb.Line($"<h5 class=\"card-title mb-0\">{E(a.Name)}</h5>");
            sb.Line($"<span class=\"badge {AddonBadge(a.Type)}\">{E(a.Type)}</span>");
            sb.Line("</div>");
            if (!string.IsNullOrEmpty(a.Status)) sb.Line($"<span class=\"badge bg-warning text-dark mb-2\">{Icon("Warning")} {E(a.Status)}</span>");
            if (a.Source == "discord") sb.Line($"<div class=\"tip-tags\"><span class=\"tip-tag tip-source-discord\" title=\"Discord での質問・会話で紹介されたもの\">{Icon("Forum")} Discord より</span></div>");
            sb.Line($"<p class=\"card-text\">{E(a.Description)}</p>");
            if (!string.IsNullOrEmpty(a.Note)) sb.Line($"<p class=\"card-text small text-body-secondary border-start border-3 border-warning ps-2\">{E(a.Note)}</p>");
            if (a.Features.Count > 0)
            {
                sb.Line("<ul class=\"mb-3\">");
                foreach (var f in a.Features) sb.Line($"<li>{E(f)}</li>");
                sb.Line("</ul>");
            }
            sb.Append($"<div class=\"d-flex justify-content-between align-items-center\"><small class=\"text-body-secondary\">価格: {E(a.Price)}</small>");
            if (!string.IsNullOrEmpty(a.Url)) sb.Append($"<a href=\"{E(a.Url)}\" target=\"_blank\" rel=\"noopener\" class=\"btn btn-primary btn-sm\">公式サイト {Icon("OpenInNew")}</a>");
            sb.Line("</div>");
            sb.Line("</div>");
            sb.Line("</div>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        sb.Line("""
            <div class="alert alert-info mt-4" role="alert">
            <h4 class="alert-heading h5">アドオンのインストール方法（Blender 4.2 以降）</h4>
            <p class="mb-2"><strong>Extensions Platform に掲載されているもの</strong>（Node Wrangler など）</p>
            <ol>
            <li>Edit &gt; Preferences を開く</li>
            <li>Get Extensions タブで名前を検索</li>
            <li>Install をクリック（自動で有効化される）</li>
            </ol>
            <p class="mb-2"><strong>.zip で配布されているもの</strong>（GitHub や Gumroad から入手したアドオン）</p>
            <ol class="mb-2">
            <li>Edit &gt; Preferences を開く</li>
            <li>Add-ons タブ右上の <kbd>∨</kbd> メニューから Install from Disk... を選択</li>
            <li>ダウンロードした .zip ファイルを選択</li>
            <li>一覧でチェックを入れて有効化</li>
            </ol>
            <p class="mb-0 small">Blender 4.1 以前は Add-ons タブの Install... ボタンから .zip を選びます。</p>
            </div>
            """);
        return sb.ToString();
    }

    public static string AddonBadge(string type) => type switch
    {
        "モデリング" => "bg-success",
        "アニメーション" => "bg-primary",
        "レンダリング" => "bg-warning text-dark",
        "ユーティリティ" => "bg-info text-dark",
        "ゲーム開発" => "bg-secondary",
        _ => "bg-dark",
    };

    public string Links()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("リンク集", "アバター改変の相談で紹介されたツール・資料・商品のリンクです。Blender のアドオンは <a href=\"/addons\">アドオン</a> のページにまとめています。"));
        sb.Line("<p class=\"small text-body-secondary\">外部のサイトです。配布・販売の状況や対応バージョンは変わることがあるので、使う前・買う前にリンク先で確かめてください。<span class=\"badge bg-warning text-dark\">古い情報</span> は、今は別の方法が主流になっている・販売や更新が終わっている可能性があるものです。</p>");
        sb.Line("<nav class=\"links-toc mb-4\" aria-label=\"区切り\">");
        foreach (var cat in c.LinkCategories)
            sb.Line($"<a href=\"/links#{E(cat.Id)}\" class=\"btn btn-sm btn-outline-primary\">{Icon(cat.Icon)} {E(cat.Title)} <span class=\"text-body-secondary\">({c.Links.Count(l => l.Category == cat.Id)})</span></a>");
        sb.Line("</nav>");
        foreach (var cat in c.LinkCategories)
        {
            sb.Line($"<section class=\"links-section mb-5\" id=\"{E(cat.Id)}\">");
            sb.Line($"<h2 class=\"h4\">{Icon(cat.Icon, "me-2")}{E(cat.Title)}</h2>");
            sb.Line($"<p class=\"text-body-secondary small\">{E(cat.Description)}</p>");
            sb.Line("<ul class=\"link-list list-unstyled\">");
            foreach (var l in c.Links.Where(l => l.Category == cat.Id))
            {
                sb.Line($"<li class=\"link-item\" id=\"{E(l.Id)}\">");
                sb.Append("<div class=\"d-flex flex-wrap align-items-baseline gap-2\">");
                sb.Append($"<a href=\"{E(l.Url)}\" target=\"_blank\" rel=\"noopener\" class=\"link-name\">{E(l.Name)} {Icon("OpenInNew", "small")}</a>");
                if (l.Status == "old") sb.Append("<span class=\"badge bg-warning text-dark\" title=\"今は別の方法が主流・販売や更新が終わっている可能性があります\">古い情報</span>");
                else if (l.Status == "current") sb.Append("<span class=\"badge bg-success\" title=\"今も使われているもの\">今も使われている</span>");
                if (l.Source == "discord") sb.Append($"<span class=\"tip-tag tip-source-discord\" title=\"Discord での質問・会話で紹介されたもの\">{Icon("Forum")} Discord より</span>");
                sb.Line("</div>");
                sb.Line($"<div class=\"small\">{E(l.Description)}</div>");
                if (!string.IsNullOrEmpty(l.Note)) sb.Line($"<div class=\"small text-body-secondary\">{E(l.Note)}</div>");
                sb.Line("</li>");
            }
            sb.Line("</ul>");
            sb.Line("</section>");
        }
        return sb.ToString();
    }

    public string Changelog()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("更新履歴", "記事の追加・見た目や機能の変更の記録です（新しい順）。"));
        foreach (var e in c.Changelog)
        {
            sb.Line("<section class=\"changelog-entry mb-4\">");
            sb.Line($"<div class=\"changelog-date\">{E(e.Date.Replace('-', '/'))}</div>");
            sb.Line($"<h2 class=\"h5\">{E(e.Title)}</h2>");
            if (e.Items.Count > 0)
            {
                sb.Line("<ul class=\"small\">");
                foreach (var item in e.Items) sb.Line($"<li>{E(item)}</li>");
                sb.Line("</ul>");
            }
            sb.Line("</section>");
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ 用語集・何て名前？・TIPS・Q&A

    public string Glossary()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("用語集", "Blender・Unity・VRChat でよく出てくる言葉を、画面での英語の表記と一緒にまとめました（読みの順）。やりたいことから操作の名前を探すなら <a href=\"/howto\">何て名前？</a>、つまずいたときは <a href=\"/tips\">TIPS</a> や <a href=\"/qa\">改変 Q&amp;A</a> へ。"));
        sb.Append(Fragments.TipsFilter(Fragments.TipTags(c.Glossary.Select(g => g.Tags)), c.Glossary.Any(g => g.Source == "discord"), c.Glossary.Count, "用語・読み・英語名で絞り込む（例: ウェイト、merge）"));
        sb.Line("<div class=\"row g-3\">");
        foreach (var g in c.Glossary)
        {
            sb.Line("<div class=\"col-md-6\" data-tip-item>");
            sb.Line($"<article class=\"tip-card card h-100\" id=\"{E(g.Id)}\" data-tags=\"{E(string.Join('|', g.Tags))}\" data-source=\"{E(g.Source)}\">");
            sb.Line("<div class=\"card-body\">");
            sb.Append($"<header class=\"d-flex flex-wrap align-items-baseline gap-2 mb-1\"><h2 class=\"h5 mb-0\"><a href=\"/glossary#{E(g.Id)}\" class=\"tip-anchor\">{E(g.Term)}</a></h2><span class=\"small text-body-secondary\">{E(g.Reading)}</span>");
            if (!string.IsNullOrEmpty(g.En)) sb.Append($"<span class=\"tip-en\" title=\"英語 UI での表記\">{E(g.En)}</span>");
            sb.Line("</header>");
            sb.Line(Fragments.Tags(g.Tags, g.Source));
            sb.Line($"<p class=\"mb-2\">{g.DescriptionHtml}</p>");
            sb.Line(Fragments.Body(g.BodyHtml));
            sb.Line(Fragments.Refs(c, g.Shortcuts, g.Articles, g.Related, g.Keywords));
            sb.Line("</div>");
            sb.Line("</article>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        return sb.ToString();
    }

    public string Howto()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("あの操作、何て名前？", "やりたいことから、操作の名前・メニューの場所（日本語 / 英語 UI）・キーを引けます。名前がわかれば、Blender では <kbd>F3</kbd>（メニュー検索）にそのまま入力して実行できます。"));
        sb.Append(Fragments.TipsFilter(Fragments.TipTags(c.Howtos.Select(h => h.Tags)), c.Howtos.Any(h => h.Source == "discord"), c.Howtos.Count, "やりたいことで絞り込む（例: くっつけ、穴、揺らす）"));
        sb.Line("<div class=\"row g-3\">");
        foreach (var h in c.Howtos)
        {
            sb.Line("<div class=\"col-lg-6\" data-tip-item>");
            sb.Line($"<article class=\"tip-card card h-100\" id=\"{E(h.Id)}\" data-tags=\"{E(string.Join('|', h.Tags))}\" data-source=\"{E(h.Source)}\" data-aliases=\"{E(string.Join(' ', h.Aliases))}\">");
            sb.Line("<div class=\"card-body\">");
            sb.Line($"<h2 class=\"h5 mb-2\"><a href=\"/howto#{E(h.Id)}\" class=\"tip-anchor\">{E(h.Question)}</a></h2>");
            sb.Append($"<p class=\"howto-answer mb-2\">{Icon("SubdirectoryArrowRight")} <strong>{E(h.Answer)}</strong>");
            if (!string.IsNullOrEmpty(h.AnswerEn)) sb.Append($" <span class=\"tip-en\">{E(h.AnswerEn)}</span>");
            foreach (var key in h.Shortcuts) sb.Append($" <kbd class=\"ms-1\">{E(key)}</kbd>");
            sb.Line("</p>");
            if (!string.IsNullOrEmpty(h.Menu))
            {
                sb.Append($"<dl class=\"howto-menu small mb-2\"><dt>メニュー</dt><dd>{E(h.Menu)}</dd>");
                if (!string.IsNullOrEmpty(h.MenuEn)) sb.Append($"<dt>英語 UI</dt><dd>{E(h.MenuEn)}</dd>");
                sb.Line("</dl>");
            }
            if (!string.IsNullOrEmpty(h.NoteHtml)) sb.Line($"<p class=\"small mb-2\">{h.NoteHtml}</p>");
            sb.Line(Fragments.Body(h.BodyHtml));
            sb.Line(Fragments.Tags(h.Tags, h.Source));
            sb.Line(Fragments.Refs(c, [], h.Articles, h.Related, h.Keywords));
            sb.Line("</div>");
            sb.Line("</article>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        return sb.ToString();
    }

    public string Tips()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("TIPS", "アバターづくりでよくあるつまずきを、症状・原因・直し方でまとめました。言葉の意味は <a href=\"/glossary\">用語集</a>、操作の名前は <a href=\"/howto\">何て名前？</a>、手順や「どうすれば？」は <a href=\"/qa\">改変 Q&amp;A</a> へ。"));
        sb.Append(Fragments.TipsFilter(Fragments.TipTags(c.Tips.Select(t => t.Tags)), c.Tips.Any(t => t.Source == "discord"), c.Tips.Count, "症状で絞り込む（例: ピンク、黒い、ボーン）"));
        sb.Line("<div class=\"row g-3\">");
        foreach (var t in c.Tips)
        {
            sb.Line("<div class=\"col-lg-6\" data-tip-item>");
            sb.Line($"<article class=\"tip-card card h-100\" id=\"{E(t.Id)}\" data-tags=\"{E(string.Join('|', t.Tags))}\" data-source=\"{E(t.Source)}\">");
            sb.Line("<div class=\"card-body\">");
            sb.Line($"<h2 class=\"h5 mb-2\">{Icon("Lightbulb", "me-1")}<a href=\"/tips#{E(t.Id)}\" class=\"tip-anchor\">{E(t.Title)}</a></h2>");
            sb.Line(Fragments.Tags(t.Tags, t.Source));
            if (!string.IsNullOrWhiteSpace(t.SummaryHtml)) sb.Line($"<p class=\"mb-2\">{t.SummaryHtml}</p>");
            if (!string.IsNullOrWhiteSpace(t.SymptomHtml) || !string.IsNullOrWhiteSpace(t.CauseHtml) || !string.IsNullOrWhiteSpace(t.FixHtml))
            {
                sb.Append("<dl class=\"tip-detail mb-2\">");
                if (!string.IsNullOrWhiteSpace(t.SymptomHtml)) sb.Append($"<dt>症状</dt><dd>{t.SymptomHtml}</dd>");
                if (!string.IsNullOrWhiteSpace(t.CauseHtml)) sb.Append($"<dt>原因</dt><dd>{t.CauseHtml}</dd>");
                if (!string.IsNullOrWhiteSpace(t.FixHtml)) sb.Append($"<dt>直し方</dt><dd>{t.FixHtml}</dd>");
                sb.Line("</dl>");
            }
            sb.Line(Fragments.Body(t.BodyHtml));
            sb.Line(Fragments.Refs(c, t.Shortcuts, t.Articles, t.Related, t.Keywords));
            sb.Line("</div>");
            sb.Line("</article>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        return sb.ToString();
    }

    public string Qa()
    {
        var sb = new StringBuilder();
        sb.Append(Heading("改変 Q&A", "アバター改変の相談でよく出た質問と、その答え・手順をまとめました。各項目の「詳しく」を開くと手順や補足が見られます。言葉の意味は <a href=\"/glossary\">用語集</a>、症状から探すなら <a href=\"/tips\">TIPS</a> へ。"));
        sb.Line("<p class=\"small text-body-secondary\">「当時」と書いてあるところは、古いバージョンの Blender・Unity・VRChat SDK での手順です。今の手順と一緒に載せています。</p>");
        sb.Append(Fragments.TipsFilter(Fragments.TipTags(c.Qas.Select(q => q.Tags)), c.Qas.Any(q => q.Source == "discord"), c.Qas.Count, "質問で絞り込む（例: 首、ウェイト、VRM）"));
        sb.Line("<div class=\"row g-3\">");
        foreach (var q in c.Qas)
        {
            sb.Line("<div class=\"col-lg-6\" data-tip-item>");
            sb.Line($"<article class=\"tip-card card h-100\" id=\"{E(q.Id)}\" data-tags=\"{E(string.Join('|', q.Tags))}\" data-source=\"{E(q.Source)}\">");
            sb.Line("<div class=\"card-body\">");
            sb.Line($"<h2 class=\"h5 mb-2\">{Icon("Chat", "me-1")}<a href=\"/qa#{E(q.Id)}\" class=\"tip-anchor\">{E(q.Question)}</a></h2>");
            sb.Line(Fragments.Tags(q.Tags, q.Source));
            sb.Line($"<p class=\"mb-2\">{q.AnswerHtml}</p>");
            sb.Line(Fragments.Body(q.BodyHtml));
            sb.Line(Fragments.Refs(c, q.Shortcuts, q.Articles, q.Related, q.Keywords));
            sb.Line("</div>");
            sb.Line("</article>");
            sb.Line("</div>");
        }
        sb.Line("</div>");
        return sb.ToString();
    }

    // ------------------------------------------------------------ 練習・検索・問い合わせ・プライバシー

    /// <summary>練習 3 ページの静的な本文（説明と使えるキー）。3D ビューそのものは WebAssembly 側（フェーズ 3）で動く。</summary>
    public string Practice() =>
        "<h1>操作の練習</h1>\n" +
        "<p class=\"lead\">ブラウザ上の 3D ビューで、Blender と同じキー操作を試せます。下のお題を順にこなしてください。<kbd>Tab</kbd> で編集モードにも入れます。</p>\n" +
        "<p class=\"text-body-secondary small\">各記事の「やってみよう」では、同じビューで 1 キーずつ案内付きで操作できます（<a href=\"/learn/basics/transform\">移動・回転・スケール</a> など）。ここは案内なしで自由に試す場所です。何人かで同じビューを操作したいときは <a href=\"/practice/room\">みんなで練習</a> へ。</p>\n" +
        "<div class=\"card practice-keys\">\n<div class=\"card-header fw-semibold\">使えるキー</div>\n<div class=\"card-body small\">\n" + PracticeKeys + "</div>\n</div>\n";

    private const string PracticeKeys = """
        <table class="table table-sm mb-0">
        <tbody>
        <tr><td>ホイールボタンをドラッグ</td><td>視点を回す</td></tr>
        <tr><td><kbd>Shift</kbd>+ホイールボタン</td><td>視点を平行移動</td></tr>
        <tr><td>ホイール</td><td>ズーム</td></tr>
        <tr><td><kbd>Numpad 1</kbd> <kbd>3</kbd> <kbd>7</kbd></td><td>前 / 右 / 上から（<kbd>Ctrl</kbd> で反対側）</td></tr>
        <tr><td><kbd>Numpad 5</kbd></td><td>透視 / 平行投影</td></tr>
        <tr><td><kbd>Numpad .</kbd> / <kbd>Home</kbd></td><td>選択を表示 / 全体を表示</td></tr>
        <tr><td>クリック / <kbd>Shift</kbd>+クリック / ドラッグ</td><td>選択 / 追加選択 / ボックス選択</td></tr>
        <tr><td><kbd>A</kbd> / <kbd>Alt</kbd>+<kbd>A</kbd></td><td>全選択 / 選択解除</td></tr>
        <tr><td><kbd>G</kbd> <kbd>R</kbd> <kbd>S</kbd></td><td>移動 / 回転 / スケール（<kbd>X</kbd> <kbd>Y</kbd> <kbd>Z</kbd> で軸固定、数値入力可）</td></tr>
        <tr><td><kbd>Ctrl</kbd> / <kbd>Shift</kbd>（操作中）</td><td>スナップ / 微調整</td></tr>
        <tr><td>左クリック / <kbd>Enter</kbd></td><td>確定</td></tr>
        <tr><td>右クリック / <kbd>Esc</kbd></td><td>キャンセル</td></tr>
        <tr><td><kbd>Shift</kbd>+<kbd>A</kbd></td><td>追加</td></tr>
        <tr><td><kbd>Shift</kbd>+<kbd>D</kbd></td><td>複製</td></tr>
        <tr><td><kbd>X</kbd> / <kbd>Delete</kbd></td><td>削除</td></tr>
        <tr><td><kbd>N</kbd></td><td>サイドバー（位置・回転・スケール）</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>Z</kbd> / <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Z</kbd></td><td>元に戻す / やり直し</td></tr>
        <tr><td><kbd>Alt</kbd>+<kbd>Z</kbd></td><td>透過表示</td></tr>
        <tr class="table-group-divider"><th colspan="2">編集モード</th></tr>
        <tr><td><kbd>Tab</kbd></td><td>編集モードの出入り</td></tr>
        <tr><td><kbd>1</kbd> <kbd>2</kbd> <kbd>3</kbd></td><td>頂点 / 辺 / 面の選択モード</td></tr>
        <tr><td><kbd>Alt</kbd>+クリック / <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+クリック</td><td>ループ / リング選択</td></tr>
        <tr><td><kbd>L</kbd> / <kbd>Ctrl</kbd>+<kbd>L</kbd></td><td>つながった部分を選択</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>I</kbd></td><td>選択を反転</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>Numpad +</kbd> / <kbd>-</kbd></td><td>選択を広げる / 狭める</td></tr>
        <tr><td><kbd>E</kbd> / <kbd>I</kbd></td><td>押し出し / 差し込み</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>R</kbd></td><td>ループカット（ホイールで本数、右クリックで中央）</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>B</kbd></td><td>ベベル（分割数 1 のみ）</td></tr>
        <tr><td><kbd>X</kbd></td><td>削除（頂点 / 辺 / 面 / 辺を溶解）</td></tr>
        <tr><td><kbd>M</kbd> / <kbd>F</kbd> / <kbd>P</kbd></td><td>マージ / 面を張る / 分離</td></tr>
        <tr><td>上のボタン</td><td>透過・統計・ミラー（X 方向）の表示</td></tr>
        </tbody>
        </table>
        """;

    public string PracticeUnity() =>
        Fragments.Breadcrumb([("/practice", "操作の練習"), (null, "Unity の練習")]) +
        "<h1>Unity の練習</h1>\n" +
        "<p class=\"lead\">Unity エディターに似た画面で、アバターに衣装を着せる流れ（Modular Avatar）を試せます。下のお題を順にこなしてください。</p>\n" +
        "<p class=\"text-body-secondary small\">本物の Unity ではなく、ブラウザで動く練習用の画面です。人形は練習用に作った簡単なもので、VRChat SDK の画面や数値も目安のシミュレーションです。手順を 1 つずつ案内付きで試すなら <a href=\"/learn/unity/modular-avatar-outfit\">Modular Avatar で衣装を着せる</a> へ。</p>\n" +
        """
        <div class="card practice-keys">
        <div class="card-header fw-semibold">使える操作</div>
        <div class="card-body small">
        <table class="table table-sm mb-0">
        <tbody>
        <tr><td><kbd>Alt</kbd>+左ドラッグ</td><td>視点を回す</td></tr>
        <tr><td>ホイールボタンでドラッグ</td><td>視点を平行移動</td></tr>
        <tr><td>ホイール</td><td>ズーム</td></tr>
        <tr><td>右ドラッグ</td><td>その場で見回す</td></tr>
        <tr><td><kbd>F</kbd></td><td>選んだものを表示</td></tr>
        <tr><td><kbd>Q</kbd> <kbd>W</kbd> <kbd>E</kbd> <kbd>R</kbd></td><td>手 / 移動 / 回転 / 拡大縮小</td></tr>
        <tr><td><kbd>Ctrl</kbd>+<kbd>Z</kbd></td><td>元に戻す</td></tr>
        <tr><td><kbd>Delete</kbd></td><td>選んだものを削除</td></tr>
        <tr><td>Hierarchy でドラッグ</td><td>親子を付け替える</td></tr>
        <tr><td>Hierarchy で右クリック</td><td>Modular Avatar ▸ Setup Outfit</td></tr>
        <tr><td>Project からドラッグ</td><td>Prefab を置く / マテリアルを設定</td></tr>
        <tr><td>VRChat SDK メニュー</td><td>Builder（ランク・Build &amp; Test）</td></tr>
        </tbody>
        </table>
        </div>
        </div>

        """;

    public string PracticeRoom() =>
        Fragments.Breadcrumb([("/practice", "操作の練習"), (null, "みんなで練習")]) +
        "<h1>みんなで練習</h1>\n" +
        "<p class=\"lead\">URL を共有すると、同じ 3D ビューを何人かで同時に操作できます。先生役が動かして見せたり、手順を一緒に確かめたりするときに。</p>\n" +
        """
        <div class="card">
        <div class="card-header fw-semibold">この部屋について</div>
        <div class="card-body small">
        <ul class="mb-0 ps-3">
        <li>共有されるのは、オブジェクトモードの追加・削除・複製・移動・回転・スケールです</li>
        <li><strong>編集モード（Tab）の変更は自分の画面だけ</strong>で、ほかの人には共有されません</li>
        <li>ほかの人が動かしているオブジェクトは、終わるまで動かせません（その人の色の枠が付きます）</li>
        <li>ほかの人が変更すると、<kbd>Ctrl</kbd>+<kbd>Z</kbd> でそれより前には戻せません</li>
        <li>「視点に合わせる」で、その人と同じ視点で見られます（自分で視点を動かすと解除）</li>
        <li>1 部屋 8 人まで、オブジェクトは 200 個まで。最後の変更から 24 時間で部屋の内容は消えます</li>
        </ul>
        </div>
        </div>
        <p class="small text-body-secondary mt-3">使えるキーは <a href="/practice">操作の練習</a> と同じです。</p>

        """;

    public string Search() =>
        Heading("検索", "記事・用語集・「何て名前？」・TIPS・改変 Q&amp;A・ショートカット・アドオン・リンク集をまとめて検索します。どのページでも <kbd>Ctrl</kbd>+<kbd>K</kbd> か <kbd>/</kbd> で上の検索欄に移動できます。");

    public string Report() =>
        "<div class=\"fsk-page-column\">\n" +
        Heading("問い合わせ", "記事の間違い・不具合・要望・ご意見を、作者に直接送れます。いただいた内容は、作者に通知されます（公開はされません）。") +
        (string.IsNullOrEmpty(s.RepositoryUrl) ? "" : $"<p class=\"small text-body-secondary\">記事の修正は、<a href=\"{E(s.RepositoryUrl)}\" target=\"_blank\" rel=\"noopener\">GitHub</a> で提案していただくこともできます。</p>\n") +
        "<p class=\"small text-body-secondary mt-3 mb-0\">送信フォームは、JavaScript を有効にすると使えます。送信すると、<a href=\"/privacy\">プライバシーポリシー</a>に書いたとおり、内容が作者に通知されます。個人情報（パスワードなど）は書かないでください。</p>\n" +
        "</div>\n";

    public string Privacy()
    {
        var t = E(s.Title);
        return $"""
            <div class="fsk-page-column fsk-page-column--wide">
            <h1>プライバシーポリシー</h1>
            <p class="lead">{t}（このサイト）で、どんな情報を、何のために扱うかをまとめています。<span class="fsk-nowrap">制定: 2026 年 10 月 22 日</span></p>

            <section class="mb-4">
                <h2 class="h5">運営者</h2>
                <p>このサイトは、Fara（個人）が運営しています。ご意見・お問い合わせは、<a href="/report">問い合わせ</a>のページからお願いします。</p>
            </section>

            <section class="mb-4">
                <h2 class="h5">ブラウザに保存する情報（サーバーには送りません）</h2>
                <p>このサイトは、あなたのブラウザの保存領域（localStorage）に、次のものを保存します。サイトの運営者には送られず、ブラウザのデータを消すと消えます。</p>
                <ul>
                    <li>ライト／ダークの設定・サイドバーの状態</li>
                    <li>読んだ記事のチェック（読み進めた状況）</li>
                    <li>「みんなで練習」で入力した表示名</li>
                </ul>
            </section>

            <section class="mb-4">
                <h2 class="h5">「みんなで練習」で扱う情報</h2>
                <p>共同練習の部屋では、部屋に入った人の表示名と、置いたオブジェクトの位置・種類などが、部屋にいる人どうしで共有されます。この内容は、Cloudflare の中の部屋ごとの保存領域に置き、最後の変更から 24 時間で自動的に消します。表示名には、本名など個人を特定できるものを使わないでください。</p>
            </section>

            <section class="mb-4">
                <h2 class="h5">アクセス解析・広告</h2>
                <p>このサイトでは、アクセス解析ツールと広告は使っていません。Cloudflare が、サイトを配信するために通常のアクセスの記録（アクセス元の IP アドレス・ブラウザなど）を取ることがあります。広告（Google AdSense など）を表示することにしたときは、この項目に、使う事業者・Cookie の扱い・配信を止める方法を追記します。</p>
            </section>

            <section class="mb-4">
                <h2 class="h5">問い合わせで送る情報</h2>
                <p>「問い合わせ」で送った内容（種類・内容・任意の連絡先・報告のきっかけのページ・国）は、作者のチャット（Discord）に通知されます。サイトの側では保存しません。返信が必要なときだけ、書いていただいた連絡先を使います。ボットによる送信を防ぐため、Cloudflare Turnstile を使っています（確認のために、あなたのブラウザの情報と IP アドレスが Cloudflare に送られます）。Discord 側には、通知された内容が残ります。報告には、パスワードなどを書かないでください。</p>
            </section>

            <section class="mb-4">
                <h2 class="h5">外部のサービス</h2>
                <ul>
                    <li>Cloudflare（サイトの配信・共同練習の部屋・Turnstile）</li>
                    <li>Google Fonts（文字の表示。サイトを開くと、Google にアクセスの情報が送られます）</li>
                    <li>jsDelivr（練習の 3D 表示の部品。練習のページを開くと、配信元にアクセスの情報が送られます）</li>
                    <li>GitHub（記事の修正の提案の受け付け）</li>
                    <li>Discord（問い合わせの通知）</li>
                </ul>
            </section>

            <section class="mb-4">
                <h2 class="h5">第三者への提供</h2>
                <p>法令に基づく場合を除いて、取得した情報を、本人の同意なく第三者に提供しません。</p>
            </section>

            <section class="mb-4">
                <h2 class="h5">変更について</h2>
                <p>この内容は、必要に応じて見直し、変えたときはこのページに載せます。</p>
            </section>
            </div>
            """;
    }

    /// <summary>404.html の本文（WebAssembly が起動すると NotFoundView に置き換わる）。</summary>
    public static string NotFound() =>
        """
        <section class="fsk-not-found">
        <p class="fsk-not-found-code" aria-hidden="true">404</p>
        <h1>ページが見つかりません</h1>
        <p class="fsk-not-found-lead">URL が間違っているか、ページが移動した可能性があります。</p>
        <a class="btn btn-primary" href="/">ホームへ戻る</a>
        </section>
        """;
}
