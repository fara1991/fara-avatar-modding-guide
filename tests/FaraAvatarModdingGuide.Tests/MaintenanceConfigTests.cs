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
    public void Wrangler_keeps_dashboard_variables_at_the_top_level()
    {
        // 公開（wrangler deploy）のたびに、ダッシュボードの MAINTENANCE が消えないように。keep_vars はトップレベルだけが有効
        // （[env.*] の中に書くと、wrangler が「知らない項目」と警告する。本番・確認用の両方に、トップレベルの値が効く）
        var toml = File.ReadAllText(RepoFile("wrangler.toml"));
        var lines = Regex.Matches(toml, @"^keep_vars = true$", RegexOptions.Multiline);
        Assert.Single(lines);
        Assert.True(lines[0].Index < toml.IndexOf("[env.", StringComparison.Ordinal), "最初の [env.*] より前（トップレベル）にある");
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
    public void Production_deploy_requires_MAINTENANCE_notice_and_turns_maintenance_on()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        // 本番（master）の公開の前に、Cloudflare の MAINTENANCE が notice かを確かめる（確認用の develop は確かめない）
        Assert.Contains("node .github/scripts/require-maintenance.mjs fara-avatar", yml);
        var gate = yml.IndexOf("Require MAINTENANCE=notice (production)", StringComparison.Ordinal);
        Assert.True(gate > 0);
        Assert.Contains("if: github.ref_name == 'master'", yml[gate..(gate + 200)]);
        // 確認は公開より前。公開と同時に MAINTENANCE=true にする（確認を飛ばしたときは、メンテナンス画面にしない）
        Assert.True(gate < yml.IndexOf("cloudflare/wrangler-action@v3", StringComparison.Ordinal));
        Assert.Contains("--var MAINTENANCE:true", yml);
        Assert.Contains("!inputs.skip_maintenance_check", yml);
        Assert.Contains("skip_maintenance_check:", yml);
        Assert.Contains("SKIP_MAINTENANCE_CHECK: ${{ inputs.skip_maintenance_check }}", yml);
    }

    [Fact]
    public void Noindex_for_the_develop_site_comes_from_the_workflow_not_from_a_variable()
    {
        // 確認用の noindex は、公開のワークフローが dist/_headers に付ける（ポケモンのサイトと同じ）。Worker の変数 NOINDEX は使わない
        var yml = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        var step = yml.IndexOf("Add noindex header (develop)", StringComparison.Ordinal);
        Assert.True(step > 0);
        var next = yml.IndexOf("- name:", step + 1, StringComparison.Ordinal);
        var body = yml[step..(next > 0 ? next : yml.Length)];
        Assert.Contains("if: github.ref_name != 'master'", body);
        Assert.Contains("X-Robots-Tag: noindex, nofollow", body);
        Assert.DoesNotContain("NOINDEX", File.ReadAllText(RepoFile("wrangler.toml")));
        Assert.DoesNotContain("NOINDEX", File.ReadAllText(RepoFile("worker/index.js")));
    }

    [Fact]
    public void Gate_script_exists_and_is_tested_in_ci()
    {
        Assert.True(File.Exists(RepoFile(".github/scripts/require-maintenance.mjs")));
        Assert.Contains("require-maintenance.test.mjs", File.ReadAllText(RepoFile(".github/workflows/deploy.yml")));
    }

    [Fact]
    public void Ci_runs_the_maintenance_tests()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        Assert.Contains("worker/maintenance.test.mjs", yml);
    }
}
