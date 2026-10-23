// practice-edit-cases.json(S1: 編集モードを含む操作の突き合わせ用の入力)を作る。決まった乱数の種から作るので、何度作っても同じ。  node practice-edit-cases.generate.mjs
// 入力の中身は、JS(practice-reference.mjs が現行の viewport.js・editmesh.js・practice.js を動かす)と C#(FaraAvatarModdingGuide.Practice)の両方に同じものを渡す。
//   scenarios  3D ビューの操作列。決まった手順(編集モードの全操作・取り消し・メニュー・N パネル・共同練習の口など)と、乱数で作った長い操作列
//
// 作り方: 現行の JS を動かしながら手順を足していく(practice-reference.mjs の createHarness / applyStep)。頂点・辺・面の画面上の位置を JS の状態から調べて、
// そこをクリックしたりドラッグしたりする(当てずっぽうのクリックだと、ほとんど何も選べないため)。できあがった手順は座標・キー・修飾キーだけの固定の列になり、
// 突き合わせでは最初から再生される。JS が例外を投げた手順と、位置が有限でなくなった手順は、そこで列を打ち切る(打ち切った数と理由は標準出力に出す)。
import fs, { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { createHarness, applyStep } from "./practice-reference.mjs";
import { pumpFrame } from "./practice-dom.mjs";
import { makeRobust } from "./practice-fragile.mjs";
const THREE = await import("three");

function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
let r = rng(20261025);
const pick = a => a[Math.floor(r() * a.length)];
const between = (lo, hi) => lo + r() * (hi - lo);
const round = (x, d = 2) => Math.round(x * 10 ** d) / 10 ** d;
const chance = p => r() < p;

const DEG = Math.PI / 180;
const ROOT = { left: 37, top: 61, width: 912, height: 529 };
const DEFAULT_VIEW = { target: [0, 0, 0], dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, ortho: false, name: "ユーザー" };
// 手書きのシナリオの視点: 既定(真横の対角線)だと、対称なメッシュの頂点・辺が画面上で重なって、丸めの誤差でどれが選ばれるかが決まってしまうので、少しずらす
const HAND_VIEW = { ...DEFAULT_VIEW, theta: -2.31, phi: 1.07 };
const CX = ROOT.left + ROOT.width / 2, CY = ROOT.top + ROOT.height / 2;

// ---------------------------------------------------------------- 小さなメッシュ(頂点数が少ないほうが、突き合わせの出力が小さく速い)
const grid = (n, m = n) => {
  const v = [];
  for (let j = 0; j <= m; j++) for (let i = 0; i <= n; i++) v.push([-1 + (2 * i) / n, -1 + (2 * j) / m, 0]);
  const f = [];
  for (let j = 0; j < m; j++) for (let i = 0; i < n; i++) f.push([j * (n + 1) + i, j * (n + 1) + i + 1, (j + 1) * (n + 1) + i + 1, (j + 1) * (n + 1) + i]);
  return { v, f };
};
const cyl = (n, h = 1) => {
  const ring = z => Array.from({ length: n }, (_, i) => [Math.cos((i / n) * Math.PI * 2), Math.sin((i / n) * Math.PI * 2), z]);
  const v = [...ring(-h), ...ring(h)];
  const f = [];
  for (let i = 0; i < n; i++) f.push([i, (i + 1) % n, n + ((i + 1) % n), n + i]);
  f.push(Array.from({ length: n }, (_, i) => n + i));
  f.push(Array.from({ length: n }, (_, i) => n - 1 - i));
  return { v, f };
};
const lshape = () => ({
  v: [[0, 0, 0], [2, 0, 0], [2, 1, 0], [1, 1, 0], [1, 2, 0], [0, 2, 0], [0, 0, 1], [2, 0, 1], [2, 1, 1], [1, 1, 1], [1, 2, 1], [0, 2, 1]],
  f: [[5, 4, 3, 2, 1, 0], [6, 7, 8, 9, 10, 11], [0, 1, 7, 6], [1, 2, 8, 7], [2, 3, 9, 8], [3, 4, 10, 9], [4, 5, 11, 10], [5, 0, 6, 11]],
});
const tetra = () => ({ v: [[1, 1, 1], [-1, -1, 1], [-1, 1, -1], [1, -1, -1]], f: [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]] });
const looseMesh = () => ({ ...grid(2), v: [...grid(2).v, [3, 0, 0.5], [3, 1, 0.5], [0, 3, 0.5]], e: [[9, 10]] });
const uvs = () => {
  // 小さな UV 球(8 分割 × 4 段): 三角形の極と四角形の帯
  const seg = 8, rings = 4;
  const v = [[0, 0, 1]];
  for (let k = 1; k < rings; k++) { const th = (k / rings) * Math.PI; for (let i = 0; i < seg; i++) v.push([Math.sin(th) * Math.cos((i / seg) * Math.PI * 2), Math.sin(th) * Math.sin((i / seg) * Math.PI * 2), Math.cos(th)]); }
  v.push([0, 0, -1]);
  const R = (k, i) => 1 + (k - 1) * seg + (i % seg);
  const f = [];
  for (let i = 0; i < seg; i++) f.push([R(1, i), R(1, i + 1), 0]);
  for (let k = 1; k < rings - 1; k++) for (let i = 0; i < seg; i++) f.push([R(k + 1, i), R(k + 1, i + 1), R(k, i + 1), R(k, i)]);
  for (let i = 0; i < seg; i++) f.push([v.length - 1, R(rings - 1, i + 1), R(rings - 1, i)]);
  return { v, f };
};
// 0.0025 だけ離れて重なった 2 枚の四角形(上の板は下の板を、見える側から 0.0025 だけ手前で隠す)。isVisible の許容(最小 0.002)の境目を試す
const layers = () => {
  const lower = grid(2);   // 中央 (0,0,0) の頂点が、上の板の真下に入る
  return { v: [...lower.v, [-0.5, -0.5, 0.0025], [0.5, -0.5, 0.0025], [0.5, 0.5, 0.0025], [-0.5, 0.5, 0.0025]], f: [...lower.f, [9, 10, 11, 12]] };
};
const MESHES = {
  cube: null, plane: null, layers: layers(), grid3: grid(3), grid2x3: grid(2, 3), cyl8: cyl(8), cyl6: cyl(6, 0.5), lshape: lshape(), tetra: tetra(), loose: looseMesh(), uvs: uvs(),
};
const objectOf = (kind, name, extra = {}) => ({ kind, name, ...extra });
const withMesh = (kind, name, meshKey, extra = {}) => ({ ...objectOf(kind, name, extra), ...(MESHES[meshKey] ? { mesh: MESHES[meshKey] } : {}) });

// ---------------------------------------------------------------- 手順を作りながら JS で動かす
const jsErrors = [];
const caseNotes = [];

const KEYCHARS = { Space: " ", Enter: "Enter", Escape: "Escape", Tab: "Tab", Backspace: "Backspace", Delete: "Delete", Home: "Home", Minus: "-", Period: "." };
function keyChar(code, shift) {
  if (code.startsWith("Key")) return shift ? code[3] : code[3].toLowerCase();
  if (code.startsWith("Digit")) return code[5];
  if (code.startsWith("Numpad")) return code.slice(6) === "Decimal" ? "." : code.slice(6) === "Add" ? "+" : code.slice(6) === "Subtract" ? "-" : code.slice(6);
  return KEYCHARS[code] ?? code;
}
const mods = o => ({ ...(o.shift ? { shift: true } : {}), ...(o.ctrl ? { ctrl: true } : {}), ...(o.alt ? { alt: true } : {}), ...(o.meta ? { meta: true } : {}) });

function finiteState(vp) {
  const ok = v => Number.isFinite(v.x) && Number.isFinite(v.y) && Number.isFinite(v.z);
  for (const o of vp.objects) {
    if (!ok(o.position) || !ok(o.scale) || ![o.quaternion.x, o.quaternion.y, o.quaternion.z, o.quaternion.w].every(Number.isFinite)) return false;
    for (const v of o.userData.em.verts) if (!ok(v.co)) return false;
  }
  return ok(vp.view.target) && Number.isFinite(vp.view.dist) && Number.isFinite(vp.view.theta) && Number.isFinite(vp.view.phi);
}

class Gen {
  constructor(name, over = {}) {
    this.c = { name, root: ROOT, emulate: false, seed: 1, view: HAND_VIEW, objects: [objectOf("cube", "Cube")], selected: [], probes: [], steps: [], ...over };
    this.steps = this.c.steps;
    this.h = createHarness({ ...this.c, steps: [] });
    this.dead = false;
    this.error = null;
    this.autoHeal = false;
    this.healing = false;
  }
  get vp() { return this.h.vp; }
  get edit() { return this.vp.mode === "edit"; }
  get obj() { return this.vp.editObj ?? this.vp.active ?? this.vp.objects[0]; }
  get em() { return this.obj?.userData.em; }
  get count() { return this.steps.length; }
  refresh() { pumpFrame(this.vp); }

  do(s) {
    if (this.dead) return false;
    this.steps.push(s);
    try {
      applyStep(this.h, s);
    } catch (err) {
      this.steps.pop();
      this.dead = true;
      this.error = `${err.message.split("\n")[0]} @ ${this.c.name} 手順 ${this.steps.length + 1} ${JSON.stringify(s)}`;
      jsErrors.push({ where: this.c.name, step: this.steps.length + 1, input: s, message: String(err.stack ?? err).split("\n").slice(0, 3).join(" | ") });
      return false;
    }
    this.h.events.length = 0;
    if (!finiteState(this.vp)) {
      this.steps.pop();
      this.dead = true;
      this.error = "位置が有限でなくなった";
      console.log(`  非有限: ${this.c.name} 手順 ${this.steps.length + 1} ${JSON.stringify(s)}`);
      return false;
    }
    if (this.autoHeal && !this.healing) this.heal();
    return true;
  }

