namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// 記事内チュートリアル(::: practice &lt;名前&gt;)の手順(wwwroot/js/scenarios.js の Blender の 3D ビューのもの 9 本)。Unity の 2 本(unity-basics・ma-outfit)は S5。
/// 文言・キー・マウス・案内・達成/ヒント/失敗の条件・スキップの処理は scenarios.js と同じ(Practice.Tests の TutorialParityTests が、本物の scenarios.js と、
/// 手順の定義・同じイベントへの答え・スキップの結果を突き合わせる)。
/// </summary>
public static class TutorialScenarios
{
    private static DoneFn D(Func<ViewEvent, bool> f) => (e, _, _) => f(e);

    private static MessageFn M(Func<ViewEvent, string?> f) => (e, _, _) => f(e);

    private static readonly Dictionary<string, string> OpName = new() { ["G"] = "移動", ["R"] = "回転", ["S"] = "スケール" };
    private static readonly Dictionary<string, string> Unit = new() { ["G"] = " m", ["R"] = "°", ["S"] = " 倍" };

    private static string Kbd(string s) => $"<kbd>{s}</kbd>";

    private static string? Interrupted(ViewEvent e) => e.Type == "cancel" ? "キャンセルされました。" : e.Type == "confirm" ? "途中で確定されました。" : null;

    private static bool IsEdit(ViewEvent e) => e.Type == "select" && e.Flag("edit");

    private static TransformKind Kind(string key) => key switch { "G" => TransformKind.G, "R" => TransformKind.R, _ => TransformKind.S };

    private static void TypeValue(ViewportCore vp, string value)
    {
        while (vp.Op is { } op && op.Numeric.Length > 0) vp.TypeOpNumeric("Backspace");
        foreach (var ch in value) vp.TypeOpNumeric(ch.ToString());
    }

    /// <summary>G / R / S → 軸 → 数値 → Enter を 1 キーずつ案内するステップ群。</summary>
    private static List<TutorialStep> OpSteps(string key, string? axis, string value, string group, string first, string? target = "Cube")
    {
        TutorialHighlight Hl(string? ax = null) => new(Object: target, Axis: ax);
        var name = OpName[key];
        var a = axis?.ToUpperInvariant();
        var steps = new List<TutorialStep>
        {
            new()
            {
                Group = group, Keys = [key], Highlight = Hl(),
                Text = first,
                Done = D(e => e.Type == "opstart" && e.StrIs("kind", key)),
                Hint = M(e => e.Type == "opstart" ? $"{OpName.GetValueOrDefault(e.Str("kind") ?? "", "別の操作")}が始まりました。{Kbd("Esc")} でキャンセルしてから {Kbd(key)} を押してください。" : null),
                Skip = vp => vp.StartOperation(Kind(key)),
            },
        };
        if (axis is not null)
        {
            steps.Add(new TutorialStep
            {
                Group = group, Keys = [a!], Highlight = Hl(axis),
                Text = $"{Kbd(a!)} を押して、{a} 軸の方向だけに固定してください。{a} 軸の線が表示されます。",
                Done = D(e => e.Type == "axis" && e.StrIs("axis", axis) && !e.Flag("exclude")),
                Hint = M(e =>
                    e.Type == "axis" && e.Str("axis") is { Length: > 0 } ax && ax != axis ? $"{ax.ToUpperInvariant()} 軸になっています。{Kbd(a!)} を押してください。" :
                    e.Type == "axis" && e.Flag("exclude") ? $"{Kbd("Shift")} を押すと「{a} 以外」になります。{Kbd(a!)} だけを押してください。" :
                    e.Type == "numeric" ? $"先に軸を固定します。{Kbd("Backspace")} で数字を消してから {Kbd(a!)} を押してください。" : null),
                Fail = M(Interrupted),
                Skip = vp => vp.SetOpAxis(axis, false),
            });
        }
        steps.Add(new TutorialStep
        {
            Group = group, Keys = [.. value.Select(c => c.ToString())], Highlight = Hl(axis),
            Text = $"数字で {Kbd(value)} と入力してください。{name}の量がちょうど {value}{Unit[key]} になります。",
            Done = D(e => e.Type == "numeric" && e.StrIs("value", value)),
            Hint = M(e =>
                e.Type == "numeric" && !value.StartsWith(e.Str("value") ?? "", StringComparison.Ordinal) ? $"「{e.Str("value")}」になっています。{Kbd("Backspace")} で消して {value} と入力してください。" :
                e.Type == "axis" && axis is not null ? $"軸が変わりました。{Kbd(a!)} を押して戻してください。" :
                e.Type == "axis" ? "軸が固定されました。同じキーをもう一度押すと解除できます。" : null),
            Fail = M(Interrupted),
            Skip = vp => TypeValue(vp, value),
        });
        steps.Add(new TutorialStep
        {
            Group = group, Keys = ["Enter"], Highlight = Hl(),
            Text = $"{Kbd("Enter")} で確定してください（左クリックでも確定できます）。",
            Done = D(e => e.Type == "confirm" && e.StrIs("value", value) && e.StrIs("axis", axis)),
            Fail = M(e => e.Type == "cancel" ? "キャンセルされました。" : e.Type == "confirm" ? "入力が変わったまま確定されました。" : null),
            Hint = M(e => e.Type == "numeric" && !e.StrIs("value", value) ? $"入力が「{e.Str("value")}」になっています。{value} に戻してから確定してください。" : null),
            Skip = vp => vp.ConfirmOperation(),
        });
        return steps;
    }

