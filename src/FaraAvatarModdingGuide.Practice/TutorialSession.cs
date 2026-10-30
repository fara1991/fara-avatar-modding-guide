namespace FaraAvatarModdingGuide.Practice;

/// <summary>ビューのイベントを 1 つ渡したときの結果(画面が、メッセージの表示・やり直しの予約・「できました」の表示に使う)。</summary>
public sealed record TutorialOutcome
{
    /// <summary>手順を達成して、次へ進んだ(完了した場合も)。</summary>
    public bool Advanced { get; init; }
    /// <summary>失敗(group の最初からやり直し)。画面は、この文を出し、少し待って <see cref="TutorialSession.ReplayTo"/> を呼ぶ。</summary>
    public string? Fail { get; init; }
    /// <summary>やり直す先の手順の番号(Fail のとき)。</summary>
    public int FailTarget { get; init; }
    /// <summary>ヒント(進まない)。</summary>
    public string? Hint { get; init; }
    /// <summary>key イベント(画面下のキー表示を光らせる)。</summary>
    public bool KeyPressed { get; init; }
}

/// <summary>
/// 記事内チュートリアル 1 本の進行(tutorial.js の Tutorial クラスから、画面(DOM)・タイマー・WebGL の読み込みを除いたもの)。
/// ビューのイベントを <see cref="OnEvent"/> に渡すと、手順の達成・失敗・ヒントを判定して返す。画面(Razor)がその結果を見せる。
/// </summary>
public sealed class TutorialSession
{
    public TutorialSession(TutorialScenario scenario, ViewportCore? vp = null)
    {
        Scenario = scenario;
        Vp = vp;
    }

    public TutorialScenario Scenario { get; }

    /// <summary>手順を再生・判定するビュー(無いとき = 画像で見ている・休止中。判定のイベントは来ない)。</summary>
    public ViewportCore? Vp { get; set; }

    /// <summary>いまの手順の番号(0 始まり)。完了すると手順の数。</summary>
    public int Index { get; private set; }

    public bool Finished { get; private set; }

    public StepContext Context { get; private set; } = new();

    public IReadOnlyList<TutorialStep> Steps => Scenario.Steps;

    public TutorialStep? Current => Index < Steps.Count ? Steps[Index] : null;

    /// <summary>手順 i のグループの最初の手順の番号(グループが無ければ i)。</summary>
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
        var vp = Vp;
        if (vp is not null)
        {
            vp.Silent = true;
            vp.ResetScene(Scenario.Scene);   // 最初の視点は、ビューを作るときの options(Scenario の ViewDist / ViewTarget)
            for (var k = 0; k < i && k < Steps.Count; k++) Steps[k].Skip?.Invoke(vp);
            vp.Silent = false;
            if (!keepStatus) vp.SetStatusText("案内にしたがって操作してください");
        }
        Index = i;
        Context = new StepContext();
    }

    /// <summary>グループの最初からやり直す(「やり直す」ボタン)。完了していれば最初から。</summary>
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
        if (Vp is { } vp)
        {
            vp.Silent = true;
            step.Skip?.Invoke(vp);
            vp.Silent = false;
        }
        Advance();
    }

    /// <summary>次の手順へ(最後なら完了)。</summary>
    public void Advance()
    {
        Index++;
        Context = new StepContext();
        if (Index >= Steps.Count) Finished = true;
    }

    /// <summary>
    /// ビューのイベントを 1 つ渡す。達成なら進み、失敗なら Fail(画面が少し待って <see cref="ReplayTo"/>)、それ以外はヒントがあれば Hint を返す。
    /// 完了後・手順が無いときは何もしない。
    /// </summary>
    public TutorialOutcome OnEvent(ViewEvent e)
    {
        if (Finished || Vp is null) return new TutorialOutcome();
        var keyPressed = e.Type == "key";
        var step = Current;
        if (step is null) return new TutorialOutcome { KeyPressed = keyPressed };
        if (step.Done(e, Vp, Context))
        {
            Advance();
            return new TutorialOutcome { Advanced = true, KeyPressed = keyPressed };
        }
        var fail = step.Fail?.Invoke(e, Vp, Context);
        if (!string.IsNullOrEmpty(fail)) return new TutorialOutcome { Fail = fail, FailTarget = GroupStart(Index), KeyPressed = keyPressed };
        var hint = step.Hint?.Invoke(e, Vp, Context)
            ?? (e.Type == "opfail" ? OpFailHint(e, Vp) : null)
            ?? (e.Type == "opstart" && (e.StrIs("kind", "G") || e.StrIs("kind", "R") || e.StrIs("kind", "S")) && !e.Flag("extrude") && !step.AllowOp
                ? "移動・回転・スケールが始まりました。<kbd>Esc</kbd> か右クリックでキャンセルしてください。" : null);
        return new TutorialOutcome { Hint = hint, KeyPressed = keyPressed };
    }

    /// <summary>操作できなかったとき(opfail)の、手順に専用のヒントがない場合の案内。</summary>
    public static string? OpFailHint(ViewEvent e, ViewportCore? vp)
    {
        if (e.StrIs("reason", "objectmode")) return "<kbd>Tab</kbd> で編集モードに入ってください。";
        if (e.StrIs("reason", "noselection")) return vp?.Mode == "edit" ? "先に頂点・辺・面を選択してください。" : "先にオブジェクトをクリックして選択してください。";
        return e.Str("reason");   // ベベルなど、理由の文章がそのまま入っている
    }

    /// <summary>画面下のキー表示 label が、押されたキー e に当たるか(tutorial.js の keyMatches)。</summary>
    public static bool KeyMatches(string label, ViewEvent e)
    {
        var l = label.ToLowerInvariant();
        var code = e.Str("code") ?? "";
        var key = e.Str("key") ?? "";
        if (l == "shift") return code.StartsWith("Shift", StringComparison.Ordinal);
        if (l == "ctrl") return code.StartsWith("Control", StringComparison.Ordinal) || code.StartsWith("Meta", StringComparison.Ordinal);
        if (l == "alt") return code.StartsWith("Alt", StringComparison.Ordinal);
        if (l == "enter") return code is "Enter" or "NumpadEnter";
        if (l == "esc") return code == "Escape";
        if (l == "home") return code == "Home";
        if (l == "tab") return code == "Tab";
        if (l.StartsWith("numpad ", StringComparison.Ordinal))
        {
            var k = l[7..];
            return k == "." ? code == "NumpadDecimal" || key == "." : code == $"Numpad{k}" || code == $"Digit{k}";
        }
        if (l.Length == 1 && l[0] is >= 'a' and <= 'z') return code == $"Key{label.ToUpperInvariant()}";
        if (l.Length == 1 && l[0] is >= '0' and <= '9') return code == $"Digit{l}" || code == $"Numpad{l}";
        if (l == ".") return key == "." || code == "NumpadDecimal";
        if (l == "-") return key == "-" || code == "NumpadSubtract";
        return false;
    }
}
