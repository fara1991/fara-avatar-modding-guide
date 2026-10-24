using System.Runtime.InteropServices;
using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>
/// <see cref="Frame"/> を、JavaScript(gl.js)に 1 回の呼び出しで渡す float の列にする。並びは gl.js の draw() と同じ:
///   先頭 32 個 = [0..15] 射影  [16..18] カメラのワールド座標  [19] 描画の数  [20] 背景(0 = 単色、1 = 空)  [21..23] 平行光の向き(ビュー空間。正規化)
///     [24] 半球光の下の色(灰色の濃さ 0〜1)  [25] 半球光の強さ  [26] 平行光の強さ  [27] 粗さ  [28..30] 半球光の上の向き(ビュー空間。正規化)  [31] 予備
///   続いて 描画 1 つあたり 28 個 = [0] バッファの番号  [1] Primitive  [2] Shading  [3] DrawFlags  [4] polygonOffset の factor  [5] units  [6] 不透明度
///   [7] 点の大きさ(CSS px)  [8..10] 色(sRGB)  [11] 予備  [12..27] ワールド行列(列順)  [28..43] ModelView = ビュー × ワールド(double で掛けてから float。three.js の modelViewMatrix と同じ。
///   gl_Position は three.js と同じ「射影 × ModelView」で計算する。ワールド行列と射影×ビューに分けると、同じ平面の面どうしの深度の丸めが three.js とずれ、重なった面のちらつき方が変わる)
/// バッファの中身(頂点)は、変わったときだけ <see cref="GpuBuffer"/> ごとに別に送る(<see cref="Frame.Uploads"/>)。
/// </summary>
public static class FramePacker
{
    public const int HeaderFloats = 32;
    public const int ItemFloats = 53;

    public static float[] Pack(Frame frame)
    {
        var data = new float[HeaderFloats + frame.Items.Count * ItemFloats];
        Pack(frame, data);
        return data;
    }

    /// <summary>data に書く(大きさは <see cref="LengthOf"/> 以上)。毎フレーム同じ配列を使い回すため。</summary>
    public static void Pack(Frame frame, float[] data)
    {
        Array.Copy(frame.Projection, data, 16);
        data[16] = (float)frame.CameraPosition.X; data[17] = (float)frame.CameraPosition.Y; data[18] = (float)frame.CameraPosition.Z;
        data[19] = frame.Items.Count;
        data[20] = (float)frame.Background;
        // 光の向きは、three.js と同じに、ビュー空間(transformDirection(viewMatrix))で渡す
        var light = frame.View.TransformDirection(frame.Lighting.LightDirection);
        var hemi = frame.View.TransformDirection(new Vec3(0, 1, 0));
        data[21] = (float)light.X; data[22] = (float)light.Y; data[23] = (float)light.Z;
        data[24] = frame.Lighting.GroundGray; data[25] = frame.Lighting.HemiIntensity; data[26] = frame.Lighting.DirectionalIntensity; data[27] = frame.Lighting.Roughness;
        data[28] = (float)hemi.X; data[29] = (float)hemi.Y; data[30] = (float)hemi.Z; data[31] = 0;
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
            var modelView = Mat4.Multiply(frame.View, item.World);
            for (var i = 0; i < 16; i++) data[o + 28 + i] = (float)modelView.E[i];
            var normal = modelView.NormalMatrix();
            for (var i = 0; i < 9; i++) data[o + 44 + i] = (float)normal[i];
            o += ItemFloats;
        }
    }

    public static int LengthOf(Frame frame) => HeaderFloats + frame.Items.Count * ItemFloats;

    /// <summary>float の列をバイト列として見る(JS に渡すため)。</summary>
    public static ReadOnlySpan<byte> AsBytes(ReadOnlySpan<float> data) => MemoryMarshal.AsBytes(data);
}
