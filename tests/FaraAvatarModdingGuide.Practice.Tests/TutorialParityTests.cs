using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// S4: 記事内チュートリアル(C# の TutorialScenarios / TutorialSession)と、現行の scenarios.js(Node で本物のまま動かす)との突き合わせ。
///   ① 手順の定義(題・視点・最初のシーン・手順ごとの group / text / keys / mouse / highlight / allowOp と、hint・fail・skip の有無)
///   ② 再生(replayTo = resetScene + skip を順に)後のビューの状態(S1 の突き合わせと同じ項目: オブジェクト・メッシュ・選択・視点・HUD の文字・ボタン・N パネル・取り消しの深さ…)を、手順ごとに
///   ③ 案内(矢印の文字と位置・点線の軸と位置・ボタンの強調)
///   ④ 手順の直前の状態で、イベントの一覧(tutorial-reference.mjs の buildEvents。選択・視点・操作・数値・確定・失敗などのバリエーション)の 1 つずつを done / fail / hint に渡した答え
/// </summary>
public class TutorialParityTests
{
    private static JsonElement Reference => TutorialReference.Output;

    public static IEnumerable<object[]> Names()
    {
        foreach (var p in Reference.GetProperty("scenarios").EnumerateObject()) yield return [p.Name];
    }

    [Fact]
    public void Every_blender_scenario_in_scenarios_js_is_ported()
    {
        var js = Reference.GetProperty("scenarios").EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        Assert.Equal(js, TutorialScenarios.All.Keys.OrderBy(x => x).ToList());
        Assert.Equal(9, js.Count);
    }

