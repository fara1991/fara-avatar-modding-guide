using FaraSiteKit.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>起動の前後の後始末（読み込みの失敗の表示・保存が使えないときの既定のテーマ）。</summary>
public static class StartupError
{
    /// <summary>
    /// 起動できなかったことを、画面下のエラー表示（#blazor-error-ui）で知らせる。静的 HTML の本文は残る。
    /// js/startup.js の avatarStartup.showError が表示を出す（インラインの script は CSP で使えないので、外部ファイル）。
    /// </summary>
    public static async Task ShowAsync(IJSRuntime js)
    {
        try { await js.InvokeVoidAsync("avatarStartup.showError"); }
        catch (JSException) { /* 表示できなくても、読み込み表示は 60 秒で外れる */ }
    }

    /// <summary>
    /// 保存されたテーマを読めない・まだ無いとき（localStorage が使えない・保存が失敗したとき）は、SSR 版の既定のダークにする。
    /// kit は、読めないときは light にするので、読み込みの前に dark に決めておく（SiteThemeState は、決めたあとは保存を読み直さない）。
    /// 描画の前の属性は js/theme-default.js が同じ規則で付ける。
    /// </summary>
    public static async Task ApplyDefaultThemeAsync(IServiceProvider services)
    {
        var js = services.GetRequiredService<IJSRuntime>();
        if (js is not IJSInProcessRuntime sync) return;
        if (HasSavedTheme(sync)) return;
        await services.GetRequiredService<SiteThemeState>().SetAsync(SiteThemeState.Dark);
    }

    /// <summary>テーマの保存値が読める（空でない）か。読めない（例外）・無いときは false。</summary>
    private static bool HasSavedTheme(IJSInProcessRuntime sync)
    {
        try { return !string.IsNullOrEmpty(sync.Invoke<string?>("localStorage.getItem", SiteInfo.StorageKey)); }
        catch (JSException) { return false; }
    }
}
