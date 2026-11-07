namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// Unity 風の画面のチュートリアル 2 本(scenarios.js の "unity-basics" と "ma-outfit")。
/// 文・キー・光らせる場所・達成 / ヒント / 失敗の条件・スキップの内容を、JavaScript 版と同じにする(tests の UnityTutorialParityTests が突き合わせる)。
/// </summary>
public static class UnityTutorialScenarios
{
    public static IReadOnlyList<UnityTutorialScenario> All => [UnityBasics, MaOutfit];

    public static UnityTutorialScenario? Find(string name) => All.FirstOrDefault(s => s.Name == name);

    private static bool Empty(string? s) => string.IsNullOrEmpty(s);

    private static void SetPosX(UnityEditorCore ed, double value)
    {
        var n = ed.FindByName("Avatar");
        if (n is not null) ed.SetTransformValue(n.Id, "p", 0, value);
    }

    /// <summary>/lilToon|Red/.test(e.material)(material が無いときは文字 "undefined" を調べる JavaScript と同じ)。</summary>
    private static bool IsLilOrRed(ViewEvent e)
    {
        var m = e.Str("material") ?? (e.Detail.TryGetValue("material", out var v) && v is null ? "null" : "undefined");
        return m.Contains("lilToon", StringComparison.Ordinal) || m.Contains("Red", StringComparison.Ordinal);
    }

