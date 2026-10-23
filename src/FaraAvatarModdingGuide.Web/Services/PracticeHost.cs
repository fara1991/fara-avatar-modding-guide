using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// 練習ページ(/practice・/practice/unity・/practice/room)の 3D を、JavaScript(Three.js)で動かして、ページを離れるときに必ず解放する橋渡し。
/// js/practice-entry.js を import して、start(PC 用のスクリプト, 引数) と dispose() を呼ぶだけ。3D の中身(viewport.js・unity.js・room.js)は、Blazor は知らない(計画書 4.5)。
/// ページは IAsyncDisposable で DisposeAsync を呼ぶ。始めている途中(import が済んでいない)でも、済むのを待ってから解放する。
/// </summary>
public sealed class PracticeHost(IJSRuntime js) : IAsyncDisposable
{
    private readonly string id = Guid.NewGuid().ToString("N");   // この橋渡しの印(js/practice-entry.js が、自分が始めたものだけを解放するため)
    private IJSObjectReference? module;
    private Task? starting;

    /// <summary>PC 用の練習(pcScript: 例 "practice.js")を始める。タッチだけの端末では、案内を出して、記事内チュートリアルの「画像で見る」を始める。</summary>
    public Task StartAsync(string pcScript, object? options) => starting = StartCoreAsync(pcScript, options);

    /// <summary>タッチだけの端末か(?engine=cs の C# 版は PC 用。タッチの端末は、これまでの案内の画面にする)。</summary>
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
    public Task StartArticleAsync(string selector) => starting = StartCoreAsync(null, selector);

    private async Task StartCoreAsync(string? pcScript, object? options)
    {
        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/practice-entry.js");
            if (pcScript is null) await module.InvokeVoidAsync("startArticle", id, options);
            else await module.InvokeVoidAsync("start", id, pcScript, options);
        }
        catch (JSException e)
        {
            // 3D を始められなくても、ページの説明は読める(WebGL が使えない環境のメッセージは、練習のスクリプトが出す)
            Console.Error.WriteLine($"[avatar] 練習を始められません: {e.Message}");
        }
        catch (JSDisconnectedException) { }
    }

    /// <summary>3D(WebGL のコンテキスト・イベント・WebSocket・タイマー)を解放する。何度呼んでもよい。</summary>
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
        catch (JSDisconnectedException) { }
        catch (JSException) { }
        module = null;
    }
}
