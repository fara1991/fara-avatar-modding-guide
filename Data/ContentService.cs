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

    public IReadOnlyList<LearnCategory> Categories => _snapshot.Categories;
    public IReadOnlyList<Article> Articles => _snapshot.Articles;
    public IReadOnlyList<Shortcut> Shortcuts => _snapshot.Shortcuts;
    public IReadOnlyList<Addon> Addons => _snapshot.Addons;

    public void Reload() => _snapshot = Load();

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

    /// <summary>記事・ショートカット・アドオンをまとめた検索インデックス。</summary>
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
        var categories = ReadYaml<List<LearnCategory>>(Path.Combine(_root, "learn", "categories.yml")) ?? [];
        var articles = categories
            .SelectMany(c => LoadArticles(c.Id))
            .ToList();
        var shortcuts = ReadYaml<List<Shortcut>>(Path.Combine(_root, "shortcuts.yml")) ?? [];
        var addons = ReadYaml<List<Addon>>(Path.Combine(_root, "addons.yml")) ?? [];

        _logger.LogInformation("Content loaded: {Categories} categories, {Articles} articles, {Shortcuts} shortcuts, {Addons} addons",
            categories.Count, articles.Count, shortcuts.Count, addons.Count);

        return new Snapshot(categories, articles, shortcuts, addons);
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

    private sealed record Snapshot(
        IReadOnlyList<LearnCategory> Categories,
        IReadOnlyList<Article> Articles,
        IReadOnlyList<Shortcut> Shortcuts,
        IReadOnlyList<Addon> Addons);
}
