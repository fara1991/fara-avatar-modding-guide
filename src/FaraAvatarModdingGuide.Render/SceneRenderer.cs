using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>編集モードの点・線・選んだ面の描画データ(viewport.js の edit-overlay)。作り直すタイミングは <see cref="ViewportCore.OverlayRebuilt"/> のとおり(それ以外では変わらない)。</summary>
public sealed class OverlayBuffers
{
    public required GpuBuffer Lines { get; init; }
    public required GpuBuffer Points { get; init; }
    public required GpuBuffer Faces { get; init; }
    /// <summary>作り直した時点で、頂点選択モードだったか(点は頂点選択モードのときだけ見える)。</summary>
    public bool PointsVisible { get; init; }
    /// <summary>作り直した時点の深度テスト(透過表示のときは、手前の面に隠れない = false)。</summary>
    public bool DepthTest { get; init; }
}

/// <summary>オブジェクト 1 つぶんの描画データ。<see cref="SceneObject.Version"/> が変わったときだけ作り直す。</summary>
public sealed class ObjectBuffers
{
    public int Version { get; set; } = -1;
    public GpuBuffer Mesh { get; } = new(BufferLayout.PositionNormal);
    /// <summary>輪郭(EdgesGeometry(g, 20))。</summary>
    public GpuBuffer Outline { get; } = new(BufferLayout.Position);
    public OverlayBuffers? Overlay { get; set; }
    public GpuBuffer? LoopPreview { get; set; }
    public List<Vec3>? LoopPreviewSource { get; set; }
}

