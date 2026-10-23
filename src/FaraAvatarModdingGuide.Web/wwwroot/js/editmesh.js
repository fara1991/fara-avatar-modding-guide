// 編集モード用のメッシュ（頂点・辺・面）。viewport.js から使う。描画やマウスには関わらず、形と選択と操作だけを扱う。
//
// - 頂点は { id, co: Vector3 }（オブジェクトのローカル座標）、面は { v: [頂点...] }（表から見て反時計回り）
// - 辺は面と「辺だけの辺」（loose）から毎回作る。辺の選択は "id-id" のキーで持つ
// - 選択は選択モード（vert / edge / face）ごとに主になる集合を持ち、flush() でほかを導く（Blender と同じ考え方）
import * as THREE from "three";

let nextId = 1;
const vert = co => ({ id: nextId++, co });
export const edgeKey = (a, b) => (a.id < b.id ? `${a.id}-${b.id}` : `${b.id}-${a.id}`);

export class EditMesh {
  constructor() {
    this.verts = [];
    this.faces = [];
    this.loose = []; // 面に属さない辺 [[a, b], ...]
    this.vsel = new Set();
    this.esel = new Set();
    this.fsel = new Set();
    this.cache = null;
  }

  // ---------------------------------------------------------------- 作成・保存
  static fromData(d) {
    const m = new EditMesh();
    m.verts = d.v.map(p => vert(new THREE.Vector3(p[0], p[1], p[2])));
    m.faces = d.f.map(f => ({ v: f.map(i => m.verts[i]) }));
    m.loose = (d.e ?? []).map(([a, b]) => [m.verts[a], m.verts[b]]);
    for (const i of d.vs ?? []) m.vsel.add(m.verts[i]);
    for (const i of d.fs ?? []) m.fsel.add(m.faces[i]);
    for (const [a, b] of d.es ?? []) m.esel.add(edgeKey(m.verts[a], m.verts[b]));
    return m;
  }

  toData() {
    const index = new Map(this.verts.map((v, i) => [v, i]));
    const byKey = this.edgeMap();
    return {
      v: this.verts.map(v => [v.co.x, v.co.y, v.co.z]),
      f: this.faces.map(f => f.v.map(v => index.get(v))),
      e: this.loose.map(([a, b]) => [index.get(a), index.get(b)]),
      vs: [...this.vsel].map(v => index.get(v)),
      fs: [...this.fsel].map(f => this.faces.indexOf(f)).filter(i => i >= 0),
      es: [...this.esel].map(k => byKey.get(k)).filter(Boolean).map(e => [index.get(e.a), index.get(e.b)]),
    };
  }

  clone() { return EditMesh.fromData(this.toData()); }

  changed() { this.cache = null; }

  // ---------------------------------------------------------------- 辺・つながり
  /** key → { a, b, key, faces: [面] } */
  edgeMap() {
    if (this.cache) return this.cache.edges;
    const edges = new Map();
    const vertEdges = new Map(this.verts.map(v => [v, []]));
    const add = (a, b, face) => {
      const k = edgeKey(a, b);
      let e = edges.get(k);
      if (!e) { e = { a, b, key: k, faces: [] }; edges.set(k, e); vertEdges.get(a)?.push(e); vertEdges.get(b)?.push(e); }
      if (face) e.faces.push(face);
    };
    for (const f of this.faces) f.v.forEach((v, i) => add(v, f.v[(i + 1) % f.v.length], f));
    for (const [a, b] of this.loose) add(a, b, null);
    this.cache = { edges, vertEdges };
    return edges;
  }

  edges() { return [...this.edgeMap().values()]; }

  vertEdges(v) { this.edgeMap(); return this.cache.vertEdges.get(v) ?? []; }

  other(e, v) { return e.a === v ? e.b : e.a; }

  faceNormal(f) {
    const n = new THREE.Vector3();
    const vs = f.v;
    for (let i = 0; i < vs.length; i++) {
      const a = vs[i].co, b = vs[(i + 1) % vs.length].co;
      n.x += (a.y - b.y) * (a.z + b.z);
      n.y += (a.z - b.z) * (a.x + b.x);
      n.z += (a.x - b.x) * (a.y + b.y);
    }
    return n.lengthSq() > 0 ? n.normalize() : new THREE.Vector3(0, 0, 1);
  }

  faceCenter(f) {
    const c = new THREE.Vector3();
    for (const v of f.v) c.add(v.co);
    return c.divideScalar(f.v.length);
  }

  /** 面 f の中で a の次が b なら true（辺 a→b の向き） */
  static hasDirected(f, a, b) {
    const i = f.v.indexOf(a);
    return i >= 0 && f.v[(i + 1) % f.v.length] === b;
  }

