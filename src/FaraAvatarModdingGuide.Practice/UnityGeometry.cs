namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// Unity 風の画面(unity.js)の 3D の部品・ギズモの形。three.js の BufferGeometry(index・position・normal)と同じ頂点・並びで作る
/// (SphereGeometry・CapsuleGeometry(= LatheGeometry)・BoxGeometry・CylinderGeometry・ConeGeometry・TorusGeometry と、translate・rotateX)。
/// 位置・法線は three.js と同じく、double で計算したあと Float32 に丸めて持つ。レイ判定(<see cref="UnityMesh.Triangles"/>)と描画(Render)が同じ配列を使う。
/// </summary>
public sealed class UnityMesh
{
    public UnityMesh(float[] positions, float[] normals, int[] indices)
    {
        Positions = positions;
        Normals = normals;
        Indices = indices;
        ComputeBounds();
    }

    public float[] Positions { get; }
    public float[] Normals { get; }
    public int[] Indices { get; }

    /// <summary>ジオメトリのローカルの外接箱(three.js の computeBoundingBox)と外接球(computeBoundingSphere: 箱の中心と、そこから最も遠い頂点までの距離)。</summary>
    public Vec3 BoundsMin { get; private set; }
    public Vec3 BoundsMax { get; private set; }
    public Vec3 SphereCenter { get; private set; }
    public double SphereRadius { get; private set; }

    public int VertexCount => Positions.Length / 3;

    /// <summary>三角形(3 頂点ずつ。インデックスを展開したもの)の位置。</summary>
    public Vec3[] Triangles()
    {
        var tri = new Vec3[Indices.Length];
        for (var i = 0; i < Indices.Length; i++) tri[i] = P(Indices[i]);
        return tri;
    }

