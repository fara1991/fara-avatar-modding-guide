using FaraAvatarModdingGuide.Web.Services;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>WebGL を使う記事内チュートリアルの登録(Unity と Blender で 1 つ。レビュー M2)。実ブラウザでの確認は tools/verify/practice-mixed.mjs。</summary>
public class ActiveGlViewTests
{
    private sealed class Fake : ITutorialCs
    {
        public int Suspended;
        public TutorialInfo Info => new("x", "x", false, []);
        public TutorialUiState State => new();
        public PracticeHud Hud => new();
        public IUnityCs? Unity => null;
        public void OnUnityStarted(bool ok) { }
        public bool Debug { get; set; }
        public bool Numpad { get; set; }
        public event Action? Changed { add { } remove { } }
        public Task ActivateAsync() => Task.CompletedTask;
        public void Suspend() => Suspended++;
        public void Retry() { }
        public void Restart() { }
        public void Skip() { }
        public void ClickTool(string tool) { }
        public void ChooseMenu(int index) { }
        public void ChangeNPanel(string prop, string axis, string value) { }
        public void Dispose() { }
    }

    [Fact]
    public void Claiming_suspends_the_previous_one_whatever_its_kind()
    {
        var unity = new Fake();
        var blender = new Fake();
        ActiveGlView.Claim(unity);
        ActiveGlView.Claim(blender);
        Assert.Equal(1, unity.Suspended);
        Assert.True(ActiveGlView.IsCurrent(blender));
        Assert.False(ActiveGlView.IsCurrent(unity));
        ActiveGlView.Claim(unity);
        Assert.Equal(1, blender.Suspended);
        ActiveGlView.Release(unity);
        Assert.False(ActiveGlView.IsCurrent(unity));
    }

    [Fact]
    public void Claiming_again_or_releasing_a_stale_one_does_not_suspend_or_clear()
    {
        var a = new Fake();
        var b = new Fake();
        ActiveGlView.Claim(a);
        ActiveGlView.Claim(a);
        Assert.Equal(0, a.Suspended);
        ActiveGlView.Claim(b);
        ActiveGlView.Release(a);   // 手放したものの後始末が、いまのものの登録を消さない
        Assert.True(ActiveGlView.IsCurrent(b));
        ActiveGlView.Release(b);
    }
}
