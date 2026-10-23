using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.Extensions.DependencyInjection;
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
    public static readonly SiteSettings Settings = SiteSettingsLoader.Load(Path.Combine(Root, "appsettings.json"), "avatar-wasm-dev.fara-labs.com");

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
        text = Regex.Replace(text, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", "");
    }
}

/// <summary>data/content.json を返すだけの HttpMessageHandler（本物の Content を、Build と同じ型情報で JSON にして返す）。</summary>
internal sealed class FakeDataHandler(SiteContent? content, bool fail = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        var json = JsonSerializer.Serialize(content, SiteJsonContext.Default.SiteContent);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}

/// <summary>ページの Razor を描画するテストの土台。</summary>
public abstract class WebTestContext : BunitContext
{
    protected WebTestContext(bool contentLoaded = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TestData.Settings);
        var store = new ContentStore(new HttpClient(new FakeDataHandler(TestData.Content, fail: !contentLoaded)) { BaseAddress = new Uri("http://localhost/") });
        store.LoadAsync().GetAwaiter().GetResult();
        Services.AddSingleton(store);
    }
}
