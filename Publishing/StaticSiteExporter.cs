using FaraAvatarGuide.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;

namespace FaraAvatarGuide.Publishing;

/// <summary>
/// 起動中のアプリから全ページを取得し、静的サイト（HTML + 静的ファイル）として書き出す。
/// Cloudflare Workers (Static Assets) にそのままデプロイできる形にする。
/// 使い方: dotnet run -- --export dist
/// </summary>
public static class StaticSiteExporter
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>記事フォルダから配信する画像・動画か（Program.cs の /learn の配信と同じ判定）。</summary>
    public static bool IsMedia(string path) =>
        ContentTypes.TryGetContentType(path, out var type) &&
        (type.StartsWith("image/", StringComparison.Ordinal) || type.StartsWith("video/", StringComparison.Ordinal));

    public static async Task ExportAsync(WebApplication app, string outputDir)
    {
        var logger = app.Logger;
        var content = app.Services.GetRequiredService<ContentService>();
        var baseAddress = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

        outputDir = Path.GetFullPath(outputDir);
        // 出力先は丸ごと消すので、プロジェクトのフォルダやその親（--export . など）は受け付けない
        var projectDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(app.Environment.ContentRootPath));
        var outputTrimmed = Path.TrimEndingDirectorySeparator(outputDir);
        if (string.Equals(projectDir, outputTrimmed, StringComparison.OrdinalIgnoreCase) ||
            projectDir.StartsWith(outputTrimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"--export には専用の出力フォルダ（例: dist）を指定してください: {outputDir}");
        }
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        // --- ページ ---------------------------------------------------------
        // Workers の html_handling = "auto-trailing-slash" に合わせ、/learn/basics は learn/basics.html に書く。
        var pages = new Dictionary<string, string>
        {
            ["/"] = "index.html",
            ["/learn"] = "learn.html",
            ["/shortcuts"] = "shortcuts.html",
            ["/addons"] = "addons.html",
            ["/practice"] = "practice.html",
            ["/practice/room"] = "practice/room.html",
            ["/practice/unity"] = "practice/unity.html",
            ["/glossary"] = "glossary.html",
            ["/howto"] = "howto.html",
            ["/tips"] = "tips.html",
            ["/qa"] = "qa.html",
            ["/links"] = "links.html",
            ["/search"] = "search.html",
            ["/search-index.json"] = "search-index.json",
        };
        foreach (var category in content.Categories)
        {
            pages[$"/learn/{category.Id}"] = $"learn/{category.Id}.html";
            foreach (var article in content.GetArticles(category.Id))
            {
                pages[article.Url] = article.Url.TrimStart('/') + ".html";
            }
        }

        foreach (var (path, file) in pages)
        {
            using var response = await http.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"{path} returned {(int)response.StatusCode}");
            }
            await WriteAsync(outputDir, file, await response.Content.ReadAsByteArrayAsync());
        }

        // 404 ページ（Workers の not_found_handling = "404-page" が /404.html を返す）
        using (var notFound = await http.GetAsync("/__static_export_404__"))
        {
            if (notFound.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Expected 404 for the not-found page, got {(int)notFound.StatusCode}");
            }
            await WriteAsync(outputDir, "404.html", await notFound.Content.ReadAsByteArrayAsync());
        }

        // 旧 URL のリダイレクト（Workers Static Assets の _redirects）
        await WriteAsync(outputDir, "_redirects", """
            /operations /learn 301
            /avatar-clothing /learn/avatar-clothing 301
            /plugins /addons 301

            """u8.ToArray());

        // --- 静的ファイル ---------------------------------------------------
        CopyDirectory(app.Environment.WebRootPath, outputDir, _ => true);

        // 記事の画像・動画（Content/learn/<category>/images/...）→ /learn/<category>/images/...
        var learnDir = Path.Combine(app.Environment.ContentRootPath, "Content", "learn");
        CopyDirectory(learnDir, Path.Combine(outputDir, "learn"),
            IsMedia);

        var fileCount = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories).Count();
        logger.LogInformation("Exported {Pages} pages, {Files} files in total to {Dir}", pages.Count + 1, fileCount, outputDir);
    }

    private static async Task WriteAsync(string outputDir, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(outputDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static void CopyDirectory(string source, string target, Func<string, bool> include)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (!include(file)) continue;
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