    public static UnityTutorialScenario UnityBasics { get; } = new()
    {
        Name = "unity-basics",
        Title = "Unity の画面を動かしてみる",
        Instances = ["Avatar"],
        Steps =
        [
            new UnityTutorialStep
            {
                Mouse = "▶ をクリック", Highlight = new UnityHighlight(Hierarchy: "Avatar", Label: "▶ をクリック"),
                Text = "Hierarchy の「Avatar」の左にある ▶ をクリックして、中身を開いてください。",
                Done = (e, ed, ctx) => e.Is("expand") && e.Str("name") == "Avatar" && e.Flag("expanded"),
                Skip = ed => ed.ExpandByName("Avatar"),
            },
            new UnityTutorialStep
            {
                Mouse = "左クリック", Highlight = new UnityHighlight(Hierarchy: "Body", Label: "クリック"),
                Text = "「Body」をクリックして選んでください。右の Inspector に、そのオブジェクトの設定（コンポーネント）が表示されます。",
                Done = (e, ed, ctx) => e.Is("select") && e.Str("name") == "Body",
                Hint = (e, ed, ctx) => e.Is("select") ? "Avatar の中の「Body」をクリックしてください。" : null,
                Skip = ed => ed.SelectByName("Avatar/Body"),
            },
            new UnityTutorialStep
            {
                Keys = ["F"], Highlight = new UnityHighlight(Scene: true, Label: "ここにマウスを置いて F"),
                Text = "Scene ビューにマウスを置いて <kbd>F</kbd> を押すと、選んだものが画面の中心に大きく表示されます（Frame Selected）。",
                Done = (e, ed, ctx) => e.Is("view") && e.Str("action") == "frame",
                Skip = ed => ed.FrameSelected(),
            },
            new UnityTutorialStep
            {
                Keys = ["Alt"], Mouse = "Alt+左ドラッグ",
                Text = "<kbd>Alt</kbd> を押しながら左ドラッグで、視点を回してください（Blender のホイールボタンのドラッグにあたります）。",
                Done = (e, ed, ctx) => e.Is("view") && e.Str("action") == "orbit",
                Skip = ed => ed.OrbitBy(0.6),
            },
            new UnityTutorialStep
            {
                Mouse = "ホイールを回す",
                Text = "ホイールを回して、近づいたり離れたりしてください。",
                Done = (e, ed, ctx) => e.Is("view") && e.Str("action") == "zoom" && ++ctx.Count >= 2,
                Skip = ed => ed.View.Dist *= 0.8,
            },
            new UnityTutorialStep
            {
                Mouse = "ホイールボタンでドラッグ",
                Text = "ホイールボタン（中ボタン）を押したままドラッグして、視点を平行移動してください。",
                Done = (e, ed, ctx) => e.Is("view") && e.Str("action") == "pan",
                Skip = ed => ed.View.Target = new Vec3(ed.View.Target.X + 0.2, ed.View.Target.Y, ed.View.Target.Z),
            },
            new UnityTutorialStep
            {
                Mouse = "左クリック", Highlight = new UnityHighlight(Hierarchy: "Avatar", Label: "クリック"),
                Text = "Hierarchy で「Avatar」をクリックして選んでください。",
                Done = (e, ed, ctx) => e.Is("select") && e.Str("name") == "Avatar",
                Skip = ed => ed.SelectByName("Avatar"),
            },
            new UnityTutorialStep
            {
                Keys = ["W"], Highlight = new UnityHighlight(Tool: "move"),
                Text = "<kbd>W</kbd> で移動ツールにしてください。Scene に赤・緑・青の矢印（X / Y / Z）が出ます。",
                Done = (e, ed, ctx) => e.Is("tool") && e.Str("tool") == "move",
                Skip = ed => ed.SetTool("move"),
            },
            new UnityTutorialStep
            {
                Mouse = "赤い矢印をドラッグ", Highlight = new UnityHighlight(Scene: true, Label: "赤い矢印をドラッグ"),
                Text = "赤い矢印をドラッグして、アバターを横（X 方向）に動かしてください。",
                Done = (e, ed, ctx) => e.Is("transform") && e.Str("kind") == "move",
                Hint = (e, ed, ctx) => e.Is("select") && Empty(e.Str("name")) ? "矢印の先の三角の部分をつかんでドラッグしてください。" : null,
                Skip = ed => SetPosX(ed, 0.5),
            },
            new UnityTutorialStep
            {
                Highlight = new UnityHighlight(Inspector: "p-0", Label: "0 と入力して Enter"),
                Text = "Inspector の Transform の <strong>Position の X</strong> に <kbd>0</kbd> と入力して <kbd>Enter</kbd>。元の位置に戻ります。数値で正確に置けるのは Blender のサイドバー（<kbd>N</kbd>）と同じです。",
                Done = (e, ed, ctx) => e.Is("edit") && e.Str("prop") == "p" && e.Num("axis") == 0 && e.Num("value") == 0,
                Hint = (e, ed, ctx) => e.Is("edit") ? "Position の X（いちばん左の欄）に 0 を入れてください。" : null,
                Skip = ed => SetPosX(ed, 0),
            },
            new UnityTutorialStep
            {
                Keys = ["E"], Highlight = new UnityHighlight(Tool: "rotate"),
                Text = "<kbd>E</kbd> で回転ツールにしてください（輪をドラッグで回転）。",
                Done = (e, ed, ctx) => e.Is("tool") && e.Str("tool") == "rotate",
                Skip = ed => ed.SetTool("rotate"),
            },
            new UnityTutorialStep
            {
                Keys = ["R"], Highlight = new UnityHighlight(Tool: "scale"),
                Text = "<kbd>R</kbd> で拡大縮小ツールにしてください（Blender の S にあたります）。",
                Done = (e, ed, ctx) => e.Is("tool") && e.Str("tool") == "scale",
                Skip = ed => ed.SetTool("scale"),
            },
            new UnityTutorialStep
            {
                Keys = ["Q"], Highlight = new UnityHighlight(Tool: "hand"),
                Text = "<kbd>Q</kbd> で手のツールにしてください。左ドラッグで視点を平行移動できます。",
                Done = (e, ed, ctx) => e.Is("tool") && e.Str("tool") == "hand",
                Skip = ed => ed.SetTool("hand"),
            },
        ],
    };

