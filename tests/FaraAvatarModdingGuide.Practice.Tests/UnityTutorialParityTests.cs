using System.Text.Json;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// S5: Unity 風の画面のチュートリアル(C# の UnityTutorialScenarios / UnityTutorialSession)と、現行の scenarios.js(Node で本物のまま動かす)との突き合わせ。
///   ① 手順の定義(題・最初に置く Prefab・手順ごとの group / text / keys / mouse / highlight と、hint・fail・skip の有無)
///   ② 再生(replayTo = resetScene + skip を順に)後のエディターの状態を、手順ごとに
///   ③ 手順の直前の状態で、イベントの一覧(unity-reference.mjs の buildEvents)の 1 つずつを done / fail / hint に渡した答え
///   ④ お題 7 件の文
/// </summary>
public class UnityTutorialParityTests
{
    private static JsonElement Definitions => UnityReference.Output.GetProperty("definitions");
    private static JsonElement Tutorials => UnityReference.Output.GetProperty("tutorials");

    public static IEnumerable<object[]> Names()
    {
        foreach (var p in Definitions.GetProperty("scenarios").EnumerateObject()) yield return [p.Name];
    }

    [Fact]
    public void Every_unity_scenario_in_scenarios_js_is_ported()
    {
        var js = Definitions.GetProperty("scenarios").EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        Assert.Equal(js, UnityTutorialScenarios.All.Select(s => s.Name).OrderBy(x => x).ToList());
        Assert.Equal(2, js.Count);
    }

    [Fact]
    public void Task_texts_match_unity_practice_js()
    {
        var js = Definitions.GetProperty("taskTexts").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(js, UnityPracticeSession.Tasks.Select(t => t.Text).ToList());
        Assert.Equal(7, js.Count);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Definitions_match_scenarios_js(string name)
    {
        var js = Definitions.GetProperty("scenarios").GetProperty(name);
        var cs = UnityTutorialScenarios.Find(name)!;
        Assert.Equal(js.GetProperty("title").GetString(), cs.Title);
        Assert.Equal(js.GetProperty("instances").EnumerateArray().Select(x => x.GetString()!).ToList(), cs.Instances.ToList());
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
            Assert.Equal(j.GetProperty("hint").GetBoolean(), c.Hint is not null);
            Assert.Equal(j.GetProperty("fail").GetBoolean(), c.Fail is not null);
            Assert.Equal(j.GetProperty("skip").GetBoolean(), c.Skip is not null);
            var h = j.GetProperty("highlight");
            if (h.ValueKind == JsonValueKind.Null) { Assert.True(c.Highlight is null, at + ": highlight なし"); continue; }
            Assert.NotNull(c.Highlight);
            string? S(string k) => h.TryGetProperty(k, out var v) ? v.GetString() : null;
            var hl = c.Highlight!;
            Assert.Equal(S("hierarchy"), hl.Hierarchy);
            Assert.Equal(S("project"), hl.Project);
            Assert.Equal(S("inspector"), hl.Inspector);
            Assert.Equal(S("menu"), hl.Menu);
            Assert.Equal(S("sdk"), hl.Sdk);
            Assert.Equal(S("tool"), hl.Tool);
            Assert.Equal(S("popup"), hl.Popup);
            Assert.Equal(S("label"), hl.Label);
            Assert.Equal(h.TryGetProperty("scene", out var sc) && sc.GetBoolean(), hl.Scene);
        }
    }

    private static UnityDriver NewDriver(UnityTutorialScenario scenario) =>
        new(JsonDocument.Parse(JsonSerializer.Serialize(new { instances = scenario.Instances, wheelNeedsFocus = true })).RootElement);

    [Theory]
    [MemberData(nameof(Names))]
    public void Replays_leave_the_same_editor_state_as_scenarios_js(string name)
    {
        var scenario = UnityTutorialScenarios.Find(name)!;
        var replays = Tutorials.GetProperty("scenarios").GetProperty(name).GetProperty("replays");
        Assert.Equal(scenario.Steps.Count + 1, replays.GetArrayLength());
        var driver = NewDriver(scenario);
        var tutorial = new UnityTutorialSession(scenario, driver.Core);
        for (var i = 0; i <= scenario.Steps.Count; i++)
        {
            tutorial.ReplayTo(i);
            var diff = JsonCompare.Diff(replays[i], driver.SnapshotLight(), 4);
            Assert.True(diff.Count == 0, $"{name}: 手順 {i} の直前まで再生したあとの状態" + Environment.NewLine + string.Join(Environment.NewLine, diff));
        }
    }

    private static ViewEvent ToEvent(JsonElement e)
    {
        var detail = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject())
            if (p.Name != "type") detail[p.Name] = Value(p.Value);
        return new ViewEvent(e.GetProperty("type").GetString()!, detail);
    }

    private static object? Value(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String => v.GetString(),
        _ => throw new InvalidOperationException(),
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void Steps_answer_every_event_like_scenarios_js(string name)
    {
        var scenario = UnityTutorialScenarios.Find(name)!;
        var answers = Tutorials.GetProperty("scenarios").GetProperty(name).GetProperty("answers");
        var events = Tutorials.GetProperty("events").EnumerateArray().Select(ToEvent).ToList();
        var errors = new List<string>();
        var doneCount = 0;
        var driver = NewDriver(scenario);
        var tutorial = new UnityTutorialSession(scenario, driver.Core);
        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            tutorial.ReplayTo(i);
            var step = scenario.Steps[i];
            var ctx = new StepContext();
            for (var k = 0; k < events.Count; k++)
            {
                var js = answers[i][k];
                var done = step.Done(events[k], driver.Core, ctx);
                var fail = step.Fail?.Invoke(events[k], driver.Core, ctx);
                var hint = step.Hint?.Invoke(events[k], driver.Core, ctx);
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
                if (errors.Count < 8) errors.Add($"{name} 手順 {i + 1} イベント {k}({JsonSerializer.Serialize(Tutorials.GetProperty("events")[k])}): JS done={jsDone} fail={jsFail} hint={jsHint} / C# done={done} fail={fail} hint={hint}");
            }
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        Assert.True(doneCount >= scenario.Steps.Count, $"{name}: 達成になるイベントが少なすぎる({doneCount})。イベントの一覧が手順を通せていない");
    }

    [Fact]
    public void The_event_list_triggers_every_kind_of_answer()
    {
        int done = 0, fail = 0, hint = 0;
        foreach (var scenario in Tutorials.GetProperty("scenarios").EnumerateObject())
            foreach (var step in scenario.Value.GetProperty("answers").EnumerateArray())
                foreach (var a in step.EnumerateArray())
                {
                    if (a[0].ValueKind == JsonValueKind.True) done++;
                    if (a[1].ValueKind != JsonValueKind.Null) fail++;
                    if (a[2].ValueKind != JsonValueKind.Null) hint++;
                }
        Assert.True(done >= 25, "達成: " + done);
        Assert.True(fail >= 3, "失敗: " + fail);
        Assert.True(hint >= 50, "ヒント: " + hint);
    }
}
