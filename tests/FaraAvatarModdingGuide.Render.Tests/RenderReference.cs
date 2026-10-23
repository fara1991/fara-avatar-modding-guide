using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FaraAvatarModdingGuide.Practice.Tests;

namespace FaraAvatarModdingGuide.Render.Tests;

/// <summary>
/// 現行の viewport.js(three.js)が作るシーンの中身(ジオメトリ・色・行列・カメラの行列)を Node で取り出した結果。
/// 入力は Practice.Tests と同じ tests/js/practice-cases.json・practice-edit-cases.json。tests/js/practice-render-reference.mjs が、
/// 本物の Viewport に操作列を通し、手順 0・40 手順ごと・最後で、シーンを読む。Node が無いと失敗する(飛ばさない)。
/// </summary>
internal sealed class RenderReferenceRun
{
    public const int Every = 40;

    private readonly string casesFile;
    private readonly Lazy<JsonElement> output;

    public RenderReferenceRun(string casesFileName)
    {
        casesFile = Path.Combine(JsReference.JsDir, casesFileName);
        output = new Lazy<JsonElement>(Run);
    }

    public JsonElement Output => output.Value;

    private JsonElement Run()
    {
        // 稀に Node が起動の途中で落ちる(メモリ不足の V8 の致命的エラー)ので 3 回まで試す(Practice.Tests の ReferenceRun と同じ)
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
        var work = Path.Combine(Path.GetTempPath(), "avatar-render-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var outFile = Path.Combine(work, "render.json");
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            info.ArgumentList.Add("--max-old-space-size=4096");
            foreach (var arg in new[] { Path.Combine(JsReference.JsDir, "practice-render-reference.mjs"), casesFile, outFile, Every.ToString() }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("node を起動できません(CI では actions/setup-node が要ります)");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return (false, $"practice-render-reference.mjs が失敗しました(終了コード {process.ExitCode}):" + Environment.NewLine + stderr.Result + stdout.Result, default);
            return (true, "", JsonDocument.Parse(File.ReadAllText(outFile, Encoding.UTF8)).RootElement.Clone());
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }
}

internal static class RenderReference
{
    public static readonly RenderReferenceRun Basic = new("practice-cases.json");
    public static readonly RenderReferenceRun Edit = new("practice-edit-cases.json");
}
