namespace FaraBlenderManual.Data;

/// <summary>学習ロードマップの1段階。Content/learn/categories.yml で定義し、並び順がそのまま学習順になる。</summary>
public sealed class LearnCategory
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Bootstrap Icons のクラス名（例: bi-arrows-move）</summary>
    public string Icon { get; set; } = "bi-book";
}

/// <summary>Content/learn/&lt;category&gt;/NN-slug.md から読み込んだ1記事。1記事 = 1 URL (/learn/{category}/{slug})。</summary>
public sealed class Article
{
    public required string Category { get; init; }
    public required string Slug { get; init; }
    public required int Order { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string? BlenderVersion { get; init; }
    public DateOnly? Updated { get; init; }
    /// <summary>この記事で使うショートカットキー。shortcuts.yml の key と一致させると一覧ページへリンクできる。</summary>
    public IReadOnlyList<string> Shortcuts { get; init; } = [];
    /// <summary>Markdown から変換済みの本文 HTML</summary>
    public required string Html { get; init; }
    /// <summary>本文のプレーンテキスト（検索インデックス用）</summary>
    public required string PlainText { get; init; }

    public string Url => $"/learn/{Category}/{Slug}";
}

/// <summary>Markdown 先頭の YAML フロントマター。記事ファイルを書く人が触るのはここ。</summary>
public sealed class ArticleFrontMatter
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Difficulty { get; set; }
    public string? BlenderVersion { get; set; }
    public DateOnly? Updated { get; set; }
    public List<string> Shortcuts { get; set; } = [];
}

public sealed class Shortcut
{
    public string Key { get; set; } = string.Empty;
    public string Function { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class Addon
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public string Url { get; set; } = string.Empty;
    /// <summary>対応状況の短いラベル（例: "Blender 4.x 非対応"）。バッジとして表示される。</summary>
    public string? Status { get; set; }
    /// <summary>補足（代替アドオン、インストール方法の違いなど）</summary>
    public string? Note { get; set; }
}

/// <summary>appsettings.json の "Site" セクション</summary>
public sealed class SiteOptions
{
    public string Title { get; set; } = "Blender Manual";
    /// <summary>サイト全体で前提にする Blender のバージョン。各記事は blender_version で上書きできる。</summary>
    public string BlenderVersion { get; set; } = "4.2";
    public string RepositoryUrl { get; set; } = string.Empty;
}
