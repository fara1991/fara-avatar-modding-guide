using FaraAvatarModdingGuide.Models;
using FaraSiteKit.Razor;
using MudBlazor;

namespace FaraAvatarModdingGuide.Web.Layout;

/// <summary>
/// サイドバーの項目。現行の SSR 版（Components/Layout/NavMenu.razor）と同じ並び:
/// ホーム・目次、目次の区切りごと（Blender / Unity / VRChat …）のカテゴリ、リファレンス、練習、このサイトについて。
/// 区切りとカテゴリは content.json から。読めなかったときは、固定の項目だけ出す。
/// </summary>
public static class NavGroups
{
    public static IReadOnlyList<SiteNavGroup> Build(SiteContent? content)
    {
        var groups = new List<SiteNavGroup>
        {
            new(null, new[]
            {
                new SiteNavItem("", "ホーム", Icons.Material.Filled.Home),
                new SiteNavItem("learn", "目次", Icons.Material.Filled.FormatListNumbered),
            }),
        };

        if (content is not null)
        {
            // まだカテゴリのない区切りは出さない
            foreach (var section in content.Sections)
            {
                var categories = content.CategoriesIn(section.Id);
                if (categories.Count == 0) continue;
                groups.Add(new SiteNavGroup(section.Title, categories.Select(c => new SiteNavItem($"learn/{c.Id}", c.Title, Services.IconMap.Get(c.Icon))).ToList()));
            }
        }

        groups.Add(new SiteNavGroup("リファレンス", new[]
        {
            new SiteNavItem("shortcuts", "ショートカット一覧", Icons.Material.Filled.Keyboard),
            new SiteNavItem("glossary", "用語集", Icons.Material.Filled.Article),
            new SiteNavItem("howto", "何て名前？", Icons.Material.Filled.HelpOutline),
            new SiteNavItem("tips", "TIPS", Icons.Material.Filled.Lightbulb),
            new SiteNavItem("qa", "改変 Q&A", Icons.Material.Filled.Chat),
            new SiteNavItem("addons", "アドオン", Icons.Material.Filled.Extension),
            new SiteNavItem("links", "リンク集", Icons.Material.Filled.Link),
            new SiteNavItem("changelog", "更新履歴", Icons.Material.Filled.History),
        }));
        groups.Add(new SiteNavGroup("練習", new[]
        {
            new SiteNavItem("practice", "操作の練習", Icons.Material.Filled.SportsEsports),
            new SiteNavItem("practice/unity", "Unity の練習", Icons.Material.Filled.GridView),
            new SiteNavItem("practice/room", "みんなで練習", Icons.Material.Filled.Groups),
        }));
        groups.Add(new SiteNavGroup("このサイトについて", new[]
        {
            new SiteNavItem("report", "問い合わせ", Icons.Material.Filled.MailOutline),
            new SiteNavItem("privacy", "プライバシーポリシー", Icons.Material.Filled.PrivacyTip),
        }));
        return groups;
    }
}
