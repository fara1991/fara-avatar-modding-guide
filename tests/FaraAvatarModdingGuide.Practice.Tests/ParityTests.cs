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
    private static ViewportCore Build(JsonElement c)
    {
        var root = c.GetProperty("root");
        var rect = new ViewRect(root.GetProperty("left").GetDouble(), root.GetProperty("top").GetDouble(), root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble());
        var emulate = c.GetProperty("emulate").GetBoolean();
        var core = new ViewportCore(rect, () => emulate);
        var view = c.GetProperty("view");
        core.View.Target = Vec(view.GetProperty("target"));
        core.View.Dist = view.GetProperty("dist").GetDouble();
        core.View.Theta = view.GetProperty("theta").GetDouble();
        core.View.Phi = view.GetProperty("phi").GetDouble();
        core.View.Ortho = view.GetProperty("ortho").GetBoolean();
        core.View.Name = view.GetProperty("name").GetString()!;
        List<SceneObject> objects = [];
        foreach (var d in c.GetProperty("objects").EnumerateArray())
        {
            var o = SceneObject.Create(d.GetProperty("kind").GetString()!, d.GetProperty("name").GetString());
            if (d.TryGetProperty("p", out var p)) o.Position = Vec(p);
            if (d.TryGetProperty("r", out var r)) o.SetRotationDegrees(r[0].GetDouble(), r[1].GetDouble(), r[2].GetDouble());
            if (d.TryGetProperty("s", out var s)) o.Scale = Vec(s);
            objects.Add(o);
        }
        core.Objects.AddRange(objects);
        core.SetSelection(c.GetProperty("selected").EnumerateArray().Select(i => objects[i.GetInt32()]));
        return core;
    }

    private static Vec3 Vec(JsonElement a) => new(a[0].GetDouble(), a[1].GetDouble(), a[2].GetDouble());

    private static Modifiers Mods(JsonElement s) => new(Flag(s, "shift"), Flag(s, "ctrl"), Flag(s, "alt"), Flag(s, "meta"));

    private static bool Flag(JsonElement s, string name) => s.TryGetProperty(name, out var v) && v.GetBoolean();

    private static double Num(JsonElement s, string name) => s.TryGetProperty(name, out var v) ? v.GetDouble() : 0;

    private static void Apply(ViewportCore core, JsonElement step)
    {
        switch (step.GetProperty("e").GetString())
        {
            case "move": core.PointerMove(Num(step, "x"), Num(step, "y"), Mods(step)); break;
            case "down": core.PointerDown((int)Num(step, "button"), Num(step, "x"), Num(step, "y"), Mods(step)); break;
            case "up": core.PointerUp(Num(step, "x"), Num(step, "y")); break;
            case "wheel": core.Wheel(Num(step, "dy")); break;
            case "key": core.KeyDown(step.GetProperty("code").GetString()!, step.GetProperty("key").GetString()!, Mods(step)); break;
            case "keyup": core.KeyUp(Mods(step)); break;
            default: throw new InvalidOperationException("未知の手順");
        }
    }

    private static JsonArray Arr(Vec3 v) => new(v.X, v.Y, v.Z);

    private static JsonNode? EventValue(object? v) => v switch
    {
        null => null,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        double d => JsonValue.Create(d),
        string[] a => new JsonArray(a.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        _ => throw new InvalidOperationException(v.GetType().Name),
    };

    private static JsonObject Snapshot(ViewportCore core)
    {
        var view = core.View;
        var (right, up, _) = view.Basis;
        var h = view.OrthoHalfHeight;
        var op = core.Op;
        return new JsonObject
        {
            ["objects"] = new JsonArray(core.Objects.Select(o => (JsonNode)new JsonObject
            {
                ["name"] = o.Name, ["p"] = Arr(o.Position), ["q"] = new JsonArray(o.Quaternion.X, o.Quaternion.Y, o.Quaternion.Z, o.Quaternion.W), ["s"] = Arr(o.Scale), ["euler"] = Arr(o.Rotation),
            }).ToArray()),
            ["selected"] = new JsonArray(core.Selected.Select(o => (JsonNode)JsonValue.Create(o.Name)!).ToArray()),
            ["active"] = core.Active?.Name,
            ["view"] = new JsonObject { ["target"] = Arr(view.Target), ["dist"] = view.Dist, ["theta"] = view.Theta, ["phi"] = view.Phi, ["name"] = view.Name },
            ["ortho"] = view.Ortho,
            ["camera"] = new JsonObject
            {
                ["position"] = Arr(view.Position), ["up"] = Arr(view.Up), ["right"] = Arr(right), ["camUp"] = Arr(up), ["dir"] = Arr(view.Forward),
                ["ortho"] = new JsonArray(-h * view.Aspect, h * view.Aspect, h, -h),
            },
            ["status"] = core.Status,
            ["numericHud"] = core.NumericHud,
            ["viewHud"] = view.Label,
            ["op"] = op is null ? null : new JsonObject { ["kind"] = op.Kind.ToString(), ["axis"] = op.Axis, ["exclude"] = op.Exclude, ["numeric"] = op.Numeric },
            ["axisLine"] = core.AxisLine.Visible,
            ["undoDepth"] = core.UndoDepth,
            ["events"] = new JsonArray(core.TakeEvents().Select(e =>
            {
                var o = new JsonObject { ["type"] = e.Type };
                foreach (var (k, v) in e.Detail) o[k] = EventValue(v);
                return (JsonNode)o;
            }).ToArray()),
        };
    }

    private static JsonObject Probe(ViewportCore core, JsonElement p)
    {
        var view = core.View;
        var rect = core.Rect;
        double x = p.GetProperty("x").GetDouble(), y = p.GetProperty("y").GetDouble();
        var ndcX = (x - rect.Left) / rect.Width * 2 - 1;
        var ndcY = -((y - rect.Top) / rect.Height) * 2 + 1;
        var ray = view.RayFromNdc(ndcX, ndcY);
        var hit = Picking.Pick(ray, core.Objects);
        var (sx, sy) = view.ToScreen(Vec(p.GetProperty("world")), rect.Width, rect.Height);
        return new JsonObject
        {
            ["ndc"] = new JsonArray(ndcX, ndcY), ["origin"] = Arr(ray.Origin), ["dir"] = Arr(ray.Direction),
            ["onPlane"] = Arr(view.UnprojectOnPlane(ndcX, ndcY, Vec(p.GetProperty("pivot")))),
            ["pick"] = hit?.Name, ["screen"] = new JsonArray(sx, sy), ["wpp"] = view.WorldPerPixel(Vec(p.GetProperty("world"))),
        };
    }

    [Fact]
    public void Viewport_operations_match_viewport_js_for_every_scenario_and_step()
    {
        var scenarios = Cases.GetProperty("scenarios");
        var js = Reference.GetProperty("scenarios");
        Assert.True(scenarios.GetArrayLength() >= 80);
        var totalSteps = 0;
        for (var i = 0; i < scenarios.GetArrayLength(); i++)
        {
            var c = scenarios[i];
            var name = c.GetProperty("name").GetString();
            var core = Build(c);
            var expectedSteps = js[i].GetProperty("steps");
            AssertNoDiff($"{name}: 最初の状態", expectedSteps[0], Snapshot(core));
            var steps = c.GetProperty("steps");
            for (var s = 0; s < steps.GetArrayLength(); s++)
            {
                Apply(core, steps[s]);
                AssertNoDiff($"{name}: 手順 {s + 1}/{steps.GetArrayLength()} {steps[s].GetRawText()}", expectedSteps[s + 1], Snapshot(core));
                totalSteps++;
            }
            var probes = c.GetProperty("probes");
            for (var k = 0; k < probes.GetArrayLength(); k++)
                AssertNoDiff($"{name}: 確認 {k}(レイ・平面・ピック・投影)", js[i].GetProperty("probes")[k], Probe(core, probes[k]));
        }
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