  /** v のまわりの面を順番に並べる。[{ face, prev, next }]。開いている（境界）なら null */
  fan(v) {
    const items = this.faces.filter(f => f.v.includes(v)).map(face => {
      const i = face.v.indexOf(v), n = face.v.length;
      return { face, prev: face.v[(i - 1 + n) % n], next: face.v[(i + 1) % n] };
    });
    if (items.length < 2) return null;
    const out = [items[0]];
    const used = new Set([items[0]]);
    while (out.length < items.length) {
      const last = out[out.length - 1];
      const nx = items.find(it => !used.has(it) && it.prev === last.next);
      if (!nx) return null;
      out.push(nx);
      used.add(nx);
    }
    return out[out.length - 1].next === out[0].prev ? out : null;
  }

  // ---------------------------------------------------------------- 選択
  /** 選択モードに合わせてほかの選択を導く */
  flush(mode) {
    const edges = this.edges();
    if (mode === "vert") {
      this.esel = new Set(edges.filter(e => this.vsel.has(e.a) && this.vsel.has(e.b)).map(e => e.key));
      this.fsel = new Set(this.faces.filter(f => f.v.every(v => this.vsel.has(v))));
    } else if (mode === "edge") {
      const byKey = this.edgeMap();
      this.esel = new Set([...this.esel].filter(k => byKey.has(k)));
      this.vsel = new Set();
      for (const k of this.esel) { const e = byKey.get(k); this.vsel.add(e.a); this.vsel.add(e.b); }
      this.fsel = new Set(this.faces.filter(f => f.v.every((v, i) => this.esel.has(edgeKey(v, f.v[(i + 1) % f.v.length])))));
    } else {
      this.fsel = new Set([...this.fsel].filter(f => this.faces.includes(f)));
      this.vsel = new Set();
      this.esel = new Set();
      for (const f of this.fsel) f.v.forEach((v, i) => { this.vsel.add(v); this.esel.add(edgeKey(v, f.v[(i + 1) % f.v.length])); });
    }
    this.vsel = new Set([...this.vsel].filter(v => this.verts.includes(v)));
  }

  counts() {
    return { verts: this.vsel.size, edges: this.esel.size, faces: this.fsel.size };
  }

  stats() {
    return {
      verts: this.verts.length, edges: this.edgeMap().size, faces: this.faces.length,
      tris: this.faces.reduce((s, f) => s + f.v.length - 2, 0),
    };
  }

  selectAll(mode, on = true) {
    if (!on) { this.vsel.clear(); this.esel.clear(); this.fsel.clear(); return; }
    this.vsel = new Set(this.verts);
    this.esel = new Set(this.edgeMap().keys());
    this.fsel = new Set(this.faces);
    this.flush(mode);
  }

  invert(mode) {
    if (mode === "vert") this.vsel = new Set(this.verts.filter(v => !this.vsel.has(v)));
    else if (mode === "edge") this.esel = new Set([...this.edgeMap().keys()].filter(k => !this.esel.has(k)));
    else this.fsel = new Set(this.faces.filter(f => !this.fsel.has(f)));
    this.flush(mode);
  }

  /** 要素（頂点・辺のキー・面）を選択に足す / 外す / 切り替える */
  setSelected(mode, item, on) {
    const set = mode === "vert" ? this.vsel : mode === "edge" ? this.esel : this.fsel;
    if (on === undefined) on = !set.has(item);
    if (on) set.add(item); else set.delete(item);
    this.flush(mode);
    return on;
  }

  /** 頂点の集合を選択する（モードに合わせて変換） */
  selectVerts(mode, verts, extend = false) {
    if (!extend) this.selectAll(mode, false);
    for (const v of verts) this.vsel.add(v);
    if (mode === "vert") return this.flush(mode);
    const set = new Set(verts);
    if (mode === "edge") for (const e of this.edges()) { if (set.has(e.a) && set.has(e.b)) this.esel.add(e.key); }
    else for (const f of this.faces) { if (f.v.every(v => set.has(v))) this.fsel.add(f); }
    this.flush(mode);
  }

  /** seeds からつながっている頂点すべて */
  linked(seeds) {
    const out = new Set(seeds);
    const stack = [...seeds];
    while (stack.length) {
      const v = stack.pop();
      for (const e of this.vertEdges(v)) {
        const o = this.other(e, v);
        if (!out.has(o)) { out.add(o); stack.push(o); }
      }
    }
    return out;
  }

  grow(mode) {
    if (mode === "face") {
      const add = this.faces.filter(f => f.v.some(v => this.vsel.has(v)));
      for (const f of add) this.fsel.add(f);
    } else {
      const add = new Set(this.vsel);
      for (const v of this.vsel) for (const e of this.vertEdges(v)) add.add(this.other(e, v));
      this.selectVerts(mode, add);
      return;
    }
    this.flush(mode);
  }

