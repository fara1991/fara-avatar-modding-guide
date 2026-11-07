using FaraAvatarModdingGuide.Practice;
using Xunit;

namespace FaraAvatarModdingGuide.Render.Tests;

/// <summary>Unity 風の画面の Scene ビューの描画の命令(<see cref="UnitySceneRenderer"/>)。three.js の描く順・材質の決まりどおりに作ること。</summary>
public class UnitySceneRendererTests
{
    private static (UnityEditorCore Core, UnitySceneRenderer Renderer) Make(params string[] instances)
    {
        var core = new UnityEditorCore(new UnityOptions { Instances = instances.Length == 0 ? ["Avatar"] : instances });
        core.Resize(new ViewRect(0, 0, 711, 400), 711, 400);
        core.Animate(0);
        return (core, new UnitySceneRenderer(core));
    }

    [Fact]
    public void Frame_uses_sky_background_and_unity_lighting()
    {
        var (_, r) = Make();
        var frame = r.Build();
        Assert.Equal(FrameBackground.Sky, frame.Background);
        Assert.Equal(FrameLighting.Unity, frame.Lighting);
    }

    [Fact]
    public void Opaque_parts_come_first_then_grid_then_gizmo()
    {
        var (core, r) = Make();
        core.Select(core.FindByName("Avatar")!.Id);
        core.SetTool("move");
        core.Animate(16);
        var items = r.Build().Items;
        var firstTransparent = items.ToList().FindIndex(i => i.Transparent);
        Assert.True(firstTransparent > 0);
        Assert.All(items.Take(firstTransparent), i => Assert.False(i.Transparent));
        Assert.Equal("grid", items[firstTransparent].Label);
        Assert.All(items.Skip(firstTransparent + 1), i => Assert.StartsWith("gizmo:", i.Label));
        Assert.True(items.Count > firstTransparent + 1, "移動ツールのギズモが描かれる");
    }

    [Fact]
    public void Gizmo_is_hidden_with_no_selection_and_hit_proxies_are_not_drawn()
    {
        var (core, r) = Make();
        core.ClearSelection();
        core.Animate(16);
        Assert.DoesNotContain(r.Build().Items, i => i.Label.StartsWith("gizmo:", StringComparison.Ordinal));
        core.Select(core.FindByName("Avatar")!.Id);
        core.SetTool("rotate");
        core.Animate(32);
        var drawn = r.Build().Items.Count(i => i.Label.StartsWith("gizmo:", StringComparison.Ordinal));
        Assert.Equal(core.GizmoItems.Count(g => !g.Hit), drawn);
    }

    [Fact]
    public void Parts_are_front_face_only_except_double_sided_ones()
    {
        var (core, r) = Make("Avatar");
        var items = r.Build().Items.Where(i => !i.Transparent).ToList();
        var parts = core.Nodes.Where(n => n.Parts is not null).SelectMany(n => n.Parts!).Where(p => p.Mesh.Visible).ToList();
        Assert.Equal(parts.Count, items.Count);
        for (var i = 0; i < parts.Count; i++)
            Assert.Equal(parts[i].DoubleSided, (items[i].Flags & DrawFlags.CullBack) == 0);
    }

    [Fact]
    public void Inactive_parts_are_not_drawn_and_buffers_upload_once()
    {
        var (core, r) = Make("Avatar");
        var first = r.Build();
        Assert.NotEmpty(first.Uploads);
        Assert.Empty(r.Build().Uploads);   // 変わっていないバッファは送り直さない
        var before = first.Items.Count(i => !i.Transparent);
        var shirt = core.FindByName("Avatar/Shirt_Default")!;
        core.SetActive(shirt.Id, false);
        core.Animate(16);
        Assert.True(r.Build().Items.Count(i => !i.Transparent) < before);
        r.InvalidateUploads();
        Assert.NotEmpty(r.Build().Uploads);
    }

    [Fact]
    public void Matrices_are_packed_for_the_current_camera()
    {
        var (core, r) = Make();
        var f = r.Build();
        Assert.Equal(16, f.ViewProjection.Length);
        Assert.Equal(core.Camera.MatrixWorld.E[12], f.CameraPosition.X, 12);
        Assert.Equal(core.Camera.MatrixWorld.E[13], f.CameraPosition.Y, 12);
    }
}