  /**
   * ほとんど重なっているだけで、ぴったりは重なっていない頂点(0 < 距離 < 1e-6)は、丸めの誤差で結果が決まる(クリックでどちらが選ばれるか、面を張る順、押し出しの向きなど)。
   * 現行の JS と C# は同じ式でも最後の桁の誤差が違うことがあるので、そういう状態は突き合わせの入力にしない: 距離でマージ(M → 距離で)して消してから続ける
   */
  heal() {
    const vp = this.vp;
    if (this.dead || !this.edit || vp.op || vp.loopcut || vp.menu) return;
    if (!this.nearCoincident()) return;
    this.healing = true;
    const keepMode = vp.selectMode;
    this.selmode(1);
    this.key("KeyA", { alt: true });
    this.key("KeyA");
    this.key("KeyM");
    this.menu(2);
    this.key("KeyA", { alt: true });
    this.selmode(keepMode === "vert" ? 1 : keepMode === "edge" ? 2 : 3);
    this.healing = false;
    if (this.nearCoincident()) { this.dead = true; this.error = "ほとんど重なった頂点を消せない"; }
  }

  nearCoincident() {
    const vs = this.em.verts;
    for (let i = 0; i < vs.length; i++) for (let j = i + 1; j < vs.length; j++) { const d = vs[i].co.distanceTo(vs[j].co); if (d < 1e-6) return true; }
    return false;
  }

  key(code, o = {}) { return this.do({ e: "key", code, key: o.key ?? keyChar(code, o.shift), ...mods(o) }); }
  keyup(o = {}) { return this.do({ e: "keyup", ...mods(o) }); }
  type(codes) { for (const c of codes) this.key(c); }
  mv(x, y, o = {}) { return this.do({ e: "move", x: round(x), y: round(y), ...mods(o) }); }
  down(x, y, button = 0, o = {}) { return this.do({ e: "down", x: round(x), y: round(y), button, ...mods(o) }); }
  up(x, y, button = 0, o = {}) { return this.do({ e: "up", x: round(x), y: round(y), button, ...mods(o) }); }
  click(x, y, o = {}) { this.mv(x, y, o); this.down(x, y, 0, o); this.up(x, y, 0, o); }
  rclick(x, y) { this.mv(x, y); this.down(x, y, 2); this.up(x, y, 2); }
  drag(x0, y0, x1, y1, button = 0, o = {}) { this.mv(x0, y0, o); this.down(x0, y0, button, o); this.mv((x0 + x1) / 2, (y0 + y1) / 2, o); this.mv(x1, y1, o); this.up(x1, y1, button, o); }
  wheel(dy) { return this.do({ e: "wheel", dy }); }
  menu(i) { return this.do({ e: "menu", i }); }
  tool(t) { return this.do({ e: "tool", tool: t }); }
  digit(d) { return this.key("Digit" + d); }
  num(text) { for (const ch of String(text)) this.key(ch === "." ? "Period" : ch === "-" ? "Minus" : "Digit" + ch); }
  enter() { return this.key("Enter"); }
  esc() { return this.key("Escape"); }
  tab() { return this.key("Tab"); }
  undo() { return this.key("KeyZ", { ctrl: true }); }
  redo() { return this.key("KeyZ", { ctrl: true, shift: true }); }
  selmode(n) { return this.key("Digit" + n); }

  // ---- 画面上の位置(JS の状態から)
  screen(w) { this.refresh(); const s = this.vp.toScreen(w); return { x: ROOT.left + s.x, y: ROOT.top + s.y }; }
  vertAt(i) { const obj = this.obj; return this.screen(this.vp.worldOf(this.em.verts[i], obj)); }
  edges() { return this.em.edges(); }
  edgeMid(e) { const obj = this.obj; return this.screen(this.vp.worldOf(e.a, obj).clone().lerp(this.vp.worldOf(e.b, obj), 0.5)); }
  faceMid(f) { return this.screen(this.em.faceCenter(f).applyMatrix4(this.obj.matrixWorld)); }
  jitter(p, a = 2) { return { x: p.x + between(-a, a), y: p.y + between(-a, a) }; }
  clickVert(i, o = {}) { const p = this.vertAt(i); this.click(p.x, p.y, o); }
  clickEdge(e, o = {}) { const p = this.edgeMid(e); this.click(p.x, p.y, o); }
  clickFace(f, o = {}) { const p = this.faceMid(f); this.click(p.x, p.y, o); }
  rect(a, b, o = {}) { this.drag(Math.min(a.x, b.x), Math.min(a.y, b.y), Math.max(a.x, b.x), Math.max(a.y, b.y), 0, o); }
  boxAround(points, margin = 8, o = {}) {
    const xs = points.map(p => p.x), ys = points.map(p => p.y);
    this.drag(Math.min(...xs) - margin, Math.min(...ys) - margin, Math.max(...xs) + margin, Math.max(...ys) + margin, 0, o);
  }
  finish(how = "enter") {
    if (how === "enter") this.enter();
    else if (how === "click") this.click(CX + between(-60, 60), CY + between(-40, 40));
    else if (how === "esc") this.esc();
    else this.rclick(CX + between(-60, 60), CY + between(-40, 40));
  }
  mouseAroundCenter(spread = 150) { this.mv(CX + between(-spread, spread), CY + between(-spread * 0.6, spread * 0.6)); }
}

const scenarios = [];
function scenario(name, over, body) {
  const g = new Gen(name, over);
  try { body(g); } catch (err) { console.log(`  組み立てで例外: ${name}: ${String(err.stack).split("\n").slice(0, 3).join(" | ")}`); g.dead = true; g.error = "組み立ての例外"; }
  if (g.dead) caseNotes.push(`${name}: 打ち切り(${g.error})`);
  g.c.probes = probesFor(g);
  scenarios.push(g.c);
  return g;
}

function probesFor(g) {
  const out = [];
  for (const [fx, fy] of [[0.5, 0.5], [0.3, 0.4], [0.7, 0.6], [0.52, 0.45]]) out.push({ x: round(ROOT.left + ROOT.width * fx, 1), y: round(ROOT.top + ROOT.height * fy, 1), world: [round(between(-2, 2), 2), round(between(-2, 2), 2), round(between(-1, 3), 2)], pivot: [0, 0, 0.5] });
  return out;
}

// ---------------------------------------------------------------- 決まった手順
const T = { ctrl: true };

// Tab で編集モードに入って出る・選択モードの切り替え・全選択/解除/反転
scenario("編集モード: Tab・1/2/3・全選択・解除・反転・選択なし", { selected: [0] }, g => {
  g.tab(); g.selmode(2); g.selmode(3); g.selmode(1);
  g.key("KeyA"); g.key("KeyA", { alt: true }); g.key("KeyA"); g.key("KeyI", T); g.key("KeyI", T);
  g.selmode(2); g.key("KeyA"); g.selmode(3); g.key("KeyA", { alt: true }); g.selmode(1); g.key("KeyA", { alt: true });
  g.key("KeyE"); g.key("KeyI"); g.key("KeyG"); g.key("KeyR"); g.key("KeyS"); g.key("KeyF"); g.key("KeyM"); g.key("KeyP"); g.key("KeyB", T); g.key("KeyR", T);
  g.key("KeyX"); g.key("Delete"); g.key("KeyL"); g.key("KeyL", T); g.key("KeyB"); g.key("KeyD", { shift: true }); g.key("KeyD"); g.key("KeyK"); g.key("KeyH"); g.key("KeyQ");
  g.key("KeyA", { shift: true }); g.tab(); g.tab();
});
scenario("Tab: 選択なし・複数オブジェクト・アクティブだけが編集に入る", { objects: [objectOf("cube", "Cube"), objectOf("cone", "Cone", { p: [3, 0, 0] })], selected: [] }, g => {
  g.tab(); g.click(CX, CY - 10); g.tab(); g.key("KeyA"); g.tab(); g.tab(); g.key("KeyA"); g.tab(); g.selmode(3); g.tab();
  g.click(CX + 200, CY + 20, { shift: true }); g.tab(); g.key("KeyA"); g.tab();
});