  shrink(mode) {
    // 選択していない頂点と辺でつながっている頂点（選択の外周）を外す
    const keep = new Set([...this.vsel].filter(v => this.vertEdges(v).every(e => this.vsel.has(this.other(e, v)))));
    if (mode === "face") {
      this.fsel = new Set([...this.fsel].filter(f => f.v.some(v => keep.has(v))));
      return this.flush(mode);
    }
    this.selectVerts(mode, keep);
  }

  // ---------------------------------------------------------------- ループ・リング
  /** 辺ループ（Alt+クリック）。e は始めの辺 */
  edgeLoop(e) {
    // v から cur の先へ進む。始めの辺に戻ったら closed
    const walk = (cur, v) => {
      const out = [];
      for (let guard = 0; guard < 10000; guard++) {
        let next = null;
        if (cur.faces.length === 1) {
          // 境界ならふちに沿って進む
          const bs = this.vertEdges(v).filter(x => x !== cur && x.faces.length === 1);
          if (bs.length === 1) next = bs[0];
        } else {
          const es = this.vertEdges(v);
          if (es.length === 4 && es.every(x => x.faces.length === 2)) {
            // 今の辺と面を共有しない辺が、向かい側の辺
            next = es.find(x => x !== cur && !x.faces.some(f => cur.faces.includes(f))) ?? null;
          }
        }
        if (next === e) return { out, closed: true };
        if (!next || out.includes(next)) break;
        out.push(next);
        v = this.other(next, v);
        cur = next;
      }
      return { out, closed: false };
    };
    const one = walk(e, e.b);
    if (one.closed) return [e, ...one.out];
    return [...new Set([e, ...one.out, ...walk(e, e.a).out])];
  }

  /**
   * 辺リング。辺の向き（a → b）をそろえた [{ a, b, e }] と、間の面（四角形）を返す。closed なら一周している
   * 四角形以外の面や境界で止まる
   */
  edgeRing(e) {
    const byKey = this.edgeMap();
    const step = (cur, face) => {
      // face（四角形）の中で cur の向かい側の辺を、向きをそろえて返す
      const q = face.v, i = q.indexOf(cur.a);
      if (q.length !== 4 || i < 0) return null;
      if (q[(i + 1) % 4] === cur.b) return { a: q[(i + 3) % 4], b: q[(i + 2) % 4] };
      if (q[(i + 3) % 4] === cur.b) return { a: q[(i + 1) % 4], b: q[(i + 2) % 4] };
      return null;
    };
    const go = (start, firstFace) => {
      const edges = [], faces = [];
      let cur = start, face = firstFace;
      for (let guard = 0; guard < 10000 && face; guard++) {
        const nx = step(cur, face);
        if (!nx) break;
        const ne = byKey.get(edgeKey(nx.a, nx.b));
        faces.push(face);
        if (ne === e) return { edges, faces, closed: true };
        edges.push({ ...nx, e: ne });
        face = ne.faces.find(f => f !== face) ?? null;
        cur = nx;
      }
      return { edges, faces, closed: false };
    };
    const start = { a: e.a, b: e.b, e };
    const f1 = go(start, e.faces[0]);
    if (f1.closed) return { ring: [start, ...f1.edges], faces: f1.faces, closed: true };
    const f2 = e.faces[1] ? go(start, e.faces[1]) : { edges: [], faces: [] };
    // f2 は逆向きに進んだ分。向きは start に合わせてあるので、そのまま前につなぐ
    return { ring: [...f2.edges.reverse(), start, ...f1.edges], faces: [...f2.faces.reverse(), ...f1.faces], closed: false };
  }

  // ---------------------------------------------------------------- 後片付け
  /** candidates のうち、どの面・辺にも使われていない頂点を消す */
  removeUnused(candidates = this.verts) {
    const used = new Set();
    for (const f of this.faces) for (const v of f.v) used.add(v);
    for (const [a, b] of this.loose) { used.add(a); used.add(b); }
    const drop = new Set([...candidates].filter(v => !used.has(v)));
    this.verts = this.verts.filter(v => !drop.has(v));
    for (const v of drop) this.vsel.delete(v);
    this.changed();
  }

  /** 面に使われている辺は loose から外す */
  tidyLoose() {
    this.changed();
    const faceEdges = new Set();
    for (const f of this.faces) f.v.forEach((v, i) => faceEdges.add(edgeKey(v, f.v[(i + 1) % f.v.length])));
    const seen = new Set();
    this.loose = this.loose.filter(([a, b]) => {
      const k = edgeKey(a, b);
      if (a === b || faceEdges.has(k) || seen.has(k)) return false;
      seen.add(k);
      return true;
    });
    this.changed();
  }

