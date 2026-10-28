using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FaraAvatarModdingGuide.Data;

/// <summary>
/// Content/ ディレクトリの Markdown / YAML を読み込んで提供する。
/// 起動時に一度だけ読み込む（開発環境ではリクエストごとに Reload される）。
/// </summary>
public sealed partial class ContentService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub) // 日本語見出しにもアンカー ID を付ける
        .UseAdvancedExtensions() // テーブル、::: tip などのカスタムコンテナなど
        .Build();

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly string _root;
    private readonly ILogger _logger;
    private Snapshot _snapshot;

    /// <summary>contentDirectory はリポジトリの Content/ ディレクトリ（learn/・shortcuts.yml などが入っている場所）。ASP.NET には依存しない。</summary>
    public ContentService(string contentDirectory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDirectory);
        _root = contentDirectory;
        _logger = logger ?? NullLogger.Instance;
        _snapshot = Load();
    }

    public IReadOnlyList<LearnSection> Sections => _snapshot.Sections;
    /// <summary>目次のカテゴリ。区切り（Sections）の順、同じ区切りの中は categories.yml の順</summary>
    public IReadOnlyList<LearnCategory> Categories => _snapshot.Categories;
    public IReadOnlyList<Article> Articles => _snapshot.Articles;
    public IReadOnlyList<Shortcut> Shortcuts => _snapshot.Shortcuts;
    public IReadOnlyList<Addon> Addons => _snapshot.Addons;
    /// <summary>用語集（読みの順）</summary>
    public IReadOnlyList<GlossaryEntry> Glossary => _snapshot.Glossary;
    public IReadOnlyList<HowtoEntry> Howtos => _snapshot.Howtos;
    public IReadOnlyList<TipEntry> Tips => _snapshot.Tips;
    public IReadOnlyList<QaEntry> Qas => _snapshot.Qas;
    public IReadOnlyList<LinkEntry> Links => _snapshot.Links;
    /// <summary>更新履歴（新しい順）。Content/changelog.yml</summary>
    public IReadOnlyList<ChangelogEntry> Changelog => _snapshot.Changelog;

    /// <summary>changelog.yml の中身を、新しい順の一覧にする。日付が無い・見出しが空・細かい内容に空の項目があれば FormatException。</summary>
    public static IReadOnlyList<ChangelogEntry> ParseChangelog(string yaml)
    {
        List<ChangelogEntry> list;
        try
        {
            list = Yaml.Deserialize<List<ChangelogEntry>>(yaml) ?? [];
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new FormatException("changelog.yml を読めません: " + ex.Message, ex);
        }
        if (list.Any(e => e is null)) throw new FormatException("changelog.yml に空の項目があります");
        foreach (var e in list)
        {
            e.Items ??= [];   // items: だけ書いて中身が空のとき
            if (e.Date == default) throw new FormatException($"changelog.yml: date がありません（{e.Title}）");
            if (string.IsNullOrWhiteSpace(e.Title)) throw new FormatException($"changelog.yml: {e.Date:yyyy-MM-dd} の title が空です");
            if (e.Items.Any(string.IsNullOrWhiteSpace)) throw new FormatException($"changelog.yml: {e.Date:yyyy-MM-dd} の items に空の項目があります");
        }
        // 同じ日付は書いた順のまま（OrderByDescending は安定ソート）
        return list.OrderByDescending(e => e.Date).ToList();
    }

    /// <summary>リンク集の区切り（この順に表示する）。Content/links.yml の category はここの id を使う</summary>
    public static IReadOnlyList<LinkCategory> LinkCategories { get; } =
    [
        new("unity-tool", "Unity のツール", "bi-tools", "Unity で使うエディタ拡張・アセット。導入は配布ページの案内（VCC / ALCOM、unitypackage など）に従ってください。"),
        new("vrchat", "VRChat", "bi-headset-vr", "PhysBone や SDK の資料、アップロード手順の解説など。"),
        new("reference", "参考になる記事・資料", "bi-journal-bookmark", "Blender・Unity での改変の手順を解説した記事や動画、画像編集ソフトなど。古いバージョンの画面で書かれたものは、メニューの名前や場所が今と違うことがあります。"),
        new("booth-avatar", "BOOTH: アバター", "bi-person-bounding-box", "相談の中で話題になったアバター。販売状況・価格・規約は変わることがあるので、購入前に商品ページで確かめてください。"),
        new("booth-outfit", "BOOTH: 衣装・小物", "bi-handbag", "相談の中で話題になった衣装・アクセサリー。対応アバター・規約（改変や他アバターへの流用の可否）は商品ページで確かめてください。"),
    ];

    /// <summary>Markdown の短い文章（用語集・TIPS の説明など）を HTML にする。1 段落だけなら &lt;p&gt; を外す</summary>
    public static string RenderInline(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToHtml(markdown.Trim(), Pipeline).Trim();
        if (html.StartsWith("<p>") && html.EndsWith("</p>") && html.IndexOf("<p>", 3, StringComparison.Ordinal) < 0)
        {
            html = html[3..^4];
        }
        return html;
    }

    public static string PlainOf(string? markdown) =>
        string.IsNullOrEmpty(markdown) ? string.Empty : ToPlainText(Markdown.ToHtml(markdown, Pipeline));

    /// <summary>TIPS・用語集のタグ（よく使うものから）</summary>
    public IReadOnlyList<string> TipTags(IEnumerable<IEnumerable<string>> tagLists) =>
        tagLists.SelectMany(t => t).GroupBy(t => t).OrderBy(g => TagOrder(g.Key)).ThenByDescending(g => g.Count()).Select(g => g.Key).ToList();

    private static int TagOrder(string tag) => tag switch { "Blender" => 0, "Unity" => 1, "VRChat" => 2, "Modular Avatar" => 3, _ => 10 };

    public void Reload() => _snapshot = Load();

    public IReadOnlyList<LearnCategory> GetCategoriesInSection(string sectionId) =>
        Categories.Where(c => c.Section.Equals(sectionId, StringComparison.OrdinalIgnoreCase)).ToList();

    public LearnSection? GetSection(string id) =>
        Sections.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public LearnCategory? GetCategory(string id) =>
        Categories.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Article> GetArticles(string categoryId) =>
        Articles.Where(a => a.Category.Equals(categoryId, StringComparison.OrdinalIgnoreCase)).ToList();

    public Article? GetArticle(string categoryId, string slug) =>
        GetArticles(categoryId).FirstOrDefault(a => a.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>ロードマップ順での前後の記事。カテゴリ末尾なら次のカテゴリの先頭記事へつなぐ。</summary>
    public (Article? Prev, Article? Next) GetNeighbors(Article article)
    {
        var ordered = Categories
            .SelectMany(c => GetArticles(c.Id))
            .ToList();
        var index = ordered.FindIndex(a => a.Url == article.Url);
        if (index < 0) return (null, null);
        return (
            index > 0 ? ordered[index - 1] : null,
            index < ordered.Count - 1 ? ordered[index + 1] : null);
    }

    public IReadOnlyList<string> ShortcutCategories =>
        Shortcuts.Select(s => s.Category).Distinct().ToList();

    /// <summary>検索の索引に入れる固定ページ（題名・読み・説明）。読みはひらがなだけにする（ローマ字・かなで探すため）</summary>
    private static readonly (string Title, string Reading, string Url, string Description)[] SearchPages =
    [
        ("目次", "もくじ", "/learn", "操作・改変の記事を、区切りごとに上から順に読む"),
        ("操作の練習", "そうさのれんしゅう", "/practice", "ブラウザ上の 3D ビューで Blender と同じキー操作を試す"),
        ("Unity の練習", "ゆにてぃーのれんしゅう", "/practice/unity", "Unity エディターに似た画面で、Modular Avatar で衣装を着せる流れを試す"),
        ("みんなで練習", "みんなでれんしゅう", "/practice/room", "同じ 3D ビューを複数人で操作する部屋"),
        ("用語集", "ようごしゅう", "/glossary", "Blender・Unity・VRChat の用語の意味"),
        ("あの操作、何て名前", "あのそうさなんてなまえ", "/howto", "やりたいことから、操作の名前・メニューの場所・キーを引く"),
        ("TIPS", "てぃっぷす", "/tips", "よくあるつまずきと直し方"),
        ("改変 Q&A", "かいへんきゅーあんどえー", "/qa", "よく出る質問と答え・手順"),
        ("ショートカット一覧", "しょーとかっといちらん", "/shortcuts", "キー操作の一覧（カテゴリで絞り込める）"),
        ("アドオン", "あどおん", "/addons", "おすすめのアドオン"),
        ("リンク集", "りんくしゅう", "/links", "Unity のツール・参考記事・BOOTH の商品など"),
        ("更新履歴", "こうしんりれき", "/changelog", "サイトの更新履歴"),
        ("問い合わせ", "といあわせ", "/report", "不具合の報告・記事の修正提案などの問い合わせ"),
        ("プライバシーポリシー", "ぷらいばしーぽりしー", "/privacy", "個人情報の扱い"),
        ("検索", "けんさく", "/search", "サイト内検索"),
    ];

    /// <summary>記事・カテゴリ・固定ページ・用語集・何て名前？・TIPS・Q&amp;A・リンク集・ショートカット・アドオンをまとめた検索インデックス。</summary>
    public IReadOnlyList<SearchEntry> BuildSearchIndex()
    {
        var entries = new List<SearchEntry>();

        foreach (var article in Categories.SelectMany(c => GetArticles(c.Id)))
        {
            var category = GetCategory(article.Category)?.Title ?? article.Category;
            var subtitle = string.IsNullOrEmpty(article.Difficulty) ? category : $"{category} · {article.Difficulty}";
            var text = string.Join(' ', [article.Description, article.PlainText, .. article.Shortcuts]);
            entries.Add(new SearchEntry("article", article.Title, subtitle, article.Url, text, article.Reading));
        }

        // 目次のカテゴリと、固定ページ（用語集・TIPS など）。題名の読みで探せるように、索引に入れる
        foreach (var c in Categories)
        {
            entries.Add(new SearchEntry("category", c.Title, c.En ?? string.Empty, $"/learn/{c.Id}",
                string.Join(' ', [c.Description, c.En ?? string.Empty]), c.Reading));
        }

        foreach (var p in SearchPages)
        {
            entries.Add(new SearchEntry("page", p.Title, string.Empty, p.Url, p.Description, p.Reading));
        }

        foreach (var g in Glossary)
        {
            var subtitle = string.Join(" · ", new[] { g.En, string.Join(" / ", g.Tags) }.Where(x => !string.IsNullOrEmpty(x)));
            entries.Add(new SearchEntry("glossary", g.Term, subtitle, $"/glossary#{g.Id}",
                string.Join(' ', [g.Reading, g.En ?? "", PlainOf(g.Description), PlainOf(g.Body), .. g.Keywords, .. g.Shortcuts, .. g.Tags]), g.Reading));
        }

        foreach (var h in Howtos)
        {
            var answer = h.Shortcuts.Count > 0 ? $"{h.Answer}（{string.Join(" / ", h.Shortcuts)}）" : h.Answer;
            entries.Add(new SearchEntry("howto", h.Question, answer, $"/howto#{h.Id}",
                string.Join(' ', [h.Answer, h.AnswerEn ?? "", h.Menu ?? "", h.MenuEn ?? "", PlainOf(h.Note), PlainOf(h.Body), .. h.Aliases, .. h.Keywords, .. h.Shortcuts, .. h.Tags]), h.Reading));
        }

        foreach (var t in Tips)
        {
            entries.Add(new SearchEntry("tip", t.Title, string.Join(" / ", t.Tags), $"/tips#{t.Id}",
                string.Join(' ', [PlainOf(t.Summary), PlainOf(t.Symptom), PlainOf(t.Cause), PlainOf(t.Fix), PlainOf(t.Body), .. t.Keywords, .. t.Shortcuts, .. t.Tags]), t.Reading));
        }

        foreach (var q in Qas)
        {
            entries.Add(new SearchEntry("qa", q.Question, string.Join(" / ", q.Tags), $"/qa#{q.Id}",
                string.Join(' ', [PlainOf(q.Answer), PlainOf(q.Body), .. q.Keywords, .. q.Shortcuts, .. q.Tags]), q.Reading));
        }

        foreach (var l in Links)
        {
            var category = LinkCategories.FirstOrDefault(c => c.Id == l.Category)?.Title ?? l.Category;
            entries.Add(new SearchEntry("link", l.Name, category, $"/links#{l.Id}",
                string.Join(' ', [l.Description, l.Note ?? ""]), l.Reading));
        }

        foreach (var shortcut in Shortcuts)
        {
            entries.Add(new SearchEntry(
                "shortcut",
                shortcut.Key,
                shortcut.Function,
                $"/shortcuts?category={Uri.EscapeDataString(shortcut.Category)}",
                $"{shortcut.Description} {shortcut.Category}"));
        }

        foreach (var addon in Addons)
        {
            var text = string.Join(' ', [addon.Description, addon.Note ?? string.Empty, .. addon.Features]);
            entries.Add(new SearchEntry("addon", addon.Name, addon.Type, "/addons", text));
        }

        return entries;
    }

    private Snapshot Load()
    {
        var sections = ReadYaml<List<LearnSection>>(Path.Combine(_root, "learn", "sections.yml")) ?? [];
        var categories = ReadYaml<List<LearnCategory>>(Path.Combine(_root, "learn", "categories.yml")) ?? [];
        foreach (var c in categories.Where(c => !sections.Any(s => s.Id == c.Section)))
        {
            _logger.LogWarning("Category {Category} has unknown section {Section}", c.Id, c.Section);
        }
        // 区切りの順に並べ替える（同じ区切りの中は元の順。OrderBy は安定ソート）
        categories = categories
            .OrderBy(c => sections.FindIndex(s => s.Id == c.Section) is var i && i >= 0 ? i : int.MaxValue)
            .ToList();
        var articles = categories
            .SelectMany(c => LoadArticles(c.Id))
            .ToList();
        var shortcuts = ReadYaml<List<Shortcut>>(Path.Combine(_root, "shortcuts.yml")) ?? [];
        var addons = ReadYaml<List<Addon>>(Path.Combine(_root, "addons.yml")) ?? [];
        var ja = System.Globalization.CultureInfo.GetCultureInfo("ja-JP");
        var glossary = (ReadYaml<List<GlossaryEntry>>(Path.Combine(_root, "tips", "glossary.yml")) ?? [])
            .OrderBy(g => g.Reading, StringComparer.Create(ja, System.Globalization.CompareOptions.IgnoreKanaType | System.Globalization.CompareOptions.IgnoreWidth))
            .ToList();
        var howtos = ReadYaml<List<HowtoEntry>>(Path.Combine(_root, "tips", "howto.yml")) ?? [];
        var tips = ReadYaml<List<TipEntry>>(Path.Combine(_root, "tips", "tips.yml")) ?? [];
        var qas = ReadYaml<List<QaEntry>>(Path.Combine(_root, "tips", "qa.yml")) ?? [];
        var links = ReadYaml<List<LinkEntry>>(Path.Combine(_root, "links.yml")) ?? [];
        var changelogPath = Path.Combine(_root, "changelog.yml");
        var changelog = File.Exists(changelogPath) ? ParseChangelog(File.ReadAllText(changelogPath)) : [];
        CheckTips(glossary, howtos, tips, qas, links, articles, shortcuts);

        _logger.LogInformation("Content loaded: {Categories} categories, {Articles} articles, {Shortcuts} shortcuts, {Addons} addons",
            categories.Count, articles.Count, shortcuts.Count, addons.Count);

        return new Snapshot(sections, categories, articles, shortcuts, addons, glossary, howtos, tips, qas, links, changelog);
    }

    private IEnumerable<Article> LoadArticles(string categoryId)
    {
        var dir = Path.Combine(_root, "learn", categoryId);
        if (!Directory.Exists(dir)) yield break;

        // ファイル名の文字列順だと 10- が 9- より前になるので、先頭の番号の数値で並べる
        foreach (var article in Directory.EnumerateFiles(dir, "*.md")
                     .Select(file => ParseArticle(categoryId, file))
                     .OfType<Article>()
                     .OrderBy(a => a.Order)
                     .ThenBy(a => a.Slug, StringComparer.Ordinal))
        {
            yield return article;
        }
    }

    private Article? ParseArticle(string categoryId, string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var match = FileNameRegex().Match(name);
        if (!match.Success)
        {
            _logger.LogWarning("Skipping {File}: file name must look like 01-slug.md", path);
            return null;
        }

        var markdown = File.ReadAllText(path);
        var document = Markdown.Parse(markdown, Pipeline);

        // 記事内の相対パス画像（例: images/move.gif）を /learn/<category>/images/move.gif に解決する。
        // 画像は Content/learn/<category>/images/ に置き、Program.cs で /learn 以下として配信している。
        // 画像の寸法（width・height）は、画像ファイルから読んで <img> に付ける（読み込み前後のレイアウトのずれを防ぐ）。
        var imageSizes = new Dictionary<string, (int Width, int Height)>();
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage && link.Url is { } url && IsRelative(url))
            {
                var size = ReadPngSize(Path.Combine(_root, "learn", categoryId, url));
                link.Url = $"/learn/{categoryId}/{url}";
                if (size is { } s) imageSizes[link.Url] = s;
            }
        }

        var scenarios = ConvertPracticeContainers(document, path);

        var front = new ArticleFrontMatter();
        if (document.Descendants<YamlFrontMatterBlock>().FirstOrDefault() is { } yamlBlock)
        {
            // Span には区切りの "---" 行も含まれるので取り除く
            var yaml = markdown.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length)
                .Trim().Trim('-').Trim();
            front = Yaml.Deserialize<ArticleFrontMatter>(yaml) ?? front;
        }

        var html = WithImageSizes(document.ToHtml(Pipeline), imageSizes);

        return new Article
        {
            Category = categoryId,
            Slug = match.Groups["slug"].Value,
            Order = int.TryParse(match.Groups["order"].Value, out var order)
                ? order
                : throw new InvalidDataException($"{name}: 順番の番号が大きすぎるか、数ではありません（{match.Groups["order"].Value}）"),
            Title = front.Title ?? name,
            Reading = front.Reading ?? string.Empty,
            Description = front.Description ?? string.Empty,
            Difficulty = front.Difficulty ?? string.Empty,
            BlenderVersion = front.BlenderVersion,
            Updated = front.Updated,
            Shortcuts = front.Shortcuts,
            Html = html,
            PlainText = ToPlainText(html),
            PracticeScenarios = scenarios,
        };
    }

    /// <summary>
    /// 「::: practice transform」のコンテナを、操作のチュートリアルの埋め込み
    /// （&lt;div class="practice-embed" data-scenario="transform"&gt;）にする。中身は Web の wwwroot/js/tutorial.js が作る。
    /// コンテナの中に書いた文章は、チュートリアルの上に説明として表示される。
    /// </summary>
    private List<string> ConvertPracticeContainers(MarkdownDocument document, string path)
    {
        var names = new List<string>();
        foreach (var container in document.Descendants<CustomContainer>())
        {
            if (container.Info != "practice") continue;
            var name = container.Arguments?.Trim() ?? string.Empty;
            if (!ScenarioNameRegex().IsMatch(name))
            {
                _logger.LogWarning("{File}: \"::: practice\" needs a scenario name like \"::: practice transform\"", path);
                continue;
            }
            var attributes = container.GetAttributes();
            attributes.Classes?.Clear();
            attributes.AddClass("practice-embed");
            attributes.AddProperty("data-scenario", name);
            names.Add(name);
        }
        return names;
    }

    private static bool IsRelative(string url) =>
        !url.StartsWith('/') && !url.StartsWith('#') && !url.Contains("://") && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PNG の寸法（IHDR の幅・高さ）を読む。PNG でない・読めないときは null（寸法を付けないだけで、表示はそのまま）。
    /// 記事の画像は PNG だけ（Content/learn/**/images）。
    /// </summary>
    private static (int Width, int Height)? ReadPngSize(string path)
    {
        Span<byte> head = stackalloc byte[24];
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Read(head) < head.Length) return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (!head[..8].SequenceEqual(signature) || !head.Slice(12, 4).SequenceEqual("IHDR"u8)) return null;
        var width = BinaryPrimitives.ReadInt32BigEndian(head[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(head[20..24]);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    /// <summary>Markdig が書いた &lt;img src="…" &gt; に、寸法（width・height 属性）を付ける。対応する画像が無ければ何もしない。</summary>
    private static string WithImageSizes(string html, IReadOnlyDictionary<string, (int Width, int Height)> sizes)
    {
        foreach (var (src, size) in sizes)
        {
            html = html.Replace($"<img src=\"{src}\" ", $"<img src=\"{src}\" width=\"{size.Width}\" height=\"{size.Height}\" ", StringComparison.Ordinal);
        }
        return html;
    }

    private static string ToPlainText(string html)
    {
        var text = HtmlTagRegex().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    private T? ReadYaml<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Content file not found: {File}", path);
            return null;
        }
        return Yaml.Deserialize<T>(File.ReadAllText(path));
    }

    [GeneratedRegex(@"^(?<order>\d+)-(?<slug>[a-z0-9]+(?:-[a-z0-9]+)*)$")]
    private static partial Regex FileNameRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex ScenarioNameRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>TIPS・用語集・Q&amp;A・リンク集の ID の重複や、存在しない記事・用語・項目への参照を警告する</summary>
    private void CheckTips(List<GlossaryEntry> glossary, List<HowtoEntry> howtos, List<TipEntry> tips, List<QaEntry> qas, List<LinkEntry> links, List<Article> articles, List<Shortcut> shortcuts)
    {
        var articleIds = articles.Select(a => a.Id).ToHashSet();
        var termIds = glossary.Select(g => g.Id).ToHashSet();
        var keys = shortcuts.Select(s => s.Key).ToHashSet();
        // 本文中の [文字](/tips#id) のようなリンクの行き先
        var anchors = new Dictionary<string, HashSet<string>>
        {
            ["glossary"] = termIds,
            ["howto"] = howtos.Select(h => h.Id).ToHashSet(),
            ["tips"] = tips.Select(t => t.Id).ToHashSet(),
            ["qa"] = qas.Select(q => q.Id).ToHashSet(),
            ["links"] = [.. links.Select(l => l.Id), .. LinkCategories.Select(c => c.Id)],
        };
        void Check(string kind, string id, IEnumerable<string> arts, IEnumerable<string> related, IEnumerable<string> keysUsed, params string?[] markdown)
        {
            if (!ScenarioNameRegex().IsMatch(id)) _logger.LogWarning("{Kind} {Id}: id must be lowercase letters, digits and hyphens", kind, id);
            foreach (var a in arts.Where(a => !articleIds.Contains(a))) _logger.LogWarning("{Kind} {Id}: unknown article {Article}", kind, id, a);
            foreach (var r in related.Where(r => !termIds.Contains(r))) _logger.LogWarning("{Kind} {Id}: unknown glossary term {Term}", kind, id, r);
            foreach (var k in keysUsed.Where(k => !keys.Contains(k))) _logger.LogDebug("{Kind} {Id}: shortcut {Key} is not in shortcuts.yml (shown without link)", kind, id, k);
            foreach (Match m in markdown.SelectMany(md => InternalLinkRegex().Matches(md ?? string.Empty)))
            {
                var page = m.Groups["page"].Value;
                var target = m.Groups["id"].Value;
                if (page == "learn")
                {
                    if (!articleIds.Contains(target)) _logger.LogWarning("{Kind} {Id}: link to unknown article /learn/{Target}", kind, id, target);
                }
                else if (target.Length > 0 && !anchors[page].Contains(target))
                {
                    _logger.LogWarning("{Kind} {Id}: link to unknown item /{Page}#{Target}", kind, id, page, target);
                }
            }
        }
        foreach (var g in glossary) Check("glossary", g.Id, g.Articles, g.Related, g.Shortcuts, g.Description, g.Body);
        foreach (var h in howtos) Check("howto", h.Id, h.Articles, h.Related, h.Shortcuts, h.Note, h.Body);
        foreach (var t in tips) Check("tip", t.Id, t.Articles, t.Related, t.Shortcuts, t.Summary, t.Symptom, t.Cause, t.Fix, t.Body);
        foreach (var q in qas) Check("qa", q.Id, q.Articles, q.Related, q.Shortcuts, q.Answer, q.Body);
        foreach (var l in links)
        {
            if (!ScenarioNameRegex().IsMatch(l.Id)) _logger.LogWarning("link {Id}: id must be lowercase letters, digits and hyphens", l.Id);
            if (!LinkCategories.Any(c => c.Id == l.Category)) _logger.LogWarning("link {Id}: unknown category {Category}", l.Id, l.Category);
            if (!Uri.TryCreate(l.Url, UriKind.Absolute, out _)) _logger.LogWarning("link {Id}: url is not absolute", l.Id);
        }
        // アンカーはページごと（/glossary、/howto、/tips、/qa、/links）なので、重複はそれぞれの中で確かめる
        foreach (var (kind, ids) in new[] { ("glossary", glossary.Select(g => g.Id)), ("howto", howtos.Select(h => h.Id)), ("tip", tips.Select(t => t.Id)), ("qa", qas.Select(q => q.Id)), ("link", links.Select(l => l.Id)) })
        {
            foreach (var dup in ids.GroupBy(x => x).Where(g => g.Count() > 1))
            {
                _logger.LogWarning("{Kind} id {Id} is used more than once", kind, dup.Key);
            }
        }
    }

    /// <summary>本文中のサイト内リンク: (/tips#id) (/glossary#id) (/howto#id) (/qa#id) (/links#id) (/learn/category/slug)</summary>
    [GeneratedRegex(@"\]\(/(?:(?<page>glossary|howto|tips|qa|links)(?:#(?<id>[^)\s]*))?|(?<page>learn)/(?<id>[a-z0-9-]+/[a-z0-9-]+))\)")]
    private static partial Regex InternalLinkRegex();

    private sealed record Snapshot(
        IReadOnlyList<LearnSection> Sections,
        IReadOnlyList<LearnCategory> Categories,
        IReadOnlyList<Article> Articles,
        IReadOnlyList<Shortcut> Shortcuts,
        IReadOnlyList<Addon> Addons,
        IReadOnlyList<GlossaryEntry> Glossary,
        IReadOnlyList<HowtoEntry> Howtos,
        IReadOnlyList<TipEntry> Tips,
        IReadOnlyList<QaEntry> Qas,
        IReadOnlyList<LinkEntry> Links,
        IReadOnlyList<ChangelogEntry> Changelog);
}
