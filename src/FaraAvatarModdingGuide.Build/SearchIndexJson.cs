using System.Globalization;
using System.Text;
using FaraAvatarModdingGuide.Data;

namespace FaraAvatarModdingGuide.Build;

/// <summary>
/// data/search.json の 1 件。検索の索引は、ビルド時に正規化した文字列（*Norm）を持つ（計画書 4.4）。
/// ブラウザの WebAssembly では string.Normalize が使えないため。正規化の規則は wwwroot/js/searchcore.js の norm() と同じ
/// （tests/js/search-norm.test.mjs が、同じ入力で同じ結果になることを確かめる）。
/// </summary>
public sealed record SearchRecord(
    string Type, string Title, string? Subtitle, string Url, string Text, string? Reading,
    string TitleNorm, string SubtitleNorm, string TextNorm, string ReadingNorm);

public static class SearchIndexJson
{
    /// <summary>NFKC・小文字・全角チルダを〜に・カタカナをひらがなに（searchcore.js の norm() と同じ）。</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var folded = text.Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture).Replace('~', '〜');
        var sb = new StringBuilder(folded.Length);
        foreach (var ch in folded) sb.Append(ch is >= 'ァ' and <= 'ヶ' ? (char)(ch - 0x60) : ch);
        return sb.ToString();
    }

    public static IReadOnlyList<SearchRecord> From(IEnumerable<SearchEntry> entries) =>
        entries.Select(e => new SearchRecord(
            e.Type, e.Title, e.Subtitle, e.Url, e.Text, e.Reading,
            Normalize(e.Title), Normalize(e.Subtitle), Normalize(e.Text), Normalize(e.Reading))).ToList();
}
