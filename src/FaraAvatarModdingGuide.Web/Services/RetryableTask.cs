namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// 「1 回だけ」の非同期の準備(JS モジュールの読み込みなど)。成功したら、結果の Task を使い回す。失敗したら捨てて、次に呼ばれたときにやり直す
/// (失敗した Task を static に持ち続けると、通信の一時的な失敗のあと、ページを開き直すまで二度と読み込めない。<see cref="PracticeCsAssemblies"/> と同じ決まり)。
/// </summary>
internal sealed class RetryableTask
{
    private Task? task;

    public async Task RunAsync(Func<Task> start)
    {
        var t = task ??= start();
        try
        {
            await t;
        }
        catch
        {
            if (ReferenceEquals(task, t)) task = null;
            throw;
        }
    }
}
