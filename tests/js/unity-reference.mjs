// Unity 風の画面(C# の FaraAvatarModdingGuide.Practice の UnityEditorCore)の突き合わせ用の「正解」を、現行の JavaScript(wwwroot/js/unity.js・unity-practice.js)を
// そのまま Node で動かして作る(Phase 3b S5)。使い方: node unity-reference.mjs <cases.json> <out.json>
//
// UnityEditor は new UnityEditor(root, options) と、本物のコンストラクタごと動かす。描画(WebGL)・画面の大きさの監視・描画のループ・DOM だけを、practice-dom.mjs と
// この中の小さな代用品にする(三次元の部分(Object3D・レイ判定・attach・ギズモ)・状態・取り消し・集計は本物)。
//   ・DOM の innerHTML は、本物のコードが書いた文字を保存して、ここで読み取る(Hierarchy の行・Inspector の欄・ポップアップ・プレビューの文字)
//   ・setTimeout / performance.now は仮想の時計(advance で進める)。C# 側も同じ時計
//   ・ポインター・ホイール・キーは、bind() が登録した本物のハンドラーに、模擬のイベントを渡す
// 入力(cases.json)は tests/js/unity-cases.json(unity-cases.generate.mjs)。C# 側も同じファイルを読む。
import { register } from "node:module";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";
import { installGlobals, El } from "./practice-dom.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(here, "../../src/FaraAvatarModdingGuide.Web/wwwroot");
register("./practice-hooks.mjs", import.meta.url, { data: { threeUrl: pathToFileURL(path.join(wwwroot, "lib/three/three.module.js")).href } });
installGlobals();
globalThis.location = { search: "" };

const THREE = await import("three");
const { UnityEditor, ASSETS, COMPONENTS } = await import(pathToFileURL(path.join(wwwroot, "js/unity.js")).href);
const { TASKS, UnityPractice } = await import(pathToFileURL(path.join(wwwroot, "js/unity-practice.js")).href);
const { SCENARIOS } = await import(pathToFileURL(path.join(wwwroot, "js/scenarios.js")).href);

// ---------------------------------------------------------------- 仮想の時計
let now = 0;
let timers = [];
let nextTimer = 1;
Object.defineProperty(globalThis, "performance", { configurable: true, value: { now: () => now } });
globalThis.setTimeout = (fn, ms = 0) => { const id = nextTimer++; timers.push({ id, at: now + ms, fn }); return id; };
globalThis.clearTimeout = id => { timers = timers.filter(t => t.id !== id); };
function advance(ms) {
  const end = now + ms;
  for (;;) {
    const due = timers.filter(t => t.at <= end).sort((a, b) => a.at - b.at || a.id - b.id)[0];
    if (!due) break;
    timers = timers.filter(t => t !== due);
    now = Math.max(now, due.at);
    due.fn();
  }
  now = end;
}

// ---------------------------------------------------------------- DOM の代用品
class UEl extends El {
  constructor(tag = "div", init = {}) {
    super(tag, init);
    this.innerHTML = "";
    this.clientWidth = 0;
    this.clientHeight = 0;
    this.rect = { left: 0, top: 0, width: 0, height: 0 };
    this.offsetParent = {};
    this.stubs = {};
  }
  getBoundingClientRect() { return { ...this.rect, right: this.rect.left + this.rect.width, bottom: this.rect.top + this.rect.height }; }
  setPointerCapture() { }
  prepend(c) { c.parent = this; this.children.unshift(c); }
  querySelectorAll() { return []; }
  querySelector(sel) {
    if (sel === "[data-add]") {
      const m = /data-add="([^"]+)"/.exec(this.innerHTML);
      return m ? { dataset: { add: m[1] } } : null;
    }
    return (this.stubs[sel] ??= new UEl("div"));
  }
  contains(x) { return x === this; }
}

// スカイのテクスチャ(skyTexture)が canvas に描くので、描かずに受け取る代用品を返す
document.createElement = tag => {
  const e = new UEl(tag);
  e.getContext = () => ({ createLinearGradient: () => ({ addColorStop() { } }), fillRect() { }, fillStyle: null });
  return e;
};

const SELECTORS = [".ue-menubar", ".ue-hier-list", ".ue-scene-view", ".ue-insp-body", ".ue-proj-grid", ".ue-status", ".ue-popup", ".ue-sdk", ".ue-hl-label", ".ue-scene-info", ".ue-preview"];

