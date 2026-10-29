using System.Runtime.InteropServices.JavaScript;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>JavaScript のモジュールを読み込む入口。JSHost はブラウザ（WebAssembly）専用なので、ここでブラウザかを確かめてから呼ぶ（CA1416）。ブラウザ以外（テスト）では呼ばれない。</summary>
internal static class BrowserJs
{
    public static Task ImportAsync(string moduleName, string modulePath)
    {
        if (!OperatingSystem.IsBrowser()) throw new PlatformNotSupportedException("JavaScript のモジュールはブラウザでだけ読み込めます");
        return JSHost.ImportAsync(moduleName, modulePath);
    }
}