  // ---------------------------------------------------------------- 操作
  /**
   * 押し出し（E）。面が選ばれていれば領域ごと、なければ辺、頂点を押し出す。
   * 戻り値 { type: "region" | "edge" | "vert", verts: 新しい頂点, normal?: 平均の法線（ローカル） }
   */
  extrude(mode) {
    const region = [...this.fsel];
    if (region.length) {
      const inRegion = new Set(region);
      const byKey = this.edgeMap();
      const boundary = [];
      for (const f of region) {
        f.v.forEach((a, i) => {
          const b = f.v[(i + 1) % f.v.length];
          const e = byKey.get(edgeKey(a, b));
          if (e.faces.filter(x => inRegion.has(x)).length === 1) boundary.push([a, b]);
        });
      }
      const normal = new THREE.Vector3();
      for (const f of region) normal.add(this.faceNormal(f));
      normal.normalize();
      const map = new Map();
      const dup = v => { if (!map.has(v)) { const n = vert(v.co.clone()); map.set(v, n); this.verts.push(n); } return map.get(v); };
      const oldVerts = new Set(region.flatMap(f => f.v));
      for (const f of region) f.v = f.v.map(dup);
      for (const [a, b] of boundary) this.faces.push({ v: [a, b, dup(b), dup(a)] });
      this.changed();
      this.removeUnused(oldVerts);
      this.fsel = new Set(region);
      this.flush("face");
      if (mode !== "face") this.flush(mode);
      return { type: "region", verts: [...map.values()], normal };
    }
    const byKey = this.edgeMap();
    const edges = [...this.esel].map(k => byKey.get(k)).filter(Boolean);
    const map = new Map();
    const dup = v => { if (!map.has(v)) { const n = vert(v.co.clone()); map.set(v, n); this.verts.push(n); } return map.get(v); };
    if (edges.length) {
      for (const e of edges) {
        const f = e.faces.find(x => EditMesh.hasDirected(x, e.a, e.b));
        const [a, b] = f ? [e.a, e.b] : e.faces.length ? [e.b, e.a] : [e.a, e.b];
        this.faces.push({ v: [b, a, dup(a), dup(b)] });
      }
      this.tidyLoose();
      this.selectVerts(mode, [...map.values()]);
      return { type: "edge", verts: [...map.values()] };
    }
    if (!this.vsel.size) return null;
    for (const v of [...this.vsel]) this.loose.push([v, dup(v)]);
    this.changed();
    this.selectVerts(mode, [...map.values()]);
    return { type: "vert", verts: [...map.values()] };
  }

  /**
   * 面の差し込み（I）。選んだ面ごとに内側に面を作る。
   * 戻り値は内側の頂点の動かし方 [{ v, base, dir }]（位置 = base + dir × 厚さ）と、上限の厚さ
   */
  inset(mode) {
    const faces = [...this.fsel];
    if (!faces.length) return null;
    const moves = [];
    let limit = Infinity;
    for (const f of faces) {
      const n = this.faceNormal(f), c = this.faceCenter(f);
      const outer = f.v, len = outer.length;
      const inner = outer.map((v, i) => {
        const p = v.co, prev = outer[(i - 1 + len) % len].co, next = outer[(i + 1) % len].co;
        const d0 = prev.clone().sub(p).normalize(), d1 = next.clone().sub(p).normalize();
        const perp = n.clone().cross(d1).normalize(); // 辺 p→next の内側
        let dir;
        const bis = d0.clone().add(d1);
        if (bis.lengthSq() < 1e-8) dir = perp;
        else {
          bis.normalize();
          if (bis.dot(perp) < 0) bis.negate();
          const s = Math.max(0.1, bis.dot(perp)); // = sin(角度/2)
          dir = bis.divideScalar(s);
        }
        limit = Math.min(limit, p.distanceTo(c) * 0.95 / Math.max(1, dir.length()));
        const nv = vert(p.clone());
        this.verts.push(nv);
        moves.push({ v: nv, base: p.clone(), dir });
        return nv;
      });
      for (let i = 0; i < len; i++) this.faces.push({ v: [outer[i], outer[(i + 1) % len], inner[(i + 1) % len], inner[i]] });
      f.v = inner;
    }
    this.changed();
    this.fsel = new Set(faces);
    this.flush("face");
    if (mode !== "face") this.flush(mode);
    return { moves, limit };
  }

