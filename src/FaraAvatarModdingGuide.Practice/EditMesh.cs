namespace FaraAvatarModdingGuide.Practice;

/// <summary>編集モードのメッシュの頂点(editmesh.js の { id, co })。同じ頂点かどうかは参照で見る(JavaScript の Set・indexOf と同じ)。</summary>
public sealed class EmVert
{
    private static int nextId = 1;

    public EmVert(Vec3 co)
    {
        Id = Interlocked.Increment(ref nextId) - 1;
        Co = co;
    }

    /// <summary>辺のキー(小さい番号が先)を作るための番号。メッシュの外からは見えない。</summary>
    public int Id { get; }

    /// <summary>オブジェクトのローカル座標。</summary>
    public Vec3 Co { get; set; }
}

/// <summary>面(editmesh.js の { v })。頂点は表から見て反時計回り。<see cref="V"/> は丸ごと差し替えられる(JavaScript の f.v = ...)。</summary>
public sealed class EmFace
{
    public EmFace(List<EmVert> v) { V = v; }
    public List<EmVert> V { get; set; }
}

/// <summary>辺の選択に使うキー(editmesh.js の "id-id" の文字。小さい番号が先)。</summary>
public readonly record struct EdgeKey(int A, int B)
{
    public static EdgeKey Of(EmVert a, EmVert b) => a.Id < b.Id ? new EdgeKey(a.Id, b.Id) : new EdgeKey(b.Id, a.Id);
}

/// <summary>辺(editmesh.js の { a, b, key, faces })。a → b は、その辺を最初に見つけた面の向き。</summary>
public sealed class EmEdge
{
    public EmEdge(EmVert a, EmVert b, EdgeKey key) { A = a; B = b; Key = key; }
    public EmVert A { get; }
    public EmVert B { get; }
    public EdgeKey Key { get; }
    public List<EmFace> Faces { get; } = [];
}

/// <summary>頂点のまわりの面を順に並べた 1 要素(editmesh.js の fan)。</summary>
public sealed class FanItem
{
    public FanItem(EmFace face, EmVert prev, EmVert next) { Face = face; Prev = prev; Next = next; }
    public EmFace Face { get; }
    public EmVert Prev { get; }
    public EmVert Next { get; }
}

/// <summary>押し出し(E)の結果。Type は "region" / "edge" / "vert"。Normal は面の押し出しのときの平均の法線(ローカル)。</summary>
public sealed record ExtrudeResult(string Type, IReadOnlyList<EmVert> Verts, Vec3? Normal);

/// <summary>頂点を base + dir × t で動かす指示(差し込み・ベベル)。</summary>
public sealed record MoveVert(EmVert V, Vec3 Base, Vec3 Dir);

/// <summary>差し込み(I)の結果。Limit は厚さの上限。</summary>
public sealed record InsetResult(IReadOnlyList<MoveVert> Moves, double Limit);

/// <summary>ベベル(Ctrl+B)の結果。対応していない形のときは Error に理由が入る。</summary>
public sealed record BevelResult(IReadOnlyList<MoveVert> Moves, double Limit, string? Error = null);

/// <summary>ループカットで入れた頂点の位置: a → b の t(+ ずらし量)。</summary>
public sealed record SlideVert(EmVert V, EmVert A, EmVert B, double T);

/// <summary>ループカットの結果。</summary>
public sealed record LoopCutResult(IReadOnlyList<EmVert> Verts, IReadOnlyList<SlideVert> Slide, int Cuts);

/// <summary>辺リングの 1 本(向きをそろえた a → b と、元の辺)。</summary>
public sealed record RingEdge(EmVert A, EmVert B, EmEdge E);

/// <summary>辺リングの結果。Ring は辺、Faces は間の四角形。Closed なら一周している。</summary>
public sealed record EdgeRingResult(List<RingEdge> Ring, List<EmFace> Faces, bool Closed);

/// <summary>
/// 編集モード用のメッシュ(頂点・辺・面)と操作(editmesh.js の EditMesh の写し)。描画やマウスには関わらず、形と選択と操作だけを扱う。
/// - 頂点は <see cref="EmVert"/>(ローカル座標)、面は <see cref="EmFace"/>(表から見て反時計回り)
/// - 辺は面と「辺だけの辺」(<see cref="Loose"/>)から作る。作った辺は、<see cref="Changed"/> を呼ぶまで使い回す(現行と同じ。
///   途中で面を書き換えて Changed を呼ばないと古い辺を見る。その呼ぶ位置も現行の JavaScript と同じにしてある)
/// - 選択は選択モード(vert / edge / face)ごとに主になる集合を持ち、<see cref="Flush"/> でほかを導く。集合は追加した順を保つ(<see cref="OrderedSet{T}"/>)
/// 式・順序・集合の順番は現行の JavaScript と同じ(メッシュの結果が1つの頂点の順番まで一致する)。
/// </summary>
public sealed class EditMesh
{
    private sealed class EdgeCache
    {
        public OrderedDictionary<EdgeKey, EmEdge> Edges = new();
        public Dictionary<EmVert, List<EmEdge>> VertEdges = new();
    }

    public List<EmVert> Verts { get; set; } = [];
    public List<EmFace> Faces { get; set; } = [];
    /// <summary>面に属さない辺 [[a, b], ...]。</summary>
    public List<(EmVert A, EmVert B)> Loose { get; set; } = [];
    public OrderedSet<EmVert> Vsel { get; set; } = new();
    public OrderedSet<EdgeKey> Esel { get; set; } = new();
    public OrderedSet<EmFace> Fsel { get; set; } = new();

    private EdgeCache? cache;

    // ---------------------------------------------------------------- 作成・保存
    public static EditMesh FromData(MeshData d)
    {
        var m = new EditMesh();
        m.Verts = [.. d.Verts.Select(p => new EmVert(p))];
        m.Faces = [.. d.Faces.Select(f => new EmFace([.. f.Select(i => m.Verts[i])]))];
        m.Loose = [.. d.Loose.Select(e => (m.Verts[e[0]], m.Verts[e[1]]))];
        foreach (var i in d.VSel) m.Vsel.Add(m.Verts[i]);
        foreach (var i in d.FSel) m.Fsel.Add(m.Faces[i]);
        foreach (var e in d.ESel) m.Esel.Add(EdgeKey.Of(m.Verts[e[0]], m.Verts[e[1]]));
        return m;
    }

