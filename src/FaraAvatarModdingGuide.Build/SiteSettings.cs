using System.Text.Json;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Build;

/// <summary>appsettings.json の "Site" から、data/site.json の中身を作る。</summary>
public static class SiteSettingsLoader
{
    /// <summary>
    /// サイトの名前・Blender/Unity のバージョン・リポジトリ・フッターの SNS は、リポジトリ直下の appsettings.json の Site に書く（ASP.NET Core の設定ではなく、Build とテストが読む JSON。
    /// 旧 SSR 版の名残の名前だが、Phase 5 でそのまま情報源として残した。公開先の設定（origin・CSP）は tools/site.json）。
    /// developHost は、確認用の公開先のホスト名。
    /// </summary>
    public static SiteSettings Load(string appSettingsPath, string developHost)
    {
        using var stream = File.OpenRead(appSettingsPath);
        using var doc = JsonDocument.Parse(stream);
        var site = doc.RootElement.GetProperty("Site").Deserialize<SiteOptions>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? throw new InvalidDataException($"{appSettingsPath} に Site がありません");
        if (string.IsNullOrWhiteSpace(site.Title)) throw new InvalidDataException("Site:Title が空です");
        return new SiteSettings(
            site.Title, site.BlenderVersion, site.UnityVersion, site.RepositoryUrl, site.PublishedYear, site.PortfolioUrl, developHost,
            site.Socials.Where(s => !string.IsNullOrWhiteSpace(s.Url)).Select(s => new SocialDto(s.Service, s.Label, s.Url)).ToList());
    }
}
