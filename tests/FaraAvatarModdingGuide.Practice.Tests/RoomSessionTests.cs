using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 共同練習(room.js)の規則の単体テスト(RoomSession と RoomWire)。通信は仮想の時計つきの偽物(FakeTransport)。
/// JSON は読みやすさのため、引用符を ' で書いて Q() で " に直す。制御文字は (char) で作る(文字列の中の逆斜線を避ける)。
/// </summary>
public class RoomSessionTests
{
    private static string Q(string s) => s.Replace('\'', '"');

    /// <summary>送受信と時計を持つ偽物。タイマーは仮想の時計で、(時刻, 番号) の順に発火する。</summary>
    private sealed class FakeTransport : IRoomTransport
    {
        private sealed record ScheduledTimer(int Id, double At, double Ms, bool Repeat, Action Callback);

        private readonly List<ScheduledTimer> timers = [];
        private int nextTimer = 1;

        public int Opened { get; private set; }
        public List<string> Outbound { get; } = [];
        public List<string> Closes { get; } = [];
        public List<string> Saved { get; } = [];
        public double Clock { get; private set; }
        public int ActiveTimers => timers.Count;

        public int Open(string roomId) => ++Opened;
        public void Send(int socket, string text) => Outbound.Add(text);
        public void Close(int socket, int code, string reason) => Closes.Add(socket + ":" + code + ":" + reason);

        public int Timer(double ms, bool repeat, Action callback)
        {
            var id = nextTimer++;
            timers.Add(new ScheduledTimer(id, Clock + ms, ms, repeat, callback));
            return id;
        }

        public void CancelTimer(int handle) => timers.RemoveAll(t => t.Id == handle);
        public double Now() => Clock;
        public void SaveName(string name) => Saved.Add(name);

        /// <summary>ms だけ時計を進め、期限の来たタイマーを呼ぶ。</summary>
        public void Advance(double ms)
        {
            var end = Clock + ms;
            while (true)
            {
                var due = timers.Where(t => t.At <= end).OrderBy(t => t.At).ThenBy(t => t.Id).FirstOrDefault();
                if (due is null) break;
                Clock = Math.Max(Clock, due.At);
                if (due.Repeat) timers[timers.IndexOf(due)] = due with { At = due.At + due.Ms };
                else timers.Remove(due);
                due.Callback();
            }
            Clock = end;
        }
    }

    private const string Welcome = "{'t':'welcome','you':{'id':'me1','name':'Alice','color':'#f97316'},'peers':[{'id':'p2','name':'Bob','color':'#22c55e'}],'objects':[{'id':'cube0','kind':'cube','name':'Cube','p':[0,0,0],'r':[0,0,0],'s':[1,1,1]}]}";

    private static ViewportCore NewCore() => new(new ViewRect(0, 0, 960, 540), new ViewportOptions { Scene = [] });

    /// <summary>接続して、welcome まで受け取った状態を作る。</summary>
    private static (ViewportCore Core, RoomSession Session, FakeTransport T) Joined(string name = "Alice")
    {
        var core = NewCore();
        var t = new FakeTransport();
        var s = new RoomSession(core, t, "abcdef12", name);
        s.Start();
        s.OnOpen(1);
        s.OnMessage(1, Q(Welcome));
        return (core, s, t);
    }

    [Fact]
    public void Hello_is_sent_on_open_with_the_name()
    {
        var t = new FakeTransport();
        var s = new RoomSession(NewCore(), t, "abcdef12", "Alice");
        s.Start();
        Assert.Equal("接続中…", s.StatusText);
        s.OnOpen(1);
        Assert.Equal(Q("{'t':'hello','name':'Alice'}"), Assert.Single(t.Outbound));
    }

