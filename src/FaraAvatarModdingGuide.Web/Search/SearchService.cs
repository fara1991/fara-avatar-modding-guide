using System.Net.Http.Json;
using FaraAvatarModdingGuide.Models;
using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Search;

/// <summary>検索語の正規化（NFKC・小文字・全角チルダを〜に・カタカナをひらがなに）。ブラウザでは JavaScript の normalize を使う（計画書 D9）。</summary>
public interface ISearchNormalizer
{
    string Norm(string text);
}

/// <summary>js/search-norm.js の avatarSearch.norm を呼ぶ。WebAssembly の JS 呼び出しは同期でできるので、検索のたび（入力のたび）に待たずに済む。</summary>
public sealed class JsSearchNormalizer(IJSRuntime js) : ISearchNormalizer
{
    public string Norm(string text) =>
        js is IJSInProcessRuntime sync
            ? sync.Invoke<string>("avatarSearch.norm", text)
            : throw new InvalidOperationException("検索語の正規化には、同期の JS 呼び出し（WebAssembly）が要ります");
}

/// <summary>
/// 検索の索引（data/search.json）を、最初に必要になったときに 1 回だけ読む（ヘッダーの検索欄にフォーカスしたとき・検索ページを開いたとき）。
/// 読めなかったときは null を返し、次に呼ばれたときにもう一度読む。
/// </summary>
public sealed class SearchService(HttpClient http, ISearchNormalizer normalizer)
{
    private Task<SearchEngine?>? _loading;

    /// <summary>読み込み済みの検索。まだ・読めなかったときは null。</summary>
    public SearchEngine? Engine { get; private set; }

    /// <summary>索引を読んで、検索を返す（2 回目からは読み込み済みのものを返す）。読めなければ null。</summary>
    public Task<SearchEngine?> GetAsync() => Engine is not null ? Task.FromResult<SearchEngine?>(Engine) : _loading ??= LoadAsync();

    private async Task<SearchEngine?> LoadAsync()
    {
        try
        {
            var records = await http.GetFromJsonAsync("data/search.json", SiteJsonContext.Default.IReadOnlyListSearchRecord);
            if (records is null) { _loading = null; return null; }
            Engine = new SearchEngine(records, normalizer.Norm);
            return Engine;
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            Console.Error.WriteLine($"[avatar] data/search.json を読めません: {e.Message}");
            _loading = null;   // 次の入力でもう一度読む
            return null;
        }
    }
}
