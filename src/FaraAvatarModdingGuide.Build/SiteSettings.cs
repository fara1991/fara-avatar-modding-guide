using System.Text.Json;
using FaraAvatarModdingGuide.Data;

namespace FaraAvatarModdingGuide.Build;

/// <summary>data/site.json の中身（WebAssembly 側の SiteInfo が読む）。サイト名・バージョン・フッターの SNS など。</summary>
public sealed record SiteSettings(
    string Title, string BlenderVersion, string UnityVersion, string RepositoryUrl, int PublishedYear, string PortfolioUrl,
    string DevelopHost, IReadOnlyList<SocialDto> Socials)
{
    /// <summary>
    /// appsettings.json の "Site" を読む（SSR 版と同じ情報源。SSR を消すフェーズ 5 で、これを tools/ の設定に移す）。
    /// developHost は、確認用の公開先のホスト名（フッターの作者リンクを確認用に切り替える基準）。
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

public sealed record SocialDto(string Service, string Label, string Url);
