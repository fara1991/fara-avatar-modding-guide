using System.Text.Json;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Practice.Tests;
using Xunit;

namespace FaraAvatarModdingGuide.Render.Tests;

/// <summary>
/// S2: 描画の命令(Render)と、現行の viewport.js(three.js)が作るシーンの突き合わせ。
/// 入力は S0・S1 と同じ操作列(tests/js/practice-cases.json の 82 シナリオ・practice-edit-cases.json の 127 シナリオ)。JS は本物の Viewport に通して、
/// 手順 0・40 手順ごと・最後で three.js のシーンのジオメトリ(メッシュの位置と法線・輪郭・編集モードの点/線/面・ループカットの線)・色・行列・カメラの行列を取り出す。
/// C# は同じ操作を ViewportCore に通して SceneRenderer が作った描画データと比べる。数値は float の丸め(1e-6)まで。
/// 陰影(シェーダー)は GPU で計算するので、ここでは見ない(Playwright の画像比較で確かめる)。
/// </summary>
public class RenderParityTests
{
    private const double FloatTolerance = 1e-6;
    private const double MatrixTolerance = 1e-9;

    public static IEnumerable<object[]> BasicIndexes() => Indexes(JsReference.Basic.Cases);

    public static IEnumerable<object[]> EditIndexes() => Indexes(JsReference.Edit.Cases);

    private static IEnumerable<object[]> Indexes(JsonElement cases)
    {
        var n = cases.GetProperty("scenarios").GetArrayLength();
        for (var i = 0; i < n; i++) yield return [i];
    }

    [Theory]
    [MemberData(nameof(BasicIndexes))]
    public void Basic_scenes_match_threejs(int index) => Check(JsReference.Basic.Cases, RenderReference.Basic.Output, index);

    [Theory]
    [MemberData(nameof(EditIndexes))]
    public void Edit_scenes_match_threejs(int index) => Check(JsReference.Edit.Cases, RenderReference.Edit.Output, index);

    [Fact]
    public void The_scenarios_cover_every_part_of_the_scene()
    {
        // 突き合わせの入力が、シーンの部品を一通り(編集モードの点/線/面・透過・ミラー・軸線・ループカットの線・平行投影・複数オブジェクト)通していること。
        // 何も描いていない状態どうしが一致しても意味がないので、JS 側の出力で数える
        var counts = new Dictionary<string, int>();
        void Count(string key, bool on) { if (on) counts[key] = counts.GetValueOrDefault(key) + 1; }
        foreach (var run in new[] { RenderReference.Basic, RenderReference.Edit })
            foreach (var scenario in run.Output.GetProperty("scenarios").EnumerateArray())
                foreach (var shot in scenario.GetProperty("shots").EnumerateArray())
                {
                    Count("shots", true);
                    Count("ortho", shot.GetProperty("camera").GetProperty("ortho").GetBoolean());
                    Count("axisLine", shot.GetProperty("axisLine").GetProperty("visible").GetBoolean());
                    Count("loopcut", shot.GetProperty("loopPreview").ValueKind != JsonValueKind.Null);
                    var objects = shot.GetProperty("objects");
                    Count("multi", objects.GetArrayLength() > 1);
                    foreach (var o in objects.EnumerateArray())
                    {
                        Count("outline", o.GetProperty("outlineVisible").GetBoolean());
                        Count("xray", o.GetProperty("transparent").GetBoolean());
                        Count("mirror", o.GetProperty("mirrorVisible").GetBoolean() && Floats(o.GetProperty("meshPos")).Length > 0);
                        var ov = o.GetProperty("overlay");
                        if (ov.ValueKind == JsonValueKind.Null) continue;
                        Count("overlay", true);
                        Count("overlay-faces", Floats(ov.GetProperty("facesPos")).Length > 0);
                        Count("overlay-points-hidden", !ov.GetProperty("pointsVisible").GetBoolean());
                        Count("overlay-xray", !ov.GetProperty("depthTest").GetBoolean());
                    }
                }
        foreach (var (key, min) in new Dictionary<string, int>
        {
            ["shots"] = 500, ["ortho"] = 100, ["axisLine"] = 10, ["loopcut"] = 10, ["multi"] = 100, ["outline"] = 200, ["xray"] = 100, ["mirror"] = 50,
            ["overlay"] = 200, ["overlay-faces"] = 80, ["overlay-points-hidden"] = 80, ["overlay-xray"] = 40,
        })
            Assert.True(counts.GetValueOrDefault(key) >= min, $"{key}: {counts.GetValueOrDefault(key)}(最低 {min})");
    }