/** 1 ケース分の UnityEditor を作る */
export function createHarness(c) {
  now = 0;
  timers = [];
  document.activeElement = null;
  const root = new UEl("div");
  root.isConnected = false;
  const parts = {};
  root.querySelector = sel => (SELECTORS.includes(sel) || sel.startsWith("[data-menu=") ? (parts[sel] ??= new UEl("div")) : null);
  root.querySelectorAll = () => [];
  root.contains = x => x === root;
  root.getBoundingClientRect = () => ({ left: 0, top: 0, width: 1000, height: 600, right: 1000, bottom: 600 });
  let ed, practice = null;
  if (c.practice) {
    // お題つき(unity-practice.js の UnityPractice。エディターはその中で作る)
    globalThis.__elements = { "unity-tasks": new UEl("ol"), "unity-tasks-done": new UEl("div"), "unity-reset": new UEl("button") };
    practice = new UnityPractice(root);
    ed = practice.ed;
  } else {
    ed = new UnityEditor(root, { scene: { instances: c.instances ?? ["Avatar"] }, wheelNeedsFocus: !!c.wheelNeedsFocus });
  }
  const h = { ed, root, practice, events: [], prevented: null, rect: null };
  ed.on("*", e => h.events.push(e));
  // 画面の大きさ
  setRect(h, c.rect ?? { left: 100, top: 50, width: 711.5, height: 400.25 }, c.client ?? { w: 711, h: 400 });
  h.frame = () => pumpAnimate(h);
  return h;
}

function setRect(h, rect, client) {
  const sv = h.ed.el.scene;
  sv.rect = { ...rect };
  sv.clientWidth = client.w;
  sv.clientHeight = client.h;
  h.ed.resize();
}

/** animate() を 1 回(requestAnimationFrame の保存した関数を呼ぶ) */
function pumpAnimate(h) {
  const fn = globalThis.__frames.get(h.ed.raf);
  if (!fn) return;
  globalThis.__frames.delete(h.ed.raf);
  fn(now);
}

const pe = (h, e) => ({ ...e, pointerId: 1, target: { closest: () => null }, preventDefault() { h.prevented = true; } });

const find = (h, p) => (p == null ? null : h.ed.findByName(p));
const idOf = (h, p) => find(h, p)?.id ?? null;

