using System.Text.Json.Nodes;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.JSInterop;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>localStorage（キー avatar-modding-guide.progress）の読み書きを、JavaScript の入れ物の代わりに、メモリの入れ物で試す。</summary>
internal sealed class FakeStorageJs : IJSInProcessRuntime
{
    public readonly Dictionary<string, string> Storage = new();
    public readonly List<string> Calls = new();
    public bool Broken;

    public TValue Invoke<TValue>(string identifier, params object?[]? args)
    {
        Calls.Add(identifier);
        if (Broken && identifier.StartsWith("localStorage", StringComparison.Ordinal)) throw new JSException("denied");
        switch (identifier)
        {
            case "localStorage.getItem":
                return (TValue)(object?)(Storage.TryGetValue((string)args![0]!, out var v) ? v : null)!;
            case "localStorage.setItem":
                Storage[(string)args![0]!] = (string)args![1]!;
                return default!;
            case "localStorage.removeItem":
                Storage.Remove((string)args![0]!);
                return default!;
            default:
                return default!;   // avatarStorage.watch など
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(Invoke<TValue>(identifier, args));

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
}

/// <summary>
/// 完了の記録（既存の localStorage の値が、そのまま読める）。SSR 版の wwwroot/js/progress.js と同じ規則: オブジェクト以外は空・値は JavaScript の真偽・知らない項目は残す・
/// 完了は「既に真の値があればそのまま、なければ ISO 8601 の今の時刻」・記録を消すと練習の記録も消える。
/// </summary>
public class ProgressStoreTests
{
    private const string Key = ProgressStore.Key;

    /// <summary>SSR 版が書いた形（progress.js が書く JSON。値は new Date().toISOString()）。</summary>
    private const string SsrWritten = "{\"basics/select\":\"2026-10-01T09:30:00.123Z\",\"modeling/edit-mode\":\"2026-10-02T10:00:00.000Z\"}";

    private static (ProgressStore Store, FakeStorageJs Js) Create(string? stored)
    {
        var js = new FakeStorageJs();
        if (stored is not null) js.Storage[Key] = stored;
        return (new ProgressStore(js), js);
    }

    [Fact]
    public void A_record_written_by_the_ssr_version_is_read_as_it_is()
    {
        var (store, _) = Create(SsrWritten);
        Assert.True(store.IsDone("basics/select"));
        Assert.True(store.IsDone("modeling/edit-mode"));
        Assert.False(store.IsDone("basics/transform"));
        Assert.Equal(1, store.CountDone(["basics/select", "basics/transform"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[\"basics/select\"]")]        // 配列
    [InlineData("\"basics/select\"")]          // 文字列
    [InlineData("null")]
    [InlineData("12")]
    public void Anything_that_is_not_an_object_is_treated_as_empty(string? stored)
    {
        var (store, _) = Create(stored);
        Assert.False(store.IsDone("basics/select"));
    }

    [Theory]
    [InlineData("\"2026-10-01\"", true)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("-1", true)]
    [InlineData("{}", true)]
    [InlineData("[]", true)]
    [InlineData("\"\"", false)]                // 空文字は JavaScript では偽
    [InlineData("0", false)]
    [InlineData("0.0", false)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    public void Values_follow_javascript_truthiness(string valueJson, bool done)
    {
        var (store, _) = Create("{\"a/b\":" + valueJson + "}");
        Assert.Equal(done, store.IsDone("a/b"));
    }

    [Fact]
    public void Marking_done_writes_an_iso_timestamp_and_keeps_every_other_entry_untouched()
    {
        var (store, js) = Create("{\"basics/select\":\"2026-10-01T09:30:00.123Z\",\"x/old\":0,\"x/unknown\":{\"nested\":[1,2]},\"y/z\":\"keep\"}");
        var changed = 0;
        store.Changed += () => changed++;
        store.Set("basics/transform", true);
        var saved = JsonNode.Parse(js.Storage[Key])!.AsObject();
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", saved["basics/transform"]!.GetValue<string>());
        Assert.Equal("2026-10-01T09:30:00.123Z", saved["basics/select"]!.GetValue<string>());
        Assert.Equal(0, saved["x/old"]!.GetValue<int>());
        Assert.Equal("[1,2]", saved["x/unknown"]!["nested"]!.ToJsonString());
        Assert.Equal("keep", saved["y/z"]!.GetValue<string>());
        Assert.Equal(1, changed);
        Assert.True(store.IsDone("basics/transform"));
    }

    [Fact]
    public void Marking_done_again_keeps_the_original_time_but_replaces_a_falsy_value()
    {
        var (store, js) = Create("{\"basics/select\":\"2026-10-01T09:30:00.123Z\",\"a/zero\":0}");
        store.Set("basics/select", true);
        Assert.Equal("2026-10-01T09:30:00.123Z", JsonNode.Parse(js.Storage[Key])!["basics/select"]!.GetValue<string>());
        store.Set("a/zero", true);                                   // 0 は未完了なので、完了にすると時刻になる（progress.js の v[id] || now と同じ）
        Assert.IsType<string>(JsonNode.Parse(js.Storage[Key])!["a/zero"]!.GetValue<string>());
    }

    [Fact]
    public void Marking_undone_removes_only_that_entry()
    {
        var (store, js) = Create(SsrWritten);
        store.Set("basics/select", false);
        var saved = JsonNode.Parse(js.Storage[Key])!.AsObject();
        Assert.False(saved.ContainsKey("basics/select"));
        Assert.True(saved.ContainsKey("modeling/edit-mode"));
        Assert.False(store.IsDone("basics/select"));
    }

    [Fact]
    public void Reset_writes_an_empty_object_and_removes_the_tutorial_record_too()
    {
        var (store, js) = Create(SsrWritten);
        js.Storage[ProgressStore.TutorialsKey] = "{\"transform\":true}";
        store.Reset();
        Assert.Equal("{}", js.Storage[Key]);
        Assert.False(js.Storage.ContainsKey(ProgressStore.TutorialsKey));
        Assert.False(store.IsDone("basics/select"));
    }

    [Fact]
    public void A_change_made_in_another_tab_is_picked_up()
    {
        var (store, js) = Create(SsrWritten);
        Assert.False(store.IsDone("x/y"));
        var changed = 0;
        store.Changed += () => changed++;
        js.Storage[Key] = "{\"x/y\":\"2026-10-03T00:00:00.000Z\"}";
        store.OnStorageChanged();                                    // storage イベント（js/storage.js）から呼ばれる
        Assert.True(store.IsDone("x/y"));
        Assert.False(store.IsDone("basics/select"));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void When_storage_is_unavailable_the_page_still_works_for_this_visit()
    {
        var js = new FakeStorageJs { Broken = true };
        var store = new ProgressStore(js);
        Assert.False(store.IsDone("a/b"));
        store.Set("a/b", true);                                      // 保存は失敗しても、例外にしない
        Assert.True(store.IsDone("a/b"));                            // このページの中では、完了になる
    }

    [Fact]
    public void Storage_is_watched_for_changes_from_other_tabs()
    {
        var (store, js) = Create(null);
        store.EnsureLoaded();
        Assert.Contains("avatarStorage.watch", js.Calls);
        Assert.Contains("avatarStorage.bridge", js.Calls);           // window.faraProgress（練習の tutorial.js が使う）
    }
}
