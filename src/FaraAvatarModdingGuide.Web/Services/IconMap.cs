using MudBlazor;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// content.json に入っているアイコンの名前（Material のメンバー名。Build の SiteIcons が bi-* から写したもの）を、MudBlazor の SVG に引く。
/// 文字列からの引き当てにリフレクションを使わない（トリミングで消えないように、定数を明示する）。
/// Build が出す名前を全部持っていることは、Web.Tests が確かめる。
/// </summary>
public static class IconMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["Accessibility"] = Icons.Material.Filled.Accessibility,
        ["AccountBox"] = Icons.Material.Filled.AccountBox,
        ["Add"] = Icons.Material.Filled.Add,
        ["AltRoute"] = Icons.Material.Filled.AltRoute,
        ["Apps"] = Icons.Material.Filled.Apps,
        ["ArrowForward"] = Icons.Material.Filled.ArrowForward,
        ["Article"] = Icons.Material.Filled.Article,
        ["Build"] = Icons.Material.Filled.Build,
        ["Chat"] = Icons.Material.Filled.Chat,
        ["CheckCircle"] = Icons.Material.Filled.CheckCircle,
        ["CloudUpload"] = Icons.Material.Filled.CloudUpload,
        ["Collections"] = Icons.Material.Filled.Collections,
        ["CollectionsBookmark"] = Icons.Material.Filled.CollectionsBookmark,
        ["ExpandMore"] = Icons.Material.Filled.ExpandMore,
        ["Extension"] = Icons.Material.Filled.Extension,
        ["FormatListNumbered"] = Icons.Material.Filled.FormatListNumbered,
        ["Forum"] = Icons.Material.Filled.Forum,
        ["GridView"] = Icons.Material.Filled.GridView,
        ["Groups"] = Icons.Material.Filled.Groups,
        ["HelpOutline"] = Icons.Material.Filled.HelpOutline,
        ["History"] = Icons.Material.Filled.History,
        ["Home"] = Icons.Material.Filled.Home,
        ["Keyboard"] = Icons.Material.Filled.Keyboard,
        ["Lightbulb"] = Icons.Material.Filled.Lightbulb,
        ["Lock"] = Icons.Material.Filled.Lock,
        ["Link"] = Icons.Material.Filled.Link,
        ["Menu"] = Icons.Material.Filled.Menu,
        ["MailOutline"] = Icons.Material.Filled.MailOutline,
        ["MenuBook"] = Icons.Material.Filled.MenuBook,
        ["OpenInNew"] = Icons.Material.Filled.OpenInNew,
        ["OpenWith"] = Icons.Material.Filled.OpenWith,
        ["Palette"] = Icons.Material.Filled.Palette,
        ["PrivacyTip"] = Icons.Material.Filled.PrivacyTip,
        ["RadioButtonUnchecked"] = Icons.Material.Filled.RadioButtonUnchecked,
        ["RestartAlt"] = Icons.Material.Filled.RestartAlt,
        ["RocketLaunch"] = Icons.Material.Filled.RocketLaunch,
        ["Search"] = Icons.Material.Filled.Search,
        ["ShoppingBag"] = Icons.Material.Filled.ShoppingBag,
        ["SportsEsports"] = Icons.Material.Filled.SportsEsports,
        ["SubdirectoryArrowRight"] = Icons.Material.Filled.SubdirectoryArrowRight,
        ["TaskAlt"] = Icons.Material.Filled.TaskAlt,
        ["ViewInAr"] = Icons.Material.Filled.ViewInAr,
        ["Vrpano"] = Icons.Material.Filled.Vrpano,
        ["Warning"] = Icons.Material.Filled.Warning,
        ["Woman"] = Icons.Material.Filled.Woman,
    };

    /// <summary>名前に対応する SVG（MudIcon の Icon に渡す）。無い名前は空。</summary>
    public static string Get(string? name) => name is not null && Map.TryGetValue(name, out var svg) ? svg : string.Empty;

    /// <summary>持っている名前の一覧。</summary>
    public static IReadOnlyCollection<string> Names => Map.Keys;
}
