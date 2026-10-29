using System.Text.Json;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>共同練習の通信・タイマー・保存の口(ブラウザでは room-cs.js が WebSocket・setTimeout・localStorage で実装する。テストでは仮想の時計で実装する)。</summary>
public interface IRoomTransport
{
    /// <summary>接続を 1 本開く。返す値は接続の番号(そのあとのイベントは、この番号で届く)。</summary>
    int Open(string roomId);

    /// <summary>接続に文字を送る。</summary>
    void Send(int socket, string text);

    /// <summary>接続を閉じる(code・reason は WebSocket の close)。</summary>
    void Close(int socket, int code, string reason);

    /// <summary>タイマー。repeat なら ms ごとに、そうでなければ 1 回だけ callback を呼ぶ。返す値は取り消しの番号。</summary>
    int Timer(double ms, bool repeat, Action callback);

    /// <summary>タイマーを取り消す(-1 や、すでに止まったものは無視する)。</summary>
    void CancelTimer(int handle);

    /// <summary>performance.now()(ms)。視点の送信の間隔と、名前の入力の間隔に使う。</summary>
    double Now();

    /// <summary>表示名を保存する(localStorage)。</summary>
    void SaveName(string name);

    /// <summary>この部屋の作成者の鍵(端末に保存したもの。無ければ null)。</summary>
    string? LoadOwnerKey(string roomId);

    /// <summary>作成者の鍵と部屋のパスワードを保存する(部屋を作ったとき)。パスワードは作成者の端末だけに残る(閉じたら消す)。</summary>
    void SaveOwnerKey(string roomId, string key, string pass);

    /// <summary>この部屋のパスワード(作成者の端末に保存したもの。無ければ null)。作成者の画面で、確認・コピーに使う。</summary>
    string? LoadOwnerPass(string roomId);

    /// <summary>作成者の鍵を消す(部屋が閉じたとき)。</summary>
    void ForgetOwnerKey(string roomId);
}

/// <summary>部屋を作る指定(作成の hello に載せる)。Pass は部屋のパスワード(英数字 32 字以上)。OwnerKey は作成者の鍵(16 バイトの乱数を 16 進にしたもの)。どちらもメモリと端末の保存だけで扱う。</summary>
public sealed record RoomCreateOptions(string Pass, string OwnerKey);

/// <summary>部屋に入る指定。Pass は一覧から入るときなどに、最初の hello で使うパスワード。Create は作成するとき。</summary>
public sealed record RoomJoinOptions(string? Pass = null, RoomCreateOptions? Create = null);

/// <summary>鍵の状態(鍵の入力を待っている・違った・試しすぎて拒否された)。None は鍵の画面を出さない。</summary>
public enum RoomGate { None, NeedPass, WrongPass, PassLimited }

/// <summary>参加者の自分(welcome の you)。Owner は作成者(部屋を閉じられる)。</summary>
public sealed record RoomMe(string Id, string Name, string Color, bool Owner = false);

/// <summary>ほかの参加者(表示用)。Cursor は 3D の点(null は無い)、Selection・Locks は部屋の id(rid)。</summary>
public sealed class RoomPeer
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Color { get; init; }
    public double[]? Cursor { get; set; }
    public IReadOnlyList<string> Selection { get; set; } = [];
    public IReadOnlyList<string> Locks { get; set; } = [];
    public ViewState? View { get; set; }
}

/// <summary>
/// 共同練習(room.js)の状態と通信の規則。ViewportCore(操作のロジック)と IRoomTransport(通信・タイマー)の間に立つ。
/// 入力は、サーバーのメッセージ(文字)・自分の操作(ViewportCore のイベント)・ポインターの位置・時計。出力は、サーバーへの文字と、画面に出す状態(<see cref="Changed"/>)。
/// room.js の関数と同じ順序で書く(onMessage・onLocal・syncOut・tick・connect の close の扱い)。
/// サーバーの文字は、形を全部確かめてから適用する(不正なものは、状態を 1 つも変えずに捨てる)。
/// </summary>
public sealed class RoomSession : IDisposable
{
    public const double TickMs = 80;
    public const double PingMs = 20000;
    /// <summary>名前の入力を確かめる間隔(1 本の繰り返しのタイマーで、入力のたびにタイマーを作らない)。</summary>
    public const double NamePollMs = 100;
    private const double NameDebounceMs = 500;
    private const double ViewMs = 250;
    private const double MaxRetryWaitMs = 15000;
    private const int MaxRetries = 6;
    /// <summary>1 部屋の人数・オブジェクト数(worker/index.js の LIMITS の participants・objects と同じ値。RoomSessionTests が一致を確かめる)。</summary>
    public const int MaxParticipants = 8;
    public const int MaxObjects = 200;
    /// <summary>操作が続くあいだ、ロックを取り直す間隔。サーバーのロックは 15 秒で切れるので、それより短くする。</summary>
    public const double LockRenewMs = 5000;
    /// <summary>送る座標の丸め（1000 分の 1）。3D の位置・視点の距離に使う。</summary>
    private const double PositionScale = 1000;
    /// <summary>送る視点の丸め（1000 分の 1）。注視点と距離に使う。</summary>
    private const double ViewScale = 1000;
    /// <summary>送る視点の角度の丸め（1 万分の 1）。</summary>
    private const double AngleScale = 10000;
    /// <summary>接続が無いときに、変更や動かしを断る文（画面の状態に出す）。</summary>
    private const string OfflineEditText = "接続が切れているため、この変更は部屋に届きません（再接続すると部屋の内容に戻ります）";
    private const string OfflineMoveText = "接続中ではないため、動かせません";

