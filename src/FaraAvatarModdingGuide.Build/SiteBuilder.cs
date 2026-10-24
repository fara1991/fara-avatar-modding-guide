using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;
using Microsoft.AspNetCore.StaticFiles;

namespace FaraAvatarModdingGuide.Build;

/// <summary>Build コマンドの引数。</summary>
public sealed record BuildOptions(
    string RepoRoot,
    string TemplatePath,
    string OutDir,
    string Env,
    string? DevOrigin = null,
    string? KitDir = null,
    string Node = "node")
{
    public string KitPath => KitDir ?? Path.Combine(RepoRoot, "external", "fara-site-kit");
}

/// <summary>
/// 公開用の静的ファイル一式を作る（計画書 3.3）。出力先は dotnet publish の wwwroot で、そこに書き足す（既存のファイルは消さない）。
///   本文入りの静的 HTML（76 本）・404.html・robots.txt・sitemap.xml（本番）・_headers・_redirects … kit の build/build-pages.mjs が書く
///   data/content.json・data/search.json・data/site.json、search-index.json、画像・動画 … ここで書く
/// </summary>
public static class SiteBuilder
{
    /// <summary>テンプレート（index.html）の中の、WebAssembly を使うページにだけ要るもの（読み込み表示・Blazor のスクリプト）を囲む印。</summary>
    /// <summary>全ページの og:image にする画像（wwwroot/images/ の中）。4 サイト共通の名前 /og.png。版はファイル名でなく URL のクエリ（OgImageVersion）で管理する。</summary>
    public const string OgImageFile = "og.png";

    /// <summary>og:image / twitter:image の URL に付ける版（?v=N）。画像を作り直したら上げる。SNS・ブラウザーのキャッシュが古い画像を覚えているため。</summary>
    public const int OgImageVersion = 1;

    public const string WasmStart = "<!-- WASM-ONLY:START -->";
    public const string WasmEnd = "<!-- WASM-ONLY:END -->";

    /// <summary>その逆。WebAssembly を使わないページ（静的 HTML のまま）にだけ要るもの（「更新予定」の帯を出す notice.js）を囲む印。</summary>
    public const string StaticStart = "<!-- STATIC-ONLY:START -->";
    public const string StaticEnd = "<!-- STATIC-ONLY:END -->";

    /// <summary>旧 URL の転送（現行の StaticSiteExporter / Program.cs と同じ）。</summary>
    public static readonly IReadOnlyList<(string From, string To)> Redirects =
    [
        ("/operations", "/learn"),
        ("/avatar-clothing", "/learn/avatar-clothing"),
        ("/plugins", "/addons"),
    ];

    private static readonly JsonSerializerOptions DataJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 日本語を \uXXXX にエスケープしない
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Web が読む JSON と同じ型情報（ソース生成）で書く。書き方（日本語のエスケープなど）だけ DataJson に合わせる。</summary>
    private static readonly SiteJsonContext Json = new(DataJson);

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>記事フォルダから配信する画像・動画か（SSR 版の StaticSiteExporter.IsMedia と同じ判定）。</summary>
    public static bool IsMedia(string path) =>
        ContentTypes.TryGetContentType(path, out var type) &&
        (type.StartsWith("image/", StringComparison.Ordinal) || type.StartsWith("video/", StringComparison.Ordinal));