    public MeshData ToData()
    {
        var index = new Dictionary<EmVert, int>();
        for (var i = 0; i < Verts.Count; i++) index[Verts[i]] = i;
        var byKey = EdgeMap();
        // 面の添字は辞書で引く(List.IndexOf を選んだ面の数だけ繰り返すと、面の数の 2 乗になる)。同じ面が 2 回入ることは無いので、最初の位置 = 唯一の位置
        var faceIndex = new Dictionary<EmFace, int>();
        for (var i = 0; i < Faces.Count; i++) faceIndex.TryAdd(Faces[i], i);
        return new MeshData([.. Verts.Select(v => v.Co)], [.. Faces.Select(f => f.V.Select(v => index[v]).ToArray())])
        {
            Loose = [.. Loose.Select(e => new[] { index[e.A], index[e.B] })],
            VSel = [.. Vsel.Select(v => index.TryGetValue(v, out var i) ? i : -1)],
            FSel = [.. Fsel.Select(f => faceIndex.TryGetValue(f, out var i) ? i : -1).Where(i => i >= 0)],
            ESel = [.. Esel.Select(k => byKey.TryGetValue(k, out var e) ? e : null).Where(e => e is not null).Select(e => new[] { index[e!.A], index[e.B] })],
        };
    }

    public EditMesh Clone() => FromData(ToData());

    /// <summary>辺の一覧を作り直す合図(面・辺を書き換えたあとに呼ぶ)。</summary>
    public void Changed() => cache = null;

    // ---------------------------------------------------------------- 辺・つながり
    /// <summary>key → 辺。</summary>
    public OrderedDictionary<EdgeKey, EmEdge> EdgeMap()
    {
        if (cache is not null) return cache.Edges;
        var edges = new OrderedDictionary<EdgeKey, EmEdge>();
        var vertEdges = new Dictionary<EmVert, List<EmEdge>>();
        foreach (var v in Verts) vertEdges[v] = [];
        void Add(EmVert a, EmVert b, EmFace? face)
        {
            var k = EdgeKey.Of(a, b);
            if (!edges.TryGetValue(k, out var e))
            {
                e = new EmEdge(a, b, k);
                edges[k] = e;
                if (vertEdges.TryGetValue(a, out var la)) la.Add(e);
                if (vertEdges.TryGetValue(b, out var lb)) lb.Add(e);
            }
            if (face is not null) e.Faces.Add(face);
        }
        foreach (var f in Faces)
            for (var i = 0; i < f.V.Count; i++) Add(f.V[i], f.V[(i + 1) % f.V.Count], f);
        foreach (var (a, b) in Loose) Add(a, b, null);
        cache = new EdgeCache { Edges = edges, VertEdges = vertEdges };
        return edges;
    }

    public List<EmEdge> Edges() => [.. EdgeMap().Values];

    public List<EmEdge> VertEdges(EmVert v)
    {
        EdgeMap();
        return cache!.VertEdges.TryGetValue(v, out var l) ? l : [];
    }

    public static EmVert Other(EmEdge e, EmVert v) => e.A == v ? e.B : e.A;

    public static Vec3 FaceNormal(EmFace f)
    {
        double nx = 0, ny = 0, nz = 0;
        var vs = f.V;
        for (var i = 0; i < vs.Count; i++)
        {
            var a = vs[i].Co;
            var b = vs[(i + 1) % vs.Count].Co;
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
        }
        var n = new Vec3(nx, ny, nz);
        return n.LengthSq > 0 ? n.Normalized() : new Vec3(0, 0, 1);
    }

    /// <summary>面の Newell ベクトル(正規化前)の長さ。面積の 2 倍。</summary>
    public static double FaceArea(EmFace f)
    {
        double nx = 0, ny = 0, nz = 0;
        var vs = f.V;
        for (var i = 0; i < vs.Count; i++)
        {
            var a = vs[i].Co;
            var b = vs[(i + 1) % vs.Count].Co;
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
        }
        return Math.Sqrt(nx * nx + ny * ny + nz * nz);
    }

    public static Vec3 FaceCenter(EmFace f)
    {
        var c = Vec3.Zero;
        foreach (var v in f.V) c += v.Co;
        return c.DivideScalar(f.V.Count);
    }

    /// <summary>面 f の中で a の次が b なら true(辺 a → b の向き)。</summary>
    public static bool HasDirected(EmFace f, EmVert a, EmVert b)
    {
        var i = f.V.IndexOf(a);
        return i >= 0 && f.V[(i + 1) % f.V.Count] == b;
    }

    /// <summary>v のまわりの面を順番に並べる。開いている(境界)なら null。</summary>
    public List<FanItem>? Fan(EmVert v)
    {
        var items = new List<FanItem>();
        foreach (var face in Faces)
        {
            if (!face.V.Contains(v)) continue;
            var i = face.V.IndexOf(v);
            var n = face.V.Count;
            items.Add(new FanItem(face, face.V[(i - 1 + n) % n], face.V[(i + 1) % n]));
        }
        if (items.Count < 2) return null;
        var output = new List<FanItem> { items[0] };
        var used = new HashSet<FanItem> { items[0] };
        while (output.Count < items.Count)
        {
            var last = output[^1];
            var nx = items.FirstOrDefault(it => !used.Contains(it) && it.Prev == last.Next);
            if (nx is null) return null;
            output.Add(nx);
            used.Add(nx);
        }
        return output[^1].Next == output[0].Prev ? output : null;
    }

