namespace FaraAvatarModdingGuide.Practice;

/// <summary>three.js の Euler の順序。unity.js はノードの回転を YXZ で持ち、部品の向きは z だけなので XYZ のまま。</summary>
public enum EulerOrder { XYZ, YXZ }

/// <summary>
/// three.js の Object3D のうち、unity.js が使う分(位置・回転(オイラー角)・クォータニオン・拡大率・ローカル行列・ワールド行列・親子・attach)。
/// <b>matrixWorld は、updateMatrixWorld / updateWorldMatrix を呼んだときにだけ更新される</b>(three.js と同じ)。レイ判定・Frame Selected・ギズモのドラッグは、
/// 最後に描いたときの(古い)ワールド行列を使う場面があり、その挙動(再生の直後は親の行列が単位行列のまま、など)まで同じにするために、同じ手順で更新する。
/// </summary>
public sealed class Obj3D
{
    private Vec3 position = Vec3.Zero;
    private Vec3 euler = Vec3.Zero;
    private Quat quaternion = Quat.Identity;
    private Vec3 scale = Vec3.One;

    public Obj3D(string name = "")
    {
        Name = name;
    }

    public string Name { get; set; }
    public Obj3D? Parent { get; private set; }
    public List<Obj3D> Children { get; } = [];
    public EulerOrder Order { get; set; } = EulerOrder.XYZ;
    public bool Visible { get; set; } = true;
    public Mat4 Matrix { get; private set; } = Mat4.Identity;
    public Mat4 MatrixWorld { get; set; } = Mat4.Identity;
    private bool matrixWorldNeedsUpdate;

    public Vec3 Position { get => position; set => position = value; }
    public Quat Quaternion { get => quaternion; }
    public Vec3 Scale { get => scale; set => scale = value; }
    /// <summary>オイラー角(ラジアン)。</summary>
    public Vec3 Rotation => euler;

    /// <summary>rotation.set(x, y, z, order)(クォータニオンも更新される)。</summary>
    public void SetRotation(double x, double y, double z, EulerOrder order)
    {
        Order = order;
        euler = new Vec3(x, y, z);
        quaternion = UnityMath.QuatFromEuler(euler, Order);
    }

    /// <summary>rotation.z = z(オイラーの順序はそのまま。クォータニオンも更新される)。</summary>
    public void SetRotationZ(double z)
    {
        euler = euler with { Z = z };
        quaternion = UnityMath.QuatFromEuler(euler, Order);
    }

    /// <summary>quaternion を直接変える(rotation はクォータニオンから求め直される)。</summary>
    public void SetQuaternion(Quat q)
    {
        quaternion = q;
        euler = UnityMath.EulerFromQuat(q, Order);
    }