// 押し出し: 領域・辺・頂点・軸・数値・キャンセル・取り消し
scenario("押し出し(面): E → 1 → Enter(お題)・軸・Esc・右クリック・取り消し", { selected: [0] }, g => {
  g.tab(); g.selmode(3);
  const top = g.em.faces.find(f => g.em.faceNormal(f).z > 0.9);
  g.clickFace(top);
  g.mv(CX + 20, CY - 30); g.key("KeyE"); g.digit(1); g.enter();
  g.undo();
  g.clickFace(g.em.faces.find(f => g.em.faceNormal(f).z > 0.9));
  g.key("KeyE"); g.mv(CX + 50, CY - 120); g.key("KeyZ"); g.mv(CX + 50, CY - 150); g.key("KeyX"); g.key("KeyX", { shift: true }); g.mv(CX + 80, CY - 150); g.enter();
  g.key("KeyE"); g.mv(CX + 5, CY - 40); g.esc();
  g.key("KeyE"); g.mv(CX + 5, CY - 80); g.rclick(CX, CY - 80);
  g.undo(); g.undo(); g.redo(); g.redo(); g.redo();
});
scenario("押し出し(複数の面の領域・辺・頂点・数値・押し出した向きを外す)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(3);
  const fs = g.em.faces;
  g.clickFace(fs[4]); g.clickFace(fs[5], { shift: true }); g.clickFace(fs[1], { shift: true });
  g.key("KeyE"); g.mv(CX + 30, CY - 100); g.key("KeyZ"); g.key("KeyZ"); g.num("0.5"); g.enter();
  g.selmode(2); g.key("KeyA", { alt: true });
  const es = g.edges();
  g.clickEdge(es[2]); g.clickEdge(es[5], { shift: true });
  g.key("KeyE"); g.mv(CX - 40, CY + 60); g.key("KeyX"); g.num("1.5"); g.key("Minus"); g.key("Backspace"); g.enter();
  g.selmode(1); g.key("KeyA", { alt: true });
  g.clickVert(3); g.clickVert(6, { shift: true });
  g.key("KeyE"); g.mv(CX + 10, CY + 10); g.num("2"); g.enter();
  g.key("KeyA"); g.key("KeyE"); g.mv(CX, CY - 60); g.enter();
  g.key("KeyA", { alt: true }); g.key("KeyE"); g.key("KeyG");
  g.tab(); g.key("KeyG"); g.tab();
});
scenario("押し出しの境界: 空の選択・1 点・輪になった辺・境界の辺(開いたメッシュ)", { selected: [0], objects: [withMesh("plane", "Plane", "grid2x3")] }, g => {
  g.tab(); g.selmode(1);
  g.key("KeyE"); g.key("KeyE", { shift: true });
  g.clickVert(0); g.key("KeyE"); g.mv(CX + 30, CY); g.enter();
  g.selmode(2); g.key("KeyA", { alt: true });
  for (const e of g.edges().filter(e => e.faces.length === 1).slice(0, 4)) g.clickEdge(e, { shift: true });
  g.key("KeyE"); g.mv(CX, CY - 80); g.key("KeyZ"); g.enter();
  g.selmode(3); g.key("KeyA");
  g.key("KeyE"); g.mv(CX + 10, CY - 10); g.num("0.7"); g.enter();
  g.undo(); g.undo(); g.undo();
});

// 差し込み
scenario("差し込み: I → 0.3 → Enter(お題)・マウス・Ctrl 刻み・Shift 細かく・上限・Esc で戻る", { selected: [0] }, g => {
  g.tab(); g.selmode(3);
  g.key("KeyI");
  g.clickFace(g.em.faces[1]);
  g.key("KeyI"); g.num("0.3"); g.enter();
  g.undo();
  g.clickFace(g.em.faces[1]); g.mv(CX + 5, CY - 30);
  g.key("KeyI"); g.mv(CX + 40, CY - 30); g.mv(CX + 120, CY - 40, T); g.mv(CX + 120, CY - 40, { shift: true }); g.mv(CX + 300, CY - 40); g.enter();
  g.key("KeyI"); g.mv(CX - 100, CY); g.mv(CX - 10, CY + 20); g.key("ShiftLeft", { shift: true }); g.keyup(); g.esc();
  g.key("KeyI"); g.num("99"); g.enter();
  g.key("KeyI"); g.num("-.5"); g.enter();
  g.key("KeyI"); g.num("0.2"); g.key("Backspace"); g.key("Backspace"); g.key("Backspace"); g.rclick(CX, CY);
  g.undo(); g.undo(); g.redo();
});
scenario("差し込み: 複数の面・三角形・n 角形・非凸", { selected: [0], objects: [withMesh("cube", "Cube", "lshape")] }, g => {
  g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyI"); g.mv(CX + 60, CY + 20); g.mv(CX - 60, CY - 30); g.enter();
  g.key("KeyA", { alt: true }); g.clickFace(g.em.faces[0]); g.key("KeyI"); g.num("0.2"); g.enter();
  g.undo(); g.undo(); g.undo(); g.undo();
});
scenario("差し込み: 円錐・円柱・UV 球(三角形・n 角形)", { selected: [0], objects: [withMesh("cone", "Cone", "cyl6"), withMesh("sphere", "Sphere", "uvs", { p: [4, 0, 0] })] }, g => {
  g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyI"); g.mv(CX - 40, CY - 40); g.enter(); g.tab();
  g.click(CX + 250, CY - 10); g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyI"); g.num("0.1"); g.enter(); g.undo();
});

// ベベル
scenario("ベベル: Ctrl+B(辺 1 本・複数・立方体の全辺・マウス・数値・ホイール・Esc)", { selected: [0] }, g => {
  g.tab(); g.key("KeyB", T); g.selmode(2);
  g.key("KeyB", T);
  const es = g.edges();
  g.clickEdge(es[0]); g.key("KeyB", T); g.mv(CX + 20, CY); g.mv(CX + 90, CY + 10); g.wheel(-100); g.mv(CX + 130, CY + 10, T); g.enter();
  g.undo();
  g.clickEdge(es[0]); g.clickEdge(es[1], { shift: true }); g.clickEdge(es[2], { shift: true });
  g.key("KeyB", T); g.num("0.1"); g.enter();
  g.undo();
  g.key("KeyA"); g.key("KeyB", T); g.mv(CX + 50, CY + 30); g.esc();
  g.key("KeyB", T); g.mv(CX + 50, CY + 30); g.num("0.2"); g.rclick(CX, CY);
  g.key("KeyB", T); g.num("0.3"); g.enter();
  g.undo(); g.undo(); g.redo();
});
scenario("ベベル: 頂点モード・面モードからの辺・角(3 辺)・失敗(開いたメッシュ・非多様体)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(2); g.key("KeyA"); g.key("KeyB", T); g.key("KeyA", { alt: true });
  g.clickEdge(g.edges().find(e => e.faces.length === 2)); g.key("KeyB", T); g.mv(CX + 40, CY); g.enter();
  g.selmode(1); g.key("KeyA", { alt: true }); g.key("KeyA"); g.key("KeyB", T); g.enter();
  g.selmode(3); g.key("KeyA", { alt: true }); g.clickFace(g.em.faces[4]); g.key("KeyB", T);
});
scenario("ベベル: 円柱・UV 球・ループ選択の辺", { selected: [0], objects: [withMesh("cylinder", "Cylinder", "cyl8")] }, g => {
  g.tab(); g.selmode(2);
  const es = g.edges();
  g.mv(CX, CY); g.do({ e: "down", x: g.edgeMid(es[0]).x, y: g.edgeMid(es[0]).y, button: 0, alt: true }); g.do({ e: "up", x: g.edgeMid(es[0]).x, y: g.edgeMid(es[0]).y, button: 0, alt: true });
  g.key("KeyB", T); g.mv(CX + 40, CY + 20); g.enter();
  g.undo(); g.key("KeyA", { alt: true });
  g.clickEdge(es[3]); g.clickEdge(es[4], { shift: true }); g.key("KeyB", T); g.num("0.15"); g.enter();
});
scenario("ベベル: 開いたメッシュと辺なし・面が 2 つでない辺", { selected: [0], objects: [withMesh("plane", "Plane", "grid2x3")] }, g => {
  g.tab(); g.selmode(2); g.key("KeyA"); g.key("KeyB", T); g.key("KeyA", { alt: true });
  g.clickEdge(g.edges().find(e => e.faces.length === 2)); g.key("KeyB", T);
  g.key("KeyA", { alt: true }); g.key("KeyB", T);
  g.undo();
});

// ループカット
scenario("ループカット: Ctrl+R → ホイールで本数 → クリックで確定 → 位置調整(お題)", { selected: [0] }, g => {
  g.tab(); g.key("KeyR", T); g.esc();
  g.key("KeyR", T);
  const e = g.edges().find(x => Math.abs(g.em.faceNormal(x.faces[0]).z) < 0.1 && Math.abs(x.a.co.z - x.b.co.z) > 0.5);
  const p = g.edgeMid(e);
  g.mv(p.x, p.y); g.wheel(-120); g.wheel(-120); g.wheel(120); g.mv(p.x + 2, p.y);
  g.down(p.x, p.y, 0); g.up(p.x, p.y, 0);
  g.mv(CX + 100, CY); g.mv(CX + 160, CY + 20); g.enter();
  g.undo();
  g.key("KeyR", T); g.mv(p.x, p.y); g.enter(); g.mv(CX + 60, CY); g.esc();
  g.key("KeyR", T); g.mv(p.x, p.y); g.rclick(p.x, p.y);
  g.key("KeyR", T); g.mv(CX - 400, CY - 250); g.enter(); g.key("KeyR", T); g.mv(CX - 400, CY - 250); g.click(CX - 400, CY - 250);
  g.key("KeyR", T); g.mv(p.x, p.y); g.num("0.4");
  for (let i = 0; i < 25; i++) g.wheel(-120);
  for (let i = 0; i < 25; i++) g.wheel(120);
  g.key("KeyX"); g.click(p.x, p.y); g.num("0.5"); g.enter();
  g.undo(); g.undo();
});
scenario("ループカット: 位置調整の数値・マウス・右クリックで中央・Esc で中央", { selected: [0] }, g => {
  g.tab();
  let n = 0;
  const e = () => { const quads = g.edges().filter(x => x.faces.length === 2 && x.faces.every(f => f.v.length === 4)); return quads[(n++ * 5) % quads.length]; };
  for (const how of ["num", "mouse", "rclick", "esc", "ctrl", "shift"]) {
    g.key("KeyR", T);
    const p = g.edgeMid(e());
    g.mv(p.x, p.y); g.wheel(-120); g.click(p.x, p.y);
    if (how === "num") { g.num("0.3"); g.enter(); }
    else if (how === "mouse") { g.mv(p.x + 15, p.y - 12); g.mv(p.x + 140, p.y + 20); g.enter(); }
    else if (how === "rclick") { g.mv(p.x + 15, p.y - 12); g.rclick(p.x + 15, p.y - 12); }
    else if (how === "esc") { g.mv(p.x + 15, p.y - 12); g.esc(); }
    else if (how === "ctrl") { g.mv(p.x + 60, p.y, T); g.enter(); }
    else { g.mv(p.x + 60, p.y, { shift: true }); g.num("-.9"); g.num("5"); g.enter(); }
  }
});
scenario("ループカット: 円柱(キャップ n 角形で止まる)・UV 球(三角形)・開いたメッシュ・四角形なし", { selected: [0], objects: [withMesh("cylinder", "Cylinder", "cyl8")] }, g => {
  g.tab();
  for (const e of g.edges().slice(0, 18).filter((_, i) => i % 3 === 0)) {
    g.key("KeyR", T); const p = g.edgeMid(e); g.mv(p.x, p.y); g.wheel(-120); g.click(p.x, p.y); g.mv(CX + 20, CY + 20); g.enter();
  }
  g.undo(); g.undo();
});
scenario("ループカット: 平面のグリッド(開いたリング)・三角形の面・L 字", { selected: [0], objects: [withMesh("plane", "Plane", "grid3")] }, g => {
  g.tab();
  for (const e of g.edges().filter(e => e.faces.length === 1).slice(0, 3)) { g.key("KeyR", T); const p = g.edgeMid(e); g.mv(p.x, p.y); g.click(p.x, p.y); g.mv(CX + 30, CY); g.enter(); }
  g.undo(); g.undo();
});
scenario("ループカット: 四角形がない(四面体)・辺がない所", { selected: [0], objects: [withMesh("cube", "Cube", "tetra")] }, g => {
  g.tab();
  g.key("KeyR", T); const p = g.edgeMid(g.edges()[0]); g.mv(p.x, p.y); g.click(p.x, p.y); g.esc();
});

