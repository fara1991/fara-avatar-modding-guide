using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Render;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>wwwroot/js/gl.js(薄い WebGL 2 層)の呼び出し。バッファの中身は変わったときだけ送り、1 フレームの描画の命令は 1 回の呼び出しで渡す。</summary>
internal static partial class PracticeGl
{
    [JSImport("start", "gl")]
    [return: JSMarshalAs<JSType.Boolean>]
    internal static partial bool Start(
        string rootId,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, double, double> frame,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number, JSType.Boolean>>] Func<int, double, double, bool> pointerDown,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, double, int> pointerMove,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> pointerUp,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Boolean>>] Func<double, bool> wheel,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.String, JSType.Number, JSType.Boolean>>] Func<string, string, int, bool> key,
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> keyup,
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> focus,
        string canvasHostId);

    [JSImport("uploadBuffer", "gl")]
    internal static partial void UploadBuffer(int id, int layout, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("releaseBuffer", "gl")]
    internal static partial void ReleaseBuffer(int id);

    [JSImport("draw", "gl")]
    internal static partial void Draw([JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("dispose", "gl")]
    internal static partial void Dispose();
}

/// <summary>js/practice-cs.js(確認用のデバッグ口とフォーカス)の呼び出し。</summary>
internal static partial class PracticeCsJs
{
    /// <summary>performance.now()(ms)。案内の点線の点滅は、JS 版と同じ時計で決める(確認では、この時計を止めて、画素を比べる)。</summary>
    [JSImport("globalThis.performance.now")]
    internal static partial double Now();

    [JSImport("focusRoot", "practice-cs")]
    internal static partial void FocusRoot(string rootId);

    /// <summary>?debug=1 のとき、window.__practice(練習の状態。tools/verify/practice.mjs が読む)を作る。select は、オブジェクトの番号を渡して選ばせる関数。</summary>
    [JSImport("exposeDebug", "practice-cs")]
    internal static partial void ExposeDebug([JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> select);

    [JSImport("setDebugState", "practice-cs")]
    internal static partial void SetDebugState(string json);

    [JSImport("clearDebug", "practice-cs")]
    internal static partial void ClearDebug();
}

/// <summary>js/practice-cs.js の、記事内チュートリアル用の関数。</summary>
internal static partial class PracticeCsJsTutorial
{
    /// <summary>完了を記録して(localStorage)、ページ内のチュートリアルがすべて終わっていればページを完了にし、完了の画面に出す文を返す。</summary>
    [JSImport("finishTutorial", "practice-cs")]
    internal static partial string Finish(string name);
}