    private readonly ViewportCore core;
    private readonly IRoomTransport transport;
    private readonly string roomId;
    private readonly RoomJoinOptions join;
    private readonly List<RoomPeer> peers = [];
    private readonly OrderedMap server = new();                 // rid → サーバーが持っている状態(roomState の JSON 文字)。差分を作るのに使う
    private readonly Dictionary<string, string> locks = [];     // rid → 操作中の人の id
    private readonly List<string> lockOrder = [];               // locks の順(JSON の順。peer.locks の並びに使う)
    private HashSet<string> previewed = [];                     // 途中経過を送ったオブジェクト(確定・取り消しのときに、最後の形を必ず送る)
    private int socket = -1;
    private bool socketOpen;
    private bool closing;
    private bool disposed;
    private int retries;
    private bool everJoined;
    private int retryTimer = -1, nameTimer = -1, tickTimer = -1, pingTimer = -1;
    private string? follow;
    private bool lockHeld;
    private bool hasPendingCursor;
    private double[]? pendingCursor;
    private string lastPreview = "";
    private string lastView = "";
    private double lastViewAt;
    private double lastLockAt;
    private string nameInput;
    private double nameDirtyAt = -1;
    private string? pass;          // 鍵(入力された、または最初の hello に使った鍵)。自動の再接続では、このメモリの鍵を送る
    private string? ownerKey;      // 作成者の鍵(この端末が作った部屋のもの)
    private string? ownerPass;     // 部屋のパスワード(この端末が作った部屋のもの。作成者の画面で確認・コピーに出す)
    private bool createPending;    // 作成の指定を、まだ welcome の前に送る(welcome のあとは送らない)

    public RoomSession(ViewportCore core, IRoomTransport transport, string roomId, string name, RoomJoinOptions? join = null)
    {
        this.core = core;
        this.transport = transport;
        this.roomId = roomId;
        this.join = join ?? new RoomJoinOptions();
        Name = name;
        nameInput = name;
        core.SetRemotePeers(peers);
        core.SetStatusText("部屋に接続しています…");
        core.EventEmitted += OnLocal;
    }

    /// <summary>表示名(確定した名前。入力欄の値とは別)。</summary>
    public string Name { get; private set; }
    public RoomMe? Me { get; private set; }
    public IReadOnlyList<RoomPeer> Peers => peers;
    public string? Follow => follow;
    public string StatusText { get; private set; } = "";
    /// <summary>"" / "ok" / "warn"(room.js の status の is-* クラス)。</summary>
    public string StatusKind { get; private set; } = "";
    public bool Disposed => disposed;
    /// <summary>部屋の名前・鍵つきか(welcome で届く)。</summary>
    public string RoomName { get; private set; } = "";
    public bool Locked { get; private set; }
    /// <summary>鍵の状態(鍵の画面を出すかどうか)。</summary>
    public RoomGate Gate { get; private set; } = RoomGate.None;
    /// <summary>試しすぎて拒否されているあいだの残り(ms)。Gate が PassLimited のときだけ。</summary>
    public double PassLimitMs { get; private set; }
    /// <summary>閉じられた理由("owner" 作成者が閉じた・"idle" 30 分操作が無かった・"expired" 旧形式の部屋・"limit" 部屋の作成の回数の上限・"closed" その他)。閉じたら null 以外。</summary>
    public string? ClosedReason { get; private set; }
    /// <summary>閉じる前の警告の残り(ms)。警告が無いときは null。</summary>
    public double? IdleWarnMs { get; private set; }

    /// <summary>作成者の画面に出す、部屋のパスワード(作成者だけ。ほかの人・作成者でない接続には null)。</summary>
    public string? OwnerPass => Me is { Owner: true } ? ownerPass : null;

    /// <summary>画面に出る状態が変わった(ステータス・参加者・表示名・追従など)。</summary>
    public event Action? Changed;

    /// <summary>部屋に入る(接続を始め、定期の送信・ping・名前の確認を始める)。</summary>
    public void Start()
    {
        pass = join.Pass ?? join.Create?.Pass;
        ownerKey = join.Create?.OwnerKey ?? transport.LoadOwnerKey(roomId);
        ownerPass = join.Create?.Pass ?? transport.LoadOwnerPass(roomId);
        createPending = join.Create is not null;
        tickTimer = transport.Timer(TickMs, true, Tick);
        pingTimer = transport.Timer(PingMs, true, Ping);
        nameTimer = transport.Timer(NamePollMs, true, PollName);
        Connect();
    }

