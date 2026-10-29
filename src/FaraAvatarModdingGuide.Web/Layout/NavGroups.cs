using FaraAvatarModdingGuide.Models;
using Microsoft.AspNetCore.Components.Routing;

namespace FaraAvatarModdingGuide.Web.Layout;

/// <summary>サイドバーの 1 項目。Match は「いまのページ」の判定(All はそのページだけ、Prefix は下位のページも)。ProgressIds があれば、カテゴリの進捗(3/5)を右に出す。</summary>
public sealed record AvNavItem(string Href, string Label, string Icon, NavLinkMatch Match = NavLinkMatch.Prefix, IReadOnlyList<string>? ProgressIds = null);

/// <summary>サイドバーの見出しと、その中の項目。Title が null なら見出しを出さない。</summary>
public sealed record AvNavGroup(string? Title, IReadOnlyList<AvNavItem> Items);

/// <summary>
/// サイドバーの項目。旧 SSR 版（Phase 5 で削除。Git の履歴にある）(Components/Layout/NavMenu.razor)と同じ並び:
/// ホーム・目次、目次の区切りごと(Blender / Unity / VRChat …)のカテゴリ、リファレンス、練習、サポート。
/// 項目の中身(並び・名前・アイコン・強調の規則)は Shared の SiteNav が持つ。静的 HTML(Build)のサイドバーの骨組みも同じものから作るので、起動の前後で食い違わない。
/// 区切りとカテゴリは content.json から。読めなかったときは、固定の項目だけ出す。
/// 「いまのページ」の強調は、SSR 版と同じ: ホーム・目次・操作の練習はそのページだけ(All)、それ以外は下位のページも(Prefix。カテゴリは、その記事のページでも強調)。
/// kit の SiteNavMenu は、ホーム以外を Prefix にするので、/learn が記事のページでも強調され、カテゴリと 2 つ強調されてしまう。
/// </summary>
public static class NavGroups
{
    public static IReadOnlyList<AvNavGroup> Build(SiteContent? content) =>
        SiteNav.Build(content)
            .Select(section => new AvNavGroup(section.Title, section.Items
                .Select(e => new AvNavItem(e.Href, e.Label, Services.IconMap.Get(e.Icon), e.Exact ? NavLinkMatch.All : NavLinkMatch.Prefix, e.ProgressIds))
                .ToList()))
            .ToList();
}
