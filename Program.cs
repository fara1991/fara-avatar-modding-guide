using System.Text.Encodings.Web;
using System.Text.Json;
using FaraBlenderManual.Components;
using FaraBlenderManual.Data;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// 閲覧専用サイトなので静的 SSR のみ。対話的 UI が必要になったら AddInteractiveServerComponents() を足す。
builder.Services.AddRazorComponents();
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddSingleton<ContentService>();

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

app.UseHttpsRedirection();
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

app.Run();
