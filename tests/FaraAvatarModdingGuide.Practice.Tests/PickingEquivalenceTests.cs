using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 軽量版のレイ判定(<see cref="Picking.Nearest"/> と <see cref="Picking.PickIndex"/>)が、従来の <see cref="Picking.Raycast"/> の先頭と、
/// 同じオブジェクト・同じ三角形・同じ距離(ビットまで)を返すこと。視点は透視・平行投影(軸にそろえたもの含む)、点は画面の内外・頂点の真上(丸めの境目)も試す。
/// </summary>
public class PickingEquivalenceTests
{
    private static ViewportCore Scene(string[] kinds)
    {
        var specs = kinds.Select((k, i) => new SceneSpec(k, null, [i * 1.2 - 1.5, (i % 2) * 0.9, 0.3 * i], [10 * i, 20 * i, 5 * i], [1, 1, 1])).ToList();
        return new ViewportCore(new ViewRect(0, 0, 960, 540), new ViewportOptions { Scene = specs });
    }

    private static IEnumerable<(double Theta, double Phi, bool Ortho)> Views()
    {
        yield return (-Math.PI * 0.75, 63 * Js.Deg, false);
        yield return (0.4, 1.0, false);
        yield return (-Math.PI / 2, Math.PI / 2, true);          // 前(軸にそろった平行投影)
        yield return (0, Math.PI / 2, true);                     // 右
        yield return (-Math.PI / 2, 0.001, true);                // 上
        yield return (2.2, 1.45, false);                         // 地平線すれすれ
        yield return (1.0, 2.6, false);                          // 下から
    }

    [Fact]
    public void Nearest_with_and_without_the_index_equals_the_first_raycast_hit()
    {
        var rnd = new Random(7);
        var checkedHits = 0;
        var checkedMisses = 0;
        foreach (var kinds in new[] { new[] { "sphere" }, new[] { "cube", "sphere", "torus" }, new[] { "plane", "cone", "cylinder", "cube" } })
        {
            var core = Scene(kinds);
            // 近い距離(0.6)は、カメラが物体の中や、三角形がカメラの面をまたぐ場合(いつも候補に残す三角形)を試す
            foreach (var ((theta, phi, ortho), dist) in Views().SelectMany(v => new[] { 9.0, 2.2, 0.6 }.Select(d => (v, d))))
            {
                core.View.Theta = theta; core.View.Phi = phi; core.View.Ortho = ortho; core.View.Dist = dist;
                core.View.Aspect = 960.0 / 540;
                var prepared = Picking.Prepare(core.Objects);
                var index = new Picking.PickIndex(prepared, core.View);
                var points = new List<(double, double)>();
                for (var i = 0; i < 1200; i++) points.Add((rnd.NextDouble() * 2.4 - 1.2, rnd.NextDouble() * 2.4 - 1.2));   // 画面の外も少し
                // 頂点の真上(レイが頂点・辺を通る。当たるかが丸めで決まる)
                foreach (var o in core.Objects.Take(2))
                    foreach (var v in o.Em.Verts.Take(80)) points.Add(core.View.ProjectToNdc(o.ToWorld(v.Co)));
                foreach (var (nx, ny) in points)
                {
                    var ray = core.View.RayFromNdc(nx, ny);
                    var expected = Picking.Raycast(ray, core.Objects);
                    var plain = Picking.Nearest(ray, prepared);
                    var indexed = Picking.Nearest(ray, prepared, null, index.At(nx, ny));
                    if (expected.Count == 0)
                    {
                        Assert.Null(plain);
                        Assert.Null(indexed);
                        checkedMisses++;
                        continue;
                    }
                    var first = expected[0];
                    foreach (var got in new[] { plain, indexed })
                    {
                        Assert.NotNull(got);
                        Assert.Same(first.Object, got!.Value.Object);
                        Assert.Equal(first.FaceIndex, got.Value.FaceIndex);
                        Assert.Equal(BitConverter.DoubleToInt64Bits(first.Distance), BitConverter.DoubleToInt64Bits(got.Value.Distance));
                    }
                    // 隠れ判定(止める版): 「基準より近い当たりがある」かが、先頭の距離との比較と同じ
                    foreach (var threshold in new[] { first.Distance * 0.5, first.Distance, first.Distance * 1.5 })
                    {
                        var stop = Picking.Nearest(ray, prepared, threshold, index.At(nx, ny));
                        Assert.Equal(first.Distance >= threshold, stop!.Value.Distance >= threshold);
                    }
                    checkedHits++;
                }
            }
        }
        Assert.True(checkedHits > 4000, "当たりの件数: " + checkedHits);
        Assert.True(checkedMisses > 500, "外れの件数: " + checkedMisses);
    }
}
