using System.Globalization;
using System.Text;
using FaraAvatarModdingGuide.Data;
using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Build;

/// <summary>検索の索引の正規化（ビルド時。規則は tests/js/fixtures/searchcore.reference.js の norm() と同じで、tests/js/search-norm.mjs が一致を確かめる）。</summary>
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

    /// <summary>一覧の絞り込み用: NFKC と小文字だけ(旧 SSR 版の tipsfilter.js の norm()。カタカナ→ひらがななどはしない)。</summary>
    public static string NormalizeFilter(string text) => text.Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture);

    public static IReadOnlyList<SearchRecord> From(IEnumerable<SearchEntry> entries) =>
        entries.Select(e => new SearchRecord(
            e.Type, e.Title, e.Subtitle, e.Url, e.Text, e.Reading,
            Normalize(e.Title), Normalize(e.Subtitle), Normalize(e.Text), Normalize(e.Reading))).ToList();
}
