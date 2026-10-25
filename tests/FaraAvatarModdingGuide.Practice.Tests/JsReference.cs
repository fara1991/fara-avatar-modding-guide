using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の JavaScript（wwwroot/js/viewport.js・editmesh.js・practice.js）を Node で動かした結果の、凍結した基準（tests/js/golden/*.json.gz）。
/// 入力は tests/js/practice-cases.json（S0）と practice-edit-cases.json（S1）（C# も同じものを読む）。
/// 基準は、three.js の版が残っていた commit 8262089 で tests/js/practice-reference.mjs が作った出力（S7 で three.js を外したあとも比べられるように、ファイルにした）。
/// 作り直すときは、その commit の tests/js/practice-reference.mjs を使う（docs/phase3b-design.md の §17）。
/// </summary>
internal static class JsReference
{
    public static readonly string Root = FindRoot();
    public static readonly string JsDir = Path.Combine(Root, "tests", "js");

    /// <summary>S0: オブジェクトモード・お題・形。</summary>
    public static readonly ReferenceRun Basic = new("practice-cases.json", "practice-basic");

    /// <summary>S1: 編集モードを含む操作列・取り消し・メニュー・N パネル・共同練習の口・お題の通し。</summary>
    public static readonly ReferenceRun Edit = new("practice-edit-cases.json", "practice-edit");

    /// <summary>S4: チュートリアルの案内（setHighlight の軸の点線）を含む操作列。描画の突き合わせ（Render.Tests）が使う。</summary>
    public static readonly ReferenceRun Tutorial = new("practice-tutorial-render-cases.json", "practice-tutorial-render");

    public static JsonElement Cases => Basic.Cases;

    /// <summary>基準の出力（shapes・taskTexts・tasks・taskSequences・scenarios）。</summary>
    public static JsonElement Output => Basic.Output;

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FaraAvatarModdingGuide.sln"))) return dir.FullName;
        throw new InvalidOperationException("リポジトリのルート(FaraAvatarModdingGuide.sln)が見つかりません");
    }
}

/// <summary>凍結した基準（tests/js/golden/&lt;名前&gt;.json.gz）を読む。無ければ、作り方を書いて失敗する。</summary>
internal static class JsGolden
{
    public static JsonElement Read(string name)
    {
        var path = Path.Combine(JsReference.JsDir, "golden", name + ".json.gz");
        if (!File.Exists(path)) throw new XunitException($"基準がありません: {path}（作り方は docs/phase3b-design.md の §17）");
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        return doc.RootElement.Clone();
    }
}

/// <summary>入力ファイル 1 つと、その基準の出力。</summary>
internal sealed class ReferenceRun
{
    private readonly Lazy<JsonElement> cases;
    private readonly Lazy<JsonElement> output;

    public ReferenceRun(string casesFileName, string goldenName)
    {
        var casesFile = Path.Combine(JsReference.JsDir, casesFileName);
        cases = new Lazy<JsonElement>(() => JsonDocument.Parse(File.ReadAllText(casesFile, Encoding.UTF8)).RootElement);
        output = new Lazy<JsonElement>(() => JsGolden.Read(goldenName));
    }

    public JsonElement Cases => cases.Value;

    public JsonElement Output => output.Value;
}