/** 操作 1 つ。存在しないノードの操作は、何もしない(C# も同じ) */
export function apply(h, op) {
  const ed = h.ed;
  h.events = [];
  h.prevented = null;
  const n = op.path != null ? find(h, op.path) : null;
  const need = op.path != null && !n;
  switch (op.k) {
    case "select": if (!need) ed.select(n.id); break;
    case "selectNone": ed.select(null); break;
    case "expand": if (!need) ed.setExpanded(n.id, op.on); break;
    case "instantiate": {
      const parent = op.parent != null ? find(h, op.parent) : null;
      if (op.parent != null && !parent) break;
      ed.instantiate(op.asset, parent?.id ?? null);
      break;
    }
    case "reparent": {
      const parent = op.parent != null ? find(h, op.parent) : null;
      if (need || (op.parent != null && !parent)) break;
      ed.reparent(n.id, parent?.id ?? null);
      break;
    }
    case "delete": if (!need) ed.deleteNode(n.id); break;
    case "active": if (!need) ed.setActive(n.id, op.on); break;
    case "tag": if (!need) ed.setTag(n.id, op.tag); break;
    case "material": if (!need) ed.assignMaterial(n.id, op.slot, op.material); break;
    case "addComponent": if (!need) ed.addComponent(n.id, op.type); break;
    case "setupOutfit": if (!need) ed.setupOutfit(n.id); break;
    case "tool": ed.setTool(op.tool); break;
    case "edit": if (!need) ed.setTransformValue(n.id, op.prop, op.axis, op.value, !!op.fromInput); break;
    case "frameSel": ed.frameSelected(); break;
    case "orbit": ed.orbitBy(op.dyaw, op.dpitch ?? 0); break;
    case "openSdk": ed.openSDK(); break;
    case "closeSdk": ed.closeSDK(!!op.quiet); break;
    case "build": ed.build(op.kind); break;
    case "advance": advance(op.ms); break;
    case "undo": ed.undo(); break;
    case "reset": if (h.practice) h.practice.reset(); else ed.resetScene(); break;
    case "key": ed.root.handlers.keydown(pe(h, { code: op.code, key: op.key ?? "", shiftKey: !!op.shift, ctrlKey: !!op.ctrl, metaKey: !!op.meta, altKey: !!op.alt, target: { matches: () => false, closest: () => null } })); break;
    case "ctxByName": ed.contextMenuByName(op.path); break;
    case "ctxItem":
      if (ed.el.popup.hidden || !/data-ctx="setup-outfit"/.test(ed.el.popup.innerHTML)) break;   // 右クリックのメニューが開いているときだけ(開いていない所のボタンは押せない)
      if (op.item === "setup-outfit") ed.setupOutfit(ed.ctxNode);
      else if (op.item === "delete") { ed.closePopup(); ed.deleteNode(ed.ctxNode); }
      break;
    case "openAdd": ed.el.popup.querySelector(".ue-addsearch").value = ""; ed.openAddComponent(); break;
    case "addSearch": {
      const input = ed.el.popup.querySelector(".ue-addsearch");
      input.value = op.text;
      input.handlers.input?.();
      break;
    }
    case "addEnter": if (ed.el.popup.hidden || !/ue-addmenu/.test(ed.el.popup.innerHTML)) break; ed.el.popup.querySelector(".ue-addsearch").handlers.keydown?.({ key: "Enter", stopPropagation() { } }); break;
    case "addEscape": if (ed.el.popup.hidden || !/ue-addmenu/.test(ed.el.popup.innerHTML)) break; ed.el.popup.querySelector(".ue-addsearch").handlers.keydown?.({ key: "Escape", stopPropagation() { } }); break;
    case "openMenu": ed.openMenu(op.name, 10, 20); break;
    case "closePopup": ed.closePopup(); break;
    case "dropAsset": {
      const t = op.target;
      const target = t.kind === "node" ? { kind: "node", id: idOf(h, t.path) } : t.kind === "slot" ? { kind: "slot", slot: t.slot } : { kind: t.kind };
      if (t.kind === "node" && !target.id) break;
      const r = ed.el.scene.rect;
      ed.drop({ kind: "asset", asset: op.asset, label: op.asset }, target, { clientX: r.left + (op.x ?? 0), clientY: r.top + (op.y ?? 0) });
      break;
    }
    case "dropNode": {
      if (need) break;
      const t = op.target;
      const target = t.kind === "node" ? { kind: "node", id: idOf(h, t.path) } : { kind: t.kind };
      if (t.kind === "node" && !target.id) break;
      ed.drop({ kind: "node", id: n.id, label: n.name }, target, {});
      break;
    }
    case "clearSelection": ed.selected = null; ed.render(); ed.updateGizmo(); break;
    case "dblclick": if (!need) { ed.select(n.id); ed.frameSelected(); } break;
    case "clickAsset": ed.setStatus(`${ASSETS[op.asset].label}: ${ASSETS[op.asset].note ?? ASSETS[op.asset].shader ?? ""}（ドラッグして使う）`); break;
    case "sceneDown": { const r = ed.el.scene.rect; ed.el.scene.handlers.pointerdown(pe(h, { clientX: r.left + op.x, clientY: r.top + op.y, button: op.button ?? 0, altKey: !!op.alt })); break; }
    case "sceneMove": { const r = ed.el.scene.rect; ed.el.scene.handlers.pointermove(pe(h, { clientX: r.left + op.x, clientY: r.top + op.y, button: 0 })); break; }
    case "sceneUp": { const r = ed.el.scene.rect; ed.el.scene.handlers.pointerup(pe(h, { clientX: r.left + op.x, clientY: r.top + op.y, button: op.button ?? 0 })); break; }
    case "wheel": {
      document.activeElement = op.focused ? ed.root : null;
      let prevented = false;
      ed.el.scene.handlers.wheel({ deltaY: op.deltaY, preventDefault() { prevented = true; } });
      h.prevented = prevented;
      break;
    }
    case "viewDistMul": ed.view.dist *= op.f; break;
    case "viewTargetX": ed.view.target.x += op.dx; break;
    case "frame": advance(op.dt ?? 16); h.frame(); break;
    case "resize": setRect(h, op.rect, op.client); break;
    case "pick": {
      const hd = ed.pickHandle(fakeEvent(h, op.x, op.y));
      h.probe = { mesh: nameOf(ed, pickMeshAt(h, op.x, op.y)), handle: hd ? [hd.kind, hd.axis] : null };
      break;
    }
    default: throw new Error(`未知の操作 ${op.k}`);
  }
}