    public static UnityTutorialScenario MaOutfit { get; } = new()
    {
        Name = "ma-outfit",
        Title = "Modular Avatar で衣装を着せる",
        Instances = ["Avatar"],
        Steps =
        [
            new UnityTutorialStep
            {
                Group = "put", Mouse = "Avatar の上へドラッグ", Highlight = new UnityHighlight(Project: "Hoodie", Label: "Hierarchy の Avatar へドラッグ"),
                Text = "下の Project ウィンドウの衣装「Hoodie」を、Hierarchy の「Avatar」の <strong>上</strong> までドラッグして離してください。衣装がアバターの中（子）に入ります。",
                Done = (e, ed, ctx) => (e.Is("instantiate") && e.Str("asset") == "Hoodie" && e.Str("parentName") == "Avatar")
                    || (e.Is("reparent") && e.Str("name") == "Hoodie" && e.Str("parentName") == "Avatar"),
                Hint = (e, ed, ctx) =>
                    e.Is("instantiate") && Empty(e.Str("parentName")) ? "いちばん外（ルート）に置かれました。Hierarchy の Hoodie を、Avatar の上にドラッグして入れてください。" :
                    e.Is("instantiate") ? $"「{e.Str("parentName")}」の中に入りました。Ctrl+Z で戻して、Avatar の上で離してください。" : null,
                Skip = ed => ed.Instantiate("Hoodie", ed.FindByName("Avatar")?.Id),
            },
            new UnityTutorialStep
            {
                Group = "setup", Mouse = "右クリック", Highlight = new UnityHighlight(Hierarchy: "Hoodie", Label: "右クリック"),
                Text = "Hierarchy の「Hoodie」を <strong>右クリック</strong> してください。",
                Done = (e, ed, ctx) => e.Is("contextmenu") && e.Str("name") == "Hoodie",
                Hint = (e, ed, ctx) => e.Is("contextmenu") ? "「Hoodie」（衣装のいちばん上）で右クリックしてください。" : null,
                Skip = ed => ed.ContextMenuByName("Avatar/Hoodie"),
            },
            new UnityTutorialStep
            {
                Group = "setup", Mouse = "メニューをクリック", Highlight = new UnityHighlight(Popup: "setup-outfit", Label: "クリック"),
                Text = "メニューの <strong>Modular Avatar ▸ Setup Outfit</strong> を選んでください。衣装のアーマチュアに <strong>MA Merge Armature</strong> が付き、アップロード時にアバターのボーンへ統合されます。",
                Done = (e, ed, ctx) => e.Is("setupOutfit") && e.Flag("ok"),
                Hint = (e, ed, ctx) => e.Is("setupOutfit") ? e.Str("reason") : null,
                Fail = (e, ed, ctx) => e.Is("select") ? "メニューが閉じました。" : null,
                Skip = ed => ed.SetupOutfit(ed.FindByName("Avatar/Hoodie")?.Id),
            },
            new UnityTutorialStep
            {
                Mouse = "左クリック", Highlight = new UnityHighlight(Hierarchy: "Shirt_Default", Label: "クリック"),
                Text = "次は、アバターが最初から着ている服を外します。Hierarchy の「Avatar」の中の <strong>Shirt_Default</strong> を選んでください。",
                Done = (e, ed, ctx) => e.Is("select") && e.Str("name") == "Shirt_Default",
                Hint = (e, ed, ctx) => e.Is("select") ? "Avatar の ▶ を開いて、「Shirt_Default」を選んでください。" : null,
                Skip = ed => ed.SelectByName("Avatar/Shirt_Default"),
            },
            new UnityTutorialStep
            {
                Mouse = "チェックを外す", Highlight = new UnityHighlight(Inspector: "active", Label: "チェックを外す"),
                Text = "Inspector の左上のチェックを外して、Shirt_Default を無効にしてください。Scene から消えます。",
                Done = (e, ed, ctx) => e.Is("active") && e.Str("name") == "Shirt_Default" && !e.Flag("active"),
                Skip = ed => ed.SetActive(ed.FindByName("Avatar/Shirt_Default")!.Id, false),
            },
            new UnityTutorialStep
            {
                Mouse = "Tag を選ぶ", Highlight = new UnityHighlight(Inspector: "tag", Label: "EditorOnly を選ぶ"),
                Text = "無効にしただけでは、アップロードに含まれて重さ（ランク）にも数えられます。<strong>Tag</strong> を <strong>EditorOnly</strong> にして、アップロードから外してください。",
                Done = (e, ed, ctx) => e.Is("tag") && e.Str("name") == "Shirt_Default" && e.Str("tag") == "EditorOnly",
                Hint = (e, ed, ctx) => e.Is("tag") ? "Tag の一覧から EditorOnly を選んでください。" : null,
                Skip = ed => ed.SetTag(ed.FindByName("Avatar/Shirt_Default")!.Id, "EditorOnly"),
            },
            new UnityTutorialStep
            {
                Mouse = "左クリック", Highlight = new UnityHighlight(Hierarchy: "Hoodie", Label: "▶ を開いて Hoodie_Mesh を選ぶ"),
                Text = "衣装の見た目（マテリアル）を変えます。Hoodie の ▶ を開いて <strong>Hoodie_Mesh</strong> を選んでください（Scene でパーカーをクリックしても選べます）。",
                Done = (e, ed, ctx) => e.Is("select") && e.Str("name") == "Hoodie_Mesh",
                Hint = (e, ed, ctx) => e.Is("select") ? "Hoodie の中の「Hoodie_Mesh」を選んでください。" : null,
                Skip = ed => ed.SelectByName("Hoodie/Hoodie_Mesh"),
            },
            new UnityTutorialStep
            {
                Mouse = "Element 0 へドラッグ", Highlight = new UnityHighlight(Project: "Hoodie_lilToon", Label: "Inspector の Element 0 へドラッグ"),
                Text = "Project の <strong>Hoodie_lilToon</strong> を、Inspector の Skinned Mesh Renderer の <strong>Materials &gt; Element 0</strong> の欄までドラッグしてください。",
                Done = (e, ed, ctx) => e.Is("material") && e.Str("name") == "Hoodie_Mesh" && IsLilOrRed(e),
                Hint = (e, ed, ctx) => e.Is("material") ? "Hoodie_Mesh を選んだ状態で、Element 0 の欄で離してください。" : null,
                Skip = ed => ed.AssignMaterial(ed.FindByName("Hoodie/Hoodie_Mesh")!.Id, 0, "Hoodie_lilToon"),
            },
            new UnityTutorialStep
            {
                Mouse = "Add Component", Highlight = new UnityHighlight(Inspector: "add-component", Label: "クリックして blend と入力"),
                Text = "体型の変更（ブレンドシェイプ）に服を合わせます。Inspector の <strong>Add Component</strong> を押し、<kbd>blend</kbd> と入力して <strong>MA Blendshape Sync</strong> を選んでください。",
                Done = (e, ed, ctx) => e.Is("componentAdded") && e.Str("component") == "MABlendshapeSync",
                Hint = (e, ed, ctx) => e.Is("componentAdded") ? "MA Blendshape Sync を追加してください。" : null,
                Skip = ed => ed.AddComponent(ed.FindByName("Hoodie/Hoodie_Mesh")!.Id, "MABlendshapeSync"),
            },
            new UnityTutorialStep
            {
                Group = "sdk", Mouse = "メニューをクリック", Highlight = new UnityHighlight(Menu: "VRChat SDK", Label: "クリック"),
                Text = "上のメニューの <strong>VRChat SDK</strong> を開いてください。",
                Done = (e, ed, ctx) => e.Is("menu") && e.Str("menu") == "VRChat SDK",
                Skip = ed => ed.OpenMenu("VRChat SDK", new UnityAnchor("below", Selector: "[data-menu=\"VRChat SDK\"]")),
            },
            new UnityTutorialStep
            {
                Group = "sdk", Mouse = "クリック", Highlight = new UnityHighlight(Popup: "sdk-panel", Label: "クリック"),
                Text = "<strong>Show Control Panel</strong> を選ぶと、アップロード用の画面（Builder）が開きます。パフォーマンスランクと、確認のメッセージを見てください。",
                Done = (e, ed, ctx) => e.Is("sdk") && e.Flag("open"),
                Skip = ed => ed.OpenSdk(),
            },
            new UnityTutorialStep
            {
                Mouse = "クリック", Highlight = new UnityHighlight(Sdk: "build-test", Label: "クリック"),
                Text = "<strong>Build &amp; Test</strong> を押して、自分の PC の VRChat だけで試します（練習なので実際には送信しません）。腕を振ったとき、衣装がついてくれば成功です。",
                Done = (e, ed, ctx) => e.Is("build") && e.Str("kind") == "test",
                Skip = ed => ed.Build("test"),
            },
        ],
    };
}
