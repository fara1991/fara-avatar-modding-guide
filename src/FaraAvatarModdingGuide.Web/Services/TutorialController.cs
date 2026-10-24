using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>記事内チュートリアルの 3D ビュー(WebGL + HUD)。シナリオの最初のシーン・視点で作り、ホイールはフォーカス中だけ効く(記事のスクロールを邪魔しない)。</summary>
internal sealed class TutorialViewEngine : CsViewEngine
{
    private readonly TutorialScenario scenario;
    private readonly Action<ViewEvent> onEvent;

    public TutorialViewEngine(string rootId, string canvasHostId, Func<bool> emulateNumpad, bool debug, TutorialScenario scenario, Action<ViewEvent> onEvent)
        : base(rootId, canvasHostId, emulateNumpad, debug)
    {
        this.scenario = scenario;
        this.onEvent = onEvent;
    }

    protected override ViewportOptions CreateOptions() => new()
    {
        Scene = scenario.Scene,
        ViewDist = scenario.ViewDist,
        ViewTarget = scenario.ViewTarget,
        EmulateNumpad = EmulateNumpadFn,
        WheelNeedsFocus = true,
    };

    protected override void OnCoreCreated(ViewportCore core) => core.EventEmitted += onEvent;

    /// <summary>画面の外で状態を変えたあと(再生・案内の変更)に、HUD を描き直す。</summary>
    public void Refresh() => Invalidate();
}

/// <summary>
/// 記事内チュートリアル 1 本(tutorial.js の Tutorial クラスに当たる)。手順の進行(<see cref="TutorialSession"/>)・メッセージ・「できました」・キーの光り・完了の保存を持ち、
/// 3D ビュー(<see cref="TutorialViewEngine"/>)は、画面に入ったとき・クリックされたときに始め、別のチュートリアルが始まったら手放す(WebGL を使うのはページ内で 1 つだけ)。
/// 画面(<c>TutorialEmbed.razor</c>)は <see cref="State"/>・<see cref="Hud"/> を読んで描く。
/// </summary>
internal sealed class TutorialController : ITutorialCs
{
    private static TutorialController? active;

    private readonly TutorialScenario scenario;
    private readonly string rootId;
    private readonly string canvasHostId;
    private readonly TutorialSession session;
    private TutorialViewEngine? engine;
    private bool disposed;
    private bool loading;
    private bool started;
    private bool viewFailed;
    private string? message;
    private string messageKind = "";
    private int okFlash;
    private readonly HashSet<string> pressed = [];
    private string completeText = "";
    private int failToken;
    private int msgToken;

    private TutorialController(TutorialScenario scenario, string rootId, string canvasHostId)
    {
        this.scenario = scenario;
        this.rootId = rootId;
        this.canvasHostId = canvasHostId;
        session = new TutorialSession(scenario);
        Info = new TutorialInfo(scenario.Name, scenario.Title, scenario.Numpad, [.. scenario.Steps.Select(s => new TutorialStepInfo(s.Text, s.Keys, s.Mouse))]);
        State = BuildState();
    }

    public static ITutorialCs? Create(string name, string rootId, string canvasHostId, string imageBase) =>
        TutorialScenarios.All.TryGetValue(name, out var scenario) ? new TutorialController(scenario, rootId, canvasHostId) : null;

    public TutorialInfo Info { get; }
    public TutorialUiState State { get; private set; }
    public PracticeHud Hud => engine?.Hud ?? new PracticeHud();
    public IUnityCs? Unity => null;
    public void OnUnityStarted(bool ok) { }
    public bool Numpad { get; set; }
    public event Action? Changed;

    /// <summary>?debug=1 のときだけ、確認用の状態を出す(window.__tutorials)。</summary>
    public bool Debug { get; set; }

    // ---------------------------------------------------------------- 画面に出す状態
    private TutorialUiState BuildState()
    {
        var steps = scenario.Steps;
        var n = steps.Count;
        var i = Math.Min(session.Index, n);
        var step = i < n ? steps[i] : null;
        return new TutorialUiState
        {
            Index = i,
            Count = n,
            Finished = session.Finished,
            StepText = session.Finished ? "" : step?.Text ?? "",
            Keys = step?.Keys ?? [],
            Mouse = step?.Mouse,
            Message = message,
            MessageKind = messageKind,
            OkFlash = okFlash,
            PressedKeys = [.. pressed],
            CompleteText = completeText,
            HasView = engine is not null,
            Loading = loading,
            Started = started,
            ViewFailed = viewFailed,
        };
    }

