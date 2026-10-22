using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FaraSearchIsland;

/// <summary>
/// 検索の判定と強調（wwwroot/js/searchcore.js の C# 移植）。
/// 同じ index と同じ語で JS と同じ結果・同じ順番になることを目標にする（パリティは試作の検証項目）。
/// </summary>
public static class SearchCore
{
    public sealed class Indexed(SearchEntry entry)
    {
        public SearchEntry Entry { get; } = entry;
        public string T { get; } = Norm(entry.Title);
        public string S { get; } = Norm(entry.Subtitle ?? "");
        public string X { get; } = Norm(entry.Text ?? "");
    }

    public sealed record Hit(SearchEntry Entry, double Score);

    public readonly record struct Segment(string Text, bool Mark);

    // 半角カナ（FF61〜FF9D）の、全角への対応（濁点・半濁点は Compat で合わせる）
    private const string HalfKana = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン";
    private static readonly Dictionary<char, char> Dakuten = Pairs("カガキギクグケゲコゴサザシジスズセゼソゾタダチヂツヅテデトドハバヒビフブヘベホボウヴ");
    private static readonly Dictionary<char, char> Handakuten = Pairs("ハパヒピフプヘペホポ");

    private static Dictionary<char, char> Pairs(string s)
    {
        var d = new Dictionary<char, char>();
        for (var i = 0; i + 1 < s.Length; i += 2) d[s[i]] = s[i + 1];
        return d;
    }

    /// <summary>
    /// NFKC の代わり。.NET の string.Normalize(FormKC) は WASM では変換せず、そのまま返す（試作で確認。ICU の有無にかかわらず）。
    /// そのため検索で使う範囲だけを自前で変換する: 全角の英数字と記号（FF01〜FF5E）、全角スペース、半角カナ（濁点・半濁点つき）。
    /// これ以外の互換文字（丸数字など）は変えない（JS の NFKC との差。試作の制約）。
    /// </summary>
    public static string Compat(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c >= '！' && c <= '～') sb.Append((char)(c - 0xFEE0));
            else if (c == '　') sb.Append(' ');
            else if (c >= '｡' && c <= 'ﾝ')
            {
                var k = HalfKana[c - '｡'];
                var next = i + 1 < s.Length ? s[i + 1] : '\0';
                if (next == 'ﾞ' && Dakuten.TryGetValue(k, out var v)) { sb.Append(v); i++; }
                else if (next == 'ﾟ' && Handakuten.TryGetValue(k, out var hv)) { sb.Append(hv); i++; }
                else sb.Append(k);
            }
            else if (c == 'ﾞ') sb.Append('゙');
            else if (c == 'ﾟ') sb.Append('゚');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>全角・半角、大文字・小文字をそろえる（JS の norm と同じ順番。NFKC の部分は Compat）</summary>
    public static string Norm(string s)
    {
        var n = Compat(s).ToLowerInvariant().Replace("~", "〜");
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
        {
            // カタカナ（ァ-ヶ）をひらがなにそろえる
            sb.Append(c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c);
        }
        return sb.ToString();
    }

    // ---- ローマ字 → ひらがな ----
    private static readonly Dictionary<string, string> Romaji = BuildRomaji();