    private Vec3 P(int i) => new(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]);

    private void ComputeBounds()
    {
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = -min;
        for (var i = 0; i < VertexCount; i++)
        {
            var p = P(i);
            min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        BoundsMin = min;
        BoundsMax = max;
        var center = (min + max) * 0.5;
        double maxRadiusSq = 0;
        for (var i = 0; i < VertexCount; i++) maxRadiusSq = Math.Max(maxRadiusSq, (P(i) - center).LengthSq);
        SphereCenter = center;
        SphereRadius = Math.Sqrt(maxRadiusSq);
    }

    /// <summary>BufferGeometry.applyMatrix4(matrix): 位置は行列で、法線は法線行列で変えて正規化し直す。</summary>
    public UnityMesh Apply(Mat4 m)
    {
        var pos = new float[Positions.Length];
        var nor = new float[Normals.Length];
        for (var i = 0; i < VertexCount; i++)
        {
            var p = m.ApplyToPoint(P(i));
            pos[i * 3] = (float)p.X; pos[i * 3 + 1] = (float)p.Y; pos[i * 3 + 2] = (float)p.Z;
            var n = m.ApplyNormalMatrix(new Vec3(Normals[i * 3], Normals[i * 3 + 1], Normals[i * 3 + 2])).Normalized();
            nor[i * 3] = (float)n.X; nor[i * 3 + 1] = (float)n.Y; nor[i * 3 + 2] = (float)n.Z;
        }
        return new UnityMesh(pos, nor, Indices);
    }

    public UnityMesh Translate(double x, double y, double z) => Apply(Mat4.Compose(new Vec3(x, y, z), Quat.Identity, Vec3.One));

    /// <summary>Matrix4.makeRotationX(angle)。</summary>
    public UnityMesh RotateX(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return Apply(Mat4.FromElements([1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1]));
    }

    private sealed class Builder
    {
        public readonly List<float> Pos = [];
        public readonly List<float> Nor = [];
        public readonly List<int> Idx = [];
        public void V(double x, double y, double z) { Pos.Add((float)x); Pos.Add((float)y); Pos.Add((float)z); }
        public void N(double x, double y, double z) { Nor.Add((float)x); Nor.Add((float)y); Nor.Add((float)z); }
        public UnityMesh Build() => new([.. Pos], [.. Nor], [.. Idx]);
    }

    // ---------------------------------------------------------------- SphereGeometry
    public static UnityMesh Sphere(double radius, int widthSegments, int heightSegments, double phiStart = 0, double phiLength = Math.PI * 2, double thetaStart = 0, double thetaLength = Math.PI)
    {
        widthSegments = Math.Max(3, widthSegments);
        heightSegments = Math.Max(2, heightSegments);
        var thetaEnd = Math.Min(thetaStart + thetaLength, Math.PI);
        var b = new Builder();
        var index = 0;
        var grid = new List<int[]>();
        for (var iy = 0; iy <= heightSegments; iy++)
        {
            var row = new int[widthSegments + 1];
            var v = (double)iy / heightSegments;
            for (var ix = 0; ix <= widthSegments; ix++)
            {
                var u = (double)ix / widthSegments;
                var x = -radius * Math.Cos(phiStart + u * phiLength) * Math.Sin(thetaStart + v * thetaLength);
                var y = radius * Math.Cos(thetaStart + v * thetaLength);
                var z = radius * Math.Sin(phiStart + u * phiLength) * Math.Sin(thetaStart + v * thetaLength);
                b.V(x, y, z);
                var n = new Vec3(x, y, z).Normalized();
                b.N(n.X, n.Y, n.Z);
                row[ix] = index++;
            }
            grid.Add(row);
        }
        for (var iy = 0; iy < heightSegments; iy++)
        {
            for (var ix = 0; ix < widthSegments; ix++)
            {
                int a = grid[iy][ix + 1], bb = grid[iy][ix], c = grid[iy + 1][ix], d = grid[iy + 1][ix + 1];
                if (iy != 0 || thetaStart > 0) { b.Idx.Add(a); b.Idx.Add(bb); b.Idx.Add(d); }
                if (iy != heightSegments - 1 || thetaEnd < Math.PI) { b.Idx.Add(bb); b.Idx.Add(c); b.Idx.Add(d); }
            }
        }
        return b.Build();
    }

    // ---------------------------------------------------------------- CapsuleGeometry(LatheGeometry)
    public static UnityMesh Capsule(double radius, double length, int capSegments, int radialSegments)
    {
        var points = CapsulePath(radius, length, capSegments);
        return Lathe(points, radialSegments);
    }

    /// <summary>Path.absarc(0, -length/2, r, 1.5π, 0) + absarc(0, length/2, r, 0, 0.5π) の getPoints(capSegments)(つなぎの LineCurve を含む。連続して同じ点は飛ばす)。</summary>
    private const double Eps = 2.220446049250313e-16;   // Number.EPSILON

    private static List<(double X, double Y)> CapsulePath(double r, double length, int capSegments)
    {
        (double X, double Y) Arc(double cy, double start, double end, double t)
        {
            const double twoPi = Math.PI * 2;
            var delta = end - start;
            var same = Math.Abs(delta) < Eps;
            while (delta < 0) delta += twoPi;
            while (delta > twoPi) delta -= twoPi;
            if (delta < Eps) delta = same ? 0 : twoPi;
            var angle = start + t * delta;
            return (0 + r * Math.Cos(angle), cy + r * Math.Sin(angle));
        }
        var pts = new List<(double X, double Y)>();
        void Add(IEnumerable<(double X, double Y)> list)
        {
            foreach (var p in list)
            {
                if (pts.Count > 0 && pts[^1] == p) continue;
                pts.Add(p);
            }
        }
        var resolution = capSegments * 2;
        IEnumerable<(double X, double Y)> Curve(double cy, double start, double end) => Enumerable.Range(0, resolution + 1).Select(d => Arc(cy, start, end, (double)d / resolution));
        Add(Curve(-length / 2, Math.PI * 1.5, 0));
        var first = Arc(length / 2, 0, Math.PI * 0.5, 0);
        var current = Arc(-length / 2, Math.PI * 1.5, 0, 1);
        if (first != current) Add([current, first]);   // lineTo(firstPoint): LineCurve は resolution 1(始点と終点の 2 点)
        Add(Curve(length / 2, 0, Math.PI * 0.5));
        return pts;
    }

    private static UnityMesh Lathe(List<(double X, double Y)> points, int segments)
    {
        var b = new Builder();
        var initNormals = new List<(double X, double Y, double Z)>();
        double dx, dy;
        (double X, double Y, double Z) prev = default;
        for (var j = 0; j <= points.Count - 1; j++)
        {
            if (j == 0)
            {
                dx = points[j + 1].X - points[j].X;
                dy = points[j + 1].Y - points[j].Y;
                var normal = new Vec3(dy * 1.0, -dx, dy * 0.0);
                prev = (normal.X, normal.Y, normal.Z);
                normal = normal.Normalized();
                initNormals.Add((normal.X, normal.Y, normal.Z));
            }
            else if (j == points.Count - 1)
            {
                initNormals.Add(prev);
            }
            else
            {
                dx = points[j + 1].X - points[j].X;
                dy = points[j + 1].Y - points[j].Y;
                var normal = new Vec3(dy * 1.0, -dx, dy * 0.0);
                var cur = (normal.X, normal.Y, normal.Z);
                normal = new Vec3(normal.X + prev.X, normal.Y + prev.Y, normal.Z + prev.Z).Normalized();
                initNormals.Add((normal.X, normal.Y, normal.Z));
                prev = cur;
            }
        }
        var inverseSegments = 1.0 / segments;
        for (var i = 0; i <= segments; i++)
        {
            var phi = 0 + i * inverseSegments * (Math.PI * 2);
            var sin = Math.Sin(phi);
            var cos = Math.Cos(phi);
            for (var j = 0; j <= points.Count - 1; j++)
            {
                b.V(points[j].X * sin, points[j].Y, points[j].X * cos);
                b.N(initNormals[j].X * sin, initNormals[j].Y, initNormals[j].X * cos);
            }
        }
        for (var i = 0; i < segments; i++)
        {
            for (var j = 0; j < points.Count - 1; j++)
            {
                var bs = j + i * points.Count;
                int a = bs, bb = bs + points.Count, c = bs + points.Count + 1, d = bs + 1;
                b.Idx.Add(a); b.Idx.Add(bb); b.Idx.Add(d);
                b.Idx.Add(c); b.Idx.Add(d); b.Idx.Add(bb);
            }
        }
        return b.Build();
    }

    // ---------------------------------------------------------------- BoxGeometry
    public static UnityMesh Box(double width, double height, double depth)
    {
        var b = new Builder();
        var numberOfVertices = 0;
        void Plane(char u, char v, char w, double udir, double vdir, double pw, double ph, double pd, int gridX, int gridY)
        {
            var segW = pw / gridX;
            var segH = ph / gridY;
            double wHalf = pw / 2, hHalf = ph / 2, dHalf = pd / 2;
            int gridX1 = gridX + 1, gridY1 = gridY + 1;
            var counter = 0;
            for (var iy = 0; iy < gridY1; iy++)
            {
                var y = iy * segH - hHalf;
                for (var ix = 0; ix < gridX1; ix++)
                {
                    var x = ix * segW - wHalf;
                    double[] vec = [0, 0, 0];
                    vec[Axis(u)] = x * udir;
                    vec[Axis(v)] = y * vdir;
                    vec[Axis(w)] = dHalf;
                    b.V(vec[0], vec[1], vec[2]);
                    vec = [0, 0, 0];
                    vec[Axis(w)] = pd > 0 ? 1 : -1;
                    b.N(vec[0], vec[1], vec[2]);
                    counter++;
                }
            }
            for (var iy = 0; iy < gridY; iy++)
            {
                for (var ix = 0; ix < gridX; ix++)
                {
                    var a = numberOfVertices + ix + gridX1 * iy;
                    var bb = numberOfVertices + ix + gridX1 * (iy + 1);
                    var c = numberOfVertices + (ix + 1) + gridX1 * (iy + 1);
                    var d = numberOfVertices + (ix + 1) + gridX1 * iy;
                    b.Idx.Add(a); b.Idx.Add(bb); b.Idx.Add(d);
                    b.Idx.Add(bb); b.Idx.Add(c); b.Idx.Add(d);
                }
            }
            numberOfVertices += counter;
        }
        Plane('z', 'y', 'x', -1, -1, depth, height, width, 1, 1);
        Plane('z', 'y', 'x', 1, -1, depth, height, -width, 1, 1);
        Plane('x', 'z', 'y', 1, 1, width, depth, height, 1, 1);
        Plane('x', 'z', 'y', 1, -1, width, depth, -height, 1, 1);
        Plane('x', 'y', 'z', 1, -1, width, height, depth, 1, 1);
        Plane('x', 'y', 'z', -1, -1, width, height, -depth, 1, 1);
        return b.Build();
    }

    private static int Axis(char c) => c switch { 'x' => 0, 'y' => 1, _ => 2 };

    // ---------------------------------------------------------------- CylinderGeometry / ConeGeometry
    public static UnityMesh Cone(double radius, double height, int radialSegments) => Cylinder(0, radius, height, radialSegments);

    public static UnityMesh Cylinder(double radiusTop, double radiusBottom, double height, int radialSegments, int heightSegments = 1)
    {
        var b = new Builder();
        var index = 0;
        var indexArray = new List<int[]>();
        var halfHeight = height / 2;
        const double thetaStart = 0, thetaLength = Math.PI * 2;
        var slope = (radiusBottom - radiusTop) / height;
        for (var y = 0; y <= heightSegments; y++)
        {
            var row = new int[radialSegments + 1];
            var v = (double)y / heightSegments;
            var radius = v * (radiusBottom - radiusTop) + radiusTop;
            for (var x = 0; x <= radialSegments; x++)
            {
                var u = (double)x / radialSegments;
                var theta = u * thetaLength + thetaStart;
                var sinT = Math.Sin(theta);
                var cosT = Math.Cos(theta);
                b.V(radius * sinT, -v * height + halfHeight, radius * cosT);
                var n = new Vec3(sinT, slope, cosT).Normalized();
                b.N(n.X, n.Y, n.Z);
                row[x] = index++;
            }
            indexArray.Add(row);
        }
        for (var x = 0; x < radialSegments; x++)
        {
            for (var y = 0; y < heightSegments; y++)
            {
                int a = indexArray[y][x], bb = indexArray[y + 1][x], c = indexArray[y + 1][x + 1], d = indexArray[y][x + 1];
                if (radiusTop > 0 || y != 0) { b.Idx.Add(a); b.Idx.Add(bb); b.Idx.Add(d); }
                if (radiusBottom > 0 || y != heightSegments - 1) { b.Idx.Add(bb); b.Idx.Add(c); b.Idx.Add(d); }
            }
        }
        void Cap(bool top)
        {
            var centerStart = index;
            var radius = top ? radiusTop : radiusBottom;
            double sign = top ? 1 : -1;
            for (var x = 1; x <= radialSegments; x++) { b.V(0, halfHeight * sign, 0); b.N(0, sign, 0); index++; }
            var centerEnd = index;
            for (var x = 0; x <= radialSegments; x++)
            {
                var u = (double)x / radialSegments;
                var theta = u * thetaLength + thetaStart;
                var cosT = Math.Cos(theta);
                var sinT = Math.Sin(theta);
                b.V(radius * sinT, halfHeight * sign, radius * cosT);
                b.N(0, sign, 0);
                index++;
            }
            for (var x = 0; x < radialSegments; x++)
            {
                int c = centerStart + x, i = centerEnd + x;
                if (top) { b.Idx.Add(i); b.Idx.Add(i + 1); b.Idx.Add(c); }
                else { b.Idx.Add(i + 1); b.Idx.Add(i); b.Idx.Add(c); }
            }
        }
        if (radiusTop > 0) Cap(true);
        if (radiusBottom > 0) Cap(false);
        return b.Build();
    }

    // ---------------------------------------------------------------- TorusGeometry
    public static UnityMesh Torus(double radius, double tube, int radialSegments, int tubularSegments)
    {
        var b = new Builder();
        const double arc = Math.PI * 2;
        for (var j = 0; j <= radialSegments; j++)
        {
            for (var i = 0; i <= tubularSegments; i++)
            {
                var u = (double)i / tubularSegments * arc;
                var v = (double)j / radialSegments * Math.PI * 2;
                var x = (radius + tube * Math.Cos(v)) * Math.Cos(u);
                var y = (radius + tube * Math.Cos(v)) * Math.Sin(u);
                var z = tube * Math.Sin(v);
                b.V(x, y, z);
                var n = (new Vec3(x, y, z) - new Vec3(radius * Math.Cos(u), radius * Math.Sin(u), 0)).Normalized();
                b.N(n.X, n.Y, n.Z);
            }
        }
        for (var j = 1; j <= radialSegments; j++)
        {
            for (var i = 1; i <= tubularSegments; i++)
            {
                var a = (tubularSegments + 1) * j + i - 1;
                var bb = (tubularSegments + 1) * (j - 1) + i - 1;
                var c = (tubularSegments + 1) * (j - 1) + i;
                var d = (tubularSegments + 1) * j + i;
                b.Idx.Add(a); b.Idx.Add(bb); b.Idx.Add(d);
                b.Idx.Add(bb); b.Idx.Add(c); b.Idx.Add(d);
            }
        }
        return b.Build();
    }
}
