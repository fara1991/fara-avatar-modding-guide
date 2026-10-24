namespace FaraAvatarModdingGuide.Practice;

/// <summary>Scene ビューの視点(unity.js の view)。注視点・距離・yaw・pitch。</summary>
public sealed class UnityView
{
    public Vec3 Target { get; set; } = new(0, 0.95, 0);
    public double Dist { get; set; } = 3.2;
    public double Yaw { get; set; } = 0.45;
    public double Pitch { get; set; } = 0.18;

    public void Reset()
    {
        Dist = 3.2;
        Yaw = 0.45;
        Pitch = 0.18;
        Target = new Vec3(0, 0.95, 0);
    }

    /// <summary>視線の向き(注視点からカメラへ)。</summary>
    public Vec3 Dir => new(Math.Sin(Yaw) * Math.Cos(Pitch), Math.Sin(Pitch), Math.Cos(Yaw) * Math.Cos(Pitch));
}

/// <summary>
/// Scene ビューのカメラ(three.js の PerspectiveCamera(50, aspect, 0.05, 200))。位置と向きは毎フレーム(<see cref="Update"/>)で視点から決める。
/// レイ・画面への投影は、その時点の(最後のフレームの)行列を使う(unity.js と同じ)。
/// </summary>
public sealed class UnityCamera
{
    public const double Fov = 50, Near = 0.05, Far = 200;

    public UnityCamera() => SetAspect(1);

    public Vec3 Position { get; private set; } = Vec3.Zero;
    public double Aspect { get; private set; } = 1;
    public Mat4 MatrixWorld { get; private set; } = Mat4.Identity;
    public Mat4 MatrixWorldInverse { get; private set; } = Mat4.Identity;
    public Mat4 Projection { get; private set; } = Mat4.Identity;
    public Mat4 ProjectionInverse { get; private set; } = Mat4.Identity;

    public void SetAspect(double aspect)
    {
        Aspect = aspect;
        var top = Near * Math.Tan(Js.Deg * 0.5 * Fov);
        var height = 2 * top;
        var width = aspect * height;
        var left = -0.5 * width;
        Projection = Mat4.MakePerspective(left, left + width, top, top - height, Near, Far);
        ProjectionInverse = Projection.Inverted();
    }

    /// <summary>animate(): camera.position = target + dir × dist、lookAt(target)、updateMatrixWorld()。</summary>
    public void Update(UnityView v)
    {
        Position = v.Target.AddScaled(v.Dir, v.Dist);
        var up = new Vec3(0, 1, 0);
        var z = Position - v.Target;
        if (z.LengthSq == 0) z = z with { Z = 1 };
        z = z.Normalized();
        var x = up.Cross(z);
        if (x.LengthSq == 0)
        {
            z = Math.Abs(up.Z) == 1 ? z with { X = z.X + 0.0001 } : z with { Z = z.Z + 0.0001 };
            z = z.Normalized();
            x = up.Cross(z);
        }
        x = x.Normalized();
        var y = z.Cross(x);
        var q = Quat.FromRotationBasis(x, y, z);
        MatrixWorld = Mat4.Compose(Position, q, Vec3.One);
        MatrixWorldInverse = MatrixWorld.Inverted();
    }

    /// <summary>画面の点(NDC)からのレイ(Raycaster.setFromCamera)。</summary>
    public Ray RayFromNdc(double x, double y)
    {
        var origin = new Vec3(MatrixWorld.E[12], MatrixWorld.E[13], MatrixWorld.E[14]);
        var direction = (MatrixWorld.ApplyToPoint(ProjectionInverse.ApplyToPoint(new Vec3(x, y, 0.5))) - origin).Normalized();
        return new Ray(origin, direction);
    }

    /// <summary>Vector3.project(camera)。</summary>
    public Vec3 Project(Vec3 world) => Projection.ApplyToPoint(MatrixWorldInverse.ApplyToPoint(world));
}

/// <summary>ギズモの部品 1 つ。Hit は当たり判定だけの見えないメッシュ(Handle が付く)。</summary>
public sealed class UnityGizmoItem
{
    /// <summary>three.js のジオメトリの種類(CylinderGeometry・ConeGeometry・BoxGeometry・TorusGeometry)。</summary>
    public required string GeometryType { get; init; }
    public required Obj3D Obj { get; init; }
    public required UnityMesh Geometry { get; init; }
    public required int Color { get; init; }
    public bool Hit { get; init; }
    public string? HandleKind { get; init; }
    public string? HandleAxis { get; init; }
}

