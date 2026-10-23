namespace FaraAvatarModdingGuide.Practice;

/// <summary>メッシュの中身(頂点と面)。面は表から見て反時計回り。編集モードの操作(押し出しなど)は、この上に後の段階で足す。</summary>
public sealed record MeshData(IReadOnlyList<Vec3> Verts, IReadOnlyList<int[]> Faces)
{
    /// <summary>
    /// 表示・レイ判定用の三角形の頂点(3 個ずつ。面ごとに最初の頂点を軸にした扇形。editmesh.js の triangles() と同じ順)。
    /// three.js はこの座標を Float32 の配列で持つので、レイ判定も同じ丸めで行う(float に丸めて double に戻す)。
    /// </summary>
    public Vec3[] Triangles()
    {
        var list = new List<Vec3>();
        foreach (var face in Faces)
            for (var i = 1; i < face.Length - 1; i++)
                foreach (var index in new[] { face[0], face[i], face[i + 1] })
                    list.Add(Round32(Verts[index]));
        return [.. list];
    }

    private static Vec3 Round32(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}

/// <summary>追加できる基本の形(editmesh.js の SHAPES と同じ頂点・面の並び)。</summary>
public static class MeshShapes
{
    public static readonly string[] Kinds = ["plane", "cube", "sphere", "cylinder", "cone", "torus"];

    public static MeshData Create(string kind) => kind switch
    {
        "plane" => new MeshData([new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)], [[0, 1, 2, 3]]),
        "cube" => new MeshData(
            [new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1), new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)],
            [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [2, 3, 7, 6], [3, 0, 4, 7], [1, 2, 6, 5]]),
        "cylinder" => Cylinder(32),
        "cone" => Cone(32),
        "sphere" => Sphere(32, 16),
        "torus" => Torus(48, 12, 1, 0.25),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知の形"),
    };

    private static Vec3[] Ring(int n, double r, double z)
    {
        var ring = new Vec3[n];
        for (var i = 0; i < n; i++)
        {
            var a = (double)i / n * Math.PI * 2;
            ring[i] = new Vec3(Math.Cos(a) * r, Math.Sin(a) * r, z);
        }
        return ring;
    }

    private static MeshData Cylinder(int n)
    {
        var v = new List<Vec3>(); v.AddRange(Ring(n, 1, -1)); v.AddRange(Ring(n, 1, 1));
        var f = new List<int[]>();
        for (var i = 0; i < n; i++) f.Add([i, (i + 1) % n, n + (i + 1) % n, n + i]);
        f.Add([.. Enumerable.Range(0, n).Select(i => n + i)]);
        f.Add([.. Enumerable.Range(0, n).Select(i => n - 1 - i)]);
        return new MeshData(v, f);
    }

    private static MeshData Cone(int n)
    {
        var v = new List<Vec3>(); v.AddRange(Ring(n, 1, -1)); v.Add(new Vec3(0, 0, 1));
        var f = new List<int[]>();
        for (var i = 0; i < n; i++) f.Add([i, (i + 1) % n, n]);
        f.Add([.. Enumerable.Range(0, n).Select(i => n - 1 - i)]);
        return new MeshData(v, f);
    }

    private static MeshData Sphere(int seg, int rings)
    {
        var v = new List<Vec3> { new(0, 0, 1) };
        for (var k = 1; k < rings; k++)
        {
            var th = (double)k / rings * Math.PI;
            v.AddRange(Ring(seg, Math.Sin(th), Math.Cos(th)));
        }
        v.Add(new Vec3(0, 0, -1));
        int R(int k, int i) => 1 + (k - 1) * seg + i % seg;
        var bottom = v.Count - 1;
        var f = new List<int[]>();
        for (var i = 0; i < seg; i++) f.Add([R(1, i), R(1, i + 1), 0]);
        for (var k = 1; k < rings - 1; k++)
            for (var i = 0; i < seg; i++) f.Add([R(k + 1, i), R(k + 1, i + 1), R(k, i + 1), R(k, i)]);
        for (var i = 0; i < seg; i++) f.Add([bottom, R(rings - 1, i + 1), R(rings - 1, i)]);
        return new MeshData(v, f);
    }

    private static MeshData Torus(int n, int m, double bigR, double r)
    {
        var v = new List<Vec3>();
        for (var i = 0; i < n; i++)
        {
            var a = (double)i / n * Math.PI * 2;
            for (var j = 0; j < m; j++)
            {
                var b = (double)j / m * Math.PI * 2;
                v.Add(new Vec3((bigR + r * Math.Cos(b)) * Math.Cos(a), (bigR + r * Math.Cos(b)) * Math.Sin(a), r * Math.Sin(b)));
            }
        }
        int I(int i, int j) => i % n * m + j % m;
        var f = new List<int[]>();
        for (var i = 0; i < n; i++)
            for (var j = 0; j < m; j++) f.Add([I(i, j), I(i + 1, j), I(i + 1, j + 1), I(i, j + 1)]);
        return new MeshData(v, f);
    }
}
