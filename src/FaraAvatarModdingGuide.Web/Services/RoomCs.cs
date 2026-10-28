using System.Runtime.InteropServices.JavaScript;
using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Web.Services;

/// <summary>
/// /practice/room の 3D の C# 版(?engine=cs)。ビューの描画と操作は <see cref="CsViewEngine"/>、部屋の同期の規則は Practice の <see cref="RoomSession"/>、
/// WebSocket・タイマー・保存・ポインターは wwwroot/js/room-cs.js(薄い JavaScript の層)が持つ。
/// 画面(PracticeRoom.razor)は Practice の型を直接使わず、<see cref="IRoomCs"/> と <see cref="RoomHud"/> だけを読む(遅れて読み込むため)。
/// </summary>
public sealed class RoomCsEngine : CsViewEngine, IRoomCs
{
    private static readonly RetryableTask importingRoom = new();
    private static RoomCsEngine? active;   // JavaScript からの接続・タイマーの通知を受ける先(同時に 1 つだけ動く)

    private readonly string roomId;
    private readonly string url;
    private RoomSession? session;
    private int attachment = -1;
    private string lastCursors = "";
    private string lastRoomSignature = "";

    private readonly RoomJoinOptions? join;

    private RoomCsEngine(string roomId, string url, bool debug, RoomJoinOptions? join)
        : base("room-viewport", "room-gl", () => false, debug)
    {
        this.roomId = roomId;
        this.url = url;
        this.join = join;
    }

    /// <summary>room-cs.js を読み込む(1 回だけ)。</summary>
    public static Task ImportRoomAsync() => importingRoom.RunAsync(() => JSHost.ImportAsync("room-cs", "../js/room-cs.js"));

    /// <summary>部屋を作る(読み込みのあと)。Start() で入る。join は、作成や鍵(一覧から入るとき)の指定。</summary>
    public static RoomCsEngine Create(string roomId, string url, bool debug, RoomJoinOptions? join = null) => new(roomId, url, debug, join);

    private RoomTransport transport = null!;

    protected override ViewportOptions CreateOptions() => new()
    {
        Scene = [],   // 部屋はシーンを持たない(welcome で届く)
        CanTransform = objects => session?.BlockedBy(objects),
    };

    protected override void OnCoreCreated(ViewportCore core)
    {
        transport = new RoomTransport();
        session = new RoomSession(core, transport, roomId, RoomSession.InitialName(RoomJs.LoadName(), Random.Shared.Next(10, 100)), join);
        session.Changed += OnRoomChanged;
    }

    /// <summary>入る。WebGL が使えなければ false(部屋には入らない)。</summary>
    public new bool Start()
    {
        if (!base.Start()) return false;
        active = this;
        RoomJs.SetHandlers(OnSocketOpen, OnSocketMessage, OnSocketClose);
        attachment = RoomJs.Attach("room-viewport", OnPointerMove, OnPointerLeave, OnPageHide);
        if (Debug) RoomJs.SetDebug(DebugJson());
        session!.Start();
        return true;
    }

    /// <summary>画面に出す部屋の状態(ステータス・参加者・表示名・追従)。</summary>
    public RoomHud Room => BuildRoom();

    /// <summary>表示名の入力欄が変わった。</summary>
    public void NameTyped(string value) => session?.NameTyped(value);

    /// <summary>「視点に合わせる」(押した人の id)。</summary>
    public void ToggleFollow(string id) => session?.ToggleFollow(id);

    /// <summary>URL をコピーする(失敗したら、入力欄の文字を選ぶ)。</summary>
    public Task CopyUrlAsync() => RoomJs.Copy(url, "room-url");

    /// <summary>鍵を入れて入り直す(鍵の画面の送信)。</summary>
    public void SubmitPass(string pass) => session?.SubmitPass(pass);

    /// <summary>部屋を閉じる(作成者だけ。確認は画面が済ませてから呼ぶ)。</summary>
    public void RequestClose() => session?.RequestClose();