  /**
   * ループカット（Ctrl+R）。edge を通る辺リングに cuts 本の辺ループを入れる。
   * 戻り値 { verts: 新しい頂点, slide: [{ v, a, b, t }]（位置 = a→b の t + ずらし量） }
   */
  loopCut(edge, cuts, mode) {
    const { ring, faces } = this.edgeRing(edge);
    if (!faces.length) return null;
    const n = cuts;
    const newVerts = ring.map(r => Array.from({ length: n }, (_, m) => {
      const t = (m + 1) / (n + 1);
      const v = vert(r.a.co.clone().lerp(r.b.co, t));
      this.verts.push(v);
      return { v, t };
    }));
    const ringFaces = new Set(faces);
    faces.forEach((face, j) => {
      const r0 = ring[j], r1 = ring[(j + 1) % ring.length];
      const P = [r0.a, ...newVerts[j].map(x => x.v), r0.b];
      const S = [r1.a, ...newVerts[(j + 1) % ring.length].map(x => x.v), r1.b];
      const forward = EditMesh.hasDirected(face, r0.a, r0.b);
      const parts = [];
      for (let m = 0; m <= n; m++) parts.push(forward ? [P[m], P[m + 1], S[m + 1], S[m]] : [P[m + 1], P[m], S[m], S[m + 1]]);
      const i = this.faces.indexOf(face);
      this.faces.splice(i, 1, ...parts.map(v => ({ v })));
      this.fsel.delete(face);
    });
    // リングの端（四角形以外の面・境界）の面にも、辺の途中の頂点を差し込む
    ring.forEach((r, j) => {
      const mids = newVerts[j].map(x => x.v);
      for (const f of this.faces) {
        if (ringFaces.has(f)) continue;
        const ia = f.v.indexOf(r.a), len = f.v.length;
        if (ia < 0) continue;
        if (f.v[(ia + 1) % len] === r.b) f.v.splice(ia + 1, 0, ...mids);
        else if (f.v[(ia - 1 + len) % len] === r.b) f.v.splice(ia, 0, ...[...mids].reverse());
      }
    });
    this.changed();
    const all = newVerts.flat();
    this.selectVerts(mode, all.map(x => x.v));
    return { verts: all.map(x => x.v), slide: newVerts.flatMap((list, j) => list.map(x => ({ v: x.v, a: ring[j].a, b: ring[j].b, t: x.t }))), cuts: n };
  }

