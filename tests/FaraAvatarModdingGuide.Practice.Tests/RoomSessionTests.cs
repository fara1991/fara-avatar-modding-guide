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
    public void A_rejected_batch_says_some_changes_were_not_applied()
    {
        var (core, s, _) = Joined();
        s.OnMessage(1, Q("{'t':'ops','upsert':[],'delete':[],'rejected':true}"));
        Assert.Equal("ほかの人の操作と重なったため、一部の変更は反映されませんでした", core.Status);
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
}