// 削除・溶解
scenario("削除(X): 頂点・辺・面・溶解・選択なし・Esc", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab();
  g.key("KeyX"); g.key("Delete"); g.esc();
  g.clickVert(5); g.key("KeyX"); g.menu(0);
  g.selmode(2); g.clickEdge(g.edges()[7]); g.key("KeyX"); g.menu(1);
  g.selmode(3); g.clickFace(g.em.faces[0]); g.key("KeyX"); g.menu(2);
  g.undo(); g.undo(); g.undo();
  g.selmode(2); g.clickEdge(g.edges().find(e => e.faces.length === 2)); g.key("Delete"); g.menu(3);
  g.key("KeyA"); g.key("KeyX"); g.menu(3);
  g.undo();
  g.key("KeyX"); g.key("KeyX"); g.click(CX, CY);
  g.key("KeyA"); g.key("Delete"); g.menu(9); g.menu(0);
});
scenario("溶解: 内部の辺・境界の辺・複数・立方体", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(2);
  const inner = g.edges().filter(e => e.faces.length === 2);
  g.clickEdge(inner[0]); g.key("KeyX"); g.menu(3);
  g.clickEdge(inner[2]); g.clickEdge(inner[3], { shift: true }); g.key("KeyX"); g.menu(3);
  g.key("KeyA", { alt: true }); g.clickEdge(g.edges().find(e => e.faces.length === 1)); g.key("KeyX"); g.menu(3);
  g.undo(); g.undo(); g.undo(); g.redo();
});
scenario("溶解: 立方体の辺(2 本残る頂点が消える)", { selected: [0] }, g => {
  g.tab(); g.selmode(2); g.clickEdge(g.edges()[0]); g.key("KeyX"); g.menu(3);
  g.key("KeyA", { alt: true }); g.clickEdge(g.edges()[1]); g.key("KeyX"); g.menu(3);
});

// マージ
scenario("マージ(M): 中心に・カーソル位置に・距離で・1 点・選択なし", { selected: [0] }, g => {
  g.tab(); g.key("KeyM"); g.selmode(1); g.key("KeyM");
  g.clickVert(0); g.key("KeyM");
  g.clickVert(1, { shift: true }); g.key("KeyM"); g.menu(0);
  g.undo();
  g.key("KeyM"); g.menu(1);
  g.undo();
  g.key("KeyM"); g.menu(2);
  g.key("KeyA"); g.key("KeyM"); g.menu(0);
  g.undo(); g.key("KeyA"); g.key("KeyM"); g.menu(1); g.undo(); g.key("KeyM"); g.menu(2); g.undo();
});
scenario("マージ: 距離で(重なった頂点を作って)・面が潰れる・重複する面を消す", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(3); g.clickFace(g.em.faces[4]); g.key("KeyE"); g.enter();
  g.selmode(1); g.key("KeyA"); g.key("KeyM"); g.menu(2);
  g.undo(); g.key("KeyM"); g.menu(0); g.key("KeyA"); g.key("KeyM"); g.menu(0);
  g.undo(); g.undo(); g.undo();
});

// 面を張る
scenario("面を張る(F): 2 点→辺・3 点以上→面・輪になった境界・既存の辺・1 点", { selected: [0], objects: [withMesh("plane", "Plane", "grid3")] }, g => {
  g.tab(); g.selmode(1); g.key("KeyF");
  g.clickVert(0); g.key("KeyF"); g.clickVert(5, { shift: true }); g.key("KeyF"); g.key("KeyF");
  g.undo();
  g.key("KeyA", { alt: true }); g.clickVert(0); g.clickVert(1, { shift: true }); g.key("KeyF");
  g.key("KeyA", { alt: true }); for (const i of [0, 3, 12, 15]) g.clickVert(i, { shift: true }); g.key("KeyF");
  g.undo(); g.undo();
  g.key("KeyA", { alt: true }); for (const i of [5, 6, 10, 9]) g.clickVert(i, { shift: true }); g.key("KeyF");
  g.selmode(2); g.key("KeyA", { alt: true }); g.key("KeyF");
});
scenario("面を張る: 押し出した辺の穴・散らばった頂点・向きをそろえる", { selected: [0], objects: [withMesh("plane", "Plane", "loose")] }, g => {
  g.tab(); g.selmode(1);
  g.key("KeyA"); g.key("KeyF"); g.key("KeyA", { alt: true });
  for (const i of [9, 10, 11]) g.clickVert(i, { shift: true });
  g.key("KeyF"); g.key("KeyA", { alt: true });
  for (const i of [9, 10, 4, 3]) g.clickVert(i, { shift: true });
  g.key("KeyF"); g.undo(); g.undo();
});
scenario("面を張る: 閉じたメッシュと開いたメッシュ・n 角形", { selected: [0], objects: [withMesh("cylinder", "Cylinder", "cyl6")] }, g => {
  g.tab(); g.selmode(3); g.clickFace(g.em.faces[g.em.faces.length - 1]); g.key("KeyX"); g.menu(2);
  g.selmode(1); g.key("KeyF");
  for (const i of [6, 7, 8, 9, 10, 11]) g.clickVert(i, { shift: true });
  g.key("KeyF"); g.undo(); g.key("KeyA", { alt: true }); g.clickVert(6); g.clickVert(8, { shift: true }); g.key("KeyF"); g.clickVert(10, { shift: true }); g.key("KeyF");
});

// 分離
scenario("分離(P): 選択した面を別のオブジェクトに・面なし", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.key("KeyP"); g.menu(0);
  g.selmode(3); g.key("KeyP"); g.menu(0);
  g.clickFace(g.em.faces[4]); g.clickFace(g.em.faces[5], { shift: true }); g.key("KeyP"); g.menu(0);
  g.tab(); g.click(CX + 5, CY - 5); g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyP"); g.menu(0);
  g.undo(); g.undo(); g.redo();
});
scenario("分離: 回転・スケールしたオブジェクト・ミラー付き・非対称", { selected: [0], objects: [objectOf("cube", "Cube", { p: [1, 0, 0], r: [10, 20, 30], s: [1, 2, 0.5] })] }, g => {
  g.tab(); g.selmode(3); g.clickFace(g.em.faces[1]); g.key("KeyP"); g.menu(0);
  g.tab(); g.tool("mirror"); g.tab(); g.key("Home"); g.key("NumpadDecimal"); g.tab(); g.tool("mirror");
});

