using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Services;

/// 練習ページ(/practice・/practice/unity・/practice/room)の JavaScript の橋渡し(js/practice-entry.js)。PC の 3D は C#（Blazor）だけ（Phase 3b S7）。
/// JavaScript が受け持つのは、タッチだけの端末の案内（練習の代わりに「画像で見る」を出す）と、記事内チュートリアルの「画像で見る」（tutorial.js）の入口。
/// ページは IAsyncDisposable で DisposeAsync を呼ぶ。始めている途中(import が済んでいない)でも、済むのを待ってから解放する。
/// </summary>
public sealed class PracticeHost(IJSRuntime js) : IAsyncDisposable
{
    private readonly string id = Guid.NewGuid().ToString("N");   // この橋渡しの印(js/practice-entry.js が、自分が始めたものだけを解放するため)
    private IJSObjectReference? module;
    private Task? starting;

    /// <summary>タッチだけの端末の案内（記事内チュートリアルの「画像で見る」）を始める。PC では何もしない（3D は C# 版が始める）。</summary>
    public Task StartGuideAsync() => starting = StartCoreAsync("start", null);

    /// <summary>タッチだけの端末か(C# 版は PC 用。タッチの端末は、これまでの案内の画面にする)。</summary>
    public async Task<bool> IsTouchOnlyAsync()
    {
        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/practice-entry.js");
            return await module.InvokeAsync<bool>("isTouchOnly");
        }
        catch (JSException) { return false; }
        catch (JSDisconnectedException) { return false; }
    }

    /// <summary>記事の本文(selector)の中の記事内チュートリアル(.practice-embed)を始める。PC は 3D ビュー、タッチだけの端末は「画像で見る」。</summary>
    public Task StartArticleAsync(string selector) => starting = StartCoreAsync("startArticle", selector);

    private async Task StartCoreAsync(string function, object? arg)
    {
        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/practice-entry.js");
            await module.InvokeVoidAsync(function, id, arg);
        }
        catch (JSException e)
        {
            // 3D を始められなくても、ページの説明は読める(WebGL が使えない環境のメッセージは、練習のスクリプトが出す)
            await Console.Error.WriteLineAsync($"[avatar] 練習を始められません: {e.Message}");
        }
        catch (JSDisconnectedException) { }   // 回線が切れた（ページを離れた）後の開始は、もう要らない
    }

    /// <summary>3D(WebGL のコンテキスト・イベント・WebSocket・タイマー)を解放する。何度呼んでもよい。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "解放の途中。StartCoreAsync が拾えなかった失敗（通信の切断など）の種類は問わず、解放は続ける")]
    public async ValueTask DisposeAsync()
    {
        if (starting is not null)
        {
            try { await starting; }
            catch (Exception) { /* StartCoreAsync が拾えなかった失敗(通信の切断など)。解放は続ける */ }
            starting = null;
        }
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("dispose", id);
            await module.DisposeAsync();
        }
        // 回線が切れている（ページを離れた後）なら、解放は要らない。画面を閉じる途中の失敗も、解放は続ける
        catch (JSDisconnectedException) { }
        catch (JSException) { }
        module = null;
    }
}
