using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;

namespace FaraAvatarModdingGuide.Web.Layout;

/// <summary>
/// 完了の記録（ProgressStore）で表示が変わる部品の土台。記録が変わったとき（このタブ・ほかのタブ）に、描き直す。
/// 最初の描画の前に記録を読む（保存された完了の印が、最初から出るように）。
/// </summary>
public abstract class ProgressAware : ComponentBase, IDisposable
{
    [Inject] protected ProgressStore Progress { get; set; } = default!;

    protected override void OnInitialized()
    {
        Progress.EnsureLoaded();
        Progress.Changed += OnProgressChanged;
    }

    private void OnProgressChanged() => _ = InvokeAsync(StateHasChanged);

    /// <summary>記事の id が完了なら "is-done"（CSS が完了の印を見せる）、そうでなければ null。</summary>
    protected string? DoneClass(string articleId) => Progress.IsDone(articleId) ? "is-done" : null;

    public virtual void Dispose() => Progress.Changed -= OnProgressChanged;
}