    /// <summary>退室して解放する。先に部屋を退出し(WebSocket を閉じる)、JavaScript からの通知は、この後は無視される(active を外す)。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        session?.Dispose();
        session = null;
        if (attachment >= 0) RoomJs.Detach(attachment);
        attachment = -1;
        if (ReferenceEquals(active, this)) active = null;
        if (Debug) RoomJs.ClearDebug();
        base.Dispose();
    }

    /// <summary>ビューの描画のあと: ほかの人のカーソルの位置を、変わったときだけ JavaScript で置く(room.js の updateRemoteCursors)。</summary>
    protected override void OnAfterFrame()
    {
        if (session is null || attachment < 0) return;
        var json = CursorsJson();
        if (json == lastCursors) return;
        lastCursors = json;
        RoomJs.PlaceCursors("room-remote", json);
    }

    private void OnRoomChanged()
    {
        if (session is null || IsDisposed) return;
        AfterInput();   // ステータス（ViewportCore の文字）が変わったら、HUD を描き直す
        if (Debug) RoomJs.SetDebug(DebugJson());   // 確認用の window.__room を、いまの部屋に合わせる
        var signature = RoomSignature();
        if (signature == lastRoomSignature) return;
        lastRoomSignature = signature;
        RaiseChanged();
    }

    private RoomHud BuildRoom()
    {
        var s = session;
        if (s is null) return new RoomHud("", "", "", [], null);
        var gate = s.Gate switch { RoomGate.NeedPass => "needpass", RoomGate.WrongPass => "wrongpass", RoomGate.PassLimited => "limited", _ => "none" };
        var peers = new List<RoomHudPeer>();
        if (s.Me is { } me) peers.Add(new RoomHudPeer(me.Id, me.Name, me.Color, Self: true, Following: false));
        foreach (var p in s.Peers) peers.Add(new RoomHudPeer(p.Id, p.Name, p.Color, Self: false, Following: s.Follow == p.Id));
        return new RoomHud(s.StatusText, s.StatusKind, s.Name, peers, s.Follow, gate, s.PassLimitMs, s.ClosedReason, s.IdleWarnMs, s.Me?.Owner ?? false, s.RoomName, s.Locked);
    }

    private string RoomSignature()
    {
        var r = BuildRoom();
        var parts = new List<string> { r.Status, r.StatusKind, r.Follow ?? "", r.Gate, r.PassLimitMs.ToString(System.Globalization.CultureInfo.InvariantCulture), r.ClosedReason ?? "", r.IdleWarnMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", r.IsOwner.ToString(), r.RoomName, r.Locked.ToString() };
        foreach (var p in r.Peers) parts.Add($"{p.Id}\u0001{p.Name}\u0001{p.Color}\u0001{p.Self}\u0001{p.Following}");
        return string.Join("\u0002", parts);
    }

    private string CursorsJson()
    {
        var cursors = Core.RemoteCursors();
        var items = cursors.Select(c => $"{{\"id\":{RoomWire.Str(c.Id)},\"hidden\":{(c.Hidden ? "true" : "false")},\"x\":{RoomWire.Num(c.X)},\"y\":{RoomWire.Num(c.Y)}}}");
        return "[" + string.Join(",", items) + "]";
    }

    private string DebugJson()
    {
        var objects = string.Join(",", Core.Objects.Select(o => $"{{\"userData\":{{\"kind\":{RoomWire.Str(o.Kind)}}}}}"));
        return $"{{\"vp\":{{\"objects\":[{objects}]}}}}";
    }

    // ---------------------------------------------------------------- JavaScript からの通知(room-cs.js)
    private static void OnSocketOpen(int id) => active?.session?.OnOpen(id);
    private static void OnSocketMessage(int id, string text) => active?.session?.OnMessage(id, text);
    private static void OnSocketClose(int id, int code) => active?.session?.OnClose(id, code);
    private static void OnPointerMove(double x, double y) => active?.session?.PointerAt(x, y);
    private static void OnPointerLeave() => active?.session?.PointerLeft();
    private static void OnPageHide() => active?.session?.LeavePage();

    /// <summary>room.js の通信: WebSocket・タイマー・保存は JavaScript(room-cs.js)が持つ。</summary>
    private sealed class RoomTransport : IRoomTransport
    {
        public int Open(string roomId) => RoomJs.Open(roomId);
        public void Send(int socket, string text) => RoomJs.Send(socket, text);
        public void Close(int socket, int code, string reason) => RoomJs.Close(socket, code, reason);
        public int Timer(double ms, bool repeat, Action callback) => RoomJs.Timer(ms, repeat, callback);
        public void CancelTimer(int handle) { if (handle >= 0) RoomJs.CancelTimer(handle); }
        public double Now() => RoomJs.Now();
        public void SaveName(string name) => RoomJs.SaveName(name);
        public string? LoadOwnerKey(string roomId) => RoomJs.LoadOwnerKey(roomId);
        public void SaveOwnerKey(string roomId, string key) => RoomJs.SaveOwnerKey(roomId, key);
        public void ForgetOwnerKey(string roomId) => RoomJs.ForgetOwnerKey(roomId);
    }
}

