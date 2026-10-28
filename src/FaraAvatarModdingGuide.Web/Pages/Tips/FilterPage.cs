using FaraAvatarModdingGuide.Web.Search;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Pages.Tips;

/// <summary>
/// 用語集・何て名前？・TIPS・改変 Q&amp;A の絞り込み（文字とタグ）の土台。旧 SSR 版の tipsfilter.js と同じ動き:
///   ・文字: 空白で区切った語が、すべて含まれる項目だけ（NFKC・小文字にそろえて、部分一致）。項目の表示される文字（折りたたみの中・別名を含む）が対象
///   ・タグ: ?tag=Unity のように URL でも指定できる（押すと URL を置き換える）。source:discord は「Discord より」
///   ・#id で飛んできたとき: その項目が隠れないように絞り込みを外し、「詳しく」を開いて、その項目へスクロールする
/// 項目の文字は data/filter.json（Build が書いたもの）。
/// </summary>
public abstract class FilterPage : ComponentBase, IDisposable
{
    [Inject] protected ContentStore Store { get; set; } = default!;
    [Inject] protected FilterTextStore Texts { get; set; } = default!;
    [Inject] protected ISearchNormalizer Normalizer { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    /// <summary>URL の ?tag=。</summary>
    [SupplyParameterFromQuery(Name = "tag")] public string? TagParam { get; set; }

    /// <summary>種類（data/filter.json のキー）: glossary / howto / tip / qa。</summary>
    protected abstract string Kind { get; }

    /// <summary>入力している文字。</summary>
    protected string Query { get; private set; } = "";

    /// <summary>選んでいるタグ（空はすべて）。</summary>
    protected string Tag { get; private set; } = "";

    /// <summary>「詳しく」を開いている項目（#id で飛んできたもの）。</summary>
    protected string? OpenId { get; private set; }

    private Dictionary<string, string>? _texts;
    private string? _seenTagParam;
    private string? _pendingScrollId;
    private string[] _terms = [];

    private DotNetObjectReference<FilterPage>? _self;

    protected override async Task OnInitializedAsync()
    {
        SyncTag();   // OnParametersSet より先に動く(読み込みが同期で済むと、絞り込みの判定が、URL のタグを見る前になる)
        Nav.LocationChanged += OnLocationChanged;
        // 同じページの中の # のリンク・戻る/進むでは、LocationChanged が起きないので、js/page.js の通知も受ける
        try
        {
            _self = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("avatarPage.watchHash", _self);
        }
        catch (JSException) { /* 通知が受けられなくても、最初に開いたときと、別のページから来たときは動く */ }
        var all = await Texts.GetAsync();
        _texts = all is not null && all.TryGetValue(Kind, out var map) ? map : null;
        ApplyHash();
    }

    protected override void OnParametersSet() => SyncTag();

    private void SyncTag()
    {
        // URL の ?tag= が変わったときだけ、選んでいるタグに反映する（#id で絞り込みを外したあとに、同じ値で上書きし直さない）
        if (TagParam != _seenTagParam)
        {
            _seenTagParam = TagParam;
            Tag = TagParam ?? "";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingScrollId is null) return;
        var id = _pendingScrollId;
        _pendingScrollId = null;
        await JS.InvokeVoidAsync("avatarPage.scrollToId", id);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (ApplyHash()) _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>js/page.js から: #id が変わった(同じページの中のリンク・戻る/進む)。</summary>
    [JSInvokable]
    public void OnHashChanged()
    {
        if (ApplyHash()) _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>この種類の項目のタグと出どころ(#id が、この一覧の項目か・いまの絞り込みで隠れているかを調べる)。この一覧に無い id は null。</summary>
    protected abstract (IReadOnlyList<string> Tags, string Source)? Meta(string id);

    /// <summary>URL の #id が、この一覧の項目なら、「詳しく」を開いてそこへ動かす。いまの絞り込みで隠れているときだけ、絞り込みを外す(SSR 版の tipsfilter.js と同じ)。変わったら true。</summary>
    private bool ApplyHash()
    {
        var uri = new Uri(Nav.Uri);
        var id = Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));
        if (id.Length == 0 || Meta(id) is not { } meta) return false;
        if (!IsShown(id, meta.Tags, meta.Source))
        {
            Tag = "";
            _seenTagParam = TagParam;   // URL の ?tag= は残っているが、この絞り込みは外す(OnParametersSet が、同じ値で上書きし直さない)
            Query = "";
            _terms = [];
        }
        OpenId = id;
        _pendingScrollId = id;
        return true;
    }

    /// <summary>入力の文字を変えた。</summary>
    protected void SetQuery(string? value)
    {
        Query = value ?? "";
        _terms = Query.Length == 0 ? [] : JsText.SplitWords(Normalizer.NormFilter(Query)).ToArray();   // 空白で区切った語（JavaScript の split(/\s+/) と同じ集合）
    }

    /// <summary>タグを選んだ。URL を置き換える（履歴は増やさない。SSR 版の history.replaceState と同じ）。</summary>
    protected void SetTag(string tag)
    {
        Tag = tag;
        _seenTagParam = tag.Length == 0 ? null : tag;
        Nav.NavigateTo(Nav.GetUriWithQueryParameter("tag", tag.Length == 0 ? null : tag), replace: true);
    }

    /// <summary>この項目を表示するか（タグと文字の両方に合うもの）。</summary>
    protected bool IsShown(string id, IReadOnlyList<string> tags, string source)
    {
        var okTag = Tag.Length == 0 || (Tag == "source:discord" ? source == "discord" : tags.Contains(Tag, StringComparer.Ordinal));
        if (!okTag) return false;
        if (_terms.Length == 0) return true;
        // 項目の文字(data/filter.json)が読めなかった・この項目が無いときは、文字では絞らない(全件が消えて、何も読めなくなるよりよい)
        if (_texts is null || !_texts.TryGetValue(id, out var text)) return true;
        return _terms.All(t => text.Contains(t, StringComparison.Ordinal));
    }

    protected string CountText(int total, int shown) => shown == total ? $"{total} 件" : $"{total} 件中 {shown} 件";

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        if (_self is not null)
        {
            if (JS is IJSInProcessRuntime sync)
            {
                try { sync.InvokeVoid("avatarPage.unwatchHash", _self); }
                catch (JSException) { }   // 画面を閉じる途中の解放。失敗しても、ほかの解放は続ける
            }
            _self.Dispose();
        }
    }
}
