using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// メンテナンス表示（Worker の変数 MAINTENANCE / MAINTENANCE_MESSAGE。worker/maintenance.js）の、設定とつなぎ込み。
/// worker/index.js は Cloudflare の部品を読み込むので、Node のテストでは動かせない。そのぶん、ここで、つなぎ込みの要点を確かめる。
/// </summary>
public class MaintenanceConfigTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void Wrangler_keeps_dashboard_variables_for_the_top_level_and_both_environments()
    {
        // 公開（wrangler deploy）のたびに、ダッシュボードの MAINTENANCE が消えないように
        var toml = File.ReadAllText(RepoFile("wrangler.toml"));
        Assert.Equal(3, Regex.Matches(toml, @"^keep_vars = true$", RegexOptions.Multiline).Count);
    }

    [Fact]
    public void Worker_checks_maintenance_before_anything_else_and_adds_the_notice_to_pages()
    {
        var js = File.ReadAllText(RepoFile("worker/index.js"));
        Assert.Contains("import { getMaintenance, maintenanceResponse, withNotice } from \"./maintenance.js\";", js);
        var fetchStart = js.IndexOf("async fetch(request, env)", StringComparison.Ordinal);
        var maintenance = js.IndexOf("maintenanceResponse(request, maintenance", StringComparison.Ordinal);
        var rooms = js.IndexOf("/api\\/rooms", StringComparison.Ordinal);
        Assert.True(fetchStart >= 0 && maintenance > fetchStart, "fetch の中でメンテナンスを確認している");
        Assert.True(maintenance < rooms, "共同練習の部屋（WebSocket）よりも先に、メンテナンスを確認する");
        Assert.Contains("withNotice(await env.ASSETS.fetch(request), maintenance)", js);
    }

    [Fact]
    public void Shared_worker_files_are_the_same_as_the_other_sites()
    {
        // 3 サイト共通のファイル。必要な書き出しがそろっている（中身の同一性は、各サイトのテストと、レビューで確かめる）
        var module = File.ReadAllText(RepoFile("worker/maintenance.js"));
        foreach (var name in new[] { "getMaintenance", "maintenanceResponse", "maintenancePageHtml", "noticeHtml", "withNotice", "escapeHtml" })
            Assert.Contains($"export function {name}", module.Replace("export const escapeHtml", "export function escapeHtml"));
        Assert.Contains("twitter.com/game_fara", module);
    }

    [Fact]
    public void Notice_banner_has_a_style_in_the_site_css()
    {
        // バナーは class だけで、インラインの style は付けない（CSP のため）。見た目は CSS にある
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Contains(".maintenance-notice {", css);
    }

    [Fact]
    public void Ci_runs_the_maintenance_tests()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        Assert.Contains("worker/maintenance.test.mjs", yml);
    }
}