    public void Add(Obj3D child)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = this;
        Children.Add(child);
    }

    public void Clear()
    {
        foreach (var c in Children) c.Parent = null;
        Children.Clear();
    }

    public void RemoveFromParent()
    {
        Parent?.Children.Remove(this);
        Parent = null;
    }

    public void UpdateMatrix()
    {
        Matrix = Mat4.Compose(position, quaternion, scale);
        matrixWorldNeedsUpdate = true;
    }

    /// <summary>Object3D.updateMatrixWorld(force)。</summary>
    public void UpdateMatrixWorld(bool force = false)
    {
        UpdateMatrix();
        if (matrixWorldNeedsUpdate || force)
        {
            MatrixWorld = Parent is null ? Matrix : Mat4.Multiply(Parent.MatrixWorld, Matrix);
            matrixWorldNeedsUpdate = false;
            force = true;
        }
        foreach (var c in Children) c.UpdateMatrixWorld(force);
    }

    /// <summary>Object3D.updateWorldMatrix(updateParents, updateChildren)。</summary>
    public void UpdateWorldMatrix(bool updateParents, bool updateChildren)
    {
        if (updateParents) Parent?.UpdateWorldMatrix(true, false);
        UpdateMatrix();
        if (matrixWorldNeedsUpdate)
        {
            MatrixWorld = Parent is null ? Matrix : Mat4.Multiply(Parent.MatrixWorld, Matrix);
            matrixWorldNeedsUpdate = false;
        }
        if (updateChildren) foreach (var c in Children) c.UpdateWorldMatrix(false, true);
    }

    public Vec3 GetWorldPosition()
    {
        UpdateWorldMatrix(true, false);
        return new Vec3(MatrixWorld.E[12], MatrixWorld.E[13], MatrixWorld.E[14]);
    }

    public Quat GetWorldQuaternion()
    {
        UpdateWorldMatrix(true, false);
        return UnityMath.Decompose(MatrixWorld).Quaternion;
    }

    /// <summary>worldToLocal(v): いま持っている matrixWorld の逆行列で(更新はしない)。</summary>
    public Vec3 WorldToLocal(Vec3 v) => MatrixWorld.Inverted().ApplyToPoint(v);

    /// <summary>applyMatrix4(m): matrix = m × matrix を、位置・回転・拡大率に分解して戻す。</summary>
    public void ApplyMatrix4(Mat4 m)
    {
        UpdateMatrix();
        Matrix = Mat4.Multiply(m, Matrix);
        var (p, q, s) = UnityMath.Decompose(Matrix);
        position = p;
        SetQuaternion(q);
        scale = s;
    }

    /// <summary>this.attach(child): child のワールドでの位置・向き・大きさを保ったまま、this の子にする。</summary>
    public void Attach(Obj3D child)
    {
        UpdateWorldMatrix(true, false);
        var m1 = MatrixWorld.Inverted();
        if (child.Parent is not null)
        {
            child.Parent.UpdateWorldMatrix(true, false);
            m1 = Mat4.Multiply(m1, child.Parent.MatrixWorld);
        }
        child.ApplyMatrix4(m1);
        child.RemoveFromParent();
        child.Parent = this;
        Children.Add(child);
        child.UpdateWorldMatrix(false, true);
    }

    /// <summary>Box3.expandByObject(object)(precise ではない): 自分と子孫のジオメトリの外接箱をワールドへ移して合わせる。updateWorldMatrix(false, false) で、親は更新しない。</summary>
    public void ExpandBox(ref Vec3 min, ref Vec3 max, Func<Obj3D, UnityMesh?> geometryOf)
    {
        UpdateWorldMatrix(false, false);
        if (geometryOf(this) is { } g && g.VertexCount > 0)
        {
            foreach (var corner in Corners(g.BoundsMin, g.BoundsMax))
            {
                var w = MatrixWorld.ApplyToPoint(corner);
                min = new Vec3(Math.Min(min.X, w.X), Math.Min(min.Y, w.Y), Math.Min(min.Z, w.Z));
                max = new Vec3(Math.Max(max.X, w.X), Math.Max(max.Y, w.Y), Math.Max(max.Z, w.Z));
            }
        }
        foreach (var c in Children) c.ExpandBox(ref min, ref max, geometryOf);
    }

    private static IEnumerable<Vec3> Corners(Vec3 a, Vec3 b)
    {
        yield return new Vec3(a.X, a.Y, a.Z);
        yield return new Vec3(a.X, a.Y, b.Z);
        yield return new Vec3(a.X, b.Y, a.Z);
        yield return new Vec3(a.X, b.Y, b.Z);
        yield return new Vec3(b.X, a.Y, a.Z);
        yield return new Vec3(b.X, a.Y, b.Z);
        yield return new Vec3(b.X, b.Y, a.Z);
        yield return new Vec3(b.X, b.Y, b.Z);
    }
}