    // ---------------------------------------------------------------- 選択
    /// <summary>選択モードに合わせてほかの選択を導く。</summary>
    public void Flush(string mode)
    {
        var edges = Edges();
        if (mode == "vert")
        {
            Esel = new OrderedSet<EdgeKey>(edges.Where(e => Vsel.Contains(e.A) && Vsel.Contains(e.B)).Select(e => e.Key));
            Fsel = new OrderedSet<EmFace>(Faces.Where(f => f.V.All(v => Vsel.Contains(v))));
        }
        else if (mode == "edge")
        {
            var byKey = EdgeMap();
            Esel = new OrderedSet<EdgeKey>(Esel.Where(byKey.ContainsKey));
            Vsel = new OrderedSet<EmVert>();
            foreach (var k in Esel)
            {
                var e = byKey[k];
                Vsel.Add(e.A);
                Vsel.Add(e.B);
            }
            Fsel = new OrderedSet<EmFace>(Faces.Where(f => f.V.Select((v, i) => Esel.Contains(EdgeKey.Of(v, f.V[(i + 1) % f.V.Count]))).All(b => b)));
        }
        else
        {
            var faceSet = new HashSet<EmFace>(Faces);   // List.Contains を選んだ面の数だけ繰り返すと 2 乗になる
            Fsel = new OrderedSet<EmFace>(Fsel.Where(faceSet.Contains));
            Vsel = new OrderedSet<EmVert>();
            Esel = new OrderedSet<EdgeKey>();
            foreach (var f in Fsel)
                for (var i = 0; i < f.V.Count; i++)
                {
                    Vsel.Add(f.V[i]);
                    Esel.Add(EdgeKey.Of(f.V[i], f.V[(i + 1) % f.V.Count]));
                }
        }
        var vertSet = new HashSet<EmVert>(Verts);
        Vsel = new OrderedSet<EmVert>(Vsel.Where(vertSet.Contains));
    }

    public (int Verts, int Edges, int Faces) Counts() => (Vsel.Count, Esel.Count, Fsel.Count);

    public (int Verts, int Edges, int Faces, int Tris) Stats() =>
        (Verts.Count, EdgeMap().Count, Faces.Count, Faces.Sum(f => f.V.Count - 2));

    public void SelectAll(string mode, bool on = true)
    {
        if (!on)
        {
            Vsel.Clear(); Esel.Clear(); Fsel.Clear();
            return;
        }
        Vsel = new OrderedSet<EmVert>(Verts);
        Esel = new OrderedSet<EdgeKey>(EdgeMap().Keys);
        Fsel = new OrderedSet<EmFace>(Faces);
        Flush(mode);
    }

    public void Invert(string mode)
    {
        if (mode == "vert") Vsel = new OrderedSet<EmVert>(Verts.Where(v => !Vsel.Contains(v)));
        else if (mode == "edge") Esel = new OrderedSet<EdgeKey>(EdgeMap().Keys.Where(k => !Esel.Contains(k)));
        else Fsel = new OrderedSet<EmFace>(Faces.Where(f => !Fsel.Contains(f)));
        Flush(mode);
    }

    /// <summary>要素(頂点・辺のキー・面)を選択に足す / 外す / 切り替える(on が null なら切り替え)。</summary>
    public bool SetSelected(string mode, object item, bool? on = null)
    {
        bool has;
        if (mode == "vert") has = Vsel.Contains((EmVert)item);
        else if (mode == "edge") has = Esel.Contains((EdgeKey)item);
        else has = Fsel.Contains((EmFace)item);
        var value = on ?? !has;
        if (mode == "vert") { if (value) Vsel.Add((EmVert)item); else Vsel.Remove((EmVert)item); }
        else if (mode == "edge") { if (value) Esel.Add((EdgeKey)item); else Esel.Remove((EdgeKey)item); }
        else { if (value) Fsel.Add((EmFace)item); else Fsel.Remove((EmFace)item); }
        Flush(mode);
        return value;
    }

    /// <summary>頂点の集合を選択する(モードに合わせて変換)。</summary>
    public void SelectVerts(string mode, IEnumerable<EmVert> verts, bool extend = false)
    {
        var list = verts.ToList();
        if (!extend) SelectAll(mode, false);
        foreach (var v in list) Vsel.Add(v);
        if (mode == "vert") { Flush(mode); return; }
        var set = new HashSet<EmVert>(list);
        if (mode == "edge")
        {
            foreach (var e in Edges())
                if (set.Contains(e.A) && set.Contains(e.B)) Esel.Add(e.Key);
        }
        else
        {
            foreach (var f in Faces)
                if (f.V.All(set.Contains)) Fsel.Add(f);
        }
        Flush(mode);
    }

    /// <summary>seeds からつながっている頂点すべて。</summary>
    public OrderedSet<EmVert> Linked(IEnumerable<EmVert> seeds)
    {
        var s = seeds.ToList();
        var output = new OrderedSet<EmVert>(s);
        var stack = new List<EmVert>(s);
        while (stack.Count > 0)
        {
            var v = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            foreach (var e in VertEdges(v))
            {
                var o = Other(e, v);
                if (!output.Contains(o)) { output.Add(o); stack.Add(o); }
            }
        }
        return output;
    }

    public void Grow(string mode)
    {
        if (mode == "face")
        {
            var add = Faces.Where(f => f.V.Any(v => Vsel.Contains(v))).ToList();
            foreach (var f in add) Fsel.Add(f);
        }
        else
        {
            var add = new OrderedSet<EmVert>(Vsel);
            foreach (var v in Vsel)
                foreach (var e in VertEdges(v)) add.Add(Other(e, v));
            SelectVerts(mode, add);
            return;
        }
        Flush(mode);
    }

    public void Shrink(string mode)
    {
        // 選択していない頂点と辺でつながっている頂点(選択の外周)を外す
        var keep = new OrderedSet<EmVert>(Vsel.Where(v => VertEdges(v).All(e => Vsel.Contains(Other(e, v)))));
        if (mode == "face")
        {
            Fsel = new OrderedSet<EmFace>(Fsel.Where(f => f.V.Any(v => keep.Contains(v))));
            Flush(mode);
            return;
        }
        SelectVerts(mode, keep);
    }