/// <summary>
/// <see cref="ViewportCore"/> の状態から、WebGL に渡す描画の命令(<see cref="Frame"/>)を作る。状態を読むだけで、ロジックには何も書き戻さない。
/// viewport.js の buildScene / makeObject / rebuild / rebuildOverlay / refreshSelection / setXray / animate に当たる。
/// <para><b>契約: <see cref="ViewportCore"/> を作ったら、操作(キー・ポインター・<c>SetMode</c> など)の前にこのクラスを作る。</b>編集モードの点・線・面は
/// <see cref="ViewportCore.OverlayRebuilt"/> の通知を受けた時点の状態で作る(現行が <c>rebuildOverlay</c> を呼ぶ時点の再現)ので、通知より後に作ると、
/// 編集中のオブジェクトのオーバーレイは次の通知まで無いままになる。</para>
/// <para>描く順は three.js の近似: 不透明(renderOrder の小さい順。同じなら、シーンに足した順)→ 透明(renderOrder の小さい順。同じなら、奥から)。
/// three.js は renderOrder → material.id → z(手前から)→ id の順で、同じ renderOrder のあいだは材質ごとにまとまる。ここは足した順なので、
/// 深度テストで決まらない重なり(同じ深さ・深度テストなしのもの)の前後は、three.js と違いうる。画像比較では差が出ていない。</para>
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    public const int BaseColor = 0x9c9c9c;
    public const int OutlineColor = 0xe37d2b, OutlineActiveColor = 0xffb766;
    public const int OverlaySelected = 0xffa22b, OverlayEdge = 0x101010, OverlayVertex = 0x101010, OverlayFace = 0xff9d2b;
    public const int LoopPreviewColor = 0xffe14a;
    public const float XrayOpacity = 0.35f, OverlayFaceOpacity = 0.28f;
    public const float PointSize = 6;

    private readonly ViewportCore core;
    private readonly Dictionary<SceneObject, ObjectBuffers> objects = [];
    private readonly List<int> releases = [];
    private readonly List<GpuBuffer> known = [];
    private readonly Dictionary<int, int> uploaded = [];

    private readonly GpuBuffer grid = new(BufferLayout.PositionColor);
    private readonly GpuBuffer axisX = new(BufferLayout.Position), axisY = new(BufferLayout.Position), axisLine = new(BufferLayout.Position);
    private readonly GpuBuffer ring = new(BufferLayout.Position);
    private readonly Mat4 gridWorld = Mat4.Compose(Vec3.Zero, Quat.FromEulerXYZ(Math.PI / 2, 0, 0), Vec3.One);
    private readonly Mat4 axisWorld = Mat4.Compose(new Vec3(0, 0, 0.002), Quat.Identity, Vec3.One);

    public SceneRenderer(ViewportCore core)
    {
        this.core = core;
        grid.Set(StaticGeometry.Grid(40, 40, 0x4e4e4e, 0x484848));
        axisX.Set(StaticGeometry.LongLine(20, Vec3.UnitX));
        axisY.Set(StaticGeometry.LongLine(20, Vec3.UnitY));
        axisLine.Set(StaticGeometry.LongLine(40, Vec3.UnitX));
        ring.Set(StaticGeometry.Ring(0.12, 0.17, 24));
        known.AddRange([grid, axisX, axisY, axisLine, ring]);
        core.OverlayRebuilt += OnOverlayRebuilt;
        core.OverlayRemoved += OnOverlayRemoved;
    }

    public void Dispose()
    {
        core.OverlayRebuilt -= OnOverlayRebuilt;
        core.OverlayRemoved -= OnOverlayRemoved;
    }

    /// <summary>オブジェクトの描画データ(なければ作る)。</summary>
    public ObjectBuffers BuffersOf(SceneObject obj) => Ensure(obj);

    /// <summary>バッファを送り直したことにしない(WebGL のコンテキストを作り直したあとなど、全部を送り直したいとき)。</summary>
    public void InvalidateUploads() => uploaded.Clear();

    private ObjectBuffers Ensure(SceneObject obj)
    {
        if (!objects.TryGetValue(obj, out var b))
        {
            b = new ObjectBuffers();
            objects[obj] = b;
            known.AddRange([b.Mesh, b.Outline]);
        }
        if (b.Version != obj.Version)
        {
            b.Version = obj.Version;
            b.Mesh.Set(MeshData(obj));
            b.Outline.Set(EdgesGeometry.Build(obj.Triangles, 20));
        }
        return b;
    }

    /// <summary>位置+法線の並び(三角形ごとに、3 頂点とも面の法線)。</summary>
    private static float[] MeshData(SceneObject obj)
    {
        var tris = obj.Triangles;
        var data = new float[tris.Length * 6];
        for (var i = 0; i < tris.Length; i++)
        {
            var p = tris[i];
            var n = obj.TriNormals[i / 3];
            var o = i * 6;
            data[o] = (float)p.X; data[o + 1] = (float)p.Y; data[o + 2] = (float)p.Z;
            data[o + 3] = (float)n.X; data[o + 4] = (float)n.Y; data[o + 5] = (float)n.Z;
        }
        return data;
    }

    // ---------------------------------------------------------------- 編集モードの点・線・面(rebuildOverlay)
    private void OnOverlayRebuilt(SceneObject obj)
    {
        var b = Ensure(obj);
        var em = obj.Em;
        var sel = Rgb.FromHex(OverlaySelected);
        var edge = Rgb.FromHex(OverlayEdge);
        var vert = Rgb.FromHex(OverlayVertex);
        var edges = em.Edges();
        var lines = new float[edges.Count * 12];
        var i = 0;
        foreach (var e in edges)
        {
            var c = em.Esel.Contains(e.Key) ? sel : edge;
            foreach (var p in new[] { e.A.Co, e.B.Co })
            {
                lines[i++] = (float)p.X; lines[i++] = (float)p.Y; lines[i++] = (float)p.Z;
                lines[i++] = c.R; lines[i++] = c.G; lines[i++] = c.B;
            }
        }
        var points = new float[em.Verts.Count * 6];
        i = 0;
        foreach (var v in em.Verts)
        {
            var c = em.Vsel.Contains(v) ? sel : vert;
            points[i++] = (float)v.Co.X; points[i++] = (float)v.Co.Y; points[i++] = (float)v.Co.Z;
            points[i++] = c.R; points[i++] = c.G; points[i++] = c.B;
        }
        var faces = new List<float>();
        foreach (var f in em.Fsel)
            for (var k = 1; k < f.V.Count - 1; k++)
                foreach (var v in new[] { f.V[0], f.V[k], f.V[k + 1] })
                {
                    faces.Add((float)v.Co.X); faces.Add((float)v.Co.Y); faces.Add((float)v.Co.Z);
                }
        var old = b.Overlay;
        var overlay = new OverlayBuffers
        {
            Lines = old?.Lines ?? new GpuBuffer(BufferLayout.PositionColor),
            Points = old?.Points ?? new GpuBuffer(BufferLayout.PositionColor),
            Faces = old?.Faces ?? new GpuBuffer(BufferLayout.Position),
            PointsVisible = core.SelectMode == "vert",
            DepthTest = !core.Xray,
        };
        if (old is null) known.AddRange([overlay.Lines, overlay.Points, overlay.Faces]);
        overlay.Lines.Set(lines);
        overlay.Points.Set(points);
        overlay.Faces.Set([.. faces]);
        b.Overlay = overlay;
    }

    private void OnOverlayRemoved(SceneObject obj)
    {
        if (!objects.TryGetValue(obj, out var b) || b.Overlay is null) return;
        Drop(b.Overlay.Lines); Drop(b.Overlay.Points); Drop(b.Overlay.Faces);
        b.Overlay = null;
    }

    private void Drop(GpuBuffer buffer)
    {
        known.Remove(buffer);
        releases.Add(buffer.Id);
    }

    // ---------------------------------------------------------------- 1 フレーム
    private sealed record Entry(DrawItem Item, double Z, int Sequence);

    /// <summary>いまの状態の描画の命令を作る。縦横比は <see cref="ViewportCore.Resize"/> で決めたもの。</summary>
    public Frame Build()
    {
        // 消えたオブジェクトのバッファを捨てる
        foreach (var gone in objects.Keys.Where(o => !core.Objects.Contains(o)).ToList())
        {
            var b = objects[gone];
            Drop(b.Mesh); Drop(b.Outline);
            if (b.Overlay is not null) { Drop(b.Overlay.Lines); Drop(b.Overlay.Points); Drop(b.Overlay.Faces); }
            if (b.LoopPreview is not null) Drop(b.LoopPreview);
            objects.Remove(gone);
        }

        var (view, projection, cameraWorld) = core.View.Matrices();
        var viewProjection = Mat4.Multiply(projection, view);
        var entries = new List<Entry>();
        var seq = 0;
        void Add(DrawItem item, Vec3? sphereCenterLocal = null)
        {
            double z = 0;
            if (item.Transparent)
                z = viewProjection.ApplyToPoint(item.World.ApplyToPoint(sphereCenterLocal ?? Vec3.Zero)).Z;
            entries.Add(new Entry(item, z, seq++));
        }

        var depth = DrawFlags.DepthTest | DrawFlags.DepthWrite;
        // 方眼(GridHelper)・X 軸(赤)・Y 軸(緑)。軸は方眼の中心線と重なって消えないように 0.002 浮かせる
        Add(new DrawItem(grid, Primitive.Lines, Shading.VertexColor, gridWorld, 0, 0, 0, 1, depth, 0, 0, 0, 0, false, "grid"));
        var (xr, xg, xb) = Rgb.FromHex(0x9a3a4a);
        Add(new DrawItem(axisX, Primitive.LineStrip, Shading.Flat, axisWorld, xr, xg, xb, 1, depth, 0, 0, 0, 0, false, "axis-x"));
        var (yr, yg, yb) = Rgb.FromHex(0x5a8a2a);
        Add(new DrawItem(axisY, Primitive.LineStrip, Shading.Flat, axisWorld, yr, yg, yb, 1, depth, 0, 0, 0, 0, false, "axis-y"));

        var (mr, mg, mb) = Rgb.FromHex(BaseColor);
        foreach (var obj in core.Objects)
        {
            var b = Ensure(obj);
            var world = obj.MatrixWorld;
            var center = obj.SphereCenter;
            var xray = obj.XrayApplied;
            // 透過表示の面は、半透明で深度を書かない
            var meshFlags = DrawFlags.DepthTest | DrawFlags.PolygonOffset | (xray ? DrawFlags.Blend : DrawFlags.DepthWrite);
            if (b.Mesh.VertexCount > 0)
            {
                Add(new DrawItem(b.Mesh, Primitive.Triangles, Shading.Lit, world, mr, mg, mb, xray ? XrayOpacity : 1,
                    meshFlags | (world.Determinant() < 0 ? DrawFlags.FrontFaceCw : 0), 1, 1, 0, 0, xray, obj.Name), center);
            }
            if (core.OutlineVisible(obj) && b.Outline.VertexCount > 0)
            {
                var (r, g, bl) = Rgb.FromHex(core.OutlineIsActive(obj) ? OutlineActiveColor : OutlineColor);
                Add(new DrawItem(b.Outline, Primitive.Lines, Shading.Flat, world, r, g, bl, 1, depth, 0, 0, 0, 0, false, obj.Name + ":outline"));
            }
            if (obj.Mirror && b.Mesh.VertexCount > 0)
            {
                var s = obj.Scale;
                var mirrored = Mat4.Compose(obj.Position, obj.Quaternion, new Vec3(-s.X, s.Y, s.Z));
                Add(new DrawItem(b.Mesh, Primitive.Triangles, Shading.Lit, mirrored, mr, mg, mb, xray ? XrayOpacity : 1,
                    meshFlags | (mirrored.Determinant() < 0 ? DrawFlags.FrontFaceCw : 0), 1, 1, 0, 0, xray, obj.Name + ":mirror"), center);
            }
            if (b.Overlay is { } o)
            {
                var test = o.DepthTest ? DrawFlags.DepthTest : 0;
                // 線と点は不透明(renderOrder 2)。選んだ面は半透明(深度は書かない。-1/-1 でメッシュの面より手前に)
                if (o.Lines.VertexCount > 0)
                    Add(new DrawItem(o.Lines, Primitive.Lines, Shading.VertexColor, world, 0, 0, 0, 1, test | DrawFlags.DepthWrite, 0, 0, 0, 2, false, obj.Name + ":edit-lines"));
                if (o.PointsVisible && o.Points.VertexCount > 0)
                    Add(new DrawItem(o.Points, Primitive.Points, Shading.VertexColor, world, 0, 0, 0, 1, test | DrawFlags.DepthWrite, 0, 0, PointSize, 2, false, obj.Name + ":edit-points"));
                if (o.Faces.VertexCount > 0)
                {
                    var (fr, fg, fb) = Rgb.FromHex(OverlayFace);
                    Add(new DrawItem(o.Faces, Primitive.Triangles, Shading.Flat, world, fr, fg, fb, OverlayFaceOpacity, test | DrawFlags.Blend | DrawFlags.PolygonOffset, -1, -1, 0, 0, true, obj.Name + ":edit-faces"), BoxCenter(o.Faces.Data));
                }
            }
            // ループカットのプレビュー(黄色。手前の面に隠れない)
            if (core.LoopCut?.Preview is { Count: > 0 } preview && obj == core.EditObj)
            {
                if (b.LoopPreview is null) { b.LoopPreview = new GpuBuffer(BufferLayout.Position); known.Add(b.LoopPreview); }
                if (!ReferenceEquals(b.LoopPreviewSource, preview))
                {
                    b.LoopPreviewSource = preview;
                    b.LoopPreview.Set(preview.SelectMany(p => new[] { (float)p.X, (float)p.Y, (float)p.Z }).ToArray());
                }
                var (lr, lg, lb) = Rgb.FromHex(LoopPreviewColor);
                Add(new DrawItem(b.LoopPreview, Primitive.Lines, Shading.Flat, world, lr, lg, lb, 1, DrawFlags.DepthWrite, 0, 0, 0, 11, false, obj.Name + ":loopcut"));
            }
        }

        // 3D カーソル(原点。白い輪。手前の面に隠れない)
        Add(new DrawItem(ring, Primitive.Triangles, Shading.Flat, Mat4.Identity, 1, 1, 1, 1, DrawFlags.DepthWrite, 0, 0, 0, 10, false, "cursor"));
        // 軸固定中の線(操作の中心に、その軸の向きで)
        if (core.AxisLine.Visible && core.Op is { } op)
        {
            var q = StaticGeometry.FromUnitVectors(Vec3.UnitX, core.AxisLine.Direction.Normalized());
            var (ar, ag, ab) = Rgb.FromHex(core.AxisLine.Color);
            Add(new DrawItem(axisLine, Primitive.LineStrip, Shading.Flat, Mat4.Compose(op.Pivot, q, Vec3.One), ar, ag, ab, 1, depth, 0, 0, 0, 0, false, "axis-line"));
        }

        // 描く順: 不透明は renderOrder → 足した順。透明は renderOrder → 奥(z が大きい)から
        var opaque = entries.Where(e => !e.Item.Transparent).OrderBy(e => e.Item.RenderOrder).ThenBy(e => e.Sequence).Select(e => e.Item);
        var transparent = entries.Where(e => e.Item.Transparent).OrderBy(e => e.Item.RenderOrder).ThenByDescending(e => e.Z).ThenBy(e => e.Sequence).Select(e => e.Item);
        var items = opaque.Concat(transparent).ToList();

        var uploads = known.Where(k => k.Version != uploaded.GetValueOrDefault(k.Id, 0)).ToList();
        foreach (var k in uploads) uploaded[k.Id] = k.Version;
        var released = releases.ToList();
        releases.Clear();
        foreach (var id in released) uploaded.Remove(id);

        var e = cameraWorld.E;
        return new Frame
        {
            ViewProjection = ToFloats(viewProjection),
            CameraPosition = new Vec3(e[12], e[13], e[14]),
            Items = items,
            Uploads = uploads,
            Releases = released,
        };
    }

    private static Vec3 BoxCenter(float[] positions)
    {
        if (positions.Length < 3) return Vec3.Zero;
        double minX = double.PositiveInfinity, minY = minX, minZ = minX, maxX = double.NegativeInfinity, maxY = maxX, maxZ = maxX;
        for (var i = 0; i + 2 < positions.Length; i += 3)
        {
            minX = Math.Min(minX, positions[i]); maxX = Math.Max(maxX, positions[i]);
            minY = Math.Min(minY, positions[i + 1]); maxY = Math.Max(maxY, positions[i + 1]);
            minZ = Math.Min(minZ, positions[i + 2]); maxZ = Math.Max(maxZ, positions[i + 2]);
        }
        return new Vec3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
    }

    public static float[] ToFloats(Mat4 m)
    {
        var f = new float[16];
        for (var i = 0; i < 16; i++) f[i] = (float)m.E[i];
        return f;
    }
}
