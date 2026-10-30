using Bunit;
using FaraAvatarModdingGuide.Web.Pages;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// みんなで練習のロビー（練習の部屋の一覧・部屋の作成・パスワードの入力）の画面の振る舞い。一覧は偽物（IRoomDirectory）で差し替える。
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

    /// <summary>
    /// この端末に保存された作成者の鍵の偽物（localStorage の代わり）。同じ端末の別のタブは、同じ Keys を読む（ロビーを開いたままでも、押したときに見える）。
    /// Fail を入れると、保存を読めないときの振る舞いを確かめられる。
    /// </summary>
    private sealed class FakeOwnerKeys : IRoomOwnerKeys
    {
        public HashSet<string> Keys { get; } = [];
        public Exception? Fail { get; set; }

        public Task<bool> HasAsync(string roomId) =>
            Fail is null ? Task.FromResult(Keys.Contains(roomId)) : Task.FromException<bool>(Fail);
    }

    private readonly FakeDirectory directory = new();
    private readonly FakeOwnerKeys ownerKeys = new();
    private bool entrySet;
    private bool directorySet;
    private bool ownerKeysSet;

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
        if (!ownerKeysSet)
        {
            ownerKeysSet = true;
            Services.AddSingleton<IRoomOwnerKeys>(ownerKeys);
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
            new RoomListItem("roomaaaa1111", "夏の練習", 2, 8, true, created),
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
            Assert.Empty(rows[0].QuerySelectorAll("input[type=password]"));   // パスワードの欄は、「入る」を押すまで出さない
            Assert.Empty(rows[1].QuerySelectorAll("input[type=password]"));
            Assert.Contains("パスワードあり", rows[1].TextContent);
            Assert.Single(rows[1].QuerySelectorAll(".room-card-lock-icon[role=img][aria-label=パスワードあり]"));   // 鍵のアイコンには代替テキスト
            Assert.Single(rows[0].QuerySelectorAll(".room-card-lock-icon"));
            Assert.DoesNotContain("鍵なし", rows[0].TextContent);
        });
    }

    [Fact]
    public void Each_room_is_a_card_whose_join_button_is_named_after_the_room()
    {
        directory.Rooms = [new RoomListItem("roomaaaa1111", "夏の練習", 2, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        var card = cut.Find("#room-list > li.room-card[data-room]");
        Assert.Equal("夏の練習", card.QuerySelector("h3")!.TextContent);      // 部屋名は見出し（ページの h1 の下の h3）
        Assert.Equal("夏の練習 に入る", card.QuerySelector("button")!.GetAttribute("aria-label"));
        Assert.Equal("false", card.QuerySelector("button")!.GetAttribute("aria-expanded"));
        Assert.False(card.QuerySelector("button")!.HasAttribute("disabled"));
        Assert.Contains("2 / 8 人", card.QuerySelector(".room-card-count")!.TextContent);
    }

    [Fact]
    public void A_full_room_is_marked_full_and_its_join_button_is_disabled_with_the_reason()
    {
        directory.Rooms = [new RoomListItem("roomcccc3333", "満員の部屋", 8, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        var card = cut.Find("#room-list li[data-room]");
        Assert.Equal("満員", card.QuerySelector(".room-card-full")!.TextContent);
        var button = card.QuerySelector("button")!;
        Assert.True(button.HasAttribute("disabled"));
        var reasonId = button.GetAttribute("aria-describedby");
        Assert.NotNull(reasonId);
        Assert.Contains("満員のため入れません", cut.Find("#" + reasonId).TextContent);   // 無効の理由は、ボタンの説明として読まれる
        Assert.Empty(card.QuerySelectorAll("input[type=password]"));
    }

    [Fact]
    public void A_full_room_shows_no_password_field_even_after_a_click_and_a_room_with_room_left_shows_no_full_mark()
    {
        directory.Rooms =
        [
            new RoomListItem("roomdddd4444", "満員の部屋", 8, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new RoomListItem("roomeeee5555", "空きあり", 7, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
        ];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("#room-list li[data-room]").Count));
        var rows = cut.FindAll("#room-list li[data-room]");
        Assert.Single(rows[0].QuerySelectorAll(".room-card-lock-icon"));
        Assert.Empty(rows[1].QuerySelectorAll(".room-card-full"));
        Assert.False(rows[1].QuerySelector("button")!.HasAttribute("disabled"));
        // 満員の部屋は、押しても欄は出ない（入れない）
        cut.FindAll("#room-list li[data-room] button")[0].Click();
        Assert.Empty(cut.FindAll("#room-list input[type=password]"));
    }

    [Fact]
    public void A_room_name_is_shown_as_text_and_never_as_markup()
    {
        const string name = "<img src=x onerror=alert(1)>";
        directory.Rooms = [new RoomListItem("roomffff6666", name, 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        Assert.Empty(cut.FindAll("#room-list img"));                              // タグとして作られない
        Assert.Equal(name, cut.Find("#room-list h3").TextContent);
        Assert.Equal(name + " に入る", cut.Find("#room-list button").GetAttribute("aria-label"));
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
    public void Clicking_a_room_opens_its_password_field_and_joining_without_a_password_does_not_leave_the_page()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        var nav = Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        Assert.Equal("true", cut.Find("#room-list li[data-room] button").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("#room-list input[type=password]"));   // 「入る」を押すと、パスワードの欄が出る
        Assert.Equal("64", cut.Find("#room-list input[type=password]").GetAttribute("maxlength"));   // 32 字以上の鍵を貼り付けられる長さ
        cut.Find("#room-list .room-pass-panel button").Click();
        Assert.Contains("パスワードを入力してください", cut.Markup);
        Assert.Equal(before, nav.Uri);
    }

    [Fact]
    public void Clicking_the_join_button_again_closes_the_password_field()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        Assert.Single(cut.FindAll("#room-list input[type=password]"));
        cut.Find("#room-list li[data-room] button").Click();
        Assert.Empty(cut.FindAll("#room-list input[type=password]"));
    }

    [Fact]
    public void Creating_a_room_moves_to_the_new_room_and_the_pass_is_not_in_the_url()
    {
        var cut = RenderLobby();
        cut.Find("#room-create").Click();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Matches(@"\?room=[a-z0-9]{10}$", nav.Uri);
        Assert.DoesNotContain("pass", nav.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_create_form_has_only_the_create_button_and_no_name_pass_or_listed_fields()
    {
        var cut = RenderLobby();
        Assert.Empty(cut.FindAll("#room-create-name, #room-create-pass, #room-create-listed"));
        Assert.Empty(cut.FindAll("#room-lobby input, #room-lobby select"));   // 作成の欄は無い（部屋の名前とパスワードは自動で決まる）
        Assert.Single(cut.FindAll("#room-create"));
    }

    [Fact]
    public void The_room_page_shows_no_copyable_url_field()
    {
        var cut = RenderLobby();
        Assert.DoesNotContain("room-url", cut.Markup);
        Assert.DoesNotContain("id=\"room-copy\"", cut.Markup);
    }

    [Fact]
    public void Without_a_directory_the_lobby_still_works_and_the_list_stays_loading()
    {
        var cut = RenderLobby(withDirectory: false);
        Assert.Contains("読み込み中…", cut.Find("#room-list").TextContent);
        Assert.Contains("部屋を作る", cut.Markup);
    }

    [Fact]
    public void A_room_password_is_40_letters_and_digits_and_differs_each_time()
    {
        var a = RoomPassFactory.Create();
        var b = RoomPassFactory.Create();
        Assert.Matches("^[A-Za-z0-9]{40}$", a);
        Assert.Matches("^[A-Za-z0-9]{40}$", b);
        Assert.NotEqual(a, b);
        Assert.Equal(62, RoomPassFactory.Chars.Distinct().Count());   // 62 字（英数字）を一様に使う
    }

    [Fact]
    public void Submitting_the_open_password_form_joins_the_room_with_the_typed_password()
    {
        directory.Rooms = [new RoomListItem("roomcccc7777", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        cut.Find("#room-list input[type=password]").Input("  合言葉を貼り付け  ");
        cut.Find("#room-list form").Submit();   // Enter と同じ送信
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Matches(@"\?room=roomcccc7777$", nav.Uri);
    }

    [Fact]
    public void The_creators_device_joins_from_the_list_without_asking_for_the_password()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "自分の部屋", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        ownerKeys.Keys.Add("roombbbb2222");   // この端末で作った部屋（作成者の鍵が保存されている）
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        var nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.Matches(@"\?room=roombbbb2222$", nav.Uri));   // パスワードの欄は出ず、そのまま入る
        Assert.Empty(cut.FindAll("#room-list input[type=password]"));
        Assert.DoesNotContain("pass", nav.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_key_is_read_when_the_button_is_pressed_so_a_room_made_in_another_tab_after_the_lobby_opened_also_joins_directly()
    {
        directory.Rooms = [new RoomListItem("roomeeee5555", "後から作った部屋", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();   // このタブのロビーは、部屋を作られる前に開いている
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        ownerKeys.Keys.Add("roomeeee5555");   // 別のタブで部屋を作り、鍵を保存した（同じ端末の保存）
        cut.Find("#room-list li[data-room] button").Click();
        var nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.Matches(@"\?room=roomeeee5555$", nav.Uri));
        Assert.Empty(cut.FindAll("#room-list input[type=password]"));
    }

    [Fact]
    public void Another_device_or_a_key_for_another_room_is_still_asked_for_the_password()
    {
        directory.Rooms =
        [
            new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            new RoomListItem("roomffff6666", "ほかの人の部屋", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
        ];
        ownerKeys.Keys.Add("roomzzzz9999");   // 別の部屋の作成者の鍵だけがある
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("#room-list li[data-room]").Count));
        cut.FindAll("#room-list li[data-room] button")[0].Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list input[type=password]")));
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotMatch(@"\?room=roombbbb2222", nav.Uri);
    }

    [Fact]
    public void When_the_saved_key_cannot_be_read_the_lobby_falls_back_to_the_password_field()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        ownerKeys.Fail = new InvalidOperationException("storage denied");
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list input[type=password]")));
        Assert.DoesNotMatch(@"\?room=roombbbb2222", Services.GetRequiredService<NavigationManager>().Uri);   // 入室はせず、欄で聞く
    }

    [Fact]
    public void The_password_field_in_the_list_is_a_password_box_that_accepts_a_paste()
    {
        directory.Rooms = [new RoomListItem("roombbbb2222", "鍵つき", 1, 8, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())];
        var cut = RenderLobby();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#room-list li[data-room]")));
        cut.Find("#room-list li[data-room] button").Click();
        var input = cut.Find("#room-list input");
        Assert.Equal("password", input.GetAttribute("type"));   // 伏せ字（●）で出す
        Assert.Equal("off", input.GetAttribute("autocomplete"));
    }
}
