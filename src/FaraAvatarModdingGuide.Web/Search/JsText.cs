using System.Globalization;
using System.Text;

namespace FaraAvatarModdingGuide.Web.Search;

/// <summary>
/// JavaScript と同じ文字列の扱い（searchcore.js と結果をそろえるため）。
/// 文化に依存しない比較（Ordinal）・JavaScript の \s と同じ空白の集合を使う。.NET の char.IsWhiteSpace は、JavaScript の \s と集合が違う（U+0085 など）。
/// </summary>
internal static class JsText
{
    /// <summary>JavaScript の \s（WhiteSpace と LineTerminator）。</summary>
    public static bool IsSpace(char c) =>
        c is '\t' or '\n' or '\v' or '\f' or '\r' or ' '
        || c == (char)0x00A0 || c == (char)0x1680 || (c >= (char)0x2000 && c <= (char)0x200A)
        || c == (char)0x2028 || c == (char)0x2029 || c == (char)0x202F || c == (char)0x205F || c == (char)0x3000 || c == (char)0xFEFF;

    /// <summary>s.split(/\s+/).filter(Boolean)。</summary>
    public static List<string> SplitWords(string s)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i < s.Length; i++)
        {
            if (IsSpace(s[i]))
            {
                if (start >= 0) { words.Add(s[start..i]); start = -1; }
            }
            else if (start < 0) start = i;
        }
        if (start >= 0) words.Add(s[start..]);
        return words;
    }

    public static string ToLower(string s) => s.ToLower(CultureInfo.InvariantCulture);

    /// <summary>JavaScript の escapeHtml と同じ（searchcore.js）。</summary>
    public static string EscapeHtml(string s)
    {
        if (s.AsSpan().IndexOfAny("&<>\"'") < 0) return s;
        var sb = new StringBuilder(s.Length + 16);
        foreach (var c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
