namespace FaraAvatarModdingGuide.Practice;

/// <summary>ほかの参加者のカーソル(画面の位置)。Hidden は、カーソルが無い・画面の外・奥に隠れたとき(room.js の updateRemoteCursors と同じ条件)。</summary>
public sealed record RemoteCursor(string Id, string Color, bool Hidden, double X, double Y);

/// <summary>
/// 共同練習(room.js)の表示の口: ほかの人の選択・操作中の色の枠(<see cref="RemoteColors"/>)、カーソルの画面の位置(<see cref="RemoteCursors"/>)、途中経過の適用(<see cref="ApplyPreview"/>)。
/// 参加者の一覧(<see cref="RemotePeers"/>)は <see cref="RoomSession"/> が持つ。
/// </summary>
public sealed partial class ViewportCore
{
    /// <summary>ほかの参加者(room.js の setRemotePeers の配列)。描画と、カーソルの位置に使う。</summary>
    public IReadOnlyList<RoomPeer> RemotePeers { get; private set; } = [];

    public void SetRemotePeers(IReadOnlyList<RoomPeer> peers) => RemotePeers = peers;

    /// <summary>オブジェクト(rid)ごとの枠の色。選択はその人の色(先に選んだ人が優先)、操作中の人の色は選択より優先する(room.js の updateRemoteSelection)。</summary>
    public IReadOnlyDictionary<string, string> RemoteColors()
    {
        var color = new Dictionary<string, string>();
        foreach (var p in RemotePeers)
        {
            foreach (var id in p.Selection) color.TryAdd(id, p.Color);
            foreach (var id in p.Locks) color[id] = p.Color;   // 操作中の人を優先
        }
        return color;
    }

    /// <summary>ほかの参加者のカーソルの画面の位置(ビューの左上が原点・CSS ピクセル)。room.js の updateRemoteCursors と同じ式と条件。</summary>
    public IReadOnlyList<RemoteCursor> RemoteCursors()
    {
        var list = new List<RemoteCursor>(RemotePeers.Count);
        foreach (var p in RemotePeers)
        {
            if (p.Cursor is not { Length: 3 } c) { list.Add(new RemoteCursor(p.Id, p.Color, true, 0, 0)); continue; }
            var world = new Vec3(c[0], c[1], c[2]);
            var ndc = View.ProjectPoint(world);
            if (ndc.Z > 1 || Math.Abs(ndc.X) > 1.2 || Math.Abs(ndc.Y) > 1.2) { list.Add(new RemoteCursor(p.Id, p.Color, true, 0, 0)); continue; }
            var (x, y) = ToScreen(world);
            list.Add(new RemoteCursor(p.Id, p.Color, false, x, y));
        }
        return list;
    }

    /// <summary>
    /// ほかの人の途中経過(preview)を当てる。自分が操作中のオブジェクトは当てない(room.js の preview)。イベントは出さない。
    /// 当てたら true。該当の id が無い・操作中なら false。
    /// </summary>
    public bool ApplyPreview(string rid, double[] p, double[] r, double[] s)
    {
        var o = Objects.FirstOrDefault(x => x.Rid == rid);
        if (o is null || p.Length != 3 || r.Length != 3 || s.Length != 3) return false;
        if (Op is not null && Op.Start.Any(st => st.Object == o)) return false;
        o.Position = new Vec3(p[0], p[1], p[2]);
        o.SetRotation(r[0], r[1], r[2]);
        o.Scale = new Vec3(s[0], s[1], s[2]);
        return true;
    }
}
