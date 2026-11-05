namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// 左のサイドバーの 1 項目。Href が無いものは、開閉の見出し（区切り。中に Children を持つ）。
/// Prefixes は「このページのとき強調する」パス（そのページと、その下のページ）。Exact は、そのページだけ。
/// ProgressIds があれば、読んだ記事の数（3/5）を右に出す（記事の id の一覧）。
/// </summary>
public sealed record SideItem(
    string Label, string Icon, string? Href = null, IReadOnlyList<string>? Prefixes = null, bool Exact = false,
    IReadOnlyList<string>? ProgressIds = null, IReadOnlyList<SideItem>? Children = null)
{
    /// <summary>開閉の見出し（リンクではない）か。</summary>
    public bool IsFold => Children is not null;
}

/// <summary>サイドバーの見出し（「学ぶ」など）と、その中の項目。Title が null なら見出しを出さない。</summary>
public sealed record SideGroup(string? Title, IReadOnlyList<SideItem> Items);

/// <summary>
/// 左のサイドバーの項目（決定したデザイン F 案。上のヘッダーに並べていた B 案をやめて、サイドバーに戻した）。
/// 静的 HTML（Build の StaticFrame）と WebAssembly の画面（Web の AvSideNav）が、この 1 か所から作る
/// （2 か所に書くと、起動の前後で項目・強調が食い違う）。区切り（Blender・Unity・VRChat…）とカテゴリは content.json から作る。
/// 記事のカテゴリは、「記事をよむ」ではなく、区切りごとの開閉の中に出す（「記事をよむ」は目次のページだけ強調）。
/// 作業の手順書の区切り（MakeSectionIds。アバター軽量化）は、「学ぶ」ではなく、その次の「つくる」に出す。
/// </summary>
public static class SideNav
{
    /// <summary>「はじめかた」のカテゴリ（Blender の区切りの先頭にあるが、サイドバーでは独立した項目にする）。</summary>
    public const string GettingStartedId = "getting-started";

    /// <summary>「学ぶ」ではなく「つくる」に出す区切り（作業の手順書。アバター軽量化）。</summary>
    public static readonly IReadOnlyList<string> MakeSectionIds = ["optimize"];

    public static IReadOnlyList<SideGroup> Build(SiteContent? content)
    {
        var learn = new List<SideItem>
        {
            new("記事をよむ", "MenuBook", "/learn", ["/learn"], Exact: true),
        };
        var make = new List<SideItem>();

        if (content is not null)
        {
            foreach (var section in content.Sections)
            {
                var categories = content.CategoriesIn(section.Id)
                    .Where(c => c.Id != GettingStartedId && content.ArticlesOf(c.Id).Count > 0)
                    .ToList();
                if (categories.Count == 0) continue;
                var children = categories
                    .Select(c => new SideItem(c.NavTitle ?? c.Title, c.Icon, $"/learn/{c.Id}", [$"/learn/{c.Id}"],
                        ProgressIds: content.ArticlesOf(c.Id).Select(a => a.Id).ToList()))
                    .ToList();
                var ids = categories.SelectMany(c => content.ArticlesOf(c.Id)).Select(a => a.Id).ToList();
                (MakeSectionIds.Contains(section.Id) ? make : learn).Add(new SideItem(section.Title, section.Icon, ProgressIds: ids, Children: children));
            }
        }

        learn.Add(new("困ったとき", "HelpOutline", "/howto", ["/howto", "/tips", "/qa"]));

        var groups = new List<SideGroup>
        {
            new(null,
            [
                new("ホーム", "Home", "/", ["/"], Exact: true),
                new("はじめかた", "RocketLaunch", "/learn/" + GettingStartedId, ["/learn/" + GettingStartedId],
                    ProgressIds: content?.ArticlesOf(GettingStartedId).Select(a => a.Id).ToList()),
            ]),
            new("学ぶ", learn),
            new("練習",
            [
                new("操作の練習", "SportsEsports", "/practice", ["/practice"], Exact: true),
                new("Unity の練習", "GridView", "/practice/unity", ["/practice/unity"]),
                new("みんなで練習", "Groups", "/practice/room", ["/practice/room"]),
            ]),
            new("参照",
            [
                new("言葉の意味", "Article", "/glossary", ["/glossary"]),
                new("ショートカットキー", "Keyboard", "/shortcuts", ["/shortcuts"]),
                new("便利なアドオン", "Extension", "/addons", ["/addons"]),
                new("リンク", "Link", "/links", ["/links"]),
                new("更新のお知らせ", "History", "/changelog", ["/changelog"]),
            ]),
            new("サポート",
            [
                new("問い合わせ", "MailOutline", "/report", ["/report"]),
                new("プライバシーポリシー", "PrivacyTip", "/privacy", ["/privacy"]),
            ]),
        };

        // 作業の手順書の区切り（アバター軽量化）は、「学ぶ」の次に「つくる」として出す
        if (make.Count > 0) groups.Insert(groups.FindIndex(g => g.Title == "学ぶ") + 1, new SideGroup("つくる", make));
        return groups;
    }

    /// <summary>
    /// 見ているページ（/ から始まるパス。クエリ・# は除く）で、この項目を「いまのページ」にするか。
    /// path が null（知らない URL＝404 の表示）では、どれも強調しない。開閉の見出し（Href が無い）は、強調しない。
    /// </summary>
    public static bool IsActive(SideItem item, string? path)
    {
        if (path is null || item.Href is null || item.Prefixes is null) return false;
        var current = path.Length > 1 ? path.TrimEnd('/') : path;
        if (item.Exact) return item.Prefixes.Any(p => current == p);
        return item.Prefixes.Any(p => Under(current, p));
    }

    /// <summary>開閉の見出しの中に、いまのページがあるか（あれば、開いた状態で出す）。</summary>
    public static bool ContainsActive(SideItem fold, string? path) =>
        fold.Children is not null && fold.Children.Any(c => IsActive(c, path));

    private static bool Under(string current, string prefix) => current == prefix || current.StartsWith(prefix + "/", StringComparison.Ordinal);
}