  /**
   * ベベル（Ctrl+B）。選んだ辺を分割数 1 で面取りする。
   * 戻り値 { moves: [{ v, base, dir }], limit } か、対応していない形なら文字列（理由）
   */
  bevel(mode) {
    const byKey = this.edgeMap();
    const bevEdges = [...this.esel].map(k => byKey.get(k)).filter(Boolean);
    if (!bevEdges.length) return "辺が選ばれていません";
    if (bevEdges.some(e => e.faces.length !== 2)) return "面が 2 つ接している辺だけベベルできます";
    const bevKeys = new Set(bevEdges.map(e => e.key));
    // 辺の両側の面（a→b の向きの面が先）。面はこのあと書き換わるので、先に決めておく
    const sides = bevEdges.map(e => (EditMesh.hasDirected(e.faces[0], e.a, e.b) ? [e.faces[0], e.faces[1]] : [e.faces[1], e.faces[0]]));
    const verts = new Set(bevEdges.flatMap(e => [e.a, e.b]));
    const plans = [];
    for (const v of verts) {
      const fan = this.fan(v);
      if (!fan) return "このメッシュの形ではベベルできません（開いた部分があります）";
      // fan[i] の手前の辺は (v, fan[i].prev)。ベベルする辺から始まるように回す
      const isBev = i => bevKeys.has(edgeKey(v, fan[i].prev));
      const start = fan.findIndex((_, i) => isBev(i));
      const items = [...fan.slice(start), ...fan.slice(0, start)];
      let i = 0;
      while (i < items.length) {
        let j = i + 1;
        while (j < items.length && !bevKeys.has(edgeKey(v, items[j].prev))) j++;
        plans.push({ v, sector: items.slice(i, j) }); // 面 items[i..j-1]、内側の辺は items[i+1..j-1].prev
        i = j;
      }
    }
    const moves = [];
    const rep = new Map(); // "頂点id:面" → 新しい頂点
    const dirTo = (v, x) => x.co.clone().sub(v.co).normalize();
    const add = (v, dir) => { const nv = vert(v.co.clone()); this.verts.push(nv); moves.push({ v: nv, base: v.co.clone(), dir }); return nv; };
    const sinBetween = (a, b) => Math.max(0.2, a.clone().cross(b).length());
    const edits = [];
    for (const { v, sector } of plans) {
      const k = sector.length - 1;
      const uStart = dirTo(v, sector[0].prev), uEnd = dirTo(v, sector[k].next);
      if (k === 0) {
        const s = sinBetween(uStart, uEnd);
        const p = add(v, uStart.clone().add(uEnd).divideScalar(s));
        edits.push([sector[0].face, v, [p]]);
        rep.set(sector[0].face, (rep.get(sector[0].face) ?? new Map()).set(v, p));
      } else if (k === 1) {
        const x = sector[1].prev, u = dirTo(v, x);
        const p = add(v, u.divideScalar(sinBetween(u, uStart)));
        for (const it of sector) { edits.push([it.face, v, [p]]); rep.set(it.face, (rep.get(it.face) ?? new Map()).set(v, p)); }
      } else {
        const x1 = sector[1].prev, xk = sector[k].prev;
        const u1 = dirTo(v, x1), uk = dirTo(v, xk);
        const p1 = add(v, u1.divideScalar(sinBetween(u1, uStart)));
        const pk = add(v, uk.divideScalar(sinBetween(uk, uEnd)));
        edits.push([sector[0].face, v, [p1]]);
        edits.push([sector[k].face, v, [pk]]);
        rep.set(sector[0].face, (rep.get(sector[0].face) ?? new Map()).set(v, p1));
        rep.set(sector[k].face, (rep.get(sector[k].face) ?? new Map()).set(v, pk));
        // 面の中で v の前（prev）側に来るものから順に並べる
        if (k === 2) edits.push([sector[1].face, v, [p1, pk]]);
        else { edits.push([sector[1].face, v, [p1, v]]); edits.push([sector[k - 1].face, v, [v, pk]]); }
      }
    }
    // 各面の中で v を置き換える（prev 側 → next 側の順。sector の面は prev が手前の辺）
    for (const [face, v, list] of edits) {
      const i = face.v.indexOf(v);
      face.v.splice(i, 1, ...list);
    }
    // 辺ごとに面取りの面を作る
    for (const [i, e] of bevEdges.entries()) {
      const [F, G] = sides[i];
      const r = (face, x) => rep.get(face)?.get(x) ?? x;
      this.faces.push({ v: [r(F, e.b), r(F, e.a), r(G, e.a), r(G, e.b)] });
    }
    // 角に残った穴（3 本の辺が集まる角や、辺の多い頂点）をふさぐ
    const created = this.faces.slice(this.faces.length - bevEdges.length);
    this.changed();
    this.removeUnused(verts);
    const local = new Set([...moves.map(m => m.v), ...verts]);
    const half = new Map(); // 面がひとつしかない辺: 穴をまわる向き（b → a）
    for (const f of this.faces) f.v.forEach((a, i) => {
      const b = f.v[(i + 1) % f.v.length];
      if (local.has(a) && local.has(b) && !this.faces.some(g => EditMesh.hasDirected(g, b, a))) half.set(b, a);
    });
    for (const startV of [...half.keys()]) {
      if (!half.has(startV)) continue;
      const loop = [];
      let x = startV;
      while (half.has(x) && !loop.includes(x)) { loop.push(x); const y = half.get(x); half.delete(x); x = y; }
      if (loop.length >= 3 && x === startV) this.faces.push({ v: loop });
    }
    this.changed();
    const limit = Math.min(...bevEdges.map(e => e.a.co.distanceTo(e.b.co))) * 0.45;
    this.selectVerts(mode === "vert" ? "vert" : mode, []);
    // 面取りでできた面を選択する（Blender と同じ）
    this.fsel = new Set(created);
    this.flush("face");
    if (mode !== "face") this.flush(mode);
    return { moves, limit };
  }

  /** 削除（X）。what: "vert" | "edge" | "face" | "dissolve" */
  remove(what) {
    const byKey = this.edgeMap();
    if (what === "vert") {
      const vs = new Set(this.vsel);
      this.faces = this.faces.filter(f => !f.v.some(v => vs.has(v)));
      this.loose = this.loose.filter(([a, b]) => !vs.has(a) && !vs.has(b));
      this.verts = this.verts.filter(v => !vs.has(v));
    } else if (what === "edge") {
      const ks = new Set(this.esel);
      this.faces = this.faces.filter(f => !f.v.some((v, i) => ks.has(edgeKey(v, f.v[(i + 1) % f.v.length]))));
      this.loose = this.loose.filter(([a, b]) => !ks.has(edgeKey(a, b)));
      this.changed();
      this.removeUnused(this.vsel);
    } else if (what === "face") {
      const fs = new Set(this.fsel);
      const touched = new Set([...fs].flatMap(f => f.v));
      this.faces = this.faces.filter(f => !fs.has(f));
      this.changed();
      this.removeUnused(touched);
    } else if (what === "dissolve") {
      const keys = [...this.esel];
      const ends = new Set();
      for (const k of keys) {
        const e0 = byKey.get(k);
        if (!e0) continue;
        const { a, b } = e0;
        const F = this.faces.find(f => EditMesh.hasDirected(f, a, b));
        const G = this.faces.find(f => EditMesh.hasDirected(f, b, a));
        if (!F || !G || F === G) continue;
        const rot = (f, from) => { const i = f.v.indexOf(from); return [...f.v.slice(i), ...f.v.slice(0, i)]; };
        const fPath = rot(F, b); // b ... a
        const gPath = rot(G, a); // a ... b
        const merged = [...fPath, ...gPath.slice(1, -1)];
        if (new Set(merged).size !== merged.length) continue;
        F.v = merged;
        this.faces = this.faces.filter(f => f !== G);
        this.fsel.delete(G);
        ends.add(a); ends.add(b);
        this.changed();
      }
      // 辺が 2 本だけ残った頂点は、辺の途中の点なので消す（Blender の「頂点を溶解」と同じ）
      for (const v of ends) {
        if (this.vertEdges(v).length !== 2) continue;
        for (const f of this.faces) if (f.v.length > 3 && f.v.includes(v)) f.v = f.v.filter(x => x !== v);
        this.changed();
      }
      this.removeUnused(ends);
    }
    this.vsel.clear(); this.esel.clear(); this.fsel.clear();
    this.changed();
  }

