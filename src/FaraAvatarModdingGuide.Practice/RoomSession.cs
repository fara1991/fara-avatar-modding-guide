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

    /// <summary>performance.now()(ms)。視点の送信の間隔に使う。</summary>
    double Now();

    /// <summary>表示名を保存する(localStorage)。</summary>
    void SaveName(string name);
}

/// <summary>参加者の自分(welcome の you)。</summary>
public sealed record RoomMe(string Id, string Name, string Color);

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
/// </summary>
public sealed class RoomSession : IDisposable
{
    public const double TickMs = 80;
    public const double PingMs = 20000;
    private const double ViewMs = 250;
    private const int MaxRetries = 6;

    private readonly ViewportCore core;
    private readonly IRoomTransport transport;
    private readonly string roomId;
    private readonly List<RoomPeer> peers = [];
    private readonly OrderedMap server = new();                 // rid → サーバーが持っている状態(roomState の JSON 文字)。差分を作るのに使う
    private readonly Dictionary<string, string> locks = [];     // rid → 操作中の人の id
    private readonly List<string> lockOrder = [];               // locks の順(JSON の順。peer.locks の並びに使う)
    private HashSet<string> previewed = [];
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
    private string nameInput;

    public RoomSession(ViewportCore core, IRoomTransport transport, string roomId, string name)
    {
        this.core = core;
        this.transport = transport;
        this.roomId = roomId;
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

    /// <summary>画面に出る状態が変わった(ステータス・参加者・表示名・追従など)。</summary>
    public event Action? Changed;

    /// <summary>部屋に入る(接続を始め、定期の送信・ping を始める)。</summary>
    public void Start()
    {
        tickTimer = transport.Timer(TickMs, true, Tick);
        pingTimer = transport.Timer(PingMs, true, Ping);
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

    /// <summary>表示名の入力欄が変わった(500 ms のあと、そのときの入力欄の値で名前を決める)。</summary>
    public void NameTyped(string value)
    {
        nameInput = value;
        if (disposed) return;
        transport.CancelTimer(nameTimer);
        nameTimer = transport.Timer(500, false, () =>
        {
            nameTimer = -1;
            var n = CleanName(nameInput);
            if (n.Length == 0 || n == Name) return;
            Name = n;
            transport.SaveName(n);
            Send(RoomWire.Name(n));
            Changed?.Invoke();
        });
    }

    /// <summary>「視点に合わせる」(押した人の id)。同じ人をもう一度押すと、やめる。</summary>
    public void ToggleFollow(string id)
    {
        follow = follow == id ? null : id;
        var v = peers.FirstOrDefault(p => p.Id == id)?.View;
        if (follow is not null && v is not null) core.SetView(v);
        Changed?.Invoke();
    }

    /// <summary>ビューの上のポインター(clientX / clientY)。カーソルの送信は次の tick に。</summary>
    public void PointerAt(double clientX, double clientY)
    {
        var p = core.PointAt(clientX, clientY);
        pendingCursor = p is null ? null : [Round3(p.Value.X), Round3(p.Value.Y), Round3(p.Value.Z)];
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
        if (closing || disposed) return;
        SetStatus("接続中…", "");
        socket = transport.Open(roomId);
        socketOpen = false;
    }

    /// <summary>接続が開いた(hello を送る)。</summary>
    public void OnOpen(int id)
    {
        if (disposed) return;
        if (id == socket) socketOpen = true;
        transport.Send(id, RoomWire.Hello(Name));
    }

    /// <summary>接続が閉じた(code は WebSocket の close コード)。古い接続のものは無視する。</summary>
    public void OnClose(int id, int code)
    {
        if (disposed || id != socket || closing) return;
        socketOpen = false;
        Me = null;
        peers.Clear();
        Changed?.Invoke();
        if (code == 4001) { SetStatus("満員です（8 人まで）", "warn"); core.SetStatusText("この部屋は満員です。別の部屋を作ってください"); return; }
        if (code == 1008) { SetStatus("切断されました", "warn"); return; }
        // 4000（しばらく通信がなかった）などは、つなぎ直す
        if (retries >= MaxRetries) { SetStatus("接続できません。ページを再読み込みしてください", "warn"); return; }
        var wait = Math.Min(15000, 1000 * Math.Pow(2, retries));
        retries++;
        SetStatus(retries == 1 && code == 1006 && !everJoined ? "接続できません（この環境では部屋を使えない可能性があります）" : "再接続しています…", "warn");
        retryTimer = transport.Timer(wait, false, () => { retryTimer = -1; Connect(); });
    }

    /// <summary>サーバーからの文字(JSON)。</summary>
    public void OnMessage(int id, string text)
    {
        if (disposed || id != socket) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException) { return; }
        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object) HandleMessage(doc.RootElement);
        }
        Changed?.Invoke();
    }

    private void HandleMessage(JsonElement m)
    {
        switch (Str(m, "t"))
        {
            case "welcome":
            {
                var you = m.GetProperty("you");
                Me = new RoomMe(Str(you, "id"), Str(you, "name"), Str(you, "color"));
                everJoined = true;
                retries = 0;
                peers.Clear();
                foreach (var p in m.GetProperty("peers").EnumerateArray()) peers.Add(NewPeer(p));
                core.LoadRoomScene(Objects(m.GetProperty("objects")));
                server.Clear();
                foreach (var o in core.RoomState()) server.Set(o.Id, RoomWire.Object(o));
                SetStatus("接続しました", "ok");
                core.SetStatusText("クリックして選択。G / R / S で動かす（変更はほかの人にも見えます）");
                SendSelection();
                break;
            }
            case "join":
            {
                var p = NewPeer(m.GetProperty("peer"));
                var index = peers.FindIndex(x => x.Id == p.Id);
                if (index >= 0) peers[index] = p; else peers.Add(p);
                lastView = "";   // 新しい人にも自分の視点を送る
                break;
            }
            case "peer":
            {
                var peer = m.GetProperty("peer");
                var id = Str(peer, "id");
                if (id == Me?.Id) { Me = Me with { Name = Str(peer, "name") }; break; }
                var p = peers.FirstOrDefault(x => x.Id == id);
                if (p is not null) p.Name = Str(peer, "name");
                break;
            }
            case "leave":
            {
                var id = Str(m, "id");
                peers.RemoveAll(x => x.Id == id);
                if (follow == id) follow = null;
                break;
            }
            case "cursor":
            {
                var p = peers.FirstOrDefault(x => x.Id == Str(m, "from"));
                if (p is not null) p.Cursor = Nums(m.GetProperty("p"));
                break;
            }
            case "select":
            {
                var p = peers.FirstOrDefault(x => x.Id == Str(m, "from"));
                if (p is not null) p.Selection = Strings(m.GetProperty("ids"));
                break;
            }
            case "locks":
            {
                locks.Clear();
                lockOrder.Clear();
                foreach (var prop in m.GetProperty("locks").EnumerateObject())
                {
                    locks[prop.Name] = prop.Value.GetString() ?? "";
                    lockOrder.Add(prop.Name);
                }
                foreach (var p in peers) p.Locks = lockOrder.Where(rid => locks[rid] == p.Id).ToList();
                break;
            }
            case "lockdenied":
                // 同時に動かし始めたときなど: こちらの操作をやめる
                if (core.Op is { Edit: false })
                {
                    core.CancelOperation();
                    core.SetStatusText("ほかの人が操作中のため、動かせませんでした");
                }
                lockHeld = false;
                break;
            case "preview":
                foreach (var d in m.GetProperty("objs").EnumerateArray())
                    core.ApplyPreview(Str(d, "id"), Nums(d.GetProperty("p")), Nums(d.GetProperty("r")), Nums(d.GetProperty("s")));
                break;
            case "ops":
            {
                var upsert = m.TryGetProperty("upsert", out var up) ? Objects(up) : [];
                var delete = m.TryGetProperty("delete", out var del) ? Strings(del) : [];
                core.ApplyRemote(upsert, delete);
                foreach (var o in upsert)
                {
                    var state = core.RoomState().FirstOrDefault(x => x.Id == o.Id);
                    server.Set(o.Id, RoomWire.Object(state ?? o));
                }
                foreach (var id in delete) server.Delete(id);
                if (m.TryGetProperty("rejected", out var rejected) && rejected.ValueKind == JsonValueKind.True)
                    core.SetStatusText("ほかの人の操作と重なったため、一部の変更は反映されませんでした");
                break;
            }
            case "view":
            {
                var p = peers.FirstOrDefault(x => x.Id == Str(m, "from"));
                if (p is null) break;
                var v = View(m.GetProperty("view"));
                p.View = v;
                if (follow == p.Id) core.SetView(v);
                break;
            }
            case "error":
                if (Str(m, "msg") == "too many objects") core.SetStatusText("この部屋のオブジェクトは 200 個までです");
                break;
        }
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
        if (disposed || Me is null) return;
        var edit = e.Detail.TryGetValue("edit", out var ed) && ed is true;
        switch (e.Type)
        {
            case "opstart":
                if (!edit && core.Op is not null)
                {
                    lockHeld = true;
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

    /// <summary>サーバーの状態との差分を送る。操作中のオブジェクトは確定してから送る。</summary>
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
        }
        if (hasPendingCursor)
        {
            Send(RoomWire.Cursor(pendingCursor));
            hasPendingCursor = false;
            pendingCursor = null;
        }
        var now = transport.Now();
        if (peers.Count > 0 && now - lastViewAt > ViewMs)
        {
            var v = core.GetView();
            var rounded = new ViewState(v.Target.Select(n => Round(n, 1000)).ToArray(), Round(v.Dist, 1000), Round(v.Theta, 10000), Round(v.Phi, 10000), v.Ortho, v.Name);
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
    /// <summary>表示名から取り除く文字: 制御文字(C0・C1)・ゼロ幅の文字・行の区切り・書式の文字・BOM(room.js の cleanName と同じ範囲)。</summary>
    private static bool IsRemovedInName(char c) =>
        c <= 0x1f || (c >= 0x7f && c <= 0x9f) || (c >= 0x200b && c <= 0x200f) || (c >= 0x2028 && c <= 0x202e) || (c >= 0x2060 && c <= 0x206f) || c == 0xfeff;

    /// <summary>表示名: 制御文字を除いて 20 文字まで(サーバーでも同じ処理をする)。JavaScript の trim と、コードポイント単位の切り取りに合わせる。</summary>
    public static string CleanName(string v)
    {
        var s = new string(v.Where(c => !IsRemovedInName(c)).ToArray()).Trim();
        var units = new List<string>();
        for (var i = 0; i < s.Length && units.Count < 20; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { units.Add(s.Substring(i, 2)); i++; }
            else units.Add(s[i].ToString());
        }
        return string.Concat(units);
    }

    /// <summary>起動時の表示名: 保存された名前(あれば)か、「ゲスト」と乱数(10〜99)。</summary>
    public static string InitialName(string? stored, int guestNumber)
    {
        var n = CleanName(stored ?? "");
        return n.Length > 0 ? n : $"ゲスト{guestNumber}";
    }

    private static double Round3(double n) => Round(n, 1000);
    private static double Round(double n, double scale) => Js.Round(n * scale) / scale;

    private static RoomPeer NewPeer(JsonElement p) => new()
    {
        Id = Str(p, "id"), Name = Str(p, "name"), Color = Str(p, "color"),
    };

    private static RoomObject[] Objects(JsonElement array) =>
        array.EnumerateArray().Select(o => new RoomObject(Str(o, "id"), Str(o, "kind"), Str(o, "name"), Nums(o.GetProperty("p")), Nums(o.GetProperty("r")), Nums(o.GetProperty("s")))).ToArray();

    private static ViewState View(JsonElement v) => new(
        Nums(v.GetProperty("target")), v.GetProperty("dist").GetDouble(), v.GetProperty("theta").GetDouble(), v.GetProperty("phi").GetDouble(),
        v.TryGetProperty("ortho", out var o) && o.ValueKind == JsonValueKind.True, Str(v, "name"));

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double[] Nums(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(x => x.GetDouble()).ToArray() : [];

    private static IReadOnlyList<string> Strings(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
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