    // ---------------------------------------------------------------- ループ・リング
    /// <summary>辺ループ(Alt+クリック)。e は始めの辺。</summary>
    public List<EmEdge> EdgeLoop(EmEdge e)
    {
        // v から cur の先へ進む。始めの辺に戻ったら closed
        (List<EmEdge> Out, bool Closed) Walk(EmEdge cur, EmVert v)
        {
            var output = new List<EmEdge>();
            for (var guard = 0; guard < 10000; guard++)
            {
                EmEdge? next = null;
                if (cur.Faces.Count == 1)
                {
                    // 境界ならふちに沿って進む
                    var bs = VertEdges(v).Where(x => x != cur && x.Faces.Count == 1).ToList();
                    if (bs.Count == 1) next = bs[0];
                }
                else
                {
                    var es = VertEdges(v);
                    if (es.Count == 4 && es.All(x => x.Faces.Count == 2))
                    {
                        // 今の辺と面を共有しない辺が、向かい側の辺
                        next = es.FirstOrDefault(x => x != cur && !x.Faces.Any(f => cur.Faces.Contains(f)));
                    }
                }
                if (next == e) return (output, true);
                if (next is null || output.Contains(next)) break;
                output.Add(next);
                v = Other(next, v);
                cur = next;
            }
            return (output, false);
        }
        var one = Walk(e, e.B);
        if (one.Closed) return [e, .. one.Out];
        return [.. new[] { e }.Concat(one.Out).Concat(Walk(e, e.A).Out).Distinct()];
    }

    /// <summary>
    /// 辺リング。辺の向き(a → b)をそろえた [{ a, b, e }] と、間の面(四角形)を返す。closed なら一周している。
    /// 四角形以外の面や境界で止まる。
    /// </summary>
    public EdgeRingResult EdgeRing(EmEdge e)
    {
        var byKey = EdgeMap();
        (EmVert A, EmVert B)? Step(RingEdge cur, EmFace face)
        {
            // face(四角形)の中で cur の向かい側の辺を、向きをそろえて返す
            var q = face.V;
            var i = q.IndexOf(cur.A);
            if (q.Count != 4 || i < 0) return null;
            if (q[(i + 1) % 4] == cur.B) return (q[(i + 3) % 4], q[(i + 2) % 4]);
            if (q[(i + 3) % 4] == cur.B) return (q[(i + 1) % 4], q[(i + 2) % 4]);
            return null;
        }
        (List<RingEdge> Edges, List<EmFace> Faces, bool Closed) Go(RingEdge start, EmFace? firstFace)
        {
            var edges = new List<RingEdge>();
            var faces = new List<EmFace>();
            var cur = start;
            var face = firstFace;
            for (var guard = 0; guard < 10000 && face is not null; guard++)
            {
                var nx = Step(cur, face);
                if (nx is null) break;
                var ne = byKey[EdgeKey.Of(nx.Value.A, nx.Value.B)];
                faces.Add(face);
                if (ne == e) return (edges, faces, true);
                var item = new RingEdge(nx.Value.A, nx.Value.B, ne);
                edges.Add(item);
                var captured = face;
                face = ne.Faces.FirstOrDefault(f => f != captured);
                cur = item;
            }
            return (edges, faces, false);
        }
        var start0 = new RingEdge(e.A, e.B, e);
        var f1 = Go(start0, e.Faces.Count > 0 ? e.Faces[0] : null);
        if (f1.Closed) return new EdgeRingResult([start0, .. f1.Edges], f1.Faces, true);
        var f2 = e.Faces.Count > 1 ? Go(start0, e.Faces[1]) : (Edges: new List<RingEdge>(), Faces: new List<EmFace>(), Closed: false);
        // f2 は逆向きに進んだ分。向きは start に合わせてあるので、そのまま前につなぐ
        f2.Edges.Reverse();
        f2.Faces.Reverse();
        return new EdgeRingResult([.. f2.Edges, start0, .. f1.Edges], [.. f2.Faces, .. f1.Faces], false);
    }

    // ---------------------------------------------------------------- 後片付け
    /// <summary>candidates のうち、どの面・辺にも使われていない頂点を消す(省略すると全頂点が候補)。</summary>
    public void RemoveUnused(IEnumerable<EmVert>? candidates = null)
    {
        var list = (candidates ?? Verts).ToList();
        var used = new HashSet<EmVert>();
        foreach (var f in Faces) foreach (var v in f.V) used.Add(v);
        foreach (var (a, b) in Loose) { used.Add(a); used.Add(b); }
        var drop = new HashSet<EmVert>(list.Where(v => !used.Contains(v)));
        Verts = [.. Verts.Where(v => !drop.Contains(v))];
        foreach (var v in drop) Vsel.Remove(v);
        Changed();
    }

    /// <summary>面に使われている辺は loose から外す。</summary>
    public void TidyLoose()
    {
        Changed();
        var faceEdges = new HashSet<EdgeKey>();
        foreach (var f in Faces)
            for (var i = 0; i < f.V.Count; i++) faceEdges.Add(EdgeKey.Of(f.V[i], f.V[(i + 1) % f.V.Count]));
        var seen = new HashSet<EdgeKey>();
        Loose = [.. Loose.Where(e =>
        {
            var k = EdgeKey.Of(e.A, e.B);
            if (e.A == e.B || faceEdges.Contains(k) || seen.Contains(k)) return false;
            seen.Add(k);
            return true;
        })];
        Changed();
    }

