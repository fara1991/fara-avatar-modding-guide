using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>突き合わせのケース 1 件(入力の手順列)を、C# の ViewportCore に通して、JavaScript の出力と手順ごとに比べる。</summary>
internal static class ScenarioRunner
{
    /// <summary>
    /// 期待の状態。JavaScript の出力はメッシュの中身を「前の手順から変わったものだけ」持つので、無いものは前の手順の値で補う
    /// (practice-reference.mjs の snapshot の lastMesh と同じ添字・同じ切り詰め)。
    /// </summary>
    private static JsonNode Expected(JsonElement e, List<JsonNode?> last)
    {
        var node = JsonNode.Parse(e.GetRawText())!;
        var objs = node["objects"]!.AsArray();
        if (last.Count > objs.Count) last.RemoveRange(objs.Count, last.Count - objs.Count);
        while (last.Count < objs.Count) last.Add(null);
        for (var i = 0; i < objs.Count; i++)
        {
            var o = objs[i]!.AsObject();
            if (o["mesh"] is { } m) last[i] = m.DeepClone();
            else o["mesh"] = last[i]?.DeepClone();
        }
        return node;
    }

    private static void Check(string what, JsonElement expected, JsonNode actual, List<JsonNode?> last)
    {
        var diff = JsonCompare.Diff(Expected(expected, last), actual);
        Assert.True(diff.Count == 0, what + Environment.NewLine + string.Join(Environment.NewLine, diff));
    }

    /// <summary>ケースを最初から最後まで通す。通した手順の数を返す。</summary>
    public static int Run(JsonElement c, JsonElement js)
    {
        var name = c.GetProperty("name").GetString();
        var session = ViewportDriver.Build(c);
        var expectedSteps = js.GetProperty("steps");
        var last = new List<JsonNode?>();
        Check($"{name}: 最初の状態", expectedSteps[0], ViewportDriver.Snapshot(session), last);
        var steps = c.GetProperty("steps");
        for (var s = 0; s < steps.GetArrayLength(); s++)
        {
            var prevented = ViewportDriver.Apply(session, steps[s]);
            Check($"{name}: 手順 {s + 1}/{steps.GetArrayLength()} {steps[s].GetRawText()}", expectedSteps[s + 1], ViewportDriver.Snapshot(session, prevented), last);
        }
        var probes = c.TryGetProperty("probes", out var p) ? p : default;
        if (probes.ValueKind == JsonValueKind.Array)
            for (var k = 0; k < probes.GetArrayLength(); k++)
            {
                var diff = JsonCompare.Diff(js.GetProperty("probes")[k], ViewportDriver.Probe(session.Core, probes[k]));
                Assert.True(diff.Count == 0, $"{name}: 確認 {k}(レイ・平面・ピック・投影)" + Environment.NewLine + string.Join(Environment.NewLine, diff));
            }
        return steps.GetArrayLength();
    }
}