// 選択
scenario("選択: クリック・Shift+クリック・外れ・頂点/辺/面で切り替えたときの変換", { selected: [0] }, g => {
  g.tab();
  for (const m of [1, 2, 3, 1]) {
    g.selmode(m);
    g.key("KeyA", { alt: true });
    for (const i of [0, 1, 2]) {
      if (m === 1) g.clickVert(i * 2, { shift: i > 0 });
      else if (m === 2) g.clickEdge(g.edges()[i * 3], { shift: i > 0 });
      else g.clickFace(g.em.faces[i], { shift: i > 0 });
    }
    g.click(CX + 380, CY - 230); g.click(CX + 380, CY - 230, { shift: true });
  }
  g.selmode(1); g.clickVert(0); g.clickVert(1, { shift: true }); g.clickVert(3, { shift: true }); g.clickVert(2, { shift: true }); g.selmode(3); g.selmode(2); g.selmode(1);
  g.clickVert(0); g.clickVert(0, { shift: true });
});
scenario("選択: ボックス(頂点・辺・面)・Shift で追加・透過で裏側", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab();
  for (const m of [1, 2, 3]) {
    g.selmode(m); g.key("KeyA", { alt: true });
    g.rect({ x: CX - 120, y: CY - 100 }, { x: CX + 120, y: CY + 80 });
    g.rect({ x: CX + 200, y: CY - 200 }, { x: CX + 300, y: CY - 100 }, { shift: true });
    g.key("KeyZ", { alt: true });
    g.key("KeyA", { alt: true }); g.rect({ x: CX - 160, y: CY - 160 }, { x: CX + 160, y: CY + 120 });
    g.key("KeyZ", { alt: true });
    g.key("KeyA", { alt: true }); g.rect({ x: CX - 40, y: CY - 40 }, { x: CX + 40, y: CY + 40 });
  }
  g.key("KeyB");
});
scenario("選択: Alt+クリックのループ・Ctrl+Alt+クリックのリング(頂点・辺・面)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab();
  for (const m of [1, 2, 3]) {
    g.selmode(m); g.key("KeyA", { alt: true });
    for (const idx of [0, 5, 11, 20]) {
      const e = g.edges()[idx];
      if (!e) continue;
      const p = g.edgeMid(e);
      g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true }); g.up(p.x, p.y, 0, { alt: true });
      g.mv(p.x + 1, p.y); g.down(p.x + 1, p.y, 0, { alt: true, ctrl: true }); g.up(p.x + 1, p.y, 0, { alt: true, ctrl: true });
      g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true, shift: true }); g.up(p.x, p.y, 0, { alt: true, shift: true });
    }
    g.mv(CX + 400, CY - 250); g.down(CX + 400, CY - 250, 0, { alt: true }); g.up(CX + 400, CY - 250, 0, { alt: true });
  }
});
scenario("選択: Alt+ドラッグは視点を回す・Alt+クリックの辺なし", { selected: [0] }, g => {
  g.tab(); g.selmode(2);
  const p = g.edgeMid(g.edges()[0]);
  g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true }); g.mv(p.x + 3, p.y); g.mv(p.x + 40, p.y + 12); g.mv(p.x + 60, p.y + 20); g.up(p.x + 60, p.y + 20, 0, { alt: true });
  g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true }); g.mv(p.x + 2, p.y); g.up(p.x + 2, p.y, 0, { alt: true });
});
scenario("選択: L(マウスの下)・Ctrl+L・Ctrl+Numpad +/-(拡大・縮小)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3"), objectOf("cone", "Cone", { p: [4, 1, 0] })] }, g => {
  g.tab();
  for (const m of [1, 2, 3]) {
    g.selmode(m); g.key("KeyA", { alt: true });
    g.key("KeyL"); g.key("KeyL", T);
    const p = g.vertAt(5); g.mv(p.x, p.y); g.key("KeyL");
    g.key("KeyA", { alt: true }); const q = g.edgeMid(g.edges()[3]); g.mv(q.x, q.y); g.key("KeyL");
    g.key("KeyA", { alt: true }); const f = g.faceMid(g.em.faces[2]); g.mv(f.x, f.y); g.key("KeyL");
    g.key("KeyA", { alt: true }); g.mv(CX + 420, CY - 250); g.key("KeyL");
    g.clickVert(0); g.key("KeyL", T);
    g.key("NumpadAdd", T); g.key("NumpadAdd", T); g.key("NumpadSubtract", T); g.key("NumpadSubtract", T); g.key("NumpadSubtract", T); g.key("NumpadSubtract", T);
    g.key("KeyA", { alt: true });
  }
});

// 取り消し・やり直し
scenario("取り消し/やり直し: 50 件の上限(それより古い操作は戻せない)・やり直しの破棄", { selected: [0] }, g => {
  g.undo(); g.redo();
  // 数値で動かす(マウスの位置に左右されない)操作を 60 回。取り消しは 50 件まで
  for (let i = 0; i < 60; i++) { g.key("KeyG"); g.key("KeyX"); g.digit(1 + (i % 3)); g.enter(); }
  for (let i = 0; i < 55; i++) g.undo();
  for (let i = 0; i < 5; i++) g.redo();
  g.key("KeyG"); g.key("KeyY"); g.digit(2); g.enter();   // 新しい操作で、やり直しの記録は消える
  g.redo(); g.undo(); g.undo(); g.redo();
});
scenario("取り消し/やり直し: 編集とモードの復元", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.undo(); g.redo();
  g.tab(); g.selmode(3);
  for (let i = 0; i < 12; i++) {
    g.clickFace(g.em.faces[i % g.em.faces.length]);
    g.key("KeyE"); g.digit(1 + (i % 2)); g.enter();
    if (g.em.verts.length > 60) { g.undo(); g.undo(); }
  }
  for (let i = 0; i < 6; i++) g.undo();
  g.redo(); g.redo();
  g.key("KeyA", { alt: true }); g.clickFace(g.em.faces[0]); g.key("KeyE"); g.num("0.5"); g.enter();
  g.redo(); g.tab(); g.undo(); g.undo(); g.redo(); g.redo(); g.redo(); g.undo(); g.tab(); g.undo(); g.undo();
});
scenario("取り消し: オブジェクトの追加・削除・複製・N パネル・ミラー・別オブジェクトの選択の復元", { selected: [] }, g => {
  g.click(CX, CY - 10);
  g.key("KeyA", { shift: true }); g.menu(2); g.key("KeyA", { shift: true }); g.menu(5);
  g.key("KeyD", { shift: true }); g.mv(CX + 100, CY); g.enter();
  g.undo(); g.undo(); g.undo(); g.undo(); g.undo(); g.redo(); g.redo(); g.redo();
  g.key("KeyX"); g.menu(0); g.undo();
  g.key("KeyN"); g.do({ e: "nchange", prop: "position", axis: "x", value: "2" }); g.do({ e: "nchange", prop: "scale", axis: "z", value: "1.5" });
  g.undo(); g.undo(); g.redo();
});

// オブジェクトモード
scenario("追加(Shift+A): 6 種類・メニューを閉じる・編集モードでは追加できない・名前の連番", { selected: [] }, g => {
  for (const i of [0, 1, 2, 3, 4, 5, 2, 2, 1]) { g.key("KeyA", { shift: true }); g.menu(i); }
  g.key("KeyA", { shift: true }); g.esc(); g.key("KeyA", { shift: true }); g.click(CX + 300, CY - 200);
  g.tab(); g.key("KeyA", { shift: true }); g.tab();
  g.key("KeyA"); g.key("KeyX"); g.menu(0); g.key("KeyX"); g.key("KeyX");
});
scenario("複製(Shift+D): 移動して確定・キャンセルしても残る・取り消しで 1 回にまとまる・複数", { selected: [0] }, g => {
  g.key("KeyD", { shift: true }); g.mv(CX + 140, CY + 20); g.enter();
  g.key("KeyD", { shift: true }); g.mv(CX - 140, CY + 60); g.esc();
  g.key("KeyD", { shift: true }); g.mv(CX - 40, CY + 60); g.rclick(CX, CY);
  g.undo(); g.undo(); g.undo(); g.undo();
  g.key("KeyA"); g.key("KeyD", { shift: true }); g.mv(CX + 30, CY); g.key("KeyZ"); g.num("2"); g.enter();
  g.key("KeyD"); g.key("KeyD", { shift: true, ctrl: true });
});
scenario("複製: 編集したメッシュ・ミラー付き・回転・スケール付き", { selected: [0], objects: [objectOf("cube", "Cube", { p: [1, 1, 0], r: [10, 20, 130], s: [1, 2, 0.5] })] }, g => {
  g.tab(); g.selmode(3); g.clickFace(g.em.faces[1]); g.key("KeyE"); g.mv(CX, CY - 80); g.enter(); g.tab();
  g.tool("mirror"); g.key("KeyD", { shift: true }); g.mv(CX + 100, CY); g.enter();
  g.tool("mirror"); g.key("Home");
});
scenario("削除(オブジェクトモード): X メニュー・Delete キー・選択なし・編集中の削除・アクティブの付け替え", { selected: [], objects: [objectOf("cube", "A"), objectOf("cube", "B", { p: [3, 0, 0] }), objectOf("cube", "C", { p: [-3, 0, 0] })] }, g => {
  g.key("KeyX"); g.key("Delete"); g.click(CX, CY); g.key("Delete");
  g.undo(); g.key("KeyA"); g.key("KeyX"); g.menu(0); g.undo(); g.key("KeyA"); g.key("KeyX"); g.key("Delete");
  g.key("KeyA"); g.tab(); g.key("KeyA", { alt: true }); g.tab(); g.key("KeyA", { alt: true }); g.key("KeyX");
});
scenario("ミラー・統計・透過・N パネル(位置・回転・スケール・90° 超の回転・不正な値)", { selected: [0] }, g => {
  g.tool("mirror"); g.tool("mirror"); g.tool("stats"); g.tool("xray"); g.tool("vert"); g.key("KeyZ", { alt: true }); g.key("KeyZ", { alt: true });
  g.key("KeyN"); g.key("KeyN"); g.key("KeyN");
  for (const [prop, axis, value] of [["position", "x", "1.5"], ["position", "y", "-2"], ["rotation", "x", "45"], ["rotation", "y", "135"], ["rotation", "z", "200"], ["scale", "z", "0.25"], ["scale", "x", "abc"], ["position", "z", ""], ["rotation", "z", "-720"], ["rotation", "y", "89.9"], ["scale", "y", "1e1"], ["position", "x", "  3.5abc"]])
    g.do({ e: "nchange", prop, axis, value });
  g.key("KeyG"); g.mv(CX + 50, CY); g.enter(); g.key("KeyR"); g.mv(CX - 90, CY - 30); g.mv(CX + 70, CY + 80); g.enter();
  g.click(CX + 450, CY - 250); g.do({ e: "nchange", prop: "position", axis: "x", value: "9" });
  g.tab(); g.tool("stats"); g.tool("mirror"); g.tab(); g.tool("face"); g.selmode(2); g.tool("face");
  g.do({ e: "nchange", prop: "rotation", axis: "z", value: "12" }); g.tool("mirror");
});
scenario("統計の表示(編集モードと選択数・追加・削除)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tool("stats"); g.tab(); g.key("KeyA"); g.selmode(3); g.clickFace(g.em.faces[0]); g.key("KeyE"); g.enter(); g.key("KeyI"); g.num("0.1"); g.enter(); g.tab();
  g.key("KeyA", { shift: true }); g.menu(2); g.tool("stats"); g.key("KeyX"); g.menu(0); g.tool("stats");
});
scenario("Home・Numpad .(編集モードで選択した頂点・全体・面に属さない頂点を含む箱)・視点", { selected: [0], objects: [withMesh("plane", "Plane", "loose")] }, g => {
  g.key("Home"); g.key("NumpadDecimal"); g.tab(); g.key("Home"); g.key("NumpadDecimal");
  g.selmode(1); g.clickVert(9); g.key("NumpadDecimal"); g.key("Home"); g.key("KeyA"); g.key("NumpadDecimal");
  g.key("KeyA", { alt: true }); g.key("NumpadDecimal"); g.tab(); g.key("NumpadDecimal"); g.key("Home");
});

