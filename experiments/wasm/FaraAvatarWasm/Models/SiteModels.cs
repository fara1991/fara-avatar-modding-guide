namespace FaraAvatarWasm.Models;

// ContentExport（本体の ContentService を使って書き出した JSON）を読む型。本体の Models.cs と違い、読むだけの最小限。

public sealed class SiteContent
{
    public List<LearnSection> Sections { get; set; } = [];
    public List<LearnCategory> Categories { get; set; } = [];
    /// <summary>目次の順（カテゴリの順、同じカテゴリの中は番号の順）。前後の記事もこの順。</summary>
    public List<ArticleEntry> Articles { get; set; } = [];
}

public sealed class LearnSection
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
}

public sealed class LearnCategory
{
    public string Id { get; set; } = "";
    public string Section { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
}

public sealed class ArticleEntry
{
    public string Category { get; set; } = "";
    public string Slug { get; set; } = "";
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string? BlenderVersion { get; set; }
    public string? Updated { get; set; }
    public List<string> Shortcuts { get; set; } = [];
    /// <summary>本文の HTML（Markdig で変換済み。画像の URL は絶対パスに直してある）</summary>
    public string Html { get; set; } = "";
    public string PlainText { get; set; } = "";
    public List<string> PracticeScenarios { get; set; } = [];
    public string Url { get; set; } = "";
}

/// <summary>/search-index.json と同じ形（本体の SearchEntry）</summary>
public sealed class SearchEntry
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>検索用の正規化済みの文字列（ContentExport が NFKC・小文字・カタカナ→ひらがなで作る）</summary>
    public string Norm { get; set; } = "";
}