    // ---------------------------------------------------------------- 操作
    /// <summary>
    /// 押し出し(E)。面が選ばれていれば領域ごと、なければ辺、頂点を押し出す。
    /// 戻り値の Verts は新しい頂点、Normal は平均の法線(ローカル。面のときだけ)。選択が無ければ null。
    /// </summary>
    public ExtrudeResult? Extrude(string mode)
    {
        var region = Fsel.ToList();
        if (region.Count > 0)
        {
            var inRegion = new HashSet<EmFace>(region);
            var byKey0 = EdgeMap();
            var boundary = new List<(EmVert A, EmVert B)>();
            foreach (var f in region)
                for (var i = 0; i < f.V.Count; i++)
                {
                    var a = f.V[i];
                    var b = f.V[(i + 1) % f.V.Count];
                    var e = byKey0[EdgeKey.Of(a, b)];
                    if (e.Faces.Count(x => inRegion.Contains(x)) == 1) boundary.Add((a, b));
                }
            var normal = Vec3.Zero;
            foreach (var f in region) normal += FaceNormal(f);
            normal = normal.Normalized();
            var map = new OrderedDictionary<EmVert, EmVert>();
            EmVert Dup(EmVert v)
            {
                if (!map.TryGetValue(v, out var n))
                {
                    n = new EmVert(v.Co);
                    map[v] = n;
                    Verts.Add(n);
                }
                return n;
            }
            var oldVerts = region.SelectMany(f => f.V).ToList();
            foreach (var f in region) f.V = [.. f.V.Select(Dup)];
            foreach (var (a, b) in boundary)
            {
                var db = Dup(b);
                var da = Dup(a);
                Faces.Add(new EmFace([a, b, db, da]));
            }
            Changed();
            RemoveUnused(oldVerts);
            Fsel = new OrderedSet<EmFace>(region);
            Flush("face");
            if (mode != "face") Flush(mode);
            return new ExtrudeResult("region", [.. map.Values], normal);
        }
        var byKey = EdgeMap();
        var edges = Esel.Select(k => byKey.TryGetValue(k, out var e) ? e : null).Where(e => e is not null).Select(e => e!).ToList();
        var map2 = new OrderedDictionary<EmVert, EmVert>();
        EmVert Dup2(EmVert v)
        {
            if (!map2.TryGetValue(v, out var n))
            {
                n = new EmVert(v.Co);
                map2[v] = n;
                Verts.Add(n);
            }
            return n;
        }
        if (edges.Count > 0)
        {
            foreach (var e in edges)
            {
                var f = e.Faces.FirstOrDefault(x => HasDirected(x, e.A, e.B));
                var (a, b) = f is not null ? (e.A, e.B) : e.Faces.Count > 0 ? (e.B, e.A) : (e.A, e.B);
                var da = Dup2(a);
                var db = Dup2(b);
                Faces.Add(new EmFace([b, a, da, db]));
            }
            TidyLoose();
            SelectVerts(mode, map2.Values);
            return new ExtrudeResult("edge", [.. map2.Values], null);
        }
        if (Vsel.Count == 0) return null;
        foreach (var v in Vsel.ToList())
        {
            var dv = Dup2(v);
            Loose.Add((v, dv));
        }
        Changed();
        SelectVerts(mode, map2.Values);
        return new ExtrudeResult("vert", [.. map2.Values], null);
    }

    /// <summary>
    /// 面の差し込み(I)。選んだ面ごとに内側に面を作る。
    /// 戻り値は内側の頂点の動かし方(位置 = base + dir × 厚さ)と、上限の厚さ。面が選ばれていなければ null。
    /// </summary>
    public InsetResult? Inset(string mode)
    {
        var faces = Fsel.ToList();
        if (faces.Count == 0) return null;
        var moves = new List<MoveVert>();
        var limit = double.PositiveInfinity;
        foreach (var f in faces)
        {
            var n = FaceNormal(f);
            var c = FaceCenter(f);
            var outer = f.V;
            var len = outer.Count;
            var inner = new List<EmVert>();
            for (var i = 0; i < len; i++)
            {
                var p = outer[i].Co;
                var prev = outer[(i - 1 + len) % len].Co;
                var next = outer[(i + 1) % len].Co;
                var d0 = (prev - p).Normalized();
                var d1 = (next - p).Normalized();
                var perp = n.Cross(d1).Normalized(); // 辺 p→next の内側
                Vec3 dir;
                var bis = d0 + d1;
                if (bis.LengthSq < 1e-8) dir = perp;
                else
                {
                    bis = bis.Normalized();
                    if (bis.Dot(perp) < 0) bis = -bis;
                    var s = Math.Max(0.1, bis.Dot(perp)); // = sin(角度/2)
                    dir = bis.DivideScalar(s);
                }
                limit = Math.Min(limit, p.DistanceTo(c) * 0.95 / Math.Max(1, dir.Length));
                var nv = new EmVert(p);
                Verts.Add(nv);
                moves.Add(new MoveVert(nv, p, dir));
                inner.Add(nv);
            }
            for (var i = 0; i < len; i++) Faces.Add(new EmFace([outer[i], outer[(i + 1) % len], inner[(i + 1) % len], inner[i]]));
            f.V = inner;
        }
        Changed();
        Fsel = new OrderedSet<EmFace>(faces);
        Flush("face");
        if (mode != "face") Flush(mode);
        return new InsetResult(moves, limit);
    }

    /// <summary>
    /// ループカット(Ctrl+R)。edge を通る辺リングに cuts 本の辺ループを入れる。
    /// 戻り値の Slide は、新しい頂点の位置 = a → b の t(+ ずらし量)。四角形が無ければ null。
    /// </summary>
    public LoopCutResult? LoopCut(EmEdge edge, int cuts, string mode)
    {
        var (ring, faces, _) = EdgeRing(edge);
        if (faces.Count == 0) return null;
        var n = cuts;
        var newVerts = ring.Select(r => Enumerable.Range(0, n).Select(m =>
        {
            var t = (double)(m + 1) / (n + 1);
            var v = new EmVert(r.A.Co.Lerp(r.B.Co, t));
            Verts.Add(v);
            return (V: v, T: t);
        }).ToList()).ToList();
        var ringFaces = new HashSet<EmFace>(faces);
        for (var j = 0; j < faces.Count; j++)
        {
            var face = faces[j];
            var r0 = ring[j];
            var r1 = ring[(j + 1) % ring.Count];
            var p = new List<EmVert> { r0.A };
            p.AddRange(newVerts[j].Select(x => x.V));
            p.Add(r0.B);
            var s = new List<EmVert> { r1.A };
            s.AddRange(newVerts[(j + 1) % ring.Count].Select(x => x.V));
            s.Add(r1.B);
            var forward = HasDirected(face, r0.A, r0.B);
            var parts = new List<EmFace>();
            for (var m = 0; m <= n; m++)
                parts.Add(new EmFace(forward ? [p[m], p[m + 1], s[m + 1], s[m]] : [p[m + 1], p[m], s[m], s[m + 1]]));
            var i = Faces.IndexOf(face);
            Js.Splice(Faces, i, 1, parts);
            Fsel.Remove(face);
        }
        // リングの端(四角形以外の面・境界)の面にも、辺の途中の頂点を差し込む
        for (var j = 0; j < ring.Count; j++)
        {
            var r = ring[j];
            var mids = newVerts[j].Select(x => x.V).ToList();
            foreach (var f in Faces.ToList())
            {
                if (ringFaces.Contains(f)) continue;
                var ia = f.V.IndexOf(r.A);
                var len = f.V.Count;
                if (ia < 0) continue;
                if (f.V[(ia + 1) % len] == r.B) Js.Splice(f.V, ia + 1, 0, mids);
                else if (f.V[(ia - 1 + len) % len] == r.B) Js.Splice(f.V, ia, 0, Enumerable.Reverse(mids));
            }
        }
        Changed();
        var all = newVerts.SelectMany(x => x).ToList();
        SelectVerts(mode, all.Select(x => x.V));
        var slide = new List<SlideVert>();
        for (var j = 0; j < newVerts.Count; j++)
            foreach (var x in newVerts[j]) slide.Add(new SlideVert(x.V, ring[j].A, ring[j].B, x.T));
        return new LoopCutResult([.. all.Select(x => x.V)], slide, n);
    }