public sealed partial class UnityEditorCore
{
    private const double GizmoOpacity = 0.95;

    /// <summary>Scene ビューの位置・大きさ(getBoundingClientRect。左上が原点)。座標の変換(NDC・画面上の軸の向き)に使う。</summary>
    public ViewRect Rect { get; private set; } = new(0, 0, 0, 0);

    /// <summary>ビューの大きさが変わったとき(resize())。rect は小数の getBoundingClientRect、clientWidth / clientHeight は整数(カメラの縦横比に使う)。</summary>
    public void Resize(ViewRect rect, double clientWidth, double clientHeight)
    {
        Rect = rect;
        if (clientWidth <= 0 || clientHeight <= 0) return;
        Camera.SetAspect(clientWidth / clientHeight);
    }

    // ---------------------------------------------------------------- ギズモ(W 移動 / E 回転 / R 拡大縮小。Unity と同じ色)
    /// <summary>ギズモ(three.js の gizmo グループ)。位置・大きさは毎フレームの <see cref="Animate"/> で決める。</summary>
    public Obj3D GizmoRoot { get; } = new("gizmo");

    public List<UnityGizmoItem> GizmoItems { get; } = [];
    private readonly List<UnityGizmoItem> handles = [];

    private static readonly Dictionary<string, UnityMesh> gizmoCache = [];

    /// <summary>ギズモの部品の形(line・cone・tip・hitline・ring・hitring・center・hitcenter)。three.js の translate / rotateX を掛けたあとの頂点。</summary>
    public static UnityMesh GizmoGeometry(string key)
    {
        if (gizmoCache.TryGetValue(key, out var m)) return m;
        m = key switch
        {
            "line" => UnityMesh.Cylinder(0.012, 0.012, 1, 8).Translate(0, 0.5, 0),
            "cone" => UnityMesh.Cone(0.06, 0.2, 16).Translate(0, 1.05, 0),
            "tip" => UnityMesh.Box(0.1, 0.1, 0.1).Translate(0, 1, 0),
            "hitline" => UnityMesh.Cylinder(0.08, 0.08, 1.2, 8).Translate(0, 0.6, 0),
            "ring" => UnityMesh.Torus(0.9, 0.012, 8, 64).RotateX(Math.PI / 2),
            "hitring" => UnityMesh.Torus(0.9, 0.07, 8, 32).RotateX(Math.PI / 2),
            "center" => UnityMesh.Box(0.13, 0.13, 0.13),
            "hitcenter" => UnityMesh.Box(0.25, 0.25, 0.25),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知のギズモの形"),
        };
        gizmoCache[key] = m;
        return m;
    }

    private void UpdateGizmo()
    {
        GizmoRoot.Clear();
        GizmoItems.Clear();
        handles.Clear();
        var n = Get(Selected);
        if (n is null || Tool == "hand" || pose is not null) return;
        var colors = new Dictionary<string, int> { ["x"] = 0xdb3b3b, ["y"] = 0x8fd14f, ["z"] = 0x3a7bd5 };
        var dirs = new Dictionary<string, Vec3> { ["x"] = new(1, 0, 0), ["y"] = new(0, 1, 0), ["z"] = new(0, 0, 1) };
        UnityGizmoItem Item(Obj3D parent, string type, UnityMesh g, int color, bool hit = false, string? kind = null, string? axis = null)
        {
            var o = new Obj3D();
            parent.Add(o);
            var item = new UnityGizmoItem { GeometryType = type, Obj = o, Geometry = g, Color = color, Hit = hit, HandleKind = kind, HandleAxis = axis };
            GizmoItems.Add(item);
            if (hit) handles.Add(item);
            return item;
        }
        foreach (var axis in new[] { "x", "y", "z" })
        {
            var g = new Obj3D();
            g.SetQuaternion(UnityMath.FromUnitVectors(new Vec3(0, 1, 0), dirs[axis]));
            if (Tool is "move" or "scale")
            {
                Item(g, "CylinderGeometry", GizmoGeometry("line"), colors[axis]);
                if (Tool == "move") Item(g, "ConeGeometry", GizmoGeometry("cone"), colors[axis]);
                else Item(g, "BoxGeometry", GizmoGeometry("tip"), colors[axis]);
                Item(g, "CylinderGeometry", GizmoGeometry("hitline"), 0xffffff, true, Tool, axis);
            }
            else
            {
                Item(g, "TorusGeometry", GizmoGeometry("ring"), colors[axis]);
                Item(g, "TorusGeometry", GizmoGeometry("hitring"), 0xffffff, true, "rotate", axis);
            }
            GizmoRoot.Add(g);
        }
        if (Tool == "scale")
        {
            Item(GizmoRoot, "BoxGeometry", GizmoGeometry("center"), 0xcccccc);
            Item(GizmoRoot, "BoxGeometry", GizmoGeometry("hitcenter"), 0xffffff, true, "scale", "all");
        }
    }

