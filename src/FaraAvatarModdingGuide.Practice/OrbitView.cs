namespace FaraAvatarModdingGuide.Practice;

/// <summary>画面上のレイ(原点と単位方向)。</summary>
public readonly record struct Ray(Vec3 Origin, Vec3 Direction)
{
    public Vec3 At(double t) => Origin + Direction * t;
}

/// <summary>
/// 3D ビューの視点(viewport.js の view / updateCamera / numpadView / orbitBy / frame)と、そこから決まるカメラの計算
/// (画面とワールドの座標変換・レイ)。Z が上。theta は Z 軸まわり、phi は Z 軸からの角度。
/// カメラの向きは three.js の lookAt と同じ手順で求める。透視は縦の視野角 50°、平行投影は高さ dist × 0.84。
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

    public Vec3 Position => Target + Direction * Dist;

    /// <summary>真上・真下から見るときは up が視線と平行になるので、画面の上を +Y にする。</summary>
    public Vec3 Up => Phi < 0.02 || Phi > Math.PI - 0.02 ? Vec3.UnitY : Vec3.UnitZ;

    /// <summary>カメラの軸(ワールド座標。画面の右・上・後ろ)。three.js の Matrix4.lookAt。</summary>
    public (Vec3 Right, Vec3 Up, Vec3 Back) Basis
    {
        get
        {
            var up = Up;
            var z = Position - Target;
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
            return (x, z.Cross(x), z);
        }
    }

    /// <summary>カメラが向いている方向(three.js の getWorldDirection)。</summary>
    public Vec3 Forward => -Basis.Back;

    /// <summary>平行投影の高さの半分。</summary>
    public double OrthoHalfHeight => Dist * OrthoHalfHeightPerDist;

    private static double TanHalfFov => Math.Tan(FovDegrees * Js.Deg / 2);

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
        Target = Target + right * (-dxPx * k) + up * (dyPx * k);
    }

    /// <summary>Ctrl+ドラッグでのズーム。</summary>
    public void ZoomDrag(double dyPx) => Dist = Math.Clamp(Dist * (1 + dyPx * 0.01), MinDist, MaxDist);

    /// <summary>ホイールでのズーム(下へ回すと遠ざかる)。</summary>
    public void ZoomWheel(double deltaY) => Dist = Math.Clamp(Dist * (deltaY > 0 ? 1.12 : 1 / 1.12), MinDist, MaxDist);

    /// <summary>テンキーの視点切り替え("1" / "3" / "7" / "5")。切り替えたら true。"Decimal"(フレーミング)は <see cref="Frame"/>。</summary>
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

    /// <summary>注視点と距離を、オブジェクト(外接箱)に合わせる。空なら何もしない。</summary>
    public void Frame(IReadOnlyList<SceneObject> items)
    {
        if (items.Count == 0) return;
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        foreach (var o in items)
        {
            var (lo, hi) = o.WorldBounds();
            min = new Vec3(Math.Min(min.X, lo.X), Math.Min(min.Y, lo.Y), Math.Min(min.Z, lo.Z));
            max = new Vec3(Math.Max(max.X, hi.X), Math.Max(max.Y, hi.Y), Math.Max(max.Z, hi.Z));
        }
        Target = (min + max) * 0.5;
        Dist = Math.Max(3, (max - min).Length * 1.6);
    }

    // ---------------------------------------------------------------- 画面とワールド
    /// <summary>画面の点(NDC。-1〜1、上が +1)からのレイ(three.js の Raycaster.setFromCamera)。</summary>
    public Ray RayFromNdc(double x, double y)
    {
        var (right, up, _) = Basis;
        var forward = Forward;
        if (Ortho)
        {
            var h = OrthoHalfHeight;
            return new Ray(Position + right * (x * h * Aspect) + up * (y * h), forward);
        }
        var t = TanHalfFov;
        return new Ray(Position, (right * (x * t * Aspect) + up * (y * t) + forward).Normalized());
    }

    /// <summary>ワールドの点を画面(NDC)へ(three.js の Vector3.project)。</summary>
    public (double X, double Y) ProjectToNdc(Vec3 world)
    {
        var (right, up, back) = Basis;
        var rel = world - Position;
        double cx = rel.Dot(right), cy = rel.Dot(up), cz = rel.Dot(back);
        if (Ortho)
        {
            var h = OrthoHalfHeight;
            return (cx / (h * Aspect), cy / h);
        }
        var t = TanHalfFov;
        var w = -cz;
        var d = 1 / (w == 0 ? 1 : w);
        return (cx / (t * Aspect) * d, cy / t * d);
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
