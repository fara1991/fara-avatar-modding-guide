namespace FaraAvatarModdingGuide.Practice;

/// <summary>画面上のレイ(原点と単位方向)。</summary>
public readonly record struct Ray(Vec3 Origin, Vec3 Direction)
{
    public Vec3 At(double t) => Origin + Direction * t;
}

/// <summary>
/// 3D ビューの視点(viewport.js の view / updateCamera / numpadView / orbitBy / frame)と、そこから決まるカメラの計算
/// (画面とワールドの座標変換・レイ)。Z が上。theta は Z 軸まわり、phi は Z 軸からの角度。
/// カメラの行列は three.js と同じ手順(lookAt → クォータニオン → compose、射影行列とその逆行列)で求める。透視は縦の視野角 50°、平行投影は高さ dist × 0.84。
/// </summary>
public sealed class OrbitView
{
    public const double FovDegrees = 50;
    public const double OrthoHalfHeightPerDist = 0.42;
    public const double MinDist = 0.5, MaxDist = 200;
    private const string UserName = "ユーザー";

    public Vec3 Target { get; set; } = Vec3.Zero;
    public double Dist { get; set; } = 11;
    public double Theta { get; set; } = -Math.PI / 4 - Math.PI / 2;
    public double Phi { get; set; } = 63 * Js.Deg;
    public string Name { get; set; } = UserName;
    public bool Ortho { get; set; }

    /// <summary>ビューポートの縦横比(幅 / 高さ)。resize() で決まる。</summary>
    public double Aspect { get; set; } = 1;

    /// <summary>ビューポートの高さ(ピクセル。worldPerPixel が使う。0 のときは 1 として扱う)。</summary>
    public double HeightPx { get; set; } = 1;

    /// <summary>視点の表示("前・透視投影" など)。</summary>
    public string Label => $"{Name}・{(Ortho ? "平行投影" : "透視投影")}";

    /// <summary>Blender の既定の視点(右前上から)に戻す。</summary>
    public void Reset(double? dist = null, Vec3? target = null)
    {
        Theta = -Math.PI / 4 - Math.PI / 2;
        Phi = 63 * Js.Deg;
        Name = UserName;
        Dist = dist ?? 11;
        Target = target ?? Vec3.Zero;
        Ortho = false;
    }

    public Vec3 Direction => new(Math.Sin(Phi) * Math.Cos(Theta), Math.Sin(Phi) * Math.Sin(Theta), Math.Cos(Phi));

    public Vec3 Position => Target.AddScaled(Direction, Dist);

    /// <summary>真上・真下から見るときは up が視線と平行になるので、画面の上を +Y にする。</summary>
    public Vec3 Up => Phi < 0.02 || Phi > Math.PI - 0.02 ? Vec3.UnitY : Vec3.UnitZ;

    // ---------------------------------------------------------------- カメラの行列(three.js と同じ手順)
    // three.js は、カメラの向きを lookAt の行列 → クォータニオン → compose し直した行列(matrixWorld)で持ち、そこからビュー行列・射影行列・レイを作る。
    // 平行投影で軸にそろえた視点(テンキー)では、レイが面とぴったり平行になり、判定が丸めの誤差で決まる(隠れているかの判定など)ので、
    // 同じ手順・同じ順で写して誤差まで揃える。

    /// <summary>カメラの matrixWorld・その逆行列・射影行列・その逆行列(いまの視点と縦横比から。透視と平行投影のうち、いま使うほう)。</summary>
    private (Mat4 World, Mat4 WorldInverse, Mat4 Projection, Mat4 ProjectionInverse) CameraMatrices()
    {
        // Matrix4.lookAt(eye, target, up) → Quaternion.setFromRotationMatrix → Matrix4.compose(position, quaternion, scale = 1)
        var up = Up;
        var eye = Position;
        var z = eye - Target;
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
        var world = Mat4.Compose(eye, q, Vec3.One);
        var inverse = world.Inverted();
        Mat4 projection;
        if (Ortho)
        {
            var h = OrthoHalfHeight;
            double left = -h * Aspect, right = h * Aspect, top = h, bottom = -h;
            double dx = (right - left) / 2, dy = (top - bottom) / 2, cx = (right + left) / 2, cy = (top + bottom) / 2;
            projection = Mat4.MakeOrthographic(cx - dx, cx + dx, cy + dy, cy - dy, -500, 500);
        }
        else
        {
            const double near = 0.1, far = 500;
            var top = near * Math.Tan(Js.Deg * 0.5 * FovDegrees);
            var height = 2 * top;
            var width = Aspect * height;
            var left = -0.5 * width;
            projection = Mat4.MakePerspective(left, left + width, top, top - height, near, far);
        }
        return (world, inverse, projection, projection.Inverted());
    }

