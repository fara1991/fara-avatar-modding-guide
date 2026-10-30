using System.Runtime.InteropServices.JavaScript;

namespace FaraAvatarModdingGuide.RenderLab;

/// <summary>wwwroot/js/gl.js(薄い WebGL 2 層)と、レンダーラボ専用の gl-lab.js(計測・確認用。Phase 3b S5 で gl.js から分けた)の呼び出し。バッファの中身は変わったときだけ送り、1 フレームの描画の命令は 1 回の呼び出しで渡す。</summary>
internal static partial class Gl
{
    [JSImport("start", "gl")]
    [return: JSMarshalAs<JSType.Boolean>]
    internal static partial bool Start(
        string rootId,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> frame,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number, JSType.Boolean>>] Func<int, double, double, bool> pointerDown,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, double, int> pointerMove,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> pointerUp,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Boolean>>] Func<double, bool> wheel,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.String, JSType.Number, JSType.Boolean>>] Func<string, string, int, bool> key,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> keyup,
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> focus);

    [JSImport("uploadBuffer", "gl")]
    internal static partial void UploadBuffer(int id, int layout, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("releaseBuffer", "gl")]
    internal static partial void ReleaseBuffer(int id);

    [JSImport("draw", "gl")]
    internal static partial void Draw([JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("dispose", "gl")]
    internal static partial void Dispose();

    [JSImport("report", "gl-lab")]
    internal static partial void Report(string name, string json);

    [JSImport("exposeControl", "gl-lab")]
    internal static partial void ExposeControl([JSMarshalAs<JSType.Function<JSType.String, JSType.Number, JSType.Number>>] Func<string, int, double> control);

    /// <summary>起動の区間を測るための performance.mark(gl.js を読む前でも使える)。</summary>
    [JSImport("globalThis.performance.mark")]
    internal static partial void Mark(string name);

    [JSImport("exposeDebugGlobals", "gl-lab")]
    internal static partial void ExposeDebugGlobals();

    [JSImport("getQuery", "gl-lab")]
    internal static partial string GetQuery();
}
