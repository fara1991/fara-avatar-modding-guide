using System.Net.Http.Json;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// 用語集・何て名前？・TIPS・改変 Q&amp;A の絞り込み用の文字（data/filter.json。Build が書く）。項目ごとに、その項目の表示される文字すべて
/// （折りたたまれた「詳しく」の中・別名を含む）を、NFKC・小文字にしたもの。SSR 版の tipsfilter.js が、DOM の textContent から作っていたのと同じ。
/// 一覧のページを開いたときに 1 回だけ読む（起動時には読まない）。キーは種類（glossary / howto / tip / qa）→ 項目の id。
/// </summary>
public sealed class FilterTextStore(HttpClient http)
{
    private Task<Dictionary<string, Dictionary<string, string>>?>? _loading;

    /// <summary>種類ごとの、項目の id → 絞り込み用の文字。読めなければ null（絞り込みの文字入力は、何も絞らない）。</summary>
    public Task<Dictionary<string, Dictionary<string, string>>?> GetAsync() => _loading ??= LoadAsync();

    private async Task<Dictionary<string, Dictionary<string, string>>?> LoadAsync()
    {
        try
        {
            return await http.GetFromJsonAsync("data/filter.json", SiteJsonContext.Default.DictionaryStringDictionaryStringString);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"[avatar] data/filter.json を読めません: {e.Message}");
            _loading = null;
            return null;
        }
    }
}