    /// <summary>オブジェクトをクリックして選択するステップ。</summary>
    private static TutorialStep ClickStep(string name, string text, string? group = null) => new()
    {
        Group = group, Text = text, Mouse = "左クリック", Highlight = new TutorialHighlight(Object: name, Label: "クリック"),
        Done = D(e => e.Type == "select" && e.StrIs("active", name) && e.StrList("selected").Count == 1),
        Hint = M(e => e.Type == "select" ? $"{name}（矢印のもの）だけをクリックしてください。" : null),
        Skip = vp => vp.SelectByName(name),
    };

    /// <summary>テンキーで視点を切り替えるステップ。</summary>
    private static TutorialStep NumpadStep(string key, bool ctrl, string viewName, string text)
    {
        var label = $"{(ctrl ? "Ctrl+" : "")}Numpad {key}";
        return new TutorialStep
        {
            Keys = [label], Highlight = new TutorialHighlight(Hud: "view"), Text = text,
            Done = D(e => e.Type == "view" && e.StrIs("action", "axis") && e.StrIs("name", viewName)),
            Hint = (e, vp, _) =>
                e.Type == "key" && e.StrIs("code", $"Digit{key}") && !vp.EmulateNumpad() ? "テンキーがない場合は、下の「テンキーを模倣」をオンにすると数字キーで同じ操作ができます。" :
                e.Type == "view" && e.StrIs("action", "axis") ? $"「{e.Str("name")}」からの視点になりました。{Kbd(label)} を押してください。" : null,
            Skip = vp => vp.NumpadViewNow(key, ctrl),
        };
    }

    // ---------------------------------------------------------------- 編集モード用
    private static readonly Dictionary<string, string> ModeKey = new() { ["vert"] = "1", ["edge"] = "2", ["face"] = "3" };
    private static readonly Dictionary<string, string> ModeName = new() { ["vert"] = "頂点", ["edge"] = "辺", ["face"] = "面" };

    private static TutorialStep TabStep(bool on, string text, string? group = null) => new()
    {
        Group = group, Keys = ["Tab"], Text = text,
        Done = D(e => e.Type == "mode" && e.StrIs("mode", on ? "edit" : "object")),
        Hint = M(e => e.Type == "opfail" && e.StrIs("kind", "Tab") ? "先にオブジェクトをクリックして選択してください。" : null),
        Skip = vp => vp.ToggleEditMode(on),
    };

    private static TutorialStep SelectModeStep(string mode, string? text = null, string? group = null) => new()
    {
        Group = group, Keys = [ModeKey[mode]], Highlight = new TutorialHighlight(Tool: mode),
        Text = text ?? $"{Kbd(ModeKey[mode])} を押して{ModeName[mode]}選択モードにしてください（上の「{ModeName[mode]}」ボタンでも切り替えられます）。",
        Done = D(e => e.Type == "selectmode" && e.StrIs("mode", mode)),
        Hint = M(e => e.Type == "view" && e.StrIs("action", "axis") ? "「テンキーを模倣」がオンだと、数字キーは視点の切り替えになります。上のボタンで切り替えてください。" : null),
        Skip = vp => vp.SetSelectMode(mode),
    };

    /// <summary>編集モードの操作（E / I / Ctrl+B）→ 数値 → Enter。</summary>
    private static List<TutorialStep> EditOpSteps(string keys, string kind, bool extrude, string value, string unit, string name, string group, string first, Action<ViewportCore> start) =>
    [
        new()
        {
            Group = group, Keys = [keys], Text = first,
            Done = D(e => e.Type == "opstart" && e.StrIs("kind", kind) && (!extrude || e.Flag("extrude"))),
            Hint = M(e =>
                e.Type == "opfail" ? (e.StrIs("reason", "noselection") ? "先に面や辺を選択してください。" : e.StrIs("reason", "objectmode") ? $"{Kbd("Tab")} で編集モードに入ってください。" : e.Str("reason")) :
                e.Type == "opstart" ? $"別の操作が始まりました。{Kbd("Esc")} でキャンセルしてください。" : null),
            Skip = start,
        },
        new()
        {
            Group = group, Keys = [.. value.Select(c => c.ToString())],
            Text = $"数字で {Kbd(value)} と入力してください（{name}が {value}{unit} になります）。",
            Done = D(e => e.Type == "numeric" && e.StrIs("value", value)),
            Hint = M(e => e.Type == "numeric" && !value.StartsWith(e.Str("value") ?? "", StringComparison.Ordinal) ? $"「{e.Str("value")}」になっています。{Kbd("Backspace")} で消して {value} と入力してください。" : null),
            Fail = M(Interrupted),
            Skip = vp => TypeValue(vp, value),
        },
        new()
        {
            Group = group, Keys = ["Enter"], Text = $"{Kbd("Enter")} で確定してください（左クリックでも確定できます）。",
            Done = D(e => e.Type == "confirm" && e.StrIs("kind", kind) && e.StrIs("value", value)),
            Fail = M(e => e.Type == "cancel" ? "キャンセルされました。" : e.Type == "confirm" ? "入力が変わったまま確定されました。" : null),
            Skip = vp => vp.ConfirmOperation(),
        },
    ];

    /// <summary>ワールド座標の点 at の近くの面・辺をクリックで選ぶ。</summary>
    private static TutorialStep EditClickStep(string text, double[] at, string group, DoneFn done, MessageFn hint, Action<ViewportCore> skip, string label = "クリック", string mouse = "左クリック") => new()
    {
        Group = group, Text = text, Mouse = mouse, Highlight = new TutorialHighlight(At: at, Label: label), Done = done, Hint = hint, Skip = skip,
    };

