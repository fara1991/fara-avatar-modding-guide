using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// 読み終えたページの記録（ログインなし。このブラウザの localStorage にだけ保存する）。旧 SSR 版の progress.js と同じキー・同じ形式で、
/// 既に保存されている記録がそのまま読める。
///   キー "avatar-modding-guide.progress"  値 { "カテゴリ/スラッグ": 完了日時（ISO 8601）, ... }
/// 読み方は progress.js と同じ: オブジェクト以外（配列・壊れた JSON）は空として扱う。値は JavaScript の真偽（空文字・0・false・null は未完了）で見る。
/// 書くときは、ほかの項目（知らない id・形式が違う値）を消さずに残す。
/// </summary>
public sealed class ProgressStore : IDisposable
{
    public const string Key = "avatar-modding-guide.progress";

    /// <summary>練習のチュートリアルの記録（tutorial.js が書く。フェーズ 3）。「記録を消す」で一緒に消す（progress.js の reset と同じ）。</summary>
    public const string TutorialsKey = "avatar-modding-guide.tutorials";

    private readonly IJSRuntime _js;
    private JsonObject _data = new();
    private bool _loaded;
    private DotNetObjectReference<ProgressStore>? _self;

    public ProgressStore(IJSRuntime js) => _js = js;

    /// <summary>記録が変わったとき（このタブで変えたとき・ほかのタブで変わったとき）。</summary>
    public event Action? Changed;

    /// <summary>保存された記録を読み、ほかのタブでの変更（storage イベント）を見張る。最初に使う前に 1 回呼ぶ（2 回目以降は何もしない）。</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _data = Parse(ReadStorage());
        if (_js is IJSInProcessRuntime sync)
        {
            try
            {
                _self = DotNetObjectReference.Create(this);
                sync.InvokeVoid("avatarStorage.watch", Key, _self);
                sync.InvokeVoid("avatarStorage.bridge", _self);
            }
            catch (JSException) { /* 見張れなくても、このタブの記録は動く */ }
        }
    }

    /// <summary>JavaScript の load(): JSON のオブジェクトならそのまま、それ以外は空。</summary>
    public static JsonObject Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new JsonObject();
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return new JsonObject();
        }
    }

    /// <summary>JavaScript の真偽値の規則（!!value）。</summary>
    public static bool Truthy(JsonNode? node)
    {
        if (node is null) return false;
        if (node is JsonValue value)
        {
            switch (node.GetValueKind())
            {
                case JsonValueKind.True: return true;
                case JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined: return false;
                case JsonValueKind.String: return value.GetValue<string>().Length > 0;
                case JsonValueKind.Number:
                    return !double.TryParse(node.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) || d != 0;
                default: return true;
            }
        }
        return true;   // オブジェクト・配列
    }

    public bool IsDone(string id)
    {
        EnsureLoaded();
        return _data.TryGetPropertyValue(id, out var node) && Truthy(node);
    }

    /// <summary>この一覧のうち、完了しているものの数。</summary>
    public int CountDone(IEnumerable<string> ids)
    {
        EnsureLoaded();
        return ids.Count(IsDone);
    }

    /// <summary>完了にする（すでに真の値があればそのまま。無ければ今の時刻）／未完了にする（項目を消す）。</summary>
    public void Set(string id, bool done)
    {
        EnsureLoaded();
        if (done)
        {
            if (!(_data.TryGetPropertyValue(id, out var existing) && Truthy(existing)))
                _data[id] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            _data.Remove(id);
        }
        Save();
        Changed?.Invoke();
    }

    /// <summary>記録をすべて消す（練習の記録も一緒に）。</summary>
    public void Reset()
    {
        EnsureLoaded();
        _data = new JsonObject();
        Save();
        if (_js is IJSInProcessRuntime sync)
        {
            try { sync.InvokeVoid("localStorage.removeItem", TutorialsKey); }
            catch (JSException) { /* 保存が使えないとき */ }
        }
        Changed?.Invoke();
    }

    private string? ReadStorage()
    {
        if (_js is not IJSInProcessRuntime sync) return null;
        try { return sync.Invoke<string?>("localStorage.getItem", Key); }
        catch (JSException) { return null; }   // 保存が使えない（プライベートモード等）
    }

    private void Save()
    {
        if (_js is not IJSInProcessRuntime sync) return;
        try { sync.InvokeVoid("localStorage.setItem", Key, _data.ToJsonString()); }
        catch (JSException) { /* 保存できなくても、このページの表示は変える */ }
    }

    /// <summary>練習の JavaScript(tutorial.js。フェーズ 3)が呼ぶ window.faraProgress.isDone(id)。SSR 版の progress.js の API をそのまま残す(js/storage.js が橋渡しする)。</summary>
    [JSInvokable]
    public bool IsDoneForScript(string id) => IsDone(id);

    /// <summary>練習の JavaScript が呼ぶ window.faraProgress.set(id, done)。表示(目次の印など)も、すぐに替わる。</summary>
    [JSInvokable]
    public void SetForScript(string id, bool done) => Set(id, done);

    /// <summary>ほかのタブで記録が変わったとき（avatarStorage.watch の storage イベント）。</summary>
    [JSInvokable]
    public void OnStorageChanged()
    {
        _data = Parse(ReadStorage());
        Changed?.Invoke();
    }

    public void Dispose() => _self?.Dispose();
}
