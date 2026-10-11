namespace FaraAvatarModdingGuide.Data;

/// <summary>目次の大きな区切り（Blender / Unity / VRChat など）。Content/learn/sections.yml で定義する。</summary>
public sealed class LearnSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Bootstrap Icons のクラス名</summary>
    public string Icon { get; set; } = "bi-collection";
}

/// <summary>目次の1段階。Content/learn/categories.yml で定義し、並び順がそのまま読む順になる。</summary>
public sealed class LearnCategory
{
    public string Id { get; set; } = string.Empty;
    /// <summary>入る区切り（sections.yml の id）</summary>
    public string Section { get; set; } = "blender";
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
    /// <summary>本文に埋め込んだ操作のチュートリアル（::: practice &lt;名前&gt;）の名前。wwwroot/js/scenarios.js のキー</summary>
    public IReadOnlyList<string> PracticeScenarios { get; init; } = [];

    public string Url => $"/learn/{Category}/{Slug}";
    /// <summary>完了の記録（wwwroot/js/progress.js）に使うキー。例: basics/transform</summary>
    public string Id => $"{Category}/{Slug}";
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
    /// <summary>manual（既定）/ discord（Discord で紹介されたもの）</summary>
    public string Source { get; set; } = "manual";
}

/// <summary>appsettings.json の "Site" セクション</summary>
public sealed class SiteOptions
{
    /// <summary>サイト名。表示はすべてここから取る（appsettings.json の Site:Title。ほかに書かない）</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>サイト全体で前提にする Blender のバージョン。各記事は blender_version で上書きできる。</summary>
    public string BlenderVersion { get; set; } = "4.2";
    /// <summary>Unity の記事で前提にするバージョン</summary>
    public string UnityVersion { get; set; } = "2022.3";
    public string RepositoryUrl { get; set; } = string.Empty;
}

// ---------------------------------------------------------------- TIPS・用語集（Content/tips/*.yml）
// どれも source に "discord" を入れると「Discord より」と表示される（Discord の会話から起こした項目用）。

/// <summary>用語集の 1 語（Content/tips/glossary.yml）</summary>
public sealed class GlossaryEntry
{
    /// <summary>URL のアンカー（/glossary#id）。英小文字・数字・ハイフン</summary>
    public string Id { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    /// <summary>読み（ひらがな）。並び順に使う</summary>
    public string Reading { get; set; } = string.Empty;
    /// <summary>英語 UI での表記（Blender / Unity の画面に出る名前）</summary>
    public string? En { get; set; }
    /// <summary>説明（Markdown。1〜3 文）</summary>
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Shortcuts { get; set; } = [];
    /// <summary>関連する記事（"category/slug"）</summary>
    public List<string> Articles { get; set; } = [];
    /// <summary>関連する用語（id）</summary>
    public List<string> Related { get; set; } = [];
    /// <summary>関連する言葉（用語集に無いもの。文字のまま表示し、検索にも使う）</summary>
    public List<string> Keywords { get; set; } = [];
    /// <summary>詳しい説明（Markdown。「詳しく」を開くと表示）</summary>
    public string? Body { get; set; }
    public string Source { get; set; } = "manual";
}

/// <summary>「あの操作、何て名前？」の 1 件（Content/tips/howto.yml）</summary>
public sealed class HowtoEntry
{
    public string Id { get; set; } = string.Empty;
    /// <summary>やりたいこと（例: 頂点をくっつけたい）</summary>
    public string Question { get; set; } = string.Empty;
    /// <summary>操作の名前（例: マージ）</summary>
    public string Answer { get; set; } = string.Empty;
    public string? AnswerEn { get; set; }
    /// <summary>メニューの場所（日本語 UI）</summary>
    public string? Menu { get; set; }
    /// <summary>メニューの場所（英語 UI）</summary>
    public string? MenuEn { get; set; }
    public List<string> Shortcuts { get; set; } = [];
    /// <summary>補足・似た操作との違い（Markdown）</summary>
    public string? Note { get; set; }
    /// <summary>同じ意味の言い方（検索用）</summary>
    public List<string> Aliases { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> Articles { get; set; } = [];
    /// <summary>関連する用語（id）</summary>
    public List<string> Related { get; set; } = [];
    /// <summary>関連する言葉（用語集に無いもの）</summary>
    public List<string> Keywords { get; set; } = [];
    /// <summary>手順などの詳しい説明（Markdown。「詳しく」を開くと表示）</summary>
    public string? Body { get; set; }
    public string Source { get; set; } = "manual";
}

/// <summary>TIPS（つまずきと直し方）の 1 件（Content/tips/tips.yml）</summary>
public sealed class TipEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>要点（Markdown。症状・原因・直し方の形にならないコツなどはこちらに書く）</summary>
    public string? Summary { get; set; }
    /// <summary>症状（Markdown）</summary>
    public string Symptom { get; set; } = string.Empty;
    /// <summary>原因（Markdown）</summary>
    public string Cause { get; set; } = string.Empty;
    /// <summary>直し方（Markdown。手順は箇条書きで）</summary>
    public string Fix { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Shortcuts { get; set; } = [];
    public List<string> Articles { get; set; } = [];
    /// <summary>関連する用語（id）</summary>
    public List<string> Related { get; set; } = [];
    /// <summary>関連する言葉（用語集に無いもの）</summary>
    public List<string> Keywords { get; set; } = [];
    /// <summary>詳しい説明（Markdown。「詳しく」を開くと表示）</summary>
    public string? Body { get; set; }
    public string Source { get; set; } = "manual";
}

/// <summary>改変 Q&amp;A の 1 件（Content/tips/qa.yml）</summary>
public sealed class QaEntry
{
    public string Id { get; set; } = string.Empty;
    /// <summary>質問・やりたいこと</summary>
    public string Question { get; set; } = string.Empty;
    /// <summary>短い答え（Markdown。1〜3 文）</summary>
    public string Answer { get; set; } = string.Empty;
    /// <summary>詳しい説明・手順（Markdown。「詳しく」を開くと表示）</summary>
    public string? Body { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Shortcuts { get; set; } = [];
    public List<string> Articles { get; set; } = [];
    /// <summary>関連する用語（id）</summary>
    public List<string> Related { get; set; } = [];
    /// <summary>関連する言葉（用語集に無いもの）</summary>
    public List<string> Keywords { get; set; } = [];
    public string Source { get; set; } = "manual";
}

/// <summary>リンク集の 1 件（Content/links.yml）</summary>
public sealed class LinkEntry
{
    /// <summary>URL のアンカー（/links#id）</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>区切り（LinkCategories の id）</summary>
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>補足（対応バージョン、代わりになるものなど）</summary>
    public string? Note { get; set; }
    /// <summary>current（今も使われている）/ old（古い情報・販売終了の可能性）/ 省略（不明）</summary>
    public string? Status { get; set; }
    public string Source { get; set; } = "manual";
}

/// <summary>リンク集の区切り</summary>
public sealed record LinkCategory(string Id, string Title, string Icon, string Description);
