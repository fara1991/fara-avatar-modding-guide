namespace FaraAvatarModdingGuide.Practice;

/// <summary>3D ビューに置くオブジェクト(viewport.js の mesh に当たる)。</summary>
public sealed class SceneObject
{
    public SceneObject(string kind, string name, MeshData mesh)
    {
        Kind = kind;
        Name = name;
        Mesh = mesh;
        Triangles = mesh.Triangles();
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        foreach (var p in Triangles)
        {
            min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        BoundsMin = min;
        BoundsMax = max;
    }

    /// <summary>基本の形から作る(位置・回転・スケールは後から設定)。</summary>
    public static SceneObject Create(string kind, string? name = null) => new(kind, name ?? kind, MeshShapes.Create(kind));

    public string Kind { get; }
    public string Name { get; set; }
    public MeshData Mesh { get; }
    /// <summary>レイ判定用の三角形(ローカル座標。3 個ずつ)。</summary>
    public Vec3[] Triangles { get; }
    /// <summary>ジオメトリのローカルの外接箱(three.js の geometry.boundingBox)。</summary>
    public Vec3 BoundsMin { get; }
    public Vec3 BoundsMax { get; }

    public Vec3 Position { get; set; } = Vec3.Zero;
    public Quat Quaternion { get; set; } = Quat.Identity;
    public Vec3 Scale { get; set; } = Vec3.One;

    /// <summary>オイラー角(ラジアン。XYZ 順)。quaternion から求める(three.js の rotation と同じ)。</summary>
    public Vec3 Rotation => Quaternion.ToEulerXYZ();

    /// <summary>オイラー角(度)で回転を設定する(resetScene の d.r)。</summary>
    public void SetRotationDegrees(double x, double y, double z) =>
        Quaternion = Quat.FromEulerXYZ(x * Js.Deg, y * Js.Deg, z * Js.Deg);

    /// <summary>ローカル座標の点をワールドへ(位置 + 回転 × (スケール × 点))。</summary>
    public Vec3 ToWorld(Vec3 local) => Position + Quaternion.Rotate(local * Scale);

    /// <summary>
    /// three.js の Box3.expandByObject と同じ外接箱: 自分のジオメトリの外接箱を変換した箱(8 隅の外接)に、
    /// 子の「mirror」(同じジオメトリで scale.x = -1。表示していなくても数える)の箱を足す。
    /// </summary>
    public (Vec3 Min, Vec3 Max) WorldBounds()
    {
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        foreach (var mirror in new[] { false, true })
            for (var i = 0; i < 8; i++)
            {
                var c = new Vec3((i & 1) == 0 ? BoundsMin.X : BoundsMax.X, (i & 2) == 0 ? BoundsMin.Y : BoundsMax.Y, (i & 4) == 0 ? BoundsMin.Z : BoundsMax.Z);
                if (mirror) c = c with { X = -c.X };
                var w = ToWorld(c);
                min = new Vec3(Math.Min(min.X, w.X), Math.Min(min.Y, w.Y), Math.Min(min.Z, w.Z));
                max = new Vec3(Math.Max(max.X, w.X), Math.Max(max.Y, w.Y), Math.Max(max.Z, w.Z));
            }
        return (min, max);
    }
}
