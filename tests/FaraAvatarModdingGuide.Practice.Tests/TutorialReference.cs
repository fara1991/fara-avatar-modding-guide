using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の scenarios.js(Blender の 3D ビューのシナリオ 9 本)を、Node で本物のまま動かした結果(tests/js/tutorial-reference.mjs)。
/// 手順の定義・再生(replayTo)後のビューの状態・案内(矢印・点線)・イベントの一覧への done / fail / hint の答え。Node が無いと失敗する(飛ばさない)。
/// </summary>
internal static class TutorialReference
{
    private static readonly Lazy<JsonElement> output = new(Run);

    public static JsonElement Output => output.Value;

    private static JsonElement Run()
    {
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
        var work = Path.Combine(Path.GetTempPath(), "avatar-tutorial-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var outFile = Path.Combine(work, "tutorial.json");
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            info.ArgumentList.Add("--max-old-space-size=4096");
            foreach (var arg in new[] { Path.Combine(JsReference.JsDir, "tutorial-reference.mjs"), outFile }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("node を起動できません(CI では actions/setup-node が要ります)");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) return (false, $"tutorial-reference.mjs が失敗しました(終了コード {process.ExitCode}):" + Environment.NewLine + stderr.Result + stdout.Result, default);
            return (true, "", JsonDocument.Parse(File.ReadAllText(outFile, Encoding.UTF8)).RootElement.Clone());
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }
}