    private static Dictionary<string, string> BuildRomaji()
    {
        var t = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string keys, string kana)
        {
            var k = keys.Split(' ');
            var h = kana.Split(' ');
            for (var i = 0; i < k.Length; i++) t[k[i]] = h[i];
        }
        Add("a i u e o", "あ い う え お");
        Add("ka ki ku ke ko", "か き く け こ");
        Add("sa si shi su se so", "さ し し す せ そ");
        Add("ta ti chi tu tsu te to", "た ち ち つ つ て と");
        Add("na ni nu ne no", "な に ぬ ね の");
        Add("ha hi hu fu he ho", "は ひ ふ ふ へ ほ");
        Add("ma mi mu me mo", "ま み む め も");
        Add("ya yu yo", "や ゆ よ");
        Add("ra ri ru re ro", "ら り る れ ろ");
        Add("wa wo", "わ を");
        Add("ga gi gu ge go", "が ぎ ぐ げ ご");
        Add("za zi ji zu ze zo", "ざ じ じ ず ぜ ぞ");
        Add("da di du de do", "だ ぢ づ で ど");
        Add("ba bi bu be bo", "ば び ぶ べ ぼ");
        Add("pa pi pu pe po", "ぱ ぴ ぷ ぺ ぽ");
        Add("kya kyu kyo", "きゃ きゅ きょ");
        Add("sha shu sho sya syu syo", "しゃ しゅ しょ しゃ しゅ しょ");
        Add("cha chu cho tya tyu tyo cya cyu cyo", "ちゃ ちゅ ちょ ちゃ ちゅ ちょ ちゃ ちゅ ちょ");
        Add("nya nyu nyo hya hyu hyo mya myu myo rya ryu ryo", "にゃ にゅ にょ ひゃ ひゅ ひょ みゃ みゅ みょ りゃ りゅ りょ");
        Add("gya gyu gyo bya byu byo pya pyu pyo dya dyu dyo", "ぎゃ ぎゅ ぎょ びゃ びゅ びょ ぴゃ ぴゅ ぴょ ぢゃ ぢゅ ぢょ");
        Add("ja ju jo jya jyu jyo zya zyu zyo", "じゃ じゅ じょ じゃ じゅ じょ じゃ じゅ じょ");
        Add("fa fi fe fo va vi vu ve vo", "ふぁ ふぃ ふぇ ふぉ ゔぁ ゔぃ ゔ ゔぇ ゔぉ");
        Add("wi we thi dhi", "うぃ うぇ てぃ でぃ");
        Add("xa xi xu xe xo la li lu le lo", "ぁ ぃ ぅ ぇ ぉ ぁ ぃ ぅ ぇ ぉ");
        Add("xya xyu xyo lya lyu lyo xtu xtsu ltu ltsu", "ゃ ゅ ょ ゃ ゅ ょ っ っ っ っ");
        return t;
    }

    private static readonly Regex RomajiOnly = new("^[a-z'-]+$", RegexOptions.CultureInvariant);

    /// <summary>ローマ字（a-z と ' -）なら、ひらがなにした文字列。変換できないときは null</summary>
    public static string? RomajiToHiragana(string src)
    {
        var s = src.ToLowerInvariant();
        if (!RomajiOnly.IsMatch(s)) return null;
        var sb = new StringBuilder();
        var i = 0;
        var n = s.Length;
        while (i < n)
        {
            var c = s[i];
            if (c == '-') { sb.Append('ー'); i++; continue; }
            if (c == '\'') { i++; continue; }
            // 促音: 同じ子音が続く（n と母音は除く）
            if (i + 1 < n && s[i + 1] == c && "aiueon".IndexOf(c) < 0) { sb.Append('っ'); i++; continue; }
            var hit = false;
            for (var len = 4; len >= 1; len--)
            {
                if (i + len > n) continue;
                if (Romaji.TryGetValue(s.Substring(i, len), out var kana))
                {
                    sb.Append(kana);
                    i += len;
                    hit = true;
                    break;
                }
            }
            if (hit) continue;
            if (c == 'n')
            {
                sb.Append('ん');
                i++;
                if (i < n && s[i] == '\'') i++;
                continue;
            }
            return null;
        }
        return sb.ToString();
    }

    /// <summary>検索語の別の形。ローマ字に見える語（3 文字以上）は、ひらがなにした形も探す</summary>
    public static List<string> Alternatives(string term)
    {
        var alts = new List<string> { term };
        if (term.Length >= 3)
        {
            var k = RomajiToHiragana(term);
            if (k is not null && k != term && k.Length >= 2) alts.Add(k);
        }
        return alts;
    }

    /// <summary>1 文字ずつ正規化した文字列と、正規化後の位置から元の位置への対応</summary>
    private static (string Normed, List<int> Map) FoldMap(string text)
    {
        var sb = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            // 単独のサロゲートは正規化せず、そのまま通す（JS の 1 文字ずつの norm と同じ結果）
            var n = char.IsSurrogate(c) ? c.ToString() : Norm(c.ToString());
            foreach (var ch in n)
            {
                sb.Append(ch);
                map.Add(i);
            }
        }
        return (sb.ToString(), map);
    }

    /// <summary>検索語ごとの、探す形の一覧（どれか 1 つが含まれればよい）</summary>
    public static List<List<string>> TermGroups(string query) =>
        Regex.Split(Norm(query), @"\s+", RegexOptions.CultureInvariant)
            .Where(x => x.Length > 0)
            .Select(Alternatives)
            .ToList();

    /// <summary>強調・抜き出し用に、すべての形を 1 列にしたもの</summary>
    private static List<string> Terms(string query)
    {
        var all = new List<string>();
        foreach (var g in TermGroups(query))
            foreach (var t in g)
                if (!all.Contains(t, StringComparer.Ordinal)) all.Add(t);
        return all;
    }

    /// <summary>1 つの語の点数（含まれなければ 0）</summary>
    private static int ScoreOf(Indexed e, string t)
    {
        if (e.T == t) return 30;
        if (e.T.StartsWith(t, StringComparison.Ordinal)) return 15;
        if (e.T.Contains(t, StringComparison.Ordinal)) return 10;
        if (e.S.Contains(t, StringComparison.Ordinal)) return 5;
        if (e.X.Contains(t, StringComparison.Ordinal)) return 1;
        return 0;
    }

    // 同じくらい一致したときは、記事や用語を先に出す（JS の TYPE_BONUS と同じ値）
    private static readonly Dictionary<string, double> TypeBonus = new()
    {
        ["article"] = 0.6, ["glossary"] = 0.5, ["howto"] = 0.5, ["tip"] = 0.4,
        ["qa"] = 0.4, ["shortcut"] = 0.2, ["addon"] = 0, ["link"] = 0,
    };

    /// <summary>すべての語を含むものを、タイトル > 小見出し > 本文の順に点数を付けて並べる（安定ソート）</summary>
    public static List<Hit> Search(IReadOnlyList<Indexed> index, string query)
    {
        var gs = TermGroups(query);
        if (gs.Count == 0) return [];
        var hits = new List<Hit>();
        foreach (var e in index)
        {
            double score = 0;
            foreach (var g in gs)
            {
                var best = 0;
                foreach (var t in g) best = Math.Max(best, ScoreOf(e, t));
                if (best == 0) { score = 0; break; }
                score += best;
            }
            if (score > 0)
            {
                hits.Add(new Hit(e.Entry, score + (TypeBonus.TryGetValue(e.Entry.Type, out var b) ? b : 0)));
            }
        }
        return hits.OrderByDescending(h => h.Score).ToList();
    }

    /// <summary>一致した部分を強調する区間に分ける（JS の highlight と同じ位置を囲む）</summary>
    public static List<Segment> Highlight(string text, string query)
    {
        var ts = Terms(query);
        if (ts.Count == 0) return [new Segment(text, false)];
        var (normed, map) = FoldMap(text);
        var pattern = new Regex(
            string.Join("|", ts.OrderByDescending(t => t.Length).Select(Regex.Escape)),
            RegexOptions.CultureInvariant);

        var segments = new List<Segment>();
        var last = 0;
        foreach (Match m in pattern.Matches(normed))
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

    /// <summary>本文から、最初に一致した語のまわりを切り出す</summary>
    public static string Snippet(string text, string query, int width = 120)
    {
        text ??= "";
        var (normed, map) = FoldMap(text);
        var ts = Terms(query);
        var pos = -1;
        for (var i = 0; i < ts.Count && pos < 0; i++) pos = normed.IndexOf(ts[i], StringComparison.Ordinal);
        if (pos < 0)
        {
            return text[..Math.Min(width, text.Length)] + (text.Length > width ? "…" : "");
        }
        pos = map[pos];
        var start = Math.Max(0, pos - width / 3);
        var end = Math.Min(text.Length, start + width);
        return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
    }
}