    private void PlaceGizmo()
    {
        var n = Get(Selected);
        if (n is null || GizmoRoot.Children.Count == 0) { GizmoRoot.Visible = false; return; }
        GizmoRoot.Visible = true;
        n.Obj!.UpdateMatrixWorld();
        var pos = n.Obj.GetWorldPosition();
        GizmoRoot.Position = pos;
        var s = Camera.Position.DistanceTo(pos) * 0.14;
        GizmoRoot.Scale = new Vec3(s, s, s);
    }

    /// <summary>(x, y) にあるギズモの矢印・輪(種類と軸)。無ければ null。</summary>
    public (string Kind, string Axis)? PickGizmoHandle(double x, double y)
    {
        var h = PickHandle(x, y);
        return h is null ? null : (h.HandleKind!, h.HandleAxis!);
    }

    private UnityGizmoItem? PickHandle(double x, double y)
    {
        if (handles.Count == 0 || !GizmoRoot.Visible) return null;
        GizmoRoot.UpdateMatrixWorld(true);
        var ray = Camera.RayFromNdc(Ndc(x, y).X, Ndc(x, y).Y);
        UnityGizmoItem? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var h in handles)
        {
            foreach (var d in HitsOnMesh(ray, h.Obj.MatrixWorld, h.Geometry, false))
            {
                if (d < bestDistance) { bestDistance = d; best = h; }
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- レイ判定(three.js の Mesh.raycast と同じ順)
    /// <summary>メッシュに当たる距離(近い順ではなく三角形の並び順)。doubleSided でなければ、裏(レイに背を向けた面)には当たらない(FrontSide)。</summary>
    internal static List<double> HitsOnMesh(Ray ray, Mat4 matrixWorld, UnityMesh g, bool doubleSided)
    {
        var hits = new List<double>();
        if (g.VertexCount == 0) return hits;
        var center = matrixWorld.ApplyToPoint(g.SphereCenter);
        var radius = g.SphereRadius * matrixWorld.MaxScaleOnAxis();
        var origin0 = ray.At(0);
        var dx = origin0.X - center.X; var dy = origin0.Y - center.Y; var dz = origin0.Z - center.Z;
        if (!(dx * dx + dy * dy + dz * dz <= radius * radius) && !Picking.HitsSphere(new Ray(origin0, ray.Direction), center, radius)) return hits;
        var inverse = matrixWorld.Inverted();
        var local = new Ray(inverse.ApplyToPoint(ray.Origin), inverse.TransformDirection(ray.Direction));
        if (!Picking.HitsBox(local, g.BoundsMin, g.BoundsMax)) return hits;
        var idx = g.Indices;
        var pos = g.Positions;
        Vec3 P(int i) => new(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]);
        for (var i = 0; i + 2 < idx.Length; i += 3)
        {
            if (RayTriangle(local, P(idx[i]), P(idx[i + 1]), P(idx[i + 2]), !doubleSided) is not { } t) continue;
            hits.Add(ray.Origin.DistanceTo(matrixWorld.ApplyToPoint(local.At(t))));
        }
        return hits;
    }

    /// <summary>Ray.intersectTriangle(a, b, c, backfaceCulling)。</summary>
    private static double? RayTriangle(Ray ray, Vec3 a, Vec3 b, Vec3 c, bool backfaceCulling)
    {
        var edge1 = b - a;
        var edge2 = c - a;
        var normal = edge1.Cross(edge2);
        var ddn = ray.Direction.Dot(normal);
        double sign;
        if (ddn > 0) { if (backfaceCulling) return null; sign = 1; }
        else if (ddn < 0) { sign = -1; ddn = -ddn; }
        else return null;
        var diff = ray.Origin - a;
        var ddqxe2 = sign * ray.Direction.Dot(diff.Cross(edge2));
        if (ddqxe2 < 0) return null;
        var dde1xq = sign * ray.Direction.Dot(edge1.Cross(diff));
        if (dde1xq < 0) return null;
        if (ddqxe2 + dde1xq > ddn) return null;
        var qdn = -sign * diff.Dot(normal);
        if (qdn < 0) return null;
        return qdn / ddn;
    }

    private (double X, double Y) Ndc(double x, double y) => (x / Rect.Width * 2 - 1, -(y / Rect.Height) * 2 + 1);

    /// <summary>Scene ビューの点(左上からのピクセル)の下のメッシュ(ノードの ID)。</summary>
    public string? PickMesh(double x, double y)
    {
        var (nx, ny) = Ndc(x, y);
        var ray = Camera.RayFromNdc(nx, ny);
        string? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var n in nodes.Values)
        {
            if (n.Parts is null) continue;
            foreach (var p in n.Parts)
            {
                if (!p.Mesh.Visible) continue;
                foreach (var d in HitsOnMesh(ray, p.Mesh.MatrixWorld, p.Geometry, p.DoubleSided))
                {
                    if (d < bestDistance) { bestDistance = d; best = p.NodeId; }
                }
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- Scene ビューのマウス
    private sealed class UnityDrag
    {
        public required string Kind { get; init; }
        public (double X, double Y) Last { get; set; }
        public (double X, double Y) Start { get; init; }
        public double Moved { get; set; }
        public UnityGizmoItem? Handle { get; init; }
        public Vec3 WorldPos { get; init; }
        public Quat WorldQuat { get; init; }
        public Vec3 Scale { get; init; }
    }

    private UnityDrag? drag;

    /// <summary>ポインターを押した。button は 0 左・1 中・2 右。preventDefault するなら true。</summary>
    public bool ScenePointerDown(int button, Modifiers m, double x, double y)
    {
        ClosePopup();
        var p = (x, y);
        if ((button == 0 && m.Alt) || (button == 0 && Tool == "hand" && !m.Alt))
        {
            drag = new UnityDrag { Kind = m.Alt ? "orbit" : "pan", Last = p, Start = p };
        }
        else if (button == 1)
        {
            drag = new UnityDrag { Kind = "pan", Last = p, Start = p };
            return true;
        }
        else if (button == 2)
        {
            drag = new UnityDrag { Kind = "look", Last = p, Start = p };
        }
        else if (button == 0)
        {
            var handle = PickHandle(x, y);
            if (handle is not null)
            {
                var n = Get(Selected)!;
                PushUndo();
                n.Obj!.UpdateMatrixWorld();
                drag = new UnityDrag
                {
                    Kind = "gizmo", Handle = handle, Last = p, Start = p,
                    WorldPos = n.Obj.GetWorldPosition(), WorldQuat = n.Obj.GetWorldQuaternion(), Scale = n.Obj.Scale,
                };
            }
            else drag = new UnityDrag { Kind = "click", Last = p, Start = p };
        }
        return false;
    }

    public void ScenePointerMove(double x, double y)
    {
        var d = drag;
        if (d is null) return;
        double dx = x - d.Last.X, dy = y - d.Last.Y;
        d.Moved += Math.Abs(dx) + Math.Abs(dy);
        d.Last = (x, y);
        if (d.Kind == "orbit") OrbitBy(-dx * 0.008, dy * 0.008);
        else if (d.Kind == "pan")
        {
            var k = View.Dist * 0.0018;
            var e = Camera.MatrixWorld.E;
            var right = new Vec3(e[0], e[1], e[2]);
            var up = new Vec3(e[4], e[5], e[6]);
            View.Target = View.Target.AddScaled(right, -dx * k).AddScaled(up, dy * k);
        }
        else if (d.Kind == "look")
        {
            // 右ドラッグ: カメラの位置はそのままで向きを変える
            var pos = Camera.Position;
            OrbitBy(-dx * 0.005, dy * 0.005);
            View.Target = pos.AddScaled(View.Dir, -View.Dist);
        }
        else if (d.Kind == "gizmo") DragGizmo(d, x, y);
    }

    public void ScenePointerUp(double x, double y)
    {
        var d = drag;
        drag = null;
        if (d is null) return;
        if (d.Kind == "click")
        {
            var id = PickMesh(x, y);
            if (id is not null) Select(id, "scene");
            else
            {
                Selected = null;
                Render();
                UpdateGizmo();
                Emit("select", ("id", null), ("name", null), ("source", "scene"));
            }
        }
        else if (d.Kind == "gizmo")
        {
            var n = Get(Selected)!;
            if (d.Moved < 2) { undoStack.RemoveAt(undoStack.Count - 1); return; }
            ReadTransform(n);
            Touch();
            var kind = d.Handle!.HandleKind!;
            SetStatus($"{n.Name}: {(kind == "move" ? "移動" : kind == "rotate" ? "回転" : "拡大縮小")}しました");
            Emit("transform", ("id", n.Id), ("name", n.Name), ("kind", kind), ("axis", d.Handle.HandleAxis), ("final", true));
        }
        else if (d.Moved > 2)
        {
            Emit("view", ("action", d.Kind));
        }
    }

    /// <summary>ホイール(下へ回すと遠ざかる)。preventDefault するなら true(フォーカスが無くて wheelNeedsFocus のときは false = ページがスクロールする)。</summary>
    public bool SceneWheel(double deltaY)
    {
        if (options.WheelNeedsFocus && !Focused) return false;
        View.Dist = Math.Clamp(View.Dist * (deltaY > 0 ? 1.12 : 1 / 1.12), 0.3, 50);
        Emit("view", ("action", "zoom"));
        return true;
    }

    private void DragGizmo(UnityDrag d, double x, double y)
    {
        var n = Get(Selected)!;
        var kind = d.Handle!.HandleKind!;
        var axis = d.Handle.HandleAxis!;
        double tx = x - d.Start.X, ty = y - d.Start.Y;
        var dirs = new Dictionary<string, Vec3> { ["x"] = new(1, 0, 0), ["y"] = new(0, 1, 0), ["z"] = new(0, 0, 1) };
        // 軸の画面上の向き
        (double X, double Y, double Len) ScreenDir(Vec3 v)
        {
            var a = Camera.Project(d.WorldPos);
            var b = Camera.Project(d.WorldPos + v);
            double sx = (b.X - a.X) * Rect.Width / 2, sy = -(b.Y - a.Y) * Rect.Height / 2;
            var length = Math.Sqrt(sx * sx + sy * sy);
            var div = length == 0 ? 1 : length;
            return (sx / div, sy / div, Math.Max(1, length));
        }
        if (kind == "move")
        {
            var (ux, uy, len) = ScreenDir(dirs[axis]);
            var amount = (tx * ux + ty * uy) / len;
            var world = d.WorldPos.AddScaled(dirs[axis], amount);
            n.Obj!.Position = n.Obj.Parent!.WorldToLocal(world);
        }
        else if (kind == "rotate")
        {
            var angle = (tx - ty) * 0.6 * Js.Deg;
            var q = Quat.FromAxisAngle(dirs[axis], angle) * d.WorldQuat;
            var pq = n.Obj!.Parent!.GetWorldQuaternion();
            var parentQ = new Quat(-pq.X, -pq.Y, -pq.Z, pq.W);   // invert() = conjugate()
            n.Obj.SetQuaternion(parentQ * q);
        }
        else if (kind == "scale")
        {
            var v = axis == "all" ? new Vec3(1, 1, 0).Normalized() : dirs[axis];
            var (ux, uy, _) = ScreenDir(v);
            var f = Math.Max(0.05, 1 + (axis == "all" ? (tx - ty) / 150 : (tx * ux + ty * uy) / 150));
            var s = d.Scale;
            if (axis == "all") s = new Vec3(s.X * f, s.Y * f, s.Z * f);
            else s = s.With(axis, s.Get(axis) * f);
            n.Obj!.Scale = s;
        }
        ReadTransform(n);
        Touch();
    }

    // ---------------------------------------------------------------- フレーム
    /// <summary>1 フレーム(unity.js の animate): カメラ → ポーズ → ギズモの位置 → ワールド行列の更新(描く)→ Scene の文字。</summary>
    public void Animate(double nowMs)
    {
        Camera.Update(View);
        UpdatePose(nowMs);
        PlaceGizmo();
        World.UpdateMatrixWorld();
        GizmoRoot.UpdateMatrixWorld();
        var n = Get(Selected);
        var tool = Tool switch { "hand" => "Hand", "move" => "Move", "rotate" => "Rotate", "scale" => "Scale", _ => Tool };
        var info = n is not null ? $"{n.Name}  ·  {tool} Tool" : $"{tool} Tool";
        if (info != InfoText)
        {
            InfoText = info;
            Touch();
        }
    }
}
