using System.Text.Json;
using FaraAvatarModdingGuide.Practice.Tests;

namespace FaraAvatarModdingGuide.Render.Tests;

/// <summary>
/// 現行の viewport.js（three.js）が作るシーンの中身（ジオメトリ・色・行列・カメラの行列）の、凍結した基準（tests/js/golden/render-*.json.gz）。
/// 入力は Practice.Tests と同じ tests/js/practice-cases.json・practice-edit-cases.json。基準は、three.js の版が残っていた commit 8262089 で
/// tests/js/practice-render-reference.mjs が作った（手順 0・40 手順ごと・最後で、シーンを読む。案内の点線は全手順）。作り方は docs/phase3b-design.md の §16。
/// </summary>
internal sealed class RenderReferenceRun
{
    public const int Every = 40;

    private readonly Lazy<JsonElement> output;

    public RenderReferenceRun(string goldenName)
    {
        output = new Lazy<JsonElement>(() => JsGolden.Read(goldenName));
    }

    public JsonElement Output => output.Value;
}

internal static class RenderReference
{
    public static readonly RenderReferenceRun Basic = new("render-basic");
    public static readonly RenderReferenceRun Edit = new("render-edit");
    /// <summary>S4: 案内の軸の点線。全手順で比べる。</summary>
    public static readonly RenderReferenceRun Tutorial = new("render-tutorial");
}
