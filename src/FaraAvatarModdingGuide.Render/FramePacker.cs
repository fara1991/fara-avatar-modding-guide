using System.Runtime.InteropServices;

namespace FaraAvatarModdingGuide.Render;

/// <summary>
/// <see cref="Frame"/> を、JavaScript(gl.js)に 1 回の呼び出しで渡す float の列にする。並びは gl.js の draw() と同じ:
///   先頭 24 個 = [0..15] 射影×ビュー  [16..18] カメラのワールド座標  [19] 描画の数  [20..23] 予備
///   続いて 描画 1 つあたり 28 個 = [0] バッファの番号  [1] Primitive  [2] Shading  [3] DrawFlags  [4] polygonOffset の factor  [5] units  [6] 不透明度
///   [7] 点の大きさ(CSS px)  [8..10] 色(sRGB)  [11] 予備  [12..27] ワールド行列(列順)
/// バッファの中身(頂点)は、変わったときだけ <see cref="GpuBuffer"/> ごとに別に送る(<see cref="Frame.Uploads"/>)。
/// </summary>
public static class FramePacker
{
    public const int HeaderFloats = 24;
    public const int ItemFloats = 28;

    public static float[] Pack(Frame frame)
    {
        var data = new float[HeaderFloats + frame.Items.Count * ItemFloats];
        Pack(frame, data);
        return data;
    }

    /// <summary>data に書く(大きさは <see cref="LengthOf"/> 以上)。毎フレーム同じ配列を使い回すため。</summary>
    public static void Pack(Frame frame, float[] data)
    {
        Array.Copy(frame.ViewProjection, data, 16);
        data[16] = (float)frame.CameraPosition.X; data[17] = (float)frame.CameraPosition.Y; data[18] = (float)frame.CameraPosition.Z;
        data[19] = frame.Items.Count;
        data[20] = data[21] = data[22] = data[23] = 0;
        var o = HeaderFloats;
        foreach (var item in frame.Items)
        {
            data[o] = item.Buffer.Id;
            data[o + 1] = (int)item.Primitive;
            data[o + 2] = (int)item.Shading;
            data[o + 3] = (int)item.Flags;
            data[o + 4] = item.PolygonOffsetFactor;
            data[o + 5] = item.PolygonOffsetUnits;
            data[o + 6] = item.Opacity;
            data[o + 7] = item.PointSize;
            data[o + 8] = item.R; data[o + 9] = item.G; data[o + 10] = item.B;
            data[o + 11] = 0;
            for (var i = 0; i < 16; i++) data[o + 12 + i] = (float)item.World.E[i];
            o += ItemFloats;
        }
    }

    public static int LengthOf(Frame frame) => HeaderFloats + frame.Items.Count * ItemFloats;

    /// <summary>float の列をバイト列として見る(JS に渡すため)。</summary>
    public static ReadOnlySpan<byte> AsBytes(ReadOnlySpan<float> data) => MemoryMarshal.AsBytes(data);
}
