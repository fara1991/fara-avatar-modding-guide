namespace FaraAvatarModdingGuide.Practice;

/// <summary>レイが当たった 1 か所(three.js の Raycaster の intersection)。FaceIndex は三角形の番号(<see cref="SceneObject.TriFace"/> の添字)。</summary>
public sealed record RayHit(SceneObject Object, int FaceIndex, double Distance, Vec3 Point);

/// <summary>
/// レイ判定(three.js の Raycaster.intersectObjects(objects, false) を、両面・三角形ごとの判定で写したもの)。
/// three.js はレイをオブジェクトのローカル空間へ移して三角形と判定し、当たった点をワールドへ戻して距離を測る(その丸めまで同じにしてある)。
/// 同じ距離のものは、先に置いたオブジェクト・先の三角形が先(three.js の安定な並べ替えと同じ)。
/// </summary>
public static class Picking
{
    /// <summary>three.js の Ray.intersectTriangle(両面)。当たったら、レイに沿った距離 t。</summary>
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

    /// <summary>three.js の Ray.intersectSphere(当たるかどうかだけ)。</summary>
    private static bool HitsSphere(Ray ray, Vec3 center, double radius)
    {
        var v = center - ray.Origin;
        var tca = v.Dot(ray.Direction);
        var d2 = v.Dot(v) - tca * tca;
        var radius2 = radius * radius;
        if (d2 > radius2) return false;
        var thc = Math.Sqrt(radius2 - d2);
        var t1 = tca + thc;
        return !(t1 < 0);
    }

    /// <summary>three.js の Ray.intersectsBox(スラブ法。NaN の扱いまで同じ)。</summary>
    private static bool HitsBox(Ray ray, Vec3 min, Vec3 max)
    {
        double tmin, tmax, tymin, tymax, tzmin, tzmax;
        double invdirx = 1 / ray.Direction.X, invdiry = 1 / ray.Direction.Y, invdirz = 1 / ray.Direction.Z;
        var origin = ray.Origin;
        if (invdirx >= 0) { tmin = (min.X - origin.X) * invdirx; tmax = (max.X - origin.X) * invdirx; }
        else { tmin = (max.X - origin.X) * invdirx; tmax = (min.X - origin.X) * invdirx; }
        if (invdiry >= 0) { tymin = (min.Y - origin.Y) * invdiry; tymax = (max.Y - origin.Y) * invdiry; }
        else { tymin = (max.Y - origin.Y) * invdiry; tymax = (min.Y - origin.Y) * invdiry; }
        if (tmin > tymax || tymin > tmax) return false;
        if (tymin > tmin || double.IsNaN(tmin)) tmin = tymin;
        if (tymax < tmax || double.IsNaN(tmax)) tmax = tymax;
        if (invdirz >= 0) { tzmin = (min.Z - origin.Z) * invdirz; tzmax = (max.Z - origin.Z) * invdirz; }
        else { tzmin = (max.Z - origin.Z) * invdirz; tzmax = (min.Z - origin.Z) * invdirz; }
        if (tmin > tzmax || tzmin > tmax) return false;
        if (tzmin > tmin || double.IsNaN(tmin)) tmin = tzmin;
        if (tzmax < tmax || double.IsNaN(tmax)) tmax = tzmax;
        return !(tmax < 0);
    }

    /// <summary>
    /// オブジェクトの三角形(いまのジオメトリ)に当たるもの全部。three.js の Mesh.raycast と同じ順に判定する:
    /// ワールド空間の外接球 → レイをローカルへ → ローカル空間の外接箱 → 三角形。外接球・外接箱の判定にも誤差の許容が無い
    /// (軸にそろえた平行投影で、頂点や辺のすぐ上を通るレイが当たるかは、丸めの誤差で決まる)ので、同じ式で写す。
    /// </summary>
    public static List<RayHit> HitsOn(Ray ray, SceneObject o)
    {
        var hits = new List<RayHit>();
        var tri = o.Triangles;
        if (tri.Length == 0) return hits;
        var matrix = o.MatrixWorld;
        // 外接球(ワールド): 中心を matrixWorld で、半径を最大の拡大率で変換。レイの始点が球の外で、球に当たらなければ、当たらない
        var center = matrix.ApplyToPoint(o.SphereCenter);
        var radius = o.SphereRadius * matrix.MaxScaleOnAxis();
        var origin0 = ray.At(0); // recast(near = 0)
        var dx = origin0.X - center.X; var dy = origin0.Y - center.Y; var dz = origin0.Z - center.Z;
        if (!(dx * dx + dy * dy + dz * dz <= radius * radius) && !HitsSphere(new Ray(origin0, ray.Direction), center, radius)) return hits;
        var inverse = matrix.Inverted();
        var local = new Ray(inverse.ApplyToPoint(ray.Origin), inverse.TransformDirection(ray.Direction));
        if (!HitsBox(local, o.BoundsMin, o.BoundsMax)) return hits;
        for (var i = 0; i + 2 < tri.Length; i += 3)
        {
            if (RayTriangle(local, tri[i], tri[i + 1], tri[i + 2]) is not { } t) continue;
            var point = matrix.ApplyToPoint(local.At(t));
            hits.Add(new RayHit(o, i / 3, ray.Origin.DistanceTo(point), point));
        }
        return hits;
    }

    /// <summary>objects に当たるものを、近い順に(three.js の intersectObjects)。</summary>
    public static List<RayHit> Raycast(Ray ray, IEnumerable<SceneObject> objects)
    {
        var hits = new List<RayHit>();
        foreach (var o in objects) hits.AddRange(HitsOn(ray, o));
        return [.. hits.OrderBy(h => h.Distance)]; // OrderBy は安定
    }

    /// <summary>オブジェクトの三角形のうち、いちばん近い当たりの距離。</summary>
    public static double? Nearest(Ray ray, SceneObject o)
    {
        double? best = null;
        foreach (var h in HitsOn(ray, o)) if (best is null || h.Distance < best) best = h.Distance;
        return best;
    }

    /// <summary>レイに最初に当たるオブジェクト。無ければ null。</summary>
    public static SceneObject? Pick(Ray ray, IEnumerable<SceneObject> objects)
    {
        var hits = Raycast(ray, objects);
        return hits.Count > 0 ? hits[0].Object : null;
    }
}
