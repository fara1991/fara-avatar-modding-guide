using System.Text.Encodings.Web;
using System.Text.Json;
using FaraBlenderManual.Components;
using FaraBlenderManual.Data;
using FaraBlenderManual.Publishing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// 閲覧専用サイトなので静的 SSR のみ。対話的 UI が必要になったら AddInteractiveServerComponents() を足す。
builder.Services.AddRazorComponents();
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddSingleton<ContentService>();

// dotnet run -- --export <dir> で静的サイトとして書き出す（Cloudflare へのデプロイ用）
var exportDir = builder.Configuration["export"];
var exporting = !string.IsNullOrEmpty(exportDir);
if (exporting)
{
    // 書き出し中は自分自身に HTTP でアクセスするので、空いているポートで HTTP のみ待ち受ける
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    // 開発中は Content/ を編集したら再起動なしで反映する
    app.Use(async (context, next) =>
    {
        context.RequestServices.GetRequiredService<ContentService>().Reload();
        await next(context);
    });
}

// 未定義の URL は空の 404 ではなく /not-found ページを 404 ステータスで返す
app.UseStatusCodePagesWithReExecute("/not-found");

if (!exporting)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

// 記事の画像は Markdown と同じ場所 (Content/learn/<category>/images/) に置けるようにする。
// 画像・動画だけを /learn/... として配信し、.md や .yml は配信しない。
var mediaTypes = new FileExtensionContentTypeProvider();
var mediaOnly = new FileExtensionContentTypeProvider(
    mediaTypes.Mappings
        .Where(m => m.Value.StartsWith("image/") || m.Value.StartsWith("video/"))
        .ToDictionary(m => m.Key, m => m.Value, StringComparer.OrdinalIgnoreCase));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Content", "learn")),
    RequestPath = "/learn",
    ContentTypeProvider = mediaOnly,
});

app.UseAntiforgery();

// 検索インデックス（クライアント側検索 wwwroot/js/search.js が読み込む）
var searchJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 日本語を \uXXXX にエスケープしない
};
app.MapGet("/search-index.json", (ContentService content) =>
    Results.Json(content.BuildSearchIndex(), searchJsonOptions));

// 旧 URL からの移行
app.MapGet("/operations", () => Results.Redirect("/learn", permanent: true));
app.MapGet("/avatar-clothing", () => Results.Redirect("/learn/avatar-clothing", permanent: true));
app.MapGet("/plugins", () => Results.Redirect("/addons", permanent: true));

app.MapRazorComponents<App>();

if (exporting)
{
    await app.StartAsync();
    await StaticSiteExporter.ExportAsync(app, exportDir!);
    await app.StopAsync();
    return;
}

app.Run();
