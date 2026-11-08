using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>
/// <see cref="UnityEditorCore"/>(Unity 風の画面)の Scene ビューの描画の命令(<see cref="Frame"/>)を作る。状態を読むだけで、ロジックには何も書き戻さない。
/// unity.js の constructor(シーン・光・方眼)・rebuild(部品)・updateGizmo・animate(2 回の render)に当たる。
/// 描く順は three.js と同じ: スカイ → 不透明(部品。作った順)→ 透明(方眼)→ ギズモ(別のシーンの render。透明で、奥から)。
/// 毎フレームの <see cref="UnityEditorCore.Animate"/> のあと(ワールド行列が最新)に <see cref="Build"/> を呼ぶ。
/// </summary>
public sealed class UnitySceneRenderer
{
    public const int GridCenter = 0x8a8a8a, GridLine = 0x6f6f6f;
    public const float GridOpacity = 0.55f, GizmoOpacity = 0.95f;

    private readonly UnityEditorCore core;
    private readonly Dictionary<UnityMesh, GpuBuffer> buffers = [];
    private readonly GpuBuffer grid = new(BufferLayout.PositionColor);
    private readonly List<GpuBuffer> known = [];
    private readonly Dictionary<int, int> uploaded = [];

    public UnitySceneRenderer(UnityEditorCore core)
    {
        this.core = core;
        grid.Set(StaticGeometry.Grid(20, 20, GridCenter, GridLine));
        known.Add(grid);
    }

    /// <summary>バッファを送り直したことにしない(WebGL のコンテキストを作り直したあとなど、全部を送り直したいとき)。</summary>
    public void InvalidateUploads() => uploaded.Clear();

    private GpuBuffer BufferOf(UnityMesh mesh)
    {
        if (buffers.TryGetValue(mesh, out var b)) return b;
        b = new GpuBuffer(BufferLayout.PositionNormal);
        // インデックスを展開した三角形(位置 + 法線)
        var data = new float[mesh.Indices.Length * 6];
        for (var i = 0; i < mesh.Indices.Length; i++)
        {
            var v = mesh.Indices[i];
            Array.Copy(mesh.Positions, v * 3, data, i * 6, 3);
            Array.Copy(mesh.Normals, v * 3, data, i * 6 + 3, 3);
        }
        b.Set(data);
        buffers[mesh] = b;
        known.Add(b);
        return b;
    }

    private sealed record Entry(DrawItem Item, double Z, int Sequence);

    public Frame Build()
    {
        var (view, projection, cameraWorld) = (core.Camera.MatrixWorldInverse, core.Camera.Projection, core.Camera.MatrixWorld);
        var viewProjection = Mat4.Multiply(projection, view);
        var opaque = new List<DrawItem>();
        var transparent = new List<Entry>();
        var gizmo = new List<Entry>();
        var seq = 0;
        double Z(Mat4 world, Vec3 center) => viewProjection.ApplyToPoint(world.ApplyToPoint(center)).Z;

        // 部品(MeshStandardMaterial。表だけ。hood・hair は両面)。作った順 = Hierarchy のノードの順 → 部品の順
        foreach (var n in core.Nodes)
        {
            if (n.Parts is null) continue;
            foreach (var p in n.Parts)
            {
                if (!p.Mesh.Visible) continue;
                var world = p.Mesh.MatrixWorld;
                var (r, g, b) = Rgb.FromHex(p.Color);
                var flags = DrawFlags.DepthTest | DrawFlags.DepthWrite | (world.Determinant() < 0 ? DrawFlags.FrontFaceCw : 0) | (p.DoubleSided ? 0 : DrawFlags.CullBack);
                opaque.Add(new DrawItem(BufferOf(p.Geometry), Primitive.Triangles, Shading.Lit, world, r, g, b, 1, flags, 0, 0, 0, 0, false, $"{n.Name}:{p.Shape}"));
            }
        }

        // 方眼(GridHelper(20, 20, 0x8a8a8a, 0x6f6f6f)。半透明 0.55 で、深度は書く)
        transparent.Add(new Entry(new DrawItem(grid, Primitive.Lines, Shading.VertexColor, Mat4.Identity, 0, 0, 0, GridOpacity, DrawFlags.DepthTest | DrawFlags.DepthWrite | DrawFlags.Blend, 0, 0, 0, 0, true, "grid"), Z(Mat4.Identity, Vec3.Zero), seq++));

        // ギズモ(別のシーンを、深度を消して描く。半透明 0.95・深度テストなし・表だけ。当たり判定だけの見えない部品は描かない)
        if (core.GizmoRoot.Visible)
        {
            foreach (var it in core.GizmoItems)
            {
                if (it.Hit) continue;
                var world = it.Obj.MatrixWorld;
                var (r, g, b) = Rgb.FromHex(it.Color);
                var flags = DrawFlags.Blend | DrawFlags.DepthWrite | (world.Determinant() < 0 ? DrawFlags.FrontFaceCw : 0) | DrawFlags.CullBack;
                gizmo.Add(new Entry(new DrawItem(BufferOf(it.Geometry), Primitive.Triangles, Shading.Flat, world, r, g, b, GizmoOpacity, flags, 0, 0, 0, 0, true, $"gizmo:{it.GeometryType}"), Z(world, it.Geometry.SphereCenter), seq++));
            }
        }

        // 透明は renderOrder → 奥(z が大きい)から → 作った順
        var items = new List<DrawItem>(opaque);
        items.AddRange(transparent.OrderByDescending(e => e.Z).ThenBy(e => e.Sequence).Select(e => e.Item));
        items.AddRange(gizmo.OrderByDescending(e => e.Z).ThenBy(e => e.Sequence).Select(e => e.Item));

        var uploads = known.Where(k => k.Version != uploaded.GetValueOrDefault(k.Id, 0)).ToList();
        foreach (var k in uploads) uploaded[k.Id] = k.Version;
        var e = cameraWorld.E;
        return new Frame
        {
            ViewProjection = SceneRenderer.ToFloats(viewProjection),
            Projection = SceneRenderer.ToFloats(projection),
            View = view,
            CameraPosition = new Vec3(e[12], e[13], e[14]),
            Lighting = FrameLighting.Unity,
            Background = FrameBackground.Sky,
            Items = items,
            Uploads = uploads,
            Releases = [],
        };
    }
}
