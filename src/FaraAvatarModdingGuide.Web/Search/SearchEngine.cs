using System.Text;
using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Web.Search;

/// <summary>検索の結果の 1 件（点数が高いほど上）。</summary>
public sealed record SearchHit(SearchRecord Entry, double Score);

/// <summary>強調表示の 1 区間。Match が true の部分を &lt;mark&gt; で囲む。</summary>
public readonly record struct Segment(string Text, bool Match);

/// <summary>
/// サイト内検索（一致の判定・並べ方・強調・抜き出し）。tests/js/fixtures/searchcore.reference.js の C# への移植で、同じ入力から同じ件数・同じ順序・同じ強調を返す
/// （Web.Tests の SearchParityTests が、searchcore.js を Node で動かした結果と、約 150 件の検索語で突き合わせる）。
/// 日本語は単語区切りがないため形態素解析はせず、スペース区切りの各語が「すべて含まれる」ものを一致とする。
/// 全角・半角の違い（Ｇ と G、ｶﾅ とカナ）は NFKC で吸収し、カタカナはひらがなにそろえる。ローマ字（adoon）も、ひらがな（あどおん）にした形で探す。
/// 索引の正規化（TitleNorm など）はビルド時に済んでいる。検索語の正規化（norm）は、ブラウザでは JavaScript の normalize（計画書 D9）を渡す。
/// 比べ方は全部 Ordinal（文化に依存しない）。並べ替えは安定（同点は索引の順）。
/// </summary>
public sealed class SearchEngine
{
    private sealed record Prepared(SearchRecord Record, string T, string S, string X, string R);

    private readonly Prepared[] _index;
    private readonly Func<string, string> _norm;
    private readonly Dictionary<char, string> _charFold = new();

    /// <param name="records">data/search.json の中身。</param>
    /// <param name="norm">検索語の正規化（NFKC・小文字・全角チルダを〜に・カタカナをひらがなに。searchcore.js の norm と同じ）。</param>
    public SearchEngine(IReadOnlyList<SearchRecord> records, Func<string, string> norm)
    {
        _norm = norm;
        _index = records.Select(r => new Prepared(r, r.TitleNorm, r.SubtitleNorm, r.TextNorm, r.ReadingNorm)).ToArray();
    }

    public int Count => _index.Length;

    // ------------------------------------------------------------ 検索語

    private readonly record struct Alt(string T, bool Roman);

    /// <summary>検索語の別の形。ローマ字に見える語（3 文字以上）は、ひらがなにした形も探す。</summary>
    private static List<Alt> Alternatives(string term)
    {
        var alts = new List<Alt> { new(term, false) };
        if (term.Length >= 3)
        {
            var k = RomajiKana.ToHiragana(term);
            // ローマ字から変換した形（ひらがな）。2 文字のかなは、本文・題名の部分一致には使わず、読みの一致だけに使う（you → 溶解 のような誤ヒットを防ぐ）
            if (k is not null && !string.Equals(k, term, StringComparison.Ordinal) && k.Length >= 2) alts.Add(new Alt(k, true));
        }
        return alts;
    }

    /// <summary>検索語ごとの、探す形の一覧。語ごとに、どれか 1 つが含まれればよい。</summary>
    private List<List<Alt>> TermGroups(string query) =>
        JsText.SplitWords(_norm(query)).Select(Alternatives).ToList();

    /// <summary>強調・抜き出し用に、すべての形を 1 列にしたもの。</summary>
    private List<string> Terms(string query)
    {
        var all = new List<string>();
        foreach (var g in TermGroups(query))
        {
            foreach (var a in g)
            {
                if (a.Roman && a.T.Length <= 2) continue;   // 2 文字の変換形は、文字として一致しないので強調しない
                if (!all.Contains(a.T, StringComparer.Ordinal)) all.Add(a.T);
            }
        }
        return all;
    }

    // ------------------------------------------------------------ 点数

    /// <summary>読み（ひらがな）との一致の点数。1 文字は見ない。2 文字のかなは完全一致と前方一致だけ（「ぎゅ」で「ほんぎゅう」に当てない）。</summary>
    private static int ReadingScore(Prepared e, string t)
    {
        if (e.R.Length == 0 || t.Length < 2) return 0;
        var key = RomajiKana.Loose(t);
        var rk = RomajiKana.Loose(e.R);
        if (string.Equals(rk, key, StringComparison.Ordinal)) return 30;
        // 2 文字のかなは前方一致だけ（5 点。部分一致はしない）
        if (key.Length <= 2) return rk.IndexOf(key, StringComparison.Ordinal) == 0 ? 5 : 0;
        var at = rk.IndexOf(key, StringComparison.Ordinal);
        if (at == 0) return 15;
        return at >= 0 ? 10 : 0;
    }

    /// <summary>検索語の 1 つの形の点数。ローマ字から変換した 2 文字のかなは、読みの完全・前方一致だけ。</summary>
    private static int GroupScore(Prepared e, Alt a) => a.Roman && a.T.Length <= 2 ? ReadingScore(e, a.T) : ScoreOf(e, a.T);

