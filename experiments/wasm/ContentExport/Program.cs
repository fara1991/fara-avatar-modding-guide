using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

// 使い方: dotnet run --project ContentExport -- <本体のリポジトリ（Content/ がある）> <出力先 wwwroot/data>
if (args.Length != 2)
{
    Console.Error.WriteLine("usage: ContentExport <repo-root> <out-dir>");
    return 2;
}

var repoRoot = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outDir);

var content = new ContentService(new FakeEnvironment(repoRoot), NullLogger<ContentService>.Instance);
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

// 記事の本文 HTML の画像（相対パス）は、WebAssembly 版の URL（/wasm/...）では解決できない。
// 本体の配信（/learn/<カテゴリ>/images/...）と同じ場所を指すように、絶対パスに直す。
var relativeSrc = new Regex("src=\"(?!/|https?:|data:|#)([^\"]+)\"", RegexOptions.Compiled);

var model = new
{
    sections = content.Sections,
    categories = content.Categories,
    articles = content.Articles.Select(a => new
    {
        a.Category,
        a.Slug,
        a.Order,
        a.Title,
        a.Description,
        a.Difficulty,
        a.BlenderVersion,
        Updated = a.Updated?.ToString("yyyy-MM-dd"),
        a.Shortcuts,
        Html = relativeSrc.Replace(a.Html, m => $"src=\"/learn/{a.Category}/{m.Groups[1].Value}\""),
        a.PlainText,
        a.PracticeScenarios,
        a.Url,
    }).ToList(),
};

await File.WriteAllTextAsync(Path.Combine(outDir, "content.json"), JsonSerializer.Serialize(model, options));
// 検索用の正規化（NFKC・小文字・〜・カタカナ→ひらがな）は、ここ（.NET 9 のビルド時）で済ませる。
// ブラウザ上の WebAssembly では string.Normalize が使えない（PlatformNotSupportedException）ため。
var search = content.BuildSearchIndex().Select(e => new
{
    e.Type,
    e.Title,
    e.Subtitle,
    e.Url,
    e.Text,
    Norm = SearchNormalize($"{e.Title} {e.Subtitle} {e.Text}"),
}).ToList();
await File.WriteAllTextAsync(Path.Combine(outDir, "search.json"), JsonSerializer.Serialize(search, options));
Console.WriteLine($"articles: {content.Articles.Count}, search entries: {search.Count}");
return 0;

static string SearchNormalize(string s)
{
    var text = s.Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant().Replace('~', '〜');
    return new string(text.Select(c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c).ToArray());
}

sealed class FakeEnvironment(string root) : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "ContentExport";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = root;
    public string EnvironmentName { get; set; } = "Production";
}
