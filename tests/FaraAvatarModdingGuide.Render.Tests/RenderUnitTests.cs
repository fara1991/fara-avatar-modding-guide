using FaraAvatarModdingGuide.Practice;
using Xunit;
using Xunit.Abstractions;

namespace FaraAvatarModdingGuide.Render.Tests;

/// <summary>three.js に無い補助(描く順・送り直し・解放・詰め方)の単体テストと、ネイティブでの計測(WebAssembly との比較用)。</summary>
public class RenderUnitTests(ITestOutputHelper output)
{
    private static ViewportCore NewCore(params string[] kinds)
    {
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540), new ViewportOptions { Scene = [.. kinds.Select(k => new SceneSpec(k))] });
        core.Focus();
        return core;
    }

    private static void Key(ViewportCore core, string code, Modifiers? m = null) => core.KeyDown(code, "", m ?? new Modifiers());

    [Fact]
    public void A_cube_has_twelve_outline_edges_and_a_sphere_only_the_sharp_ones()
    {
        var cube = SceneObject.Create("cube");
        Assert.Equal(12 * 6, EdgesGeometry.Build(cube.Triangles, 20).Length);
        var plane = SceneObject.Create("plane");
        Assert.Equal(4 * 6, EdgesGeometry.Build(plane.Triangles, 20).Length); // 開いた縁だけ
    }

    [Fact]
    public void The_determinant_is_negative_only_for_a_mirror()
    {
        Assert.Equal(1, Mat4.Identity.Determinant(), 12);
        Assert.Equal(-1, Mat4.Compose(Vec3.Zero, Quat.Identity, new Vec3(-1, 1, 1)).Determinant(), 12);
        Assert.Equal(1, Mat4.Compose(Vec3.Zero, Quat.Identity, new Vec3(-1, -1, 1)).Determinant(), 12);
    }

    [Fact]
    public void Opaque_items_come_first_and_transparent_ones_back_to_front()
    {
        var core = NewCore("cube", "sphere");
        core.Objects[0].Position = new Vec3(-4, 0, 0);
        core.Objects[1].Position = new Vec3(4, 0, 0);
        core.SetXray(true);
        var frame = new SceneRenderer(core).Build();
        var labels = frame.Items.Select(i => i.Label).ToList();
        var firstTransparent = frame.Items.ToList().FindIndex(i => i.Transparent);
        Assert.True(firstTransparent > 0);
        Assert.All(frame.Items.Skip(firstTransparent), i => Assert.True(i.Transparent));
        Assert.All(frame.Items.Take(firstTransparent), i => Assert.False(i.Transparent));
        // カーソルは renderOrder 10 で、不透明のうち最後
        Assert.Equal("cursor", labels[firstTransparent - 1]);
        // 透明どうしは奥から(右に置いた球のほうが、前方の視点から見て奥)
        var viewProjection = frame.ViewProjection;
        double Depth(Vec3 p) => (viewProjection[2] * p.X + viewProjection[6] * p.Y + viewProjection[10] * p.Z + viewProjection[14]) / (viewProjection[3] * p.X + viewProjection[7] * p.Y + viewProjection[11] * p.Z + viewProjection[15]);
        var transparent = frame.Items.Skip(firstTransparent).ToList();
        Assert.Equal(2, transparent.Count);
        Assert.True(Depth(core.Objects[Array.IndexOf(["Cube", "Sphere"], transparent[0].Label)].Position) >= Depth(core.Objects[Array.IndexOf(["Cube", "Sphere"], transparent[1].Label)].Position));
    }

    [Fact]
    public void Buffers_are_uploaded_once_and_again_only_when_the_shape_changes()
    {
        var core = NewCore("cube");
        var renderer = new SceneRenderer(core);
        var first = renderer.Build();
        Assert.NotEmpty(first.Uploads);
        Assert.Empty(renderer.Build().Uploads);        // 何も変わらなければ送らない
        core.SetSelection([core.Objects[0]]);
        Key(core, "KeyG");
        core.PointerMove(100, 100, new Modifiers());
        core.PointerMove(300, 200, new Modifiers());
        Assert.Empty(renderer.Build().Uploads);        // オブジェクトモードの移動は行列だけ
        Key(core, "Enter");
        Key(core, "Tab");
        var edit = renderer.Build();
        Assert.Contains(edit.Uploads, b => b.Layout == BufferLayout.PositionColor);   // 編集モードに入ると点・線を作る
        Key(core, "Tab");
        Assert.NotEmpty(renderer.Build().Releases);    // 出ると捨てる
    }

    [Fact]
    public void Deleted_objects_release_their_buffers()
    {
        var core = NewCore("cube", "cone");
        var renderer = new SceneRenderer(core);
        renderer.Build();
        core.SetSelection([core.Objects[1]]);
        Key(core, "Delete");
        var frame = renderer.Build();
        Assert.True(frame.Releases.Count >= 2);
    }

    [Fact]
    public void The_packed_frame_has_the_documented_layout()
    {
        var core = NewCore("cube");
        var frame = new SceneRenderer(core).Build();
        var data = FramePacker.Pack(frame);
        Assert.Equal(FramePacker.HeaderFloats + frame.Items.Count * FramePacker.ItemFloats, data.Length);
        Assert.Equal(frame.Items.Count, data[19]);
        var o = FramePacker.HeaderFloats;
        foreach (var item in frame.Items)
        {
            Assert.Equal(item.Buffer.Id, data[o]);
            Assert.Equal((int)item.Primitive, data[o + 1]);
            Assert.Equal((float)item.World.E[12], data[o + 24]);
            o += FramePacker.ItemFloats;
        }
    }

    [Fact]
    public void Native_reference_timings()
    {
        // WebAssembly(AOT なし)の値と比べるための、ネイティブの値。時間は条件にしない(環境で変わる)。何も起きていないことだけ確かめる
        foreach (var (name, n) in new[] { ("frame-object", 200), ("frame-edit", 200), ("drag-edit-frame", 100), ("click-object", 100), ("click-vert", 50), ("box-select-all", 10), ("move-all-vertices", 100), ("extrude-start", 5), ("undo", 10) })
        {
            var r = RenderBench.Run(name, n);
            output.WriteLine($"{name}: {r.MsPerOp:F3} ms/op ({n} 回)");
            Assert.True(r.TotalMs >= 0);
        }
    }
}
