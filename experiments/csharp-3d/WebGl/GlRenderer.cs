using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Csharp3d.Shared;

namespace Csharp3d.WebGl;

/// <summary>JS の gl.js（WebGL 2 の薄い橋渡し）を呼ぶ。JSImport は 1 呼び出しごとに JS 境界を越える</summary>
internal static partial class Gl
{
    [JSImport("globalThis.glBridge.init")]
    internal static partial bool Init(string canvasId, string vs, string fs);

    [JSImport("globalThis.glBridge.uploadVertices")]
    internal static partial void UploadVertices(byte[] bytes);

    [JSImport("globalThis.glBridge.setViewport")]
    internal static partial void SetViewport(int w, int h);

    [JSImport("globalThis.glBridge.clear")]
    internal static partial void Clear(double r, double g, double b);

    [JSImport("globalThis.glBridge.uniformMat4")]
    internal static partial void UniformMat4(string name, byte[] bytes);

    [JSImport("globalThis.glBridge.uniformVec3")]
    internal static partial void UniformVec3(string name, double x, double y, double z);

    [JSImport("globalThis.glBridge.uniformInt")]
    internal static partial void UniformInt(string name, int v);

    [JSImport("globalThis.glBridge.drawRange")]
    internal static partial void DrawRange(int mode, int first, int count);

    [JSImport("globalThis.glBridge.canvasWidth")]
    internal static partial int CanvasWidth(string canvasId);

    [JSImport("globalThis.glBridge.canvasHeight")]
    internal static partial int CanvasHeight(string canvasId);
}

/// <summary>
/// WebGL 2 の描画。シェーダーと行列の並び・ライトはここで書く（平面シェーディング、半球光 + 平行光源の近似）。
/// Practice3D.Draws() の順に、ワールド行列と「線かどうか」だけを送る。
/// </summary>
public sealed class GlRenderer
{
    const string CanvasId = "theCanvas";

    // 行ベクトル規約（v * M）の行列は、メモリ上の並びが WebGL の列優先の転置になるので、そのまま渡せば v * M と同じ結果になる
    const string VertexShader = @"#version 300 es
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec3 aColor;
uniform mat4 uWorld;
uniform mat4 uViewProj;
out vec3 vNormal;
out vec3 vColor;
void main() {
    vec4 wp = uWorld * vec4(aPos, 1.0);
    gl_Position = uViewProj * wp;
    vNormal = mat3(uWorld) * aNormal;
    vColor = aColor;
}";

    const string FragmentShader = @"#version 300 es
precision highp float;
in vec3 vNormal;
in vec3 vColor;
uniform int uLit;
uniform vec3 uLightDir;
out vec4 fragColor;
void main() {
    if (uLit == 0) {
        fragColor = vec4(vColor, 1.0);
        return;
    }
    // Three.js の既定に合わせる: 色は sRGB → 線形、Lambert は albedo / PI、出力は線形 → sRGB
    // 半球光（空 1.1 / 地面 0x444444 * 1.1。上方向は Three.js の既定の +Y）と平行光源（強さ 1.2）
    vec3 n = normalize(vNormal);
    vec3 albedo = pow(vColor, vec3(2.2));
    float sky = n.y * 0.5 + 0.5;
    vec3 hemi = mix(vec3(0.0572), vec3(1.0), sky) * 1.1;
    float key = max(dot(n, uLightDir), 0.0) * 1.2;
    vec3 lin = albedo / 3.14159265 * (hemi + vec3(key));
    fragColor = vec4(pow(lin, vec3(1.0 / 2.2)), 1.0);
}";

    readonly Practice3D core;
    readonly byte[] matBytes = new byte[64];
    readonly Stopwatch frameWatch = new();
    int lastW = -1, lastH = -1;

    /// <summary>計測用: Frame の CPU 時間（ms）の合計と回数</summary>
    public double FrameMsTotal { get; private set; }
    public int FrameCount { get; private set; }
    public bool Ready { get; private set; }

    public GlRenderer(Practice3D core)
    {
        this.core = core;
    }

    public bool Init()
    {
        if (!Gl.Init(CanvasId, VertexShader, FragmentShader)) return false;
        Gl.UploadVertices(PackVertices(Geometry.Vertices));
        Ready = true;
        return true;
    }

    /// <summary>1 頂点 9 個の float（位置・法線・色）を、バイト列にする</summary>
    static byte[] PackVertices(Vertex[] verts)
    {
        var floats = new float[verts.Length * 9];
        for (int i = 0; i < verts.Length; i++)
        {
            var v = verts[i];
            int o = i * 9;
            floats[o + 0] = v.P.X; floats[o + 1] = v.P.Y; floats[o + 2] = v.P.Z;
            floats[o + 3] = v.N.X; floats[o + 4] = v.N.Y; floats[o + 5] = v.N.Z;
            floats[o + 6] = v.C.X; floats[o + 7] = v.C.Y; floats[o + 8] = v.C.Z;
        }
        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }

    void SetMatrix(string name, Matrix4x4 m)
    {
        var src = m;
        MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in src)).CopyTo(matBytes);
        Gl.UniformMat4(name, matBytes);
    }

    static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(4, -6, 8));

    /// <summary>1 フレーム描く（JS の rAF から呼ばれる）</summary>
    public void Frame()
    {
        if (!Ready) return;
        frameWatch.Restart();

        int w = Gl.CanvasWidth(CanvasId), h = Gl.CanvasHeight(CanvasId);
        if (w != lastW || h != lastH)
        {
            Gl.SetViewport(w, h);
            lastW = w;
            lastH = h;
        }
        core.Resize(w, h);

        Gl.Clear(0x3d / 255.0, 0x3d / 255.0, 0x3d / 255.0);
        SetMatrix("uViewProj", core.View * core.Proj);
        Gl.UniformVec3("uLightDir", LightDir.X, LightDir.Y, LightDir.Z);
        foreach (var d in core.Draws())
        {
            var (start, count) = Geometry.Ranges[d.Mesh];
            bool lines = Geometry.IsLines(d.Mesh);
            Gl.UniformInt("uLit", lines ? 0 : 1);
            SetMatrix("uWorld", d.World);
            Gl.DrawRange(lines ? 1 : 0, start, count);
        }

        frameWatch.Stop();
        FrameMsTotal += frameWatch.Elapsed.TotalMilliseconds;
        FrameCount++;
    }
}
