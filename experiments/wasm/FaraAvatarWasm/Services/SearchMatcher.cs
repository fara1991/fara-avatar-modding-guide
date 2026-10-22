using FaraAvatarWasm.Models;

namespace FaraAvatarWasm.Services;

/// <summary>
/// wwwroot/js/searchcore.js の簡易版。スペース区切りの語がすべて含まれる項目を返す。
/// 索引の文字列（SearchEntry.Norm）は ContentExport が NFKC で作る。ブラウザの WebAssembly では
/// string.Normalize が使えない（PlatformNotSupportedException）ので、検索語だけはここで同じ規則にそろえる（全角の英数字・記号・空白、〜、カタカナ→ひらがな）。
/// ローマ字の読み替え・半角カナ・強調・抜き出しは省いている。
/// </summary>
public static class SearchMatcher
{
    public static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c0 in s)
        {
            var c = c0;
            if (c is >= '！' and <= '～') c = (char)(c - 0xFEE0); // 全角の英数字・記号 → 半角
            else if (c == '\u3000') c = ' ';
            c = char.ToLowerInvariant(c);
            if (c == '~') c = '〜';
            if (c is >= 'ァ' and <= 'ヶ') c = (char)(c - 0x60); // カタカナ → ひらがな
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static IReadOnlyList<SearchEntry> Search(IEnumerable<SearchEntry> index, string query, int limit = 50)
    {
        var terms = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0) return [];

        return index
            .Where(e => terms.All(t => e.Norm.Contains(t, StringComparison.Ordinal)))
            // 見出しに含まれるものを先に
            .OrderBy(e => terms.All(t => Normalize(e.Title).Contains(t, StringComparison.Ordinal)) ? 0 : 1)
            .Take(limit)
            .ToList();
    }
}