    /// <summary>カメラの軸(ワールド座標。画面の右・上・後ろ)= matrixWorld の列 0・1・2(setFromMatrixColumn)。</summary>
    public (Vec3 Right, Vec3 Up, Vec3 Back) Basis
    {
        get
        {
            var e = CameraMatrices().World.E;
            return (new Vec3(e[0], e[1], e[2]), new Vec3(e[4], e[5], e[6]), new Vec3(e[8], e[9], e[10]));
        }
    }

    /// <summary>カメラが向いている方向(three.js の getWorldDirection: matrixWorld の列 2 の逆向き)。</summary>
    public Vec3 Forward
    {
        get
        {
            var e = CameraMatrices().World.E;
            return new Vec3(-e[8], -e[9], -e[10]).Normalized();
        }
    }

    /// <summary>平行投影の高さの半分。</summary>
    public double OrthoHalfHeight => Dist * OrthoHalfHeightPerDist;

    // ---------------------------------------------------------------- 視点の操作
    /// <summary>視点を回す(ラジアン)。回すと透視投影に戻る(Blender の自動透視)。</summary>
    public void OrbitBy(double dTheta, double dPhi = 0)
    {
        Theta += dTheta;
        Phi = Math.Clamp(Phi + dPhi, 0.01, Math.PI - 0.01);
        Name = UserName;
        Ortho = false;
    }

    /// <summary>ドラッグでの平行移動(画面の右・上へ。距離に比例)。</summary>
    public void Pan(double dxPx, double dyPx)
    {
        var (right, up, _) = Basis;
        var k = Dist * 0.0016;
        Target = Target.AddScaled(right, -dxPx * k).AddScaled(up, dyPx * k);
    }

    /// <summary>Ctrl+ドラッグでのズーム。</summary>
    public void ZoomDrag(double dyPx) => Dist = Math.Clamp(Dist * (1 + dyPx * 0.01), MinDist, MaxDist);

    /// <summary>ホイールでのズーム(下へ回すと遠ざかる)。</summary>
    public void ZoomWheel(double deltaY) => Dist = Math.Clamp(Dist * (deltaY > 0 ? 1.12 : 1 / 1.12), MinDist, MaxDist);

    /// <summary>テンキーの視点切り替え("1" / "3" / "7" / "5")。切り替えたら true。"Decimal"(フレーミング)は <see cref="Frame(IReadOnlyList{SceneObject}, SceneObject?)"/>。</summary>
    public bool NumpadAxis(string key, bool ctrl)
    {
        switch (key)
        {
            case "1": SetAxis(ctrl ? "後" : "前", ctrl ? Math.PI / 2 : -Math.PI / 2, Math.PI / 2); return true;
            case "3": SetAxis(ctrl ? "左" : "右", ctrl ? Math.PI : 0, Math.PI / 2); return true;
            case "7": SetAxis(ctrl ? "下" : "上", -Math.PI / 2, ctrl ? Math.PI - 0.001 : 0.001); return true;
            case "5": Ortho = !Ortho; return true;
            default: return false;
        }
    }

    private void SetAxis(string name, double theta, double phi)
    {
        Theta = theta;
        Phi = phi;
        Name = name;
        Ortho = true;
    }

    /// <summary>
    /// 注視点と距離を、オブジェクト(外接箱)に合わせる。空なら何もしない。editObj は編集中のオブジェクト
    /// (編集モードの点・線の箱も数える。<see cref="SceneObject.WorldBounds"/>)。
    /// </summary>
    public void Frame(IReadOnlyList<SceneObject> items, SceneObject? editObj = null)
    {
        if (items.Count == 0) return;
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        foreach (var o in items)
        {
            var (lo, hi) = o.WorldBounds(o == editObj);
            min = new Vec3(Math.Min(min.X, lo.X), Math.Min(min.Y, lo.Y), Math.Min(min.Z, lo.Z));
            max = new Vec3(Math.Max(max.X, hi.X), Math.Max(max.Y, hi.Y), Math.Max(max.Z, hi.Z));
        }
        FrameBox(min, max);
    }

