using FaraBlenderManual.Components;
using FaraBlenderManual.Data;

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
app.UseAntiforgery();

// 旧 URL からの移行
app.MapGet("/operations", () => Results.Redirect("/learn", permanent: true));
app.MapGet("/avatar-clothing", () => Results.Redirect("/learn/avatar-clothing", permanent: true));
app.MapGet("/plugins", () => Results.Redirect("/addons", permanent: true));

app.MapRazorComponents<App>();

app.Run();
