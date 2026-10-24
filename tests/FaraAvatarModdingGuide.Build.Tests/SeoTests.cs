using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Build;
using Xunit;

namespace FaraAvatarModdingGuide.Build.Tests;

/// <summary>
/// SEO の最終形（計画書 5 の Phase 4・13）: sitemap.xml・robots.txt・全 76 ページの head（title・description・canonical・OGP・noindex）。
/// kit の build-pages.mjs を実際に通した出力（本番用・確認用・OGP 画像あり）を検査する。
/// </summary>
public class SeoTests(BuildFixture fx) : IClassFixture<BuildFixture>
{
    private const string Origin = "https://avatar.fara-labs.com";
    private const string DevOrigin = "https://avatar-preview.fara-labs.com";

    private static readonly IReadOnlyList<PagePlan> Pages = PageCatalog.Build(
        SiteContentFactory.From(new FaraAvatarModdingGuide.Data.ContentService(Path.Combine(Repo.Root, "Content"))),
        SiteSettingsLoader.Load(Path.Combine(Repo.Root, "appsettings.json"), "avatar-dev.fara-labs.com"));

    private static string Read(string dir, string relative) => File.ReadAllText(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>head の中（&lt;/head&gt; まで）。</summary>
    private static string Head(string dir, string path)
    {
        var html = Read(dir, SiteBuilder.FileFor(path));
        return html[..html.IndexOf("</head>", StringComparison.Ordinal)];
    }

    private static int Count(string head, string pattern) => Regex.Matches(head, pattern).Count;

    private static string Meta(string head, string pattern) => Regex.Match(head, pattern).Groups[1].Value;

    private static readonly string[] OldUrls = ["/operations", "/avatar-clothing", "/plugins"];

    [NodeFact]
    public void Sitemap_lists_exactly_the_74_indexable_pages_on_the_production_origin()
    {
        var locs = Regex.Matches(Read(fx.Production, "sitemap.xml"), "<loc>([^<]+)</loc>").Select(m => m.Groups[1].Value).ToList();
        var expected = Pages.Where(p => !p.NoIndex).Select(p => Origin + p.Path).Order().ToList();
        Assert.Equal(74, expected.Count);                                // 固定 14（問い合わせ・検索を除く）+ カテゴリ 10 + 記事 50
        Assert.Equal(expected, locs.Order().ToList());
        Assert.Equal(locs.Count, locs.Distinct().Count());
        Assert.All(locs, loc => Assert.DoesNotContain("?", loc));
        // 練習の 3 ページとプライバシーは含める。問い合わせ・検索・旧 URL・404・検索索引は含めない
        foreach (var path in new[] { "/practice", "/practice/unity", "/practice/room", "/privacy", "/learn" }) Assert.Contains(Origin + path, locs);
        Assert.Contains(Origin + "/", locs);
        foreach (var path in new[] { "/report", "/search", "/404", "/404.html", "/search-index.json" }.Concat(OldUrls)) Assert.DoesNotContain(Origin + path, locs);
        Assert.Equal(50, locs.Count(l => l.StartsWith(Origin + "/learn/", StringComparison.Ordinal) && l.Split('/').Length == 6));
        Assert.EndsWith("\n", Read(fx.Production, "sitemap.xml"));
    }

    [NodeFact]
    public void Production_robots_points_at_the_sitemap_and_keeps_noindex_pages_crawlable()
    {
        var robots = Read(fx.Production, "robots.txt");
        Assert.StartsWith("User-agent: *\n", robots);
        Assert.Contains("Sitemap: " + Origin + "/sitemap.xml\n", robots);
        // 共有ルームの URL（?room=）と API は、クローラーに読ませない
        Assert.Contains("Disallow: /api/\n", robots);
        Assert.Contains("Disallow: /practice/room?room=\n", robots);
        // noindex のページ（問い合わせ・検索）は、noindex の meta を読めるように、読み込みを禁止しない（robots で禁止すると noindex が効かない）
        var disallows = Regex.Matches(robots, "^Disallow: (.*)$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["/api/", "/practice/room?room="], disallows);
        Assert.EndsWith("\n", robots);
    }

    [NodeFact]
    public void Develop_has_no_sitemap_allows_crawling_and_sends_noindex_in_the_header()
    {
        Assert.False(File.Exists(Path.Combine(fx.Develop, "sitemap.xml")));
        Assert.Equal("User-agent: *\nAllow: /\n", Read(fx.Develop, "robots.txt"));          // 禁止はしない（X-Robots-Tag を読ませる）
        Assert.DoesNotContain("Sitemap:", Read(fx.Develop, "robots.txt"));
        Assert.Matches(@"(?m)^  X-Robots-Tag: noindex$", Read(fx.Develop, "_headers"));
        Assert.DoesNotContain("X-Robots-Tag", Read(fx.Production, "_headers"));
    }

    [NodeFact]
    public void Every_page_has_one_title_description_and_canonical_and_complete_open_graph_with_one_image()
    {
        Assert.Equal(76, Pages.Count);
        foreach (var page in Pages)
        {
            var head = Head(fx.Production, page.Path);
            var url = Origin + page.Path;
            Assert.True(Count(head, "<title>") == 1, page.Path + " の title");
            Assert.True(Count(head, @"<meta name=""description""") == 1, page.Path + " の description");
            Assert.True(Count(head, @"<link rel=""canonical""") == 1, page.Path + " の canonical");
            Assert.Equal(url, Meta(head, @"<link rel=""canonical"" href=""([^""]+)"""));
            foreach (var key in new[] { "og:site_name", "og:locale", "og:type", "og:title", "og:description", "og:url" })
                Assert.True(Count(head, $@"<meta property=""{key}""") == 1, page.Path + " の " + key);
            Assert.Equal(url, Meta(head, @"<meta property=""og:url"" content=""([^""]+)"""));
            Assert.Equal(Meta(head, "<title>(.*?)</title>"), Meta(head, @"<meta property=""og:title"" content=""([^""]*)"""));
            Assert.Equal(Meta(head, @"<meta name=""description"" content=""([^""]*)"""), Meta(head, @"<meta property=""og:description"" content=""([^""]*)"""));
            Assert.Equal(page.OgType, Meta(head, @"<meta property=""og:type"" content=""([^""]+)"""));
            Assert.Equal("ja_JP", Meta(head, @"<meta property=""og:locale"" content=""([^""]+)"""));
            // OGP 画像は全ページに 1 つ。カードは summary_large_image
            Assert.Equal(Origin + "/images/" + SiteBuilder.OgImageFile, Meta(head, @"<meta property=""og:image"" content=""([^""]+)"""));
            Assert.True(Count(head, @"<meta property=""og:image""") == 1, page.Path + " の og:image");
            Assert.Equal("summary_large_image", Meta(head, @"<meta name=""twitter:card"" content=""([^""]+)"""));
            // noindex は /report と /search だけ（meta は 1 つ）
            Assert.Equal(page.NoIndex ? 1 : 0, Count(head, @"<meta name=""robots"""));
            // hreflang・JSON-LD は使わない（単一言語。構造化データは、必要になるまで入れない）
            Assert.DoesNotContain("hreflang", head);
            Assert.DoesNotContain("application/ld+json", head);
        }
        Assert.Equal(50, Pages.Count(p => Head(fx.Production, p.Path).Contains("content=\"article\"", StringComparison.Ordinal)));
    }

    [NodeFact]
    public void Titles_and_descriptions_are_unique_across_the_site_and_404_is_noindex()
    {
        var titles = Pages.Select(p => Meta(Head(fx.Production, p.Path), "<title>(.*?)</title>")).ToList();
        Assert.Equal(76, titles.Distinct().Count());
        var descriptions = Pages.Select(p => Meta(Head(fx.Production, p.Path), @"<meta name=""description"" content=""([^""]*)""")).ToList();
        Assert.Equal(76, descriptions.Distinct().Count());
        var notFound = Read(fx.Production, "404.html");
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", notFound);
        Assert.DoesNotContain("rel=\"canonical\"", notFound);
    }

    [NodeFact]
    public void Develop_pages_use_the_dev_origin_everywhere()
    {
        foreach (var page in Pages)
        {
            var head = Head(fx.Develop, page.Path);
            Assert.DoesNotContain("avatar.fara-labs.com", head);
            Assert.Equal(DevOrigin + page.Path, Meta(head, @"<link rel=""canonical"" href=""([^""]+)"""));
        }
    }

    [NodeFact]
    public void Pages_json_for_the_head_sync_matches_the_head_of_every_page()
    {
        // js/head-sync.js が、アプリ内の移動のあとに head を合わせる元の表。静的 HTML の head と同じ値でなければならない
        var map = System.Text.Json.JsonDocument.Parse(Read(fx.Production, "data/pages.json")).RootElement;
        Assert.Equal(76, map.EnumerateObject().Count());
        foreach (var page in Pages)
        {
            var entry = map.GetProperty(page.Path);
            var head = Head(fx.Production, page.Path);
            Assert.Equal(System.Net.WebUtility.HtmlDecode(Meta(head, "<title>(.*?)</title>")), entry.GetProperty("title").GetString());
            Assert.Equal(System.Net.WebUtility.HtmlDecode(Meta(head, @"<meta name=""description"" content=""([^""]*)""")), entry.GetProperty("description").GetString());
            Assert.Equal(Meta(head, @"<meta property=""og:type"" content=""([^""]+)"""), entry.GetProperty("ogType").GetString());
            Assert.Equal(Count(head, @"<meta name=""robots"" content=""noindex""") == 1, entry.TryGetProperty("noindex", out var noindex) && noindex.GetBoolean());
        }
    }

    [NodeFact]
    public void The_og_image_is_a_1200_by_630_png_and_the_real_template_adds_its_size_alt_and_favicons()
    {
        // 画像の中身（PNG の IHDR）。SNS のカードは 1200×630。ファイル名の版（og-v1）を変えるときは SiteBuilder.OgImageFile と合わせる
        var png = File.ReadAllBytes(Path.Combine(Repo.Root, "wwwroot", "images", SiteBuilder.OgImageFile));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal(1200, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(630, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.True(File.Exists(Path.Combine(fx.Production, "images", SiteBuilder.OgImageFile)), "Build が画像を出力にコピーする");

        // 本物のテンプレートで、全ページに og:image の「後ろ」へ、大きさ・説明・ファビコンが付く
        foreach (var page in Pages)
        {
            var html = Read(fx.Real, SiteBuilder.FileFor(page.Path));
            var image = html.IndexOf("<meta property=\"og:image\"", StringComparison.Ordinal);
            Assert.True(image >= 0, page.Path);
            Assert.True(html.IndexOf("<meta property=\"og:image:width\" content=\"1200\"", StringComparison.Ordinal) > image, page.Path);
            Assert.True(html.IndexOf("<meta property=\"og:image:height\" content=\"630\"", StringComparison.Ordinal) > image, page.Path);
            Assert.True(html.IndexOf("<meta property=\"og:image:alt\" content=\"", StringComparison.Ordinal) > image, page.Path);
            Assert.Contains("<link rel=\"icon\" href=\"/favicon.ico\" sizes=\"48x48\" />", html);
            Assert.Contains("<link rel=\"icon\" type=\"image/svg+xml\" href=\"/favicon.svg\" />", html);
            Assert.Contains("<link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\" />", html);
        }
        // /favicon.ico は必ず実在させる(無いと not_found_handling の 404 ページが返る)
        foreach (var file in new[] { "favicon.ico", "favicon.svg", "apple-touch-icon.png" })
            Assert.True(File.Exists(Path.Combine(Repo.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", file)), file);
    }
}
