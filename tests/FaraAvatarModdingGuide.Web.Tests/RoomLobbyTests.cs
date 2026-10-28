using Bunit;
using FaraAvatarModdingGuide.Web.Pages;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// みんなで練習のロビー（練習の部屋の一覧・作成の指定・鍵の入力）の画面の振る舞い。一覧は偽物（IRoomDirectory）で差し替える。
/// 部屋の中（3D・WebSocket）は、ブラウザ（tools/room-check）で確かめる。
/// </summary>
public class RoomLobbyTests : WebTestContext
{
    /// <summary>一覧の偽物。呼ばれた回数と、返す一覧（または例外）を変えられる。</summary>
    private sealed class FakeDirectory : IRoomDirectory
    {
        public List<RoomListItem> Rooms { get; set; } = [];
        public Exception? Fail { get; set; }
        public int Calls { get; private set; }

        public Task<IReadOnlyList<RoomListItem>> ListAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (Fail is not null) return Task.FromException<IReadOnlyList<RoomListItem>>(Fail);
            return Task.FromResult<IReadOnlyList<RoomListItem>>(Rooms);
        }
    }

    private readonly FakeDirectory directory = new();
    private bool entrySet;
    private bool directorySet;

    /// <summary>入口（practice-entry.js）の模擬。1 回だけ設定する（タッチだけの端末の判定などは、PracticeTests と同じく、ここでは扱わない）。</summary>
    private void SetupEntry()
    {
        if (entrySet) return;
        entrySet = true;
        var module = JSInterop.SetupModule("./js/practice-entry.js");
        module.SetupVoid("start", _ => true).SetVoidResult();
        module.SetupVoid("startArticle", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <summary>ロビー（?room= なし）を描く。withDirectory なら一覧の偽物を入れる（一度だけ登録する）。</summary>
    private IRenderedComponent<PracticeRoom> RenderLobby(bool withDirectory = true)
    {
        SetupEntry();
        if (withDirectory && !directorySet)
        {
            directorySet = true;
            Services.AddSingleton<IRoomDirectory>(directory);
        }
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/practice/room");
        return Render<PracticeRoom>();
    }

    [Fact]
    public void The_list_shows_loading_first_and_then_the_rooms_with_name_count_lock_and_age()
    {
        var created = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        directory.Rooms =
        [
            new RoomListItem("roomaaaa1111", "夏の練習", 2, 8, false, created),
            new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, created),
        ];
        var cut = RenderLobby();
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("#room-list li[data-room]");
            Assert.Equal(2, rows.Count);
            Assert.Contains("夏の練習", rows[0].TextContent);
            Assert.Contains("2 / 8 人", rows[0].TextContent);
            Assert.Contains("作成から 5 分", rows[0].TextContent);
            Assert.Empty(rows[0].QuerySelectorAll("input[type=password]"));
            Assert.Single(rows[1].QuerySelectorAll("input[type=password]"));   // 鍵つきは、鍵の欄を出す
            Assert.Contains("鍵つき", rows[1].TextContent);
        });
    }

    [Fact]
    public void An_empty_list_says_so_and_a_failed_read_says_it_failed()
    {
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Contains("今、一覧に出ている部屋はありません", cut.Find("#room-list").TextContent));

        directory.Rooms = [];
        directory.Fail = new HttpRequestException("down");
        var failing = RenderLobby();
        failing.WaitForAssertion(() => Assert.Contains("一覧を読めませんでした", failing.Markup));
    }

    [Fact]
    public void Joining_a_locked_room_without_the_pass_asks_for_the_pass_and_does_not_leave_the_page()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        var nav = Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        Assert.Contains("鍵を入力してから", cut.Markup);
        Assert.Equal(before, nav.Uri);
    }

    [Fact]
    public void Creating_a_room_moves_to_the_new_room_and_the_pass_is_not_in_the_url()
    {
        var cut = RenderLobby();
        cut.Find("#room-create-name").Input("練習会");
        cut.Find("#room-create-pass").Input("ひみつ123");
        cut.Find("#room-create").Click();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Matches(@"\?room=[a-z0-9]{10}$", nav.Uri);
        Assert.DoesNotContain("ひみつ", nav.Uri);
    }

    [Fact]
    public void The_create_form_has_the_listed_checkbox_off_by_default_and_a_name_limit_of_40()
    {
        var cut = RenderLobby();
        Assert.Equal("no", cut.Find("#room-create-listed option[selected]").GetAttribute("value"));
        Assert.Equal("40", cut.Find("#room-create-name").GetAttribute("maxlength"));
        Assert.Equal("64", cut.Find("#room-create-pass").GetAttribute("maxlength"));
    }

    [Fact]
    public void Without_a_directory_the_lobby_still_works_and_the_list_stays_loading()
    {
        var cut = RenderLobby(withDirectory: false);
        Assert.Contains("読み込み中…", cut.Find("#room-list").TextContent);
        Assert.Contains("部屋を作る", cut.Markup);
    }
}