    [Fact]
    public void Welcome_loads_the_scene_and_peers_and_sends_the_selection()
    {
        var (core, s, t) = Joined();
        Assert.Equal("接続しました", s.StatusText);
        Assert.Equal("ok", s.StatusKind);
        Assert.Equal("me1", s.Me!.Id);
        Assert.Equal("Bob", Assert.Single(s.Peers).Name);
        Assert.Equal("cube0", Assert.Single(core.Objects).Rid);
        Assert.Equal(Q("{'t':'select','ids':[]}"), t.Outbound[^1]);
    }

    [Fact]
    public void Local_add_sends_the_new_object_as_an_upsert()
    {
        var (core, _, t) = Joined();
        t.Outbound.Clear();
        core.AddPrimitiveNow("sphere");
        var ops = Assert.Single(t.Outbound, m => m.StartsWith(Q("{'t':'ops'"), StringComparison.Ordinal));
        Assert.Contains(Q("'kind':'sphere'"), ops);
        Assert.EndsWith(Q("],'delete':[]}"), ops);
    }

    [Fact]
    public void Ops_from_others_are_applied_and_do_not_echo_back()
    {
        var (core, s, t) = Joined();
        t.Outbound.Clear();
        s.OnMessage(1, Q("{'t':'ops','upsert':[{'id':'s9','kind':'sphere','name':'Sphere','p':[3,0,0],'r':[0,0,0],'s':[1,1,1]}],'delete':[]}"));
        Assert.Equal(2, core.Objects.Count);
        Assert.Contains(core.Objects, o => o.Rid == "s9" && o.Position.X == 3);
        Assert.Empty(t.Outbound);
    }

    [Fact]
    public void Close_4001_says_the_room_is_full()
    {
        var (core, s, _) = Joined();
        s.OnClose(1, 4001);
        Assert.Equal("満員です（8 人まで）", s.StatusText);
        Assert.Equal("warn", s.StatusKind);
        Assert.Equal("この部屋は満員です。別の部屋を作ってください", core.Status);
        Assert.Empty(s.Peers);
        Assert.Null(s.Me);
    }

    [Fact]
    public void Close_1008_stops_without_retrying()
    {
        var (_, s, t) = Joined();
        s.OnClose(1, 1008);
        Assert.Equal("切断されました", s.StatusText);
        t.Advance(60000);
        Assert.Equal(1, t.Opened);
    }

    [Fact]
    public void A_dropped_connection_retries_with_backoff_and_gives_up_after_six()
    {
        var (_, s, t) = Joined();
        double[] waits = [1000, 2000, 4000, 8000, 15000, 15000];
        var socket = 1;
        foreach (var wait in waits)
        {
            s.OnClose(socket, 4000);
            Assert.Equal("warn", s.StatusKind);
            t.Advance(wait - 1);
            Assert.Equal(socket, t.Opened);   // まだつながない
            t.Advance(1);
            socket = t.Opened;                // つなぎ直した
        }
        s.OnClose(socket, 4000);
        Assert.Equal("接続できません。ページを再読み込みしてください", s.StatusText);
        t.Advance(60000);
        Assert.Equal(socket, t.Opened);
    }

    [Fact]
    public void A_closed_socket_from_before_is_ignored()
    {
        var (_, s, t) = Joined();
        s.OnClose(99, 1006);
        Assert.Equal("接続しました", s.StatusText);
        Assert.Equal(1, t.Opened);
    }

    [Fact]
    public void Name_is_saved_after_500_ms_and_cleaned_to_20_code_points()
    {
        var (_, s, t) = Joined();
        t.Outbound.Clear();
        s.NameTyped("  Bo" + (char)7 + "b" + new string('x', 30) + "  ");
        t.Advance(499);
        Assert.Empty(t.Saved);
        t.Advance(1);
        var expected = "Bob" + new string('x', 17);
        Assert.Equal(expected, Assert.Single(t.Saved));
        Assert.Equal(expected, s.Name);
        Assert.Equal(Q("{'t':'name','name':'" + expected + "'}"), Assert.Single(t.Outbound, m => m.StartsWith(Q("{'t':'name'"), StringComparison.Ordinal)));
    }

