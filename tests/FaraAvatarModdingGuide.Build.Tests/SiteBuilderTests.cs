using FaraAvatarModdingGuide.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Build;
using Xunit;

namespace FaraAvatarModdingGuide.Build.Tests;

/// <summary>
/// Build の通しの検証（kit の build-pages.mjs を実際に呼ぶ）。本番用・確認用の 2 回分の出力を共有する。
/// 計画書 8 の完了条件のうち、静的 HTML が JS 無しで本文を読めること・旧 URL・noindex・CSP・メタ情報を確かめる。
/// </summary>
public class SiteBuilderTests(BuildFixture fx) : IClassFixture<BuildFixture>
{
    private string Read(string dir, string relative) => File.ReadAllText(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));

    [NodeFact]
    public void Every_page_is_written_with_its_body()
    {
        Assert.True(fx.Available);
        var content = fx.Content;
        var expected = new List<string> { "index.html", "learn.html", "404.html" };
        foreach (var article in content.Articles)
        {
            var file = article.Url.TrimStart('/') + ".html";
            expected.Add(file);
            var html = Read(fx.Production, file);
            Assert.Contains(article.Html, html);                        // 記事の本文（Markdig の出力）がそのまま入っている
            Assert.Contains($"<h1>{System.Net.WebUtility.HtmlEncode(article.Title)}</h1>", html);
            Assert.Contains($"<link rel=\"canonical\" href=\"https://avatar.fara-labs.com{article.Url}\"", html);
        }
        foreach (var category in content.Categories) expected.Add($"learn/{category.Id}.html");
        foreach (var path in new[] { "shortcuts", "addons", "practice", "practice/room", "practice/unity", "glossary", "howto", "tips", "qa", "links", "changelog", "report", "privacy", "search" })
            expected.Add(path + ".html");

        foreach (var file in expected) Assert.True(File.Exists(Path.Combine(fx.Production, file)), file + " がありません");
        // 静的 HTML は、固定ページ 16 + カテゴリ 10 + 記事 50 = 76 本と 404.html
        Assert.Equal(77, Directory.EnumerateFiles(fx.Production, "*.html", SearchOption.AllDirectories).Count(f => !f.Contains(Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar)));
    }

    [NodeFact]
    public void Only_wasm_pages_keep_the_loader_and_the_blazor_script()
    {
        // フェーズ 3 で、練習の 3 ページも WebAssembly になった。今は、全 76 ページと 404.html が、読み込み表示と Blazor のスクリプトを持つ
        foreach (var path in PageCatalog.WasmPaths.Concat(new[] { "/learn/basics", "/learn/basics/select" }))
        {
            var html = Read(fx.Production, SiteBuilder.FileFor(path));
            Assert.Contains("id=\"boot-indicator\"", html);
            Assert.Contains("_framework/blazor.webassembly.js", html);
            Assert.DoesNotContain("WASM-ONLY", html);                    // 印は残さない
            Assert.DoesNotContain("STATIC-ONLY", html);
            Assert.DoesNotContain("notice.js", html);                    // 帯は kit の MaintenanceBanner が出す（二重にしない）
        }
        foreach (var file in new[] { "practice.html", "practice/room.html", "practice/unity.html" })
        {
            var html = Read(fx.Production, file);
            Assert.Contains("id=\"boot-indicator\"", html);
            Assert.Contains("_framework/blazor.webassembly.js", html);
            Assert.DoesNotContain("notice.js", html);
        }
        // 404.html は WebAssembly が起動して、NotFoundView を出す
        Assert.Contains("blazor.webassembly.js", Read(fx.Production, "404.html"));
        Assert.DoesNotContain("notice.js", Read(fx.Production, "404.html"));
    }

    [NodeFact]
    public void Redirects_headers_and_robots_are_written_with_a_trailing_newline()
    {
        var redirects = Read(fx.Production, "_redirects");
        Assert.Equal("/operations /learn 301\n/avatar-clothing /learn/avatar-clothing 301\n/plugins /addons 301\n", redirects);

        var headers = Read(fx.Production, "_headers");
        Assert.EndsWith("\n", headers);
        var csp = headers.Split('\n').Single(l => l.TrimStart().StartsWith("Content-Security-Policy:", StringComparison.Ordinal));
        Assert.Matches("script-src 'self' 'wasm-unsafe-eval' https://challenges.cloudflare.com 'sha256-[A-Za-z0-9+/]{43}=';", csp);   // Turnstile と、importmap のハッシュ(Build が計算)
        Assert.DoesNotContain("'unsafe-eval'", csp.Replace("'wasm-unsafe-eval'", ""));
        var script = Regex.Match(csp, "script-src [^;]*;").Value;
        Assert.DoesNotContain("unsafe-inline", script);                  // script に unsafe-inline は付けない
        Assert.Contains("frame-src https://challenges.cloudflare.com", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("X-Robots-Tag", headers);                  // 本番には noindex を付けない

        var robots = Read(fx.Production, "robots.txt");
        Assert.Contains("Sitemap: https://avatar.fara-labs.com/sitemap.xml", robots);
    }

    [NodeFact]
    public void Production_has_a_sitemap_without_noindex_pages_and_develop_does_not()
    {
        var sitemap = Read(fx.Production, "sitemap.xml");
        Assert.Contains("<loc>https://avatar.fara-labs.com/learn</loc>", sitemap);
        Assert.Contains("<loc>https://avatar.fara-labs.com/privacy</loc>", sitemap);
        Assert.DoesNotContain("/report<", sitemap);                      // 問い合わせは noindex
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", Read(fx.Production, "report.html"));
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", Read(fx.Production, "404.html"));
        Assert.False(File.Exists(Path.Combine(fx.Develop, "sitemap.xml")));
    }

    [NodeFact]
    public void Develop_uses_the_dev_origin_and_marks_everything_noindex()
    {
        var home = Read(fx.Develop, "index.html");
        Assert.Contains("<link rel=\"canonical\" href=\"https://avatar-preview.fara-labs.com/\"", home);
        Assert.Contains("X-Robots-Tag: noindex", Read(fx.Develop, "_headers"));
        var site = JsonDocument.Parse(Read(fx.Develop, "data/site.json")).RootElement;
        Assert.Equal("avatar-preview.fara-labs.com", site.GetProperty("developHost").GetString());   // フッターの作者リンクを確認用に切り替える基準
    }

    [NodeFact]
    public void Home_page_has_title_description_and_open_graph_tags()
    {
        var home = Read(fx.Production, "index.html");
        Assert.Contains("<title>ホーム - Avatar Modding Guide</title>", home);
        Assert.Contains("<meta name=\"description\"", home);
        Assert.Contains("<meta property=\"og:title\"", home);
        Assert.Contains("<meta property=\"og:url\" content=\"https://avatar.fara-labs.com/\"", home);
        Assert.DoesNotContain("og:image", home);                         // OGP 画像は未承認のため、置かれるまで付けない
        Assert.Contains("avatar.fara-labs.com", Read(fx.Production, "learn/basics/select.html"));
    }

    [NodeFact]
    public void Data_files_are_valid_json_with_all_articles_and_a_normalised_search_index()
    {
        var content = JsonDocument.Parse(Read(fx.Production, "data/content.json")).RootElement;
        Assert.Equal(50, content.GetProperty("articles").GetArrayLength());
        Assert.Equal(10, content.GetProperty("categories").GetArrayLength());
        var first = content.GetProperty("articles")[0];
        Assert.StartsWith("/learn/", first.GetProperty("url").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("html").GetString()));
        Assert.DoesNotContain("bi-", Read(fx.Production, "data/content.json"));   // Bootstrap Icons の名前は Material に写してある

        var search = JsonDocument.Parse(Read(fx.Production, "data/search.json")).RootElement;
        Assert.True(search.GetArrayLength() > 100);
        foreach (var entry in search.EnumerateArray())
        {
            Assert.Equal(SearchIndexJson.Normalize(entry.GetProperty("title").GetString()), entry.GetProperty("titleNorm").GetString());
        }
        // 現行の URL（/search-index.json）は同じ件数のまま残す
        Assert.Equal(search.GetArrayLength(), JsonDocument.Parse(Read(fx.Production, "search-index.json")).RootElement.GetArrayLength());
    }

    [NodeFact]
    public void Search_index_normalisation_is_identical_to_searchcore_js()
    {
        // 検索の読み（ローマ字 → ひらがな）の判定は searchcore.js のまま。索引の正規化済みの文字列が JS の norm() と一致することを、実際の索引で確かめる
        var info = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(Path.Combine(Repo.Root, "tests", "js", "search-norm.mjs"));
        info.ArgumentList.Add(Path.Combine(fx.Production, "data", "search.json"));
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output);
    }

    [NodeFact]
    public void Images_and_videos_are_copied_like_the_ssr_export()
    {
        var learnImages = Directory.EnumerateFiles(Path.Combine(Repo.Root, "Content", "learn"), "*", SearchOption.AllDirectories).Where(SiteBuilder.IsMedia).ToList();
        Assert.Equal(51, learnImages.Count);                              // 計画書 1.1: 記事の画像は 51 枚
        foreach (var image in learnImages)
        {
            var target = Path.Combine(fx.Production, "learn", Path.GetRelativePath(Path.Combine(Repo.Root, "Content", "learn"), image));
            Assert.True(File.Exists(target), target);
        }
        Assert.False(Directory.EnumerateFiles(Path.Combine(fx.Production, "learn"), "*.md", SearchOption.AllDirectories).Any());   // .md は配信しない
        Assert.True(File.Exists(Path.Combine(fx.Production, "images", "site-icon.svg")));
        Assert.True(Directory.EnumerateFiles(Path.Combine(fx.Production, "images", "tutorials"), "*.webp", SearchOption.AllDirectories).Count() >= 145);
    }

    [NodeFact]
    public void Static_pages_have_no_inline_scripts_and_no_bootstrap_cdn()
    {
        foreach (var file in Directory.EnumerateFiles(fx.Production, "*.html", SearchOption.AllDirectories))
        {
            var html = File.ReadAllText(file);
            Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);   // CDN の参照も class 名も無い（CSS は自前の base.css）
            Assert.DoesNotContain("class=\"bi ", html);
            // インラインの script は CSP で止まるので置かない（JSON-LD など type が実行されないものは対象外）
            foreach (Match m in Regex.Matches(html, "<script(?<attrs>[^>]*)>(?<body>.*?)</script>", RegexOptions.Singleline))
            {
                var attrs = m.Groups["attrs"].Value;
                if (attrs.Contains("application/ld+json")) continue;
                if (attrs.Contains("type=\"importmap\"")) continue;   // importmap だけは外部ファイルにできない。CSP はハッシュで許可する(別のテストで確かめる)
                Assert.Contains("src=", attrs);
            }
        }
    }

    [Fact]
    public void RemoveWasmOnly_removes_or_unwraps_the_marked_blocks_and_rejects_unbalanced_marks()
    {
        const string template = "a\n<!-- WASM-ONLY:START -->\n<x/>\n<!-- WASM-ONLY:END -->\nb\n";
        Assert.Equal("a\nb\n", SiteBuilder.RemoveWasmOnly(template, keep: false));
        Assert.Equal("a\n<x/>\nb\n", SiteBuilder.RemoveWasmOnly(template, keep: true));
        Assert.Equal("a", SiteBuilder.RemoveWasmOnly("a", keep: false));
        Assert.Throws<InvalidDataException>(() => SiteBuilder.RemoveWasmOnly("<!-- WASM-ONLY:START -->", keep: false));
    }

    [Fact]
    public void RemoveStaticOnly_is_the_inverse_of_RemoveWasmOnly()
    {
        const string template = "a\n<!-- STATIC-ONLY:START -->\n<s/>\n<!-- STATIC-ONLY:END -->\nb\n";
        Assert.Equal("a\nb\n", SiteBuilder.RemoveStaticOnly(template, keep: false));
        Assert.Equal("a\n<s/>\nb\n", SiteBuilder.RemoveStaticOnly(template, keep: true));
        Assert.Throws<InvalidDataException>(() => SiteBuilder.RemoveStaticOnly("<!-- STATIC-ONLY:END -->", keep: true));
        // 2 種類の印が混ざっていても、それぞれ独立に外せる
        const string both = "<!-- WASM-ONLY:START -->\nw\n<!-- WASM-ONLY:END -->\n<!-- STATIC-ONLY:START -->\ns\n<!-- STATIC-ONLY:END -->\n";
        Assert.Equal("s\n", SiteBuilder.RemoveStaticOnly(SiteBuilder.RemoveWasmOnly(both, keep: false), keep: true));
        Assert.Equal("w\n", SiteBuilder.RemoveStaticOnly(SiteBuilder.RemoveWasmOnly(both, keep: true), keep: false));
    }

    [Fact]
    public void Run_refuses_output_folders_that_would_overwrite_sources_or_the_parent_tree()
    {
        var template = Path.Combine(Path.GetTempPath(), "avatar-out-guard-" + Guid.NewGuid().ToString("N")[..8] + ".html");
        File.WriteAllText(template, TestTemplate.Html);
        try
        {
            foreach (var bad in new[] { Repo.Root, Path.GetDirectoryName(Repo.Root)!, Path.Combine(Repo.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot"), Path.Combine(Repo.Root, "Content") })
                Assert.Throws<ArgumentException>(() => SiteBuilder.Run(new BuildOptions(Repo.Root, template, bad, "production")));
        }
        finally { File.Delete(template); }
    }

    [Fact]
    public void FileFor_follows_the_html_url_style()
    {
        Assert.Equal("index.html", SiteBuilder.FileFor("/"));
        Assert.Equal("learn/basics/select.html", SiteBuilder.FileFor("/learn/basics/select"));
    }

    // ---- importmap(three.js の読み込み元)と CSP ------------------------------------------------

    private static string Sha256(string text) => "'sha256-" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))) + "'";

    [Fact]
    public void Import_map_hash_is_taken_from_the_exact_text_between_the_tags_and_ignores_comments()
    {
        const string map = "{\"imports\":{\"three\":\"/lib/three/three.module.js\"}}";
        Assert.Equal([Sha256(map)], SiteBuilder.ImportMapHashes($"<head><script type=\"importmap\">{map}</script></head>"));
        // 空白・改行も数える（ブラウザは、タグの間の文字をそのまま CSP のハッシュに使う）
        Assert.Equal([Sha256($"\n{map}\n")], SiteBuilder.ImportMapHashes($"<script type=\"importmap\">\n{map}\n</script>"));
        Assert.NotEqual(SiteBuilder.ImportMapHashes($"<script type=\"importmap\">{map}</script>"), SiteBuilder.ImportMapHashes($"<script type=\"importmap\"> {map}</script>"));
        // コメントの中の記述と、importmap ではない script は数えない。同じ中身の重複は 1 つ
        Assert.Empty(SiteBuilder.ImportMapHashes($"<!-- <script type=\"importmap\">{map}</script> --><script src=\"a.js\"></script><script>var x = 1;</script>"));
        Assert.Single(SiteBuilder.ImportMapHashes($"<script type=\"importmap\">{map}</script><script type=\"importmap\">{map}</script>"));
    }

    [NodeFact]
    public void Csp_allows_the_import_map_by_hash_only_and_no_cdn_for_three()
    {
        var map = Regex.Match(Read(fx.Production, "practice.html"), "<script type=\"importmap\">(?<body>.*?)</script>", RegexOptions.Singleline).Groups["body"].Value;
        Assert.Contains("/lib/three/three.module.js", map);
        var csp = Read(fx.Production, "_headers").Split('\n').Single(l => l.TrimStart().StartsWith("Content-Security-Policy:", StringComparison.Ordinal));
        var script = Regex.Match(csp, "script-src [^;]*;").Value;
        Assert.Contains(Sha256(map), script);                           // ページに実際に出た importmap の中身のハッシュ
        Assert.DoesNotContain("jsdelivr", csp);                         // three.js は同梱(自己ホスト)。CDN は許可しない
        Assert.DoesNotContain("unsafe-inline", script);
        Assert.Equal(1, Regex.Matches(script, "'sha256-").Count);       // 余計なハッシュは許可しない
    }

    [NodeFact]
    public void Real_template_has_the_import_map_in_every_page_and_its_hash_is_in_the_headers()
    {
        var headers = Read(fx.Real, "_headers");
        var csp = headers.Split('\n').Single(l => l.TrimStart().StartsWith("Content-Security-Policy:", StringComparison.Ordinal));
        var script = Regex.Match(csp, "script-src [^;]*;").Value;
        var hashes = Regex.Matches(script, "'sha256-[A-Za-z0-9+/]{43}='").Select(m => m.Value).ToList();
        Assert.Single(hashes);
        var pages = Directory.EnumerateFiles(fx.Real, "*.html", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar)).ToList();
        Assert.Equal(77, pages.Count);
        foreach (var file in pages)
        {
            var html = File.ReadAllText(file);
            var maps = Regex.Matches(html, "<script type=\"importmap\">(?<body>.*?)</script>", RegexOptions.Singleline).Select(m => m.Groups["body"].Value).ToList();
            Assert.Single(maps);                                         // 1 ページに importmap は 1 つ(複数あると、後のものは無視される)
            Assert.Equal(hashes[0], Sha256(maps[0]));                    // kit と Build の書き換えを通っても、中身が変わっていない
            // importmap は、最初の module が読まれる前(blazor.webassembly.js・練習の module より前)にある
            Assert.True(html.IndexOf("type=\"importmap\"", StringComparison.Ordinal) < html.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal), file);
        }
        Assert.DoesNotContain("jsdelivr", headers);
    }
}
