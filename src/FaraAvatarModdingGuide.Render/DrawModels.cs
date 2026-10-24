using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>頂点バッファの 1 頂点あたりの並び(float)。位置だけ(3)・位置+法線(6)・位置+色(6)。gl.js の属性の指定と同じ番号。</summary>
public enum BufferLayout { Position = 0, PositionNormal = 1, PositionColor = 2 }

/// <summary>描き方。gl.js の gl.TRIANGLES / LINES / LINE_STRIP / POINTS。</summary>
public enum Primitive { Triangles = 0, Lines = 1, LineStrip = 2, Points = 3 }

/// <summary>色の付け方。Lit = 半球光 + 平行光(MeshStandardMaterial の見た目)、Flat = 1 色、VertexColor = 頂点の色、Dashed = 1 色の点線(X 方向に作った線。0.3 描いて 0.2 空ける。案内の軸の線)。</summary>
public enum Shading { Lit = 0, Flat = 1, VertexColor = 2, Dashed = 3 }

/// <summary>描くときの GL の状態。</summary>
[Flags]
public enum DrawFlags
{
    None = 0,
    /// <summary>深度テストをする(しないと手前の面に隠れない。カーソルや透過表示の点・線)。</summary>
    DepthTest = 1,
    /// <summary>深度を書く(透過表示の面は書かない)。</summary>
    DepthWrite = 2,
    /// <summary>アルファブレンド(SRC_ALPHA, ONE_MINUS_SRC_ALPHA)。</summary>
    Blend = 4,
    /// <summary>ワールド行列が鏡映(行列式が負)なので、表の向きを時計回りにする(three.js の frontFaceCW)。</summary>
    FrontFaceCw = 8,
    /// <summary>polygonOffset を使う。</summary>
    PolygonOffset = 16,
    /// <summary>透明で両面の材質は、裏の面 → 表の面の 2 回に分けて描く(three.js が `transparent && side === DoubleSide` で行う。半透明の重なりの順が、三角形の並び順ではなく、裏が先・表があとになる)。</summary>
    TwoPass = 32,
}

/// <summary>GPU に送る頂点のバッファ 1 つ。Data を差し替えるたびに <see cref="Version"/> が増え、描画層は増えたときだけ送り直す。</summary>
public sealed class GpuBuffer
{
    private static int nextId = 1;

    public GpuBuffer(BufferLayout layout)
    {
        Layout = layout;
        Id = Interlocked.Increment(ref nextId) - 1;
    }

    public int Id { get; }
    public BufferLayout Layout { get; }
    public float[] Data { get; private set; } = [];
    public int Version { get; private set; }

    /// <summary>1 頂点の float の数。</summary>
    public int Stride => Layout == BufferLayout.Position ? 3 : 6;

    public int VertexCount => Data.Length / Stride;

    public void Set(float[] data)
    {
        if (data.Length % Stride != 0) throw new ArgumentException($"頂点の数が合いません(float {data.Length} 個、1 頂点 {Stride} 個)", nameof(data));
        Data = data;
        Version++;
    }
}

/// <summary>描画 1 回。色は sRGB(0〜1)。Lit のときは基本色、Flat のときは線・面の色(VertexColor では使わない)。</summary>
public sealed record DrawItem(
    GpuBuffer Buffer, Primitive Primitive, Shading Shading, Mat4 World, float R, float G, float B, float Opacity, DrawFlags Flags,
    float PolygonOffsetFactor, float PolygonOffsetUnits, float PointSize, int RenderOrder, bool Transparent, string Label);

/// <summary>1 フレームぶんの描画の命令。<see cref="Items"/> は描く順(不透明が先、透明は奥から)。<see cref="Uploads"/> は、前回から変わって送り直すバッファ、<see cref="Releases"/> は不要になったバッファの番号。</summary>
public sealed class Frame
{
    /// <summary>射影 × ビュー(float。列順)。描く順の計算や確認に使う(GPU へは <see cref="Projection"/> と、描画ごとの ModelView を送る)。</summary>
    public required float[] ViewProjection { get; init; }
    /// <summary>射影(float。列順)。</summary>
    public required float[] Projection { get; init; }
    /// <summary>ビュー(カメラのワールド行列の逆。double のまま。描画ごとの ModelView = View × World を double で計算するため)。</summary>
    public required Mat4 View { get; init; }
    /// <summary>カメラのワールド座標(鏡面反射の視線。three.js は -modelViewPosition から求める)。</summary>
    public required Vec3 CameraPosition { get; init; }
    public required IReadOnlyList<DrawItem> Items { get; init; }
    public required IReadOnlyList<GpuBuffer> Uploads { get; init; }
    public required IReadOnlyList<int> Releases { get; init; }
}