    [Fact]
    public void A_name_that_cleans_to_empty_or_stays_the_same_is_not_sent()
    {
        var (_, s, t) = Joined();
        t.Outbound.Clear();
        s.NameTyped("   ");
        t.Advance(600);
        s.NameTyped("Alice");
        t.Advance(600);
        Assert.DoesNotContain(t.Outbound, m => m.StartsWith(Q("{'t':'name'"), StringComparison.Ordinal));   // 視点などの定期の送信は別
        Assert.Empty(t.Saved);
    }

    [Fact]
    public void Initial_name_uses_the_saved_name_or_a_guest_number()
    {
        Assert.Equal("Taro", RoomSession.InitialName("  Taro  ", 42));
        Assert.Equal("ゲスト42", RoomSession.InitialName("   ", 42));
        Assert.Equal("ゲスト42", RoomSession.InitialName(null, 42));
    }

    [Fact]
    public void Clean_name_keeps_surrogate_pairs_whole()
    {
        var smile = char.ConvertFromUtf32(0x1F600);
        var name = string.Concat(Enumerable.Repeat(smile, 25));
        Assert.Equal(string.Concat(Enumerable.Repeat(smile, 20)), RoomSession.CleanName(name));
    }

    [Fact]
    public void Dispose_leaves_the_room_and_stops_all_timers()
    {
        var (_, s, t) = Joined();
        s.Dispose();
        Assert.Equal("1:1000:leave", Assert.Single(t.Closes));
        Assert.Equal(0, t.ActiveTimers);
        t.Outbound.Clear();
        s.Tick();
        s.Ping();
        Assert.Empty(t.Outbound);
    }

    [Fact]
    public void Ping_is_sent_only_while_the_socket_is_open()
    {
        var t = new FakeTransport();
        var s = new RoomSession(NewCore(), t, "abcdef12", "Alice");
        s.Start();
        s.Ping();
        Assert.Empty(t.Outbound);
        s.OnOpen(1);
        t.Advance(RoomSession.PingMs);
        Assert.Equal(Q("{'t':'ping'}"), t.Outbound[^1]);
    }

    [Fact]
    public void Leaving_the_pointer_sends_a_null_cursor_on_the_next_tick()
    {
        var (_, s, t) = Joined();
        t.Outbound.Clear();
        s.PointerLeft();
        t.Advance(RoomSession.TickMs);
        Assert.Equal(Q("{'t':'cursor','p':null}"), Assert.Single(t.Outbound));
    }

