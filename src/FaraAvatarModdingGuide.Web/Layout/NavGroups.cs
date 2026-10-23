using FaraAvatarModdingGuide.Models;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace FaraAvatarModdingGuide.Web.Layout;

/// <summary>サイドバーの 1 項目。Match は「いまのページ」の判定(All はそのページだけ、Prefix は下位のページも)。ProgressIds があれば、カテゴリの進捗(3/5)を右に出す。</summary>
public sealed record AvNavItem(string Href, string Label, string Icon, NavLinkMatch Match = NavLinkMatch.Prefix, IReadOnlyList<string>? ProgressIds = null);

/// <summary>サイドバーの見出しと、その中の項目。Title が null なら見出しを出さない。</summary>
public sealed record AvNavGroup(string? Title, IReadOnlyList<AvNavItem> Items);

/// <summary>
/// サイドバーの項目。現行の SSR 版(Components/Layout/NavMenu.razor)と同じ並び:
/// ホーム・目次、目次の区切りごと(Blender / Unity / VRChat …)のカテゴリ、リファレンス、練習、このサイトについて。
/// 区切りとカテゴリは content.json から。読めなかったときは、固定の項目だけ出す。
/// 「いまのページ」の強調は、SSR 版と同じ: ホーム・目次・操作の練習はそのページだけ(All)、それ以外は下位のページも(Prefix。カテゴリは、その記事のページでも強調)。
/// kit の SiteNavMenu は、ホーム以外を Prefix にするので、/learn が記事のページでも強調され、カテゴリと 2 つ強調されてしまう。
/// </summary>
public static class NavGroups
{
    public static IReadOnlyList<AvNavGroup> Build(SiteContent? content)
    {
        var groups = new List<AvNavGroup>
        {
            new(null, new[]
            {
                new AvNavItem("", "ホーム", Icons.Material.Filled.Home, NavLinkMatch.All),
                new AvNavItem("learn", "目次", Icons.Material.Filled.FormatListNumbered, NavLinkMatch.All),
            }),
        };

        if (content is not null)
        {
            // まだカテゴリのない区切りは出さない
            foreach (var section in content.Sections)
            {
                var categories = content.CategoriesIn(section.Id);
                if (categories.Count == 0) continue;
                groups.Add(new AvNavGroup(section.Title, categories.Select(c =>
                    new AvNavItem($"learn/{c.Id}", c.Title, Services.IconMap.Get(c.Icon), NavLinkMatch.Prefix, content.ArticlesOf(c.Id).Select(a => a.Id).ToList())).ToList()));
            }
        }

        groups.Add(new AvNavGroup("リファレンス", new[]
        {
            new AvNavItem("shortcuts", "ショートカット一覧", Icons.Material.Filled.Keyboard),
            new AvNavItem("glossary", "用語集", Icons.Material.Filled.Article),
            new AvNavItem("howto", "何て名前？", Icons.Material.Filled.HelpOutline),
            new AvNavItem("tips", "TIPS", Icons.Material.Filled.Lightbulb),
            new AvNavItem("qa", "改変 Q&A", Icons.Material.Filled.Chat),
            new AvNavItem("addons", "アドオン", Icons.Material.Filled.Extension),
            new AvNavItem("links", "リンク集", Icons.Material.Filled.Link),
            new AvNavItem("changelog", "更新履歴", Icons.Material.Filled.History),
        }));
        groups.Add(new AvNavGroup("練習", new[]
        {
            new AvNavItem("practice", "操作の練習", Icons.Material.Filled.SportsEsports, NavLinkMatch.All),
            new AvNavItem("practice/unity", "Unity の練習", Icons.Material.Filled.GridView),
            new AvNavItem("practice/room", "みんなで練習", Icons.Material.Filled.Groups),
        }));
        groups.Add(new AvNavGroup("このサイトについて", new[]
        {
            new AvNavItem("report", "問い合わせ", Icons.Material.Filled.MailOutline),
            new AvNavItem("privacy", "プライバシーポリシー", Icons.Material.Filled.PrivacyTip),
        }));
        return groups;
    }
}
