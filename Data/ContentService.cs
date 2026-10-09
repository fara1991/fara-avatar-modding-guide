using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
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

        foreach (var file in Directory.EnumerateFiles(dir, "*.md").Order(StringComparer.Ordinal))
        {
            var article = ParseArticle(categoryId, file);
            if (article is not null) yield return article;
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

        var front = new ArticleFrontMatter();
        if (document.Descendants<YamlFrontMatterBlock>().FirstOrDefault() is { } yamlBlock)
        {
            // Span には区切りの "---" 行も含まれるので取り除く
            var yaml = markdown.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length)
                .Trim().Trim('-').Trim();
            front = Yaml.Deserialize<ArticleFrontMatter>(yaml) ?? front;
        }

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
            Html = document.ToHtml(Pipeline),
        };
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

    private sealed record Snapshot(
        IReadOnlyList<LearnCategory> Categories,
        IReadOnlyList<Article> Articles,
        IReadOnlyList<Shortcut> Shortcuts,
        IReadOnlyList<Addon> Addons);
}