/// <summary>wwwroot/js/room-cs.js(WebSocket・タイマー・localStorage・ポインター・クリップボード)の呼び出し。</summary>
internal static partial class RoomJs
{
    [JSImport("setHandlers", "room-cs")]
    internal static partial void SetHandlers(
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<int> onOpen,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.String>>] Action<int, string> onMessage,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<int, int> onClose);

    [JSImport("open", "room-cs")]
    internal static partial int Open(string roomId);

    [JSImport("send", "room-cs")]
    internal static partial void Send(int socket, string text);

    [JSImport("close", "room-cs")]
    internal static partial void Close(int socket, int code, string reason);

    [JSImport("timer", "room-cs")]
    internal static partial int Timer(double ms, [JSMarshalAs<JSType.Boolean>] bool repeat, [JSMarshalAs<JSType.Function>] Action callback);

    [JSImport("cancelTimer", "room-cs")]
    internal static partial void CancelTimer(int handle);

    [JSImport("now", "room-cs")]
    internal static partial double Now();

    [JSImport("loadName", "room-cs")]
    internal static partial string? LoadName();

    [JSImport("saveName", "room-cs")]
    internal static partial void SaveName(string name);

    [JSImport("loadOwnerKey", "room-cs")]
    internal static partial string? LoadOwnerKey(string roomId);

    [JSImport("saveOwnerKey", "room-cs")]
    internal static partial void SaveOwnerKey(string roomId, string key);

    [JSImport("forgetOwnerKey", "room-cs")]
    internal static partial void ForgetOwnerKey(string roomId);

    [JSImport("attach", "room-cs")]
    internal static partial int Attach(
        string rootId,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number>>] Action<double, double> onMove,
        [JSMarshalAs<JSType.Function>] Action onLeave,
        [JSMarshalAs<JSType.Function>] Action onHide);

    [JSImport("detach", "room-cs")]
    internal static partial void Detach(int attachment);

    [JSImport("placeCursors", "room-cs")]
    internal static partial void PlaceCursors(string boxId, string json);

    [JSImport("copy", "room-cs")]
    internal static partial Task Copy(string text, string urlInputId);

    [JSImport("setDebug", "room-cs")]
    internal static partial void SetDebug(string json);

    [JSImport("clearDebug", "room-cs")]
    internal static partial void ClearDebug();
}

/// <summary>部屋の画面(Razor)が読む状態。Practice の型を使わない(遅れて読み込むため)。</summary>
/// <summary>
/// Gate は "none"・"needpass"・"wrongpass"・"limited"（鍵の画面）。ClosedReason は閉じた理由（null は開いている）。IdleWarnMs は閉じる前の残り（null は無い）。
/// IsOwner は作成者(閉じられる)。RoomName と Locked は部屋の名前と鍵つきか。
/// </summary>
public sealed record RoomHud(string Status, string StatusKind, string Name, IReadOnlyList<RoomHudPeer> Peers, string? Follow,
    string Gate = "none", double PassLimitMs = 0, string? ClosedReason = null, double? IdleWarnMs = null, bool IsOwner = false, string RoomName = "", bool Locked = false);

/// <summary>参加者 1 人(一覧の 1 行)。</summary>
public sealed record RoomHudPeer(string Id, string Name, string Color, bool Self, bool Following);

/// <summary>部屋の 3D の C# 版(IRoomCs)。PracticeRoom.razor が、Practice の読み込みのあとに作る。</summary>
public interface IRoomCs : IDisposable
{
    PracticeHud Hud { get; }
    RoomHud Room { get; }
    event Action? Changed;
    bool Start();
    void ClickTool(string tool);
    void ChooseMenu(int index);
    void ChangeNPanel(string prop, string axis, string value);
    void NameTyped(string value);
    void ToggleFollow(string id);
    Task CopyUrlAsync();
    void SubmitPass(string pass);
    void RequestClose();
}

/// <summary>
/// 部屋に入る指定(画面から渡す。Web の型だけ。鍵・作成者の鍵はメモリだけで扱う)。
/// 一覧から鍵つきの部屋に入るときは Pass。部屋を作るときは Create（名前・一覧に出すか・鍵・作成者の鍵）。
/// </summary>
public sealed record RoomStartOptions(string? Pass = null, string? CreateName = null, bool Listed = false, string? CreatePass = null, string? OwnerKey = null);

/// <summary>部屋の C# 版の入口(Practice・Render を遅れて読み込んだあとに作る)。</summary>
public static class RoomCsLoader
{
    public static async Task<IRoomCs> CreateAsync(string roomId, string url, bool debug, RoomStartOptions? start = null)
    {
        await PracticeCsEngine.ImportAsync();
        await PracticeCsEngine.ImportHelperAsync();
        await RoomCsEngine.ImportRoomAsync();
        return RoomCsEngine.Create(roomId, url, debug, ToJoin(start));
    }

    /// <summary>画面の指定を、Practice の入ったあとの型に直す（鍵の指定が無ければ null）。</summary>
    private static RoomJoinOptions? ToJoin(RoomStartOptions? start)
    {
        if (start is null) return null;
        RoomCreateOptions? create = start.OwnerKey is { } key && start.CreateName is { } name
            ? new RoomCreateOptions(name, start.Listed, string.IsNullOrEmpty(start.CreatePass) ? null : start.CreatePass, key)
            : null;
        return new RoomJoinOptions(string.IsNullOrEmpty(start.Pass) ? null : start.Pass, create);
    }
}
