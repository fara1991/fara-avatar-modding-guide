namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// WebAssembly 側に @page があるパス。ここに無いパス（静的 HTML のままのページ）への移動は、Blazor の移動ではなく、ブラウザの通常の移動（再読み込み）にする。
/// Build の PageCatalog.WasmPaths と同じ一覧にする（Web.Tests が、@page のルートとも突き合わせる）。フェーズが進んで @page を足したら、ここも足す。
/// </summary>
public static class PageRoutes
{
    public static readonly IReadOnlyList<string> Wasm = ["/", "/links", "/changelog", "/privacy", "/report"];

    /// <summary>パス（先頭は /。クエリ・ハッシュは除く）が WebAssembly のページか。末尾の / は無視する。</summary>
    public static bool IsWasmPage(string path)
    {
        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        return Wasm.Contains(normalized, StringComparer.Ordinal);
    }

    /// <summary>
    /// 移動先の URL を、静的 HTML のページとして読み直すべきか。同じサイトの中のパスで、WebAssembly のページではないとき true。
    /// 別のサイト・別のスキーム（外部リンク）は false（ブラウザに任せる）。
    /// </summary>
    public static bool ShouldReload(string targetUri, string baseUri)
    {
        if (!Uri.TryCreate(targetUri, UriKind.Absolute, out var target) || !Uri.TryCreate(baseUri, UriKind.Absolute, out var origin)) return false;
        if (!string.Equals(target.GetLeftPart(UriPartial.Authority), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)) return false;
        return !IsWasmPage(target.AbsolutePath);
    }
}
