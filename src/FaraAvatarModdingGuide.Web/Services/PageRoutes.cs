using FaraAvatarModdingGuide.Models;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// WebAssembly で動くページ。ここに無いパス(静的 HTML のままのページ。フェーズ 2 では練習の 3 ページ)への移動は、Blazor の移動ではなく、ブラウザの通常の移動(再読み込み)にする。
/// 固定のページは Build の PageCatalog.WasmPaths と同じ一覧にする。記事・カテゴリは content.json にあるものすべて(SiteLookups.IsContentPath。Build の PageCatalog.IsWasm と同じ規則)。
/// Web.Tests が、Build の全ページ(76 本)・@page のルートと突き合わせる。フェーズが進んで @page を足したら、ここも足す。
/// </summary>
public static class PageRoutes
{
    public static readonly IReadOnlyList<string> Wasm =
    [
        "/", "/learn", "/shortcuts", "/addons", "/glossary", "/howto", "/tips", "/qa", "/links", "/changelog", "/report", "/privacy", "/search",
    ];

    /// <summary>パス(先頭は /。クエリ・ハッシュは除く)が WebAssembly のページか。末尾の / は無視する。content が無いときは、記事・カテゴリは静的 HTML 扱い。</summary>
    public static bool IsWasmPage(string path, SiteContent? content)
    {
        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        return Wasm.Contains(normalized, StringComparer.Ordinal) || (content?.IsContentPath(normalized) ?? false);
    }

    /// <summary>
    /// 移動先の URL を、静的 HTML のページとして読み直すべきか。同じサイトの中のパスで、WebAssembly のページではないとき true
    /// (知らない URL も true: 読み直して 404.html を出す)。別のサイト・別のスキーム(外部リンク)は false(ブラウザに任せる)。
    /// </summary>
    public static bool ShouldReload(string targetUri, string baseUri, SiteContent? content)
    {
        if (!Uri.TryCreate(targetUri, UriKind.Absolute, out var target) || !Uri.TryCreate(baseUri, UriKind.Absolute, out var origin)) return false;
        if (!string.Equals(target.GetLeftPart(UriPartial.Authority), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)) return false;
        return !IsWasmPage(target.AbsolutePath, content);
    }
}
