using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// S5: Unity 風の画面(C# の UnityEditorCore)と、現行の unity.js(Node で本物のまま動かす)との突き合わせ。
///   ① 操作列(tests/js/unity-cases.json: 台本・ギズモのドラッグ・視点の操作・Scene のクリックの格子・乱数)を 1 操作ずつ通し、操作のたびに状態(ノード・Hierarchy の行・Inspector・SDK の集計・
///      ポップアップ・プレビュー・ステータス・視点・カメラ・ギズモ・3D のオブジェクトと部品のワールド行列)・イベント・preventDefault・クリックの下にあるものを比べる
///   ② 3D の部品・ギズモのジオメトリ(three.js の Sphere・Capsule・Box・Cylinder・Cone・Torus と translate・rotateX)の頂点・法線・インデックス
///   ③ アセット・コンポーネントの一覧
/// </summary>
public class UnityParityTests
{
    public static IEnumerable<object[]> CaseNames()
    {
        foreach (var c in UnityReference.Cases.EnumerateArray()) yield return [c.GetProperty("name").GetString()!];
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_matches_unity_js(string name)
    {
        var c = UnityReference.Cases.EnumerateArray().First(x => x.GetProperty("name").GetString() == name);
        var js = UnityReference.Output.GetProperty("cases").EnumerateArray().First(x => x.GetProperty("name").GetString() == name).GetProperty("steps");
        var cs = new UnityDriver(c).Run(c.GetProperty("ops"));
        var errors = new List<string>();
        var jsSteps = js.EnumerateArray().ToList();
        Assert.Equal(jsSteps.Count, cs.Count);
        for (var i = 0; i < jsSteps.Count && errors.Count < 6; i++)
        {
            var diff = JsonCompare.Diff(jsSteps[i], cs[i], 4);
            var op = jsSteps[i].GetProperty("op").GetRawText();
            foreach (var d in diff) errors.Add($"手順 {i} {op}: {d}");
        }
        Assert.True(errors.Count == 0, name + ":" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Part_and_gizmo_geometry_match_threejs()
    {
        var geo = UnityReference.Output.GetProperty("geometries");
        var errors = new List<string>();
        var core = new UnityEditorCore(new UnityOptions { Instances = ["Avatar", "Hoodie"] });
        var jsParts = geo.GetProperty("parts").EnumerateArray().ToList();
        var i = 0;
        foreach (var n in core.Nodes)
            foreach (var p in n.Parts ?? [])
            {
                Compare($"部品 {i} ({n.Name} {p.Shape})", jsParts[i], p.Geometry, errors);
                i++;
            }
        Assert.Equal(jsParts.Count, i);
        core.Select(core.FindByName("Avatar")!.Id);
        foreach (var tool in new[] { "move", "rotate", "scale" })
        {
            core.SetTool(tool);
            var js = geo.GetProperty("gizmo").GetProperty(tool).EnumerateArray().ToList();
            Assert.Equal(js.Count, core.GizmoItems.Count);
            for (var k = 0; k < js.Count; k++) Compare($"ギズモ {tool} {k}", js[k], core.GizmoItems[k].Geometry, errors);
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(12)));
    }

    private static float[] Floats(JsonElement e)
    {
        var bytes = Convert.FromBase64String(e.GetString()!);
        var f = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
        return f;
    }

    private static void Compare(string what, JsonElement js, UnityMesh mesh, List<string> errors)
    {
        var jp = Floats(js.GetProperty("positions"));
        var jn = Floats(js.GetProperty("normals"));
        var ji = js.GetProperty("indices").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        if (jp.Length != mesh.Positions.Length) { errors.Add($"{what}: 頂点の数 JS={jp.Length / 3} / C#={mesh.VertexCount}"); return; }
        if (!ji.SequenceEqual(mesh.Indices)) { errors.Add($"{what}: インデックスが違う(JS {ji.Length} 個 / C# {mesh.Indices.Length} 個)"); return; }
        // 位置・法線は double で計算して float に丸めたもの。三角関数の最後の桁の違いで、float の 1 ulp(約 6e-8)違うことがある
        for (var k = 0; k < jp.Length; k++)
        {
            if (Math.Abs(jp[k] - mesh.Positions[k]) > 2e-7f) { errors.Add($"{what}: 位置[{k}] JS={jp[k]:R} / C#={mesh.Positions[k]:R}"); return; }
            if (Math.Abs(jn[k] - mesh.Normals[k]) > 2e-6f) { errors.Add($"{what}: 法線[{k}] JS={jn[k]:R} / C#={mesh.Normals[k]:R}"); return; }
        }
    }

    [Fact]
    public void Assets_components_and_task_texts_match()
    {
        var def = UnityReference.Output.GetProperty("definitions");
        var assets = def.GetProperty("assets").EnumerateArray().ToList();
        Assert.Equal(assets.Count, UnityData.Assets.Count);
        for (var i = 0; i < assets.Count; i++)
        {
            var a = assets[i];
            var c = UnityData.Assets[i];
            Assert.Equal(a.GetProperty("key").GetString(), c.Key);
            Assert.Equal(a.GetProperty("type").GetString(), c.Type);
            Assert.Equal(a.GetProperty("label").GetString(), c.Label);
            Assert.Equal(a.GetProperty("color").ValueKind == JsonValueKind.Null ? null : a.GetProperty("color").GetInt32(), c.Color);
            Assert.Equal(a.TryGetProperty("shader", out var sh) ? sh.GetString() : null, c.Shader);
            Assert.Equal(a.TryGetProperty("note", out var no) ? no.GetString() : null, c.Note);
            Assert.Equal(a.GetProperty("hidden").GetBoolean(), c.Hidden);
        }
        var comps = def.GetProperty("components").EnumerateArray().ToList();
        Assert.Equal(comps.Count, UnityData.Components.Count);
        for (var i = 0; i < comps.Count; i++)
        {
            Assert.Equal(comps[i].GetProperty("type").GetString(), UnityData.Components[i].Type);
            Assert.Equal(comps[i].GetProperty("label").GetString(), UnityData.Components[i].Label);
            Assert.Equal(comps[i].GetProperty("path").GetString(), UnityData.Components[i].Path);
        }
    }
}
