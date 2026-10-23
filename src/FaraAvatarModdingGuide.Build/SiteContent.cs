using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Build;

/// <summary>ContentService（Markdown / YAML の読み込み結果）から、content.json の中身（SiteContent）を作る。</summary>
public static class SiteContentFactory
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
                a.Updated?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), a.Shortcuts, a.Html, a.PracticeScenarios, a.Url, a.Id)).ToList(),
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
            content.Changelog.Select(e => new ChangelogDto(e.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), e.Title, e.Items)).ToList());
    }
}