    /// <summary>退室して、タイマー・接続・イベントを解放する。何度呼んでもよい。</summary>
    public void Dispose()
    {
        if (disposed) return;
        closing = true;
        disposed = true;
        core.EventEmitted -= OnLocal;
        foreach (var h in new[] { tickTimer, pingTimer, retryTimer, nameTimer }) transport.CancelTimer(h);
        tickTimer = pingTimer = retryTimer = nameTimer = -1;
        if (socket >= 0) transport.Close(socket, 1000, "leave");
        socket = -1;
        socketOpen = false;
        Me = null;
        peers.Clear();
    }

    /// <summary>ページを離れるとき: すぐに退出を知らせる(pagehide)。</summary>
    public void LeavePage()
    {
        closing = true;
        if (socket >= 0) transport.Close(socket, 1000, "leave");
    }

    // ---------------------------------------------------------------- 画面からの入力

    /// <summary>表示名の入力欄が変わった。確定は、最後の入力から 500 ms のあと(そのときの入力欄の値で決める)。</summary>
    public void NameTyped(string value)
    {
        if (disposed) return;
        nameInput = value;
        nameDirtyAt = transport.Now();
    }

    private void PollName()
    {
        if (disposed || nameDirtyAt < 0 || transport.Now() - nameDirtyAt < NameDebounceMs) return;
        nameDirtyAt = -1;
        var n = CleanName(nameInput);
        if (n.Length == 0 || n == Name) return;
        Name = n;
        transport.SaveName(n);
        Send(RoomWire.Name(n));
        Changed?.Invoke();
    }

    /// <summary>「視点に合わせる」(押した人の id)。同じ人をもう一度押すと、やめる。</summary>
    public void ToggleFollow(string id)
    {
        follow = follow == id ? null : id;
        var v = peers.FirstOrDefault(p => p.Id == id)?.View;
        if (follow is not null && v is not null) core.SetView(v);
        Changed?.Invoke();
    }

    /// <summary>鍵を入れて、もう一度入る(鍵の画面の送信)。鍵はこの画面のあいだだけメモリに持つ。</summary>
    public void SubmitPass(string value)
    {
        if (disposed || ClosedReason is not null) return;
        pass = value.Trim();   // 貼り付けた文字の前後の空白（末尾の改行など）は、鍵の一部にしない
        Gate = RoomGate.None;
        PassLimitMs = 0;
        retries = 0;
        if (socket >= 0) transport.Close(socket, 1000, "retry");
        socket = -1;
        socketOpen = false;
        Connect();
        Changed?.Invoke();
    }

    /// <summary>部屋を閉じる(作成者だけ。確認は画面で済ませてから呼ぶ)。作成者でなければ何もしない。</summary>
    public void RequestClose()
    {
        if (disposed || Me is not { Owner: true }) return;
        Send(RoomWire.Close());
    }

    /// <summary>ビューの上のポインター(clientX / clientY)。カーソルの送信は次の tick に。</summary>
    public void PointerAt(double clientX, double clientY)
    {
        var p = core.PointAt(clientX, clientY);
        pendingCursor = p is null ? null : [Round(p.Value.X, PositionScale), Round(p.Value.Y, PositionScale), Round(p.Value.Z, PositionScale)];
        hasPendingCursor = true;
    }

    /// <summary>ビューからポインターが出た。</summary>
    public void PointerLeft()
    {
        pendingCursor = null;
        hasPendingCursor = true;
    }

    /// <summary>ほかの人が操作中のオブジェクトは動かせない(room.js の canTransform)。理由を返す。</summary>
    public string? BlockedBy(IReadOnlyList<SceneObject> objects)
    {
        // 接続が無い間は、部屋の状態（ロック）が分からないので、動かさせない
        if (Me is null) return OfflineMoveText;
        foreach (var o in objects)
        {
            if (!locks.TryGetValue(o.Rid, out var who) || who == Me?.Id) continue;
            var name = peers.FirstOrDefault(p => p.Id == who)?.Name ?? "ほかの人";
            return $"{name}さんが操作中のため、終わるまで動かせません";
        }
        return null;
    }

    // ---------------------------------------------------------------- 通信
    private void Connect()
    {
        if (closing || disposed || ClosedReason is not null) return;
        SetStatus("接続中…", "");
        socket = transport.Open(roomId);
        socketOpen = false;
    }

    /// <summary>接続が開いた(hello を送る)。今の接続のものだけ扱う。</summary>
    public void OnOpen(int id)
    {
        if (disposed || id != socket) return;
        socketOpen = true;
        // 作成の指定は、welcome の前の最初の hello だけ送る。作成者の鍵は、この端末に保存する（作成のとき）
        var create = createPending ? join.Create : null;
        if (create is not null)
        {
            ownerKey = create.OwnerKey;
            transport.SaveOwnerKey(roomId, create.OwnerKey, create.Pass);
        }
        transport.Send(id, RoomWire.Hello(Name, ownerKey, pass, create));
    }

