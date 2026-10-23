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
            Assert.Contains("src=", m.Groups["attrs"].Value);
            Assert.True(string.IsNullOrWhiteSpace(m.Groups["body"].Value), "インラインの script があります");
        }
        Assert.DoesNotContain("bootstrap", NoComments(Html), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.jsdelivr.net", NoComments(Html));
    }

    [Fact]
    public void Static_only_pages_lose_the_loader_and_blazor_but_keep_theme_and_css()
    {
        var stripped = NoComments(SiteBuilder.RemoveWasmOnly(Html, keep: false));
        Assert.DoesNotContain("boot-indicator", stripped);
        Assert.DoesNotContain("blazor", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MudBlazor", stripped);
        Assert.Contains("css/base.css", stripped);
        Assert.Contains("css/app.css", stripped);
        Assert.Contains("fara-site-kit.css", stripped);
        Assert.Contains("theme-init.js", stripped);
        Assert.Contains("fonts.googleapis.com", stripped);

        var kept = NoComments(SiteBuilder.RemoveWasmOnly(Html, keep: true));
        Assert.Contains("id=\"boot-indicator\"", kept);
        Assert.Contains("_framework/blazor.webassembly.js", kept);
        Assert.Contains("_content/MudBlazor/MudBlazor.min.js", kept);
        Assert.DoesNotContain("WASM-ONLY", kept);
    }

    [Fact]
    public void Default_theme_is_dark_like_the_ssr_site()
    {
        Assert.Contains("<html lang=\"ja\" data-bs-theme=\"dark\">", Html);
    }
}
