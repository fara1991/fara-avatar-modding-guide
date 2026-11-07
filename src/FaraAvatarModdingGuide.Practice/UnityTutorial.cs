namespace FaraAvatarModdingGuide.Practice;

/// <summary>イベント e で達成か(Unity 風の画面のチュートリアル)。</summary>
public delegate bool UnityDoneFn(ViewEvent e, UnityEditorCore ed, StepContext ctx);

/// <summary>ヒント(進まない)・失敗(group の最初からやり直す)の文。出さないなら null。</summary>
public delegate string? UnityMessageFn(ViewEvent e, UnityEditorCore ed, StepContext ctx);

/// <summary>Unity 風の画面のチュートリアルの手順 1 つ(scenarios.js の engine "unity" の step)。意味は <see cref="TutorialStep"/> と同じ。</summary>
public sealed class UnityTutorialStep
{
    public string? Group { get; init; }
    public required string Text { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
    public string? Mouse { get; init; }
    public UnityHighlight? Highlight { get; init; }
    public required UnityDoneFn Done { get; init; }
    public UnityMessageFn? Hint { get; init; }
    public UnityMessageFn? Fail { get; init; }
    public Action<UnityEditorCore>? Skip { get; init; }
}

/// <summary>Unity 風の画面のチュートリアル 1 本(scenarios.js の SCENARIOS の engine "unity" の値)。</summary>
public sealed class UnityTutorialScenario
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    /// <summary>最初に置く Prefab(scene.instances)。</summary>
    public IReadOnlyList<string> Instances { get; init; } = ["Avatar"];
    public required IReadOnlyList<UnityTutorialStep> Steps { get; init; }
}

/// <summary>
/// Unity 風の画面のチュートリアル 1 本の進行(<see cref="TutorialSession"/> と同じ決まり。ビューが <see cref="UnityEditorCore"/>)。
/// エディターのイベントを <see cref="OnEvent"/> に渡すと、手順の達成・失敗・ヒントを判定して返す。画面(Razor)がその結果を見せる。
/// </summary>
public sealed class UnityTutorialSession
{
    public UnityTutorialSession(UnityTutorialScenario scenario, UnityEditorCore? ed = null)
    {
        Scenario = scenario;
        Ed = ed;
    }

    public UnityTutorialScenario Scenario { get; }

    /// <summary>手順を再生・判定するエディター(無いとき = 画像で見ている・休止中)。</summary>
    public UnityEditorCore? Ed { get; set; }

    public int Index { get; private set; }

    public bool Finished { get; private set; }

    public StepContext Context { get; private set; } = new();

    public IReadOnlyList<UnityTutorialStep> Steps => Scenario.Steps;

    public UnityTutorialStep? Current => Index < Steps.Count ? Steps[Index] : null;

    public int GroupStart(int i)
    {
        var g = i < Steps.Count ? Steps[i].Group : null;
        if (g is null) return i;
        while (i > 0 && Steps[i - 1].Group == g) i--;
        return i;
    }

    /// <summary>最初の状態から、i 番目の手順の直前まで進めた状態にする。keepStatus なら最後の操作のステータス表示を残す。</summary>
    public void ReplayTo(int i, bool keepStatus = false)
    {
        var ed = Ed;
        if (ed is not null)
        {
            ed.Silent = true;
            ed.ResetScene(Scenario.Instances);
            for (var k = 0; k < i && k < Steps.Count; k++) Steps[k].Skip?.Invoke(ed);
            ed.Silent = false;
            if (!keepStatus) ed.SetStatus("案内にしたがって操作してください");
        }
        Index = i;
        Context = new StepContext();
    }

    public void Retry()
    {
        if (Finished) { Restart(); return; }
        ReplayTo(GroupStart(Index));
    }

    public void Restart()
    {
        Finished = false;
        ReplayTo(0);
    }

    /// <summary>この手順を終えた状態にして、次へ進む(「次へ(スキップ)」)。</summary>
    public void Skip()
    {
        if (Finished) return;
        var step = Current;
        if (step is null) return;
        if (Ed is { } ed)
        {
            ed.Silent = true;
            step.Skip?.Invoke(ed);
            ed.Silent = false;
        }
        Advance();
    }

    public void Advance()
    {
        Index++;
        Context = new StepContext();
        if (Index >= Steps.Count) Finished = true;
    }

    public TutorialOutcome OnEvent(ViewEvent e)
    {
        if (Finished || Ed is null) return new TutorialOutcome();
        var keyPressed = e.Type == "key";
        var step = Current;
        if (step is null) return new TutorialOutcome { KeyPressed = keyPressed };
        if (step.Done(e, Ed, Context))
        {
            Advance();
            return new TutorialOutcome { Advanced = true, KeyPressed = keyPressed };
        }
        var fail = step.Fail?.Invoke(e, Ed, Context);
        if (!string.IsNullOrEmpty(fail)) return new TutorialOutcome { Fail = fail, FailTarget = GroupStart(Index), KeyPressed = keyPressed };
        var hint = step.Hint?.Invoke(e, Ed, Context);
        return new TutorialOutcome { Hint = string.IsNullOrEmpty(hint) ? null : hint, KeyPressed = keyPressed };
    }
}
