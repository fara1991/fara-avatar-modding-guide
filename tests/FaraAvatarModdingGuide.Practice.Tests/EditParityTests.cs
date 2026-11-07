using System.Text.Json;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// S1: 編集モードを含む操作の、現行の JavaScript(viewport.js・editmesh.js・practice.js)との突き合わせ。
/// 入力は tests/js/practice-edit-cases.json(practice-edit-cases.generate.mjs が、JS を動かしながら作った固定の操作列)。JS は Node で本物のコンストラクタ・ハンドラーに通し、
/// C# は ViewportCore に通して、手順ごとに(オブジェクト・メッシュの頂点/面/選択の順番まで・視点・カメラ・HUD の文字・ボタン・N パネル・メニュー・ループカットの状態・
/// 取り消しの深さ・お題・出たイベント・preventDefault)を比べる。
/// </summary>
public class EditParityTests
{
    private static JsonElement Cases => JsReference.Edit.Cases;
    private static JsonElement Reference => JsReference.Edit.Output;

    public static IEnumerable<object[]> ScenarioIndexes()
    {
        var n = Cases.GetProperty("scenarios").GetArrayLength();
        for (var i = 0; i < n; i++) yield return [i];
    }

    [Theory]
    [MemberData(nameof(ScenarioIndexes))]
    public void Edit_operations_match_viewport_js(int index)
    {
        var steps = ScenarioRunner.Run(Cases.GetProperty("scenarios")[index], Reference.GetProperty("scenarios")[index]);
        Assert.True(steps > 0);
    }

    [Fact]
    public void The_edit_scenarios_are_big_enough()
    {
        var scenarios = Cases.GetProperty("scenarios");
        var total = scenarios.EnumerateArray().Sum(s => s.GetProperty("steps").GetArrayLength());
        Assert.True(scenarios.GetArrayLength() >= 100, "シナリオの数: " + scenarios.GetArrayLength());
        Assert.True(total >= 15000, "手順の数: " + total);
        Assert.True(scenarios.EnumerateArray().Count(s => s.GetProperty("steps").GetArrayLength() >= 800) >= 2, "800 手順以上の長いシナリオ");
        Assert.True(scenarios.EnumerateArray().Count(s => s.GetProperty("steps").GetArrayLength() >= 300) >= 8, "300 手順以上のシナリオ");
    }

