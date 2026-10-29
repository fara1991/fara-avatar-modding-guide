namespace FaraAvatarModdingGuide.Models;

/// <summary>サイドバーの 1 項目。Href は <base href="/"> からの相対（先頭に / を付けない。ホームは空）。Icon は Material のメンバー名。Exact は「そのページだけ強調」（false は下位のページも）。ProgressIds があれば進捗（3/5）を右に出す。</summary>
public sealed record NavEntry(string Href, string Label, string Icon, bool Exact = false, IReadOnlyList<string>? ProgressIds = null);

/// <summary>サイドバーの見出しと、その中の項目。Title が null なら見出しを出さない。</summary>
public sealed record NavSection(string? Title, IReadOnlyList<NavEntry> Items);

/// <summary>
/// サイドバーの項目（現行の SSR 版 NavMenu.razor と同じ並び）。Build（静的 HTML のサイドバーの骨組み）と Web（WebAssembly のサイドバー。NavGroups）が、
/// この 1 か所から作る。2 か所に書くと、起動の前後でサイドバーの項目・強調が食い違う（計画書 4.10）。
/// ホーム・目次・操作の練習はそのページだけ（Exact）、それ以外は下位のページも強調する（カテゴリは、その記事のページでも強調）。
/// </summary>
public static class SiteNav
{
    public static IReadOnlyList<NavSection> Build(SiteContent? content)
    {
        var sections = new List<NavSection>
        {
            new(null, new[]
            {
                new NavEntry("", "ホーム", "Home", Exact: true),
                new NavEntry("learn", "目次", "FormatListNumbered", Exact: true),
            }),
        };

        if (content is not null)
        {
            // まだカテゴリのない区切りは出さない
            foreach (var section in content.Sections)
            {
                var categories = content.CategoriesIn(section.Id);
                if (categories.Count == 0) continue;
                sections.Add(new NavSection(section.Title, categories.Select(c =>
                    new NavEntry($"learn/{c.Id}", c.NavTitle ?? c.Title, c.Icon, ProgressIds: content.ArticlesOf(c.Id).Select(a => a.Id).ToList())).ToList()));
            }
        }

        sections.Add(new NavSection("リファレンス", new[]
        {
            new NavEntry("shortcuts", "ショートカット一覧", "Keyboard"),
            new NavEntry("glossary", "用語集", "Article"),
            new NavEntry("howto", "何て名前？", "HelpOutline"),
            new NavEntry("tips", "TIPS", "Lightbulb"),
            new NavEntry("qa", "改変 Q&A", "Chat"),
            new NavEntry("addons", "アドオン", "Extension"),
            new NavEntry("links", "リンク集", "Link"),
            new NavEntry("changelog", "更新履歴", "History"),
        }));
        sections.Add(new NavSection("練習", new[]
        {
            new NavEntry("practice", "操作の練習", "SportsEsports", Exact: true),
            new NavEntry("practice/unity", "Unity の練習", "GridView"),
            new NavEntry("practice/room", "みんなで練習", "Groups"),
        }));
        sections.Add(new NavSection("サポート", new[]
        {
            new NavEntry("report", "問い合わせ", "MailOutline"),
            new NavEntry("privacy", "プライバシーポリシー", "PrivacyTip"),
        }));
        return sections;
    }

    /// <summary>
    /// 見ているページ（/ から始まるパス）で、この項目を「いまのページ」として強調するか。Blazor の NavLink の Match と同じ規則:
    /// Exact はそのページだけ、それ以外は下位のページ（href の後ろが / か末尾）も。末尾の / は無視する。
    /// </summary>
    public static bool IsActive(NavEntry entry, string path)
    {
        var current = path.Trim('/');
        if (entry.Exact) return current == entry.Href;
        return current == entry.Href || current.StartsWith(entry.Href + "/", StringComparison.Ordinal);
    }
}