  /** マージ（M）。how: "center" | "cursor" | "distance"。消えた頂点の数を返す */
  merge(how, cursor = new THREE.Vector3(), dist = 0.0001) {
    const sel = [...this.vsel];
    const map = new Map();
    if (how === "distance") {
      for (let i = 0; i < sel.length; i++) {
        if (map.has(sel[i])) continue;
        for (let j = i + 1; j < sel.length; j++) {
          if (!map.has(sel[j]) && sel[i].co.distanceTo(sel[j].co) <= dist) map.set(sel[j], sel[i]);
        }
      }
    } else {
      if (sel.length < 2) return 0;
      const keep = sel[0];
      if (how === "center") { const c = new THREE.Vector3(); for (const v of sel) c.add(v.co); keep.co.copy(c.divideScalar(sel.length)); }
      else keep.co.copy(cursor);
      for (const v of sel.slice(1)) map.set(v, keep);
    }
    if (!map.size) return 0;
    const m = v => map.get(v) ?? v;
    const seen = new Set();
    this.faces = this.faces.filter(f => {
      const vs = f.v.map(m).filter((v, i, arr) => v !== arr[(i + 1) % arr.length]);
      f.v = vs;
      const key = [...vs].map(v => v.id).sort((a, b) => a - b).join(",");
      if (vs.length < 3 || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
    this.loose = this.loose.map(([a, b]) => [m(a), m(b)]);
    this.tidyLoose();
    const removed = [...map.keys()];
    this.verts = this.verts.filter(v => !map.has(v));
    for (const v of removed) this.vsel.delete(v);
    this.changed();
    return removed.length;
  }

  /** 面を張る（F）。作ったものを返す: "face" | "edge" | null */
  fill(mode) {
    const sel = [...this.vsel];
    if (sel.length < 2) return null;
    const byKey = this.edgeMap();
    if (sel.length === 2) {
      if (byKey.has(edgeKey(sel[0], sel[1]))) return null;
      this.loose.push([sel[0], sel[1]]);
      this.changed();
      return "edge";
    }
    // 選択した境界の辺が輪になっていれば、その順に面を張る
    const set = new Set(sel);
    const cand = this.edges().filter(e => set.has(e.a) && set.has(e.b) && e.faces.length < 2);
    let order = null;
    if (cand.length === sel.length) {
      const next = new Map();
      for (const v of sel) next.set(v, cand.filter(e => e.a === v || e.b === v));
      if ([...next.values()].every(l => l.length === 2)) {
        order = [sel[0]];
        let prev = null, cur = sel[0];
        for (let i = 1; i < sel.length; i++) {
          const e = next.get(cur).find(x => this.other(x, cur) !== prev);
          prev = cur; cur = this.other(e, cur);
          order.push(cur);
        }
        if (new Set(order).size !== sel.length) order = null;
      }
    }
    if (!order) {
      // 輪になっていなければ、中心のまわりの角度で並べる
      const c = new THREE.Vector3(); for (const v of sel) c.add(v.co); c.divideScalar(sel.length);
      const n = new THREE.Vector3();
      for (let i = 0; i < sel.length; i++) n.add(sel[i].co.clone().sub(c).cross(sel[(i + 1) % sel.length].co.clone().sub(c)));
      if (n.lengthSq() < 1e-10) n.set(0, 0, 1);
      n.normalize();
      const u = sel[0].co.clone().sub(c).normalize(), w = n.clone().cross(u);
      order = [...sel].sort((a, b) => {
        const pa = a.co.clone().sub(c), pb = b.co.clone().sub(c);
        return Math.atan2(pa.dot(w), pa.dot(u)) - Math.atan2(pb.dot(w), pb.dot(u));
      });
    }
    // となりの面と向きをそろえる（となりの面が a→b なら、新しい面は b→a）
    for (let i = 0; i < order.length; i++) {
      const a = order[i], b = order[(i + 1) % order.length];
      const e = byKey.get(edgeKey(a, b));
      if (e?.faces.length && e.faces.some(f => EditMesh.hasDirected(f, a, b))) { order.reverse(); break; }
      if (e?.faces.length) break;
    }
    const face = { v: order };
    this.faces.push(face);
    this.tidyLoose();
    this.fsel.add(face);
    this.flush(mode === "face" ? "face" : mode);
    return "face";
  }

  /** 選んだ面を取り出して、新しいメッシュにする（P） */
  separate() {
    const fs = [...this.fsel];
    if (!fs.length) return null;
    const vs = [...new Set(fs.flatMap(f => f.v))];
    const index = new Map(vs.map((v, i) => [v, i]));
    const data = { v: vs.map(v => [v.co.x, v.co.y, v.co.z]), f: fs.map(f => f.v.map(v => index.get(v))) };
    this.faces = this.faces.filter(f => !this.fsel.has(f));
    this.changed();
    this.removeUnused(vs);
    this.vsel.clear(); this.esel.clear(); this.fsel.clear();
    return EditMesh.fromData(data);
  }

  // ---------------------------------------------------------------- 描画用
  /** 三角形に分けた位置・法線と、三角形 → 面の対応 */
  triangles() {
    const pos = [], nor = [], triFace = [];
    for (const f of this.faces) {
      const n = this.faceNormal(f);
      for (let i = 1; i < f.v.length - 1; i++) {
        for (const v of [f.v[0], f.v[i], f.v[i + 1]]) { pos.push(v.co.x, v.co.y, v.co.z); nor.push(n.x, n.y, n.z); }
        triFace.push(f);
      }
    }
    return { pos, nor, triFace };
  }
}

// ---------------------------------------------------------------- 基本の形（Blender と同じ面の構成）
function data(v, f) { return { v, f }; }
function ring(n, r, z, phase = 0) {
  return Array.from({ length: n }, (_, i) => { const a = (i / n) * Math.PI * 2 + phase; return [Math.cos(a) * r, Math.sin(a) * r, z]; });
}

export const SHAPES = {
  plane: () => data([[-1, -1, 0], [1, -1, 0], [1, 1, 0], [-1, 1, 0]], [[0, 1, 2, 3]]),
  cube: () => data(
    [[-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1], [-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1]],
    [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [2, 3, 7, 6], [3, 0, 4, 7], [1, 2, 6, 5]]),
  cylinder: (n = 32) => {
    const v = [...ring(n, 1, -1), ...ring(n, 1, 1)];
    const f = [];
    for (let i = 0; i < n; i++) f.push([i, (i + 1) % n, n + (i + 1) % n, n + i]);
    f.push(Array.from({ length: n }, (_, i) => n + i));
    f.push(Array.from({ length: n }, (_, i) => n - 1 - i));
    return data(v, f);
  },
  cone: (n = 32) => {
    const v = [...ring(n, 1, -1), [0, 0, 1]];
    const f = [];
    for (let i = 0; i < n; i++) f.push([i, (i + 1) % n, n]);
    f.push(Array.from({ length: n }, (_, i) => n - 1 - i));
    return data(v, f);
  },
  sphere: (seg = 32, rings = 16) => {
    const v = [[0, 0, 1]];
    for (let k = 1; k < rings; k++) {
      const th = (k / rings) * Math.PI;
      v.push(...ring(seg, Math.sin(th), Math.cos(th)));
    }
    v.push([0, 0, -1]);
    const R = (k, i) => 1 + (k - 1) * seg + (i % seg); // k = 1..rings-1
    const bottom = v.length - 1;
    const f = [];
    for (let i = 0; i < seg; i++) f.push([R(1, i), R(1, i + 1), 0]);
    for (let k = 1; k < rings - 1; k++) for (let i = 0; i < seg; i++) f.push([R(k + 1, i), R(k + 1, i + 1), R(k, i + 1), R(k, i)]);
    for (let i = 0; i < seg; i++) f.push([bottom, R(rings - 1, i + 1), R(rings - 1, i)]);
    return data(v, f);
  },
  torus: (n = 48, m = 12, R = 1, r = 0.25) => {
    const v = [];
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2;
      for (let j = 0; j < m; j++) {
        const b = (j / m) * Math.PI * 2;
        v.push([(R + r * Math.cos(b)) * Math.cos(a), (R + r * Math.cos(b)) * Math.sin(a), r * Math.sin(b)]);
      }
    }
    const I = (i, j) => (i % n) * m + (j % m);
    const f = [];
    for (let i = 0; i < n; i++) for (let j = 0; j < m; j++) f.push([I(i, j), I(i + 1, j), I(i + 1, j + 1), I(i, j + 1)]);
    return data(v, f);
  },
};
