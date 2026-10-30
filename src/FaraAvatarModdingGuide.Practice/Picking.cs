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
    internal static bool HitsSphere(Ray ray, Vec3 center, double radius)
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
    internal static bool HitsBox(Ray ray, Vec3 min, Vec3 max)
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

    // ---------------------------------------------------------------- 軽量版(隠れ判定・ボックス選択用)
    // 結果は Raycast / HitsOn と同じ(同じ式・同じ順。距離が同じなら先のオブジェクト・先の三角形)。変えたのは、使い回しと、当たらない三角形を先に捨てること。
    //   ① オブジェクトごとの行列・逆行列・外接球をレイごとに作り直さない(Prepared)  ② 当たりの一覧を作って並べ替えず、いちばん近いものだけ持つ(隠れるかだけなら、手前に 1 つ見つかった時点で止める)
    //   ③ 画面の点(NDC)のレイなら、三角形を画面に投影した外接箱の格子(PickIndex)で、そこを通りえない三角形を飛ばす。飛ばすのは「レイが通りえない」ものだけ(余裕を付けて広めに残す)ので、
    //      残った三角形は同じ判定(RayTriangle)にかける。

    /// <summary>レイごとに変わらない、オブジェクトのワールドでの値。</summary>
    public sealed class Prepared
    {
        public Prepared(SceneObject o)
        {
            Object = o;
            Matrix = o.MatrixWorld;
            Inverse = Matrix.Inverted();
            Center = Matrix.ApplyToPoint(o.SphereCenter);
            Radius = o.SphereRadius * Matrix.MaxScaleOnAxis();
        }

        public SceneObject Object { get; }
        public Mat4 Matrix { get; }
        public Mat4 Inverse { get; }
        public Vec3 Center { get; }
        public double Radius { get; }
    }

    public static List<Prepared> Prepare(IEnumerable<SceneObject> objects) => [.. objects.Where(o => o.Triangles.Length > 0).Select(o => new Prepared(o))];

    /// <summary>外接球 → ローカルへ → 外接箱(HitsOn の前半)。通ったら、ローカルのレイを返す。</summary>
    private static bool Broad(Prepared p, Ray ray, out Ray local)
    {
        local = default;
        var origin0 = ray.At(0);
        var dx = origin0.X - p.Center.X; var dy = origin0.Y - p.Center.Y; var dz = origin0.Z - p.Center.Z;
        if (!(dx * dx + dy * dy + dz * dz <= p.Radius * p.Radius) && !HitsSphere(new Ray(origin0, ray.Direction), p.Center, p.Radius)) return false;
        local = new Ray(p.Inverse.ApplyToPoint(ray.Origin), p.Inverse.TransformDirection(ray.Direction));
        return HitsBox(local, p.Object.BoundsMin, p.Object.BoundsMax);
    }

    /// <summary>
    /// ray に最初に当たるもの(<see cref="Raycast"/> の先頭と同じ)。stopBelow を指定すると、それより近い当たりが見つかった時点で、その当たりを返して止める
    /// (隠れているかだけを知りたいとき。先頭かどうかは問わない)。candidates は、三角形の番号を絞る関数(null なら全部)。
    /// </summary>
    public static (SceneObject Object, int FaceIndex, double Distance)? Nearest(Ray ray, IReadOnlyList<Prepared> prepared, double? stopBelow = null, Func<Prepared, IReadOnlyList<int>?>? candidates = null)
    {
        (SceneObject, int, double)? best = null;
        foreach (var p in prepared)
        {
            if (!Broad(p, ray, out var local)) continue;
            var tri = p.Object.Triangles;
            var list = candidates?.Invoke(p);
            var n = list?.Count ?? tri.Length / 3;
            for (var j = 0; j < n; j++)
            {
                var i = list is null ? j : list[j];
                if (RayTriangle(local, tri[i * 3], tri[i * 3 + 1], tri[i * 3 + 2]) is not { } t) continue;
                var distance = ray.Origin.DistanceTo(p.Matrix.ApplyToPoint(local.At(t)));
                if (best is null || distance < best.Value.Item3)
                {
                    best = (p.Object, i, distance);
                    if (stopBelow is { } limit && distance < limit) return best;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// 三角形を画面(NDC)に投影して、格子に入れておく。同じ視点で、画面の点を何度もレイにして調べる(ボックス選択の隠れ判定)ときに作る。
    /// 点(nx, ny)のレイが通りうる三角形 = その点の格子の中の三角形 + 画面に収まらない・カメラの面をまたぐ三角形(いつも)。番号は小さい順。
    /// </summary>
    public sealed class PickIndex
    {
        private const int Cells = 32;
        private const double Margin = 1e-6;   // 投影の丸めの誤差より十分大きい余裕(NDC)
        private readonly Dictionary<SceneObject, (List<int>[] Grid, List<int> Always, List<int> All)> byObject = [];
        public IReadOnlyList<Prepared> Prepared { get; }

        public PickIndex(IReadOnlyList<Prepared> prepared, OrbitView view)
        {
            Prepared = prepared;
            var (v, proj, _) = view.Matrices();
            var m = Mat4.Multiply(proj, v).E;
            foreach (var p in prepared)
            {
                var grid = new List<int>[Cells * Cells];
                var always = new List<int>();
                var all = new List<int>();
                var tri = p.Object.Triangles;
                var world = p.Matrix.E;
                var count = tri.Length / 3;
                for (var t = 0; t < count; t++)
                {
                    all.Add(t);
                    double minX = double.PositiveInfinity, minY = minX, maxX = double.NegativeInfinity, maxY = maxX;
                    var behind = false;
                    for (var k = 0; k < 3; k++)
                    {
                        var q = tri[t * 3 + k];
                        double wx = world[0] * q.X + world[4] * q.Y + world[8] * q.Z + world[12];
                        double wy = world[1] * q.X + world[5] * q.Y + world[9] * q.Z + world[13];
                        double wz = world[2] * q.X + world[6] * q.Y + world[10] * q.Z + world[14];
                        double cx = m[0] * wx + m[4] * wy + m[8] * wz + m[12];
                        double cy = m[1] * wx + m[5] * wy + m[9] * wz + m[13];
                        double cw = m[3] * wx + m[7] * wy + m[11] * wz + m[15];
                        if (!(cw > 1e-9)) { behind = true; break; }
                        var nx = cx / cw; var ny = cy / cw;
                        if (nx < minX) minX = nx; if (nx > maxX) maxX = nx;
                        if (ny < minY) minY = ny; if (ny > maxY) maxY = ny;
                    }
                    if (behind || !double.IsFinite(minX + minY + maxX + maxY)) { always.Add(t); continue; }
                    int x0 = Cell(minX - Margin), x1 = Cell(maxX + Margin), y0 = Cell(minY - Margin), y1 = Cell(maxY + Margin);
                    for (var y = y0; y <= y1; y++)
                        for (var x = x0; x <= x1; x++) (grid[y * Cells + x] ??= []).Add(t);
                }
                byObject[p.Object] = (grid, always, all);
            }
        }

        private static int Cell(double ndc) => Math.Clamp((int)Math.Floor((ndc + 1) / 2 * Cells), 0, Cells - 1);

        /// <summary>点(nx, ny)のレイが通りうる三角形の番号(小さい順)を、オブジェクトごとに返す関数(<see cref="Picking.Nearest"/> の candidates に渡す)。</summary>
        public Func<Prepared, IReadOnlyList<int>?> At(double nx, double ny)
        {
            var outside = !(Math.Abs(nx) <= 1 && Math.Abs(ny) <= 1);
            var cell = outside ? 0 : Cell(ny) * Cells + Cell(nx);
            return p =>
            {
                var (grid, always, all) = byObject[p.Object];
                if (outside) return all;
                var inCell = grid[cell];
                if (always.Count == 0) return inCell ?? (IReadOnlyList<int>)[];
                if (inCell is null) return always;
                // 小さい順に合わせる(同じ距離のとき、先の三角形が先になるように)
                var merged = new List<int>(inCell.Count + always.Count);
                merged.AddRange(inCell);
                merged.AddRange(always);
                merged.Sort();
                return merged;
            };
        }
    }
}