    /// <summary>接続が閉じた(code は WebSocket の close コード)。古い接続のものは無視する。</summary>
    public void OnClose(int id, int code)
    {
        if (disposed || id != socket || closing) return;
        socketOpen = false;
        // 切れた接続には送れないので、途中の操作は取り消す（Me を外してから。取り消しの通知は、送らずに無視される）
        Me = null;
        if (core.Op is not null) core.CancelOperation();
        // ロックと途中経過も、サーバーのものは無くなる（再接続で取り直す）
        lockHeld = false;
        previewed = [];
        lastPreview = "";
        locks.Clear();
        lockOrder.Clear();
        peers.Clear();
        Changed?.Invoke();
        // 閉じられた部屋・鍵の入力待ち・鍵の拒否: 自動では再接続しない（画面の案内に任せる）
        if (code == 4004) { ClosedReason ??= "closed"; transport.ForgetOwnerKey(roomId); return; }
        // 作成の回数の上限（4005）: 部屋は作られていない。保存した鍵・パスワードは消し、つなぎ直さない
        if (code == 4005) { ClosedReason ??= "limit"; transport.ForgetOwnerKey(roomId); SetStatus("部屋を作る回数の上限に達しました。しばらくしてからもう一度", "warn"); return; }
        if (code == 4003)
        {
            if (Gate == RoomGate.None) Gate = RoomGate.NeedPass;   // 理由のメッセージが無かったら、鍵を聞く
            SetStatus(Gate == RoomGate.PassLimited ? "鍵の入力が続けて違ったため、しばらく入れません" : "この部屋には鍵があります", "warn");
            return;
        }
        if (code == 4001) { SetStatus($"満員です（{MaxParticipants} 人まで）", "warn"); core.SetStatusText("この部屋は満員です。別の部屋を作ってください"); return; }
        if (code == 1008) { SetStatus("切断されました", "warn"); return; }
        // 閉じられた部屋（理由は closed の文字で届く）には、つなぎ直さない
        if (ClosedReason is not null) return;
        // 4000（しばらく通信がなかった）などは、つなぎ直す
        if (retries >= MaxRetries) { SetStatus("接続できません。ページを再読み込みしてください", "warn"); return; }
        var wait = Math.Min(MaxRetryWaitMs, 1000 * Math.Pow(2, retries));
        retries++;
        SetStatus(retries == 1 && code == 1006 && !everJoined ? "接続できません（この環境では部屋を使えない可能性があります）" : "再接続しています…", "warn");
        retryTimer = transport.Timer(wait, false, () => { retryTimer = -1; Connect(); });
    }