    /// <summary>
    /// ベベル(Ctrl+B)。選んだ辺を分割数 1 で面取りする。
    /// 対応していない形のときは Error に理由が入る(メッシュは変わらない)。
    /// </summary>
    public BevelResult Bevel(string mode)
    {
        var byKey = EdgeMap();
        var bevEdges = Esel.Select(k => byKey.TryGetValue(k, out var e) ? e : null).Where(e => e is not null).Select(e => e!).ToList();
        if (bevEdges.Count == 0) return new BevelResult([], 0, "辺が選ばれていません");
        if (bevEdges.Any(e => e.Faces.Count != 2)) return new BevelResult([], 0, "面が 2 つ接している辺だけベベルできます");
        var bevKeys = new HashSet<EdgeKey>(bevEdges.Select(e => e.Key));
        // 辺の両側の面(a→b の向きの面が先)。面はこのあと書き換わるので、先に決めておく
        var sides = bevEdges.Select(e => HasDirected(e.Faces[0], e.A, e.B) ? (e.Faces[0], e.Faces[1]) : (e.Faces[1], e.Faces[0])).ToList();
        var verts = new OrderedSet<EmVert>(bevEdges.SelectMany(e => new[] { e.A, e.B }));
        var plans = new List<(EmVert V, List<FanItem> Sector)>();
        foreach (var v in verts)
        {
            var fan = Fan(v);
            if (fan is null) return new BevelResult([], 0, "このメッシュの形ではベベルできません（開いた部分があります）");
            // fan[i] の手前の辺は (v, fan[i].prev)。ベベルする辺から始まるように回す
            bool IsBev(int i) => bevKeys.Contains(EdgeKey.Of(v, fan[i].Prev));
            var start = -1;
            for (var i = 0; i < fan.Count; i++) if (IsBev(i)) { start = i; break; }
            var items = new List<FanItem>();
            items.AddRange(fan.Skip(start));
            items.AddRange(fan.Take(start));
            var ii = 0;
            while (ii < items.Count)
            {
                var j = ii + 1;
                while (j < items.Count && !bevKeys.Contains(EdgeKey.Of(v, items[j].Prev))) j++;
                plans.Add((v, items.GetRange(ii, j - ii))); // 面 items[i..j-1]、内側の辺は items[i+1..j-1].prev
                ii = j;
            }
        }
        var moves = new List<MoveVert>();
        var rep = new Dictionary<EmFace, Dictionary<EmVert, EmVert>>(); // 面 → (頂点 → 新しい頂点)
        Vec3 DirTo(EmVert v, EmVert x) => (x.Co - v.Co).Normalized();
        EmVert AddVert(EmVert v, Vec3 dir)
        {
            var nv = new EmVert(v.Co);
            Verts.Add(nv);
            moves.Add(new MoveVert(nv, v.Co, dir));
            return nv;
        }
        static double SinBetween(Vec3 a, Vec3 b) => Math.Max(0.2, a.Cross(b).Length);
        void SetRep(EmFace face, EmVert v, EmVert p)
        {
            if (!rep.TryGetValue(face, out var m)) { m = new Dictionary<EmVert, EmVert>(); rep[face] = m; }
            m[v] = p;
        }
        var edits = new List<(EmFace Face, EmVert V, EmVert[] List)>();
        foreach (var (v, sector) in plans)
        {
            var k = sector.Count - 1;
            var uStart = DirTo(v, sector[0].Prev);
            var uEnd = DirTo(v, sector[k].Next);
            if (k == 0)
            {
                var s = SinBetween(uStart, uEnd);
                var p = AddVert(v, (uStart + uEnd).DivideScalar(s));
                edits.Add((sector[0].Face, v, [p]));
                SetRep(sector[0].Face, v, p);
            }
            else if (k == 1)
            {
                var x = sector[1].Prev;
                var u = DirTo(v, x);
                var p = AddVert(v, u.DivideScalar(SinBetween(u, uStart)));
                foreach (var it in sector) { edits.Add((it.Face, v, [p])); SetRep(it.Face, v, p); }
            }
            else
            {
                var x1 = sector[1].Prev;
                var xk = sector[k].Prev;
                var u1 = DirTo(v, x1);
                var uk = DirTo(v, xk);
                var p1 = AddVert(v, u1.DivideScalar(SinBetween(u1, uStart)));
                var pk = AddVert(v, uk.DivideScalar(SinBetween(uk, uEnd)));
                edits.Add((sector[0].Face, v, [p1]));
                edits.Add((sector[k].Face, v, [pk]));
                SetRep(sector[0].Face, v, p1);
                SetRep(sector[k].Face, v, pk);
                // 面の中で v の前(prev)側に来るものから順に並べる
                if (k == 2) edits.Add((sector[1].Face, v, [p1, pk]));
                else
                {
                    edits.Add((sector[1].Face, v, [p1, v]));
                    edits.Add((sector[k - 1].Face, v, [v, pk]));
                }
            }
        }
        // 各面の中で v を置き換える(prev 側 → next 側の順。sector の面は prev が手前の辺)
        foreach (var (face, v, list) in edits)
        {
            var i = face.V.IndexOf(v);
            Js.Splice(face.V, i, 1, list);
        }
        // 辺ごとに面取りの面を作る
        for (var i = 0; i < bevEdges.Count; i++)
        {
            var e = bevEdges[i];
            var (fF, gG) = sides[i];
            EmVert R(EmFace face, EmVert x) => rep.TryGetValue(face, out var m) && m.TryGetValue(x, out var y) ? y : x;
            Faces.Add(new EmFace([R(fF, e.B), R(fF, e.A), R(gG, e.A), R(gG, e.B)]));
        }
        // 角に残った穴(3 本の辺が集まる角や、辺の多い頂点)をふさぐ
        var created = Faces.Skip(Faces.Count - bevEdges.Count).ToList();
        Changed();
        RemoveUnused(verts);
        var local = new HashSet<EmVert>(moves.Select(m => m.V));
        foreach (var v in verts) local.Add(v);
        var half = new OrderedDictionary<EmVert, EmVert>(); // 面がひとつしかない辺: 穴をまわる向き(b → a)
        foreach (var f in Faces)
            for (var i = 0; i < f.V.Count; i++)
            {
                var a = f.V[i];
                var b = f.V[(i + 1) % f.V.Count];
                if (local.Contains(a) && local.Contains(b) && !Faces.Any(g => HasDirected(g, b, a))) half[b] = a;
            }
        foreach (var startV in half.Keys.ToList())
        {
            if (!half.ContainsKey(startV)) continue;
            var loop = new List<EmVert>();
            var x = startV;
            while (half.ContainsKey(x) && !loop.Contains(x))
            {
                loop.Add(x);
                var y = half[x];
                half.Remove(x);
                x = y;
            }
            if (loop.Count >= 3 && x == startV) Faces.Add(new EmFace(loop));
        }
        Changed();
        var limit = bevEdges.Min(e => e.A.Co.DistanceTo(e.B.Co)) * 0.45;
        SelectVerts(mode, []);
        // 面取りでできた面を選択する(Blender と同じ)
        Fsel = new OrderedSet<EmFace>(created);
        Flush("face");
        if (mode != "face") Flush(mode);
        return new BevelResult(moves, limit);
    }

