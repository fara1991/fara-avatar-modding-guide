using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// Unity 風の画面の記事内チュートリアル 1 本(tutorial.js の Tutorial クラスの engine "unity" に当たる。Phase 3b S5)。
/// <see cref="TutorialController"/> と同じ作りで、ビューが Unity 風の画面(<see cref="UnityCsEngine"/>)。画面(TutorialEmbed.razor)は、<see cref="Unity"/> があるあいだ、その中に UnityEditorView を置く。
/// 手順の進行は <see cref="UnityTutorialSession"/>。Scene ビューの WebGL は、画面がエディターの DOM を描いたあと(UnityEditorView の最初の描画のあと)に始まるので、始まった知らせ(<see cref="OnUnityStarted"/>)を待つ。
/// </summary>
internal sealed class UnityTutorialController : ITutorialCs
{
    private readonly UnityTutorialScenario scenario;
    private readonly string rootId;
    private readonly string canvasHostId;
    private readonly string sceneViewId;
    private readonly UnityTutorialSession session;
    private UnityCsEngine? engine;
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

    private UnityTutorialController(UnityTutorialScenario scenario, string rootId, string canvasHostId, string sceneViewId)
    {
        this.scenario = scenario;
        this.rootId = rootId;
        this.canvasHostId = canvasHostId;
        this.sceneViewId = sceneViewId;
        session = new UnityTutorialSession(scenario);
        Info = new TutorialInfo(scenario.Name, scenario.Title, false, [.. scenario.Steps.Select(s => new TutorialStepInfo(s.Text, s.Keys, s.Mouse))], "unity");
        State = BuildState();
    }

    public static ITutorialCs? Create(string name, string rootId, string canvasHostId, string sceneViewId) =>
        UnityTutorialScenarios.Find(name) is { } scenario ? new UnityTutorialController(scenario, rootId, canvasHostId, sceneViewId) : null;

    public TutorialInfo Info { get; }
    public TutorialUiState State { get; private set; }
    public PracticeHud Hud => new();
    public IUnityCs? Unity => engine;
    public bool Numpad { get; set; }
    public bool Debug { get; set; }
    public event Action? Changed;

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
            HasView = engine is not null && started,
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "3D ビューの開始は WebGL・JS 連携・エンジン生成など失敗の種類が多い。どれでも「3D を使えない」表示にして、ページの説明は読めるままにする（終了はさせない）")]
    public async Task ActivateAsync()
    {
        if (engine is not null || loading || disposed) return;
        ActiveGlView.Claim(this);
        loading = true;
        viewFailed = false;
        Publish();
        try
        {
            // 画面の更新(overlay の「読み込み中…」)を先に見せてから始める
            await Task.Yield();
            if (!ActiveGlView.IsCurrent(this) || disposed) { loading = false; return; }
            var created = (UnityCsEngine)await UnityCsLoader.CreateAsync(rootId, sceneViewId, canvasHostId, true, [.. scenario.Instances], Debug);
            if (!ActiveGlView.IsCurrent(this) || disposed) { created.Dispose(); loading = false; return; }
            engine = created;
            created.Changed += OnEngineChanged;
            created.EventEmitted += OnEvent;
            session.Ed = created.Core;
            session.ReplayTo(session.Finished ? scenario.Steps.Count : session.GroupStart(session.Index));
            RenderStep();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[avatar] Unity のチュートリアルの 3D ビューを始められません: {ex.Message}");
            viewFailed = true;
            loading = false;
        }
        finally
        {
            Publish();
        }
    }

    /// <summary>画面がエディターを描いて、Scene ビューの WebGL を始めた(UnityEditorView の OnStarted)。</summary>
    public void OnUnityStarted(bool ok)
    {
        if (disposed || engine is null) return;
        loading = false;
        if (ok)
        {
            started = true;
            engine.Refresh();
        }
        else
        {
            viewFailed = true;
            DropEngine();
        }
        Publish();
    }

    private void DropEngine()
    {
        if (engine is null) return;
        failToken++;
        engine.Changed -= OnEngineChanged;
        engine.EventEmitted -= OnEvent;
        engine.Dispose();
        engine = null;
        session.Ed = null;
        ActiveGlView.Release(this);
    }

    /// <summary>別のチュートリアルにビューを譲る(進み具合は残す)。</summary>
    public void Suspend()
    {
        if (engine is null) return;
        DropEngine();
        loading = false;
        Publish();
    }

    private void OnEngineChanged() => Changed?.Invoke();

    public void Dispose()
    {
        if (disposed) return;
        Suspend();
        ActiveGlView.Release(this);
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
        if (step is not null) session.Ed?.SetHighlight(step.Highlight);
        engine?.Refresh();
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
        session.Ed?.SetHighlight(null);
        engine?.Refresh();
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

    public void ClickTool(string tool) { }

    public void ChooseMenu(int index) { }

    public void ChangeNPanel(string prop, string axis, string value) { }
}
