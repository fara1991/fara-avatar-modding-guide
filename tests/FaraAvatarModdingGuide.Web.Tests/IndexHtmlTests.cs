using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Web.Services;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>wwwroot/index.html（Build のテンプレート）の検査。印の形・設定のキー・CSP で止まる書き方をしていないか。</summary>
public class IndexHtmlTests
{
    private static readonly string Html = File.ReadAllText(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "index.html"));

    /// <summary>コメントを除いた HTML（コメントの説明文に出てくる語を、実際の記述と取り違えないため）。</summary>
    private static string NoComments(string html) => Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);

    [Fact]
    public void Template_has_the_seo_marks_once_and_balanced_wasm_marks()
    {
        foreach (var mark in new[] { "<!-- SEO-HEAD:START -->", "<!-- SEO-HEAD:END -->", "<!-- SEO-BODY:START -->", "<!-- SEO-BODY:END -->" })
            Assert.Single(Regex.Matches(Html, Regex.Escape(mark)));
        // #app の直下に SEO-BODY（kit の決まり）
        Assert.Matches(@"<div id=""app"">\s*<!-- SEO-BODY:START -->\s*<!-- SEO-BODY:END -->\s*</div>", Html);
        SiteBuilder.RemoveWasmOnly(Html, keep: true);    // 数が合わなければ例外
        SiteBuilder.RemoveStaticOnly(Html, keep: true);
    }

    [Fact]
    public void Storage_keys_in_the_template_match_the_site_options()
    {
        // 違うと、描画の前のテーマの反映（theme-init.js）と、保存（SiteThemeState）が別のキーを見る。SSR 版と同じキーのままにする
        Assert.Contains($"data-storage-key=\"{SiteInfo.StorageKey}\"", Html);
        Assert.Contains($"data-sidebar-key=\"{SiteInfo.SidebarKey}\"", Html);
        Assert.Equal("avatar-modding-guide.theme", SiteInfo.StorageKey);
        Assert.Equal("avatar-modding-guide.sidebar-hidden", SiteInfo.SidebarKey);
    }

    [Fact]
    public void Storage_migration_runs_before_theme_init()
    {
        var html = NoComments(Html);
        Assert.True(html.IndexOf("js/storage-migrate.js", StringComparison.Ordinal) is var migrate && migrate > 0);
        Assert.True(migrate < html.IndexOf("theme-init.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Scripts_are_all_external_so_the_csp_does_not_block_them()
    {
        foreach (Match m in Regex.Matches(NoComments(Html), "<script(?<attrs>[^>]*)>(?<body>.*?)</script>", RegexOptions.Singleline))
        {
            if (m.Groups["attrs"].Value.Contains("type=\"importmap\"", StringComparison.Ordinal)) continue;   // importmap だけは外部ファイルにできない(src が使えない)。CSP はハッシュで許可する(下のテスト)
            Assert.Contains("src=", m.Groups["attrs"].Value);
            Assert.True(string.IsNullOrWhiteSpace(m.Groups["body"].Value), "インラインの script があります");
        }
        Assert.DoesNotContain("bootstrap", NoComments(Html), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.jsdelivr.net", NoComments(Html));
    }

    [Fact]
    public void Static_only_pages_lose_the_loader_and_blazor_but_keep_theme_and_css()
    {
        var stripped = NoComments(SiteBuilder.RemoveStaticOnly(SiteBuilder.RemoveWasmOnly(Html, keep: false), keep: true));
        Assert.DoesNotContain("boot-indicator", stripped);
        Assert.DoesNotContain("blazor", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MudBlazor", stripped);
        Assert.Contains("css/base.css", stripped);
        Assert.Contains("css/app.css", stripped);
        Assert.Contains("fara-site-kit.css", stripped);
        Assert.Contains("theme-init.js", stripped);
        Assert.Contains("fonts.googleapis.com", stripped);
        Assert.Contains("js/notice.js", stripped);                  // 静的なページは、notice.js が「更新予定」の帯を出す
        Assert.DoesNotContain("STATIC-ONLY", stripped);

        var kept = NoComments(SiteBuilder.RemoveStaticOnly(SiteBuilder.RemoveWasmOnly(Html, keep: true), keep: false));
        Assert.Contains("id=\"boot-indicator\"", kept);
        Assert.Contains("_framework/blazor.webassembly.js", kept);
        Assert.Contains("_content/MudBlazor/MudBlazor.min.js", kept);
        Assert.DoesNotContain("WASM-ONLY", kept);
        Assert.DoesNotContain("notice.js", kept);                   // WebAssembly のページの帯は、kit の MaintenanceBanner
    }

    [Fact]
    public void Default_theme_is_dark_like_the_ssr_site()
    {
        Assert.Contains("<html lang=\"ja\" data-bs-theme=\"dark\">", Html);
    }

    [Fact]
    public void Import_map_points_three_at_the_bundled_file_and_comes_before_every_module()
    {
        var html = NoComments(Html);
        var maps = Regex.Matches(html, "<script type=\"importmap\">(?<body>.*?)</script>", RegexOptions.Singleline).Select(m => m.Groups["body"].Value).ToList();
        var map = Assert.Single(maps);                                        // importmap は 1 つだけ(複数あると、後のものは無視される)
        using var json = System.Text.Json.JsonDocument.Parse(map);
        Assert.Equal("/lib/three/three.module.js", json.RootElement.GetProperty("imports").GetProperty("three").GetString());
        Assert.False(json.RootElement.TryGetProperty("integrity", out _));    // 同じサイトのファイル。SRI は要らない
        // 同梱のファイルが、importmap の指す場所にある(three.js 0.170.0)
        var file = Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "lib", "three", "three.module.js");
        Assert.True(File.Exists(file));
        Assert.Contains("const REVISION = '170';", File.ReadAllText(file));
        Assert.True(File.Exists(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "lib", "three", "LICENSE")));   // MIT のライセンス表示
        // 最初の module(Blazor の読み込み)より前
        Assert.True(html.IndexOf("type=\"importmap\"", StringComparison.Ordinal) < html.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal));
        // WebAssembly のページだけに残る領域の中(印の中)
        Assert.Contains("importmap", SiteBuilder.RemoveStaticOnly(SiteBuilder.RemoveWasmOnly(Html, keep: true), keep: false));
        // CSP に入れるハッシュは、Build が計算する(手で書かない)
        Assert.Single(SiteBuilder.ImportMapHashes(Html));
    }

    [Fact]
    public void Practice_styles_icons_and_background_script_are_linked_once()
    {
        var html = NoComments(Html);
        foreach (var asset in new[] { "css/bi-icons.css", "css/practice.css", "js/bgscene.js" })
            Assert.Single(Regex.Matches(html, Regex.Escape(asset)));
        // 背景の 3D(bgscene.js)は全ページで 1 度だけ動く。WASM-ONLY / STATIC-ONLY のどちらの中にも入れない(起動の前後で同じ canvas が残る)
        var wasmOnly = Regex.Matches(Html, "<!-- WASM-ONLY:START -->(.*?)<!-- WASM-ONLY:END -->", RegexOptions.Singleline).Select(m => m.Groups[1].Value);
        var staticOnly = Regex.Matches(Html, "<!-- STATIC-ONLY:START -->(.*?)<!-- STATIC-ONLY:END -->", RegexOptions.Singleline).Select(m => m.Groups[1].Value);
        Assert.DoesNotContain(wasmOnly.Concat(staticOnly), part => part.Contains("bgscene.js", StringComparison.Ordinal));
        Assert.True(html.IndexOf("css/practice.css", StringComparison.Ordinal) < html.IndexOf("css/app.css", StringComparison.Ordinal));   // サイトの app.css が勝つ
    }

    [Fact]
    public void Boot_indicator_is_a_small_corner_badge_that_does_not_cover_or_block_the_static_body()
    {
        // 起動までの表示は、静的な本文を覆わない(画面全体の不透明なオーバーレイをやめた。計画書 4.10・11.3)。右下の小さな表示で、操作は止めない
        var style = Regex.Match(NoComments(Html), @"#boot-indicator \{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.Contains("position: fixed", style);
        Assert.Contains("pointer-events: none", style);
        Assert.DoesNotContain("inset: 0", style);
        Assert.DoesNotContain("100vw", style.Replace("calc(100vw - 2rem)", ""));
        Assert.DoesNotContain("100vh", style);
        Assert.Contains("right:", style);
        Assert.Contains("bottom:", style);
    }
}