    /// <summary>削除(X)。what: "vert" | "edge" | "face" | "dissolve"。</summary>
    public void Remove(string what)
    {
        var byKey = EdgeMap();
        if (what == "vert")
        {
            var vs = new HashSet<EmVert>(Vsel);
            Faces = [.. Faces.Where(f => !f.V.Any(vs.Contains))];
            Loose = [.. Loose.Where(e => !vs.Contains(e.A) && !vs.Contains(e.B))];
            Verts = [.. Verts.Where(v => !vs.Contains(v))];
        }
        else if (what == "edge")
        {
            var ks = new HashSet<EdgeKey>(Esel);
            Faces = [.. Faces.Where(f => !f.V.Where((v, i) => ks.Contains(EdgeKey.Of(v, f.V[(i + 1) % f.V.Count]))).Any())];
            Loose = [.. Loose.Where(e => !ks.Contains(EdgeKey.Of(e.A, e.B)))];
            Changed();
            RemoveUnused(Vsel.ToList());
        }
        else if (what == "face")
        {
            var fs = new HashSet<EmFace>(Fsel);
            var touched = fs.SelectMany(f => f.V).ToList();
            Faces = [.. Faces.Where(f => !fs.Contains(f))];
            Changed();
            RemoveUnused(touched);
        }
        else if (what == "dissolve")
        {
            var keys = Esel.ToList();
            var ends = new OrderedSet<EmVert>();
            foreach (var k in keys)
            {
                if (!byKey.TryGetValue(k, out var e0)) continue;
                var a = e0.A;
                var b = e0.B;
                var fF = Faces.FirstOrDefault(f => HasDirected(f, a, b));
                var gG = Faces.FirstOrDefault(f => HasDirected(f, b, a));
                if (fF is null || gG is null || fF == gG) continue;
                static List<EmVert> Rot(EmFace f, EmVert from)
                {
                    var i = f.V.IndexOf(from);
                    return [.. f.V.Skip(i), .. f.V.Take(i)];
                }
                var fPath = Rot(fF, b); // b ... a
                var gPath = Rot(gG, a); // a ... b
                var merged = new List<EmVert>(fPath);
                merged.AddRange(gPath.Skip(1).Take(Math.Max(gPath.Count - 2, 0)));
                if (new HashSet<EmVert>(merged).Count != merged.Count) continue;
                fF.V = merged;
                Faces = [.. Faces.Where(f => f != gG)];
                Fsel.Remove(gG);
                ends.Add(a);
                ends.Add(b);
                Changed();
            }
            // 辺が 2 本だけ残った頂点は、辺の途中の点なので消す(Blender の「頂点を溶解」と同じ)
            foreach (var v in ends)
            {
                if (VertEdges(v).Count != 2) continue;
                foreach (var f in Faces)
                    if (f.V.Count > 3 && f.V.Contains(v)) f.V = [.. f.V.Where(x => x != v)];
                Changed();
            }
            RemoveUnused(ends.ToList());
        }
        Vsel.Clear(); Esel.Clear(); Fsel.Clear();
        Changed();
    }