    /// <summary>Ctrl+R → ホイールで本数 → クリック → 位置（右クリックで中央）。</summary>
    private static List<TutorialStep> LoopCutSteps(double[] at, int cuts, Func<EdgeProbe, bool> edge, string where, bool center = false, string group = "loopcut")
    {
        var steps = new List<TutorialStep>
        {
            new()
            {
                Group = group, Keys = ["Ctrl+R"], Highlight = new TutorialHighlight(At: at, Label: "ここに置く"),
                Text = $"{Kbd("Ctrl")}+{Kbd("R")} を押して、{where}にマウスを置いてください。黄色い線でカットの位置が表示されます。",
                Done = D(e => e.Type == "opstart" && e.StrIs("kind", "loopcut")),
                Hint = M(e => e.Type == "opfail" && e.StrIs("reason", "objectmode") ? $"{Kbd("Tab")} で編集モードに入ってください。" : null),
                Skip = vp => vp.PreviewLoopCut(edge),
            },
        };
        if (cuts > 1)
        {
            steps.Add(new TutorialStep
            {
                Group = group, Mouse = "ホイールを回す", Highlight = new TutorialHighlight(At: at, Label: "ホイール"),
                Text = $"黄色い線が出ている状態でホイールを回して、{cuts} 本にしてください。",
                Done = D(e => e.Type == "cuts" && e.Num("cuts") == cuts),
                Hint = M(e => e.Type == "cuts" ? $"今は {e.Num("cuts")} 本です。{(e.Num("cuts") > cuts ? "逆に回して減らして" : "もう少し回して")}ください。" : null),
                Fail = M(e => e.Type == "cancel" && e.StrIs("kind", "loopcut") ? "ループカットをやめました。" : null),
                Skip = vp => vp.SetLoopCuts(cuts),
            });
        }
        steps.Add(new TutorialStep
        {
            Group = group, Mouse = "左クリック", Highlight = new TutorialHighlight(At: at, Label: "クリック"),
            Text = "黄色い線が出ている状態で <strong>左クリック</strong> して、辺を入れてください。",
            Done = D(e => e.Type == "loopcut" && e.Num("cuts") == cuts),
            Hint = M(e => e.Type == "loopcut" ? $"{e.Num("cuts")} 本で入りました。" : null),
            Fail = M(e => e.Type == "cancel" && e.StrIs("kind", "loopcut") ? "ループカットをやめました。" : e.Type == "loopcut" && e.Num("cuts") != cuts ? $"本数が {cuts} 本ではありませんでした。" : null),
            Skip = vp => vp.CommitLoopCutAt(vp.FindEdge(edge)),
        });
        steps.Add(new TutorialStep
        {
            Group = group, Mouse = center ? "右クリック" : "動かしてクリック / 右クリック",
            Text = center
                ? "続けて位置を決めます。今回は <strong>右クリック</strong>（または <kbd>Esc</kbd>）で、ちょうど真ん中に入れてください。"
                : "マウスを動かすと辺が滑ります。クリックで確定、<strong>右クリック</strong> だとちょうど真ん中に入ります。",
            Done = D(e => e.Type == "confirm" && e.StrIs("kind", "slide") && (!center || e.Flag("centered"))),
            Fail = M(e => e.Type == "confirm" && e.StrIs("kind", "slide") ? "真ん中からずれました。右クリックで真ん中に入れましょう。" : null),
            Skip = vp => vp.CancelOperation(),
        });
        return steps;
    }

    private static bool UpNormal(ViewportCore vp) => vp.GetEditInfo().Faces.Count > 0 && vp.GetEditInfo().Faces[0].Normal.Z > 0.9;

    private static List<TutorialStep> S(params object[] parts)
    {
        var list = new List<TutorialStep>();
        foreach (var p in parts)
        {
            if (p is TutorialStep s) list.Add(s);
            else if (p is IEnumerable<TutorialStep> many) list.AddRange(many);
        }
        return list;
    }

    private static SceneSpec Obj(string kind, string? name = null, double[]? p = null, double[]? s = null) => new(kind, name, p, null, s);

    public static readonly IReadOnlyDictionary<string, TutorialScenario> All = Build();

