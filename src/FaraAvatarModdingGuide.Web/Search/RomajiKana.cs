using System.Text;

namespace FaraAvatarModdingGuide.Web.Search;

/// <summary>
/// ローマ字 → ひらがな（検索語用）と、長音の書き方の揺れの吸収。tests/js/fixtures/searchcore.reference.js の romajiToHiragana・looseKana の移植。
/// 同じ入力で同じ結果になることを、Web.Tests の検索の突き合わせ（SearchParityTests）が確かめる。
/// </summary>
public static class RomajiKana
{
    private static readonly Dictionary<string, string> Table = BuildTable();

    private static Dictionary<string, string> BuildTable()
    {
        var t = new Dictionary<string, string>(StringComparer.Ordinal);
        // 同じキーが後で出てきたら上書きする（searchcore.js の add と同じ）
        void Add(string keys, string kana)
        {
            var k = keys.Split(' ');
            var v = kana.Split(' ');
            for (var i = 0; i < k.Length; i++) t[k[i]] = v[i];
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

    /// <summary>ローマ字をひらがなにする。変換できない文字が残るときは null（searchcore.js の romajiToHiragana）。</summary>
    public static string? ToHiragana(string src)
    {
        var s = JsText.ToLower(src);
        if (!IsRomajiText(s)) return null;
        var sb = new StringBuilder();
        int i = 0, n = s.Length;
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
                if (i + len > n) continue;   // JS の substr は短く返るので、len と長さが合わず一致しない
                var k = s.Substring(i, len);
                if (Table.TryGetValue(k, out var kana)) { sb.Append(kana); i += len; hit = true; break; }
            }
            if (hit) continue;
            var next = i + 1 < n ? s[i + 1] : '\0';
            var after = i + 2 < n ? s[i + 2] : 'x';
            // nn は「ん」1 つ（kennri → けんり）
            if (c == 'n' && next == 'n' && "aiueoy".IndexOf(after) < 0) { sb.Append('ん'); i += 2; continue; }
            // 母音・y の前以外の n
            if (c == 'n')
            {
                sb.Append('ん'); i++;
                if (i < n && s[i] == '\'') i++;
                continue;
            }
            return null;
        }
        return sb.ToString();
    }

    /// <summary>/^[a-z'-]+$/（空は不一致）。</summary>
    private static bool IsRomajiText(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s) if (!(c is >= 'a' and <= 'z' or '\'' or '-')) return false;
        return true;
    }

    private const string LongO = "おこそとのほもよろごぞどぼぽょぉ";

    /// <summary>
    /// 長音の書き方の揺れ（ひょうき・ひょき、ー）を同じに扱う。お段＋う・お（ほうき→ほき）だけ。えい（せいじん）は別の語になるので、そろえない
    /// （searchcore.js の looseKana）。
    /// </summary>
    public static string Loose(string s)
    {
        var t = s.Replace("ー", "", StringComparison.Ordinal);
        var sb = new StringBuilder(t.Length);
        for (var i = 0; i < t.Length; i++)
        {
            var c = t[i];
            var prev = i > 0 ? t[i - 1] : '\0';
            if ((c == 'う' || c == 'お') && prev != '\0' && LongO.IndexOf(prev) >= 0) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