    /// <summary>マージ(M)。how: "center" | "cursor" | "distance"。消えた頂点の数を返す。</summary>
    public int Merge(string how, Vec3 cursor, double dist = 0.0001)
    {
        var sel = Vsel.ToList();
        var map = new OrderedDictionary<EmVert, EmVert>();
        if (how == "distance")
        {
            for (var i = 0; i < sel.Count; i++)
            {
                if (map.ContainsKey(sel[i])) continue;
                for (var j = i + 1; j < sel.Count; j++)
                    if (!map.ContainsKey(sel[j]) && sel[i].Co.DistanceTo(sel[j].Co) <= dist) map[sel[j]] = sel[i];
            }
        }
        else
        {
            if (sel.Count < 2) return 0;
            var keep = sel[0];
            if (how == "center")
            {
                var c = Vec3.Zero;
                foreach (var v in sel) c += v.Co;
                keep.Co = c.DivideScalar(sel.Count);
            }
            else keep.Co = cursor;
            foreach (var v in sel.Skip(1)) map[v] = keep;
        }
        if (map.Count == 0) return 0;
        EmVert M(EmVert v) => map.TryGetValue(v, out var t) ? t : v;
        var seen = new HashSet<string>();
        var kept = new List<EmFace>();
        foreach (var f in Faces)
        {
            var mapped = f.V.Select(M).ToList();
            var vs = mapped.Where((v, i) => v != mapped[(i + 1) % mapped.Count]).ToList();
            f.V = vs;
            var key = string.Join(",", vs.Select(v => v.Id).OrderBy(x => x));
            if (vs.Count < 3 || seen.Contains(key)) continue;
            seen.Add(key);
            kept.Add(f);
        }
        Faces = kept;
        Loose = [.. Loose.Select(e => (M(e.A), M(e.B)))];
        TidyLoose();
        var removed = map.Keys.ToList();
        Verts = [.. Verts.Where(v => !map.ContainsKey(v))];
        foreach (var v in removed) Vsel.Remove(v);
        Changed();
        return removed.Count;
    }

    /// <summary>面を張る(F)。作ったものを返す: "face" | "edge" | null。</summary>
    public string? Fill(string mode)
    {
        var sel = Vsel.ToList();
        if (sel.Count < 2) return null;
        var byKey = EdgeMap();
        if (sel.Count == 2)
        {
            if (byKey.ContainsKey(EdgeKey.Of(sel[0], sel[1]))) return null;
            Loose.Add((sel[0], sel[1]));
            Changed();
            return "edge";
        }
        // 選択した境界の辺が輪になっていれば、その順に面を張る
        var set = new HashSet<EmVert>(sel);
        var cand = Edges().Where(e => set.Contains(e.A) && set.Contains(e.B) && e.Faces.Count < 2).ToList();
        List<EmVert>? order = null;
        if (cand.Count == sel.Count)
        {
            var next = new Dictionary<EmVert, List<EmEdge>>();
            foreach (var v in sel) next[v] = cand.Where(e => e.A == v || e.B == v).ToList();
            if (next.Values.All(l => l.Count == 2))
            {
                order = [sel[0]];
                EmVert? prev = null;
                var cur = sel[0];
                for (var i = 1; i < sel.Count; i++)
                {
                    var e = next[cur].First(x => Other(x, cur) != prev);
                    prev = cur;
                    cur = Other(e, cur);
                    order.Add(cur);
                }
                if (new HashSet<EmVert>(order).Count != sel.Count) order = null;
            }
        }
        if (order is null)
        {
            // 輪になっていなければ、中心のまわりの角度で並べる
            var c = Vec3.Zero;
            foreach (var v in sel) c += v.Co;
            c = c.DivideScalar(sel.Count);
            var n = Vec3.Zero;
            for (var i = 0; i < sel.Count; i++) n += (sel[i].Co - c).Cross(sel[(i + 1) % sel.Count].Co - c);
            if (n.LengthSq < 1e-10) n = new Vec3(0, 0, 1);
            n = n.Normalized();
            var u = (sel[0].Co - c).Normalized();
            var w = n.Cross(u);
            order = [.. sel.OrderBy(a =>
            {
                var pa = a.Co - c;
                return Math.Atan2(pa.Dot(w), pa.Dot(u));
            })];
        }
        // となりの面と向きをそろえる(となりの面が a→b なら、新しい面は b→a)
        for (var i = 0; i < order.Count; i++)
        {
            var a = order[i];
            var b = order[(i + 1) % order.Count];
            byKey.TryGetValue(EdgeKey.Of(a, b), out var e);
            if (e is not null && e.Faces.Count > 0 && e.Faces.Any(f => HasDirected(f, a, b))) { order.Reverse(); break; }
            if (e is not null && e.Faces.Count > 0) break;
        }
        var face = new EmFace(order);
        Faces.Add(face);
        TidyLoose();
        Fsel.Add(face);
        Flush(mode);
        return "face";
    }

    /// <summary>選んだ面を取り出して、新しいメッシュにする(P)。面が選ばれていなければ null。</summary>
    public EditMesh? Separate()
    {
        var fs = Fsel.ToList();
        if (fs.Count == 0) return null;
        var vs = fs.SelectMany(f => f.V).Distinct().ToList();
        var index = new Dictionary<EmVert, int>();
        for (var i = 0; i < vs.Count; i++) index[vs[i]] = i;
        var data = new MeshData([.. vs.Select(v => v.Co)], [.. fs.Select(f => f.V.Select(v => index[v]).ToArray())]);
        Faces = [.. Faces.Where(f => !Fsel.Contains(f))];
        Changed();
        RemoveUnused(vs);
        Vsel.Clear(); Esel.Clear(); Fsel.Clear();
        return FromData(data);
    }

    // ---------------------------------------------------------------- 描画用
    /// <summary>三角形に分けた位置(面ごとに最初の頂点を軸にした扇形)と、三角形 → 面の対応。位置は倍精度のまま(three.js の配列は Float32 に丸める)。</summary>
    public (List<Vec3> Pos, List<EmFace> TriFace) Triangles()
    {
        var pos = new List<Vec3>();
        var triFace = new List<EmFace>();
        foreach (var f in Faces)
            for (var i = 1; i < f.V.Count - 1; i++)
            {
                pos.Add(f.V[0].Co);
                pos.Add(f.V[i].Co);
                pos.Add(f.V[i + 1].Co);
                triFace.Add(f);
            }
        return (pos, triFace);
    }
}