    [Fact]
    public void Another_persons_lock_blocks_the_transform_with_their_name()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'locks','locks':{'cube0':'p2'}}"));
        Assert.Equal("Bobさんが操作中のため、終わるまで動かせません", s.BlockedBy(core.Objects));
        Assert.Equal("cube0", Assert.Single(Assert.Single(s.Peers).Locks));
        Assert.Equal("#22c55e", core.RemoteColors()["cube0"]);
    }

    [Fact]
    public void A_remote_selection_colours_the_object_until_the_peer_leaves()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'select','from':'p2','ids':['cube0']}"));
        Assert.Equal("#22c55e", core.RemoteColors()["cube0"]);
        s.OnMessage(1, Q("{'t':'leave','id':'p2'}"));
        Assert.Empty(core.RemoteColors());
    }

    [Fact]
    public void Following_a_peer_sets_our_view_and_our_own_zoom_stops_following()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'view','from':'p2','view':{'target':[1,2,0],'dist':6,'theta':0.3,'phi':1.1,'ortho':false,'name':'ユーザー'}}"));
        s.ToggleFollow("p2");
        Assert.Equal("p2", s.Follow);
        Assert.Equal(6.0, core.GetView().Dist, 9);
        core.Wheel(100);   // 自分の操作（ズーム）は、追従をやめる（view の通知を経由する）
        Assert.Null(s.Follow);
    }

    [Fact]
    public void Lock_denied_cancels_our_own_move_and_releases_the_lock()
    {
        var (core, s, t) = Joined();
        core.SetSelection([core.Objects[0]]);
        t.Outbound.Clear();
        core.KeyDown("KeyG", "g", new Modifiers());
        Assert.Contains(t.Outbound, m => m.StartsWith(Q("{'t':'lock'"), StringComparison.Ordinal));
        s.OnMessage(1, Q("{'t':'lockdenied'}"));
        Assert.Null(core.Op);
        Assert.Equal("ほかの人が操作中のため、動かせませんでした", core.Status);
        Assert.Equal(Q("{'t':'unlock'}"), t.Outbound[^1]);
    }

    [Fact]
    public void Lock_is_renewed_while_the_move_lasts_longer_than_the_server_lock()
    {
        // サーバーのロックは 15 秒で切れる。操作が続くあいだは取り直し、途中で切れないようにする
        var (core, _, t) = Joined();
        core.SetSelection([core.Objects[0]]);
        t.Outbound.Clear();
        core.KeyDown("KeyG", "g", new Modifiers());
        t.Advance(20000);
        var locks = t.Outbound.Count(m => m.StartsWith(Q("{'t':'lock'"), StringComparison.Ordinal));
        Assert.True(locks >= 4, "lock が " + locks + " 回しか送られなかった");
        Assert.True(RoomSession.LockRenewMs < 15000);
    }

    [Fact]
    public void Ops_without_upsert_still_apply_the_delete()
    {
        var (core, s, _) = Joined();
        Assert.Single(core.Objects);
        s.OnMessage(1, Q("{'t':'ops','delete':['cube0']}"));
        Assert.Empty(core.Objects);
    }

    [Fact]
    public void Ops_with_a_non_array_upsert_are_dropped_as_a_whole()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':'x','delete':['cube0']}"));
        Assert.Single(core.Objects);
    }

    [Fact]
    public void Display_names_drop_the_html_characters_like_the_server()
    {
        Assert.Equal("ab", RoomSession.CleanName("<a>&\"'b"));
        Assert.Equal("Alice", RoomSession.CleanName("  Ali\u0007ce  "));
    }

    [Fact]
    public void Room_limits_match_the_worker_and_the_page_text()
    {
        var worker = File.ReadAllText(Path.Combine(JsReference.Root, "worker", "index.js"));
        Assert.Contains($"participants: {RoomSession.MaxParticipants},", worker);
        Assert.Contains($"objects: {RoomSession.MaxObjects},", worker);
        var lockMs = int.Parse(Regex.Match(worker, @"lockMs:\s*(\d+)").Groups[1].Value);
        Assert.True(RoomSession.LockRenewMs < lockMs);
        var pageBodies = File.ReadAllText(Path.Combine(JsReference.Root, "src", "FaraAvatarModdingGuide.Build", "PageBodies.cs"));
        Assert.Contains($"{RoomSession.MaxParticipants} 人まで", pageBodies);
        Assert.Contains($"{RoomSession.MaxObjects} 個まで", pageBodies);
    }

    [Fact]
    public void A_rejected_batch_says_some_changes_were_not_applied()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':[],'delete':[],'rejected':true}"));
        Assert.Equal("ほかの人の操作と重なったため、一部の変更は反映されませんでした", core.Status);
    }

    [Fact]
    public void Broken_json_and_non_objects_change_nothing()
    {
        var (core, s, _) = Joined();
        var before = s.StatusText;
        s.OnMessage(1, "{not json");
        s.OnMessage(1, Q("[1,2]"));
        s.OnMessage(1, Q("{'t':5}"));
        Assert.Equal(before, s.StatusText);
        Assert.Single(core.Objects);
    }

    [Fact]
    public void A_welcome_with_a_missing_field_is_ignored_as_a_whole()
    {
        var core = NewCore();
        var t = new FakeTransport();
        var s = new RoomSession(core, t, "abcdef12", "Alice");
        s.Start();
        s.OnOpen(1);
        s.OnMessage(1, Q("{'t':'welcome','you':{'id':'me1','name':'Alice','color':'#f97316'},'peers':[]}"));   // objects が無い
        Assert.Null(s.Me);
        Assert.Equal("接続中…", s.StatusText);
        Assert.Empty(core.Objects);
    }

    [Fact]
    public void A_bad_number_or_a_short_array_drops_the_whole_ops_message()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':[{'id':'a1','kind':'sphere','name':'S','p':[1,'x',0],'r':[0,0,0],'s':[1,1,1]}],'delete':[]}"));
        s.OnMessage(1, Q("{'t':'ops','upsert':[{'id':'a2','kind':'sphere','name':'S','p':[1,2],'r':[0,0,0],'s':[1,1,1]}],'delete':[]}"));
        s.OnMessage(1, Q("{'t':'ops','upsert':[{'id':'a3','name':'S','p':[1,1,1],'r':[0,0,0],'s':[1,1,1]}],'delete':[]}"));   // kind が無い
        Assert.Single(core.Objects);
    }

    [Fact]
    public void Preview_skips_the_object_we_are_moving_but_applies_the_others()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':[{'id':'s9','kind':'sphere','name':'Sphere','p':[3,0,0],'r':[0,0,0],'s':[1,1,1]}],'delete':[]}"));
        core.SetSelection([core.Objects.Single(o => o.Rid == "cube0")]);
        core.KeyDown("KeyG", "g", new Modifiers());
        s.OnMessage(1, Q("{'t':'preview','objs':[{'id':'cube0','p':[5,5,5],'r':[0,0,0],'s':[1,1,1]},{'id':'s9','p':[7,0,0],'r':[0,0,0],'s':[1,1,1]}]}"));
        Assert.Equal(0, core.Objects.Single(o => o.Rid == "cube0").Position.X);
        Assert.Equal(7, core.Objects.Single(o => o.Rid == "s9").Position.X);
    }

    [Fact]
    public void A_tick_during_a_move_sends_a_preview_and_cancel_then_sends_the_last_state_even_if_unchanged()
    {
        var (core, s, t) = Joined();
        core.SetSelection([core.Objects.Single(o => o.Rid == "cube0")]);
        core.KeyDown("KeyG", "g", new Modifiers());
        t.Outbound.Clear();
        t.Advance(RoomSession.TickMs);
        Assert.Contains(t.Outbound, m => m.StartsWith(Q("{'t':'preview'"), StringComparison.Ordinal));
        core.KeyDown("Escape", "Escape", new Modifiers());   // 取り消し: 元の位置に戻る（サーバーとの差は無い）
        var ops = Assert.Single(t.Outbound, m => m.StartsWith(Q("{'t':'ops'"), StringComparison.Ordinal));
        Assert.Contains(Q("'id':'cube0'"), ops);             // previewed のため、差が無くても最後の形を送る
        Assert.Equal(Q("{'t':'unlock'}"), t.Outbound[^1]);
    }

    [Fact]
    public void Join_with_the_same_id_replaces_the_peer()
    {
        var (_, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'join','peer':{'id':'p2','name':'Bobby','color':'#22c55e'}}"));
        Assert.Equal("Bobby", Assert.Single(s.Peers).Name);
    }

    [Fact]
    public void A_peer_message_with_our_own_id_changes_our_name()
    {
        var (_, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'peer','peer':{'id':'me1','name':'Alicia'}}"));
        Assert.Equal("Alicia", s.Me!.Name);
    }

    [Fact]
    public void Too_many_objects_says_so()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'error','msg':'too many objects'}"));
        Assert.Equal("この部屋のオブジェクトは 200 個までです", core.Status);
    }

    [Fact]
    public void A_socket_that_is_not_the_current_one_is_not_greeted()
    {
        var t = new FakeTransport();
        var s = new RoomSession(NewCore(), t, "abcdef12", "Alice");
        s.Start();
        s.OnOpen(99);
        Assert.Empty(t.Outbound);
    }

    [Fact]
    public void Rejected_follows_javascript_truthiness()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':[],'delete':[],'rejected':0}"));
        Assert.Equal("接続しました", s.StatusText);
        s.OnMessage(1, Q("{'t':'ops','upsert':[],'delete':[],'rejected':1}"));
        Assert.Equal("ほかの人の操作と重なったため、一部の変更は反映されませんでした", core.Status);
    }

    [Fact]
    public void A_lock_whose_owner_is_not_a_string_is_ignored()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'locks','locks':{'cube0':5}}"));
        Assert.Empty(Assert.Single(s.Peers).Locks);
        Assert.Null(s.BlockedBy(core.Objects));
    }

    [Fact]
    public void Number_text_matches_javascript()
    {
        Assert.Equal("0", RoomWire.Num(-0.0));
        Assert.Equal("0.1", RoomWire.Num(0.1));
        Assert.Equal("-2.5", RoomWire.Num(-2.5));
        Assert.Equal("0.000001", RoomWire.Num(0.000001));
        Assert.Equal("1.5e-7", RoomWire.Num(1.5e-7));
        Assert.Equal("1e-7", RoomWire.Num(1e-7));
        Assert.Equal("123456789012345680000", RoomWire.Num(1.2345678901234568e20));
        Assert.Equal("1e+21", RoomWire.Num(1e21));
    }

    [Fact]
    public void String_text_matches_json_stringify_for_quotes_controls_and_non_ascii()
    {
        var quote = (char)34;
        var back = (char)92;
        Assert.Equal(quote + "a" + back + quote + "b" + quote, RoomWire.Str("a" + quote + "b"));
        Assert.Equal(quote + "" + back + "u0007" + quote, RoomWire.Str("" + (char)7));
        Assert.Equal(quote + "" + back + "n" + quote, RoomWire.Str("" + (char)10));
        Assert.Equal(quote + "表示" + quote, RoomWire.Str("表示"));
    }

    [Fact]
    public void Ordered_map_keeps_javascript_map_order()
    {
        var m = new OrderedMap();
        m.Set("a", "1");
        m.Set("b", "2");
        m.Set("a", "3");
        Assert.Equal(new[] { "a", "b" }, m.Keys);
        m.Delete("a");
        m.Set("a", "4");
        Assert.Equal(new[] { "b", "a" }, m.Keys);
    }

    [Fact]
    public void While_disconnected_an_edit_is_announced_and_does_not_reach_the_room()
    {
        var (core, s, t) = Joined();
        s.OnClose(1, 1006);
        t.Outbound.Clear();
        core.AddPrimitiveNow("sphere");
        // 変更は部屋に届かない（送らない）。届かないことを、画面の文で知らせる
        Assert.Empty(t.Outbound);
        Assert.Contains("部屋に届きません", core.Status);
    }

    [Fact]
    public void While_disconnected_moving_is_refused_with_a_reason()
    {
        var (core, s, _) = Joined();
        s.OnClose(1, 1006);
        Assert.Equal("接続中ではないため、動かせません", s.BlockedBy(core.Objects));
    }

    [Fact]
    public void Reconnecting_clears_the_old_lock_of_a_dropped_session()
    {
        var (core, s, t) = Joined();
        // ほかの人（p2）が操作中のロックを受け取ったまま切れる
        s.OnMessage(1, Q("{'t':'locks','locks':{'cube0':'p2'}}"));
        Assert.NotNull(s.BlockedBy(core.Objects));
        s.OnClose(1, 1006);
        t.Advance(1000);
        s.OnOpen(2);
        s.OnMessage(2, Q(Welcome));
        // 再接続の時点では、サーバーのロックは分からない。古いロックが残って動かせなくなっていないこと
        Assert.Null(s.BlockedBy(core.Objects));
    }
}
