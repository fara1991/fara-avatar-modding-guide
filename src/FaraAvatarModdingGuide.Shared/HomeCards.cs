namespace FaraAvatarModdingGuide.Models;

/// <summary>
/// ホームの分野別カードの文言と状態（静的 HTML の Build と WebAssembly の画面が同じ元を使う）。
/// 状態は「色だけに頼らない」ため、必ず文字（全部読んだ／読んでいる途中／これから）を付ける。
/// </summary>
public static class HomeCards
{
    public const string Done = "done";
    public const string Going = "go";
    public const string Todo = "todo";

    /// <summary>読んだ件数から状態を決める。</summary>
    public static string State(int done, int total) => total > 0 && done >= total ? Done : done > 0 ? Going : Todo;

    public static string StateLabel(string state) => state switch { Done => "全部読んだ", Going => "読んでいる途中", _ => "これから" };

    /// <summary>むずかしさの幅（例: やさしい〜ふつう）。front matter の初級・中級・上級を、やさしい言葉にして、軽い方から重い方へ。</summary>
    public static string DifficultyRange(IEnumerable<string?> difficulties)
    {
        string[] order = ["初級", "中級", "上級"];
        var ranks = difficulties.Select(d => Array.IndexOf(order, d)).Where(i => i >= 0).ToList();
        if (ranks.Count == 0) return "";
        var low = SiteLookups.DifficultyLabel(order[ranks.Min()]);
        var high = SiteLookups.DifficultyLabel(order[ranks.Max()]);
        return low == high ? low : low + "〜" + high;
    }

    /// <summary>カードの 2 行目（例: 5 記事 · やさしい〜ふつう）。</summary>
    public static string Meta(IReadOnlyList<ArticleDto> articles)
    {
        var range = DifficultyRange(articles.Select(a => a.Difficulty));
        return range.Length == 0 ? $"{articles.Count} 記事" : $"{articles.Count} 記事 · {range}";
    }

    /// <summary>区切りの見出しの横（例: 5 テーマ · 24 記事）。記事のあるテーマだけ数える。</summary>
    public static string SectionMeta(SiteContent content, string sectionId)
    {
        var categories = content.CategoriesIn(sectionId).Where(c => content.ArticlesOf(c.Id).Count > 0).ToList();
        return $"{categories.Count} テーマ · {categories.Sum(c => content.ArticlesOf(c.Id).Count)} 記事";
    }

    /// <summary>「読んだ記事」の 1 行（例: 読んだ記事: 16/50（32%））。</summary>
    public static string ReadStat(int done, int total) => $"読んだ記事: {done}/{total}（{(total == 0 ? 0 : (int)Math.Round(done * 100.0 / total))}%）";

    /// <summary>バーの幅（%。小数 2 桁まで。カルチャに依らず '.' 区切り）。</summary>
    public static string Percent(int done, int total) => (total == 0 ? 0 : done * 100.0 / total).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>下に並べる 4 つのリンク（Href, アイコン, 題）。</summary>
    public static readonly IReadOnlyList<(string Href, string Icon, string Title)> Links =
    [
        ("/tips", "Lightbulb", "困ったときのコツ（TIPS）"),
        ("/qa", "Chat", "よくある質問（改変 Q&A）"),
        ("/shortcuts", "Keyboard", "ショートカットキー"),
        ("/changelog", "History", "更新のお知らせ"),
    ];

    public const string SearchPlaceholder = "知りたいことを入力（例: ウェイト、ループカット）";
    public const string Lead = "アバター改変のための Blender・Unity・VRChat の操作ガイドです。気になる記事を選んで、好きな順番で読めます。はじめての方は「はじめに」からどうぞ。";
}
