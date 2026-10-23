using FaraAvatarModdingGuide.Data;

namespace FaraAvatarModdingGuide.Build;

// content.json の形（WebAssembly 側の Web/Models/SiteModels.cs と同じ形にする。JSON は camelCase）。
// Markdown で書かれた短い文章（用語集・TIPS の説明など）は、ここで HTML にしておく（WebAssembly 側に Markdig を持たない。計画書 D2）。
// アイコンは Material のメンバー名（SiteIcons）。

public sealed record SectionDto(string Id, string Title, string Description, string Icon);

public sealed record CategoryDto(string Id, string Section, string Title, string Reading, string? En, string Description, string Icon);

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
    public static SiteContent From(ContentService content)
    {
        static string? Inline(string? markdown) => string.IsNullOrWhiteSpace(markdown) ? null : ContentService.RenderInline(markdown);

        var linkCategories = ContentService.LinkCategories
            .Where(c => content.Links.Any(l => l.Category == c.Id))
            .Select(c => new LinkCategoryDto(c.Id, c.Title, SiteIcons.ToMaterial(c.Icon), c.Description))
            .ToList();

        return new SiteContent(
            content.Sections.Select(s => new SectionDto(s.Id, s.Title, s.Description, SiteIcons.ToMaterial(s.Icon))).ToList(),
            content.Categories.Select(c => new CategoryDto(c.Id, c.Section, c.Title, c.Reading, c.En, c.Description, SiteIcons.ToMaterial(c.Icon))).ToList(),
            // ロードマップ順（区切り → カテゴリ → 記事）。ContentService.Articles と同じ並び
            content.Categories.SelectMany(c => content.GetArticles(c.Id)).Select(a => new ArticleDto(
                a.Category, a.Slug, a.Order, a.Title, a.Reading, a.Description, a.Difficulty, a.BlenderVersion,
                a.Updated?.ToString("yyyy-MM-dd"), a.Shortcuts, a.Html, a.PracticeScenarios, a.Url, a.Id)).ToList(),
            content.Shortcuts.Select(s => new ShortcutDto(s.Key, s.Function, s.Category, s.Description)).ToList(),
            content.Addons.Select(a => new AddonDto(a.Name, a.Description, a.Type, a.Price, a.Features, a.Url, a.Status, a.Note, a.Source)).ToList(),
            content.Glossary.Select(g => new GlossaryDto(g.Id, g.Term, g.Reading, g.En, ContentService.RenderInline(g.Description), Inline(g.Body),
                g.Tags, g.Shortcuts, g.Articles, g.Related, g.Keywords, g.Source)).ToList(),
            content.Howtos.Select(h => new HowtoDto(h.Id, h.Question, h.Reading, h.Answer, h.AnswerEn, h.Menu, h.MenuEn, h.Shortcuts, Inline(h.Note),
                h.Aliases, h.Tags, h.Articles, h.Related, h.Keywords, Inline(h.Body), h.Source)).ToList(),
            content.Tips.Select(t => new TipDto(t.Id, t.Title, t.Reading, Inline(t.Summary), Inline(t.Symptom), Inline(t.Cause), Inline(t.Fix), t.Tags,
                t.Shortcuts, t.Articles, t.Related, t.Keywords, Inline(t.Body), t.Source)).ToList(),
            content.Qas.Select(q => new QaDto(q.Id, q.Question, q.Reading, ContentService.RenderInline(q.Answer), Inline(q.Body), q.Tags, q.Shortcuts,
                q.Articles, q.Related, q.Keywords, q.Source)).ToList(),
            linkCategories,
            content.Links.Select(l => new LinkDto(l.Id, l.Name, l.Reading, l.Url, l.Category, l.Description, l.Note, l.Status, l.Source)).ToList(),
            content.Changelog.Select(e => new ChangelogDto(e.Date.ToString("yyyy-MM-dd"), e.Title, e.Items)).ToList());
    }

    public SectionDto? SectionOf(CategoryDto category) => Sections.FirstOrDefault(s => s.Id == category.Section);
    public IReadOnlyList<CategoryDto> CategoriesIn(string sectionId) => Categories.Where(c => c.Section == sectionId).ToList();
    public IReadOnlyList<ArticleDto> ArticlesOf(string categoryId) => Articles.Where(a => a.Category == categoryId).ToList();
}