const fakeEvent = (h, x, y) => { const r = h.ed.el.scene.rect; return { clientX: r.left + x, clientY: r.top + y }; };
const pickMeshAt = (h, x, y) => h.ed.pickMesh(fakeEvent(h, x, y));
const nameOf = (ed, id) => (id ? pathOf(ed, ed.get(id)) : null);

// ---------------------------------------------------------------- 状態の読み取り
const num = v => (Object.is(v, -0) ? 0 : v);
const pathOf = (ed, n) => (n ? [...ed.ancestors(n).reverse().map(a => a.name), n.name].join("/") : null);
const mat = m => Array.from(m.elements).map(num);
const vec = v => [num(v.x), num(v.y), num(v.z)];
const unesc = s => s.replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, "&");

function compSnap(ed, c) {
  const out = { type: c.type };
  if (c.materials) out.materials = [...c.materials];
  if (c.blendshapes) out.blendshapes = Object.keys(c.blendshapes);
  if (c.tris !== undefined) out.tris = c.tris;
  if (c.avatar !== undefined) out.avatar = c.avatar;
  if (c.view) out.view = [...c.view];
  if (c.blueprintId !== undefined) out.blueprintId = c.blueprintId;
  if (c.target !== undefined) out.target = c.target;
  if (c.targetId !== undefined) out.targetPath = pathOf(ed, ed.get(c.targetId));
  if (c.prefix !== undefined) out.prefix = c.prefix;
  if (c.suffix !== undefined) out.suffix = c.suffix;
  if (c.bindings) out.bindings = c.bindings.map(b => [b.from, b.blendshape]);
  if (c.root !== undefined) out.root = c.root;
  if (c.pull !== undefined) out.pull = c.pull;
  if (c.spring !== undefined) out.spring = c.spring;
  return out;
}

function parseRows(html) {
  const rows = [];
  for (const chunk of html.split('<div class="ue-row').slice(1)) {
    const cls = /^([^"]*)"/.exec(chunk)[1];
    rows.push({
      id: /data-node="([^"]*)"/.exec(chunk)[1],
      name: unesc(/data-node-name="([^"]*)"/.exec(chunk)[1]),
      depth: ((+/padding-left:(\d+)px/.exec(chunk)[1]) - 4) / 14,
      hasKids: /aria-expanded/.test(chunk),
      expanded: /aria-expanded="true"/.test(chunk),
      selected: /is-selected/.test(cls),
      inactive: /is-inactive/.test(cls),
      prefab: /is-prefab/.test(cls),
      icon: /ue-row-icon/.test(chunk) ? /<i class="bi ([^ "]*) ue-row-icon/.exec(chunk)?.[1] ?? null : null,
      editorOnly: /ue-row-tag/.test(chunk),
    });
  }
  return rows;
}

function parseInspector(html) {
  if (!html || /ue-insp-empty/.test(html)) return null;
  const out = {};
  out.active = /class="ue-active"[^>]*checked|checked[^>]*class="ue-active"|ue-active" checked/.test(html);
  out.name = unesc(/class="ue-name" value="([^"]*)"/.exec(html)[1]);
  out.tag = unesc(/<option selected>([^<]*)<\/option>/.exec(html)?.[1] ?? "");
  const inputs = [...html.matchAll(/data-field="(p|r|s)" data-axis="(\d)"/g)].map(m => m[1] + m[2]);
  out.values = [...html.matchAll(/<input type="number" step="[^"]*" value="([^"]*)"/g)].map(m => m[1]);
  out.inputs = inputs;
  out.comps = [...html.matchAll(/<div class="ue-comp" data-comp="([^"]+)">/g)].map(m => m[1]);
  out.fields = [...html.matchAll(/<span class="ue-field-label">([^<]*)<\/span>(?:\s*<span class="(ue-muted|ue-objfield)">(.*?)<\/span>)?/g)].map(m => [unesc(m[1]), m[2] ?? null, m[3] !== undefined ? unesc(m[3].replace(/<[^>]*>/g, "").trim()) : null]);
  out.notes = [...html.matchAll(/<div class="ue-muted">([^<]*)<\/div>/g)].map(m => unesc(m[1]));
  out.slots = [...html.matchAll(/data-slot="(\d+)" data-hl="([^"]*)"/g)].map(m => [+m[1], m[2]]);
  out.slotColors = [...html.matchAll(/<i class="bi bi-circle-fill" style="color:(#[0-9a-f]{6})"/g)].map(m => m[1]);
  return out;
}