    /// <summary>サーバーからの文字(JSON)。形が違うものは、状態を変えずに捨てる。</summary>
    public void OnMessage(int id, string text)
    {
        if (disposed || id != socket) return;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) HandleMessage(doc.RootElement);
        }
        catch (JsonException)
        {
            // 壊れた JSON は捨てる
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // 想定外の形の文字で、ここまで来ないはずのもの。画面を止めず、理由だけ残す
            Console.Error.WriteLine($"[avatar] 部屋の文字を扱えません: {e.Message}");
        }
        Changed?.Invoke();
    }

    private void HandleMessage(JsonElement m)
    {
        if (!TryStr(m, "t", out var type)) return;
        switch (type)
        {
            case "welcome": ApplyWelcome(m); break;
            case "join": ApplyJoin(m); break;
            case "peer": ApplyPeer(m); break;
            case "leave": ApplyLeave(m); break;
            case "cursor": ApplyCursor(m); break;
            case "select": ApplySelect(m); break;
            case "locks": ApplyLocks(m); break;
            case "lockdenied": ApplyLockDenied(); break;
            case "lockexpired": ApplyLockExpired(); break;
            case "lockwarn": ApplyLockWarn(m); break;
            case "preview": ApplyPreviewMessage(m); break;
            case "ops": ApplyOps(m); break;
            case "view": ApplyView(m); break;
            case "needpass": Gate = RoomGate.NeedPass; break;
            case "wrongpass": Gate = RoomGate.WrongPass; break;
            case "passlimit": ApplyPassLimit(m); break;
            case "closed": ApplyClosed(m); break;
            case "idlewarn": ApplyIdleWarn(m); break;
            case "error":
                if (TryStr(m, "msg", out var msg) && msg == "too many objects") core.SetStatusText($"この部屋のオブジェクトは {MaxObjects} 個までです");
                break;
        }
    }

    /// <summary>鍵を続けて間違えて、しばらく拒否されている(passlimit の ms)。</summary>
    private void ApplyPassLimit(JsonElement m)
    {
        Gate = RoomGate.PassLimited;
        PassLimitMs = TryNum(m, "ms", out var ms) ? ms : 0;
    }

    /// <summary>部屋が閉じられた(reason は owner・idle。ほかは closed)。以後は再接続しない。鍵の保存も消す。</summary>
    private void ApplyClosed(JsonElement m)
    {
        var reason = TryStr(m, "reason", out var r) ? r : "";
        ClosedReason = reason is "owner" or "idle" or "expired" or "limit" ? reason : "closed";
        transport.ForgetOwnerKey(roomId);
        SetStatus("部屋は閉じられました", "warn");
    }

    /// <summary>閉じる前の警告(ms。null は取り消し)。</summary>
    private void ApplyIdleWarn(JsonElement m)
    {
        if (!TryGet(m, "ms", out var v)) return;
        IdleWarnMs = v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;
    }

    private void ApplyWelcome(JsonElement m)
    {
        if (!TryGet(m, "you", out var you) || !TryPeer(you, out var self)) return;
        if (!TryGet(m, "peers", out var pa) || pa.ValueKind != JsonValueKind.Array || !TryPeers(pa, out var newPeers)) return;
        if (!TryGet(m, "objects", out var oa) || oa.ValueKind != JsonValueKind.Array || !TryObjects(oa, out var objects)) return;
        // 部屋の名前・鍵つき（無ければ空と false）と、自分が作成者か
        var roomName = TryGet(m, "room", out var rv) && TryStr(rv, "name", out var rn) ? rn : "";
        var locked = TryGet(m, "room", out var rl) && TryGet(rl, "locked", out var lv) && lv.ValueKind == JsonValueKind.True;
        var owner = TryGet(you, "owner", out var ov) && ov.ValueKind == JsonValueKind.True;
        // ここまでで形は確かめた。以下で状態を変える
        Me = new RoomMe(self.Id, self.Name, self.Color, owner);
        RoomName = roomName;
        Locked = locked;
        Gate = RoomGate.None;
        PassLimitMs = 0;
        createPending = false;   // 作成は済んだ。以後の再接続では、作成の指定を送らない
        everJoined = true;
        retries = 0;
        peers.Clear();
        peers.AddRange(newPeers);
        core.LoadRoomScene(objects);
        server.Clear();
        foreach (var o in core.RoomState()) server.Set(o.Id, RoomWire.Object(o));
        SetStatus("接続しました", "ok");
        core.SetStatusText("クリックして選択。G / R / S で動かす（変更はほかの人にも見えます）");
        SendSelection();
    }

    private void ApplyJoin(JsonElement m)
    {
        if (!TryGet(m, "peer", out var jp) || !TryPeer(jp, out var p)) return;
        var index = peers.FindIndex(x => x.Id == p.Id);
        if (index >= 0) peers[index] = p; else peers.Add(p);
        lastView = "";   // 新しい人にも自分の視点を送る
    }

    private void ApplyPeer(JsonElement m)
    {
        if (!TryGet(m, "peer", out var pe) || !TryStr(pe, "id", out var id) || !TryStr(pe, "name", out var name)) return;
        if (id == Me?.Id) { Me = Me with { Name = name }; return; }
        var p = peers.FirstOrDefault(x => x.Id == id);
        if (p is not null) p.Name = name;
    }

    private void ApplyLeave(JsonElement m)
    {
        if (!TryStr(m, "id", out var id)) return;
        peers.RemoveAll(x => x.Id == id);
        if (follow == id) follow = null;
    }

    private void ApplyCursor(JsonElement m)
    {
        if (!TryStr(m, "from", out var from) || !TryGet(m, "p", out var pv)) return;
        double[]? cursor;
        if (pv.ValueKind == JsonValueKind.Null) cursor = null;
        else if (!TryNums(pv, 3, out var c)) return;
        else cursor = c;
        var p = peers.FirstOrDefault(x => x.Id == from);
        if (p is not null) p.Cursor = cursor;
    }

    private void ApplySelect(JsonElement m)
    {
        if (!TryStr(m, "from", out var from) || !TryGet(m, "ids", out var ids) || !TryStrings(ids, out var list)) return;
        var p = peers.FirstOrDefault(x => x.Id == from);
        if (p is not null) p.Selection = list;
    }

    private void ApplyLocks(JsonElement m)
    {
        if (!TryGet(m, "locks", out var lk) || lk.ValueKind != JsonValueKind.Object) return;
        var order = new List<string>();
        var map = new Dictionary<string, string>();
        foreach (var prop in lk.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;   // 値が人の id でないものは、ロックとして扱わない
            map[prop.Name] = prop.Value.GetString() ?? "";
            order.Add(prop.Name);
        }
        locks.Clear();
        foreach (var kv in map) locks[kv.Key] = kv.Value;
        lockOrder.Clear();
        lockOrder.AddRange(order);
        foreach (var p in peers) p.Locks = lockOrder.Where(rid => locks[rid] == p.Id).ToList();
    }

    private void ApplyLockDenied() => CancelLockedMove("ほかの人が操作中のため、動かせませんでした");

    /// <summary>同じ人が同じものを長く持ち続けて、上限(lockexpired)に達した: 操作を解除し、ほかの人が動かせるようにしたことを知らせる。</summary>
    private void ApplyLockExpired() => CancelLockedMove("同じものを長く動かしたため、操作を解除しました。少し待ってからやり直してください");

    /// <summary>上限が近い(lockwarn): 残りの時間を知らせる。操作はそのまま続く。</summary>
    private void ApplyLockWarn(JsonElement m)
    {
        if (!TryGet(m, "ms", out var ms) || ms.ValueKind != JsonValueKind.Number || !ms.TryGetDouble(out var value)) return;
        core.SetStatusText($"同じものを長く動かしているため、あと約 {Math.Max(1, (int)Math.Ceiling(value / 1000))} 秒で操作が解除されます");
    }

    /// <summary>こちらの操作をやめる(room.js の lockdenied。解除の文字は、キャンセルのときに送られる)。</summary>
    private void CancelLockedMove(string text)
    {
        if (core.Op is { Edit: false })
        {
            core.CancelOperation();
            core.SetStatusText(text);
        }
        lockHeld = false;
    }

    private void ApplyPreviewMessage(JsonElement m)
    {
        if (!TryGet(m, "objs", out var objs) || objs.ValueKind != JsonValueKind.Array) return;
        var list = new List<(string Id, double[] P, double[] R, double[] S)>();
        foreach (var d in objs.EnumerateArray())
        {
            if (!TryStr(d, "id", out var id) || !TryNumsOf(d, "p", 3, out var p) || !TryNumsOf(d, "r", 3, out var r) || !TryNumsOf(d, "s", 3, out var s)) return;
            list.Add((id, p, r, s));
        }
        foreach (var d in list) core.ApplyPreview(d.Id, d.P, d.R, d.S);   // 自分が操作中のものは当てない(ViewportCore が見る)
    }

    private void ApplyOps(JsonElement m)
    {
        if (!OptionalArray(m, "upsert", out var up) || !TryObjects(up, out var upsert)) return;
        if (!OptionalArray(m, "delete", out var del) || !TryStringsOf(del, out var delete)) return;
        // ここまでで形は確かめた
        core.ApplyRemote(upsert, delete);
        foreach (var o in upsert)
        {
            var state = core.RoomState().FirstOrDefault(x => x.Id == o.Id);
            server.Set(o.Id, RoomWire.Object(state ?? o));
        }
        foreach (var id in delete) server.Delete(id);
        if (IsTruthy(m, "rejected")) core.SetStatusText("ほかの人の操作と重なったため、一部の変更は反映されませんでした");
    }

    private void ApplyView(JsonElement m)
    {
        if (!TryStr(m, "from", out var from) || !TryGet(m, "view", out var vv) || !TryView(vv, out var v)) return;
        var p = peers.FirstOrDefault(x => x.Id == from);
        if (p is null) return;
        p.View = v;
        if (follow == p.Id) core.SetView(v);
    }

    private void Send(string text)
    {
        if (socketOpen && Me is not null && socket >= 0) transport.Send(socket, text);
    }

    private void SetStatus(string text, string kind)
    {
        StatusText = text;
        StatusKind = kind;
    }

    // ---------------------------------------------------------------- 自分の操作
    private void OnLocal(ViewEvent e)
    {
        if (disposed) return;
        if (Me is null)
        {
            // 接続が無い間の変更は、部屋に届かない。再接続のとき部屋の内容に置き換わるので、黙って消えないように知らせる
            if (e.Type is "add" or "delete" or "duplicate" or "undo" or "redo" or "edit" or "separate" or "confirm")
            {
                core.SetStatusText(OfflineEditText);
                Changed?.Invoke();
            }
            return;
        }
        var edit = e.Detail.TryGetValue("edit", out var ed) && ed is true;
        switch (e.Type)
        {
            case "opstart":
                if (!edit && core.Op is not null)
                {
                    lockHeld = true;
                    lastLockAt = transport.Now();
                    Send(RoomWire.Lock(core.Op.Start.Select(s => s.Object.Rid)));
                }
                break;
            case "confirm":
            case "cancel":
                // 途中経過を送ったものは、キャンセルで元に戻った場合も含めて最終の状態を必ず送る
                SyncOut(previewed);
                previewed = [];
                if (lockHeld) { Send(RoomWire.Unlock()); lockHeld = false; }
                lastPreview = "";
                break;
            case "select":
                if (!edit) SendSelection();
                break;
            case "view":
                // 自分で視点を動かしたら「視点に合わせる」をやめる
                if (follow is not null) follow = null;
                break;
            case "add": case "delete": case "duplicate": case "undo": case "redo": case "edit": case "separate": case "mode":
                SyncOut(new HashSet<string>());
                if (e.Type != "mode") SendSelection();
                break;
        }
        Changed?.Invoke();
    }

    private void SendSelection() => Send(RoomWire.Select(core.Selected.Select(o => o.Rid)));

    /// <summary>サーバーの状態との差分を送る。操作中のオブジェクトは確定してから送る。force の印のものは、差が無くても送る。</summary>
    private void SyncOut(IReadOnlySet<string> force)
    {
        if (Me is null) return;
        var moving = new HashSet<string>(core.Op is { Edit: false } op ? op.Start.Select(s => s.Object.Rid) : []);
        var upsert = new List<string>();
        var seen = new HashSet<string>();
        foreach (var o in core.RoomState())
        {
            seen.Add(o.Id);
            if (moving.Contains(o.Id)) continue;
            var j = RoomWire.Object(o);
            if (server.Get(o.Id) != j || force.Contains(o.Id)) { upsert.Add(j); server.Set(o.Id, j); }
        }
        var del = server.Keys.Where(id => !seen.Contains(id) && !moving.Contains(id)).ToList();
        foreach (var id in del) server.Delete(id);
        if (upsert.Count > 0 || del.Count > 0) Send(RoomWire.Ops(upsert, del));
    }

    // ---------------------------------------------------------------- 定期
    /// <summary>80 ms ごと: 操作中の変形・カーソル・視点を、変わっていれば送る。</summary>
    public void Tick()
    {
        if (disposed || Me is null) return;
        var op = core.Op;
        var now = transport.Now();
        if (op is { Edit: false } && lockHeld)
        {
            var ids = new HashSet<string>(op.Start.Select(s => s.Object.Rid));
            var objs = core.RoomState().Where(o => ids.Contains(o.Id)).Select(o => new RoomPreview(o.Id, o.P, o.R, o.S)).ToList();
            var message = RoomWire.Preview(objs);
            if (message != lastPreview && objs.Count > 0)
            {
                lastPreview = message;
                foreach (var o in objs) previewed.Add(o.Id);
                Send(message);
            }
            // ロックは 15 秒で切れる。操作が続くあいだは取り直す（ほかの人が取っていたら lockdenied が返り、こちらの操作はやめる）
            if (now - lastLockAt >= LockRenewMs)
            {
                lastLockAt = now;
                Send(RoomWire.Lock(ids));
            }
        }
        if (hasPendingCursor)
        {
            Send(RoomWire.Cursor(pendingCursor));
            hasPendingCursor = false;
            pendingCursor = null;
        }
        if (peers.Count > 0 && now - lastViewAt > ViewMs)
        {
            var v = core.GetView();
            var rounded = new ViewState(v.Target.Select(n => Round(n, ViewScale)).ToArray(), Round(v.Dist, ViewScale), Round(v.Theta, AngleScale), Round(v.Phi, AngleScale), v.Ortho, v.Name);
            var j = RoomWire.View(rounded);
            if (j != lastView)
            {
                lastView = j;
                lastViewAt = now;
                Send(j);
            }
        }
    }

    /// <summary>20 秒ごと: 接続が生きていることを知らせる(サーバーは 60 秒何も届かない接続を外す)。</summary>
    public void Ping()
    {
        if (disposed || !socketOpen || socket < 0) return;
        transport.Send(socket, RoomWire.Ping());
    }

    // ---------------------------------------------------------------- 文字・数
    /// <summary>表示名: 制御文字と HTML の特殊文字（&lt; &gt; &amp; " '）を除き、前後の空白を取って max 文字まで（既定 20。部屋名は 40。worker/index.js の cleanName と同じ処理。コードポイント単位で切る）。</summary>
    public static string CleanName(string v, int max = 20)
    {
        var s = new string(v.Where(c => !IsRemovedInName(c)).ToArray()).Trim();
        var units = new List<string>();
        for (var i = 0; i < s.Length && units.Count < max; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { units.Add(s.Substring(i, 2)); i++; }
            else units.Add(s[i].ToString());
        }
        return string.Concat(units);
    }

    /// <summary>表示名から取り除く文字: 制御文字(C0・C1)・ゼロ幅の文字・行の区切り・書式の文字・BOM・HTML の特殊文字（worker/index.js の cleanName の正規表現と同じ範囲）。</summary>
    private static bool IsRemovedInName(char c) =>
        c <= 0x1f || (c >= 0x7f && c <= 0x9f) || (c >= 0x200b && c <= 0x200f) || (c >= 0x2028 && c <= 0x202e) || (c >= 0x2060 && c <= 0x206f) || c == 0xfeff
        || c is '<' or '>' or '&' or '"' or '\'';

    /// <summary>起動時の表示名: 保存された名前(あれば)か、「ゲスト」と乱数(10〜99)。</summary>
    public static string InitialName(string? stored, int guestNumber)
    {
        var n = CleanName(stored ?? "");
        return n.Length > 0 ? n : $"ゲスト{guestNumber}";
    }

    /// <summary>scale で丸める（JavaScript の Math.round と同じ向き）。</summary>
    private static double Round(double n, double scale) => Js.Round(n * scale) / scale;

    // ---------------------------------------------------------------- サーバーの文字の形(不正なものは false)
    private static bool TryGet(JsonElement e, string name, out JsonElement v)
    {
        v = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out v);
    }

    private static bool TryStr(JsonElement e, string name, out string s)
    {
        s = "";
        if (!TryGet(e, name, out var v) || v.ValueKind != JsonValueKind.String) return false;
        s = v.GetString() ?? "";
        return true;
    }

    private static bool TryNum(JsonElement e, string name, out double d)
    {
        d = 0;
        return TryGet(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out d) && double.IsFinite(d);
    }

    /// <summary>name の配列(長さ length・全部が有限の数)。</summary>
    private static bool TryNumsOf(JsonElement e, string name, int length, out double[] a)
    {
        a = [];
        return TryGet(e, name, out var v) && TryNums(v, length, out a);
    }

    private static bool TryNums(JsonElement e, int length, out double[] a)
    {
        a = [];
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != length) return false;
        var arr = new double[length];
        var i = 0;
        foreach (var x in e.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.Number || !x.TryGetDouble(out var d) || !double.IsFinite(d)) return false;
            arr[i++] = d;
        }
        a = arr;
        return true;
    }

    private static bool TryStrings(JsonElement e, out IReadOnlyList<string> list)
    {
        list = [];
        if (e.ValueKind != JsonValueKind.Array) return false;
        var items = new List<string>();
        foreach (var x in e.EnumerateArray())
        {
            if (x.ValueKind != JsonValueKind.String) return false;
            items.Add(x.GetString() ?? "");
        }
        list = items;
        return true;
    }

    /// <summary>配列(delete)。無い・null は空。配列でなければ false。</summary>
    private static bool TryStringsOf(JsonElement e, out IReadOnlyList<string> list)
    {
        list = [];
        return e.ValueKind == JsonValueKind.Undefined || e.ValueKind == JsonValueKind.Null || TryStrings(e, out list);
    }

    /// <summary>任意の配列(upsert)。無い・null は空の配列として扱う。配列でなければ false。</summary>
    private static bool OptionalArray(JsonElement m, string name, out JsonElement arr)
    {
        arr = default;
        if (!TryGet(m, name, out var v) || v.ValueKind == JsonValueKind.Null) return true;
        arr = v;
        return v.ValueKind == JsonValueKind.Array;
    }

    /// <summary>JavaScript の真偽(undefined・null・false・0・NaN・空の文字は偽。それ以外は真)。</summary>
    private static bool IsTruthy(JsonElement m, string name)
    {
        if (!TryGet(m, name, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => (v.GetString() ?? "").Length > 0,
            JsonValueKind.Number => v.TryGetDouble(out var d) && d != 0 && !double.IsNaN(d),
            JsonValueKind.Object or JsonValueKind.Array => true,
            _ => false,
        };
    }

    private static bool TryPeer(JsonElement p, out RoomPeer peer)
    {
        peer = null!;
        if (!TryStr(p, "id", out var id) || !TryStr(p, "name", out var name) || !TryStr(p, "color", out var color)) return false;
        peer = new RoomPeer { Id = id, Name = name, Color = color };
        return true;
    }

    private static bool TryPeers(JsonElement array, out List<RoomPeer> list)
    {
        list = [];
        foreach (var p in array.EnumerateArray())
        {
            if (!TryPeer(p, out var peer)) return false;
            list.Add(peer);
        }
        return true;
    }

    private static bool TryObject(JsonElement o, out RoomObject r)
    {
        r = null!;
        if (!TryStr(o, "id", out var id) || !TryStr(o, "kind", out var kind) || !TryStr(o, "name", out var name)) return false;
        if (!TryNumsOf(o, "p", 3, out var p) || !TryNumsOf(o, "r", 3, out var rot) || !TryNumsOf(o, "s", 3, out var s)) return false;
        r = new RoomObject(id, kind, name, p, rot, s);
        return true;
    }

    /// <summary>オブジェクトの配列。無い・null は空（upsert を省いた ops でも、delete は反映する）。配列でなければ false。</summary>
    private static bool TryObjects(JsonElement array, out RoomObject[] list)
    {
        list = [];
        if (array.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return true;
        if (array.ValueKind != JsonValueKind.Array) return false;
        var items = new List<RoomObject>();
        foreach (var o in array.EnumerateArray())
        {
            if (!TryObject(o, out var r)) return false;
            items.Add(r);
        }
        list = [.. items];
        return true;
    }

    private static bool TryView(JsonElement v, out ViewState view)
    {
        view = null!;
        if (!TryNumsOf(v, "target", 3, out var target) || !TryNum(v, "dist", out var dist) || !TryNum(v, "theta", out var theta) || !TryNum(v, "phi", out var phi)) return false;
        if (!TryStr(v, "name", out var name)) return false;
        var ortho = TryGet(v, "ortho", out var o) && o.ValueKind == JsonValueKind.True;
        view = new ViewState(target, dist, theta, phi, ortho, name);
        return true;
    }
}

/// <summary>挿入の順を保つ対応表(JavaScript の Map と同じ: 既存の鍵への代入は位置を変えず、削除して足すと最後に来る)。</summary>
public sealed class OrderedMap
{
    private readonly List<string> keys = [];
    private readonly Dictionary<string, string> values = [];

    public IReadOnlyList<string> Keys => keys;

    public string? Get(string key) => values.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value)
    {
        if (!values.ContainsKey(key)) keys.Add(key);
        values[key] = value;
    }

    public void Delete(string key)
    {
        if (values.Remove(key)) keys.Remove(key);
    }

    public void Clear()
    {
        keys.Clear();
        values.Clear();
    }
}