    private void Publish()
    {
        if (disposed) return;
        State = BuildState();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- ビューの用意
    public async Task ActivateAsync()
    {
        if (engine is not null || loading || disposed) return;
        if (active is not null && active != this) active.Suspend();
        active = this;
        loading = true;
        Publish();
        try
        {
            // 画面の更新(overlay の「読み込み中…」)を先に見せてから始める
            await Task.Yield();
            if (active != this || disposed) return;
            var e = new TutorialViewEngine(rootId, canvasHostId, () => Numpad, Debug, scenario, OnEvent);
            if (!e.Start())
            {
                e.Dispose();
                viewFailed = true;
                return;
            }
            engine = e;
            e.Changed += OnEngineChanged;
            session.Vp = e.Core;
            started = true;
            session.ReplayTo(session.Finished ? scenario.Steps.Count : session.GroupStart(session.Index));
            RenderStep();
            if (e.Core is { } core) core.SetStatusText("案内にしたがって操作してください");
            e.Refresh();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[avatar] チュートリアルの 3D ビューを始められません: {ex.Message}");
            viewFailed = true;
        }
        finally
        {
            loading = false;
            Publish();
        }
    }

    /// <summary>別のチュートリアルにビューを譲る(進み具合は残す)。</summary>
    public void Suspend()
    {
        if (engine is null) return;
        failToken++;
        engine.Changed -= OnEngineChanged;
        engine.Dispose();
        engine = null;
        session.Vp = null;
        if (active == this) active = null;
        Publish();
    }

    private void OnEngineChanged() => Changed?.Invoke();

    public void Dispose()
    {
        if (disposed) return;
        Suspend();
        disposed = true;
        failToken++;
        msgToken++;
    }

    // ---------------------------------------------------------------- 手順
    private void OnEvent(ViewEvent e)
    {
        if (session.Finished || disposed) return;
        if (e.Type == "key") FlashKey(e);
        var outcome = session.OnEvent(e);
        if (outcome.Advanced)
        {
            failToken++;   // 失敗後のやり直し待ちのあいだに進んだら、やり直さない
            FlashOk();
            if (session.Finished) Finish();
            else RenderStep("できました！");
            return;
        }
        if (outcome.Fail is { } fail)
        {
            // 少し待ってから戻す(確定やキャンセルの結果が一瞬見えるように)
            ShowMessage($"{fail} この操作の最初からやり直します。", "warn");
            _ = ReplayAfterFailAsync(fail, outcome.FailTarget);
            return;
        }
        if (outcome.Hint is { } hint) ShowMessage(hint, "hint");
    }

    private async Task ReplayAfterFailAsync(string fail, int target)
    {
        var token = ++failToken;
        await PracticeCsJsTutorial.DelayAsync(400);
        if (token != failToken || disposed || engine is null) return;
        session.ReplayTo(target);
        RenderStep();
        ShowMessage($"{fail} この操作の最初からやり直しましょう。", "warn");
        engine.Refresh();
    }

    /// <summary>いまの手順の案内を出す(renderStep)。okText があれば「できました！」を出す。</summary>
    private void RenderStep(string? okText = null)
    {
        var step = session.Current;
        if (okText is not null) ShowMessage(okText, "ok"); else HideMessage();
        if (step is not null) session.Vp?.SetHighlight(step.Highlight);
        Publish();
    }

    private void ShowMessage(string html, string kind)
    {
        message = html;
        messageKind = kind;
        var token = ++msgToken;
        Publish();
        if (kind == "ok") _ = HideLaterAsync(token, 1500);
    }

    private void HideMessage()
    {
        message = null;
        messageKind = "";
        msgToken++;
    }

    private async Task HideLaterAsync(int token, int ms)
    {
        await PracticeCsJsTutorial.DelayAsync(ms);
        if (token != msgToken || disposed) return;
        HideMessage();
        Publish();
    }

    private void FlashOk()
    {
        okFlash++;
        Publish();
    }

    private void FlashKey(ViewEvent e)
    {
        var step = session.Current;
        if (step is null) return;
        foreach (var combo in step.Keys)
            foreach (var label in combo.Split('+'))
            {
                if (!TutorialSession.KeyMatches(label, e)) continue;
                pressed.Add(label);
                _ = UnflashAsync(label);
            }
        Publish();
    }

    private async Task UnflashAsync(string label)
    {
        await PracticeCsJsTutorial.DelayAsync(250);
        if (disposed) return;
        pressed.Remove(label);
        Publish();
    }

    private void Finish()
    {
        session.Vp?.SetHighlight(null);
        HideMessage();
        completeText = PracticeCsJsTutorial.Finish(scenario.Name);
        Publish();
    }

    public void Retry()
    {
        if (session.Finished) { Restart(); return; }
        failToken++;
        session.ReplayTo(session.GroupStart(session.Index));
        RenderStep();
        ShowMessage("この操作の最初からやり直します。", "info");
        engine?.Refresh();
    }

    public void Restart()
    {
        failToken++;
        completeText = "";
        session.Restart();
        RenderStep();
        engine?.Refresh();
    }

    public void Skip()
    {
        if (session.Finished) return;
        failToken++;
        session.Skip();
        if (session.Finished) Finish();
        else RenderStep();
        engine?.Refresh();
    }

    public void ClickTool(string tool) => engine?.ClickTool(tool);

    public void ChooseMenu(int index) => engine?.ChooseMenu(index);

    public void ChangeNPanel(string prop, string axis, string value) => engine?.ChangeNPanel(prop, axis, value);
}
