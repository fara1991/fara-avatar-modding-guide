using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// C# の練習ロジックが、現行の JavaScript(viewport.js・practice.js・editmesh.js)と同じ結果になることの突き合わせ。
/// 同じ入力(tests/js/practice-cases.json)を、JS は Node で本物のコードに通し(tests/js/practice-reference.mjs)、C# は ViewportCore などに通して、
/// 位置・回転・スケール・選択・視点・カメラ・状態表示の文字・イベント・お題の判定を比べる。
/// </summary>
public class ParityTests
{
    private static JsonElement Reference => JsReference.Output;
    private static JsonElement Cases => JsReference.Cases;

    private static void AssertNoDiff(string what, JsonElement expected, JsonNode? actual)
    {
        var diff = JsonCompare.Diff(expected, actual);
        Assert.True(diff.Count == 0, what + Environment.NewLine + string.Join(Environment.NewLine, diff));
    }

    // ---------------------------------------------------------------- 形
    [Fact]
    public void Primitive_shapes_have_the_same_vertices_faces_and_triangles()
    {
        foreach (var kind in MeshShapes.Kinds)
        {
            var js = Reference.GetProperty("shapes").GetProperty(kind);
            var mesh = MeshShapes.Create(kind);
            var actual = new JsonObject
            {
                ["v"] = new JsonArray(mesh.Verts.Select(v => (JsonNode)new JsonArray(v.X, v.Y, v.Z)).ToArray()),
                ["f"] = new JsonArray(mesh.Faces.Select(f => (JsonNode)new JsonArray(f.Select(i => (JsonNode)i).ToArray())).ToArray()),
                ["tri"] = new JsonArray(mesh.Triangles().SelectMany(v => new[] { v.X, v.Y, v.Z }).Select(d => (JsonNode)d).ToArray()),
            };
            // 三角形は three.js の Float32 の配列と同じ丸め(JS の出力は丸める前の値)なので、float の誤差まで許す
            var vertsAndFaces = new JsonObject { ["v"] = actual["v"]!.DeepClone(), ["f"] = actual["f"]!.DeepClone() };
            var jsVertsAndFaces = JsonDocument.Parse($"{{\"v\":{js.GetProperty("v").GetRawText()},\"f\":{js.GetProperty("f").GetRawText()}}}").RootElement;
            AssertNoDiff($"形 {kind} の頂点・面", jsVertsAndFaces, vertsAndFaces);
            var jsTri = js.GetProperty("tri");
            var cTri = (JsonArray)actual["tri"]!;
            Assert.Equal(jsTri.GetArrayLength(), cTri.Count);
            for (var i = 0; i < cTri.Count; i++)
                Assert.True(Math.Abs(jsTri[i].GetDouble() - cTri[i]!.GetValue<double>()) <= 1e-6, $"形 {kind} の三角形の座標 {i}: JS={jsTri[i].GetDouble():R} / C#={cTri[i]!.GetValue<double>():R}");
        }
    }

    // ---------------------------------------------------------------- お題
    private static SceneObject ToObject(JsonElement o)
    {
        var obj = SceneObject.Create(o.GetProperty("kind").GetString()!);
        var p = o.GetProperty("p");
        obj.Position = new Vec3(p[0].GetDouble(), p[1].GetDouble(), p[2].GetDouble());
        var r = o.GetProperty("r");
        obj.Quaternion = Quat.FromEulerXYZ(r[0].GetDouble(), r[1].GetDouble(), r[2].GetDouble());
        var s = o.GetProperty("s");
        obj.Scale = new Vec3(s[0].GetDouble(), s[1].GetDouble(), s[2].GetDouble());
        return obj;
    }

    private static TaskContext ToContext(JsonElement c)
    {
        var objects = c.GetProperty("objects").EnumerateArray().Select(ToObject).ToList();
        var flags = c.GetProperty("flags");
        return new TaskContext(
            objects, [.. c.GetProperty("selected").EnumerateArray().Select(i => objects[i.GetInt32()])],
            c.GetProperty("viewName").GetString()!, c.GetProperty("ortho").GetBoolean(), c.GetProperty("mode").GetString()!, c.GetProperty("selectMode").GetString()!,
            [.. c.GetProperty("faces").EnumerateArray().Select(f => new Vec3(f[0].GetDouble(), f[1].GetDouble(), f[2].GetDouble()))],
            new TaskFlags
            {
                UndoUsed = flags.GetProperty("undoUsed").GetBoolean(), SphereAdded = flags.GetProperty("sphereAdded").GetBoolean(), Extruded = flags.GetProperty("extruded").GetBoolean(),
                Inset = flags.GetProperty("inset").GetBoolean(), Loopcut = flags.GetProperty("loopcut").GetBoolean(), Bevel = flags.GetProperty("bevel").GetBoolean(),
            });
    }