    public static void Run(BuildOptions options)
    {
        if (options.Env is not ("production" or "develop")) throw new ArgumentException("--env は production か develop を指定してください");
        if (!File.Exists(options.TemplatePath)) throw new FileNotFoundException($"テンプレート（dotnet publish の index.html）がありません: {options.TemplatePath}");
        var repo = Path.GetFullPath(options.RepoRoot);
        var outDir = Path.GetFullPath(options.OutDir);
        if (string.Equals(Path.TrimEndingDirectorySeparator(outDir), Path.TrimEndingDirectorySeparator(repo), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--out にリポジトリのルートは指定できません（専用の出力フォルダを指定してください）");
        // 出力先は、リポジトリのルート・その親・ソース（src / tests / external / Content）の中ではないこと（テンプレートやソースの上書き、親の下の圧縮ファイルの削除を防ぐ）
        var repoPrefix = Path.TrimEndingDirectorySeparator(repo) + Path.DirectorySeparatorChar;
        var outPrefix = Path.TrimEndingDirectorySeparator(outDir) + Path.DirectorySeparatorChar;
        if (repoPrefix.StartsWith(outPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--out にリポジトリのルートやその親は指定できません（専用の出力フォルダを指定してください）");
        foreach (var protectedDir in new[] { "src", "tests", "external", "Content", "worker", ".github" })
        {
            if (outPrefix.StartsWith(Path.Combine(repo, protectedDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"--out にソースのフォルダ（{protectedDir}）の中は指定できません: {outDir}");
        }
        Directory.CreateDirectory(outDir);

        var kitScript = Path.Combine(options.KitPath, "build", "build-pages.mjs");
        if (!File.Exists(kitScript)) throw new FileNotFoundException($"fara-site-kit がありません（git submodule update --init が必要）: {kitScript}");

        var contentService = new ContentService(Path.Combine(repo, "Content"));
        var content = SiteContentFactory.From(contentService);
        var siteJson = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, "tools", "site.json")))!.AsObject();
        var devOrigin = options.DevOrigin ?? siteJson["devOrigin"]!.GetValue<string>();
        AddImportMapHashes(siteJson, File.ReadAllText(options.TemplatePath));
        var settings = SiteSettingsLoader.Load(Path.Combine(repo, "appsettings.json"), new Uri(devOrigin).Host);

        // --- データ（WebAssembly が起動時に読む）--------------------------------
        WriteJson(Path.Combine(outDir, "data", "content.json"), content, Json.SiteContent);
        WriteJson(Path.Combine(outDir, "data", "site.json"), settings, Json.SiteSettings);
        WriteJson(Path.Combine(outDir, "data", "filter.json"), PageCatalog.FilterTexts(content, settings).ToDictionary(kv => kv.Key, kv => kv.Value), Json.DictionaryStringDictionaryStringString);
        var searchEntries = contentService.BuildSearchIndex();
        WriteJson(Path.Combine(outDir, "data", "search.json"), SearchIndexJson.From(searchEntries), Json.IReadOnlyListSearchRecord);
        // 現行と同じ URL・同じ形（/search-index.json）。外部からの参照を壊さない
        WriteJson(Path.Combine(outDir, "search-index.json"), searchEntries.Select(e => new LegacySearchEntry(e.Type, e.Title, e.Subtitle, e.Url, e.Text, e.Reading)).ToList(), Json.IReadOnlyListLegacySearchEntry);

        // --- 画像・動画 -----------------------------------------------------
        CopyDirectory(Path.Combine(repo, "wwwroot", "images"), Path.Combine(outDir, "images"), _ => true);
        var ogSource = Path.Combine(repo, "wwwroot", OgImageFile);
        if (File.Exists(ogSource)) File.Copy(ogSource, Path.Combine(outDir, OgImageFile), overwrite: true);
        CopyDirectory(Path.Combine(repo, "Content", "learn"), Path.Combine(outDir, "learn"), IsMedia);

        // --- 静的 HTML・robots・sitemap・_headers・_redirects（kit）------------------
        var pages = PageCatalog.Build(content, settings);
        // OGP 画像（1200×630 の PNG）。全ページの og:image と twitter:card=summary_large_image になる。作り直したら OgImageVersion を上げる（/og.png?v=2…）。SNS 側が古い画像を覚えているので、URL の ?v= を変える。大きさ・説明は index.html の og:image:width / height / alt
        var ogImage = File.Exists(Path.Combine(outDir, OgImageFile)) ? $"/{OgImageFile}?v={OgImageVersion}" : null;
        // アプリ内の移動のあとに、head（description・canonical・OGP・robots）を移った先のページに合わせるための表（js/head-sync.js が読む）
        WritePageMeta(Path.Combine(outDir, "data", "pages.json"), pages);
        var kitPages = new JsonArray(pages.Select(p =>
        {
            var page = new JsonObject
            {
                ["path"] = p.Path,
                ["title"] = p.Title,
                ["description"] = p.Description,
                ["bodyHtml"] = PageCatalog.Wrap(p.Main, settings, content, p.Path),
            };
            if (p.NoIndex) page["noindex"] = true;
            if (p.OgType != PageCatalog.OgWebsite) page["ogType"] = p.OgType;
            if (ogImage is not null) page["ogImage"] = ogImage;
            return (JsonNode)page;
        }).ToArray());
        var kitRedirects = new JsonArray(Redirects.Select(r => (JsonNode)new JsonObject { ["from"] = r.From, ["to"] = r.To, ["status"] = 301 }).ToArray());
        siteJson["devOrigin"] = devOrigin;
        siteJson["notFoundPage"] = new JsonObject
        {
            ["title"] = PageCatalog.NotFoundTitle(settings),
            ["bodyHtml"] = PageCatalog.Wrap(PageBodies.NotFound(), settings, content),
        };

        var work = Path.Combine(repo, "obj", "build-pages", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        var templateCopy = Path.Combine(work, "index.html");   // 出力先の index.html は kit が上書きするので、テンプレートは先に別の場所へ写す
        File.Copy(options.TemplatePath, templateCopy, overwrite: true);
        var siteFile = Path.Combine(work, "site.json");
        var pagesFile = Path.Combine(work, "pages.json");
        File.WriteAllText(siteFile, siteJson.ToJsonString(DataJson), new UTF8Encoding(false));
        File.WriteAllText(pagesFile, new JsonObject { ["pages"] = kitPages, ["redirects"] = kitRedirects }.ToJsonString(DataJson), new UTF8Encoding(false));
        RunKit(options, kitScript, siteFile, pagesFile, templateCopy, outDir);

        DeletePrecompressed(outDir);

        // --- WebAssembly を使わないページから、読み込み表示と Blazor のスクリプトを外す -------
        foreach (var page in pages) StripWasm(outDir, page);
        StripWasmFile(Path.Combine(outDir, "404.html"), wasm: true);
    }

    /// <summary>data/pages.json: パス → { title, description, ogType, noindex }。head に書く値と同じ（kit の head と同じ文字列。js/head-sync.js が使う）。</summary>
    private static void WritePageMeta(string path, IReadOnlyList<PagePlan> pages)
    {
        var map = new JsonObject();
        foreach (var p in pages)
        {
            var entry = new JsonObject { ["title"] = p.Title, ["description"] = p.Description, ["ogType"] = p.OgType };
            if (p.NoIndex) entry["noindex"] = true;
            map[p.Path] = entry;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, map.ToJsonString(DataJson), new UTF8Encoding(false));
    }

    /// <summary>
    /// テンプレートの importmap（&lt;script type="importmap"&gt;）の中身の sha256（CSP の script-src に入れる形: 'sha256-…'）。
    /// ハッシュは、タグの間の文字を、そのまま（空白・改行も含めて）UTF-8 で計算する。コメントの中の記述は数えない。
    /// importmap は外部ファイルにできない（src が使えない）ので、CSP では 'unsafe-inline' ではなく、このハッシュで許可する（計画書 4.9）。
    /// </summary>
    public static IReadOnlyList<string> ImportMapHashes(string html)
    {
        var withoutComments = Regex.Replace(html, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        return Regex.Matches(withoutComments, @"<script\s+type=""importmap""\s*>(?<body>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Select(m => "'sha256-" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(m.Groups["body"].Value))) + "'")
            .Distinct()
            .ToList();
    }

    /// <summary>サイト設定の csp.scriptSrc に、テンプレートの importmap のハッシュを足す（csp が無い設定では何もしない）。</summary>
    private static void AddImportMapHashes(JsonObject siteJson, string templateHtml)
    {
        if (siteJson["csp"] is not JsonObject csp) return;
        var scriptSrc = csp["scriptSrc"] as JsonArray ?? new JsonArray();
        foreach (var hash in ImportMapHashes(templateHtml))
        {
            if (!scriptSrc.Any(n => n?.GetValue<string>() == hash)) scriptSrc.Add(hash);
        }
        csp["scriptSrc"] = scriptSrc;
    }

    /// <summary>kit が書いたページのファイル名（urlStyle=html: /x/y → x/y.html、/ → index.html）。</summary>
    public static string FileFor(string path) => path == "/" ? "index.html" : path.TrimStart('/') + ".html";

    private static void StripWasm(string outDir, PagePlan page) => StripWasmFile(Path.Combine(outDir, FileFor(page.Path)), wasm: page.Wasm);

    /// <summary>WebAssembly を使うページ（wasm）は WASM-ONLY を残し、STATIC-ONLY を外す。使わないページは逆。</summary>
    private static void StripWasmFile(string file, bool wasm)
    {
        var html = File.ReadAllText(file);
        var updated = RemoveStaticOnly(RemoveWasmOnly(html, keep: wasm), keep: !wasm);
        if (updated != html) File.WriteAllText(file, updated, new UTF8Encoding(false));
    }

    /// <summary>
    /// 印（WASM-ONLY）で囲んだ部分を、WebAssembly を使うページ（keep）では印だけ外し、使わないページでは中身ごと外す。
    /// 印の対応が崩れていたら例外にする（テンプレートの書き間違いに気づくため）。
    /// </summary>
    public static string RemoveWasmOnly(string html, bool keep) => RemoveMarked(html, WasmStart, WasmEnd, keep);

    /// <summary>印（STATIC-ONLY）で囲んだ部分。静的 HTML のままのページ（keep）では印だけ外し、WebAssembly を使うページでは中身ごと外す。</summary>
    public static string RemoveStaticOnly(string html, bool keep) => RemoveMarked(html, StaticStart, StaticEnd, keep);

    private static string RemoveMarked(string html, string start, string end, bool keep)
    {
        var starts = Regex.Matches(html, Regex.Escape(start)).Count;
        var ends = Regex.Matches(html, Regex.Escape(end)).Count;
        if (starts != ends) throw new InvalidDataException($"{start} と {end} の数が違います（{starts} / {ends}）");
        if (starts == 0) return html;
        var pattern = new Regex(Regex.Escape(start) + @"(.*?)" + Regex.Escape(end) + @"[ \t]*\r?\n?", RegexOptions.Singleline);
        return keep
            ? pattern.Replace(html, m => m.Groups[1].Value.TrimStart('\r', '\n'))
            : pattern.Replace(html, string.Empty);
    }

    private static void RunKit(BuildOptions options, string script, string site, string pages, string template, string outDir)
    {
        var info = new ProcessStartInfo(options.Node)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[] { script, "--site", site, "--pages", pages, "--template", template, "--out", outDir, "--env", options.Env })
        {
            info.ArgumentList.Add(arg);
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{options.Node} を起動できません");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"fara-site-kit の build-pages.mjs が失敗しました（終了コード {process.ExitCode}）:\n{stderr.Result}{stdout.Result}");
        Console.WriteLine(stdout.Result.Trim());
    }

    /// <summary>
    /// dotnet publish が作った事前圧縮のファイル（x.br / x.gz。元の x があるものだけ）を消す。Cloudflare は配信時に圧縮するので使われず、
    /// Build が書き換えた index.html などの古い圧縮ファイルが残ると紛らわしいため。
    /// </summary>
    public static int DeletePrecompressed(string outDir)
    {
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories).ToList())
        {
            var ext = Path.GetExtension(file);
            if (ext is not (".br" or ".gz")) continue;
            if (!File.Exists(file[..^ext.Length])) continue;
            File.Delete(file);
            deleted++;
        }
        return deleted;
    }

    private static void WriteJson<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, typeInfo), new UTF8Encoding(false));
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
