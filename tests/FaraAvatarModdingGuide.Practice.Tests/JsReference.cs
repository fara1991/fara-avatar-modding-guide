using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の JavaScript(wwwroot/js/viewport.js・practice.js・editmesh.js)を Node で動かした結果。入力は tests/js/practice-cases.json(C# も同じものを読む)。
/// tests/js/practice-reference.mjs が、本物の Viewport のメソッドとハンドラーに模擬のイベントを渡して結果を出す(SearchParityTests と同じ方式)。
/// Node が無いと失敗する(飛ばさない)。
/// </summary>
internal static class JsReference
{
    public static readonly string Root = FindRoot();
    public static readonly string JsDir = Path.Combine(Root, "tests", "js");
    public static readonly string CasesFile = Path.Combine(JsDir, "practice-cases.json");

    public static readonly JsonElement Cases = JsonDocument.Parse(File.ReadAllText(CasesFile, Encoding.UTF8)).RootElement;

    private static readonly Lazy<JsonElement> Result = new(Run);

    /// <summary>practice-reference.mjs の出力(shapes・taskTexts・tasks・taskSequences・scenarios)。</summary>
    public static JsonElement Output => Result.Value;

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FaraAvatarModdingGuide.sln"))) return dir.FullName;
        throw new InvalidOperationException("リポジトリのルート(FaraAvatarModdingGuide.sln)が見つかりません");
    }

    private static JsonElement Run()
    {
        // 稀に Node が起動の途中で落ちる(マシンのメモリが足りないときの V8 の致命的エラー)ので、3 回まで試す。毎回落ちるなら、本当の失敗
        for (var attempt = 1; ; attempt++)
        {
            var (ok, error, result) = RunOnce();
            if (ok) return result;
            if (attempt == 3) throw new Xunit.Sdk.XunitException(error);
            Thread.Sleep(500);
        }
    }

    private static (bool Ok, string Error, JsonElement Result) RunOnce()
    {
        var work = Path.Combine(Path.GetTempPath(), "avatar-practice-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var outFile = Path.Combine(work, "reference.json");
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var arg in new[] { Path.Combine(JsDir, "practice-reference.mjs"), CasesFile, outFile }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("node を起動できません(CI では actions/setup-node が要ります)");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return (false, $"practice-reference.mjs が失敗しました(終了コード {process.ExitCode}):" + Environment.NewLine + stderr.Result + stdout.Result, default);
            return (true, "", JsonDocument.Parse(File.ReadAllText(outFile, Encoding.UTF8)).RootElement.Clone());
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }
}