    [Fact]
    public void Task_texts_match_practice_js()
    {
        Assert.Equal(Reference.GetProperty("taskTexts").EnumerateArray().Select(t => t.GetString()), PracticeTasks.Tasks.Select(t => t.Text));
    }

    [Fact]
    public void Every_task_check_matches_practice_js_for_every_state()
    {
        var js = Reference.GetProperty("tasks");
        var cases = Cases.GetProperty("tasks");
        Assert.True(cases.GetArrayLength() >= 300);
        for (var i = 0; i < cases.GetArrayLength(); i++)
        {
            var context = ToContext(cases[i]);
            var actual = PracticeTasks.Tasks.Select(t => t.Check(context)).ToArray();
            var expected = js[i].EnumerateArray().Select(b => b.GetBoolean()).ToArray();
            Assert.True(expected.SequenceEqual(actual), $"状態 {i}: JS={string.Join(",", expected.Select(b => b ? 1 : 0))} / C#={string.Join(",", actual.Select(b => b ? 1 : 0))}");
        }
        // 満たす状態も満たさない状態も、それぞれのお題にある(判定が常に同じ値で一致している、ということではない)
        for (var t = 0; t < PracticeTasks.Tasks.Count; t++)
        {
            var column = js.EnumerateArray().Select(row => row[t].GetBoolean()).ToArray();
            Assert.True(column.Any(b => b) && column.Any(b => !b), $"お題 {t}: 両方の値が出る状態がある");
        }
    }

    [Fact]
    public void Tasks_advance_in_order_like_checkTasks()
    {
        var js = Reference.GetProperty("taskSequences");
        var sequences = Cases.GetProperty("taskSequences");
        for (var s = 0; s < sequences.GetArrayLength(); s++)
        {
            var progress = new TaskProgress();
            var steps = sequences[s];
            for (var i = 0; i < steps.GetArrayLength(); i++)
            {
                progress.Check(ToContext(steps[i]));
                var expected = js[s][i];
                Assert.Equal(expected.GetProperty("index").GetInt32(), progress.Index);
                Assert.Equal(expected.GetProperty("done").EnumerateArray().Select(b => b.GetBoolean()), progress.Done);
            }
        }
    }

    // ---------------------------------------------------------------- 3D ビューの操作
    [Fact]
    public void Viewport_operations_match_viewport_js_for_every_scenario_and_step()
    {
        var scenarios = Cases.GetProperty("scenarios");
        var js = Reference.GetProperty("scenarios");
        Assert.True(scenarios.GetArrayLength() >= 80);
        var totalSteps = 0;
        for (var i = 0; i < scenarios.GetArrayLength(); i++) totalSteps += ScenarioRunner.Run(scenarios[i], js[i]);
        Assert.True(totalSteps >= 4000, "手順の数: " + totalSteps);
    }

    [Fact]
    public void The_scenarios_cover_selection_ops_axes_numeric_cancel_camera_and_picking()
    {
        // 突き合わせの入力が、一通りの操作を通していること(一致しても、何も試していないのでは意味がない)
        var types = new HashSet<string>();
        foreach (var scenario in Reference.GetProperty("scenarios").EnumerateArray())
            foreach (var step in scenario.GetProperty("steps").EnumerateArray())
                foreach (var e in step.GetProperty("events").EnumerateArray())
                    types.Add(e.GetProperty("type").GetString() + ":" + (e.TryGetProperty("kind", out var k) ? k.GetString() : e.TryGetProperty("source", out var src) ? src.GetString() : e.TryGetProperty("action", out var a) ? a.GetString() : ""));
        foreach (var expected in new[]
        {
            "select:click", "select:shift-click", "select:box", "select:all", "select:none", "select:invert", "opstart:G", "opstart:R", "opstart:S", "axis:G", "axis:R", "axis:S",
            "numeric:G", "numeric:R", "numeric:S", "confirm:G", "confirm:R", "confirm:S", "cancel:G", "cancel:R", "cancel:S", "opfail:G",
            "view:orbit", "view:pan", "view:zoom", "view:axis", "view:ortho", "view:frame", "view:home",
        })
            Assert.Contains(expected, types);
        var picked = Reference.GetProperty("scenarios").EnumerateArray().SelectMany(s => s.GetProperty("probes").EnumerateArray()).Count(p => p.GetProperty("pick").ValueKind == JsonValueKind.String);
        Assert.True(picked >= 10, "ピックが当たる確認の数: " + picked);
    }
}
