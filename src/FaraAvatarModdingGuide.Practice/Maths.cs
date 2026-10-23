using System.Globalization;

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

    /// <summary>parseFloat のうち、練習の数値入力(数字・小数点・先頭の "-")に必要な範囲。数値にならないもの("-." など)は null。</summary>
    public static double? ParseNumeric(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : null;
}
