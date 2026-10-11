namespace FaraAvatarGuide.Data;

/// <summary>/search-index.json の1件。検索はクライアント側 (wwwroot/js/search.js) で部分一致により行う。</summary>
public sealed record SearchEntry(
    /// <summary>article / shortcut / addon</summary>
    string Type,
    string Title,
    string? Subtitle,
    string Url,
    /// <summary>本文のプレーンテキスト。スニペット表示と部分一致に使う。</summary>
    string Text);