    /// <summary>1 つの語の点数（含まれなければ 0）。読みは題名の一致の後、小見出し・本文より前。</summary>
    private static int ScoreOf(Prepared e, string t)
    {
        if (string.Equals(e.T, t, StringComparison.Ordinal)) return 30;
        var at = e.T.IndexOf(t, StringComparison.Ordinal);
        if (at == 0) return 15;
        if (at >= 0) return 10;
        var r = ReadingScore(e, t);
        if (r != 0) return r;
        if (e.S.IndexOf(t, StringComparison.Ordinal) >= 0) return 5;
        if (e.X.IndexOf(t, StringComparison.Ordinal) >= 0) return 1;
        return 0;
    }

    /// <summary>同じくらい一致したときは、記事や用語を先に出す。</summary>
    private static double TypeBonus(string type) => type switch
    {
        "article" => 0.6,
        "category" => 0.5,
        "glossary" => 0.5,
        "howto" => 0.5,
        "tip" => 0.4,
        "qa" => 0.4,
        "shortcut" => 0.2,
        _ => 0,
    };

    /// <summary>すべての語を含むものを、タイトル &gt; 小見出し &gt; 本文の順に点数を付けて並べる（同点は索引の順）。</summary>
    public IReadOnlyList<SearchHit> Search(string query)
    {
        var gs = TermGroups(query);
        if (gs.Count == 0) return [];
        var hits = new List<SearchHit>();
        foreach (var e in _index)
        {
            var score = 0;
            foreach (var g in gs)
            {
                var best = 0;
                foreach (var a in g) best = Math.Max(best, GroupScore(e, a));
                if (best == 0) { score = 0; break; }
                score += best;
            }
            if (score > 0) hits.Add(new SearchHit(e.Record, score + TypeBonus(e.Record.Type)));
        }
        return hits.OrderByDescending(h => h.Score).ToList();   // OrderBy は安定（JavaScript の sort と同じ）
    }

    // ------------------------------------------------------------ 強調・抜き出し

    /// <summary>1 文字ずつ正規化した文字列と、正規化後の位置から元の位置への対応（… が ... のように文字数が変わっても、位置がずれないように）。</summary>
    private (string Normed, List<int> Map) FoldMap(string text)
    {
        var normed = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var n = FoldChar(text[i]);
            normed.Append(n);
            for (var k = 0; k < n.Length; k++) map.Add(i);
        }
        return (normed.ToString(), map);
    }

    private string FoldChar(char c)
    {
        if (c < 0x80) return c == '~' ? "〜" : JsText.ToLower(c.ToString());   // ASCII の NFKC は変わらない。小文字にして、~ は 〜 にそろえる
        if (char.IsSurrogate(c)) return c.ToString();                            // 片側だけのサロゲートは、JavaScript の normalize でも変わらない
        if (_charFold.TryGetValue(c, out var cached)) return cached;
        return _charFold[c] = _norm(c.ToString());
    }

    /// <summary>一致した部分を強調する区間に分ける。全角・半角が違っても一致するように、1 文字ずつ正規化した文字列で位置を探す。</summary>
    public IReadOnlyList<Segment> Highlight(string? text, string query)
    {
        text ??= "";
        var ts = Terms(query);
        if (ts.Count == 0) return text.Length == 0 ? [] : [new Segment(text, false)];
        var (normed, map) = FoldMap(text);
        var pattern = new Regex(string.Join("|", ts.OrderByDescending(t => t.Length).Select(Regex.Escape)), RegexOptions.CultureInvariant);
        var segments = new List<Segment>();
        var last = 0;
        for (var m = pattern.Match(normed); m.Success; m = m.NextMatch())
        {
            if (m.Length == 0) continue;
            var s = map[m.Index];
            var e = map[m.Index + m.Length - 1] + 1;
            if (s < last) continue;
            if (s > last) segments.Add(new Segment(text[last..s], false));
            segments.Add(new Segment(text[s..e], true));
            last = e;
        }
        if (last < text.Length) segments.Add(new Segment(text[last..], false));
        return segments;
    }

    /// <summary>区間を、searchcore.js の highlight と同じ HTML にする（突き合わせ用。画面は区間をそのまま描く）。</summary>
    public static string ToHtml(IEnumerable<Segment> segments)
    {
        var sb = new StringBuilder();
        foreach (var seg in segments)
            sb.Append(seg.Match ? "<mark>" + JsText.EscapeHtml(seg.Text) + "</mark>" : JsText.EscapeHtml(seg.Text));
        return sb.ToString();
    }

    /// <summary>本文から、最初に一致した語のまわりを切り出す。</summary>
    public string Snippet(string? text, string query, int width = 120)
    {
        text ??= "";
        var (normed, map) = FoldMap(text);
        var ts = Terms(query);
        var pos = -1;
        for (var i = 0; i < ts.Count && pos < 0; i++) pos = normed.IndexOf(ts[i], StringComparison.Ordinal);
        if (pos < 0) return text.Length > width ? text[..width] + "…" : text;
        pos = map[pos];   // 元の文字列の位置に戻す
        var start = Math.Max(0, pos - width / 3);
        var end = Math.Min(text.Length, start + width);
        return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
    }
}
