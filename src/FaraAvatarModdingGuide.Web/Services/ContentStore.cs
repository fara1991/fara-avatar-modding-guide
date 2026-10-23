using System.Net.Http.Json;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// 記事・用語・リンクなどのデータ（data/content.json。Build が書く）。起動時に 1 回だけ読む（計画書 D2: 全記事を 1 つの JSON に入れる）。
/// 読めなかったときは Content が null のまま（Error に理由）。各ページは、その場合に案内を出す。
/// </summary>
public sealed class ContentStore(HttpClient http)
{
    /// <summary>読み込んだデータ。まだ・または読めなかったときは null。</summary>
    public SiteContent? Content { get; private set; }

    /// <summary>読めなかったときの理由（画面には出さない。コンソール用）。</summary>
    public string? Error { get; private set; }

    public async Task LoadAsync()
    {
        if (Content is not null) return;
        try
        {
            var loaded = await http.GetFromJsonAsync("data/content.json", SiteJsonContext.Default.SiteContent);
            // JSON として読めても形が違う({} など)ときは、読み込み失敗として扱う(各ページが null 参照で落ちて、空白になるのを防ぐ)
            Error = SiteContentValidation.Check(loaded);
            if (Error is null) Content = loaded;
            else Console.Error.WriteLine($"[avatar] {Error}");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            Error = e.Message;
            Console.Error.WriteLine($"[avatar] data/content.json を読めません: {e.Message}");
        }
    }
}
