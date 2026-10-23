using System.Globalization;
using System.Text.RegularExpressions;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>ベクトル(three.js の Vector3 に当たる。倍精度)。</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 One = new(1, 1, 1);
    public static readonly Vec3 UnitX = new(1, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    /// <summary>成分ごとの積(three.js の multiply)。</summary>
    public static Vec3 operator *(Vec3 a, Vec3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double LengthSq => Dot(this);
    public double Length => Math.Sqrt(LengthSq);
    public Vec3 Normalized() { var l = Length; return this * (1 / (l == 0 ? 1 : l)); }
    public double DistanceTo(Vec3 b) => (this - b).Length;

    /// <summary>"x" / "y" / "z" の成分。</summary>
    public double Get(string axis) => axis switch { "x" => X, "y" => Y, "z" => Z, _ => throw new ArgumentOutOfRangeException(nameof(axis)) };

    /// <summary>"x" / "y" / "z" の成分だけを差し替えたもの。</summary>
    public Vec3 With(string axis, double v) => axis switch { "x" => this with { X = v }, "y" => this with { Y = v }, "z" => this with { Z = v }, _ => this };

    public double[] ToArray() => [X, Y, Z];

    /// <summary>three.js の divideScalar(1/s を掛ける。x / s とは丸めが違う)。</summary>
    public Vec3 DivideScalar(double s) => this * (1 / s);

    /// <summary>three.js の lerp: x += (v.x - x) * t。</summary>
    public Vec3 Lerp(Vec3 v, double t) => new(X + (v.X - X) * t, Y + (v.Y - Y) * t, Z + (v.Z - Z) * t);

    /// <summary>three.js の addScaledVector: x += v.x * s。</summary>
    public Vec3 AddScaled(Vec3 v, double s) => new(X + v.X * s, Y + v.Y * s, Z + v.Z * s);

    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>
/// 4x4 の行列(three.js の Matrix4。要素は列順)。オブジェクトの matrixWorld(位置・回転・スケールから compose)と、その逆行列。
/// 編集モードでローカル座標とワールド座標を行き来する計算(applyMatrix4・invert)と、レイをローカル空間へ移す計算を、three.js と同じ式・同じ順で写す
/// (ビューの親は単位行列なので matrixWorld = matrix)。
/// </summary>
public readonly struct Mat4
{
    public readonly double[] E;
    private Mat4(double[] e) { E = e; }

    /// <summary>単位行列。</summary>
    public static Mat4 Identity => Compose(Vec3.Zero, Quat.Identity, Vec3.One);

    /// <summary>16 個の要素(列順。three.js の elements)から作る。描画層が行列を受け渡すためのもの(配列は写す)。</summary>
    public static Mat4 FromElements(ReadOnlySpan<double> e)
    {
        if (e.Length != 16) throw new ArgumentException("16 個の要素が要ります", nameof(e));
        return new Mat4(e.ToArray());
    }

    /// <summary>three.js の Matrix4.multiplyMatrices(a, b)(= a × b。b を先に適用する)。</summary>
    public static Mat4 Multiply(Mat4 a, Mat4 b)
    {
        var ae = a.E;
        var be = b.E;
        var te = new double[16];
        double a11 = ae[0], a12 = ae[4], a13 = ae[8], a14 = ae[12];
        double a21 = ae[1], a22 = ae[5], a23 = ae[9], a24 = ae[13];
        double a31 = ae[2], a32 = ae[6], a33 = ae[10], a34 = ae[14];
        double a41 = ae[3], a42 = ae[7], a43 = ae[11], a44 = ae[15];
        double b11 = be[0], b12 = be[4], b13 = be[8], b14 = be[12];
        double b21 = be[1], b22 = be[5], b23 = be[9], b24 = be[13];
        double b31 = be[2], b32 = be[6], b33 = be[10], b34 = be[14];
        double b41 = be[3], b42 = be[7], b43 = be[11], b44 = be[15];
        te[0] = a11 * b11 + a12 * b21 + a13 * b31 + a14 * b41;
        te[4] = a11 * b12 + a12 * b22 + a13 * b32 + a14 * b42;
        te[8] = a11 * b13 + a12 * b23 + a13 * b33 + a14 * b43;
        te[12] = a11 * b14 + a12 * b24 + a13 * b34 + a14 * b44;
        te[1] = a21 * b11 + a22 * b21 + a23 * b31 + a24 * b41;
        te[5] = a21 * b12 + a22 * b22 + a23 * b32 + a24 * b42;
        te[9] = a21 * b13 + a22 * b23 + a23 * b33 + a24 * b43;
        te[13] = a21 * b14 + a22 * b24 + a23 * b34 + a24 * b44;
        te[2] = a31 * b11 + a32 * b21 + a33 * b31 + a34 * b41;
        te[6] = a31 * b12 + a32 * b22 + a33 * b32 + a34 * b42;
        te[10] = a31 * b13 + a32 * b23 + a33 * b33 + a34 * b43;
        te[14] = a31 * b14 + a32 * b24 + a33 * b34 + a34 * b44;
        te[3] = a41 * b11 + a42 * b21 + a43 * b31 + a44 * b41;
        te[7] = a41 * b12 + a42 * b22 + a43 * b32 + a44 * b42;
        te[11] = a41 * b13 + a42 * b23 + a43 * b33 + a44 * b43;
        te[15] = a41 * b14 + a42 * b24 + a43 * b34 + a44 * b44;
        return new Mat4(te);
    }

    /// <summary>three.js の Matrix4.determinant()。負なら、鏡映(スケールの符号が奇数個マイナス)で、面の表裏が逆になる。</summary>
    public double Determinant()
    {
        var te = E;
        double n11 = te[0], n21 = te[1], n31 = te[2], n41 = te[3];
        double n12 = te[4], n22 = te[5], n32 = te[6], n42 = te[7];
        double n13 = te[8], n23 = te[9], n33 = te[10], n43 = te[11];
        double n14 = te[12], n24 = te[13], n34 = te[14], n44 = te[15];
        return n41 * (+n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34)
            + n42 * (+n11 * n23 * n34 - n11 * n24 * n33 + n14 * n21 * n33 - n13 * n21 * n34 + n13 * n24 * n31 - n14 * n23 * n31)
            + n43 * (+n11 * n24 * n32 - n11 * n22 * n34 - n14 * n21 * n32 + n12 * n21 * n34 + n14 * n22 * n31 - n12 * n24 * n31)
            + n44 * (-n13 * n22 * n31 - n11 * n23 * n32 + n11 * n22 * n33 + n13 * n21 * n32 - n12 * n21 * n33 + n12 * n23 * n31);
    }

    public static Mat4 Compose(Vec3 position, Quat q, Vec3 scale)
    {
        var te = new double[16];
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        double x2 = x + x, y2 = y + y, z2 = z + z;
        double xx = x * x2, xy = x * y2, xz = x * z2;
        double yy = y * y2, yz = y * z2, zz = z * z2;
        double wx = w * x2, wy = w * y2, wz = w * z2;
        double sx = scale.X, sy = scale.Y, sz = scale.Z;
        te[0] = (1 - (yy + zz)) * sx;
        te[1] = (xy + wz) * sx;
        te[2] = (xz - wy) * sx;
        te[3] = 0;
        te[4] = (xy - wz) * sy;
        te[5] = (1 - (xx + zz)) * sy;
        te[6] = (yz + wx) * sy;
        te[7] = 0;
        te[8] = (xz + wy) * sz;
        te[9] = (yz - wx) * sz;
        te[10] = (1 - (xx + yy)) * sz;
        te[11] = 0;
        te[12] = position.X;
        te[13] = position.Y;
        te[14] = position.Z;
        te[15] = 1;
        return new Mat4(te);
    }

    /// <summary>three.js の Matrix4.makePerspective(WebGL の座標系)。</summary>
    public static Mat4 MakePerspective(double left, double right, double top, double bottom, double near, double far)
    {
        var te = new double[16];
        var x = 2 * near / (right - left);
        var y = 2 * near / (top - bottom);
        var a = (right + left) / (right - left);
        var b = (top + bottom) / (top - bottom);
        var c = -(far + near) / (far - near);
        var d = -2 * far * near / (far - near);
        te[0] = x; te[4] = 0; te[8] = a; te[12] = 0;
        te[1] = 0; te[5] = y; te[9] = b; te[13] = 0;
        te[2] = 0; te[6] = 0; te[10] = c; te[14] = d;
        te[3] = 0; te[7] = 0; te[11] = -1; te[15] = 0;
        return new Mat4(te);
    }

    /// <summary>three.js の Matrix4.makeOrthographic(WebGL の座標系)。</summary>
    public static Mat4 MakeOrthographic(double left, double right, double top, double bottom, double near, double far)
    {
        var te = new double[16];
        var w = 1.0 / (right - left);
        var h = 1.0 / (top - bottom);
        var p = 1.0 / (far - near);
        var x = (right + left) * w;
        var y = (top + bottom) * h;
        var z = (far + near) * p;
        var zInv = -2 * p;
        te[0] = 2 * w; te[4] = 0; te[8] = 0; te[12] = -x;
        te[1] = 0; te[5] = 2 * h; te[9] = 0; te[13] = -y;
        te[2] = 0; te[6] = 0; te[10] = zInv; te[14] = -z;
        te[3] = 0; te[7] = 0; te[11] = 0; te[15] = 1;
        return new Mat4(te);
    }

    /// <summary>three.js の Matrix4.invert(行列式が 0 のときは 0 の行列)。</summary>
    public Mat4 Inverted()
    {
        var te = E;
        double n11 = te[0], n21 = te[1], n31 = te[2], n41 = te[3],
            n12 = te[4], n22 = te[5], n32 = te[6], n42 = te[7],
            n13 = te[8], n23 = te[9], n33 = te[10], n43 = te[11],
            n14 = te[12], n24 = te[13], n34 = te[14], n44 = te[15],
            t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44,
            t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44,
            t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44,
            t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;
        var det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
        var r = new double[16];
        if (det == 0) return new Mat4(r);
        var detInv = 1 / det;
        r[0] = t11 * detInv;
        r[1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * detInv;
        r[2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * detInv;
        r[3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * detInv;
        r[4] = t12 * detInv;
        r[5] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * detInv;
        r[6] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * detInv;
        r[7] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * detInv;
        r[8] = t13 * detInv;
        r[9] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * detInv;
        r[10] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * detInv;
        r[11] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * detInv;
        r[12] = t14 * detInv;
        r[13] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * detInv;
        r[14] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * detInv;
        r[15] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * detInv;
        return new Mat4(r);
    }

    /// <summary>three.js の Matrix4.getMaxScaleOnAxis(列ベクトルの長さの最大)。</summary>
    public double MaxScaleOnAxis()
    {
        var te = E;
        var scaleXSq = te[0] * te[0] + te[1] * te[1] + te[2] * te[2];
        var scaleYSq = te[4] * te[4] + te[5] * te[5] + te[6] * te[6];
        var scaleZSq = te[8] * te[8] + te[9] * te[9] + te[10] * te[10];
        return Math.Sqrt(Math.Max(Math.Max(scaleXSq, scaleYSq), scaleZSq));
    }

    /// <summary>three.js の Vector3.applyMatrix4(点。w で割る)。</summary>
    public Vec3 ApplyToPoint(Vec3 v)
    {
        double x = v.X, y = v.Y, z = v.Z;
        var e = E;
        var w = 1 / (e[3] * x + e[7] * y + e[11] * z + e[15]);
        return new Vec3((e[0] * x + e[4] * y + e[8] * z + e[12]) * w, (e[1] * x + e[5] * y + e[9] * z + e[13]) * w, (e[2] * x + e[6] * y + e[10] * z + e[14]) * w);
    }

    /// <summary>three.js の Vector3.transformDirection(向き。正規化する)。</summary>
    public Vec3 TransformDirection(Vec3 v)
    {
        double x = v.X, y = v.Y, z = v.Z;
        var e = E;
        return new Vec3(e[0] * x + e[4] * y + e[8] * z, e[1] * x + e[5] * y + e[9] * z, e[2] * x + e[6] * y + e[10] * z).Normalized();
    }

    /// <summary>
    /// three.js の Matrix3.getNormalMatrix(上 3x3 の逆行列の転置)で法線 n を変換する(Vector3.applyMatrix3。正規化はしない)。
    /// </summary>
    public Vec3 ApplyNormalMatrix(Vec3 n)
    {
        var te = E;
        double n11 = te[0], n21 = te[1], n31 = te[2], n12 = te[4], n22 = te[5], n32 = te[6], n13 = te[8], n23 = te[9], n33 = te[10];
        double t11 = n33 * n22 - n32 * n23, t12 = n32 * n13 - n33 * n12, t13 = n23 * n12 - n22 * n13;
        var det = n11 * t11 + n21 * t12 + n31 * t13;
        var r = new double[9];
        if (det != 0)
        {
            var detInv = 1 / det;
            r[0] = t11 * detInv;
            r[1] = (n31 * n23 - n33 * n21) * detInv;
            r[2] = (n32 * n21 - n31 * n22) * detInv;
            r[3] = t12 * detInv;
            r[4] = (n33 * n11 - n31 * n13) * detInv;
            r[5] = (n31 * n12 - n32 * n11) * detInv;
            r[6] = t13 * detInv;
            r[7] = (n21 * n13 - n23 * n11) * detInv;
            r[8] = (n22 * n11 - n21 * n12) * detInv;
        }
        (r[1], r[3]) = (r[3], r[1]);
        (r[2], r[6]) = (r[6], r[2]);
        (r[5], r[7]) = (r[7], r[5]);
        return new Vec3(r[0] * n.X + r[3] * n.Y + r[6] * n.Z, r[1] * n.X + r[4] * n.Y + r[7] * n.Z, r[2] * n.X + r[5] * n.Y + r[8] * n.Z);
    }
}

/// <summary>クォータニオン(three.js の Quaternion に当たる。正規化はしない: three.js も掛け算のあとに正規化しない)。</summary>
public readonly record struct Quat(double X, double Y, double Z, double W)
{
    public static readonly Quat Identity = new(0, 0, 0, 1);

    /// <summary>軸は単位ベクトルであること(three.js の setFromAxisAngle と同じ。ここでは正規化しない)。</summary>
    public static Quat FromAxisAngle(Vec3 axis, double angle)
    {
        var half = angle / 2;
        var s = Math.Sin(half);
        return new Quat(axis.X * s, axis.Y * s, axis.Z * s, Math.Cos(half));
    }

    /// <summary>オイラー角(ラジアン。XYZ 順)から。three.js の Quaternion.setFromEuler(order = XYZ)。</summary>
    public static Quat FromEulerXYZ(double x, double y, double z)
    {
        double c1 = Math.Cos(x / 2), c2 = Math.Cos(y / 2), c3 = Math.Cos(z / 2);
        double s1 = Math.Sin(x / 2), s2 = Math.Sin(y / 2), s3 = Math.Sin(z / 2);
        return new Quat(
            s1 * c2 * c3 + c1 * s2 * s3,
            c1 * s2 * c3 - s1 * c2 * s3,
            c1 * c2 * s3 + s1 * s2 * c3,
            c1 * c2 * c3 - s1 * s2 * s3);
    }

    /// <summary>
    /// 回転行列(列 x・y・z)から(three.js の Quaternion.setFromRotationMatrix)。カメラの向きは lookAt の行列をいったんクォータニオンにして、compose し直す。
    /// </summary>
    public static Quat FromRotationBasis(Vec3 x, Vec3 y, Vec3 z)
    {
        double m11 = x.X, m12 = y.X, m13 = z.X, m21 = x.Y, m22 = y.Y, m23 = z.Y, m31 = x.Z, m32 = y.Z, m33 = z.Z;
        var trace = m11 + m22 + m33;
        if (trace > 0)
        {
            var s = 0.5 / Math.Sqrt(trace + 1.0);
            return new Quat((m32 - m23) * s, (m13 - m31) * s, (m21 - m12) * s, 0.25 / s);
        }
        if (m11 > m22 && m11 > m33)
        {
            var s = 2.0 * Math.Sqrt(1.0 + m11 - m22 - m33);
            return new Quat(0.25 * s, (m12 + m21) / s, (m13 + m31) / s, (m32 - m23) / s);
        }
        if (m22 > m33)
        {
            var s = 2.0 * Math.Sqrt(1.0 + m22 - m11 - m33);
            return new Quat((m12 + m21) / s, 0.25 * s, (m23 + m32) / s, (m13 - m31) / s);
        }
        {
            var s = 2.0 * Math.Sqrt(1.0 + m33 - m11 - m22);
            return new Quat((m13 + m31) / s, (m23 + m32) / s, 0.25 * s, (m21 - m12) / s);
        }
    }

    /// <summary>this * b(three.js の multiply と同じ順)。</summary>
    public static Quat operator *(Quat a, Quat b) => new(
        a.X * b.W + a.W * b.X + a.Y * b.Z - a.Z * b.Y,
        a.Y * b.W + a.W * b.Y + a.Z * b.X - a.X * b.Z,
        a.Z * b.W + a.W * b.Z + a.X * b.Y - a.Y * b.X,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    /// <summary>ベクトルを回す(three.js の applyQuaternion)。</summary>
    public Vec3 Rotate(Vec3 v)
    {
        double tx = 2 * (Y * v.Z - Z * v.Y), ty = 2 * (Z * v.X - X * v.Z), tz = 2 * (X * v.Y - Y * v.X);
        return new Vec3(
            v.X + W * tx + Y * tz - Z * ty,
            v.Y + W * ty + Z * tx - X * tz,
            v.Z + W * tz + X * ty - Y * tx);
    }

    /// <summary>
    /// オイラー角(ラジアン。XYZ 順)。three.js は、オブジェクトの quaternion を変えるたびに rotation をこの式で求め直す
    /// (Euler.setFromQuaternion → makeRotationFromQuaternion → setFromRotationMatrix)。お題の「回転した」判定は rotation.z を読む。
    /// </summary>
    public Vec3 ToEulerXYZ()
    {
        double x2 = X + X, y2 = Y + Y, z2 = Z + Z;
        double xx = X * x2, xy = X * y2, xz = X * z2, yy = Y * y2, yz = Y * z2, zz = Z * z2;
        double wx = W * x2, wy = W * y2, wz = W * z2;
        double m11 = 1 - (yy + zz), m12 = xy - wz, m13 = xz + wy;
        double m22 = 1 - (xx + zz), m23 = yz - wx;
        double m32 = yz + wx, m33 = 1 - (xx + yy);
        var y = Math.Asin(Math.Clamp(m13, -1, 1));
        if (Math.Abs(m13) < 0.9999999)
            return new Vec3(Math.Atan2(-m23, m33), y, Math.Atan2(-m12, m11));
        return new Vec3(Math.Atan2(m32, m22), y, 0);
    }
}

/// <summary>JavaScript の挙動に合わせる小さな関数(数値の丸めと文字の書き方)。</summary>
public static class Js
{
    public const double Deg = Math.PI / 180;

    /// <summary>Math.round(0.5 は正の無限大のほうへ)。</summary>
    public static double Round(double x)
    {
        var f = Math.Floor(x);
        return x - f >= 0.5 ? f + 1 : f;
    }

    /// <summary>
    /// Number.prototype.toFixed。正確な 10 進展開を、ちょうど半分は 0 から遠いほうへ丸める(.NET の "F3" は半分の扱いが符号で変わるので使わない)。
    /// 負のゼロは符号なし("0.000")。負の値は丸めて 0 になっても "-0.000"(JavaScript と同じ)。
    /// </summary>
    public static string ToFixed(double x, int digits)
    {
        if (double.IsNaN(x)) return "NaN";
        if (double.IsInfinity(x)) return x > 0 ? "Infinity" : "-Infinity";
        var negative = x < 0;
        // 余分に 40 桁まで正確に出し(.NET Core 3.0 以降は桁数を増やしても正確)、digits 桁の次の桁が 5 以上なら切り上げる
        var text = Math.Abs(x).ToString("F" + (digits + 40), CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        var kept = text[..dot] + text.Substring(dot + 1, digits);
        var roundUp = text[dot + 1 + digits] >= '5';
        var n = System.Numerics.BigInteger.Parse(kept, CultureInfo.InvariantCulture) + (roundUp ? 1 : 0);
        var s = n.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        var result = digits == 0 ? s : s[..^digits] + "." + s[^digits..];
        return negative ? "-" + result : result;
    }

    /// <summary>Math.sign。</summary>
    public static int Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

    private static readonly Regex FloatPrefix = new(@"^[\s﻿]*([+-]?(?:Infinity|(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?))", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// parseFloat(文字の先頭の数だけを読む。先頭の空白は飛ばす。読めなければ NaN)。N パネルの入力欄(type=number)の値に使う。
    /// </summary>
    public static double ParseFloat(string s)
    {
        var m = FloatPrefix.Match(s);
        if (!m.Success) return double.NaN;
        var t = m.Groups[1].Value;
        if (t.EndsWith("Infinity", StringComparison.Ordinal)) return t.StartsWith('-') ? double.NegativeInfinity : double.PositiveInfinity;
        return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Array.prototype.splice(start, deleteCount, ...items)。start が負なら末尾から数え、範囲外は端に寄せる(JavaScript と同じ)。
    /// </summary>
    public static void Splice<T>(List<T> list, int start, int deleteCount, IEnumerable<T> items)
    {
        var len = list.Count;
        var s = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);
        var d = Math.Min(Math.Max(deleteCount, 0), len - s);
        list.RemoveRange(s, d);
        list.InsertRange(s, items.ToList());
    }

    /// <summary>parseFloat のうち、練習の数値入力(数字・小数点・先頭の "-")に必要な範囲。数値にならないもの("-." など)は null。</summary>
    public static double? ParseNumeric(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : null;
}
