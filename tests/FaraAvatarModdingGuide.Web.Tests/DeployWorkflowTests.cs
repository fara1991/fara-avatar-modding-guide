using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 公開の設定（.github/workflows/deploy.yml・wrangler.toml・worker/index.js）の検査。
/// 守ること: (1) 本番（avatar.fara-labs.com、Worker fara-avatar）へ届く経路は、master への push（と master を選んだ手動実行）だけ。
/// (2) 本番用のビルドと確認用のビルドを取り違えない。(3) 本番の最初の公開は、メンテナンス画面のまま作られる。
/// bash で実際に動かして、wrangler（npx）に渡る引数を確かめるテストを含む（bash が無い環境では飛ばす。CI の ubuntu には有る）。
/// </summary>
public class DeployWorkflowTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(TestData.Root, relative.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    private static string Deploy => Read(".github/workflows/deploy.yml");
    private static string Toml => Read("wrangler.toml");

    /// <summary>"- name: <名前>" から次のステップの手前まで。</summary>
    private static string Step(string yml, string name)
    {
        var start = yml.IndexOf($"- name: {name}\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"ステップ「{name}」がある");
        var next = yml.IndexOf("\n      - ", start + 1, StringComparison.Ordinal);
        return yml[start..(next > 0 ? next : yml.Length)];
    }

    // ------------------------------------------------------------------ ワークフローの置き場と、本番に届く経路

    [Fact]
    public void There_is_one_deploy_workflow_and_no_leftover_wasm_dev_workflow()
    {
        var files = Directory.GetFiles(Path.Combine(TestData.Root, ".github", "workflows")).Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "deploy.yml" }, files);                                // Deploy (wasm-dev) は develop への統合で不要（avatar-dev が WASM 版になる）
        Assert.DoesNotContain("migration/wasm", Deploy);
        Assert.DoesNotContain("wasm-dev", Deploy);
    }

    [Fact]
    public void Only_a_push_to_master_or_develop_or_a_manual_run_starts_the_workflow_and_only_those_refs_deploy()
    {
        var yml = Deploy;
        Assert.Matches(@"(?ms)^on:\s*\n\s*push:\s*\n\s*branches: \[master, develop\]\s*\n\s*pull_request:\s*\n\s*workflow_dispatch:", yml);
        var deploy = yml[yml.IndexOf("  deploy:\n", StringComparison.Ordinal)..];
        Assert.Contains("if: github.event_name != 'pull_request' && (github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop')", deploy);
        // 公開する job は deploy だけ。build は何も公開しない（wrangler は dry-run だけ）
        var build = yml[..yml.IndexOf("  deploy:\n", StringComparison.Ordinal)];
        foreach (Match m in Regex.Matches(build, @"wrangler@\S+ deploy[^\n]*"))
            Assert.Contains("--dry-run", m.Value);
        Assert.DoesNotContain("secrets.", build);                                    // build は Cloudflare の鍵を使わない
    }

    [Fact]
    public void Every_wrangler_deploy_names_its_environment_so_the_top_level_production_worker_is_never_deployed_by_default()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(TestData.Root, ".github", "workflows")))
        {
            var yml = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(yml, @"npx [^\n]*wrangler@\S+ deploy[^\n]*"))
                Assert.Contains("--env ", m.Value);
        }
        // 本番を表す式は、master のときだけ production になる形が、公開の job の中で一貫している
        var deploy = Deploy[Deploy.IndexOf("  deploy:\n", StringComparison.Ordinal)..];
        Assert.Contains("DEPLOY_ENV: ${{ github.ref_name == 'master' && 'production' || 'dev' }}", deploy);
        Assert.Contains("SITE_ENV: ${{ github.ref_name == 'master' && 'production' || 'develop' }}", deploy);
        Assert.Contains("name: site-${{ github.ref_name == 'master' && 'production' || 'develop' }}", deploy);
        Assert.Contains("WORKER_NAME: ${{ github.ref_name == 'master' && 'fara-avatar' || 'fara-avatar-dev' }}", deploy);
        // 'production' が出てくる式は、すべて master の判定とセット
        foreach (Match m in Regex.Matches(deploy, @"\$\{\{[^}]*'production'[^}]*\}\}"))
            Assert.Contains("github.ref_name == 'master' && 'production'", m.Value);
        Assert.Single(Regex.Matches(deploy, @"--env ""\$DEPLOY_ENV"""));              // 公開の wrangler deploy は 1 か所
    }

    [Fact]
    public void Each_environment_gets_its_own_build_and_the_deploy_job_takes_only_the_one_for_its_target()
    {
        var yml = Deploy;
        // build: 本番用と確認用を別のフォルダに作る（--env production / --env develop）。--dev-origin は付けない（devOrigin は tools/site.json の avatar-dev）
        Assert.Contains("--template publish/wwwroot/index.html --out site-production --env production", yml);
        Assert.Contains("--template publish/wwwroot/index.html --out site-develop --env develop", yml);
        Assert.DoesNotContain("--dev-origin", yml);
        Assert.Contains("node .github/scripts/verify-site.mjs site-production production", yml);
        Assert.Contains("node .github/scripts/verify-site.mjs site-develop develop", yml);
        Assert.Contains("name: site-production\n          path: site-production", yml);
        Assert.Contains("name: site-develop\n          path: site-develop", yml);
        // deploy: 取り出した一式を、公開の直前に確かめる。確認は、メンテナンスの確認・公開より前
        var deploy = yml[yml.IndexOf("  deploy:\n", StringComparison.Ordinal)..];
        var download = deploy.IndexOf("actions/download-artifact@v4", StringComparison.Ordinal);
        var verify = deploy.IndexOf("- name: Verify the output matches the target", StringComparison.Ordinal);
        var gate = deploy.IndexOf("- name: Require MAINTENANCE_MODE=notice (production)", StringComparison.Ordinal);
        var wrangler = deploy.IndexOf("- name: Deploy to Cloudflare Workers", StringComparison.Ordinal);
        Assert.True(0 < download && download < verify && verify < gate && gate < wrangler);
        Assert.Contains("node .github/scripts/verify-site.mjs dist \"$SITE_ENV\"", deploy);
        // 確認用の noindex を追記するステップは無い（Build が環境ごとに出す。追記すると二重になる）
        Assert.DoesNotContain("Add noindex header", yml);
        Assert.DoesNotContain("cat >> dist/_headers", yml);
    }

    [Fact]
    public void Ci_dry_runs_wrangler_for_both_environments_against_their_own_output()
    {
        var yml = Deploy;
        var production = Step(yml, "Wrangler dry-run (production)");
        Assert.Contains("rm -rf dist && cp -r site-production dist", production);
        Assert.Contains("npx --yes wrangler@4.149.0 deploy --dry-run --env production", production);
        var dev = Step(yml, "Wrangler dry-run (dev)");
        Assert.Contains("rm -rf dist && cp -r site-develop dist", dev);
        Assert.Contains("npx --yes wrangler@4.149.0 deploy --dry-run --env dev", dev);
        // dry-run は、出力が環境のものか確かめたあと
        Assert.True(yml.IndexOf("- name: Verify the outputs (production, develop)", StringComparison.Ordinal) < yml.IndexOf("- name: Wrangler dry-run (production)", StringComparison.Ordinal));
    }

    [Fact]
    public void Ci_no_longer_exports_or_compares_ssr_and_still_runs_the_tests()
    {
        var yml = Deploy;
        foreach (var gone in new[] { "--export", "dist-ssr", "ssr-develop", "ref: develop", "compare-text", "nav.test", "Export static site" })
            Assert.DoesNotContain(gone, yml);
        foreach (var kept in new[]
        {
            "dotnet build -c Release", "dotnet test -c Release --no-build", "external/fara-site-kit/worker/report.test.mjs", "external/fara-site-kit/worker/maintenance.test.mjs", "worker/index.test.mjs",
            ".github/scripts/require-maintenance.test.mjs", ".github/scripts/verify-site.test.mjs", "tests/js/storage-migrate.test.mjs", "tests/js/head-sync.test.mjs",
            "dotnet publish src/FaraAvatarModdingGuide.Web -c Release", "submodules: true", "tests/js/search-norm.mjs site-production/data/search.json",
        })
            Assert.Contains(kept, yml);
        // 書いてある node のテストが、実在する
        foreach (Match m in Regex.Matches(yml, @"(?<![\w/.-])((?:tests/js|worker|\.github/scripts|external/fara-site-kit/worker)/[\w./-]+\.(?:test\.)?mjs)"))
            Assert.True(File.Exists(Path.Combine(TestData.Root, m.Groups[1].Value)), m.Groups[1].Value);
    }

    // ------------------------------------------------------------------ メンテナンス（MAINTENANCE_MODE）

    [Fact]
    public void Production_deploy_requires_MAINTENANCE_MODE_notice_unless_a_mode_is_given()
    {
        var yml = Deploy;
        // 本番（master）の公開の前に、Cloudflare の MAINTENANCE_MODE が notice かを確かめる（確認用の develop は確かめない）。
        // mode を true / notice / false で指定した手動実行（maintenance-cli の set）は、確認を飛ばす
        Assert.Contains("node .github/scripts/require-maintenance.mjs fara-avatar", yml);
        var gate = Step(yml, "Require MAINTENANCE_MODE=notice (production)");
        Assert.Contains("if: github.ref_name == 'master' && !contains(fromJSON('[\"true\",\"notice\",\"false\"]'), inputs.maintenance_mode)", gate);
        Assert.Contains("SKIP_MAINTENANCE_CHECK: ${{ inputs.skip_maintenance_check }}", gate);
        Assert.Contains("skip_maintenance_check:", yml);
        // 旧名 MAINTENANCE（_MODE なし）は使わない
        Assert.DoesNotContain("MAINTENANCE:", yml.Replace("MAINTENANCE_MODE:", "").Replace("MAINTENANCE_MESSAGE:", ""));
        Assert.DoesNotMatch(@"MAINTENANCE(?![_A-Z])", yml);
    }

    [Fact]
    public void The_gate_lets_a_missing_production_worker_through_so_the_first_release_is_created_under_maintenance()
    {
        var gate = Read(".github/scripts/require-maintenance.mjs");
        Assert.Contains("if (settings.missing)", gate);
        Assert.Contains("初回の公開", gate);
        Assert.Contains("return 0;", gate[gate.IndexOf("if (settings.missing)", StringComparison.Ordinal)..]);
        // 通した公開は MAINTENANCE_MODE:true を付ける（入力なし・skip でないとき）。実際の引数は下の bash のテストが確かめる
        Assert.Contains("vars+=(--var \"MAINTENANCE_MODE:true\")", Deploy);
    }

    [Fact]
    public void Workflow_takes_maintenance_inputs_and_writes_a_summary()
    {
        var yml = Deploy;
        Assert.Contains("      maintenance_mode:\n", yml);
        Assert.Contains("options: [\"\", \"true\", \"notice\", \"false\"]", yml);
        Assert.Contains("      maintenance_message:\n", yml);
        Assert.Contains("MAINTENANCE_MODE_INPUT: ${{ inputs.maintenance_mode }}", yml);
        Assert.Contains("MAINTENANCE_MESSAGE_INPUT: ${{ inputs.maintenance_message }}", yml);
        // 入力はシェルに直接埋め込まない（run の中に ${{ を書かない）
        foreach (var run in RunBlocks(yml)) Assert.DoesNotContain("${{", run);
        // node -e '...' の中に、シングルクォートを書かない(bash の引用が途中で閉じて、構文エラーで落ちる。JS の中では \u0027 と書く)。閉じの引用符の直後が、グループの } であること
        foreach (var run in RunBlocks(yml).Where(r => r.Contains("node -e '", StringComparison.Ordinal)))
            Assert.Matches(@"node -e '[^']*'\s*\n\s*\} >> ""\$GITHUB_STEP_SUMMARY""", run);
        var summary = Step(yml, "Summary (maintenance)");
        Assert.Contains("$GITHUB_STEP_SUMMARY", summary);
        Assert.Contains("MAINTENANCE_MODE", summary);
        Assert.Contains("if: ${{ always() }}", summary);
        Assert.Contains("WORKER_NAME: ${{ github.ref_name == 'master' && 'fara-avatar' || 'fara-avatar-dev' }}", summary);
    }

    [Fact]
    public void Gate_script_exists_and_is_tested_in_ci()
    {
        Assert.True(File.Exists(Path.Combine(TestData.Root, ".github", "scripts", "require-maintenance.mjs")));
        Assert.True(File.Exists(Path.Combine(TestData.Root, ".github", "scripts", "verify-site.mjs")));
        Assert.Contains("require-maintenance.test.mjs", Deploy);
        Assert.Contains("verify-site.test.mjs", Deploy);
    }

    // ------------------------------------------------------------------ wrangler.toml・Worker

    [Fact]
    public void Wrangler_has_only_production_and_dev_environments_with_the_agreed_names_domains_and_assets()
    {
        var toml = Toml;
        Assert.DoesNotContain("wasm-dev", toml);
        Assert.DoesNotContain("publish/wwwroot", toml);
        Assert.Equal(new[] { "[env.dev]", "[env.production]" }, Regex.Matches(toml, @"^\[env\.\w+\]$", RegexOptions.Multiline).Select(m => m.Value).OrderBy(x => x).ToArray());
        var prod = Section(toml, "[env.production]", "[env.dev]");
        var dev = toml[toml.IndexOf("[env.dev]", StringComparison.Ordinal)..];
        Assert.Contains("name = \"fara-avatar\"", prod);
        Assert.Contains("pattern = \"avatar.fara-labs.com\", custom_domain = true", prod);
        Assert.DoesNotContain("avatar-dev", prod);
        Assert.Contains("name = \"fara-avatar-dev\"", dev);
        Assert.Contains("pattern = \"avatar-dev.fara-labs.com\", custom_domain = true", dev);
        Assert.DoesNotContain("pattern = \"avatar.fara-labs.com\"", dev);
        foreach (var section in new[] { prod, dev })
        {
            Assert.Contains("directory = \"./dist\"", section);
            Assert.Contains("html_handling = \"auto-trailing-slash\"", section);
            Assert.Contains("not_found_handling = \"404-page\"", section);
            Assert.Contains("run_worker_first = true", section);
            Assert.Contains("workers_dev = false", section);
            Assert.Contains("preview_urls = false", section);
            Assert.Contains("class_name = \"Room\"", section);                          // 共同練習の部屋
            Assert.DoesNotContain("MAINTENANCE", section);                              // 変数はワークフローが渡す（--var）。toml には書かない
        }
        Assert.Matches(@"(?m)^name = ""fara-avatar""$", toml);
        Assert.Matches(@"(?m)^directory = ""\./dist""$", toml);
    }

    [Fact]
    public void Wrangler_creates_the_room_durable_object_for_a_brand_new_production_worker()
    {
        // 本番の Worker はまだ無い（master への最初の push で作られる）。Durable Object の Room が、その公開で作られるよう、マイグレーションを明示する
        var toml = Toml;
        Assert.Matches(@"(?m)^\[\[migrations\]\]\ntag = ""v1""\nnew_sqlite_classes = \[""Room""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.migrations\]\]\ntag = ""v1""\nnew_sqlite_classes = \[""Room""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.durable_objects\.bindings\]\]\nname = ""ROOMS""\nclass_name = ""Room""$", toml);
    }

    [Fact]
    public void Wrangler_keeps_dashboard_variables_at_the_top_level()
    {
        // 公開（wrangler deploy）のたびに、ダッシュボードの MAINTENANCE_MODE・報告の鍵などが消えないように。keep_vars はトップレベルだけが有効
        // （[env.*] の中に書くと、wrangler が「知らない項目」と警告する。本番・確認用の両方に、トップレベルの値が効く）
        var toml = Toml;
        var lines = Regex.Matches(toml, @"^keep_vars = true$", RegexOptions.Multiline);
        Assert.Single(lines);
        Assert.True(lines[0].Index < Regex.Match(toml, @"^\[+env\.", RegexOptions.Multiline).Index, "最初の [env.*] より前（トップレベル）にある");
    }

    [Fact]
    public void Site_config_has_the_production_origin_and_the_avatar_dev_origin_for_the_develop_build()
    {
        var site = Read("tools/site.json");
        Assert.Contains("\"origin\": \"https://avatar.fara-labs.com\"", site);
        Assert.Contains("\"devOrigin\": \"https://avatar-dev.fara-labs.com\"", site);
    }

    [Fact]
    public void Worker_checks_maintenance_first_and_serves_the_state_api_without_rewriting_html()
    {
        var js = Read("worker/index.js");
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
        // 旧名の変数は読まない。確認用の noindex は Build が出す（Worker の変数 NOINDEX は使わない）
        Assert.DoesNotMatch(@"env\??\.MAINTENANCE(?![_A-Z])", js);
        Assert.DoesNotContain("NOINDEX", js);
        Assert.DoesNotContain("NOINDEX", Toml);
    }

    [Fact]
    public void Worker_has_no_copies_of_the_shared_kit_files()
    {
        Assert.False(File.Exists(Path.Combine(TestData.Root, "worker", "maintenance.js")), "worker/maintenance.js は kit にある");
        Assert.False(File.Exists(Path.Combine(TestData.Root, "worker", "report.js")), "worker/report.js は kit にある");
        var module = Read("external/fara-site-kit/worker/maintenance.js");
        foreach (var name in new[] { "getMaintenance", "maintenanceResponse" })
            Assert.Contains($"export function {name}", module);
        Assert.Contains("twitter.com/game_fara", module);
        Assert.Contains("export async function handleReport", Read("external/fara-site-kit/worker/report.js"));
    }

    private static string Section(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{from} .. {to}");
        return text[start..end];
    }

    // ------------------------------------------------------------------ 公開のステップを、実際に bash で動かして、wrangler（npx）に渡る引数を確かめる

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

    private static string DeployRun(string stepName) => RunBlocks(Step(Deploy, stepName)).Single();

    private static string? FindBash()
    {
        foreach (var candidate in new[] { "/bin/bash", "/usr/bin/bash", @"C:\Program Files\Git\bin\bash.exe", @"C:\Program Files\Git\usr\bin\bash.exe" })
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    /// <summary>run を bash で実行し、npx に渡った引数を返す。bash が無い環境では null。止まった（exit 1）ときは、終了コードと標準エラーを返す。</summary>
    private static (int Exit, string Stderr, List<string> Args)? Run(string script, Dictionary<string, string> env)
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
            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return (proc.ExitCode, stdout + stderr, File.Exists(argsOut) ? File.ReadAllLines(argsOut).ToList() : new List<string>());
        }
        finally { Directory.Delete(dir, true); }
    }

    private static List<string>? Args(string script, Dictionary<string, string> env)
    {
        if (Run(script, env) is not { } r) return null;
        Assert.True(r.Exit == 0, r.Stderr);
        return r.Args;
    }

    /// <summary>ブランチは、環境に合うもの（production = master、dev = develop）を既定にする。</summary>
    private static Dictionary<string, string> Inputs(string env, string mode = "", string message = "", string skip = "", string? gitRef = null) => new()
    {
        ["DEPLOY_ENV"] = env,
        ["GITHUB_REF"] = gitRef ?? (env == "production" ? "refs/heads/master" : "refs/heads/develop"),
        ["MAINTENANCE_MODE_INPUT"] = mode, ["MAINTENANCE_MESSAGE_INPUT"] = message, ["SKIP_MAINTENANCE_CHECK"] = skip,
    };

    private static string[] Expected(string env, params string[] vars) => new[] { "--yes", "wrangler@4.149.0", "deploy", "--env", env }.Concat(vars).ToArray();

    [Fact]
    public void Production_deploy_step_passes_the_right_vars_for_each_trigger()
    {
        var script = DeployRun("Deploy to Cloudflare Workers");
        if (Args(script, Inputs("production")) is not { } push) return;   // bash が無い環境では飛ばす
        // push（入力なし）: 公開と同時にメンテナンス画面（本番の最初の公開も、これ）
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:true"), push);
        // 確認を飛ばしたとき（skip_maintenance_check）: メンテナンス画面にしない（--var なし）
        Assert.Equal(Expected("production"), Args(script, Inputs("production", skip: "true")));
        // mode を指定: 指定した値（skip でも指定が優先）
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:notice"), Args(script, Inputs("production", mode: "notice")));
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:false"), Args(script, Inputs("production", mode: "false", skip: "true")));
        // mode と message
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MODE:notice", "--var", "MAINTENANCE_MESSAGE:20:00 ごろ"), Args(script, Inputs("production", mode: "notice", message: "20:00 ごろ")));
        // message だけ: mode は変えない（勝手にメンテナンス画面にしない）
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MESSAGE:更新します"), Args(script, Inputs("production", message: "更新します")));
        // 記号を含む文面も、そのまま 1 つの引数で渡る（シェルに解釈されない）
        var tricky = "a `b` $(c) \"d\" 'e' ; f & g";
        Assert.Equal(Expected("production", "--var", "MAINTENANCE_MESSAGE:" + tricky), Args(script, Inputs("production", message: tricky)));
    }

    [Fact]
    public void Develop_deploy_step_adds_vars_only_when_inputs_are_given()
    {
        var script = DeployRun("Deploy to Cloudflare Workers");
        if (Args(script, Inputs("dev")) is not { } push) return;
        Assert.Equal(Expected("dev"), push);                                          // push: 何も付けない（keep_vars で現在の値が残る）
        Assert.Equal(Expected("dev"), Args(script, Inputs("dev", skip: "true")));
        Assert.Equal(Expected("dev", "--var", "MAINTENANCE_MODE:true"), Args(script, Inputs("dev", mode: "true")));
        Assert.Equal(Expected("dev", "--var", "MAINTENANCE_MESSAGE:x"), Args(script, Inputs("dev", message: "x")));
    }

    [Theory]
    [InlineData("production", "refs/heads/develop")]
    [InlineData("production", "refs/heads/migration/wasm")]
    [InlineData("production", "refs/heads/feature/x")]
    [InlineData("production", "refs/pull/1/merge")]
    [InlineData("production", "")]
    [InlineData("dev", "refs/heads/master")]
    [InlineData("dev", "refs/heads/feature/x")]
    public void The_deploy_step_refuses_an_environment_that_does_not_match_the_branch(string env, string gitRef)
    {
        // 式の取り違え（production を master 以外から・dev を develop 以外から）は、wrangler を呼ぶ前に止まる
        var script = DeployRun("Deploy to Cloudflare Workers");
        if (Run(script, Inputs(env, gitRef: gitRef)) is not { } r) return;
        Assert.NotEqual(0, r.Exit);
        Assert.Empty(r.Args);                                                         // npx（wrangler）は呼ばれていない
        Assert.Contains("::error::", r.Stderr);
    }
}