// 視点・テンキー(編集モードでの選択と隠れ判定)
scenario("軸にそろえた平行投影(前・右・上・後・左・下)での編集モードの選択と押し出し", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(1);
  for (const [k, ctrl] of [["1", false], ["3", false], ["7", false], ["1", true], ["3", true], ["7", true]]) {
    g.key("Numpad" + k, { ctrl }); g.key("KeyA", { alt: true });
    g.rect({ x: CX - 200, y: CY - 150 }, { x: CX + 200, y: CY + 150 });
    g.key("KeyA", { alt: true });
    g.key("KeyZ", { alt: true }); g.rect({ x: CX - 200, y: CY - 150 }, { x: CX + 200, y: CY + 150 }); g.key("KeyZ", { alt: true });
    g.selmode(2); g.key("KeyA", { alt: true }); g.rect({ x: CX - 60, y: CY - 60 }, { x: CX + 60, y: CY + 60 });
    g.selmode(3); g.key("KeyA", { alt: true }); g.rect({ x: CX - 60, y: CY - 60 }, { x: CX + 60, y: CY + 60 });
    g.key("KeyE"); g.mv(CX + 40, CY - 40); g.enter(); g.undo();
    g.selmode(1);
  }
  g.key("Numpad5"); g.key("Numpad5");
});
scenario("クリックで拾う半径: 頂点は 14 ピクセル・辺は 12 ピクセル(その内側と外側)", { selected: [0], objects: [withMesh("plane", "Plane", "plane")] }, g => {
  g.tab();
  const center = g.screen(new THREE.Vector3(0, 0, 0));
  for (const m of [1, 2]) {
    g.selmode(m);
    const items = m === 1 ? g.em.verts.map((_, i) => g.vertAt(i)) : g.edges().map(e => g.edgeMid(e));
    const radius = m === 1 ? 14 : 12;
    for (const p of items) {
      // メッシュの外側へ向かう向きに、半径の少し内側と少し外側の点をクリックする(ほかの頂点・辺は遠い)
      let dx = p.x - center.x, dy = p.y - center.y; const len = Math.hypot(dx, dy) || 1; dx /= len; dy /= len;
      for (const off of [radius - 0.6, radius + 0.6, radius - 0.1, radius + 0.1]) { g.click(p.x + dx * off, p.y + dy * off); g.key("KeyA", { alt: true }); }
    }
  }
});
scenario("Alt+クリックは 5 ピクセル動かすと視点の回転になる(4.5 は辺のループ選択のまま)", { selected: [0], objects: [withMesh("cube", "Cube", "grid3")] }, g => {
  g.tab(); g.selmode(2);
  for (const move of [4.5, 4.9, 5.1, 5.5]) {
    const e = g.edges()[7];
    const p = g.edgeMid(e);
    g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true }); g.mv(p.x + move, p.y); g.up(p.x + move, p.y, 0, { alt: true });
  }
});
scenario("隠れ判定の許容: カメラが近いと許容は最小の 0.002(重なった板の下の頂点は隠れている)", { selected: [0], objects: [withMesh("plane", "Plane", "layers")], options: { view: { dist: 0.6 } } }, g => {
  g.tab(); g.selmode(1);
  g.do({ e: "setview", view: { target: [0, 0, 0], dist: 0.6, theta: -Math.PI / 2, phi: 0.05, ortho: false, name: "ユーザー" } });
  // 中央の頂点(4)は上の板の真下にあり、0.0025 だけ手前の板に隠れている(許容は max(0.002, d × 0.002) で、d < 1 なので 0.002)。視点を傾けても同じ
  g.rect({ x: ROOT.left, y: ROOT.top }, { x: ROOT.left + ROOT.width, y: ROOT.top + ROOT.height });
  for (const i of [0, 1, 2, 3, 4, 5, 6, 7]) g.clickVert(i);
  for (const phi of [0.3, 0.8, 1.2]) {
    g.do({ e: "setview", view: { target: [0, 0, 0], dist: 0.6, theta: -Math.PI / 2, phi, ortho: false, name: "ユーザー" } });
    g.key("KeyA", { alt: true }); g.rect({ x: ROOT.left, y: ROOT.top }, { x: ROOT.left + ROOT.width, y: ROOT.top + ROOT.height });
  }
  g.selmode(3); g.key("KeyA", { alt: true }); g.rect({ x: ROOT.left, y: ROOT.top }, { x: ROOT.left + ROOT.width, y: ROOT.top + ROOT.height });
});
scenario("透視と平行投影でのクリック選択・隠れた頂点(透過のオン・オフ)", { selected: [0] }, g => {
  g.tab(); g.selmode(1);
  for (let v = 0; v < 8; v++) { g.clickVert(v); }
  g.key("KeyZ", { alt: true });
  for (let v = 0; v < 8; v++) { g.clickVert(v); }
  g.key("Numpad1"); g.key("KeyZ", { alt: true });
  for (let v = 0; v < 8; v++) { g.clickVert(v); }
  g.key("KeyZ", { alt: true });
  for (let v = 0; v < 8; v++) { g.clickVert(v); }
  g.selmode(2); for (const e of g.edges()) g.clickEdge(e);
  g.selmode(3); for (const f of g.em.faces) g.clickFace(f);
});
scenario("視点のドラッグ・ホイール・リサイズ・編集モードの中", { selected: [0] }, g => {
  g.tab(); g.selmode(3);
  g.drag(CX, CY, CX + 90, CY - 40, 1); g.drag(CX, CY, CX - 30, CY + 60, 1, { shift: true }); g.drag(CX, CY, CX, CY + 80, 1, { ctrl: true });
  g.wheel(120); g.wheel(-120); g.wheel(120);
  g.do({ e: "resize", w: 600, h: 400 }); g.clickFace(g.em.faces[1]); g.key("KeyE"); g.mv(CX, CY - 50); g.enter();
  g.do({ e: "resize", w: 1000, h: 300 }); g.click(CX, CY); g.key("NumpadDecimal"); g.key("Home");
  g.do({ e: "resize", w: 912, h: 529 });
  g.do({ e: "orbit", dTheta: 0.5, dPhi: -0.2 }); g.key("Numpad5");
  g.do({ e: "setview", view: { target: [1, 2, 0.5], dist: 7, theta: 1, phi: 1.2, ortho: false, name: "ユーザー" } });
});

