using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>色。three.js は 16 進の色を線形に直して計算し、出力で sRGB に戻す。1 色のものは行って戻って同じなので、sRGB のまま渡す(陰影のある面だけ、シェーダーが線形で計算する)。</summary>
public static class Rgb
{
    /// <summary>0xRRGGBB → sRGB の 0〜1。</summary>
    public static (float R, float G, float B) FromHex(int hex) => (((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f);

    /// <summary>sRGB(0〜1)→ 線形(three.js の ColorManagement.SRGBToLinear)。テストが three.js の頂点の色(線形で持つ)と比べるのに使う。</summary>
    public static double ToLinear(double c) => c < 0.04045 ? c * 0.0773993808049536 : Math.Pow(c * 0.9478672985781991 + 0.0521327014218009, 2.4);
}

/// <summary>three.js の EdgesGeometry(geometry, thresholdAngle)。面の法線の角度が閾値より大きい辺と、1 つの三角形にしか属さない辺だけを線にする。</summary>
public static class EdgesGeometry
{
    private readonly record struct Hash(long X, long Y, long Z);

    private sealed record EdgeData(int Index0, int Index1, Vec3 Normal);

    /// <summary>pos は三角形(3 頂点ずつ)の位置(Float32 の値)。戻りは線分(2 点ずつ)の位置(x, y, z の並び)。</summary>
    public static float[] Build(IReadOnlyList<Vec3> pos, double thresholdAngleDegrees)
    {
        const double precision = 10000; // 4 桁
        var thresholdDot = Math.Cos(Math.PI / 180 * thresholdAngleDegrees);
        var edgeData = new OrderedDictionary<(Hash, Hash), EdgeData?>();
        var vertices = new List<float>();
        var hashes = new Hash[3];
        var v = new Vec3[3];
        for (var i = 0; i + 2 < pos.Count; i += 3)
        {
            v[0] = pos[i]; v[1] = pos[i + 1]; v[2] = pos[i + 2];
            // Triangle.getNormal: (c - b) × (a - b) を正規化(長さ 0 なら 0)
            var n = (v[2] - v[1]).Cross(v[0] - v[1]);
            var lengthSq = n.LengthSq;
            n = lengthSq > 0 ? n * (1 / Math.Sqrt(lengthSq)) : Vec3.Zero;
            for (var k = 0; k < 3; k++) hashes[k] = new Hash((long)Js.Round(v[k].X * precision), (long)Js.Round(v[k].Y * precision), (long)Js.Round(v[k].Z * precision));
            // 縮退した三角形は飛ばす
            if (hashes[0] == hashes[1] || hashes[1] == hashes[2] || hashes[2] == hashes[0]) continue;
            for (var j = 0; j < 3; j++)
            {
                var jNext = (j + 1) % 3;
                var key = (hashes[j], hashes[jNext]);
                var reverse = (hashes[jNext], hashes[j]);
                if (edgeData.TryGetValue(reverse, out var other) && other is not null)
                {
                    // 向かい合う辺が見つかった: 角度が閾値を超えていれば線にして、辺の記録を消す(null にする。キーは残る)
                    if (n.Dot(other.Normal) <= thresholdDot) Add(vertices, v[j], v[jNext]);
                    edgeData[reverse] = null;
                }
                else if (!edgeData.ContainsKey(key))
                {
                    edgeData[key] = new EdgeData(i + j, i + jNext, n);
                }
            }
        }
        // 向かい合う辺の無かった辺(開いた縁)
        foreach (var data in edgeData.Values)
            if (data is not null) Add(vertices, pos[data.Index0], pos[data.Index1]);
        return [.. vertices];
    }

    private static void Add(List<float> list, Vec3 a, Vec3 b)
    {
        list.Add((float)a.X); list.Add((float)a.Y); list.Add((float)a.Z);
        list.Add((float)b.X); list.Add((float)b.Y); list.Add((float)b.Z);
    }
}

/// <summary>方眼・軸線・3D カーソルなど、シーンに最初から置くものの頂点(buildScene と同じ)。</summary>
public static class StaticGeometry
{
    /// <summary>GridHelper(size, divisions, color1, color2) の位置と色(位置+色の並び。色は sRGB)。XZ 平面で作られる(使う側が X 軸まわりに 90° 回して XY 平面にする)。</summary>
    public static float[] Grid(int size, int divisions, int color1, int color2)
    {
        var c1 = Rgb.FromHex(color1);
        var c2 = Rgb.FromHex(color2);
        var center = divisions / 2.0;
        var step = size / (double)divisions;
        var half = size / 2.0;
        var data = new List<float>();
        void Vertex(double x, double y, double z, (float R, float G, float B) c) { data.Add((float)x); data.Add((float)y); data.Add((float)z); data.Add(c.R); data.Add(c.G); data.Add(c.B); }
        var k = -half;
        for (var i = 0; i <= divisions; i++, k += step)
        {
            var color = i == center ? c1 : c2;
            Vertex(-half, 0, k, color); Vertex(half, 0, k, color);
            Vertex(k, 0, -half, color); Vertex(k, 0, half, color);
        }
        return [.. data];
    }

    /// <summary>longLine(half, dir): 原点を通る、方向 dir の点の列(-half〜half を 1 刻み。LINE_STRIP)。1 本の長い線分がカメラの後ろにまたがると描かれない環境があるので、短い区間に分けてある。</summary>
    public static float[] LongLine(int half, Vec3 dir)
    {
        var data = new float[(half * 2 + 1) * 3];
        for (var i = 0; i < half * 2 + 1; i++)
        {
            var p = dir * (i - half);
            data[i * 3] = (float)p.X; data[i * 3 + 1] = (float)p.Y; data[i * 3 + 2] = (float)p.Z;
        }
        return data;
    }

    /// <summary>RingGeometry(inner, outer, thetaSegments) を三角形に並べたもの(位置だけ。XY 平面)。</summary>
    public static float[] Ring(double innerRadius, double outerRadius, int thetaSegments)
    {
        var verts = new List<Vec3>();
        var radius = innerRadius;
        var radiusStep = outerRadius - innerRadius; // phiSegments = 1
        for (var j = 0; j <= 1; j++)
        {
            for (var i = 0; i <= thetaSegments; i++)
            {
                var segment = i / (double)thetaSegments * Math.PI * 2;
                verts.Add(new Vec3(radius * Math.Cos(segment), radius * Math.Sin(segment), 0));
            }
            radius += radiusStep;
        }
        var data = new List<float>();
        void Tri(int a, int b, int c) { foreach (var i in new[] { a, b, c }) { data.Add((float)verts[i].X); data.Add((float)verts[i].Y); data.Add((float)verts[i].Z); } }
        for (var i = 0; i < thetaSegments; i++)
        {
            int a = i, b = i + thetaSegments + 1, c = i + thetaSegments + 2, d = i + 1;
            Tri(a, b, d);
            Tri(b, c, d);
        }
        return [.. data];
    }

    /// <summary>Quaternion.setFromUnitVectors(from, to)(軸固定の線を、X 方向に作ったものから向けるため)。</summary>
    public static Quat FromUnitVectors(Vec3 from, Vec3 to)
    {
        var r = from.Dot(to) + 1;
        double x, y, z, w;
        if (r < 2.220446049250313e-16) // Number.EPSILON
        {
            w = 0;
            if (Math.Abs(from.X) > Math.Abs(from.Z)) { x = -from.Y; y = from.X; z = 0; }
            else { x = 0; y = -from.Z; z = from.Y; }
        }
        else
        {
            x = from.Y * to.Z - from.Z * to.Y;
            y = from.Z * to.X - from.X * to.Z;
            z = from.X * to.Y - from.Y * to.X;
            w = r;
        }
        var l = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (l == 0) return Quat.Identity;
        l = 1 / l;
        return new Quat(x * l, y * l, z * l, w * l);
    }
}
