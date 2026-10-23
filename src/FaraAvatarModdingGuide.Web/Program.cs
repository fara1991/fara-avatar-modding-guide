using System.Net.Http.Json;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web;
using FaraAvatarModdingGuide.Web.Services;
using FaraSiteKit.Core;
using FaraSiteKit.Razor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var http = new HttpClient { BaseAddress = baseAddress };
builder.Services.AddSingleton(http);

// サイトの設定（サイト名・バージョン・フッターの SNS・確認用ホスト名）。Build が appsettings.json の Site から data/site.json に書いたもの。
// 共通部品（レイアウト・フッター・報告フォームなど）が読む SiteOptions に入れる。
var settings = await http.GetFromJsonAsync("data/site.json", SiteJsonContext.Default.SiteSettings)
               ?? throw new InvalidOperationException("data/site.json を読めません");
builder.Services.AddSingleton(settings);
builder.Services.AddFaraSiteKit(options => SiteInfo.Configure(options, settings));

// メンテナンスの状態（Worker の変数 MAINTENANCE。external/fara-site-kit/worker/maintenance.js の /api/maintenance）。読めなければ通常。
// 503（MAINTENANCE=true）は Worker が全リクエストに返すので、WebAssembly が動いているときは On にならない
builder.Services.AddSingleton(await MaintenanceInfo.LoadAsync(http));

// 記事・用語などのデータ（data/content.json）。最初の描画の前に読む（サイドバーの目次が最初から出るように）。読めないときは null のまま（各ページが案内を出す）
var store = new ContentStore(http);
await store.LoadAsync();
builder.Services.AddSingleton(store);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");   // 報告・404 の noindex と、ページの題名に必要

await builder.Build().RunAsync();
