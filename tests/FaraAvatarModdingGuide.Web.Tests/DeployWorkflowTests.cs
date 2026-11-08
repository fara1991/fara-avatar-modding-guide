using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 公開の設定（.github/workflows/build.yml・deploy-develop.yml・deploy.yml・wrangler.toml・worker/index.js）の検査。
/// 守ること: (1) 本番（avatar.fara-labs.com、Worker fara-avatar）へ届く経路は、master への push（と master を選んだ手動実行）だけ。
/// (2) 本番用のビルドと確認用のビルドを取り違えない。(3) 本番の最初の公開は、メンテナンス画面のまま作られる。
/// bash で実際に動かして、wrangler（npx）に渡る引数を確かめるテストを含む（bash が無い環境では飛ばす。CI の ubuntu には有る）。
/// </summary>
public class DeployWorkflowTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(TestData.Root, relative.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    private static string Build => Read(".github/workflows/build.yml");
    private static string Dev => Read(".github/workflows/deploy-develop.yml");
    private static string Prod => Read(".github/workflows/deploy.yml");
    private static string Toml => Read("wrangler.toml");

    /// <summary>"- name: <名前>" から次のステップの手前まで。</summary>
    private static string Step(string yml, string name)
    {
        var start = yml.IndexOf($"- name: {name}\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"ステップ「{name}」がある");
        var next = yml.IndexOf("\n      - ", start + 1, StringComparison.Ordinal);
        return yml[start..(next > 0 ? next : yml.Length)];
    }

    /// <summary>ステップ名が、並べた順に出てくる。</summary>
    private static void AssertOrder(string yml, params string[] names)
    {
        var positions = names.Select(n => yml.IndexOf($"- name: {n}\n", StringComparison.Ordinal)).ToArray();
        for (var i = 0; i < names.Length; i++) Assert.True(positions[i] >= 0, $"ステップ「{names[i]}」がある");
        for (var i = 1; i < names.Length; i++) Assert.True(positions[i - 1] < positions[i], $"「{names[i - 1]}」は「{names[i]}」より前");
    }

    // ------------------------------------------------------------------ ワークフローの置き場と、本番に届く経路

    [Fact]
    public void There_are_three_workflows_in_the_same_shape_as_pokemon()
    {
        // build.yml（ビルドとテスト）・deploy-develop.yml（develop → 確認用）・deploy.yml（master → 本番）。Pokémon・portfolio と同じ名前
        var files = Directory.GetFiles(Path.Combine(TestData.Root, ".github", "workflows")).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "build.yml", "deploy-develop.yml", "deploy.yml" }, files);
        foreach (var yml in new[] { Build, Dev, Prod })
        {
            Assert.DoesNotContain("migration/wasm", yml);
            Assert.DoesNotContain("wasm-dev", yml);
            Assert.Contains("permissions:\n  contents: read", yml);
            // 成果物は保存も取り出しもしない（公開のワークフローは自分でビルドする）
            Assert.DoesNotContain("upload-artifact", yml);
            Assert.DoesNotContain("download-artifact", yml);
        }
    }

    [Fact]
    public void Only_a_push_to_master_reaches_production_and_only_a_push_to_develop_reaches_dev()
    {
        Assert.Matches(@"(?ms)^on:\s*\n(?:\s*#[^\n]*\n)?\s*push:\s*\n\s*branches: \[develop, master\]\s*\n\s*pull_request:\s*\n\s*workflow_dispatch:", Build);
        Assert.Matches(@"(?ms)^on:\s*\n\s*push:\s*\n\s*branches: \[develop\]\s*\n\s*workflow_dispatch:", Dev);
        Assert.Matches(@"(?ms)^on:\s*\n\s*push:\s*\n\s*branches: \[master\]\s*\n\s*workflow_dispatch:", Prod);
        Assert.DoesNotContain("pull_request", Dev);
        Assert.DoesNotContain("pull_request", Prod);
        Assert.Contains("\n    if: github.ref == 'refs/heads/develop'\n", Dev);
        Assert.Contains("\n    if: github.ref == 'refs/heads/master'\n", Prod);
        Assert.Contains("environment:\n      name: develop\n      url: https://avatar-dev.fara-labs.com\n", Dev);
        Assert.Contains("environment:\n      name: production\n      url: https://avatar.fara-labs.com\n", Prod);
        Assert.Matches(@"concurrency:\n  group: deploy\n(?:  #.*\n)?  cancel-in-progress: false", Prod);
        // build は何も公開しない（wrangler は dry-run だけ。Cloudflare の鍵と environment を使わない）
        foreach (Match m in Regex.Matches(Build, @"wrangler@\S+ deploy[^\n]*"))
            Assert.Contains("--dry-run", m.Value);
        Assert.DoesNotContain("secrets.", Build);
        Assert.DoesNotContain("environment:", Build);
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
        // 確認用のファイルに本番の名前は出てこない。本番のファイルに確認用の名前は出てこない
        Assert.Single(Regex.Matches(Dev, @"wrangler@4\.149\.0 deploy --env dev ""\$\{vars\[@\]\}"""));
        Assert.Single(Regex.Matches(Prod, @"wrangler@4\.149\.0 deploy --env production ""\$\{vars\[@\]\}"""));
        Assert.DoesNotContain("production", Dev);
        Assert.DoesNotContain("fara-avatar\n", Dev);
        Assert.DoesNotContain("--env dev", Prod);
        Assert.DoesNotContain("--env develop", Prod);
        Assert.DoesNotContain("avatar-dev", Prod);
        Assert.Contains("WORKER_NAME: fara-avatar-dev\n", Dev);
        Assert.Contains("WORKER_NAME: fara-avatar\n", Prod);
    }

    [Fact]
    public void Each_deploy_workflow_builds_only_its_own_environment_and_verifies_it_before_deploying()
    {
        // build: 本番用と確認用を別のフォルダに作って確かめる。--dev-origin は付けない（devOrigin は tools/site.json の avatar-dev）
        Assert.Contains("--template publish/wwwroot/index.html --out site-production --env production", Build);
        Assert.Contains("--template publish/wwwroot/index.html --out site-develop --env develop", Build);
        Assert.Contains("node .github/scripts/verify-site.mjs site-production production", Build);
        Assert.Contains("node .github/scripts/verify-site.mjs site-develop develop", Build);
        // 公開: その環境だけを dist に作り、公開の直前に確かめる。確認は、メンテナンスの確認・公開より前
        Assert.Contains("--template publish/wwwroot/index.html --out dist --env develop", Dev);
        Assert.Contains("node .github/scripts/verify-site.mjs dist develop\n", Dev);
        AssertOrder(Dev, "Publish web (WebAssembly)", "Build static pages (develop)", "Verify the output matches the target", "Deploy to Cloudflare Workers (develop)", "Summary (maintenance)");
        Assert.Contains("--template publish/wwwroot/index.html --out dist --env production", Prod);
        Assert.Contains("node .github/scripts/verify-site.mjs dist production\n", Prod);
        AssertOrder(Prod, "Publish web (WebAssembly)", "Build static pages (production)", "Verify the output matches the target", "Require MAINTENANCE_MODE=notice (production)", "Deploy to Cloudflare Workers", "Summary (maintenance)");
        foreach (var yml in new[] { Build, Dev, Prod })
        {
            Assert.DoesNotContain("--dev-origin", yml);
            // 確認用の noindex を追記するステップは無い（Build が環境ごとに出す。追記すると二重になる）
            Assert.DoesNotContain("Add noindex header", yml);
            Assert.DoesNotContain("cat >> dist/_headers", yml);
            Assert.Contains("submodules: true", yml);
        }
    }

    [Fact]
    public void Ci_dry_runs_wrangler_for_both_environments_against_their_own_output()
    {
        var production = Step(Build, "Wrangler dry-run (production)");
        Assert.Contains("rm -rf dist && cp -r site-production dist", production);
        Assert.Contains("npx --yes wrangler@4.149.0 deploy --dry-run --env production", production);
        var dev = Step(Build, "Wrangler dry-run (dev)");
        Assert.Contains("rm -rf dist && cp -r site-develop dist", dev);
        Assert.Contains("npx --yes wrangler@4.149.0 deploy --dry-run --env dev", dev);
        // dry-run は、出力が環境のものか確かめたあと
        AssertOrder(Build, "Verify the outputs (production, develop)", "Wrangler dry-run (production)", "Wrangler dry-run (dev)");
    }

    [Fact]
    public void Ci_no_longer_exports_or_compares_ssr_and_still_runs_the_tests()
    {
        var yml = Build;
        foreach (var gone in new[] { "--export", "dist-ssr", "ssr-develop", "ref: develop", "compare-text", "nav.test", "Export static site" })
            Assert.DoesNotContain(gone, yml);
        foreach (var kept in new[]
        {
            "dotnet build -c Release", "dotnet test -c Release --no-build", "external/fara-blazor-component/worker/report.test.mjs", "external/fara-blazor-component/worker/maintenance.test.mjs", "worker/index.test.mjs",
            ".github/scripts/require-maintenance.test.mjs", ".github/scripts/verify-site.test.mjs", "tests/js/storage-migrate.test.mjs", "tests/js/head-sync.test.mjs",
            "dotnet publish src/FaraAvatarModdingGuide.Web -c Release", "submodules: true", "tests/js/search-norm.mjs site-production/data/search.json",
        })
            Assert.Contains(kept, yml);
        // 書いてある node のテストが、実在する
        foreach (Match m in Regex.Matches(yml, @"(?<![\w/.-])((?:tests/js|worker|\.github/scripts|external/fara-blazor-component/worker)/[\w./-]+\.(?:test\.)?mjs)"))
            Assert.True(File.Exists(Path.Combine(TestData.Root, m.Groups[1].Value)), m.Groups[1].Value);
    }

    // ------------------------------------------------------------------ メンテナンス（MAINTENANCE_MODE）

    [Fact]
    public void Production_deploy_requires_MAINTENANCE_MODE_notice_unless_a_mode_is_given()
    {
        // 本番の公開の前に、Cloudflare の MAINTENANCE_MODE が notice かを確かめる（確認用は確かめない）。
        // mode を true / notice / false で指定した手動実行（maintenance-cli の set）は、確認を飛ばす
        var gate = Step(Prod, "Require MAINTENANCE_MODE=notice (production)");
        Assert.Contains("node .github/scripts/require-maintenance.mjs fara-avatar", gate);
        Assert.Contains("if: ${{ !contains(fromJSON('[\"true\",\"notice\",\"false\"]'), inputs.maintenance_mode) }}", gate);
        Assert.Contains("SKIP_MAINTENANCE_CHECK: ${{ inputs.skip_maintenance_check }}", gate);
        Assert.Contains("ALLOW_MISSING_WORKER: ${{ inputs.first_publish && '1' || '' }}", gate);
        Assert.Contains("skip_maintenance_check:", Prod);
        Assert.Contains("first_publish:", Prod);
        Assert.DoesNotContain("first_publish:", Dev);
        Assert.DoesNotContain("require-maintenance.mjs", Dev);
        foreach (var yml in new[] { Dev, Prod })
        {
            // 旧名 MAINTENANCE（_MODE なし）は使わない
            Assert.DoesNotContain("MAINTENANCE:", yml.Replace("MAINTENANCE_MODE:", "").Replace("MAINTENANCE_MESSAGE:", ""));
            Assert.DoesNotMatch(@"MAINTENANCE(?![_A-Z])", yml);
        }
    }

    [Fact]
    public void The_gate_lets_a_missing_production_worker_through_so_the_first_release_is_created_under_maintenance()
    {
        var gate = Read(".github/scripts/require-maintenance.mjs");
        Assert.Contains("if (settings.missing)", gate);
        Assert.Contains("初回の公開", gate);
        Assert.Contains("return 0;", gate[gate.IndexOf("if (settings.missing)", StringComparison.Ordinal)..]);
        // 既定では止める（404 は Worker 名の誤りでも起きる）。通すのは ALLOW_MISSING_WORKER のときだけ
        Assert.Contains("allowsMissingWorker(env)", gate);
        Assert.Contains("ALLOW_MISSING_WORKER", gate);
        // 通した公開は MAINTENANCE_MODE:true を付ける（入力なし・skip でないとき）。実際の引数は下の bash のテストが確かめる
        Assert.Contains("vars+=(--var \"MAINTENANCE_MODE:true\")", Prod);
    }

    [Fact]
    public void Deploy_workflows_take_maintenance_inputs_and_write_a_summary()
    {
        foreach (var yml in new[] { Dev, Prod })
        {
            Assert.Contains("      maintenance_mode:\n", yml);
            Assert.Contains("options: [\"\", \"true\", \"notice\", \"false\"]", yml);
            Assert.Contains("      maintenance_message:\n", yml);
            Assert.Contains("MAINTENANCE_MODE_INPUT: ${{ inputs.maintenance_mode }}", yml);
            Assert.Contains("MAINTENANCE_MESSAGE_INPUT: ${{ inputs.maintenance_message }}", yml);
            var summary = Step(yml, "Summary (maintenance)");
            Assert.Contains("$GITHUB_STEP_SUMMARY", summary);
            Assert.Contains("MAINTENANCE_MODE", summary);
            Assert.Contains("if: ${{ always() }}", summary);
            // 文面の検査は、--var に渡す前の独立したステップ。Summary と中身は、共通のスクリプト（develop と同じ）
            Assert.Contains("node .github/scripts/maintenance-deploy.mjs check-message", Step(yml, "Check the maintenance message"));
            Assert.Contains("node .github/scripts/maintenance-deploy.mjs summary", summary);
            Assert.Contains("MAINTENANCE_MESSAGE_INPUT: ${{ inputs.maintenance_message }}", Step(yml, "Check the maintenance message"));
        }
        foreach (var yml in new[] { Build, Dev, Prod })
        {
            // 入力はシェルに直接埋め込まない（run の中に ${{ を書かない）
            foreach (var run in RunBlocks(yml)) Assert.DoesNotContain("${{", run);
            // node -e '...' の中に、シングルクォートを書かない(bash の引用が途中で閉じて、構文エラーで落ちる。JS の中では \u0027 と書く)。閉じの引用符の直後が、グループの } であること
            foreach (var run in RunBlocks(yml).Where(r => r.Contains("node -e '", StringComparison.Ordinal)))
                Assert.Matches(@"node -e '[^']*'\s*\n\s*\} >> ""\$GITHUB_STEP_SUMMARY""", run);
        }
    }

    [Fact]
    public void Gate_script_exists_and_is_tested_in_ci()
    {
        Assert.True(File.Exists(Path.Combine(TestData.Root, ".github", "scripts", "require-maintenance.mjs")));
        Assert.True(File.Exists(Path.Combine(TestData.Root, ".github", "scripts", "verify-site.mjs")));
        Assert.Contains("require-maintenance.test.mjs", Build);
        Assert.Contains("verify-site.test.mjs", Build);
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
    public void Report_rate_limiter_is_bound_and_migrated_in_every_environment()
    {
        // 報告の 1 日の送信回数の制限（kit の ReportRateLimiter）。本番・確認用・トップレベルの 3 か所に、バインディングと migrations（v2）を書く
        var toml = Toml;
        Assert.Matches(@"(?m)^\[\[durable_objects\.bindings\]\]\nname = ""REPORT_LIMITER""\nclass_name = ""ReportRateLimiter""$", toml);
        Assert.Matches(@"(?m)^\[\[migrations\]\]\ntag = ""v2""\nnew_sqlite_classes = \[""ReportRateLimiter""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.durable_objects\.bindings\]\]\nname = ""REPORT_LIMITER""\nclass_name = ""ReportRateLimiter""$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.migrations\]\]\ntag = ""v2""\nnew_sqlite_classes = \[""ReportRateLimiter""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.dev\.durable_objects\.bindings\]\]\nname = ""REPORT_LIMITER""\nclass_name = ""ReportRateLimiter""$", toml);
        Assert.Matches(@"(?m)^\[\[env\.dev\.migrations\]\]\ntag = ""v2""\nnew_sqlite_classes = \[""ReportRateLimiter""\]$", toml);
        // 既存の Room の migration（v1）は変えない。tag の重複も無い
        Assert.Equal(3, Regex.Matches(toml, @"tag = ""v1""").Count);
        Assert.Equal(3, Regex.Matches(toml, @"tag = ""v2""").Count);
        Assert.Equal(3, Regex.Matches(toml, @"tag = ""v3""").Count);
    }

    [Fact]
    public void Room_directory_is_bound_and_migrated_in_every_environment_without_touching_v1_or_v2()
    {
        // 練習の部屋の一覧（RoomDirectory。固定名の 1 つの Durable Object）。本番・確認用・トップレベルの 3 か所に、バインディングと migrations（v3）を書く
        var toml = Toml;
        Assert.Matches(@"(?m)^\[\[durable_objects\.bindings\]\]\nname = ""ROOM_DIRECTORY""\nclass_name = ""RoomDirectory""$", toml);
        Assert.Matches(@"(?m)^\[\[migrations\]\]\ntag = ""v3""\nnew_sqlite_classes = \[""RoomDirectory""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.durable_objects\.bindings\]\]\nname = ""ROOM_DIRECTORY""\nclass_name = ""RoomDirectory""$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.migrations\]\]\ntag = ""v3""\nnew_sqlite_classes = \[""RoomDirectory""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.dev\.durable_objects\.bindings\]\]\nname = ""ROOM_DIRECTORY""\nclass_name = ""RoomDirectory""$", toml);
        Assert.Matches(@"(?m)^\[\[env\.dev\.migrations\]\]\ntag = ""v3""\nnew_sqlite_classes = \[""RoomDirectory""\]$", toml);
        // 既存の Room（v1）と ReportRateLimiter（v2）の migration は、そのまま残る（同じ tag を新しいクラスに付け替えない）
        Assert.Matches(@"(?m)^\[\[env\.production\.migrations\]\]\ntag = ""v1""\nnew_sqlite_classes = \[""Room""\]$", toml);
        Assert.Matches(@"(?m)^\[\[env\.production\.migrations\]\]\ntag = ""v2""\nnew_sqlite_classes = \[""ReportRateLimiter""\]$", toml);
    }

    [Fact]
    public void Worker_exports_the_report_rate_limiter_durable_object_from_the_kit()
    {
        var js = Read("worker/index.js");
        Assert.Contains("export { ReportRateLimiter } from \"../external/fara-blazor-component/worker/report-rate-limit.js\";", js);
        // 練習の部屋の一覧の Durable Object も、Worker の本体から export する（wrangler の class_name と一致させる）
        Assert.Contains("export class RoomDirectory extends DurableObject", js);
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
        // 共通部品（fara-blazor-component）のファイルを使う。メンテナンスの判定・報告の受け口は、各サイトにコピーを持たない
        Assert.Contains("import { getMaintenance, maintenanceResponse } from \"../external/fara-blazor-component/worker/maintenance.js\";", js);
        Assert.Contains("import { handleReport } from \"../external/fara-blazor-component/worker/report.js\";", js);
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
        var module = Read("external/fara-blazor-component/worker/maintenance.js");
        foreach (var name in new[] { "getMaintenance", "maintenanceResponse" })
            Assert.Contains($"export function {name}", module);
        Assert.Contains("twitter.com/game_fara", module);
        Assert.Contains("export async function handleReport", Read("external/fara-blazor-component/worker/report.js"));
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

    private static string DeployRun(string yml, string stepName) => RunBlocks(Step(yml, stepName)).Single();
    private static string ProdScript => DeployRun(Prod, "Deploy to Cloudflare Workers");
    private static string DevScript => DeployRun(Dev, "Deploy to Cloudflare Workers (develop)");

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
        ["GITHUB_REF"] = gitRef ?? (env == "production" ? "refs/heads/master" : "refs/heads/develop"),
        ["MAINTENANCE_MODE_INPUT"] = mode, ["MAINTENANCE_MESSAGE_INPUT"] = message, ["SKIP_MAINTENANCE_CHECK"] = skip,
    };

    private static string[] Expected(string env, params string[] vars) => new[] { "--yes", "wrangler@4.149.0", "deploy", "--env", env }.Concat(vars).ToArray();

    [Fact]
    public void Production_deploy_step_passes_the_right_vars_for_each_trigger()
    {
        var script = ProdScript;
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
        var script = DevScript;
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
        // ブランチの取り違え（本番を master 以外から・dev を develop 以外から）は、wrangler を呼ぶ前に止まる
        var script = env == "production" ? ProdScript : DevScript;
        if (Run(script, Inputs(env, gitRef: gitRef)) is not { } r) return;
        Assert.NotEqual(0, r.Exit);
        Assert.Empty(r.Args);                                                         // npx（wrangler）は呼ばれていない
        Assert.Contains("::error::", r.Stderr);
    }
}
