using System.Numerics;

namespace Csharp3d.Shared;

/// <summary>
/// 軌道カメラ（viewport.js の view と同じ式）。Z が上。theta は Z 軸まわり、phi は Z 軸からの角度。
/// 行列は System.Numerics の行ベクトル規約（v * M）。投影は WebGL の深度範囲 -1〜1 に合わせる（System.Numerics の
/// CreatePerspectiveFieldOfView は 0〜1 なので使わない）。
/// </summary>
public sealed class OrbitCamera
{
    public Vector3 Target = Vector3.Zero;
    public float Theta = -MathF.PI / 4 - MathF.PI / 2; // 右前上からの既定視点（DEFAULT_VIEW）
    public float Phi = 63f * MathF.PI / 180f;
    public float Dist = 11f;
    public const float FovY = 50f * MathF.PI / 180f;
    public const float Near = 0.1f;
    public const float Far = 500f;

    public Vector3 Eye
    {
        get
        {
            var dir = new Vector3(MathF.Sin(Phi) * MathF.Cos(Theta), MathF.Sin(Phi) * MathF.Sin(Theta), MathF.Cos(Phi));
            return Target + dir * Dist;
        }
    }

    public Matrix4x4 View()
    {
        // 真上・真下から見るときは up が視線と平行になるので、画面の上を +Y にする
        var up = Phi < 0.02f || Phi > MathF.PI - 0.02f ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
        return Matrix4x4.CreateLookAt(Eye, Target, up);
    }

    public static Matrix4x4 Projection(float aspect)
    {
        float f = 1f / MathF.Tan(FovY / 2);
        float a = (Far + Near) / (Near - Far);
        float b = 2 * Far * Near / (Near - Far);
        // 行ベクトル: 列 4 の (0,0,-1,0) で w = -z になる
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, a, -1,
            0, 0, b, 0);
    }

    public void Orbit(float dTheta, float dPhi)
    {
        Theta += dTheta;
        Phi = Math.Clamp(Phi + dPhi, 0.01f, MathF.PI - 0.01f);
    }

    /// <summary>平行移動（画面の右・上方向に動かす）。aspect と FovY から 1 ピクセルあたりの m を出す</summary>
    public void Pan(float dxPx, float dyPx, float viewportHeight)
    {
        var eye = Eye;
        var forward = Vector3.Normalize(Target - eye);
        var up = Phi < 0.02f || Phi > MathF.PI - 0.02f ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
        var right = Vector3.Normalize(Vector3.Cross(forward, up));
        var camUp = Vector3.Normalize(Vector3.Cross(right, forward));
        float wpp = 2 * Dist * MathF.Tan(FovY / 2) / viewportHeight;
        Target += (-right * dxPx + camUp * dyPx) * wpp;
    }

    public void Zoom(float factor)
    {
        Dist = Math.Clamp(Dist * factor, 0.5f, 200f);
    }
}

/// <summary>ピッキング・平面との交差・画面からのレイ（viewport.js の pick / unprojectOnPlane と同じ考え方）</summary>
public static class Picking
{
    public readonly struct Ray
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Dir;
        public Ray(Vector3 o, Vector3 d) { Origin = o; Dir = d; }
    }

    /// <summary>ピクセル座標（左上が原点）から、ワールドのレイを作る</summary>
    public static Ray ScreenRay(float px, float py, float width, float height, Matrix4x4 view, Matrix4x4 proj)
    {
        Matrix4x4.Invert(view * proj, out var invVp);
        float nx = px / width * 2 - 1;
        float ny = 1 - py / height * 2;
        var near = Unproject(new Vector3(nx, ny, -1), invVp);
        var far = Unproject(new Vector3(nx, ny, 1), invVp);
        return new Ray(near, Vector3.Normalize(far - near));
    }

    static Vector3 Unproject(Vector3 ndc, Matrix4x4 invVp)
    {
        var v = Vector4.Transform(new Vector4(ndc, 1), invVp);
        return new Vector3(v.X, v.Y, v.Z) / v.W;
    }

    /// <summary>Möller–Trumbore。ヒットしたら距離 t（レイの Dir が単位ベクトルなら m）</summary>
    public static bool RayTriangle(Ray r, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(r.Dir, e2);
        float det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-7f) return false;
        float inv = 1f / det;
        var s = r.Origin - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(r.Dir, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 1e-4f;
    }

    /// <summary>平面（pivot を通り normal に垂直）とレイの交点。平行なら pivot を返す</summary>
    public static Vector3 RayPlane(Ray r, Vector3 pivot, Vector3 normal)
    {
        float denom = Vector3.Dot(normal, r.Dir);
        if (MathF.Abs(denom) < 1e-6f) return pivot;
        float t = Vector3.Dot(normal, pivot - r.Origin) / denom;
        return r.Origin + r.Dir * t;
    }
}
