using System.Text.Json;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Build;

/// <summary>appsettings.json の "Site" から、data/site.json の中身を作る。</summary>
public static class SiteSettingsLoader
{
    /// <summary>
    /// SSR 版と同じ情報源（appsettings.json の Site）を読む。SSR を消すフェーズ 5 で、これを tools/ の設定に移す。
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
