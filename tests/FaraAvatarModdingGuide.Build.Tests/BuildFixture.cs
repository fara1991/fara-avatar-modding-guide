using FaraAvatarModdingGuide.Models;
using System.Diagnostics;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Data;
using Xunit;

namespace FaraAvatarModdingGuide.Build.Tests;

/// <summary>リポジトリのルート（Content/ と appsettings.json がある場所）。</summary>
internal static class Repo
{
    public static readonly string Root = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")) && Directory.Exists(Path.Combine(dir.FullName, "Content"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("リポジトリのルートが見つかりません");
    }

    public static bool NodeAvailable()
    {
        try
        {
            var info = new ProcessStartInfo("node", "--version") { RedirectStandardOutput = true, UseShellExecute = false };
            using var p = Process.Start(info)!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
}

/// <summary>
/// テスト用のテンプレート（dotnet publish の index.html の代わり）。印（SEO-HEAD / SEO-BODY / WASM-ONLY）の形は Web の wwwroot/index.html と同じ。
/// </summary>
internal static class TestTemplate
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="ja" data-bs-theme="light">
        <head>
            <meta charset="utf-8" />
            <!-- SEO-HEAD:START -->
            <title>x</title>
            <!-- SEO-HEAD:END -->
            <base href="/" />
            <script src="_content/FaraSiteKit.Razor/js/theme-init.js" data-storage-key="avatar-modding-guide.theme" data-sidebar-key="avatar-modding-guide.sidebar-hidden"></script>
            <!-- WASM-ONLY:START -->
            <style>#boot-indicator { position: fixed; }</style>
            <!-- WASM-ONLY:END -->
        </head>
        <body>
            <!-- WASM-ONLY:START -->
            <div id="boot-indicator" class="boot-indicator"><div class="boot-indicator-spinner" aria-hidden="true"></div><span class="boot-indicator-label"></span></div>
            <!-- WASM-ONLY:END -->
            <div id="app">
                <!-- SEO-BODY:START -->
                <!-- SEO-BODY:END -->
            </div>
            <!-- WASM-ONLY:START -->
            <script src="_framework/blazor.webassembly.js"></script>
            <!-- WASM-ONLY:END -->
        </body>
        </html>
        """;
}

/// <summary>Build を 1 回だけ実行して、出力のフォルダを共有する（本番用と確認用）。Node が無い環境では Available が false。</summary>
public sealed class BuildFixture : IDisposable
{
    private readonly string _root;

    public bool Available { get; }
    public string Production { get; }
    public string Develop { get; }
    public ContentService Content { get; }

    public BuildFixture()
    {
        Content = new ContentService(Path.Combine(Repo.Root, "Content"));
        _root = Path.Combine(Path.GetTempPath(), "avatar-build-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Production = Path.Combine(_root, "production");
        Develop = Path.Combine(_root, "develop");
        Available = Repo.NodeAvailable() && File.Exists(Path.Combine(Repo.Root, "external", "fara-site-kit", "build", "build-pages.mjs"));
        if (!Available) return;

        Directory.CreateDirectory(_root);
        var template = Path.Combine(_root, "index.html");
        File.WriteAllText(template, TestTemplate.Html);
        SiteBuilder.Run(new BuildOptions(Repo.Root, template, Production, "production"));
        SiteBuilder.Run(new BuildOptions(Repo.Root, template, Develop, "develop", DevOrigin: "https://avatar-wasm-dev.fara-labs.com"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* 後始末の失敗でテストを落とさない */ }
    }
}

/// <summary>
/// Node（と kit）が無い環境では、Build の通しのテストを飛ばす。ただし CI（環境変数 CI）では飛ばさず、失敗させる
/// （サブモジュールの取得漏れで、テストが黙って飛ばされないように）。
/// </summary>
public sealed class NodeFactAttribute : FactAttribute
{
    public NodeFactAttribute()
    {
        var ci = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
        if (!ci && (!Repo.NodeAvailable() || !File.Exists(Path.Combine(Repo.Root, "external", "fara-site-kit", "build", "build-pages.mjs"))))
            Skip = "Node.js か external/fara-site-kit（git submodule update --init）がありません";
    }
}
