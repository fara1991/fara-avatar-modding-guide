using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 確認用 Worker（fara-avatar-wasm-dev）への公開の設定。migration/wasm の push が、本番や develop の確認用に出てしまわないこと
/// （計画書 D7・D14: master は触らない）を、設定のファイルで確かめる。
/// </summary>
public class DeployConfigTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(TestData.Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Workflow => Read(".github/workflows/deploy-wasm-dev.yml");
    private static string Toml => Read("wrangler.toml");

    [Fact]
    public void Wasm_dev_workflow_runs_only_for_the_migration_branch_and_never_touches_production()
    {
        var yml = Workflow;
        Assert.Matches(@"(?ms)^on:\s*\n\s*push:\s*\n\s*branches: \[migration/wasm\]", yml);
        Assert.Contains("if: github.ref == 'refs/heads/migration/wasm'", yml);     // 手動実行で別のブランチを選んでも公開しない
        Assert.Contains("name: wasm-dev", yml);
        Assert.Contains("url: https://avatar-wasm-dev.fara-labs.com/", yml);
        Assert.Contains("deploy --env wasm-dev \"${vars[@]}\"", yml);                // 実際に渡る引数は MaintenanceConfigTests が bash で確かめる
        Assert.DoesNotContain("--env production", yml);
        Assert.DoesNotContain("--env dev", yml.Replace("--env develop", ""));       // develop（avatar-dev）の確認用にも出さない
        Assert.DoesNotContain("MAINTENANCE_MODE:true", yml);                       // push では --var を付けない（確認用なので、メンテナンス画面にしない）。入力があるときだけ付く
    }

    [Fact]
    public void Wasm_dev_workflow_pins_wrangler_gets_the_submodule_and_builds_for_the_wasm_dev_origin()
    {
        var yml = Workflow;
        Assert.Contains("wrangler@4.149.0", yml);                                      // deploy.yml と同じ版
        Assert.Contains("submodules: true", yml);                                     // kit（Build・Web・Worker が使う）
        Assert.Contains("node-version: 22", yml);
        Assert.Contains("--env develop --dev-origin https://avatar-wasm-dev.fara-labs.com", yml);
        Assert.Contains("--template publish/wwwroot/index.html --out publish/wwwroot", yml);
        // 公開の前にテスト。公開は、Web の publish と Build の後
        Assert.DoesNotContain("\"**.md\"", yml);                                      // 記事（Content/learn/**/*.md）の変更も公開する
        Assert.Contains("ref: develop", yml);                                            // 比べる SSR は develop のコード（同じコミットの SSR は Content を共有していて、食い違いが見えない）
        Assert.Contains("compare-text.mjs dist-ssr publish/wwwroot", yml);               // 本文の一致を確かめてから公開
        var test = yml.IndexOf("dotnet test", StringComparison.Ordinal);
        var publish = yml.IndexOf("dotnet publish src/FaraAvatarModdingGuide.Web", StringComparison.Ordinal);
        var build = yml.IndexOf("src/FaraAvatarModdingGuide.Build", StringComparison.Ordinal);
        var deploy = yml.IndexOf("wrangler@4.149.0 deploy", StringComparison.Ordinal);
        Assert.True(0 < test && test < publish && publish < build && build < deploy);
        Assert.Contains("secrets.CLOUDFLARE_API_TOKEN", yml);
        Assert.Contains("vars.CLOUDFLARE_ACCOUNT_ID", yml);
    }

    [Fact]
    public void Existing_deploy_workflow_still_publishes_only_master_and_develop()
    {
        var yml = Read(".github/workflows/deploy.yml");
        Assert.Matches(@"(?ms)^on:\s*\n\s*push:\s*\n\s*branches: \[master, develop\]", yml);
        Assert.DoesNotContain("migration/wasm", yml);
        Assert.Contains("github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop'", yml);
    }

    [Fact]
    public void Wrangler_has_a_separate_wasm_dev_environment_that_mirrors_the_others()
    {
        var toml = Toml;
        var section = toml[toml.IndexOf("[env.wasm-dev]", StringComparison.Ordinal)..];
        Assert.Contains("name = \"fara-avatar-wasm-dev\"", section);
        Assert.Contains("pattern = \"avatar-wasm-dev.fara-labs.com\", custom_domain = true", section);
        Assert.Contains("directory = \"./publish/wwwroot\"", section);                // dotnet publish の出力に Build が書き足したもの
        Assert.Contains("html_handling = \"auto-trailing-slash\"", section);
        Assert.Contains("not_found_handling = \"404-page\"", section);
        Assert.Contains("run_worker_first = true", section);
        Assert.Contains("workers_dev = false", section);
        Assert.Contains("preview_urls = false", section);
        Assert.Contains("class_name = \"Room\"", section);                            // 共同練習の部屋
        Assert.Contains("new_sqlite_classes = [\"Room\"]", section);                  // 新しい Worker に Durable Object を作る
        Assert.DoesNotContain("MAINTENANCE_MODE", section);                           // 変数は付けない（公開してもメンテナンス画面にならない）
        // 本番・develop の設定は変えていない
        Assert.Contains("pattern = \"avatar.fara-labs.com\"", toml);
        Assert.Contains("pattern = \"avatar-dev.fara-labs.com\"", toml);
        Assert.Matches(@"(?m)^name = ""fara-avatar""$", toml);
        Assert.Matches(@"(?m)^name = ""fara-avatar-dev""$", toml);
        Assert.Matches(@"(?m)^keep_vars = true$", toml);                              // ダッシュボードで足した変数（問い合わせの鍵など）を消さない
    }

    [Fact]
    public void Site_config_keeps_the_dev_origin_for_after_the_switch_and_the_workflow_overrides_it()
    {
        // 移行後の確認用は avatar-dev。migration/wasm の確認用（wasm-dev）の canonical は、Build の --dev-origin で上書きする
        var site = Read("tools/site.json");
        Assert.Contains("\"devOrigin\": \"https://avatar-dev.fara-labs.com\"", site);
        Assert.Contains("\"origin\": \"https://avatar.fara-labs.com\"", site);
    }
}