    [Fact]
    public void The_static_scene_matches_threejs()
    {
        var s = RenderReference.Edit.Output.GetProperty("statics");
        var errors = new List<string>();
        var core = new ViewportCore(new ViewRect(0, 0, 800, 450));
        var frame = new SceneRenderer(core).Build();
        var byLabel = frame.Items.ToDictionary(i => i.Label);

        // 方眼: 位置(XZ 平面)・色(three.js は線形で持つ)・向き(X 軸まわりに 90°)
        var grid = byLabel["grid"];
        CompareFloats(errors, "grid.pos", Floats(s.GetProperty("grid").GetProperty("pos")), Every(grid.Buffer.Data, 6, 0, 3));
        CompareFloats(errors, "grid.col", Floats(s.GetProperty("grid").GetProperty("col")), Every(grid.Buffer.Data, 6, 3, 3).Select(c => (float)Rgb.ToLinear(c)).ToArray());
        CompareMatrix(errors, "grid.world", s.GetProperty("grid").GetProperty("world"), grid.World);
        // 軸(赤・緑): 1 本ずつ、点の列と向き(0.002 浮かせる)と色
        var axes = s.GetProperty("axes");
        Assert.Equal(2, axes.GetArrayLength());
        for (var k = 0; k < 2; k++)
        {
            var item = byLabel[k == 0 ? "axis-x" : "axis-y"];
            CompareFloats(errors, "axis" + k + ".pos", Floats(axes[k].GetProperty("pos")), item.Buffer.Data);
            CompareMatrix(errors, "axis" + k + ".world", axes[k].GetProperty("world"), item.World);
            Assert.Equal(axes[k].GetProperty("color").GetInt32(), HexOf(item));
        }
        // 3D カーソルの輪
        CompareFloats(errors, "ring", Floats(s.GetProperty("ring")), byLabel["cursor"].Buffer.Data);
        // 軸固定の線の点の列
        CompareFloats(errors, "axisLine.pos", Floats(s.GetProperty("axisLinePos")), StaticGeometry.LongLine(40, Vec3.UnitX));
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static void Check(JsonElement cases, JsonElement reference, int index)
    {
        var c = cases.GetProperty("scenarios")[index];
        var name = c.GetProperty("name").GetString();
        var shots = reference.GetProperty("scenarios")[index].GetProperty("shots");
        var session = ViewportDriver.Build(c);
        var renderer = new SceneRenderer(session.Core); // 編集モードの通知(OverlayRebuilt)を受けるため、操作の前に作る
        var steps = c.GetProperty("steps");
        var shotIndex = 0;
        var errors = new List<string>();
        void Compare(int step)
        {
            while (shotIndex < shots.GetArrayLength() && shots[shotIndex].GetProperty("step").GetInt32() < step) shotIndex++;
            if (shotIndex >= shots.GetArrayLength() || shots[shotIndex].GetProperty("step").GetInt32() != step) return;
            var local = new List<string>();
            CompareShot(local, session, renderer, shots[shotIndex]);
            if (local.Count > 0) errors.Add($"{name}: 手順 {step}/{steps.GetArrayLength()}:" + Environment.NewLine + string.Join(Environment.NewLine, local.Take(8)));
        }
        Compare(0);
        for (var s = 0; s < steps.GetArrayLength() && errors.Count == 0; s++)
        {
            ViewportDriver.Apply(session, steps[s]);
            Compare(s + 1);
        }
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static void CompareShot(List<string> errors, Session session, SceneRenderer renderer, JsonElement shot)
    {
        var core = session.Core;
        var frame = renderer.Build();
        var byLabel = frame.Items.GroupBy(i => i.Label).ToDictionary(g => g.Key, g => g.First());

        var (view, projection, _) = core.View.Matrices();
        CompareMatrix(errors, "camera.view", shot.GetProperty("camera").GetProperty("view"), view);
        CompareMatrix(errors, "camera.proj", shot.GetProperty("camera").GetProperty("proj"), projection);

        var objects = shot.GetProperty("objects");
        if (objects.GetArrayLength() != core.Objects.Count) { errors.Add($"オブジェクトの数: JS={objects.GetArrayLength()} C#={core.Objects.Count}"); return; }
        for (var i = 0; i < core.Objects.Count; i++)
        {
            var o = core.Objects[i];
            var j = objects[i];
            var p = $"objects[{i}] {o.Name}";
            if (j.GetProperty("name").GetString() != o.Name) errors.Add($"{p}: 名前 JS={j.GetProperty("name").GetString()}");
            var b = renderer.BuffersOf(o);
            CompareMatrix(errors, p + ".world", j.GetProperty("world"), o.MatrixWorld);
            CompareFloats(errors, p + ".mesh.pos", Floats(j.GetProperty("meshPos")), Every(b.Mesh.Data, 6, 0, 3));
            // 面積がほとんど 0 の面の法線は、丸めの誤差で向きが決まる(S1 の突き合わせと同じく、1e-4 未満は比べない)
            var jsNormals = Floats(j.GetProperty("meshNor"));
            var csNormals = Every(b.Mesh.Data, 6, 3, 3);
            for (var t = 0; t < o.TriFace.Length && (t + 1) * 9 <= Math.Min(jsNormals.Length, csNormals.Length); t++)
            {
                if (EditMesh.FaceArea(o.TriFace[t]) >= 1e-4) continue;
                Array.Clear(jsNormals, t * 9, 9);
                Array.Clear(csNormals, t * 9, 9);
            }
            CompareFloats(errors, p + ".mesh.nor", jsNormals, csNormals);
            CompareFloats(errors, p + ".outline.pos", Floats(j.GetProperty("outlinePos")), b.Outline.Data);

            // 輪郭の表示と色
            var outlineItem = byLabel.GetValueOrDefault(o.Name + ":outline");
            var jsOutline = j.GetProperty("outlineVisible").GetBoolean() && Floats(j.GetProperty("outlinePos")).Length > 0;
            if (jsOutline != (outlineItem is not null)) errors.Add($"{p}.outline: 表示 JS={jsOutline} C#={outlineItem is not null}");
            else if (outlineItem is not null && HexOf(outlineItem) != j.GetProperty("outlineColor").GetInt32()) errors.Add($"{p}.outline: 色 JS={j.GetProperty("outlineColor").GetInt32():x6} C#={HexOf(outlineItem):x6}");

            // ミラー
            var mirrorItem = byLabel.GetValueOrDefault(o.Name + ":mirror");
            // 三角形の無いメッシュ(面を全部消した)は、ミラーが有効でも描くものが無い
            var jsMirror = j.GetProperty("mirrorVisible").GetBoolean() && Floats(j.GetProperty("meshPos")).Length > 0;
            if (jsMirror != (mirrorItem is not null)) errors.Add($"{p}.mirror: JS={jsMirror} C#={mirrorItem is not null}");
            else if (mirrorItem is not null) CompareMatrix(errors, p + ".mirror.world", j.GetProperty("mirrorWorld"), mirrorItem.World);

            // 材質(透過表示)
            var meshItem = byLabel.GetValueOrDefault(o.Name);
            if (meshItem is not null)
            {
                if (j.GetProperty("transparent").GetBoolean() != meshItem.Transparent) errors.Add($"{p}.transparent: JS={j.GetProperty("transparent").GetBoolean()} C#={meshItem.Transparent}");
                if (Math.Abs(j.GetProperty("opacity").GetDouble() - meshItem.Opacity) > 1e-6) errors.Add($"{p}.opacity: JS={j.GetProperty("opacity").GetDouble()} C#={meshItem.Opacity}");
                if (j.GetProperty("depthWrite").GetBoolean() != meshItem.Flags.HasFlag(DrawFlags.DepthWrite)) errors.Add($"{p}.depthWrite");
            }
            else if (b.Mesh.VertexCount > 0) errors.Add($"{p}: メッシュの描画が無い");

            // 編集モードの点・線・面
            var jo = j.GetProperty("overlay");
            if (jo.ValueKind == JsonValueKind.Null)
            {
                if (b.Overlay is not null) errors.Add($"{p}.overlay: JS=なし C#=あり");
            }
            else if (b.Overlay is null) errors.Add($"{p}.overlay: JS=あり C#=なし");
            else
            {
                var ov = b.Overlay;
                CompareFloats(errors, p + ".overlay.lines.pos", Floats(jo.GetProperty("linesPos")), Every(ov.Lines.Data, 6, 0, 3));
                CompareFloats(errors, p + ".overlay.lines.col", Floats(jo.GetProperty("linesCol")), Every(ov.Lines.Data, 6, 3, 3).Select(c => (float)Rgb.ToLinear(c)).ToArray());
                CompareFloats(errors, p + ".overlay.points.pos", Floats(jo.GetProperty("pointsPos")), Every(ov.Points.Data, 6, 0, 3));
                CompareFloats(errors, p + ".overlay.points.col", Floats(jo.GetProperty("pointsCol")), Every(ov.Points.Data, 6, 3, 3).Select(c => (float)Rgb.ToLinear(c)).ToArray());
                CompareFloats(errors, p + ".overlay.faces.pos", Floats(jo.GetProperty("facesPos")), ov.Faces.Data);
                if (jo.GetProperty("pointsVisible").GetBoolean() != ov.PointsVisible) errors.Add($"{p}.overlay.pointsVisible: JS={jo.GetProperty("pointsVisible").GetBoolean()} C#={ov.PointsVisible}");
                foreach (var key in new[] { "depthTest", "pointsDepthTest", "facesDepthTest" })
                    if (jo.GetProperty(key).GetBoolean() != ov.DepthTest) errors.Add($"{p}.overlay.{key}: JS={jo.GetProperty(key).GetBoolean()} C#={ov.DepthTest}");
            }
        }

        // 軸固定の線
        var axis = shot.GetProperty("axisLine");
        var axisItem = byLabel.GetValueOrDefault("axis-line");
        if (axis.GetProperty("visible").GetBoolean() != (axisItem is not null)) errors.Add($"axisLine: JS={axis.GetProperty("visible").GetBoolean()} C#={axisItem is not null}");
        else if (axisItem is not null)
        {
            CompareMatrix(errors, "axisLine.world", axis.GetProperty("world"), axisItem.World);
            if (HexOf(axisItem) != axis.GetProperty("color").GetInt32()) errors.Add($"axisLine.color: JS={axis.GetProperty("color").GetInt32():x6} C#={HexOf(axisItem):x6}");
        }

        // ループカットの線(編集中のオブジェクトの子)
        var loop = shot.GetProperty("loopPreview");
        var loopItem = byLabel.Values.FirstOrDefault(i => i.Label.EndsWith(":loopcut", StringComparison.Ordinal));
        if ((loop.ValueKind != JsonValueKind.Null) != (loopItem is not null)) errors.Add($"loopcut: JS={loop.ValueKind != JsonValueKind.Null} C#={loopItem is not null}");
        else if (loopItem is not null)
        {
            CompareFloats(errors, "loopcut.pos", Floats(loop), loopItem.Buffer.Data);
            CompareMatrix(errors, "loopcut.world", shot.GetProperty("loopPreviewWorld"), loopItem.World);
        }
    }

    // ---------------------------------------------------------------- 比べる道具
    private static int HexOf(DrawItem item) => ((int)MathF.Round(item.R * 255) << 16) | ((int)MathF.Round(item.G * 255) << 8) | (int)MathF.Round(item.B * 255);

    private static float[] Floats(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Null) return [];
        var bytes = Convert.FromBase64String(e.GetString()!);
        var f = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
        return f;
    }

    /// <summary>1 頂点 stride 個の float の並びから、offset から count 個ずつを取り出す。</summary>
    private static float[] Every(float[] data, int stride, int offset, int count)
    {
        var r = new List<float>();
        for (var i = 0; i + stride <= data.Length; i += stride)
            for (var k = 0; k < count; k++) r.Add(data[i + offset + k]);
        return [.. r];
    }

    private static void CompareFloats(List<string> errors, string what, float[] js, float[] cs)
    {
        if (js.Length != cs.Length) { errors.Add($"{what}: float の数 JS={js.Length} C#={cs.Length}"); return; }
        for (var i = 0; i < js.Length; i++)
        {
            if (Math.Abs(js[i] - cs[i]) <= FloatTolerance * Math.Max(1, Math.Abs(js[i]))) continue;
            errors.Add($"{what}[{i}]: JS={js[i]:R} C#={cs[i]:R}");
            return;
        }
    }

    private static void CompareMatrix(List<string> errors, string what, JsonElement js, Mat4 cs)
    {
        for (var i = 0; i < 16; i++)
        {
            var a = js[i].GetDouble();
            var b = cs.E[i];
            if (Math.Abs(a - b) <= MatrixTolerance * Math.Max(1, Math.Abs(a))) continue;
            errors.Add($"{what}[{i}]: JS={a:R} C#={b:R}");
            return;
        }
    }
}
