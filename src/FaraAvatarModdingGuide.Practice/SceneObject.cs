namespace FaraAvatarModdingGuide.Practice;

/// <summary>3D ビューに置くオブジェクト(viewport.js の mesh に当たる)。メッシュの中身は <see cref="Em"/>(編集モードのメッシュ)。</summary>
public sealed class SceneObject
{
    public SceneObject(string kind, string name, EditMesh em)
    {
        Kind = kind;
        Name = name;
        Em = em;
        Rebuild();
    }

    /// <summary>基本の形から作る(位置・回転・スケールは後から設定)。</summary>
    public static SceneObject Create(string kind, string? name = null) => new(kind, name ?? kind, EditMesh.FromData(MeshShapes.Create(kind)));

    /// <summary>メッシュの中身を指定して作る。</summary>
    public static SceneObject Create(string kind, string? name, MeshData mesh) => new(kind, name ?? kind, EditMesh.FromData(mesh));

    public string Kind { get; }
    public string Name { get; set; }
    /// <summary>共同練習の部屋で、同じオブジェクトを指す ID(userData.rid)。</summary>
    public string Rid { get; set; } = "";
    /// <summary>ミラー(X 方向)を付けているか(userData.mirror)。</summary>
    public bool Mirror { get; set; }
    public EditMesh Em { get; set; }

    // ---------------------------------------------------------------- 形(rebuild() で作り直す表示・レイ判定用のジオメトリ)
    /// <summary>レイ判定用の三角形(ローカル座標。3 個ずつ。three.js の Float32 の配列と同じ丸め)。</summary>
    public Vec3[] Triangles { get; private set; } = [];
    /// <summary>三角形(3 頂点ずつ)→ 面。</summary>
    public EmFace[] TriFace { get; private set; } = [];
    /// <summary>ジオメトリのローカルの外接箱(three.js の geometry.boundingBox。三角形が無ければ空 = 最小が +∞)。</summary>
    public Vec3 BoundsMin { get; private set; }
    public Vec3 BoundsMax { get; private set; }
    /// <summary>ジオメトリの外接球(three.js の geometry.boundingSphere: 箱の中心と、そこから最も遠い頂点までの距離)。レイ判定の最初のふるいに使う。</summary>
    public Vec3 SphereCenter { get; private set; }
    public double SphereRadius { get; private set; }

    /// <summary>
    /// viewport.js の rebuild(): メッシュの中身から、表示・レイ判定用のジオメトリを作り直す。現行は編集のたびに呼ぶ所が決まっていて、
    /// それ以外のときは前回の形のままレイ判定をする(C# も同じ呼び方にしてある)。
    /// </summary>
    public void Rebuild()
    {
        var (pos, triFace) = Em.Triangles();
        Triangles = [.. pos.Select(Round32)];
        TriFace = [.. triFace];
        BoundsMin = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        BoundsMax = -BoundsMin;
        foreach (var p in Triangles)
        {
            BoundsMin = new Vec3(Math.Min(BoundsMin.X, p.X), Math.Min(BoundsMin.Y, p.Y), Math.Min(BoundsMin.Z, p.Z));
            BoundsMax = new Vec3(Math.Max(BoundsMax.X, p.X), Math.Max(BoundsMax.Y, p.Y), Math.Max(BoundsMax.Z, p.Z));
        }
        SphereCenter = Triangles.Length == 0 ? Vec3.Zero : (BoundsMin + BoundsMax) * 0.5;
        var maxRadiusSq = 0.0;
        foreach (var p in Triangles)
        {
            double dx = SphereCenter.X - p.X, dy = SphereCenter.Y - p.Y, dz = SphereCenter.Z - p.Z;
            maxRadiusSq = Math.Max(maxRadiusSq, dx * dx + dy * dy + dz * dz);
        }
        SphereRadius = Math.Sqrt(maxRadiusSq);
    }

