using System.Reflection;
using MudBlazor;

namespace FaraAvatarModdingGuide.Build;

/// <summary>
/// Content の YAML や C# に書いてある Bootstrap Icons の名前（bi-*）を、MudBlazor の Material アイコン（Icons.Material.Filled のメンバー名）に写す（計画書 D6）。
/// content.json には Material 側の名前を入れる。WebAssembly 側（Web の IconMap）は、同じ名前を Icons.Material.Filled の定数に引く。
/// </summary>
public static class SiteIcons
{
    /// <summary>bi-* → Icons.Material.Filled のメンバー名。</summary>
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["bi-arrow-counterclockwise"] = "RestartAlt",
        ["bi-arrow-return-right"] = "SubdirectoryArrowRight",
        ["bi-arrow-right"] = "ArrowForward",
        ["bi-arrows-move"] = "OpenWith",
        ["bi-book"] = "MenuBook",
        ["bi-box"] = "ViewInAr",
        ["bi-box-arrow-up-right"] = "OpenInNew",
        ["bi-chat-left-text"] = "Chat",
        ["bi-check-circle-fill"] = "CheckCircle",
        ["bi-check2-circle"] = "TaskAlt",
        ["bi-circle"] = "RadioButtonUnchecked",
        ["bi-clock-history"] = "History",
        ["bi-cloud-upload"] = "CloudUpload",
        ["bi-collection"] = "Collections",
        ["bi-controller"] = "SportsEsports",
        ["bi-discord"] = "Forum",
        ["bi-envelope"] = "MailOutline",
        ["bi-exclamation-triangle"] = "Warning",
        ["bi-grid-3x3-gap"] = "GridView",
        ["bi-handbag"] = "ShoppingBag",
        ["bi-headset-vr"] = "Vrpano",
        ["bi-house-door"] = "Home",
        ["bi-journal-bookmark"] = "CollectionsBookmark",
        ["bi-journal-text"] = "Article",
        ["bi-keyboard"] = "Keyboard",
        ["bi-lightbulb"] = "Lightbulb",
        ["bi-link-45deg"] = "Link",
        ["bi-list-ol"] = "FormatListNumbered",
        ["bi-palette"] = "Palette",
        ["bi-people"] = "Groups",
        ["bi-person-arms-up"] = "Accessibility",
        ["bi-person-bounding-box"] = "AccountBox",
        ["bi-person-standing-dress"] = "Woman",
        ["bi-plus-lg"] = "Add",
        ["bi-puzzle"] = "Extension",
        ["bi-question-circle"] = "HelpOutline",
        ["bi-rocket-takeoff"] = "RocketLaunch",
        ["bi-search"] = "Search",
        ["bi-shield-check"] = "PrivacyTip",
        ["bi-signpost-split"] = "AltRoute",
        ["bi-tools"] = "Build",
        ["bi-ui-radios-grid"] = "Apps",
    };

    /// <summary>bi-* の名前を Material のメンバー名にする。未知の名前は例外（アイコンを足したときに写し忘れない）。</summary>
    public static string ToMaterial(string biName)
    {
        if (Map.TryGetValue(biName, out var material)) return material;
        throw new KeyNotFoundException($"アイコン {biName} の写し先が Build/Icons.cs にありません");
    }

    /// <summary>Material 側の名前の一覧（Web の IconMap が全部持っているかのテスト用）。</summary>
    public static IReadOnlyCollection<string> MaterialNames => Map.Values.Distinct().Order(StringComparer.Ordinal).ToList();

    /// <summary>bi-* の名前の一覧。</summary>
    public static IReadOnlyCollection<string> BootstrapNames => Map.Keys;

    /// <summary>Material のメンバー名から SVG の path（MudBlazor の定数は <path .../> の文字列）を引く。</summary>
    public static string SvgMarkup(string materialName)
    {
        var field = typeof(MudBlazor.Icons.Material.Filled).GetField(materialName, BindingFlags.Public | BindingFlags.Static)
                    ?? throw new KeyNotFoundException($"Icons.Material.Filled.{materialName} がありません");
        return (string)field.GetRawConstantValue()!;
    }

    /// <summary>静的 HTML に埋め込むアイコン（装飾なので読み上げには出さない）。</summary>
    public static string Svg(string biOrMaterialName, string cssClass = "av-icon")
    {
        var material = biOrMaterialName.StartsWith("bi-", StringComparison.Ordinal) ? ToMaterial(biOrMaterialName) : biOrMaterialName;
        return $"<svg class=\"{cssClass}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\" fill=\"currentColor\">{SvgMarkup(material)}</svg>";
    }
}