    private static Dictionary<string, TutorialScenario> Build()
    {
        var all = new Dictionary<string, TutorialScenario>();
        void Add(TutorialScenario s) => all[s.Name] = s;

        // getting-started/navigation（前半）
        Add(new TutorialScenario
        {
            Name = "navigation-mouse", Title = "マウスで視点を動かす",
            Steps =
            [
                new TutorialStep
                {
                    Text = "ビューポートの上で <strong>ホイールボタン（中ボタン）を押したまま</strong> ドラッグして、視点をぐるっと回してください。",
                    Mouse = "ホイールボタンでドラッグ（または Alt+左ドラッグ）",
                    Done = (e, _, c) => e.Type == "view" && e.StrIs("action", "orbit") && (c.Total += e.Num("distance") ?? 0) >= 150,
                    Hint = M(e =>
                        e.Type == "view" && e.StrIs("action", "orbit") ? "その調子です。もう少し大きく回してみましょう。" :
                        e.Type == "select" ? "左クリックは選択です。ホイールボタンを押し込んだままドラッグしてください。" : null),
                    Skip = vp => vp.OrbitBy(0.8, -0.2),
                },
                new TutorialStep
                {
                    Text = "<kbd>Shift</kbd> を押しながらホイールボタンでドラッグして、視点を平行移動してください。",
                    Keys = ["Shift"], Mouse = "ホイールボタンでドラッグ",
                    Done = (e, _, c) => e.Type == "view" && e.StrIs("action", "pan") && (c.Total += e.Num("distance") ?? 0) >= 100,
                    Hint = M(e => e.Type == "view" && e.StrIs("action", "orbit") ? "視点が回りました。<kbd>Shift</kbd> を押したままドラッグしてください。" : null),
                    Skip = vp => vp.View.Target = vp.View.Target with { X = vp.View.Target.X + 1.5 },
                },
                new TutorialStep
                {
                    Text = "ホイールを回して、近づいたり離れたりしてください（3 回ほど）。",
                    Mouse = "ホイールを回す",
                    Done = (e, _, c) => e.Type == "view" && e.StrIs("action", "zoom") && ++c.Count >= 3,
                    Skip = vp => vp.View.Dist *= 0.7,
                },
                new TutorialStep
                {
                    Text = "<kbd>Home</kbd> を押して、シーン全体が見える位置に戻してください。",
                    Keys = ["Home"],
                    Done = D(e => e.Type == "view" && e.StrIs("action", "home")),
                    Skip = vp => vp.FrameAll(),
                },
            ],
        });

        // getting-started/navigation（後半）
        Add(new TutorialScenario
        {
            Name = "navigation-numpad", Title = "テンキーで視点を切り替える", Numpad = true,
            Steps = S(
                NumpadStep("1", false, "前", "<kbd>Numpad 1</kbd> で正面（前）から見てください。左上の表示が「前・平行投影」に変わります。"),
                NumpadStep("3", false, "右", "<kbd>Numpad 3</kbd> で右から見てください。"),
                NumpadStep("7", false, "上", "<kbd>Numpad 7</kbd> で真上から見てください。"),
                NumpadStep("1", true, "後", "<kbd>Ctrl</kbd>+<kbd>Numpad 1</kbd> で反対側（後ろ）から見てください。"),
                new TutorialStep
                {
                    Text = "今は平行投影です。<kbd>Numpad 5</kbd> で透視投影に切り替えてください。",
                    Keys = ["Numpad 5"], Highlight = new TutorialHighlight(Hud: "view"),
                    Done = D(e => e.Type == "view" && e.StrIs("action", "ortho") && !e.Flag("ortho")),
                    Hint = M(e => e.Type == "view" && e.StrIs("action", "ortho") ? "平行投影に戻りました。もう一度 <kbd>Numpad 5</kbd> を押してください。" : null),
                    Skip = vp => { if (vp.View.Ortho) vp.NumpadViewNow("5"); },
                },
                ClickStep("Cube", "立方体をクリックして選択してください。", "frame"),
                new TutorialStep
                {
                    Group = "frame",
                    Text = "<kbd>Numpad .</kbd> で、選択した立方体を画面の中心に大きく表示してください。",
                    Keys = ["Numpad ."], Highlight = new TutorialHighlight(Object: "Cube"),
                    Done = D(e => e.Type == "view" && e.StrIs("action", "frame")),
                    Skip = vp => vp.NumpadViewNow("Decimal"),
                }),
        });

        // basics/select
        Add(new TutorialScenario
        {
            Name = "select", Title = "選択・追加・削除・複製",
            Scene = [Obj("cube", "Cube.002", [-3.5, 0, 0]), Obj("cube", "Cube", [0, 0, 0]), Obj("cube", "Cube.001", [3.5, 0, 0])],
            Steps = S(
                ClickStep("Cube", "真ん中の立方体（Cube）をクリックして選択してください。オレンジの枠が付きます。", "shift"),
                new TutorialStep
                {
                    Group = "shift",
                    Text = "<kbd>Shift</kbd> を押しながら右の立方体をクリックして、選択に追加してください。最後に選んだものが明るい枠の「アクティブ」になります。",
                    Keys = ["Shift"], Mouse = "左クリック", Highlight = new TutorialHighlight(Object: "Cube.001", Label: "Shift+クリック"),
                    Done = D(e => e.Type == "select" && e.StrList("selected").Count == 2 && e.StrList("selected").Contains("Cube") && e.StrIs("active", "Cube.001")),
                    Fail = M(e => e.Type == "select" && e.StrIs("source", "click") ? "Shift なしでクリックすると、選択が入れ替わります。" : null),
                    Hint = M(e => e.Type == "select" ? "右の立方体（矢印のもの）を Shift+クリックしてください。" : null),
                    Skip = vp => vp.SelectByName("Cube.001", true),
                },
                new TutorialStep
                {
                    Text = "何もない背景をクリックして、選択を解除してください。",
                    Mouse = "背景を左クリック",
                    Done = D(e => e.Type == "select" && e.StrIs("source", "click") && e.StrList("selected").Count == 0),
                    Hint = M(e => e.Type == "select" && e.StrList("selected").Count > 0 ? "オブジェクトのない、背景の部分をクリックしてください。" : null),
                    Skip = vp => vp.SelectAllItems(false),
                },
                new TutorialStep
                {
                    Text = "<kbd>A</kbd> を押して、すべてを選択してください。",
                    Keys = ["A"],
                    Done = D(e => e.Type == "select" && e.StrIs("source", "all")),
                    Skip = vp => vp.SelectAllItems(true),
                },
                new TutorialStep
                {
                    Text = "<kbd>Alt</kbd>+<kbd>A</kbd> で、選択をすべて解除してください。",
                    Keys = ["Alt+A"],
                    Done = D(e => e.Type == "select" && e.StrIs("source", "none")),
                    Skip = vp => vp.SelectAllItems(false),
                },
                new TutorialStep
                {
                    Text = "何もないところから <strong>左ドラッグ</strong> して、立方体を 2 つ以上囲んでください（ボックス選択）。",
                    Mouse = "左ドラッグで囲む",
                    Done = D(e => e.Type == "select" && e.StrIs("source", "box") && e.StrList("selected").Count >= 2),
                    Hint = M(e =>
                        e.Type == "select" && e.StrIs("source", "box") ? $"囲めたのは {e.StrList("selected").Count} 個でした。立方体の中心が枠に入るように、大きめに囲んでください。" :
                        e.Type == "select" ? "クリックではなく、押したままドラッグして四角で囲みます。" : null),
                    Skip = vp => vp.SelectAllItems(true),
                },
                new TutorialStep
                {
                    Group = "add",
                    Text = "<kbd>Shift</kbd>+<kbd>A</kbd> で追加メニューを開いてください。",
                    Keys = ["Shift+A"],
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "add")),
                    Skip = vp => vp.ShowAddMenuNow(),
                },
                new TutorialStep
                {
                    Group = "add",
                    Text = "メニューから「UV球」をクリックしてください。新しいものは 3D カーソル（中央の白い輪）の位置に置かれます。",
                    Mouse = "メニューをクリック",
                    Done = D(e => e.Type == "add"),
                    Hint = M(e => e.Type == "select" ? "メニューが閉じました。もう一度 <kbd>Shift</kbd>+<kbd>A</kbd> で開いてください。" : null),
                    Skip = vp => { vp.CloseMenuNow(); vp.AddPrimitiveNow("sphere"); },
                },
                new TutorialStep
                {
                    Group = "delete",
                    Text = "追加したものが選択されたままです。<kbd>X</kbd> を押して削除メニューを出してください。",
                    Keys = ["X"],
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "delete")),
                    Fail = M(e => e.Type == "delete" ? "<kbd>Delete</kbd> キーは確認なしで削除します。今回は <kbd>X</kbd> を試しましょう。" : null),
                    Skip = vp => vp.ShowDeleteMenuNow(),
                },
                new TutorialStep
                {
                    Group = "delete",
                    Text = "「削除」をクリックしてください。",
                    Mouse = "メニューをクリック",
                    Done = D(e => e.Type == "delete"),
                    Skip = vp => { vp.CloseMenuNow(); vp.DeleteSelectedObjects(); },
                },
                ClickStep("Cube", "真ん中の立方体をクリックして選択してください。", "dup"),
                new TutorialStep
                {
                    Group = "dup", AllowOp = true,
                    Text = "<kbd>Shift</kbd>+<kbd>D</kbd> で複製してください。複製したものがマウスについてきます。",
                    Keys = ["Shift+D"], Highlight = new TutorialHighlight(Object: "Cube"),
                    Done = D(e => e.Type == "duplicate"),
                    Skip = vp => vp.DuplicateSelected(),
                },
                new TutorialStep
                {
                    Group = "dup",
                    Text = "マウスを動かして置き場所を決め、<strong>左クリック</strong>で確定してください（<kbd>Esc</kbd> だと元の位置に重なって置かれます）。",
                    Mouse = "動かして左クリック",
                    Done = D(e => e.Type == "confirm" || e.Type == "cancel"),
                    Skip = vp => vp.ConfirmOperation(),
                },
                new TutorialStep
                {
                    Text = "<kbd>Ctrl</kbd>+<kbd>Z</kbd> で、複製を元に戻してください。",
                    Keys = ["Ctrl+Z"],
                    Done = D(e => e.Type == "undo"),
                    Skip = vp => vp.UndoNow(),
                }),
        });

        // basics/transform
        Add(new TutorialScenario
        {
            Name = "transform", Title = "移動・回転・スケール",
            Steps = S(
                ClickStep("Cube", "まず立方体をクリックして選択してください。", "move-cancel"),
                new TutorialStep
                {
                    Group = "move-cancel", AllowOp = true, Keys = ["G"], Highlight = new TutorialHighlight(Object: "Cube"),
                    Text = "<kbd>G</kbd> を押して移動を始めてください。マウスを動かすと立方体がついてきます。",
                    Done = D(e => e.Type == "opstart" && e.StrIs("kind", "G")),
                    Skip = vp => vp.StartOperation(TransformKind.G),
                },
                new TutorialStep
                {
                    Group = "move-cancel", Keys = ["Esc"], Mouse = "または右クリック",
                    Text = "マウスを動かしてみたら、<kbd>Esc</kbd>（または右クリック）でキャンセルしてください。元の位置に戻ります。",
                    Done = D(e => e.Type == "cancel"),
                    Fail = M(e => e.Type == "confirm" ? "確定されました。キャンセルを試すので、もう一度やりましょう。" : null),
                    Skip = vp => vp.CancelOperation(),
                },
                OpSteps("G", "z", "2", "move", "今度は正確に動かします。<kbd>G</kbd> を押して移動を始めてください。"),
                OpSteps("R", "z", "45", "rotate", "<kbd>R</kbd> を押して回転を始めてください。"),
                OpSteps("S", null, "0.5", "scale", "<kbd>S</kbd> を押してスケールを始めてください。軸を固定しなければ、全体が同じ比率で変わります。"),
                new TutorialStep
                {
                    Keys = ["N"],
                    Text = "<kbd>N</kbd> でサイドバーを開いてください。位置 Z が 2、回転 Z が 45°、スケールが 0.5 になっているはずです。",
                    Done = D(e => e.Type == "npanel" && e.Flag("open")),
                    Skip = vp => vp.ToggleSidebar(true),
                },
                new TutorialStep
                {
                    Keys = ["Ctrl+Z"],
                    Text = "最後に <kbd>Ctrl</kbd>+<kbd>Z</kbd> で、直前のスケールを元に戻してください。",
                    Done = D(e => e.Type == "undo"),
                    Skip = vp => vp.UndoNow(),
                }),
        });

        // modeling/edit-mode
        Add(new TutorialScenario
        {
            Name = "edit-mode", Title = "編集モードの選択", ViewDist = 9,
            Scene = [Obj("sphere", "Sphere", null, [2.4, 2.4, 2.4])],
            Steps = S(
                ClickStep("Sphere", "まず球をクリックして選択してください。"),
                TabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。左上が「編集モード」になり、頂点が点で表示されます。"),
                SelectModeStep("edge"),
                new TutorialStep
                {
                    Group = "loop", Mouse = "Alt+左クリック", Keys = ["Alt"], Highlight = new TutorialHighlight(At: [-1.84, -1.51, 0], Label: "Alt+クリック"),
                    Text = "赤道（真ん中の横の辺）の上で <kbd>Alt</kbd>+クリックして、一周ぶんの辺ループを選んでください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "loop") && e.Counts().Edges >= 30),
                    Hint = M(e =>
                        IsEdit(e) && e.StrIs("source", "loop") ? "縦方向のループになりました。横向きの辺の上で Alt+クリックしてください。" :
                        IsEdit(e) ? "<kbd>Alt</kbd> を押したままクリックします。" : null),
                    Skip = vp => vp.SelectLoopWhere(p => Math.Abs(p.C.Z) < 0.01 && Math.Abs(p.D.Z) < 0.01),
                },
                OpSteps("S", null, "1.2", "loop-scale", "選んだループだけを大きくします。<kbd>S</kbd> を押してください。", target: null),
                SelectModeStep("face"),
                new TutorialStep
                {
                    Keys = ["A"], Text = "<kbd>A</kbd> で、すべての面を選択してください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "all")),
                    Skip = vp => vp.SelectAllItems(true),
                },
                new TutorialStep
                {
                    Keys = ["Alt+A"], Text = "<kbd>Alt</kbd>+<kbd>A</kbd> で、選択を解除してください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "none")),
                    Skip = vp => vp.SelectAllItems(false),
                },
                new TutorialStep
                {
                    Keys = ["Alt+Z"], Highlight = new TutorialHighlight(Tool: "xray"),
                    Text = "<kbd>Alt</kbd>+<kbd>Z</kbd> で透過表示にしてください。裏側の面も選べるようになります。",
                    Done = D(e => e.Type == "xray" && e.Flag("on")),
                    Skip = vp => vp.SetXray(true),
                },
                new TutorialStep
                {
                    Mouse = "左ドラッグで囲む",
                    Text = "何もないところから左ドラッグして、球の <strong>上半分</strong> を囲んでください（裏側の面も選ばれます）。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "box") && e.Counts().Faces >= 100),
                    Hint = M(e => IsEdit(e) && e.StrIs("source", "box") ? $"選べたのは {e.Counts().Faces} 面でした。もっと大きく囲んでください。" : null),
                    Skip = vp => vp.EditSelectWhere(p => p.Center.Z > 0),
                },
                new TutorialStep
                {
                    Keys = ["Alt+Z"], Highlight = new TutorialHighlight(Tool: "xray"),
                    Text = "もう一度 <kbd>Alt</kbd>+<kbd>Z</kbd> で、透過表示を元に戻してください。",
                    Done = D(e => e.Type == "xray" && !e.Flag("on")),
                    Skip = vp => vp.SetXray(false),
                },
                TabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。")),
        });

        // modeling/extrude-inset-loopcut
        Add(new TutorialScenario
        {
            Name = "extrude-inset-loopcut", Title = "押し出し・差し込み・ループカット", ViewDist = 7.5, ViewTarget = [0, 0, 0.4],
            Steps = S(
                ClickStep("Cube", "立方体をクリックして選択してください。"),
                TabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
                SelectModeStep("face"),
                EditClickStep("立方体の <strong>上の面</strong> をクリックして、その面だけを選んでください。", [0, 0, 1], "top",
                    (e, vp, _) => IsEdit(e) && e.Counts().Faces == 1 && UpNormal(vp),
                    M(e => IsEdit(e) ? "上の面（矢印のところ）だけをクリックしてください。" : null),
                    vp => vp.EditSelectWhere(p => p.Normal.Z > 0.9)),
                EditOpSteps("E", "G", true, "1", " m", "押し出す長さ", "extrude",
                    "<kbd>E</kbd> を押して押し出してください。面が引き出され、マウスで長さが変わります（面の向きに固定されています）。", vp => vp.ExtrudeNow()),
                EditOpSteps("I", "I", false, "0.3", " m", "差し込みの幅", "inset",
                    "続けて <kbd>I</kbd> を押して、面の内側に面を差し込んでください。マウスを面の中心に近づけるほど幅が広がります。", vp => vp.InsetNow()),
                EditOpSteps("E", "G", true, "-0.5", " m", "押し出す長さ", "dent",
                    "内側の面を <kbd>E</kbd> で押し出し、マイナスの値で <strong>へこませ</strong> ます。<kbd>E</kbd> を押してください。", vp => vp.ExtrudeNow()),
                LoopCutSteps([-1, -1, 0], 2, p => Math.Abs(p.D.Z) > 0.9 && p.C.X < -0.9 && p.C.Y < -0.9 && Math.Abs(p.C.Z) < 0.1, "手前の左の縦の辺"),
                TabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。")),
        });

        // modeling/bevel-knife-merge
        Add(new TutorialScenario
        {
            Name = "bevel-merge", Title = "ベベル・マージ・面を張る", ViewDist = 7,
            Steps = S(
                ClickStep("Cube", "立方体をクリックして選択してください。"),
                TabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
                SelectModeStep("edge"),
                EditClickStep("上の面の <strong>手前の辺</strong> をクリックして、その辺だけを選んでください。", [0, -1, 1], "edge",
                    D(e => IsEdit(e) && e.Counts().Edges == 1),
                    M(e => IsEdit(e) ? "辺を 1 本だけ選びます。矢印の辺をクリックしてください。" : null),
                    vp => vp.EditSelectWhere(p => p.C.Y < -0.9 && p.C.Z > 0.9)),
                EditOpSteps("Ctrl+B", "B", false, "0.3", " m", "面取りの幅", "bevel",
                    "<kbd>Ctrl</kbd>+<kbd>B</kbd> でベベルを始めてください。マウスを離すほど面取りの幅が広がります。", vp => vp.BevelNow()),
                SelectModeStep("face"),
                EditClickStep("上の面をクリックして選んでください。", [0, 0.2, 1], "dup",
                    (e, vp, _) => IsEdit(e) && e.Counts().Faces == 1 && UpNormal(vp),
                    M(e => IsEdit(e) ? "上の面だけをクリックしてください。" : null),
                    vp => vp.EditSelectWhere(p => p.Normal.Z > 0.9 && p.Center.Z > 0.9)),
                new TutorialStep
                {
                    Group = "dup", Keys = ["E"],
                    Text = "わざと重複した頂点を作ります。<kbd>E</kbd> を押してください。",
                    Done = D(e => e.Type == "opstart" && e.Flag("extrude")),
                    Skip = vp => vp.ExtrudeNow(),
                },
                new TutorialStep
                {
                    Group = "dup", Keys = ["Esc"],
                    Text = "すぐに <kbd>Esc</kbd> でキャンセルしてください。見た目は戻りますが、押し出した面は同じ位置に <strong>重なって残って</strong> います。",
                    Done = D(e => e.Type == "cancel" && e.Flag("extrude")),
                    Fail = M(e => e.Type == "confirm" ? "確定されました。もう一度やりましょう。" : null),
                    Skip = vp => vp.CancelOperation(),
                },
                SelectModeStep("vert"),
                new TutorialStep
                {
                    Keys = ["A"], Text = "<kbd>A</kbd> ですべての頂点を選択してください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "all")),
                    Skip = vp => vp.SelectAllItems(true),
                },
                new TutorialStep
                {
                    Group = "merge", Keys = ["M"], Text = "<kbd>M</kbd> でマージのメニューを出してください。",
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "merge")),
                    Skip = vp => vp.ShowMergeMenuNow(),
                },
                new TutorialStep
                {
                    Group = "merge", Mouse = "メニューをクリック",
                    Text = "「距離で」をクリックしてください。重なった頂点がまとめられ、下に消えた数が出ます。",
                    Done = D(e => e.Type == "merge" && e.StrIs("how", "distance") && (e.Num("removed") ?? 0) > 0),
                    Fail = M(e => e.Type == "merge" && !e.StrIs("how", "distance") ? "「距離で」以外を選びました。" : null),
                    Hint = M(e => e.Type == "merge" ? "重なった頂点がありませんでした。" : null),
                    Skip = vp => { vp.CloseMenuNow(); vp.MergeEditNow("distance"); },
                },
                SelectModeStep("face"),
                EditClickStep("もう一度、上の面をクリックして選んでください。", [0, 0.2, 1], "hole",
                    (e, vp, _) => IsEdit(e) && e.Counts().Faces == 1 && UpNormal(vp),
                    M(e => IsEdit(e) ? "上の面だけをクリックしてください。" : null),
                    vp => vp.EditSelectWhere(p => p.Normal.Z > 0.9 && p.Center.Z > 0.9)),
                new TutorialStep
                {
                    Group = "hole", Keys = ["X"], Text = "<kbd>X</kbd> で削除のメニューを出してください。",
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "delete")),
                    Skip = vp => vp.ShowDeleteMenuNow(),
                },
                new TutorialStep
                {
                    Group = "hole", Mouse = "メニューをクリック", Text = "「面」をクリックして、上の面を削除してください。穴が開きます。",
                    Done = D(e => e.Type == "delete" && e.Flag("edit") && e.StrIs("what", "face")),
                    Fail = M(e => e.Type == "delete" ? "「面」以外で削除しました。" : null),
                    Skip = vp => { vp.CloseMenuNow(); vp.DeleteEditNow("face"); },
                },
                SelectModeStep("edge"),
                new TutorialStep
                {
                    Group = "fill", Mouse = "Alt+左クリック", Keys = ["Alt"], Highlight = new TutorialHighlight(At: [0, -0.7, 1], Label: "Alt+クリック"),
                    Text = "穴のふちの辺を <kbd>Alt</kbd>+クリックして、ふちを一周選んでください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "loop") && e.Counts().Edges == 4),
                    Hint = M(e => IsEdit(e) ? "穴のふち（矢印の辺）を Alt+クリックしてください。" : null),
                    Skip = vp => vp.SelectLoopWhere(p => p.C.Z > 0.99 && p.C.Y < -0.6 && Math.Abs(p.D.X) > 0.9),
                },
                new TutorialStep
                {
                    Group = "fill", Keys = ["F"], Text = "<kbd>F</kbd> で穴に面を張ってください。",
                    Done = D(e => e.Type == "fill" && e.StrIs("made", "face")),
                    Skip = vp => vp.FillNow(),
                },
                TabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。")),
        });

        // modeling/mirror-smooth
        Add(new TutorialScenario
        {
            Name = "mirror", Title = "半分を消してミラーで作る", ViewDist = 7, Numpad = true,
            Steps = S(
                ClickStep("Cube", "立方体をクリックして選択してください。"),
                TabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
                LoopCutSteps([0.4, -1, 1], 1, p => Math.Abs(p.D.X) > 0.9 && p.C.Y < -0.9 && p.C.Z > 0.9, "上の面の手前の辺（左右にのびる辺）", center: true),
                SelectModeStep("face"),
                NumpadStep("1", false, "前", "<kbd>Numpad 1</kbd> で正面から見てください（テンキーがなければ下の「テンキーを模倣」をオンにして <kbd>1</kbd>）。"),
                new TutorialStep
                {
                    Keys = ["Alt+Z"], Highlight = new TutorialHighlight(Tool: "xray"),
                    Text = "<kbd>Alt</kbd>+<kbd>Z</kbd> で透過表示にしてください。裏側の面もまとめて囲めるようになります。",
                    Done = D(e => e.Type == "xray" && e.Flag("on")),
                    Skip = vp => vp.SetXray(true),
                },
                new TutorialStep
                {
                    Group = "half", Mouse = "左ドラッグで囲む",
                    Text = "真ん中の線より <strong>左側</strong>（X がマイナス側）の面を、ドラッグで囲んで選んでください。",
                    Done = (e, vp, _) => IsEdit(e) && e.StrIs("source", "box") && e.Counts().Faces == 5 && vp.GetEditInfo().Faces.All(f => f.Center.X < 0),
                    Hint = (e, vp, _) => IsEdit(e) && e.StrIs("source", "box")
                        ? (vp.GetEditInfo().Faces.Any(f => f.Center.X > 0) ? "右側の面まで入っています。真ん中の線より左だけを囲んでください。" : $"選べたのは {e.Counts().Faces} 面でした（左半分は 5 面）。上下にも大きめに囲んでください。")
                        : null,
                    Skip = vp => vp.EditSelectWhere(p => p.Center.X < -0.01),
                },
                new TutorialStep
                {
                    Group = "half", Keys = ["X"], Text = "<kbd>X</kbd> で削除のメニューを出してください。",
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "delete")),
                    Skip = vp => vp.ShowDeleteMenuNow(),
                },
                new TutorialStep
                {
                    Group = "half", Mouse = "メニューをクリック", Text = "「面」をクリックして、左半分を削除してください。",
                    Done = (e, vp, _) => e.Type == "delete" && e.Flag("edit") && e.StrIs("what", "face") && vp.GetEditInfo().Verts.All(p => p.X > -0.001),
                    Fail = M(e => e.Type == "delete" ? "左半分がきれいに消えませんでした。" : null),
                    Skip = vp => { vp.CloseMenuNow(); vp.DeleteEditNow("face"); },
                },
                TabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
                new TutorialStep
                {
                    Highlight = new TutorialHighlight(Tool: "mirror"), Mouse = "上の「ミラー」をクリック",
                    Text = "上の「ミラー」ボタンを押して、ミラー モディファイアーを付けてください（Blender ではプロパティのモディファイアーから追加します）。左半分が鏡写しで表示されます。",
                    Done = D(e => e.Type == "mirror" && e.Flag("on")),
                    Skip = vp => vp.SetMirror(true),
                }),
        });

        // modeling/topology
        Add(new TutorialScenario
        {
            Name = "topology", Title = "統計を見ながら辺を足す・溶解する", ViewDist = 7,
            Steps = S(
                ClickStep("Cube", "立方体をクリックして選択してください。"),
                new TutorialStep
                {
                    Highlight = new TutorialHighlight(Tool: "stats"), Mouse = "上の「統計」をクリック",
                    Text = "上の「統計」ボタンを押して、頂点・辺・面・三角形の数を表示してください（Blender ではオーバーレイ > 統計）。",
                    Done = D(e => e.Type == "stats" && e.Flag("on")),
                    Skip = vp => vp.SetStats(true),
                },
                TabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。統計が「選択 / 全体」の表示になります。"),
                LoopCutSteps([-1, -1, 0], 1, p => Math.Abs(p.D.Z) > 0.9 && p.C.X < -0.9 && p.C.Y < -0.9 && Math.Abs(p.C.Z) < 0.1, "手前の左の縦の辺", center: true),
                SelectModeStep("edge", "<kbd>2</kbd> で辺選択モードにしてください。面が 6 → 10 に増えたことを統計で確かめましょう。"),
                new TutorialStep
                {
                    Group = "dissolve", Mouse = "Alt+左クリック", Keys = ["Alt"], Highlight = new TutorialHighlight(At: [0, -1, 0], Label: "Alt+クリック"),
                    Text = "今入れた辺（手前の真ん中の横の辺）を <kbd>Alt</kbd>+クリックして、ループを一周選んでください。",
                    Done = D(e => IsEdit(e) && e.StrIs("source", "loop") && e.Counts().Edges == 4),
                    Hint = M(e => IsEdit(e) ? "真ん中の横の辺（矢印）を Alt+クリックしてください。" : null),
                    Skip = vp => vp.SelectLoopWhere(p => Math.Abs(p.C.Z) < 0.01 && p.C.Y < -0.9 && Math.Abs(p.D.X) > 0.9),
                },
                new TutorialStep
                {
                    Group = "dissolve", Keys = ["X"], Text = "<kbd>X</kbd> で削除のメニューを出してください。",
                    Done = D(e => e.Type == "menu" && e.StrIs("menu", "delete")),
                    Skip = vp => vp.ShowDeleteMenuNow(),
                },
                new TutorialStep
                {
                    Group = "dissolve", Mouse = "メニューをクリック",
                    Text = "「辺を溶解」をクリックしてください。穴を開けずに辺だけが消え、面の数が元の 6 に戻ります。",
                    Done = D(e => e.Type == "delete" && e.Flag("edit") && e.StrIs("what", "dissolve")),
                    Fail = M(e => e.Type == "delete" ? "「辺を溶解」以外で削除したので、穴が開きました。" : null),
                    Skip = vp => { vp.CloseMenuNow(); vp.DeleteEditNow("dissolve"); },
                },
                TabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。")),
        });

        return all;
    }
}