    private static Vec3 Round32(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    // ---------------------------------------------------------------- 位置・回転・スケール
    private Quat quaternion = Quat.Identity;
    private Vec3 euler = Vec3.Zero;

    public Vec3 Position { get; set; } = Vec3.Zero;
    public Vec3 Scale { get; set; } = Vec3.One;

    /// <summary>回転(quaternion)。three.js は quaternion を変えるたびに rotation(オイラー角)をここから求め直す。</summary>
    public Quat Quaternion
    {
        get => quaternion;
        set { quaternion = value; euler = value.ToEulerXYZ(); }
    }

    /// <summary>
    /// オイラー角(ラジアン。XYZ 順)。<see cref="SetRotation"/> で設定した値はそのまま保たれ(|y| が 90° を超える値も)、
    /// quaternion を変えたときだけ quaternion から求め直される(three.js の rotation と同じ)。
    /// </summary>
    public Vec3 Rotation => euler;

    /// <summary>rotation.set(x, y, z)(ラジアン): オイラー角を保ったまま、quaternion を求める。</summary>
    public void SetRotation(double x, double y, double z)
    {
        euler = new Vec3(x, y, z);
        quaternion = Quat.FromEulerXYZ(x, y, z);
    }

    /// <summary>オイラー角(度)で回転を設定する(resetScene の d.r)。</summary>
    public void SetRotationDegrees(double x, double y, double z) => SetRotation(x * Js.Deg, y * Js.Deg, z * Js.Deg);

    /// <summary>matrixWorld(親は単位行列)。</summary>
    public Mat4 MatrixWorld => Mat4.Compose(Position, Quaternion, Scale);

    /// <summary>ローカル座標の点をワールドへ(matrixWorld を掛ける。three.js の applyMatrix4)。</summary>
    public Vec3 ToWorld(Vec3 local) => MatrixWorld.ApplyToPoint(local);

    /// <summary>
    /// three.js の Box3.expandByObject と同じ外接箱: 自分のジオメトリの外接箱を変換した箱(8 隅の外接)に、
    /// 子の「mirror」(同じジオメトリで scale.x = -1。表示していなくても数える)の箱を足す。
    /// 編集中は、編集モードの点・線(overlay。全頂点を含む。面に属さない頂点も)の箱も足す(<paramref name="editing"/>)。
    /// 輪郭の線は面の頂点の部分集合なので、箱は変わらない。
    /// </summary>
    public (Vec3 Min, Vec3 Max) WorldBounds(bool editing = false)
    {
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        var m = MatrixWorld;
        void Expand(Vec3 lo, Vec3 hi, bool mirror)
        {
            if (lo.X > hi.X || lo.Y > hi.Y || lo.Z > hi.Z) return; // 空の箱
            for (var i = 0; i < 8; i++)
            {
                var c = new Vec3((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z);
                // 子の mirror は scale.x = -1 を matrix に持つ(matrixWorld = 親 × 自分)
                var w = mirror ? m.ApplyToPoint(new Vec3(-c.X, c.Y, c.Z)) : m.ApplyToPoint(c);
                min = new Vec3(Math.Min(min.X, w.X), Math.Min(min.Y, w.Y), Math.Min(min.Z, w.Z));
                max = new Vec3(Math.Max(max.X, w.X), Math.Max(max.Y, w.Y), Math.Max(max.Z, w.Z));
            }
        }
        Expand(BoundsMin, BoundsMax, false);
        Expand(BoundsMin, BoundsMax, true);
        if (editing && Em.Verts.Count > 0)
        {
            var lo = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
            var hi = -lo;
            foreach (var v in Em.Verts)
            {
                var p = Round32(v.Co);
                lo = new Vec3(Math.Min(lo.X, p.X), Math.Min(lo.Y, p.Y), Math.Min(lo.Z, p.Z));
                hi = new Vec3(Math.Max(hi.X, p.X), Math.Max(hi.Y, p.Y), Math.Max(hi.Z, p.Z));
            }
            Expand(lo, hi, false);
        }
        return (min, max);
    }
}