// wheelNeedsFocus・canTransform・共同練習の口
scenario("wheelNeedsFocus: フォーカスが無いとホイールで拡大しない(ページのスクロールを優先)", { options: { wheelNeedsFocus: true }, selected: [0] }, g => {
  g.wheel(120); g.do({ e: "focus" }); g.wheel(120); g.do({ e: "blur" }); g.wheel(-120);
  g.click(CX, CY); g.wheel(120); g.do({ e: "blur" }); g.tool("xray"); g.wheel(120); g.do({ e: "blur" }); g.key("KeyA", { shift: true }); g.menu(1); g.wheel(120);
  g.tab(); g.key("KeyR", T); g.wheel(120); g.do({ e: "blur" }); g.wheel(120); g.esc();
});
scenario("canTransform: ほかの人が操作中のオブジェクトは動かせない", { options: { locked: ["Locked"] }, objects: [objectOf("cube", "Cube"), objectOf("cube", "Locked", { p: [3, 0, 0] })], selected: [1] }, g => {
  g.key("KeyG"); g.key("KeyR"); g.key("KeyS");
  g.click(CX, CY - 10); g.key("KeyG"); g.mv(CX + 30, CY); g.enter();
  g.click(CX + 250, CY - 10, { shift: true }); g.key("KeyG"); g.key("KeyA"); g.key("KeyG");
  g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyG"); g.mv(CX + 30, CY); g.enter();
  g.tab(); g.key("KeyD", { shift: true }); g.mv(CX + 60, CY); g.enter();
});
scenario("共同練習: applyRemote・loadRoomScene・roomState・setView・編集中の削除", { selected: [0] }, g => {
  g.do({ e: "room", upsert: [{ id: "zz000001", kind: "sphere", name: "Remote", p: [2, 1, 0], r: [0.1, 0.2, 0.3], s: [1, 1, 2] }] });
  g.click(CX + 120, CY + 10);
  g.do({ e: "room", upsert: [{ id: "zz000001", kind: "sphere", name: "Remote2", p: [-2, 1, 0], r: [0, 0, 1], s: [1, 1, 1] }], keepUndo: true });
  g.key("KeyG"); g.mv(CX + 60, CY); g.enter(); g.undo();
  g.tab(); g.do({ e: "room", delete: ["zz000001"] }); g.do({ e: "room", upsert: [{ id: "zz000002", kind: "torus", name: "T", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }] });
  g.tab(); g.do({ e: "roomload", objects: [{ id: "aa000001", kind: "cube", name: "Cube", p: [0, 0, 1], r: [0, 0, 0.5], s: [1, 1, 1] }, { id: "aa000002", kind: "cone", name: "Cone", p: [3, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }] });
  g.key("KeyA"); g.key("KeyG"); g.mv(CX + 20, CY); g.enter(); g.do({ e: "room", delete: ["aa000002", "nothere"] }); g.undo();
  g.do({ e: "reset" }); g.do({ e: "room", upsert: [], delete: [] });
});
scenario("リセット: 編集モード・メニュー・操作中・ループカット中のリセット", { selected: [0] }, g => {
  g.tab(); g.key("KeyX"); g.do({ e: "reset" });
  g.key("KeyG"); g.tab(); g.do({ e: "reset" });
  g.tab(); g.key("KeyR", T); g.do({ e: "reset" });
  g.tab(); g.selmode(3); g.key("KeyA"); g.key("KeyE"); g.do({ e: "reset" }); g.key("KeyA"); g.tool("stats"); g.do({ e: "reset" });
});

// ---------------------------------------------------------------- お題(練習 1)の 18 件を、画面の操作でこなす
function playthrough() {
  const g = new Gen("お題 18 件を通しでこなす(practice.js のイベントとお題の判定まで)", { practice: true, objects: [], selected: [] });
  const task = () => g.h.practice.taskIndex;
  // 1: 立方体をクリックして選択
  g.click(CX, CY - 12);
  // 2: G Z 2 Enter
  g.key("KeyG"); g.key("KeyZ"); g.digit(2); g.enter();
  // 3: R Z 45 Enter
  g.key("KeyR"); g.key("KeyZ"); g.num("45"); g.enter();
  // 4: S 2 Enter
  g.key("KeyS"); g.digit(2); g.enter();
  // 5: Shift+A → UV 球
  g.key("KeyA", { shift: true }); g.menu(2);
  // 6: 球を選んで G X 3 Enter
  g.key("KeyG"); g.key("KeyX"); g.digit(3); g.enter();
  // 7: 立方体を選んで Shift+D、Y 方向に 3 m
  const cube = g.vp.objects.find(o => o.userData.kind === "cube");
  const sp = g.screen(cube.position);
  g.click(sp.x, sp.y);
  g.key("KeyD", { shift: true }); g.key("KeyY"); g.digit(3); g.enter();
  // 8: Numpad 1(テンキーを模倣しないので Numpad キー)
  g.key("Numpad1");
  // 9: Numpad 5
  g.key("Numpad5");
  // 10: Ctrl+Z
  g.undo();
  // 11: 球を選んで X で削除
  const ball = g.vp.objects.find(o => o.userData.kind === "sphere");
  const bp = g.screen(ball.position);
  g.click(bp.x, bp.y);
  g.key("KeyX"); g.menu(0);
  // 12: 立方体を選んで Tab(編集モードへ)
  const c2 = g.vp.objects.find(o => o.userData.kind === "cube");
  const p2 = g.screen(c2.position);
  g.click(p2.x, p2.y);
  g.key("Numpad7");   // 上から見る(上面が見やすい)
  g.tab();
  // 13: 3 で面選択、上の面をクリック
  g.selmode(3);
  g.key("KeyA", { alt: true });
  const top = g.em.faces.find(f => g.em.faceNormal(f).z > 0.9);
  g.clickFace(top);
  // 14: E 1 Enter
  g.key("KeyE"); g.digit(1); g.enter();
  // 15: I 0.3 Enter
  g.key("KeyI"); g.num("0.3"); g.enter();
  // 16: Ctrl+R で側面にループカット
  g.key("Numpad1");
  g.key("KeyR", T);
  const side = g.edges().find(e => Math.abs(g.em.faceNormal(e.faces[0]).z) < 0.1 && Math.abs(e.a.co.z - e.b.co.z) > 0.4);
  const mid = g.edgeMid(side);
  g.mv(mid.x, mid.y); g.wheel(-120); g.click(mid.x, mid.y); g.enter();
  // 17: 2 で辺選択、辺を 1 本選び Ctrl+B
  g.selmode(2); g.key("KeyA", { alt: true });
  const edge = g.edges().find(e => e.faces.length === 2 && Math.abs(e.a.co.z - e.b.co.z) < 1e-6);
  g.clickEdge(edge); g.key("KeyB", T); g.mv(CX + 30, CY + 10); g.enter();
  // 18: Tab でオブジェクトモードに戻る
  g.tab();
  g.c.note = `お題の番号: ${task()} / 18`;
  scenarios.push((g.c.probes = probesFor(g), g.c));
  if (task() !== 18) throw new Error(`お題 18 件を通しでこなす手順が最後まで進みません(いま ${task()} 件目)`);
  if (g.dead) throw new Error(g.error);
}
playthrough();

// ---------------------------------------------------------------- 乱数で作った長い操作列(JS の状態を見ながら、見えている頂点・辺・面を狙って操作する)
function randomAct(g) {
  const vp = g.vp;
  const edit = g.edit;
  const n = g.em?.verts.length ?? 0;
  const t = r();
  if (!edit) {
    if (t < 0.18) { // クリックで選択(オブジェクトの位置あたり)
      const o = pick(vp.objects); if (o) { const p = g.screen(o.position); g.click(p.x + between(-8, 8), p.y + between(-8, 8), { shift: chance(0.2) }); } else g.click(CX, CY);
    } else if (t < 0.3) { g.key("Key" + pick(["G", "R", "S"])); if (vp.op) opTail(g); }
    else if (t < 0.45) { g.tab(); if (g.edit) { g.selmode(pick([1, 2, 3])); } }
    else if (t < 0.52) { if (vp.objects.length < 6) { g.key("KeyA", { shift: true }); g.menu(Math.floor(r() * 6)); } else g.undo(); }
    else if (t < 0.57) { if (vp.objects.length < 6) { g.key("KeyD", { shift: true }); if (vp.op) opTail(g, true); } else g.undo(); }
    else if (t < 0.62) { g.key("KeyX"); g.menu(0); }
    else if (t < 0.7) viewAct(g);
    else if (t < 0.76) { g.key("KeyA", { alt: chance(0.4) }); }
    else if (t < 0.8) { g.key("KeyI", T); }
    else if (t < 0.86) { chance(0.5) ? g.undo() : g.redo(); }
    else if (t < 0.9) { g.tool(pick(["xray", "stats", "mirror"])); }
    else if (t < 0.94) { g.key("KeyN"); g.do({ e: "nchange", prop: pick(["position", "rotation", "scale"]), axis: pick(["x", "y", "z"]), value: String(pick([1, -1, 0.5, 80, 135, 2])) }); }
    else g.mouseAroundCenter();
    return;
  }
  const em = g.em;
  if (t < 0.12) { // 頂点・辺・面をクリック
    const m = vp.selectMode;
    if (m === "vert" && n) g.clickVert(Math.floor(r() * n), { shift: chance(0.35) });
    else if (m === "edge") { const es = g.edges(); if (es.length) g.clickEdge(pick(es), { shift: chance(0.35) }); }
    else if (em.faces.length) g.clickFace(pick(em.faces), { shift: chance(0.35) });
  } else if (t < 0.17) { // ボックス選択
    const verts = Array.from({ length: Math.min(n, 3) }, () => g.vertAt(Math.floor(r() * n)));
    if (verts.length) g.boxAround(verts, between(2, 40), { shift: chance(0.3) });
  } else if (t < 0.21) { // Alt+クリックのループ/リング
    const es = g.edges();
    if (es.length) { const p = g.edgeMid(pick(es)); g.mv(p.x, p.y); g.down(p.x, p.y, 0, { alt: true, ctrl: chance(0.4), shift: chance(0.3) }); g.up(p.x + (chance(0.15) ? 8 : 0), p.y, 0, { alt: true }); }
  } else if (t < 0.25) g.selmode(pick([1, 2, 3]));
  else if (t < 0.29) { g.key("KeyA", { alt: chance(0.4) }); if (chance(0.3)) g.key("KeyI", T); }
  else if (t < 0.33) { // L / Ctrl+L / 拡大縮小
    const c = pick(["l", "ctrlL", "grow", "shrink"]);
    if (c === "l" && n) { const p = g.vertAt(Math.floor(r() * n)); g.mv(p.x + between(-4, 4), p.y + between(-4, 4)); g.key("KeyL"); }
    else if (c === "ctrlL") g.key("KeyL", T);
    else g.key(c === "grow" ? "NumpadAdd" : "NumpadSubtract", T);
  } else if (t < 0.43) { if (extrudeAmbiguous(g)) g.key("KeyA", { alt: true }); else { g.key("KeyE"); if (vp.op) opTail(g); } }
  else if (t < 0.5) { if (insetAmbiguous(g)) g.key("KeyA", { alt: true }); else { g.key("KeyI"); if (vp.op) opTail(g); } }
  else if (t < 0.56) { if (bevelAmbiguous(g)) g.key("KeyA", { alt: true }); else { g.key("KeyB", T); if (vp.op) opTail(g); } }
  else if (t < 0.62) loopCutAct(g);
  else if (t < 0.68) { g.key("Key" + pick(["G", "R", "S"])); if (vp.op) opTail(g); }
  else if (t < 0.73) { g.key(pick(["KeyX", "Delete"])); if (vp.menu) { chance(0.9) ? g.menu(Math.floor(r() * 4)) : g.esc(); } }
  else if (t < 0.77) { g.key("KeyM"); if (vp.menu) { g.menu(Math.floor(r() * 3)); } }
  else if (t < 0.81) { if (!fillAmbiguous(g)) g.key("KeyF"); }
  else if (t < 0.84) { if (vp.objects.length < 6) { g.key("KeyP"); if (vp.menu) g.menu(0); } else g.undo(); }
  else if (t < 0.89) { chance(0.6) ? g.undo() : g.redo(); }
  else if (t < 0.92) g.tab();
  else if (t < 0.96) viewAct(g);
  else if (t < 0.98) g.tool(pick(["xray", "stats", "vert", "edge", "face", "mirror"]));
  else g.key(pick(["KeyZ", "KeyN", "KeyB", "KeyK", "KeyH", "KeyQ", "KeyD"]), { alt: chance(0.3), shift: chance(0.3) });
  if (g.em && g.em.verts.length > 70 && edit) { g.undo(); g.undo(); }
}

/** F で面を張る向きが、角度の並べ替えの丸めの誤差で決まりそうか(角度がほとんど同じで、ぴったりは同じでない頂点がある・±π の境目に近い) */
function fillAmbiguous(g) {
  const em = g.em, sel = [...em.vsel];
  if (sel.length < 3) return false;
  const c = sel.reduce((a, v) => a.add(v.co), new THREE.Vector3()).divideScalar(sel.length);
  const n = new THREE.Vector3();
  for (let i = 0; i < sel.length; i++) n.add(sel[i].co.clone().sub(c).cross(sel[(i + 1) % sel.length].co.clone().sub(c)));
  if (n.lengthSq() < 1e-10) n.set(0, 0, 1);
  n.normalize();
  const u = sel[0].co.clone().sub(c).normalize(), w = n.clone().cross(u);
  const items = sel.map(v => { const p = v.co.clone().sub(c); return { v, a: Math.atan2(p.dot(w), p.dot(u)) }; }).sort((x, y) => x.a - y.a);
  if (items.some(x => !Number.isFinite(x.a) || Math.abs(x.a) > Math.PI - 1e-6)) return true;
  // 角度がほとんど同じ(ぴったり同じも含む)で、位置が違う頂点が並ぶと、atan2 の最後の桁で順番が変わる(同じ位置の頂点どうしは同じ入力なので問題ない)
  for (let i = 1; i < items.length; i++) { const d = items[i].a - items[i - 1].a; if (d < 1e-6 && !items[i].v.co.equals(items[i - 1].v.co)) return true; }
  return false;
}

/** E で面を押し出す向き(選んだ面の法線の和)が、ほとんど 0 になる(閉じたメッシュの全部の面など)と、向きが丸めの誤差で決まる */
function faceArea(f) {
  let x = 0, y = 0, z = 0;
  for (let i = 0; i < f.v.length; i++) { const a = f.v[i].co, b = f.v[(i + 1) % f.v.length].co; x += (a.y - b.y) * (a.z + b.z); y += (a.z - b.z) * (a.x + b.x); z += (a.x - b.x) * (a.y + b.y); }
  return Math.sqrt(x * x + y * y + z * z);
}
/** I(差し込み)は選んだ面の法線を使う。面積がほとんど 0 の面があると向きが丸めの誤差で決まる */
function insetAmbiguous(g) { const em = g.em; return !!em && [...em.fsel].some(f => faceArea(f) < 1e-4); }
/** Ctrl+B(ベベル)は辺の向き(正規化)を使う。ほとんど長さ 0 の辺があると向きが丸めの誤差で決まる */
function bevelAmbiguous(g) { const em = g.em; return !!em && em.edges().some(e => em.esel.has(e.key) && e.a.co.distanceTo(e.b.co) < 1e-4); }
function extrudeAmbiguous(g) {
  const em = g.em;
  if (!em || em.fsel.size === 0) return false;
  if ([...em.fsel].some(f => faceArea(f) < 1e-4)) return true;
  const sum = new THREE.Vector3();
  for (const f of em.fsel) sum.add(em.faceNormal(f));
  return sum.length() < 1e-3;
}

function viewAct(g) {
  const t = r();
  if (t < 0.4) g.key("Numpad" + pick(["1", "3", "7", "5", "Decimal"]), { ctrl: chance(0.3) });
  else if (t < 0.55) g.key("Home");
  else if (t < 0.7) g.wheel(pick([120, -120, 60]));
  else if (t < 0.9) { const x = CX + between(-150, 150), y = CY + between(-100, 100); g.drag(x, y, x + between(-120, 120), y + between(-80, 80), pick([1, 1, 0]), pick([{}, { shift: true }, T, { alt: true }])); }
  else g.do({ e: "orbit", dTheta: between(-1, 1), dPhi: between(-0.3, 0.3) });
}

/** 操作(G/R/S/I/B/E)が始まったあとの続き: マウス・軸・数値・刻み・確定/キャンセル */
function opTail(g, forceMouse = false) {
  const kind = g.vp.op?.kind;
  const moves = 1 + Math.floor(r() * 3);
  for (let i = 0; i < moves; i++) {
    if (!g.vp.op) return;
    const o = chance(0.2) ? T : chance(0.15) ? { shift: true } : {};
    g.mv(CX + between(-220, 220), CY + between(-150, 150), o);
    if (chance(0.15) && (kind === "G" || kind === "R" || kind === "S")) g.key("Key" + pick(["X", "Y", "Z"]), { shift: chance(0.3) });
    if (chance(0.12) && g.vp.op) g.wheel(pick([120, -120]));
  }
  if (!forceMouse && chance(0.4) && g.vp.op) {
    for (let i = 0; i < 1 + Math.floor(r() * 4); i++) {
      const k = pick(["digit", "digit", "digit", "period", "minus", "back"]);
      if (k === "digit") g.digit(kind === "S" ? pick([1, 2, 3, 4, 5, 6, 7, 8, 9]) : pick([0, 1, 2, 3, 4, 5, 6, 7, 8, 9])); else if (k === "period") g.key("Period"); else if (k === "minus") g.key("Minus"); else g.key("Backspace");
    }
  }
  if (g.vp.op) g.finish(pick(["enter", "enter", "enter", "click", "click", "esc", "rclick"]));
}

function loopCutAct(g) {
  g.mouseAroundCenter();
  g.key("KeyR", T);
  if (!g.vp.loopcut) return;
  const es = g.edges();
  if (es.length) { const p = g.edgeMid(pick(es)); g.mv(p.x + between(-3, 3), p.y + between(-3, 3)); }
  if (chance(0.5)) g.wheel(pick([120, -120]));
  if (chance(0.3)) g.mouseAroundCenter();
  if (!g.vp.loopcut) return;
  const how = pick(["click", "enter", "enter", "esc", "rclick"]);
  if (how === "click") g.click(...Object.values(g.vp.loopcut.edge ? g.edgeMid(g.vp.loopcut.edge) : { x: CX, y: CY }).slice(0, 2));
  else g.finish(how);
  if (g.vp.op) opTail(g);
}

function randomScenario(name, steps, objects, over = {}) {
  const g = new Gen(name, { objects, selected: [0], ...over });
  g.autoHeal = true;
  g.mouseAroundCenter();
  g.tab();
  if (chance(0.6)) g.selmode(pick([1, 2, 3]));
  let guard = 0;
  while (g.count < steps && !g.dead && guard++ < steps * 3) randomAct(g);
  if (g.vp.op) g.esc();
  scenarios.push((g.c.probes = probesFor(g), g.c));
  if (g.dead) caseNotes.push(`${name}: 打ち切り(${g.error}) ${g.count} 手順`);
  return g;
}

const SETS = [
  [withMesh("cube", "Cube", "cube")],
  [withMesh("cube", "Cube", "grid3")],
  [withMesh("plane", "Plane", "plane")],
  [withMesh("plane", "Plane", "grid2x3")],
  [withMesh("cylinder", "Cylinder", "cyl8")],
  [withMesh("cube", "Cube", "lshape")],
  [withMesh("sphere", "Sphere", "uvs")],
  [withMesh("cube", "Cube", "tetra")],
  [withMesh("plane", "Plane", "loose")],
  [withMesh("cone", "Cone", "cyl6")],
  [objectOf("cube", "Cube", { p: [0.5, -0.5, 0.2], r: [20, 35, 50], s: [1.2, 0.7, 1.5] })],
  [objectOf("cube", "Cube"), objectOf("sphere", "Sphere", { p: [3, 0, 0] })],
];
const randomViews = () => ({ ...DEFAULT_VIEW, theta: round(between(-Math.PI, Math.PI), 3), phi: round(between(0.4, 2.6), 3), dist: round(between(8, 16), 1), ortho: chance(0.25) });
for (let i = 0; i < 60; i++) {
  const set = SETS[i % SETS.length];
  randomScenario(`乱数(短) ${i}: ${set.map(o => o.mesh ? o.name + "(小)" : o.kind).join("+")}`, 120 + Math.floor(r() * 80), set, { view: randomViews(), emulate: chance(0.2) });
}
for (let i = 0; i < 14; i++) {
  const set = SETS[(i * 5) % SETS.length];
  randomScenario(`乱数(長) ${i}: ${set.map(o => o.mesh ? o.name + "(小)" : o.kind).join("+")}`, 800 + Math.floor(r() * 400), set, { view: randomViews() });
}

// 三角関数の 1 ulp の誤差に強い列だけを残す(practice-fragile.mjs)
makeRobust(scenarios);

const outFile = path.join(path.dirname(fileURLToPath(import.meta.url)), "practice-edit-cases.json");
writeFileSync(outFile, JSON.stringify({ scenarios }));
const total = scenarios.reduce((a, s) => a + s.steps.length, 0);
console.log(`scenarios ${scenarios.length}, steps ${total}, file ${(JSON.stringify({ scenarios }).length / 1024).toFixed(0)} KB`);
for (const n of caseNotes) console.log("  note:", n);
console.log(`JS が例外を投げた手順: ${jsErrors.length}`);
for (const e of jsErrors) console.log("  JS 例外:", e.where, "手順", e.step, JSON.stringify(e.input), e.message);
