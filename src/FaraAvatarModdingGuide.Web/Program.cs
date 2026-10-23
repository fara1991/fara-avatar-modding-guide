using System.Net.Http.Json;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web;
using FaraAvatarModdingGuide.Web.Search;
using FaraAvatarModdingGuide.Web.Services;
using FaraSiteKit.Core;
using FaraSiteKit.Razor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var http = new HttpClient { BaseAddress = baseAddress };
builder.Services.AddSingleton(http);

// 起動に要る 3 つ（サイトの設定 data/site.json・メンテナンスの状態 /api/maintenance・記事などのデータ data/content.json）を、同時に読み始める
// （順に待つと、通信 3 回分の往復が起動に足される）。メンテナンスの状態は 2 秒で打ち切り、読めなければ通常（kit の MaintenanceInfo）。
var settingsTask = LoadSettingsAsync(http);
var maintenanceTask = MaintenanceInfo.LoadAsync(http);
var store = new ContentStore(http);
var contentTask = store.LoadAsync();
await Task.WhenAll(settingsTask, maintenanceTask, contentTask);

var settings = settingsTask.Result;
if (settings is null || store.Content is null)
{
    // 設定か記事のデータが読めないときは、アプリを起動しない。静的 HTML（本文入り）のページが、そのまま残って読める。
    // 読み込み表示は、#blazor-error-ui が出ると外れる（kit の boot-indicator.js）ので、60 秒待たせない
    await using var failed = builder.Build();
    await StartupError.ShowAsync(failed.Services.GetRequiredService<IJSRuntime>());
    return;
}

builder.Services.AddSingleton(settings);
// 共通部品（レイアウト・フッター・報告フォームなど）が読む SiteOptions に、サイトの設定を入れる
builder.Services.AddFaraSiteKit(options => SiteInfo.Configure(options, settings));
builder.Services.AddSingleton(maintenanceTask.Result);
builder.Services.AddSingleton(store);
builder.Services.AddSingleton<ISearchNormalizer, JsSearchNormalizer>();
builder.Services.AddSingleton<SearchService>();   // 検索の索引は、最初に検索するときに読む

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");   // 報告・404 の noindex と、ページの題名に必要

var host = builder.Build();
await StartupError.ApplyDefaultThemeAsync(host.Services);
await host.RunAsync();

/// <summary>data/site.json を読む。読めなければ null（コンソールに理由）。</summary>
static async Task<SiteSettings?> LoadSettingsAsync(HttpClient http)
{
    try
    {
        return await http.GetFromJsonAsync("data/site.json", SiteJsonContext.Default.SiteSettings);
    }
    catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
    {
        Console.Error.WriteLine($"[avatar] data/site.json を読めません: {e.Message}");
        return null;
    }
}
