using FaraAvatarModdingGuide.Web.Services;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>「1 回だけ」の読み込み(gl.js・practice-cs.js・unity-ui.js)。失敗した Task を持ち続けず、次に呼ばれたときにやり直す(レビュー L5)。</summary>
public class RetryableTaskTests
{
    [Fact]
    public async Task A_successful_start_runs_once()
    {
        var once = new RetryableTask();
        var calls = 0;
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_failed_start_is_retried_on_the_next_call()
    {
        var once = new RetryableTask();
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => once.RunAsync(() => { calls++; return Task.FromException(new InvalidOperationException("通信の失敗")); }));
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task A_start_that_throws_synchronously_is_not_cached()
    {
        var once = new RetryableTask();
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => once.RunAsync(() => { calls++; throw new InvalidOperationException("同期の失敗"); }));
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        Assert.Equal(2, calls);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "TaskCompletionSource で「まだ終わっていない開始」をテストが自分で操作する。待つのは自分で作った Task（デッドロックの心配は無い）")]
    public async Task Callers_during_a_pending_start_share_it_and_fail_together()
    {
        var once = new RetryableTask();
        var calls = 0;
        var gate = new TaskCompletionSource();
        var a = once.RunAsync(() => { calls++; return gate.Task; });
        var b = once.RunAsync(() => { calls++; return gate.Task; });
        gate.SetException(new InvalidOperationException("失敗"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => a);
        await Assert.ThrowsAsync<InvalidOperationException>(() => b);
        Assert.Equal(1, calls);
        await once.RunAsync(() => { calls++; return Task.CompletedTask; });
        Assert.Equal(2, calls);
    }
}
