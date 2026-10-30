using FaraAvatarModdingGuide.Models;
using FaraBlazorComponent.Core;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// このサイトの名前・作者・保存のキー・SNS。共通部品（fara-blazor-component の Razor）が読む SiteOptions に、起動時に入れる（Program.cs）。
/// 名前・SNS などは data/site.json（Build が appsettings.json の Site から書く）、保存のキーはここで決める。
/// </summary>
public static class SiteInfo
{
    public const string Author = "Fara";

    /// <summary>
    /// 設定（テーマ）を入れる localStorage のキー。SSR 版と同じキー（既存の保存値を引き継ぐ）。index.html の theme-init.js の data-storage-key と同じ値。
    /// SSR 版は文字列 "light" / "dark" をそのまま入れていたが、kit は JSON（{"Theme":"dark"}）なので、js/storage-migrate.js が描画の前に書き換える。
    /// </summary>
    public const string StorageKey = "avatar-modding-guide.theme";

    /// <summary>PC のサイドバーを隠しているか（"1" で隠す）の localStorage のキー。SSR 版と同じキー・同じ値。index.html の theme-init.js の data-sidebar-key と同じ値。</summary>
    public const string SidebarKey = "avatar-modding-guide.sidebar-hidden";

    /// <summary>共通部品の設定に、このサイトの値を入れる。</summary>
    public static void Configure(SiteOptions options, SiteSettings settings)
    {
        options.Name = settings.Title;
        options.Author = Author;
        options.PublishedYear = settings.PublishedYear;
        options.AuthorHomeUrl = settings.PortfolioUrl;
        options.HomeHref = "/";
        options.DevelopHost = settings.DevelopHost;
        options.StorageKey = StorageKey;
        options.SidebarKey = SidebarKey;
        foreach (var social in settings.Socials) options.Social.Add(new SiteSocialLink(social.Service, social.Url));
    }
}