    [Fact]
    public void The_edit_scenarios_cover_every_edit_operation_and_failure()
    {
        // 突き合わせの入力が、編集モードの操作を一通り(成功も失敗も)通していること。一致しても、何も試していないのでは意味がない。
        // JS の出力(正解側)のイベントで数える: 入力が JS で実際にその操作を起こしたことを確かめる
        var types = new HashSet<string>();
        var reasons = new HashSet<string>();
        var statuses = new HashSet<string>();
        foreach (var scenario in Reference.GetProperty("scenarios").EnumerateArray())
            foreach (var step in scenario.GetProperty("steps").EnumerateArray())
            {
                statuses.Add(step.GetProperty("status").GetString()!);
                foreach (var e in step.GetProperty("events").EnumerateArray())
                {
                    var type = e.GetProperty("type").GetString();
                    string Detail(string name) => e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText() : "";
                    types.Add(type + ":" + (e.TryGetProperty("kind", out _) ? Detail("kind") : e.TryGetProperty("source", out _) ? Detail("source") : e.TryGetProperty("action", out _) ? Detail("action") : e.TryGetProperty("what", out _) ? Detail("what") : e.TryGetProperty("type", out _) ? "" : ""));
                    // 現行の不具合: extrude イベントは detail の type で種類が上書きされ、"region" / "edge" / "vert" として出る
                    if (type is "region" or "edge" or "vert") types.Add("extrude-type:" + type);
                    if (type == "merge") types.Add("merge-how:" + Detail("how"));
                    if (type == "fill") types.Add("fill-made:" + Detail("made"));
                    if (type == "add") types.Add("add-kind:" + Detail("kind"));
                    if (type == "menu") types.Add("menu:" + Detail("menu"));
                    if (type == "opfail") reasons.Add(Detail("kind") + "/" + Detail("reason"));
                    if (type == "confirm" && Detail("centered") == "true") types.Add("confirm:centered");
                    if (type == "confirm" && Detail("extrude") == "true") types.Add("confirm:extrude");
                }
            }
        foreach (var expected in new[]
        {
            "mode:", "selectmode:", "xray:", "stats:", "mirror:", "npanel:", "edit:", "undo:", "redo:", "duplicate:", "delete:", "separate:",
            "select:click", "select:shift-click", "select:box", "select:all", "select:none", "select:invert", "select:loop", "select:ring", "select:linked", "select:grow", "select:shrink",
            "opstart:G", "opstart:R", "opstart:S", "opstart:I", "opstart:B", "opstart:slide", "opstart:loopcut",
            "confirm:G", "confirm:R", "confirm:S", "confirm:I", "confirm:B", "confirm:slide", "confirm:centered", "confirm:extrude",
            "cancel:G", "cancel:R", "cancel:S", "cancel:I", "cancel:B", "cancel:loopcut",
            "extrude-type:region", "extrude-type:edge", "extrude-type:vert", "inset:", "bevel:", "loopcut:", "cuts:",
            "merge-how:center", "merge-how:cursor", "merge-how:distance", "fill-made:face", "fill-made:edge",
            "delete:vert", "delete:edge", "delete:face", "delete:dissolve",
            "add-kind:plane", "add-kind:cube", "add-kind:sphere", "add-kind:cylinder", "add-kind:cone", "add-kind:torus",
            "menu:add", "menu:delete", "menu:merge", "menu:separate", "view:orbit", "view:pan", "view:zoom", "view:axis", "view:ortho", "view:frame", "view:home",
        })
            Assert.True(types.Contains(expected), "JS の出力にこのイベントが無い: " + expected);
        // 失敗(opfail)の理由も一通り
        // (「押し出しは編集モードの操作です」などの objectmode の失敗は、キーの割り当て上、オブジェクトモードでは起きない)
        foreach (var expected in new[] { "E/noselection", "I/noselection", "G/noselection", "R/noselection", "S/noselection", "Tab/noselection", "G/ほかの人が操作中です" })
            Assert.True(reasons.Contains(expected), "JS の出力にこの失敗の理由が無い: " + expected);
        Assert.Contains("辺が選ばれていません", reasons.Concat(statuses));
        Assert.Contains("面が 2 つ接している辺だけベベルできます", reasons.Concat(statuses));
        Assert.Contains("このメッシュの形ではベベルできません（開いた部分があります）", reasons.Concat(statuses));
        Assert.Contains("ここにはループカットを入れられません（四角形の面がありません）", statuses);
        Assert.Contains("これ以上元に戻せません", statuses);
        Assert.Contains("面を張れません（頂点を 2 つ以上選んでください）", statuses);
    }

    [Fact]
    public void The_practice_playthrough_reaches_all_18_tasks_in_javascript()
    {
        // 突き合わせで一致していても、お題が最後まで進んでいないなら意味がないので、JS 側(正解)で 18 件目まで進んだことを確かめる
        var scenarios = Reference.GetProperty("scenarios");
        var cases = Cases.GetProperty("scenarios");
        var found = false;
        for (var i = 0; i < cases.GetArrayLength(); i++)
        {
            if (!(cases[i].TryGetProperty("practice", out var p) && p.GetBoolean())) continue;
            found = true;
            var steps = scenarios[i].GetProperty("steps");
            var last = steps[steps.GetArrayLength() - 1].GetProperty("tasks");
            Assert.Equal(18, last.GetProperty("index").GetInt32());
            Assert.All(last.GetProperty("done").EnumerateArray(), d => Assert.True(d.GetBoolean()));
        }
        Assert.True(found);
    }
}
