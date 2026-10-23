using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// メンテナンス表示（Worker の変数 MAINTENANCE_MODE / MAINTENANCE_MESSAGE。kit の worker/maintenance.js）の、設定とつなぎ込み。
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
        // 公開（wrangler deploy）のたびに、ダッシュボードの MAINTENANCE_MODE が消えないように。keep_vars はトップレベルだけが有効
        // （[env.*] の中に書くと、wrangler が「知らない項目」と警告する。本番・確認用の両方に、トップレベルの値が効く）
        var toml = File.ReadAllText(RepoFile("wrangler.toml"));
        var lines = Regex.Matches(toml, @"^keep_vars = true$", RegexOptions.Multiline);
        Assert.Single(lines);
        Assert.True(lines[0].Index < toml.IndexOf("[env.", StringComparison.Ordinal), "最初の [env.*] より前（トップレベル）にある");
    }

    [Fact]
    public void Worker_checks_maintenance_first_and_serves_the_state_api_without_rewriting_html()
    {
        var js = File.ReadAllText(RepoFile("worker/index.js"));
        // 共通部品（fara-site-kit）のファイルを使う。メンテナンスの判定・報告の受け口は、各サイトにコピーを持たない
        Assert.Contains("import { getMaintenance, maintenanceResponse } from \"../external/fara-site-kit/worker/maintenance.js\";", js);
        Assert.Contains("import { handleReport } from \"../external/fara-site-kit/worker/report.js\";", js);
        var fetchStart = js.IndexOf("async fetch(request, env)", StringComparison.Ordinal);
        var maintenance = js.IndexOf("maintenanceResponse(request, maintenance", StringComparison.Ordinal);
        var rooms = js.IndexOf("/api\\/rooms", StringComparison.Ordinal);
        Assert.True(fetchStart >= 0 && maintenance > fetchStart, "fetch の中でメンテナンスを確認している");
        Assert.True(maintenance < rooms, "共同練習の部屋（WebSocket）よりも先に、メンテナンスを確認する");
        // お知らせの帯は、アプリが /api/maintenance を読んで出す。HTML への注入（withNotice）は使わない
        Assert.Contains("url.pathname === \"/api/maintenance\"", js);
        Assert.DoesNotContain("withNotice", js);
        Assert.Contains("return env.ASSETS.fetch(request);", js);
    }

    [Fact]
    public void Worker_has_no_copies_of_the_shared_kit_files()
    {
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(RepoFile("worker/index.js"))!, "maintenance.js")), "worker/maintenance.js は kit にある");
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(RepoFile("worker/index.js"))!, "report.js")), "worker/report.js は kit にある");
        var module = File.ReadAllText(RepoFile("external/fara-site-kit/worker/maintenance.js"));
        foreach (var name in new[] { "getMaintenance", "maintenanceResponse" })
            Assert.Contains($"export function {name}", module);
        Assert.Contains("twitter.com/game_fara", module);
        Assert.Contains("export async function handleReport", File.ReadAllText(RepoFile("external/fara-site-kit/worker/report.js")));
    }

    [Fact]
    public void Notice_banner_has_a_style_in_the_site_css()
    {
        // バナーは class だけで、インラインの style は付けない（CSP のため）。見た目は CSS にある
        var css = File.ReadAllText(RepoFile("wwwroot/css/site.css"));
        Assert.Contains(".maintenance-notice {", css);
    }

    [Fact]
    public void Ssr_notice_banner_puts_the_heading_and_the_text_on_their_own_lines_in_bold()
    {
        // SSR 版（フェーズ 5 で消す）の CSS。WebAssembly 版の帯は kit の MaintenanceBanner（Web.Tests の MaintenanceTests が確かめる）
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
    public void Production_deploy_requires_MAINTENANCE_MODE_notice_unless_a_mode_is_given()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        // 本番（master）の公開の前に、Cloudflare の MAINTENANCE_MODE が notice かを確かめる（確認用の develop は確かめない）。
        // mode を true / notice / false で指定した手動実行（maintenance-cli の set）は、確認を飛ばす
        Assert.Contains("node .github/scripts/require-maintenance.mjs fara-avatar", yml);
        var gate = yml.IndexOf("- name: Require MAINTENANCE_MODE=notice (production)", StringComparison.Ordinal);
        Assert.True(gate > 0);
        Assert.Contains("if: github.ref_name == 'master' && !contains(fromJSON('[\"true\",\"notice\",\"false\"]'), inputs.maintenance_mode)", yml[gate..(gate + 300)]);
        // 確認は公開より前
        var deploy = yml.IndexOf("- name: Deploy to Cloudflare Workers", StringComparison.Ordinal);
        Assert.True(gate < deploy);
        Assert.Contains("SKIP_MAINTENANCE_CHECK: ${{ inputs.skip_maintenance_check }}", yml);
        Assert.Contains("skip_maintenance_check:", yml);
        // 旧名は使わない
        Assert.DoesNotContain("MAINTENANCE:", yml.Replace("MAINTENANCE_MODE:", "").Replace("MAINTENANCE_MESSAGE:", ""));
        // --env の式は、master = production・それ以外 = dev
        Assert.Contains("DEPLOY_ENV: ${{ github.ref_name == 'master' && 'production' || 'dev' }}", yml);
    }

    [Fact]
    public void Workflows_take_maintenance_inputs_and_write_a_summary()
    {
        foreach (var file in new[] { ".github/workflows/deploy.yml", ".github/workflows/deploy-wasm-dev.yml" })
        {
            var yml = File.ReadAllText(RepoFile(file));
            Assert.Contains("      maintenance_mode:\n", yml.Replace("\r\n", "\n"));
            Assert.Contains("options: [\"\", \"true\", \"notice\", \"false\"]", yml);
            Assert.Contains("      maintenance_message:\n", yml.Replace("\r\n", "\n"));
            Assert.Contains("MAINTENANCE_MODE_INPUT: ${{ inputs.maintenance_mode }}", yml);
            Assert.Contains("MAINTENANCE_MESSAGE_INPUT: ${{ inputs.maintenance_message }}", yml);
            // 入力はシェルに直接埋め込まない（run の中に ${{ を書かない）
            foreach (var run in RunBlocks(yml)) Assert.DoesNotContain("${{", run);
            // node -e '...' の中に、シングルクォートを書かない(bash の引用が途中で閉じて、構文エラーで落ちる。JS の中では \u0027 と書く)。閉じの引用符の直後が、グループの } であること
            foreach (var run in RunBlocks(yml).Where(r => r.Contains("node -e '", StringComparison.Ordinal)))
                Assert.Matches(@"node -e '[^']*'\s*\n\s*\} >> ""\$GITHUB_STEP_SUMMARY""", run.Replace("\r\n", "\n"));
            var summary = yml.IndexOf("- name: Summary (maintenance)", StringComparison.Ordinal);
            Assert.True(summary > 0);
            Assert.Contains("$GITHUB_STEP_SUMMARY", yml[summary..]);
            Assert.Contains("MAINTENANCE_MODE", yml[summary..]);
            Assert.Contains("if: ${{ always() }}", yml[summary..(summary + 120)]);
        }
    }

    [Fact]
    public void Deploy_workflows_keep_their_branch_guards_and_worker_names()
    {
        var prod = File.ReadAllText(RepoFile(".github/workflows/deploy.yml"));
        Assert.Contains("github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop'", prod);
        Assert.Contains("WORKER_NAME: ${{ github.ref_name == 'master' && 'fara-avatar' || 'fara-avatar-dev' }}", prod);
        var wasm = File.ReadAllText(RepoFile(".github/workflows/deploy-wasm-dev.yml"));
        Assert.Contains("if: github.ref == 'refs/heads/migration/wasm'", wasm);
        Assert.Contains("WORKER_NAME: fara-avatar-wasm-dev", wasm);
    }

    // ---- 公開のステップの run を、実際に bash で動かして、wrangler（npx）に渡る引数を確かめる ----

    private static IEnumerable<string> RunBlocks(string yml)
    {
        var lines = yml.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var m = Regex.Match(lines[i], @"^(\s*)run: \|\s*$");
            if (!m.Success) continue;
            var indent = m.Groups[1].Length + 2;
            var body = new List<string>();
            for (i++; i < lines.Length && (lines[i].Length == 0 || lines[i].StartsWith(new string(' ', indent))); i++)
                body.Add(lines[i].Length >= indent ? lines[i][indent..] : "");
            i--;
            yield return string.Join("\n", body);
        }
    }

    private static string DeployRun(string file, string stepName)
    {
        var yml = File.ReadAllText(RepoFile(file)).Replace("\r\n", "\n");
        var start = yml.IndexOf($"- name: {stepName}\n", StringComparison.Ordinal);
        Assert.True(start >= 0, stepName);
        var next = yml.IndexOf("\n      - ", start + 1, StringComparison.Ordinal);
        return RunBlocks(yml[start..(next > 0 ? next : yml.Length)]).Single();
    }

    private static string? FindBash()
    {
        foreach (var candidate in new[] { "/bin/bash", "/usr/bin/bash", @"C:\Program Files\Git\bin\bash.exe", @"C:\Program Files\Git\usr\bin\bash.exe" })
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    /// <summary>run を bash で実行し、npx に渡った引数を返す。bash が無い環境（CI の ubuntu には有る）では null。</summary>
    private static List<string>? RunDeploy(string script, Dictionary<string, string> env)
    {
        var bash = FindBash();
        if (bash is null) return null;
        var dir = Path.Combine(AppContext.BaseDirectory, "fake-npx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fake = Path.Combine(dir, "npx");
            File.WriteAllText(fake, "#!/bin/bash\nfor a in \"$@\"; do printf '%s\\n' \"$a\"; done > \"$ARGS_OUT\"\n");
            var scriptPath = Path.Combine(dir, "run.sh");
            File.WriteAllText(scriptPath, script.Replace("\r\n", "\n") + "\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var argsOut = Path.Combine(dir, "args.txt");
            var psi = new System.Diagnostics.ProcessStartInfo(bash) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("-eo");
            psi.ArgumentList.Add("pipefail");
            psi.ArgumentList.Add(scriptPath);
            psi.Environment["PATH"] = dir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            psi.Environment["ARGS_OUT"] = argsOut;
            foreach (var (k, v) in env) psi.Environment[k] = v;
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stderr = proc.StandardError.ReadToEnd();
            proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            Assert.True(proc.ExitCode == 0, stderr);
            return File.ReadAllLines(argsOut).ToList();
        }
        finally { Directory.Delete(dir, true); }
    }

    private static Dictionary<string, string> Inputs(string env, string mode = "", string message = "", string skip = "") => new()
    {
        ["DEPLOY_ENV"] = env, ["MAINTENANCE_MODE_INPUT"] = mode, ["MAINTENANCE_MESSAGE_INPUT"] = message, ["SKIP_MAINTENANCE_CHECK"] = skip,
    };

    private static string[] Expected(string env, params string[] vars) => new[] { "--yes", "wrangler@4.149.0", "deploy", "--env", env }.Concat(vars).ToArray();

    [Fact]
    public void Production_deploy_step_passes_the_right_vars_for_each_trigger()
    {
        var script = DeployRun(".github/workflows/deploy.yml", "Deploy to Cloudflare Workers");
        if (RunDeploy(script, Inputs("production")) is not { } push) return;   // bash が無い環境では飛ばす
        // push（入力なし）: 従来どおり、公開と同時にメンテナンス画面
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:true"), push);
        // 確認を飛ばしたとき（skip_maintenance_check）: メンテナンス画面にしない（--var なし）
        Assert.Equal(Expected("production"), RunDeploy(script, Inputs("production", skip: "true")));
        // mode を指定: 指定した値（skip でも指定が優先）
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:notice"), RunDeploy(script, Inputs("production", mode: "notice")));
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:false"), RunDeploy(script, Inputs("production", mode: "false", skip: "true")));
        // mode と message
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:notice", "--var", "MAINTENANCE_MESSAGE:20:00 ごろ"), RunDeploy(script, Inputs("production", mode: "notice", message: "20:00 ごろ")));
        // message だけ: mode は変えない（勝手にメンテナンス画面にしない）
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MESSAGE:更新します"), RunDeploy(script, Inputs("production", message: "更新します")));
        // 記号を含む文面も、そのまま 1 つの引数で渡る（シェルに解釈されない）
        var tricky = "a `b` $(c) \"d\" 'e' ; f & g";
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MESSAGE:" + tricky), RunDeploy(script, Inputs("production", message: tricky)));
    }

    [Fact]
    public void Develop_deploy_step_adds_vars_only_when_inputs_are_given()
    {
        var script = DeployRun(".github/workflows/deploy.yml", "Deploy to Cloudflare Workers");
        if (RunDeploy(script, Inputs("dev")) is not { } push) return;
        Assert.Equal(Expected("dev"), push);                                          // push: 何も付けない（keep_vars で現在の値が残る）
        Assert.Equal(Expected("dev"), RunDeploy(script, Inputs("dev", skip: "true")));
        Assert.Equal(Expected("dev", "--var", "MAINTENANCE_MODE:true"), RunDeploy(script, Inputs("dev", mode: "true")));
        Assert.Equal(Expected("dev", "--var", "MAINTENANCE_MESSAGE:x"), RunDeploy(script, Inputs("dev", message: "x")));
    }

    [Fact]
    public void Wasm_dev_deploy_step_adds_vars_only_when_inputs_are_given()
    {
        var script = DeployRun(".github/workflows/deploy-wasm-dev.yml", "Deploy to Cloudflare Workers (wasm-dev)");
        if (RunDeploy(script, Inputs("")) is not { } push) return;
        Assert.Equal(Expected("wasm-dev"), push);
        Assert.Equal(Expected("wasm-dev", "--var", "MAINTENANCE_MODE:notice", "--var", "MAINTENANCE_MESSAGE:m"), RunDeploy(script, Inputs("", mode: "notice", message: "m")));
        Assert.Equal(Expected("wasm-dev", "--var", "MAINTENANCE_MODE:false"), RunDeploy(script, Inputs("", mode: "false")));
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
        Assert.Contains("external/fara-site-kit/worker/maintenance.test.mjs", yml);
        Assert.Contains("worker/index.test.mjs", yml);
    }
}