function popupSnap(ed) {
  const p = ed.el.popup;
  if (p.hidden) return null;
  const html = p.innerHTML;
  if (/ue-addmenu/.test(html)) return { kind: "add", list: [...p.querySelector(".ue-addlist").innerHTML.matchAll(/data-add="([^"]+)"/g)].map(m => m[1]), search: p.querySelector(".ue-addsearch").value ?? "" };
  if (/data-ctx="setup-outfit"/.test(html)) return { kind: "context", title: unesc(/<div class="ue-menu-title">([^<]*)<\/div>/.exec(html)[1]), node: nameOf(ed, ed.ctxNode) };
  if (/data-menuitem="sdk-panel"/.test(html)) return { kind: "menu", menu: "VRChat SDK" };
  return { kind: "menu", menu: "other" };
}

function previewSnap(ed) {
  const p = ed.el.preview;
  if (p.hidden) return null;
  const m = /<div>([^<]*)<\/div><\/div>$/.exec(p.innerHTML);
  return { text: m ? unesc(m[1]) : null, busy: /ue-progress/.test(p.innerHTML) };
}

function objSnap(o) {
  return { position: vec(o.position), quaternion: [o.quaternion.x, o.quaternion.y, o.quaternion.z, o.quaternion.w].map(num), scale: vec(o.scale), world: mat(o.matrixWorld) };
}

function statsSnap(ed) {
  const st = ed.stats();
  if (!st) return null;
  return {
    avatar: st.avatar.name, values: st.values, ranks: st.ranks, overall: st.overall,
    notes: st.notes.map(n => [n.level, n.text]), merged: [...st.merged].map(a => pathOf(ed, a)),
  };
}

export function snapshot(h, full = true) {
  const ed = h.ed;
  // Inspector の欄は、入力欄からの編集(fromInput)のあとは作り直さない(ブラウザの入力欄は、打った文字をそのまま見せている)。比べる前に、最新の状態で書き直す
  ed.renderInspector();
  ed.renderHierarchy();
  const nodes = [...ed.nodes.values()].map(n => ({
    path: pathOf(ed, n), active: n.active, tag: n.tag, expanded: n.expanded, p: n.p.map(num), r: n.r.map(num), s: n.s.map(num),
    prefab: n.prefab ?? null, mesh: n.mesh ?? null, icon: n.icon ?? null, bone: !!n.bone, children: n.children.map(id => ed.get(id).name),
    comps: n.components.map(c => compSnap(ed, c)),
    ...(full ? { obj: n.obj ? objSnap(n.obj) : null } : {}),
    parts: (n.parts ?? []).map(p => ({ geometry: p.geometry.type, vertices: p.geometry.attributes.position.count, indices: p.geometry.index.count, visible: p.visible, color: p.material.color.getHex(), doubleSided: p.material.side === THREE.DoubleSide, ...(full ? { world: mat(p.matrixWorld) } : {}) })),
  }));
  const gizmo = {
    visible: ed.gizmo.visible, ...(full ? { position: vec(ed.gizmo.position), scale: vec(ed.gizmo.scale) } : {}),
    items: [], handles: ed.handles.map(x => ({ handle: x.userData.handle, ...(full ? { world: mat(x.matrixWorld) } : {}) })),
  };
  ed.gizmo.traverse(o => { if (o.isMesh) gizmo.items.push({ geometry: o.geometry.type, vertices: o.geometry.attributes.position.count, indices: o.geometry.index.count, visible: o.material.visible, color: o.material.color.getHex(), ...(full ? { world: mat(o.matrixWorld) } : {}) }); });
  return {
    nodes,
    roots: ed.roots.map(id => ed.get(id).name),
    selected: pathOf(ed, ed.get(ed.selected)),
    tool: ed.tool,
    status: ed.el.status.textContent,
    info: ed.el.info.textContent,
    popup: popupSnap(ed),
    sdkOpen: !ed.el.sdk.hidden,
    preview: previewSnap(ed),
    posing: !!ed.pose,
    undo: ed.undoStack.length,
    view: { target: vec(ed.view.target), dist: ed.view.dist, yaw: ed.view.yaw, pitch: ed.view.pitch },
    ...(full ? { camera: { position: vec(ed.camera.position), world: mat(ed.camera.matrixWorld), projection: mat(ed.camera.projectionMatrix) } } : {}),
    rows: parseRows(ed.el.hier.innerHTML).map(r => ({ ...r, id: undefined })),
    inspector: parseInspector(ed.el.insp.innerHTML),
    stats: statsSnap(ed),
    ...(h.practice ? { tasks: { index: h.practice.index, sdk: h.practice.state.sdk, builtMerged: h.practice.state.builtMerged } } : {}),
    gizmo,
  };
}

/** イベント(ID は比べない: 名前・親の名前で足りる) */
function cleanEvent(e) {
  const out = {};
  for (const [k, v] of Object.entries(e)) {
    if (k === "id" || k === "parent") continue;
    out[k] = typeof v === "number" ? num(v) : v ?? null;
  }
  return out;
}

export function runCases(cases) {
  return cases.map(c => {
    const h = createHarness(c);
    const steps = [];
    const light = c.detail === "light";
    steps.push({ op: { k: "init" }, snapshot: snapshot(h, true), events: [] });
    for (const op of c.ops) {
      h.probe = null;
      apply(h, op);
      steps.push({ op, snapshot: snapshot(h, !light || op.full === true), events: h.events.map(cleanEvent), prevented: h.prevented === true ? true : null, probe: h.probe });
    }
    return { name: c.name, steps };
  });
}

/** 3D の部品・ギズモのジオメトリ(位置・法線・インデックス)。Avatar と Hoodie を置いた画面の、ノードの順・部品の順で出す */
const b64 = arr => Buffer.from(new Float32Array(arr).buffer).toString("base64");
const geo = g => ({ type: g.type, positions: b64(g.attributes.position.array), normals: b64(g.attributes.normal.array), indices: Array.from(g.index.array) });
export function geometries() {
  const h = createHarness({ name: "geometry", instances: ["Avatar", "Hoodie"], ops: [] });
  const ed = h.ed;
  const parts = [];
  for (const n of ed.nodes.values()) for (const p of n.parts ?? []) parts.push({ node: n.name, ...geo(p.geometry) });
  const gizmo = {};
  ed.select(ed.findByName("Avatar").id);
  for (const tool of ["move", "rotate", "scale"]) {
    ed.setTool(tool);
    const items = [];
    ed.gizmo.traverse(o => { if (o.isMesh) items.push(geo(o.geometry)); });
    gizmo[tool] = items;
  }
  return { parts, gizmo };
}

/** お題(unity-practice.js の TASKS)の文章と、チュートリアル(scenarios.js の Unity 2 本)の手順の定義 */
export function definitions() {
  const scenarios = {};
  for (const [name, s] of Object.entries(SCENARIOS)) {
    if (s.engine !== "unity") continue;
    scenarios[name] = {
      title: s.title, instances: s.scene?.instances ?? null,
      steps: s.steps.map(st => ({
        group: st.group ?? null, text: st.text, keys: st.keys ?? [], mouse: st.mouse ?? null, highlight: st.highlight ?? null,
        hint: !!st.hint, fail: !!st.fail, skip: !!st.skip,
      })),
    };
  }
  return { taskTexts: TASKS.map(t => t.text), scenarios, assets: Object.entries(ASSETS).map(([key, a]) => ({ key, ...a, color: a.color ?? null, hidden: !!a.hidden })), components: COMPONENTS };
}

// ---------------------------------------------------------------- チュートリアル(scenarios.js の Unity 2 本)
/** tutorial.js の replayTo と同じ */
function replayTo(h, scenario, i) {
  const ed = h.ed;
  ed.silent = true;
  ed.resetScene(scenario.scene);
  for (let k = 0; k < i && k < scenario.steps.length; k++) scenario.steps[k].skip?.(ed);
  ed.silent = false;
  ed.setStatus("案内にしたがって操作してください");
}

/** done / hint / fail に渡すイベントの一覧(実際のエディターが出すものの形。名前・親の名前・値のバリエーション) */
export function buildEvents() {
  const ev = [];
  const add = (type, detail = {}) => ev.push({ type, ...detail });
  const names = ["Avatar", "Body", "Hoodie", "Hoodie_Mesh", "Shirt_Default", "Armature", null];
  for (const name of names) for (const expanded of [true, false]) add("expand", { id: "n1", name: name ?? "Avatar", expanded });
  for (const source of ["hierarchy", "scene", "undo"]) for (const name of names) add("select", { id: name ? "n1" : null, name, source });
  for (const action of ["frame", "orbit", "zoom", "zoom", "pan", "zoom"]) add("view", { action });
  for (const tool of ["move", "rotate", "scale", "hand", "rect"]) add("tool", { tool });
  for (const kind of ["move", "rotate", "scale"]) for (const axis of ["x", "y", "z", "all"]) add("transform", { id: "n1", name: "Avatar", kind, axis, final: true });
  for (const [prop, axis, value] of [["p", 0, 0], ["p", 0, 0.5], ["p", 1, 0], ["r", 0, 0], ["s", 0, 0], ["p", 0, -0.5], ["p", 2, 0]]) add("edit", { id: "n1", name: "Avatar", prop, axis, value });
  for (const parentName of [null, "Avatar", "Body", "Hoodie", "Armature"]) for (const asset of ["Hoodie", "Avatar", "Hoodie_lilToon"]) add("instantiate", { asset, id: "n2", name: asset, parent: parentName ? "n1" : null, parentName });
  for (const parentName of [null, "Avatar", "Body"]) for (const name of ["Hoodie", "Body", "Armature"]) add("reparent", { id: "n2", name, parent: "n1", parentName });
  for (const name of names) add("contextmenu", { id: "n1", name: name ?? "Avatar" });
  add("setupOutfit", { id: "n2", name: "Hoodie", ok: true });
  for (const reason of ["Armature がありません", "もう設定済みです", "衣装を選んでください"]) add("setupOutfit", { id: "n2", name: "Hoodie", ok: false, reason });
  for (const name of ["Shirt_Default", "Body", "Hoodie"]) for (const active of [true, false]) add("active", { id: "n3", name, active });
  for (const name of ["Shirt_Default", "Body"]) for (const tag of ["EditorOnly", "Untagged", "Player"]) add("tag", { id: "n3", name, tag });
  for (const name of ["Hoodie_Mesh", "Body"]) for (const material of ["Hoodie_lilToon", "Hoodie_Red", "Skin", "Hoodie_Default", "Red", "lilToonRed", "Blue"]) add("material", { id: "n4", name, slot: 0, material });
  for (const component of ["MABlendshapeSync", "MAMergeArmature", "VRCPhysBone", "MAMenuInstaller"]) for (const name of ["Hoodie_Mesh", "Body"]) add("componentAdded", { id: "n4", name, component });
  for (const menu of ["VRChat SDK", "add-component", "Window", "File", "Tools"]) add("menu", { menu });
  for (const open of [true, false]) add("sdk", { open });
  for (const kind of ["test", "upload", "build"]) add("build", { kind, merged: kind === "test", rank: "Good" });
  add("undo");
  for (const key of ["w", "e", "r", "q", "f", "Alt", "Escape"]) add("key", { code: key.length === 1 ? "Key" + key.toUpperCase() : key, key, shift: false, ctrl: false, alt: key === "Alt" });
  add("delete", { name: "Hoodie" });
  return ev;
}

export function tutorials() {
  const events = buildEvents();
  const out = { events, scenarios: {} };
  for (const [name, scenario] of Object.entries(SCENARIOS)) {
    if (scenario.engine !== "unity") continue;
    const h = createHarness({ name, instances: scenario.scene?.instances ?? ["Avatar"], wheelNeedsFocus: true });
    const n = scenario.steps.length;
    const replays = [], answers = [];
    for (let i = 0; i <= n; i++) {
      replayTo(h, scenario, i);
      replays.push(snapshot(h, false));
    }
    for (let i = 0; i < n; i++) {
      replayTo(h, scenario, i);
      const step = scenario.steps[i];
      const ctx = {};
      const row = [];
      for (const e of events) {
        const done = !!step.done(e, h.ed, ctx);
        const fail = step.fail?.(e, h.ed, ctx) || null;
        const hint = step.hint?.(e, h.ed, ctx) || null;
        row.push([done, fail, hint]);
      }
      answers.push(row);
    }
    out.scenarios[name] = { replays, answers };
  }
  return out;
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const [casesFile, outFile] = process.argv.slice(2);
  const cases = JSON.parse(readFileSync(casesFile, "utf8"));
  writeFileSync(outFile, JSON.stringify({ cases: runCases(cases), definitions: definitions(), geometries: geometries(), tutorials: tutorials() }));
}