/// <summary>three.js の Euler・Quaternion・Matrix4.decompose のうち、unity.js が使うもの。</summary>
public static class UnityMath
{
    /// <summary>Quaternion.setFromEuler(euler)(order: XYZ または YXZ)。</summary>
    public static Quat QuatFromEuler(Vec3 e, EulerOrder order)
    {
        double c1 = Math.Cos(e.X / 2), c2 = Math.Cos(e.Y / 2), c3 = Math.Cos(e.Z / 2);
        double s1 = Math.Sin(e.X / 2), s2 = Math.Sin(e.Y / 2), s3 = Math.Sin(e.Z / 2);
        if (order == EulerOrder.YXZ)
            return new Quat(s1 * c2 * c3 + c1 * s2 * s3, c1 * s2 * c3 - s1 * c2 * s3, c1 * c2 * s3 - s1 * s2 * c3, c1 * c2 * c3 + s1 * s2 * s3);
        return new Quat(s1 * c2 * c3 + c1 * s2 * s3, c1 * s2 * c3 - s1 * c2 * s3, c1 * c2 * s3 + s1 * s2 * c3, c1 * c2 * c3 - s1 * s2 * s3);
    }

    /// <summary>Euler.setFromQuaternion(q, order)(makeRotationFromQuaternion → setFromRotationMatrix)。</summary>
    public static Vec3 EulerFromQuat(Quat q, EulerOrder order)
    {
        var m = Mat4.Compose(Vec3.Zero, q, Vec3.One).E;
        double m11 = m[0], m12 = m[4], m13 = m[8], m21 = m[1], m22 = m[5], m23 = m[9], m31 = m[2], m32 = m[6], m33 = m[10];
        double x, y, z;
        if (order == EulerOrder.YXZ)
        {
            x = Math.Asin(-Math.Clamp(m23, -1, 1));
            if (Math.Abs(m23) < 0.9999999) { y = Math.Atan2(m13, m33); z = Math.Atan2(m21, m22); }
            else { y = Math.Atan2(-m31, m11); z = 0; }
        }
        else
        {
            y = Math.Asin(Math.Clamp(m13, -1, 1));
            if (Math.Abs(m13) < 0.9999999) { x = Math.Atan2(-m23, m33); z = Math.Atan2(-m12, m11); }
            else { x = Math.Atan2(m32, m22); z = 0; }
        }
        return new Vec3(x, y, z);
    }

    /// <summary>Matrix4.decompose(position, quaternion, scale)。</summary>
    public static (Vec3 Position, Quat Quaternion, Vec3 Scale) Decompose(Mat4 m)
    {
        var te = m.E;
        var sx = new Vec3(te[0], te[1], te[2]).Length;
        var sy = new Vec3(te[4], te[5], te[6]).Length;
        var sz = new Vec3(te[8], te[9], te[10]).Length;
        if (m.Determinant() < 0) sx = -sx;
        var position = new Vec3(te[12], te[13], te[14]);
        double invSX = 1 / sx, invSY = 1 / sy, invSZ = 1 / sz;
        var x = new Vec3(te[0] * invSX, te[1] * invSX, te[2] * invSX);
        var y = new Vec3(te[4] * invSY, te[5] * invSY, te[6] * invSY);
        var z = new Vec3(te[8] * invSZ, te[9] * invSZ, te[10] * invSZ);
        return (position, Quat.FromRotationBasis(x, y, z), new Vec3(sx, sy, sz));
    }

    /// <summary>Vector3.setFromUnitVectors(from, to) で作るクォータニオン(Quaternion.setFromUnitVectors)。</summary>
    public static Quat FromUnitVectors(Vec3 from, Vec3 to)
    {
        const double eps = 2.220446049250313e-16;   // Number.EPSILON
        var r = from.Dot(to) + 1;
        double x, y, z, w;
        if (r < eps)
        {
            r = 0;
            if (Math.Abs(from.X) > Math.Abs(from.Z)) { x = -from.Y; y = from.X; z = 0; w = r; }
            else { x = 0; y = -from.Z; z = from.Y; w = r; }
        }
        else
        {
            x = from.Y * to.Z - from.Z * to.Y;
            y = from.Z * to.X - from.X * to.Z;
            z = from.X * to.Y - from.Y * to.X;
            w = r;
        }
        var q = new Quat(x, y, z, w);
        var l = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        if (l == 0) return new Quat(0, 0, 0, 1);
        l = 1 / l;
        return new Quat(q.X * l, q.Y * l, q.Z * l, q.W * l);
    }
}