    /// <summary>編集モードで選んだ頂点(ワールド座標)に合わせる。空なら何もしない。</summary>
    public void Frame(IReadOnlyList<Vec3> points)
    {
        if (points.Count == 0) return;
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        foreach (var p in points)
        {
            min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        FrameBox(min, max);
    }

    /// <summary>箱の中心と対角線の長さから、注視点と距離を決める(空の箱は中心 0・大きさ 0)。</summary>
    private void FrameBox(Vec3 min, Vec3 max)
    {
        var empty = max.X < min.X || max.Y < min.Y || max.Z < min.Z;
        Target = empty ? Vec3.Zero : (min + max) * 0.5;
        Dist = Math.Max(3, (empty ? 0 : (max - min).Length) * 1.6);
    }

    // ---------------------------------------------------------------- 画面とワールド
    /// <summary>画面の点(NDC。-1〜1、上が +1)からのレイ(three.js の Raycaster.setFromCamera)。</summary>
    public Ray RayFromNdc(double x, double y)
    {
        var (world, _, _, projectionInverse) = CameraMatrices();
        if (Ortho)
        {
            // origin.set(x, y, (near + far) / (near - far)).unproject(camera); direction.set(0, 0, -1).transformDirection(camera.matrixWorld)
            var origin = world.ApplyToPoint(projectionInverse.ApplyToPoint(new Vec3(x, y, (-500.0 + 500.0) / (-500.0 - 500.0))));
            return new Ray(origin, world.TransformDirection(new Vec3(0, 0, -1)));
        }
        var cameraPosition = new Vec3(world.E[12], world.E[13], world.E[14]);
        var direction = (world.ApplyToPoint(projectionInverse.ApplyToPoint(new Vec3(x, y, 0.5))) - cameraPosition).Normalized();
        return new Ray(cameraPosition, direction);
    }

    /// <summary>ワールドの点を画面(NDC)へ(three.js の Vector3.project: matrixWorldInverse → projectionMatrix)。</summary>
    public (double X, double Y) ProjectToNdc(Vec3 worldPoint)
    {
        var (_, inverse, projection, _) = CameraMatrices();
        var p = projection.ApplyToPoint(inverse.ApplyToPoint(worldPoint));
        return (p.X, p.Y);
    }

    /// <summary>ワールドの点を、ビューポート内のピクセル(左上が原点)へ。</summary>
    public (double X, double Y) ToScreen(Vec3 world, double widthPx, double heightPx)
    {
        var (x, y) = ProjectToNdc(world);
        return ((x + 1) / 2 * widthPx, (1 - y) / 2 * heightPx);
    }

    /// <summary>p の奥行きで、画面の 1 ピクセルがワールドで何 m か。</summary>
    public double WorldPerPixel(Vec3 p)
    {
        var h = HeightPx == 0 ? 1 : HeightPx;
        if (Ortho) return OrthoHalfHeight * 2 / h;
        var d = Position.DistanceTo(p);
        return 2 * d * Math.Tan(FovDegrees * Js.Deg / 2) / h;
    }

    /// <summary>画面の点を、pivot を通るビュー平面上のワールド座標にする。平面と交わらない(平行・後ろ)ときは pivot。</summary>
    public Vec3 UnprojectOnPlane(double ndcX, double ndcY, Vec3 pivot)
    {
        var normal = Forward;
        var ray = RayFromNdc(ndcX, ndcY);
        var constant = -pivot.Dot(normal);
        var denominator = normal.Dot(ray.Direction);
        double t;
        if (denominator == 0)
        {
            var distanceToPoint = normal.Dot(ray.Origin) + constant;
            if (distanceToPoint != 0) return pivot;
            t = 0;
        }
        else
        {
            t = -(ray.Origin.Dot(normal) + constant) / denominator;
            if (t < 0) return pivot;
        }
        return ray.At(t);
    }
}
