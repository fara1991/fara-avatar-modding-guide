using System.Text.RegularExpressions;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>手順ごとの作業用の値(scenarios.js の ctx。視点を回した距離の合計・ホイールの回数)。</summary>
public sealed class StepContext
{
    public double Total { get; set; }
    public int Count { get; set; }
}

/// <summary>イベント e で達成か。</summary>
public delegate bool DoneFn(ViewEvent e, ViewportCore vp, StepContext ctx);

/// <summary>ヒント(進まない)・失敗(group の最初からやり直す)の文。出さないなら null。</summary>
public delegate string? MessageFn(ViewEvent e, ViewportCore vp, StepContext ctx);

/// <summary>
/// チュートリアルの手順 1 つ(scenarios.js の step)。Text は HTML(キーは &lt;kbd&gt;)。Keys は画面下に出すキー("Shift+A" は同時押し)、Mouse はマウス操作の説明。
/// Group は失敗・「やり直す」で戻る単位(同じ group の最初の手順に戻る)。AllowOp は、この手順中に G / R / S が始まっても注意を出さない。
/// Skip は「次へ(スキップ)」や再開のときに、この手順を終えた状態にする。
/// </summary>
public sealed class TutorialStep
{
    public string? Group { get; init; }
    public required string Text { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
    public string? Mouse { get; init; }
    public TutorialHighlight? Highlight { get; init; }
    public bool AllowOp { get; init; }
    public required DoneFn Done { get; init; }
    public MessageFn? Hint { get; init; }
    public MessageFn? Fail { get; init; }
    public Action<ViewportCore>? Skip { get; init; }
}

/// <summary>チュートリアル 1 本(scenarios.js の SCENARIOS の値)。</summary>
public sealed class TutorialScenario
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    /// <summary>最初に置くオブジェクト(省略は立方体 1 つ)。</summary>
    public IReadOnlyList<SceneSpec>? Scene { get; init; }
    /// <summary>最初の視点の距離・注視点(省略は既定)。</summary>
    public double? ViewDist { get; init; }
    public double[]? ViewTarget { get; init; }
    /// <summary>「テンキーを模倣」のチェックボックスを出すか。</summary>
    public bool Numpad { get; init; }
    public required IReadOnlyList<TutorialStep> Steps { get; init; }
}

/// <summary>ビューのイベント(viewport.js の emit)の読み取り。JavaScript のオブジェクトと同じに、無い項目は undefined(= 空)として扱う。</summary>
public static class EventExtensions
{
    public static string? Str(this ViewEvent e, string key) => e.Detail.TryGetValue(key, out var v) ? v as string : null;

    /// <summary>項目があり、その値が文字(または null)と等しいか(JavaScript の e.x === "..." / e.x === null)。</summary>
    public static bool StrIs(this ViewEvent e, string key, string? value) => e.Detail.TryGetValue(key, out var v) && (v is null ? value is null : v is string s && value is not null && s == value);

    public static bool Flag(this ViewEvent e, string key) => e.Detail.TryGetValue(key, out var v) && v is true;

    public static double? Num(this ViewEvent e, string key) => e.Detail.TryGetValue(key, out var v) ? v switch { double d => d, int i => i, _ => null } : null;

    public static IReadOnlyList<string> StrList(this ViewEvent e, string key) => e.Detail.TryGetValue(key, out var v) && v is string[] a ? a : [];

    public static bool Is(this ViewEvent e, string type) => e.Type == type;

    /// <summary>select イベントの counts(verts / edges / faces)。</summary>
    public static (int Verts, int Edges, int Faces) Counts(this ViewEvent e)
    {
        if (!e.Detail.TryGetValue("counts", out var v) || v is not IReadOnlyDictionary<string, object?> d) return (0, 0, 0);
        int N(string k) => d.TryGetValue(k, out var x) ? x switch { int i => i, double dd => (int)dd, _ => 0 } : 0;
        return (N("verts"), N("edges"), N("faces"));
    }
}

/// <summary>手順の文(HTML)から文字だけを取り出す(画像の代替テキスト用)。</summary>
public static partial class TutorialText
{
    public static string Plain(string html) => System.Net.WebUtility.HtmlDecode(Tags().Replace(html, ""));

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}
