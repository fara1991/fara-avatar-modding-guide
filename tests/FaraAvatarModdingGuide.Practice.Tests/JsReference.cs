using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の JavaScript(wwwroot/js/viewport.js・practice.js・editmesh.js)を Node で動かした結果。入力は tests/js/practice-cases.json(S0)と practice-edit-cases.json(S1)(C# も同じものを読む)。
/// tests/js/practice-reference.mjs が、本物の Viewport のコンストラクタとハンドラーに模擬のイベントを渡して結果を出す(SearchParityTests と同じ方式)。
/// Node が無いと失敗する(飛ばさない)。
/// </summary>
internal static class JsReference
{
    public static readonly string Root = FindRoot();
    public static readonly string JsDir = Path.Combine(Root, "tests", "js");

    /// <summary>S0: オブジェクトモード・お題・形。</summary>
    public static readonly ReferenceRun Basic = new("practice-cases.json");

    /// <summary>S1: 編集モードを含む操作列・取り消し・メニュー・N パネル・共同練習の口・お題の通し。</summary>
    public static readonly ReferenceRun Edit = new("practice-edit-cases.json");

    public static JsonElement Cases => Basic.Cases;

    /// <summary>practice-reference.mjs の出力(shapes・taskTexts・tasks・taskSequences・scenarios)。</summary>
    public static JsonElement Output => Basic.Output;

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FaraAvatarModdingGuide.sln"))) return dir.FullName;
        throw new InvalidOperationException("リポジトリのルート(FaraAvatarModdingGuide.sln)が見つかりません");
    }
}

/// <summary>入力ファイル 1 つと、それを JavaScript に通した結果。</summary>
internal sealed class ReferenceRun
{
    private readonly string casesFile;
    private readonly Lazy<JsonElement> cases;
    private readonly Lazy<JsonElement> output;

    public ReferenceRun(string casesFileName)
    {
        casesFile = Path.Combine(JsReference.JsDir, casesFileName);
        cases = new Lazy<JsonElement>(() => JsonDocument.Parse(File.ReadAllText(casesFile, Encoding.UTF8)).RootElement);
        output = new Lazy<JsonElement>(Run);
    }

    public JsonElement Cases => cases.Value;

    public JsonElement Output => output.Value;

    private JsonElement Run()
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

    private (bool Ok, string Error, JsonElement Result) RunOnce()
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
            // 出力は数十 MB になることがあるので、Node の既定のメモリの上限を上げておく
            info.ArgumentList.Add("--max-old-space-size=4096");
            foreach (var arg in new[] { Path.Combine(JsReference.JsDir, "practice-reference.mjs"), casesFile, outFile }) info.ArgumentList.Add(arg);
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
