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
    public void Notice_banner_puts_the_heading_and_the_text_on_their_own_lines_in_bold()
    {
        // 1 行目に見出し「【お知らせ】」、本文は次の行（worker/maintenance.js の noticeHtml の class と合わせる）
        var js = File.ReadAllText(RepoFile("worker/maintenance.js"));
        Assert.Contains("<strong class=\"maintenance-notice-title\">【お知らせ】</strong><span class=\"maintenance-notice-text\">", js);

        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Matches(@"\.maintenance-notice-title,\s*\.maintenance-notice-text\s*\{\s*display:\s*block;\s*\}", css);
        // 太字は、帯（.maintenance-notice）の font-weight: 700 のまま
        Assert.Matches(@"\.maintenance-notice \{[^}]*font-weight:\s*700;", css);
    }

    [Fact]
    public void Notice_band_height_is_estimated_for_two_lines()
    {
        // 見出し 1 行 + 本文 1 行。PC の目次・見出しへのジャンプの位置に使う（帯の高さの見積もり）
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Matches(@"html:has\(\.maintenance-notice\)\s*\{\s*--notice-band:\s*3\.75rem;", css);
    }

    private static string RuleBody(string css, string selector)
    {
        var match = Regex.Match(css, @"^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}", RegexOptions.Multiline);
        Assert.True(match.Success, $"{selector} の書き方が見つからない");
        return match.Groups[1].Value;
    }

    [Fact]
    public void Notice_banner_is_sticky_at_the_top_and_stays_above_the_content_but_below_the_header()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        var rule = RuleBody(css, ".maintenance-notice");

        // スクロールしても消えないよう、貼り付ける（スマホは画面の最上部）
        Assert.Matches(@"position:\s*sticky;", rule);
        Assert.Matches(@"top:\s*0;", rule);

        // 重なり順: 本文・サイドバーの上（ほかの最大は 30）、固定ヘッダー（1040）より下
        var z = int.Parse(Regex.Match(rule, @"z-index:\s*(\d+);").Groups[1].Value);
        Assert.InRange(z, 31, 1039);

        // PC（641px 以上）: 固定ヘッダーの真下に貼り付く。ヘッダーの高さ 3.5rem は、Bootstrap の box-sizing: border-box で下線を含む（App.razor で読み込み）
        Assert.Matches(@"\.app-header\s*\{[^}]*?height:\s*3\.5rem;", css);
        Assert.Contains("bootstrap@5", File.ReadAllText(RepoFile("Components/App.razor")));
        Assert.Matches(@"@media \(min-width: 641px\) \{\s*\.maintenance-notice \{\s*top:\s*3\.5rem;", css);
    }

    [Fact]
    public void Notice_banner_does_not_hide_anchors_the_article_toc_or_the_mobile_search_bar()
    {
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));

        // 見出しへのジャンプ（scroll-padding-top）と、記事右の目次（top）は、帯の下に合わせる（帯が出ているときだけ）
        Assert.Matches(@"html:has\(\.maintenance-notice\)\s*\{[^}]*scroll-padding-top:\s*calc\(var\(--notice-band\) \+ 1rem\);", css);
        Assert.Matches(@"@media \(min-width: 641px\) \{[^@]*html:has\(\.maintenance-notice\)\s*\{\s*scroll-padding-top:\s*calc\(3\.5rem \+ var\(--notice-band\) \+ 1rem\);", css);

        // 記事右の目次は、.article-aside が貼り付く幅（992px 以上）の中だけ。狭い幅では流れたまま（元の static の指定）
        Assert.Matches(@"@media \(max-width: 991\.98px\) \{\s*\.article-aside \{\s*position:\s*static;", css);
        Assert.Matches(@"@media \(min-width: 992px\) \{\s*main:has\(> \.maintenance-notice\) \.article-aside\s*\{\s*top:\s*calc\(3\.5rem \+ var\(--notice-band\) \+ 1\.25rem\);\s*max-height:\s*calc\(100vh - 5\.5rem - var\(--notice-band\)\);", css);
        Assert.DoesNotMatch(@"^main:has\(> \.maintenance-notice\) \.article-aside", css);

        // スマホの検索欄は、帯と重ならないよう貼り付けない（帯の下に流す）
        Assert.Matches(@"main > \.maintenance-notice \+ \.site-header\s*\{\s*position:\s*static;", css);
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
