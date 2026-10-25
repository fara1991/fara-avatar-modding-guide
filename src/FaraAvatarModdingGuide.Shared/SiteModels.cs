namespace FaraAvatarModdingGuide.Models;

// content.json の形（WebAssembly 側の Web/Models/SiteModels.cs と同じ形にする。JSON は camelCase）。
// Markdown で書かれた短い文章（用語集・TIPS の説明など）は、ここで HTML にしておく（WebAssembly 側に Markdig を持たない。計画書 D2）。
// アイコンは Material のメンバー名（SiteIcons）。

public sealed record SectionDto(string Id, string Title, string Description, string Icon);

public sealed record CategoryDto(string Id, string Section, string Title, string Reading, string? En, string Description, string Icon, string? NavTitle = null);

public sealed record ArticleDto(
    string Category, string Slug, int Order, string Title, string Reading, string Description, string Difficulty,
    string? BlenderVersion, string? Updated, IReadOnlyList<string> Shortcuts, string Html, IReadOnlyList<string> PracticeScenarios,
    string Url, string Id);

public sealed record ShortcutDto(string Key, string Function, string Category, string Description);

public sealed record AddonDto(string Name, string Description, string Type, string Price, IReadOnlyList<string> Features, string Url, string? Status, string? Note, string Source);

public sealed record GlossaryDto(
    string Id, string Term, string Reading, string? En, string DescriptionHtml, string? BodyHtml, IReadOnlyList<string> Tags,
    IReadOnlyList<string> Shortcuts, IReadOnlyList<string> Articles, IReadOnlyList<string> Related, IReadOnlyList<string> Keywords, string Source);

public sealed record HowtoDto(
    string Id, string Question, string Reading, string Answer, string? AnswerEn, string? Menu, string? MenuEn, IReadOnlyList<string> Shortcuts,
    string? NoteHtml, IReadOnlyList<string> Aliases, IReadOnlyList<string> Tags, IReadOnlyList<string> Articles, IReadOnlyList<string> Related,
    IReadOnlyList<string> Keywords, string? BodyHtml, string Source);

public sealed record TipDto(
    string Id, string Title, string Reading, string? SummaryHtml, string? SymptomHtml, string? CauseHtml, string? FixHtml, IReadOnlyList<string> Tags,
    IReadOnlyList<string> Shortcuts, IReadOnlyList<string> Articles, IReadOnlyList<string> Related, IReadOnlyList<string> Keywords, string? BodyHtml, string Source);

public sealed record QaDto(
    string Id, string Question, string Reading, string AnswerHtml, string? BodyHtml, IReadOnlyList<string> Tags, IReadOnlyList<string> Shortcuts,
    IReadOnlyList<string> Articles, IReadOnlyList<string> Related, IReadOnlyList<string> Keywords, string Source);

public sealed record LinkCategoryDto(string Id, string Title, string Icon, string Description);

public sealed record LinkDto(string Id, string Name, string Reading, string Url, string Category, string Description, string? Note, string? Status, string Source);

public sealed record ChangelogDto(string Date, string Title, IReadOnlyList<string> Items);

/// <summary>content.json の中身全体。静的 HTML の本文づくりにも使う。</summary>
public sealed record SiteContent(
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ArticleDto> Articles,
    IReadOnlyList<ShortcutDto> Shortcuts,
    IReadOnlyList<AddonDto> Addons,
    IReadOnlyList<GlossaryDto> Glossary,
    IReadOnlyList<HowtoDto> Howtos,
    IReadOnlyList<TipDto> Tips,
    IReadOnlyList<QaDto> Qas,
    IReadOnlyList<LinkCategoryDto> LinkCategories,
    IReadOnlyList<LinkDto> Links,
    IReadOnlyList<ChangelogDto> Changelog)
{
    public SectionDto? SectionOf(CategoryDto category) => Sections.FirstOrDefault(s => s.Id == category.Section);
    public IReadOnlyList<CategoryDto> CategoriesIn(string sectionId) => Categories.Where(c => c.Section == sectionId).ToList();
    public IReadOnlyList<ArticleDto> ArticlesOf(string categoryId) => Articles.Where(a => a.Category == categoryId).ToList();
}

/// <summary>data/site.json の中身（サイト名・バージョン・フッターの SNS など）。DevelopHost は確認用の公開先のホスト名（フッターの作者リンクを確認用に切り替える基準）。</summary>
public sealed record SiteSettings(
    string Title, string BlenderVersion, string UnityVersion, string RepositoryUrl, int PublishedYear, string PortfolioUrl,
    string DevelopHost, IReadOnlyList<SocialDto> Socials);

public sealed record SocialDto(string Service, string Label, string Url);

/// <summary>
/// data/search.json の 1 件。検索の索引は、ビルド時に正規化した文字列（*Norm）を持つ（計画書 4.4）。
/// ブラウザの WebAssembly では string.Normalize が使えないため。正規化の規則は tests/js/fixtures/searchcore.reference.js の norm() と同じ。
/// </summary>
public sealed record SearchRecord(
    string Type, string Title, string? Subtitle, string Url, string Text, string? Reading,
    string TitleNorm, string SubtitleNorm, string TextNorm, string ReadingNorm);

/// <summary>/search-index.json（現行と同じ URL・同じ形）の 1 件。</summary>
public sealed record LegacySearchEntry(string Type, string Title, string? Subtitle, string Url, string Text, string? Reading);
