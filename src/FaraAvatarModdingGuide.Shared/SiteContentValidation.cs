namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// content.json・site.json が、JSON として読めても、形が違う(例: {})ときに、画面が null 参照で落ちて空白になるのを防ぐ検査。
/// System.Text.Json は、足りない項目を null のままにする(型は null を許さない書き方でも)。必須の一覧と、項目の中の一覧が null でないことを確かめる。
/// 問題があれば、その理由(コンソール用)を返す。無ければ null。
/// </summary>
public static class SiteContentValidation
{
    public static string? Check(SiteContent? c)
    {
        if (c is null) return "data/content.json が空です";
        foreach (var (name, list) in new (string, object?)[]
        {
            ("sections", c.Sections), ("categories", c.Categories), ("articles", c.Articles), ("shortcuts", c.Shortcuts), ("addons", c.Addons),
            ("glossary", c.Glossary), ("howtos", c.Howtos), ("tips", c.Tips), ("qas", c.Qas), ("linkCategories", c.LinkCategories),
            ("links", c.Links), ("changelog", c.Changelog),
        })
        {
            if (list is null) return $"data/content.json の {name} がありません";
        }
        if (c.Articles.Any(a => a is null || a.Html is null || a.Shortcuts is null || a.PracticeScenarios is null || a.Url is null || a.Id is null || a.Title is null)) return "data/content.json の articles の形が違います";
        if (c.Categories.Any(x => x is null || x.Id is null || x.Title is null)) return "data/content.json の categories の形が違います";
        if (c.Sections.Any(x => x is null || x.Id is null || x.Title is null)) return "data/content.json の sections の形が違います";
        if (c.Shortcuts.Any(x => x is null || x.Key is null || x.Category is null)) return "data/content.json の shortcuts の形が違います";
        if (c.Addons.Any(x => x is null || x.Features is null || x.Name is null)) return "data/content.json の addons の形が違います";
        if (c.Glossary.Any(x => x is null || x.Tags is null || x.Shortcuts is null || x.Articles is null || x.Related is null || x.Keywords is null || x.DescriptionHtml is null)) return "data/content.json の glossary の形が違います";
        if (c.Howtos.Any(x => x is null || x.Tags is null || x.Shortcuts is null || x.Articles is null || x.Related is null || x.Keywords is null || x.Aliases is null)) return "data/content.json の howtos の形が違います";
        if (c.Tips.Any(x => x is null || x.Tags is null || x.Shortcuts is null || x.Articles is null || x.Related is null || x.Keywords is null)) return "data/content.json の tips の形が違います";
        if (c.Qas.Any(x => x is null || x.Tags is null || x.Shortcuts is null || x.Articles is null || x.Related is null || x.Keywords is null || x.AnswerHtml is null)) return "data/content.json の qas の形が違います";
        if (c.LinkCategories.Any(x => x is null || x.Id is null)) return "data/content.json の linkCategories の形が違います";
        if (c.Links.Any(x => x is null || x.Url is null || x.Name is null)) return "data/content.json の links の形が違います";
        if (c.Changelog.Any(x => x is null || x.Items is null || x.Date is null)) return "data/content.json の changelog の形が違います";
        return null;
    }

    /// <summary>data/site.json の検査(SNS の一覧・サイト名)。</summary>
    public static string? Check(SiteSettings? s)
    {
        if (s is null) return "data/site.json が空です";
        if (string.IsNullOrWhiteSpace(s.Title) || s.Socials is null || s.Socials.Any(x => x is null)) return "data/site.json の形が違います";
        return null;
    }
}
