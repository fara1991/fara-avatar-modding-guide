namespace FaraAvatarModdingGuide.Practice;

/// <summary>レイ判定(three.js の Raycaster.intersectObjects を、両面・三角形ごとの判定で写したもの)。</summary>
public static class Picking
{
    /// <summary>three.js の Ray.intersectTriangle(両面)。当たったら、レイに沿った距離。</summary>
    public static double? RayTriangle(Ray ray, Vec3 a, Vec3 b, Vec3 c)
    {
        var edge1 = b - a;
        var edge2 = c - a;
        var normal = edge1.Cross(edge2);
        var ddn = ray.Direction.Dot(normal);
        double sign;
        if (ddn > 0) sign = 1;
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

    /// <summary>オブジェクトの三角形(ワールド座標へ変換して)のうち、いちばん近い当たりの距離。</summary>
    public static double? Nearest(Ray ray, SceneObject o)
    {
        double? best = null;
        var tri = o.Triangles;
        for (var i = 0; i + 2 < tri.Length; i += 3)
        {
            var t = RayTriangle(ray, o.ToWorld(tri[i]), o.ToWorld(tri[i + 1]), o.ToWorld(tri[i + 2]));
            if (t is { } d && (best is null || d < best)) best = d;
        }
        return best;
    }

    /// <summary>レイに最初に当たるオブジェクト。無ければ null。同じ距離なら先に置いたほう(three.js の安定な並べ替えと同じ)。</summary>
    public static SceneObject? Pick(Ray ray, IEnumerable<SceneObject> objects)
    {
        SceneObject? hit = null;
        var best = double.PositiveInfinity;
        foreach (var o in objects)
            if (Nearest(ray, o) is { } d && d < best) { best = d; hit = o; }
        return hit;
    }
}