    [Fact]
    public void Scenario_list_in_practice_cs_js_matches_the_ported_scenarios()
    {
        // practice-cs.js の CS_SCENARIOS(どのチュートリアルを C# 版で動かすか)が、移したシナリオと同じであること(違うと、移したのに動かない・無いものを動かそうとする)
        var source = File.ReadAllText(Path.Combine(JsReference.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "js", "practice-cs.js"));
        var m = System.Text.RegularExpressions.Regex.Match(source, @"export const CS_SCENARIOS = \[(?<list>[^\]]*)\]");
        Assert.True(m.Success);
        var listed = System.Text.RegularExpressions.Regex.Matches(m.Groups["list"].Value, "\"([^\"]+)\"").Select(x => x.Groups[1].Value).OrderBy(x => x).ToList();
        Assert.Equal(TutorialScenarios.All.Keys.OrderBy(x => x).ToList(), listed);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Definitions_match_scenarios_js(string name)
    {
        var js = Reference.GetProperty("scenarios").GetProperty(name).GetProperty("definition");
        var cs = TutorialScenarios.All[name];
        Assert.Equal(js.GetProperty("title").GetString(), cs.Title);
        Assert.Equal(js.GetProperty("numpad").GetBoolean(), cs.Numpad);
        var view = js.GetProperty("view");
        Assert.Equal(view.ValueKind == JsonValueKind.Object && view.TryGetProperty("dist", out var d) ? d.GetDouble() : null, cs.ViewDist);
        Assert.Equal(view.ValueKind == JsonValueKind.Object && view.TryGetProperty("target", out var t) ? t.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null, cs.ViewTarget);
        var scene = js.GetProperty("scene");
        if (scene.ValueKind == JsonValueKind.Null) Assert.Null(cs.Scene);
        else
        {
            Assert.Equal(scene.GetArrayLength(), cs.Scene!.Count);
            for (var i = 0; i < cs.Scene.Count; i++)
            {
                var s = scene[i];
                Assert.Equal(s.GetProperty("kind").GetString(), cs.Scene[i].Kind);
                Assert.Equal(s.TryGetProperty("name", out var n) ? n.GetString() : null, cs.Scene[i].Name);
                Assert.Equal(s.TryGetProperty("p", out var p) ? p.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null, cs.Scene[i].P);
                Assert.Equal(s.TryGetProperty("s", out var sc) ? sc.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null, cs.Scene[i].S);
                Assert.Null(cs.Scene[i].R);
                Assert.False(s.TryGetProperty("r", out _));
            }
        }
        var steps = js.GetProperty("steps");
        Assert.Equal(steps.GetArrayLength(), cs.Steps.Count);
        for (var i = 0; i < cs.Steps.Count; i++)
        {
            var j = steps[i];
            var c = cs.Steps[i];
            var at = $"{name} 手順 {i + 1}";
            Assert.True(j.GetProperty("group").ValueKind == JsonValueKind.Null ? c.Group is null : j.GetProperty("group").GetString() == c.Group, at + ": group");
            Assert.Equal(j.GetProperty("text").GetString(), c.Text);
            Assert.Equal(j.GetProperty("keys").EnumerateArray().Select(x => x.GetString()!).ToList(), c.Keys.ToList());
            Assert.True(j.GetProperty("mouse").ValueKind == JsonValueKind.Null ? c.Mouse is null : j.GetProperty("mouse").GetString() == c.Mouse, at + ": mouse");
            Assert.Equal(j.GetProperty("allowOp").GetBoolean(), c.AllowOp);
            Assert.Equal(j.GetProperty("hasHint").GetBoolean(), c.Hint is not null);
            Assert.Equal(j.GetProperty("hasFail").GetBoolean(), c.Fail is not null);
            Assert.Equal(j.GetProperty("hasSkip").GetBoolean(), c.Skip is not null);
            var h = j.GetProperty("highlight");
            if (h.ValueKind == JsonValueKind.Null) Assert.True(c.Highlight is null, at + ": highlight なし");
            else
            {
                Assert.NotNull(c.Highlight);
                string? S(string k) => h.TryGetProperty(k, out var v) ? v.GetString() : null;
                Assert.Equal(S("object"), c.Highlight!.Object);
                Assert.Equal(S("label"), c.Highlight.Label);
                Assert.Equal(S("axis"), c.Highlight.Axis);
                Assert.Equal(S("hud"), c.Highlight.Hud);
                Assert.Equal(S("tool"), c.Highlight.Tool);
                Assert.Equal(h.TryGetProperty("at", out var a) ? a.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null, c.Highlight.At);
            }
        }
    }

    private static JsonArray Doubles(double[] a) => new(a.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());

    private static Session NewSession(string name, TutorialScenario scenario)
    {
        // null の項目は入れない(ViewportDriver.Build は、あれば読む)
        JsonNode Obj(SceneSpec s)
        {
            var o = new JsonObject { ["kind"] = s.Kind };
            if (s.Name is not null) o["name"] = s.Name;
            if (s.P is not null) o["p"] = Doubles(s.P);
            if (s.S is not null) o["s"] = Doubles(s.S);
            return o;
        }
        var options = new JsonObject { ["wheelNeedsFocus"] = true };
        if (scenario.ViewDist is not null || scenario.ViewTarget is not null)
        {
            var view = new JsonObject();
            if (scenario.ViewDist is { } dist) view["dist"] = dist;
            if (scenario.ViewTarget is { } target) view["target"] = Doubles(target);
            options["view"] = view;
        }
        var c = new JsonObject
        {
            ["name"] = name, ["seed"] = 1, ["emulate"] = false,
            ["root"] = new JsonObject { ["left"] = 0, ["top"] = 0, ["width"] = 960, ["height"] = 540 },
            ["objects"] = scenario.Scene is null ? new JsonArray(new JsonObject { ["kind"] = "cube" }) : new JsonArray(scenario.Scene.Select(Obj).ToArray()),
            ["options"] = options,
            ["steps"] = new JsonArray(),
        };
        return ViewportDriver.Build(JsonDocument.Parse(c.ToJsonString()).RootElement);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Replays_leave_the_same_viewport_state_as_scenarios_js(string name)
    {
        var scenario = TutorialScenarios.All[name];
        var replays = Reference.GetProperty("scenarios").GetProperty(name).GetProperty("replays");
        Assert.Equal(scenario.Steps.Count + 1, replays.GetArrayLength());
        for (var i = 0; i <= scenario.Steps.Count; i++)
        {
            var session = NewSession(name, scenario);
            var tutorial = new TutorialSession(scenario, session.Core);
            tutorial.ReplayTo(i);
            var diff = JsonCompare.Diff(replays[i], ViewportDriver.Snapshot(session));
            Assert.True(diff.Count == 0, $"{name}: 手順 {i} の直前まで再生したあとの状態" + Environment.NewLine + string.Join(Environment.NewLine, diff));
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Highlights_match_scenarios_js(string name)
    {
        var scenario = TutorialScenarios.All[name];
        var highlights = Reference.GetProperty("scenarios").GetProperty(name).GetProperty("highlights");
        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            var session = NewSession(name, scenario);
            new TutorialSession(scenario, session.Core).ReplayTo(i);
            var core = session.Core;
            core.SetHighlight(scenario.Steps[i].Highlight);
            var js = highlights[i];
            var at = $"{name} 手順 {i + 1}";
            var screen = core.MarkerScreen;
            Assert.True(js.GetProperty("markerHidden").GetBoolean() == (screen is null), at + $": 矢印の表示 JS 隠れ={js.GetProperty("markerHidden").GetBoolean()}");
            Assert.Equal(js.GetProperty("markerLabel").GetString(), core.MarkerLabel);
            if (screen is { } s)
            {
                var jsScreen = js.GetProperty("markerScreen");
                Assert.True(Math.Abs(jsScreen[0].GetDouble() - s.X) < 1e-6 && Math.Abs(jsScreen[1].GetDouble() - s.Y) < 1e-6, at + $": 矢印の位置 JS=({jsScreen[0]},{jsScreen[1]}) C#=({s.X},{s.Y})");
            }
            var guide = core.GuideLine;
            Assert.True(js.GetProperty("guideVisible").GetBoolean() == (guide is not null), at + ": 点線の表示");
            if (guide is { } g)
            {
                var world = js.GetProperty("guideWorld");
                Assert.True(Math.Abs(world[12].GetDouble() - g.Position.X) < 1e-9 && Math.Abs(world[13].GetDouble() - g.Position.Y) < 1e-9 && Math.Abs(world[14].GetDouble() - g.Position.Z) < 1e-9, at + ": 点線の位置");
                Assert.Equal(js.GetProperty("guideColor").GetInt32(), TransformOp.AxisColor(g.Axis));
            }
            Assert.Equal(js.GetProperty("hudViewHighlight").GetBoolean(), scenario.Steps[i].Highlight?.Hud == "view");
            var tools = js.GetProperty("toolHighlight").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.Equal(tools, scenario.Steps[i].Highlight?.Tool is { } tool ? new List<string> { tool } : []);
        }
    }

    private static ViewEvent ToEvent(JsonElement e)
    {
        var detail = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject())
        {
            if (p.Name == "type") continue;
            detail[p.Name] = Convert(p.Value);
        }
        return new ViewEvent(e.GetProperty("type").GetString()!, detail);
    }

    private static object? Convert(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Array => v.EnumerateArray().Select(x => x.GetString()!).ToArray(),
        JsonValueKind.Object => v.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value)),
        _ => throw new InvalidOperationException(),
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void Steps_answer_every_event_like_scenarios_js(string name)
    {
        var scenario = TutorialScenarios.All[name];
        var answers = Reference.GetProperty("scenarios").GetProperty(name).GetProperty("answers");
        var events = Reference.GetProperty("events").EnumerateArray().Select(ToEvent).ToList();
        var errors = new List<string>();
        var doneCount = 0;
        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            var session = NewSession(name, scenario);
            new TutorialSession(scenario, session.Core).ReplayTo(i);
            var step = scenario.Steps[i];
            var ctx = new StepContext();
            for (var k = 0; k < events.Count; k++)
            {
                var js = answers[i][k];
                var done = step.Done(events[k], session.Core, ctx);
                var fail = step.Fail?.Invoke(events[k], session.Core, ctx);
                var hint = step.Hint?.Invoke(events[k], session.Core, ctx);
                if (string.IsNullOrEmpty(fail)) fail = null;
                if (string.IsNullOrEmpty(hint)) hint = null;
                var jsDone = js[0].ValueKind == JsonValueKind.True;
                var jsFail = js[1].ValueKind == JsonValueKind.Null ? null : js[1].GetString();
                var jsHint = js[2].ValueKind == JsonValueKind.Null ? null : js[2].GetString();
                if (done == jsDone && fail == jsFail && hint == jsHint)
                {
                    if (done) doneCount++;
                    continue;
                }
                if (errors.Count < 8) errors.Add($"{name} 手順 {i + 1} イベント {k}({JsonSerializer.Serialize(Reference.GetProperty("events")[k])}): JS done={jsDone} fail={jsFail} hint={jsHint} / C# done={done} fail={fail} hint={hint}");
            }
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        Assert.True(doneCount >= scenario.Steps.Count / 2, $"{name}: 達成になるイベントが少なすぎる({doneCount})。イベントの一覧が手順を通せていない");
    }

    [Fact]
    public void The_event_list_triggers_every_kind_of_answer()
    {
        // 一致しても、全部「何も起きない」答えでは意味がない。JS の答えで、達成・失敗・ヒントがそれぞれ十分にあること
        int done = 0, fail = 0, hint = 0;
        foreach (var scenario in Reference.GetProperty("scenarios").EnumerateObject())
            foreach (var step in scenario.Value.GetProperty("answers").EnumerateArray())
                foreach (var a in step.EnumerateArray())
                {
                    if (a[0].ValueKind == JsonValueKind.True) done++;
                    if (a[1].ValueKind != JsonValueKind.Null) fail++;
                    if (a[2].ValueKind != JsonValueKind.Null) hint++;
                }
        Assert.True(done >= 150, "達成: " + done);
        Assert.True(fail >= 150, "失敗: " + fail);
        Assert.True(hint >= 500, "ヒント: " + hint);
    }
}
