using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Net;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FaraBlenderManual.Data;

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
    private readonly ILogger<ContentService> _logger;
    private Snapshot _snapshot;

    public ContentService(IWebHostEnvironment env, ILogger<ContentService> logger)
    {
        _root = Path.Combine(env.ContentRootPath, "Content");
        _logger = logger;
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

    /// <summary>記事・用語集・何て名前？・TIPS・ショートカット・アドオンをまとめた検索インデックス。</summary>
    public IReadOnlyList<SearchEntry> BuildSearchIndex()
    {
        var entries = new List<SearchEntry>();

        foreach (var article in Categories.SelectMany(c => GetArticles(c.Id)))
        {
            var category = GetCategory(article.Category)?.Title ?? article.Category;
            var subtitle = string.IsNullOrEmpty(article.Difficulty) ? category : $"{category} · {article.Difficulty}";
            var text = string.Join(' ', [article.Description, article.PlainText, .. article.Shortcuts]);
            entries.Add(new SearchEntry("article", article.Title, subtitle, article.Url, text));
        }

        foreach (var g in Glossary)
        {
            var subtitle = string.Join(" · ", new[] { g.En, string.Join(" / ", g.Tags) }.Where(x => !string.IsNullOrEmpty(x)));
            entries.Add(new SearchEntry("glossary", g.Term, subtitle, $"/glossary#{g.Id}",
                string.Join(' ', [g.Reading, g.En ?? "", PlainOf(g.Description), .. g.Shortcuts, .. g.Tags])));
        }

        foreach (var h in Howtos)
        {
            var answer = h.Shortcuts.Count > 0 ? $"{h.Answer}（{string.Join(" / ", h.Shortcuts)}）" : h.Answer;
            entries.Add(new SearchEntry("howto", h.Question, answer, $"/howto#{h.Id}",
                string.Join(' ', [h.Answer, h.AnswerEn ?? "", h.Menu ?? "", h.MenuEn ?? "", PlainOf(h.Note), .. h.Aliases, .. h.Shortcuts, .. h.Tags])));
        }

        foreach (var t in Tips)
        {
            entries.Add(new SearchEntry("tip", t.Title, string.Join(" / ", t.Tags), $"/tips#{t.Id}",
                string.Join(' ', [PlainOf(t.Symptom), PlainOf(t.Cause), PlainOf(t.Fix), .. t.Shortcuts, .. t.Tags])));
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
        CheckTips(glossary, howtos, tips, articles, shortcuts);

        _logger.LogInformation("Content loaded: {Categories} categories, {Articles} articles, {Shortcuts} shortcuts, {Addons} addons",
            categories.Count, articles.Count, shortcuts.Count, addons.Count);

        return new Snapshot(sections, categories, articles, shortcuts, addons, glossary, howtos, tips);
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
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage && link.Url is { } url && IsRelative(url))
            {
                link.Url = $"/learn/{categoryId}/{url}";
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

        var html = document.ToHtml(Pipeline);

        return new Article
        {
            Category = categoryId,
            Slug = match.Groups["slug"].Value,
            Order = int.Parse(match.Groups["order"].Value),
            Title = front.Title ?? name,
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
    /// （&lt;div class="practice-embed" data-scenario="transform"&gt;）にする。中身は wwwroot/js/tutorial.js が作る。
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

    /// <summary>TIPS・用語集の ID の重複や、存在しない記事・用語・ショートカットへの参照を警告する</summary>
    private void CheckTips(List<GlossaryEntry> glossary, List<HowtoEntry> howtos, List<TipEntry> tips, List<Article> articles, List<Shortcut> shortcuts)
    {
        var articleIds = articles.Select(a => a.Id).ToHashSet();
        var termIds = glossary.Select(g => g.Id).ToHashSet();
        var keys = shortcuts.Select(s => s.Key).ToHashSet();
        void Check(string kind, string id, IEnumerable<string> arts, IEnumerable<string> related, IEnumerable<string> keysUsed)
        {
            if (!ScenarioNameRegex().IsMatch(id)) _logger.LogWarning("{Kind} {Id}: id must be lowercase letters, digits and hyphens", kind, id);
            foreach (var a in arts.Where(a => !articleIds.Contains(a))) _logger.LogWarning("{Kind} {Id}: unknown article {Article}", kind, id, a);
            foreach (var r in related.Where(r => !termIds.Contains(r))) _logger.LogWarning("{Kind} {Id}: unknown glossary term {Term}", kind, id, r);
            foreach (var k in keysUsed.Where(k => !keys.Contains(k))) _logger.LogDebug("{Kind} {Id}: shortcut {Key} is not in shortcuts.yml (shown without link)", kind, id, k);
        }
        foreach (var g in glossary) Check("glossary", g.Id, g.Articles, g.Related, g.Shortcuts);
        foreach (var h in howtos) Check("howto", h.Id, h.Articles, [], h.Shortcuts);
        foreach (var t in tips) Check("tip", t.Id, t.Articles, [], t.Shortcuts);
        // アンカーはページごと（/glossary、/howto、/tips）なので、重複はそれぞれの中で確かめる
        foreach (var (kind, ids) in new[] { ("glossary", glossary.Select(g => g.Id)), ("howto", howtos.Select(h => h.Id)), ("tip", tips.Select(t => t.Id)) })
        {
            foreach (var dup in ids.GroupBy(x => x).Where(g => g.Count() > 1))
            {
                _logger.LogWarning("{Kind} id {Id} is used more than once", kind, dup.Key);
            }
        }
    }

    private sealed record Snapshot(
        IReadOnlyList<LearnSection> Sections,
        IReadOnlyList<LearnCategory> Categories,
        IReadOnlyList<Article> Articles,
        IReadOnlyList<Shortcut> Shortcuts,
        IReadOnlyList<Addon> Addons,
        IReadOnlyList<GlossaryEntry> Glossary,
        IReadOnlyList<HowtoEntry> Howtos,
        IReadOnlyList<TipEntry> Tips);
}
