using System.Net.Http.Json;
using FaraAvatarWasm.Models;

namespace FaraAvatarWasm.Services;

/// <summary>ビルド時に書き出した JSON（wwwroot/data）を、最初に使うときに一度だけ取得する。</summary>
public sealed class ContentStore(HttpClient http)
{
    private Task<SiteContent>? _content;
    private Task<List<SearchEntry>>? _search;

    public Task<SiteContent> GetContentAsync() => _content ??= Get<SiteContent>("data/content.json");

    public Task<List<SearchEntry>> GetSearchIndexAsync() => _search ??= Get<List<SearchEntry>>("data/search.json");

    // 相対パス（<base href> から解決される）。失敗したら次回また取り直す
    private async Task<T> Get<T>(string path)
    {
        try
        {
            return (await http.GetFromJsonAsync<T>(path))!;
        }
        catch
        {
            _content = null;
            _search = null;
            throw;
        }
    }
}
