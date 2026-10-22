using System.Numerics;

namespace Csharp3d.Shared;

public enum ObjectKind { Cube, Sphere }

/// <summary>シーンのオブジェクト（位置・Z 軸回転（度）・拡大。viewport.js の mesh.position / rotation.z / scale に当たる）</summary>
public sealed class SceneObject
{
    public required string Name { get; init; }
    public required ObjectKind Kind { get; init; }
    public Vector3 Position = Vector3.Zero;
    public float RotationZDeg;
    public Vector3 Scale = Vector3.One;
    public bool Selected;

    public Geometry.Mesh Mesh => Kind == ObjectKind.Cube ? Geometry.Mesh.Cube : Geometry.Mesh.Sphere;

    /// <summary>ワールド行列（行ベクトル: S * R * T）</summary>
    public Matrix4x4 World()
    {
        return Matrix4x4.CreateScale(Scale)
             * Matrix4x4.CreateRotationZ(RotationZDeg * MathF.PI / 180f)
             * Matrix4x4.CreateTranslation(Position);
    }
}

public sealed class Scene
{
    public readonly List<SceneObject> Objects = new();

    public SceneObject AddCube()
    {
        var o = new SceneObject { Name = UniqueName("Cube"), Kind = ObjectKind.Cube };
        Objects.Add(o);
        return o;
    }

    public void Clear() => Objects.Clear();

    string UniqueName(string baseName)
    {
        for (int i = 1; ; i++)
        {
            var name = i == 1 ? baseName : $"{baseName}.{i - 1:000}";
            if (!Objects.Any(o => o.Name == name)) return name;
        }
    }

    /// <summary>レイに当たった最も手前のオブジェクト（ローカル空間で三角形と判定し、ワールドでの距離で比べる）</summary>
    public SceneObject? Pick(Picking.Ray ray)
    {
        SceneObject? best = null;
        float bestDist = float.MaxValue;
        foreach (var o in Objects)
        {
            if (!Matrix4x4.Invert(o.World(), out var inv)) continue;
            // レイをローカルへ。方向は平行移動を含めない（TransformNormal）
            var lo = Vector3.Transform(ray.Origin, inv);
            var ld = Vector3.TransformNormal(ray.Dir, inv);
            var localRay = new Picking.Ray(lo, ld);
            var (start, count) = Geometry.Ranges[o.Mesh];
            var verts = Geometry.Vertices;
            for (int i = start; i + 2 < start + count; i += 3)
            {
                if (!Picking.RayTriangle(localRay, verts[i].P, verts[i + 1].P, verts[i + 2].P, out float t)) continue;
                var hitWorld = Vector3.Transform(lo + ld * t, o.World());
                float d = Vector3.Distance(hitWorld, ray.Origin);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = o;
                }
            }
        }
        return best;
    }
}
