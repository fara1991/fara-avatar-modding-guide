using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Search;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using FaraSiteKit.Razor;
using Xunit;

// SiteLinks（確認用ホスト）などは静的な設定なので、テストを並列に動かさない
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>リポジトリのルートと、本物の Content から作ったデータ（Build と同じ手順）。</summary>
internal static class TestData
{
    public static readonly string Root = FindRoot();
    public static readonly ContentService Service = new(Path.Combine(Root, "Content"));
    public static readonly SiteContent Content = SiteContentFactory.From(Service);
    public static readonly SiteSettings Settings = SiteSettingsLoader.Load(Path.Combine(Root, "appsettings.json"), "avatar-preview.fara-labs.com");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")) && Directory.Exists(Path.Combine(dir.FullName, "Content"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("リポジトリのルートが見つかりません");
    }

    /// <summary>HTML から文字だけを取り出し、空白を全部落とす（構造・改行の違いは見ない。本文のテキストの一致の検証用）。</summary>
    public static string TextOf(string html)
    {
        var text = Regex.Replace(html, "<svg.*?</svg>", "", RegexOptions.Singleline);
        text = Regex.Replace(text, "<!--.*?-->", "", RegexOptions.Singleline);
        text = Regex.Replace(text, "<noscript>.*?</noscript>", "", RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", "");
    }
}

/// <summary>検索語の正規化の、テスト用（C# の NFKC。ブラウザでは JavaScript の normalize。同じ結果になることは SearchParityTests・search-norm-fn.test.mjs が確かめる）。</summary>
internal sealed class ManagedNormalizer : ISearchNormalizer
{
    public string Norm(string text) => SearchIndexJson.Normalize(text);

    public string NormFilter(string text) => SearchIndexJson.NormalizeFilter(text);
}

/// <summary>data/*.json を返すだけの HttpMessageHandler（本物の Content から、Build と同じ型情報・同じ手順で作る）。</summary>
internal sealed class FakeDataHandler(SiteContent? content, bool fail = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        var path = request.RequestUri!.AbsolutePath;
        string json;
        if (path.EndsWith("filter.json", StringComparison.Ordinal))
            json = JsonSerializer.Serialize(PageCatalog.FilterTexts(TestData.Content, TestData.Settings).ToDictionary(kv => kv.Key, kv => kv.Value), SiteJsonContext.Default.DictionaryStringDictionaryStringString);
        else if (path.EndsWith("search.json", StringComparison.Ordinal))
            json = JsonSerializer.Serialize(SearchIndexJson.From(TestData.Service.BuildSearchIndex()), SiteJsonContext.Default.IReadOnlyListSearchRecord);
        else
            json = JsonSerializer.Serialize(content, SiteJsonContext.Default.SiteContent);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}

/// <summary>ページの Razor を描画するテストの土台。</summary>
public abstract class WebTestContext : BunitContext, IAsyncLifetime
{
    protected WebTestContext(bool contentLoaded = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TestData.Settings);
        // SiteOptions・テーマの状態・MudBlazor（AddFaraSiteKit。SiteInfo.Configure は Program.cs と同じ値を入れる）
        Services.AddFaraSiteKit(o => SiteInfo.Configure(o, TestData.Settings));
        var http = new HttpClient(new FakeDataHandler(TestData.Content, fail: !contentLoaded)) { BaseAddress = new Uri("http://localhost/") };
        var store = new ContentStore(http);
        store.LoadAsync().GetAwaiter().GetResult();
        Services.AddSingleton(store);
        Services.AddSingleton<ISearchNormalizer, ManagedNormalizer>();
        Services.AddSingleton(sp => new SearchService(http, sp.GetRequiredService<ISearchNormalizer>()));
        Services.AddSingleton(new FilterTextStore(http));
        Services.AddSingleton<ProgressStore>();
    }

    /// <summary>MudBlazor のサービスは非同期に破棄するため、xUnit からは非同期の破棄（DisposeAsync）を呼ばせる。</summary>
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
}
