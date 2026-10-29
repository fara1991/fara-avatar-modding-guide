using FaraBlazorComponent.Razor;
using MudBlazor;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// このサイトの MudBlazor のテーマ。kit の既定（紫）から、色と角丸だけを替える（字体・寸法は kit の SiteTheme のまま）。
/// 値は wwwroot/css/app.css の先頭の「テーマ」ブロック（--accent など）と同じ系統の色にそろえる。
/// </summary>
public static class AvatarTheme
{
    public const string DefaultBorderRadius = "8px";

    public static MudTheme Create()
    {
        var theme = SiteTheme.Create();

        theme.PaletteLight.Primary = "#0e7490";
        theme.PaletteLight.PrimaryContrastText = "#ffffff";
        theme.PaletteLight.Background = "#f1f8f9";
        theme.PaletteLight.Surface = "#ffffff";
        theme.PaletteLight.TextPrimary = "#10262d";
        theme.PaletteLight.TextSecondary = "#476670";
        theme.PaletteLight.DrawerBackground = "#0a2029";
        theme.PaletteLight.AppbarBackground = "#0d2a34";

        theme.PaletteDark.Primary = "#2bb8c9";
        theme.PaletteDark.PrimaryContrastText = "#04161c";   // 明るいアクセントの上は、白ではなく濃い文字（コントラスト 7.7）
        theme.PaletteDark.Background = "#071318";
        theme.PaletteDark.Surface = "#0f2a33";
        theme.PaletteDark.TextPrimary = "#e8f4f6";
        theme.PaletteDark.TextSecondary = "#9db8bf";
        theme.PaletteDark.DrawerBackground = "#0a2029";
        theme.PaletteDark.AppbarBackground = "#0d2a34";

        theme.LayoutProperties.DefaultBorderRadius = DefaultBorderRadius;
        return theme;
    }
}
