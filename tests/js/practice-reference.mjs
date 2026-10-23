// 練習の操作ロジック(C# の FaraAvatarModdingGuide.Practice)の突き合わせ用の「正解」を、現行の JavaScript(wwwroot/js/viewport.js・practice.js・editmesh.js)を
// そのまま Node で動かして作る。SearchParityTests(検索)と同じ方式。 使い方: node practice-reference.mjs <cases.json> <out.json>
//
// Viewport は new Viewport(...) と、本物のコンストラクタごと動かす。描画(WebGL)・DOM・描画のループだけを practice-dom.mjs の代用品にする
// (S0 は一部のメソッドを空にしていたが、S1 の編集モードはメッシュの再構築・ジオメトリ・取り消し・統計・メニューまで本物を動かす)。
// ポインター・ホイール・キーは、bind() が root に登録する本物のハンドラーに、模擬のイベントを渡して動かす。
//   - three.js は wwwroot/lib/three/three.module.js(ブラウザでは importmap が指す同じファイル)
//   - practice.js の TASKS・Practice は、読み込みのフック(practice-hooks.mjs)が export を足して取り出す(配信するファイルは書き換えない)
// 入力(cases.json)は tests/js/practice-cases.json(S0。practice-cases.generate.mjs)と practice-edit-cases.json(S1。practice-edit-cases.generate.mjs)。C# 側も同じファイルを読む。
import { register } from "node:module";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";
import { installGlobals, makeRoot, seedRandomIds, pumpFrame, linesOf, El } from "./practice-dom.mjs";
import { withTrigNoise } from "./practice-noise.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(here, "../../src/FaraAvatarModdingGuide.Web/wwwroot");
register("./practice-hooks.mjs", import.meta.url, { data: { threeUrl: pathToFileURL(path.join(wwwroot, "lib/three/three.module.js")).href } });
installGlobals();

const THREE = await import("three");
const { Viewport } = await import(pathToFileURL(path.join(wwwroot, "js/viewport.js")).href);
const { EditMesh, SHAPES } = await import(pathToFileURL(path.join(wwwroot, "js/editmesh.js")).href);
const { TASKS, Practice } = await import(pathToFileURL(path.join(wwwroot, "js/practice.js")).href);

/** 基本の形(editmesh.js の SHAPES)の頂点・面・三角形(viewport.js の rebuild が作るジオメトリと同じ順) */
export function shapeData(kind) {
  const data = SHAPES[kind]();
  const em = EditMesh.fromData(data);
  const { pos } = em.triangles();
  return { v: data.v, f: data.f, tri: pos };
}

const num = v => (Object.is(v, -0) ? 0 : v);
const vec = v => [num(v.x), num(v.y), num(v.z)];
const LOCKED = "ほかの人が操作中です";

/** ケース 1 件のための Viewport(または Practice)を作る。部屋の ID は決まった列にする(seed) */
export function createHarness(c) {
  seedRandomIds(c.seed ?? 1);
  const root = makeRoot(c.root);
  document.activeElement = null;
  const o = c.options ?? {};
  let vp, practice = null;
  if (c.practice) {
    globalThis.__elements = {
      "practice-tasks": new El("ol"), "practice-tasks-done": new El("div"), "practice-emulate-numpad": { checked: !!c.emulate }, "practice-reset": new El("button"),
    };
    practice = new Practice(root);
    vp = practice.vp;
  } else {
    vp = new Viewport(root, {
      scene: c.objects.map(({ kind, name, p, r, s }) => ({ kind, name, p, r, s })),
      emulateNumpad: () => !!c.emulate,
      wheelNeedsFocus: !!o.wheelNeedsFocus,
      view: o.view,
      canTransform: o.locked ? sel => (sel.some(x => o.locked.includes(x.name)) ? LOCKED : null) : undefined,
    });
  }
  // メッシュの中身を差し替える(小さなメッシュで試すため)
  (c.objects ?? []).forEach((d, i) => {
    if (!d.mesh) return;
    const obj = vp.objects[i];
    obj.userData.em = EditMesh.fromData(d.mesh);
    vp.rebuild(obj);
  });
  if (c.view) {
    Object.assign(vp.view, { dist: c.view.dist, theta: c.view.theta, phi: c.view.phi, name: c.view.name });
    vp.view.target.fromArray(c.view.target);
    vp.ortho = !!c.view.ortho;
  }
  for (const idx of c.selected ?? []) { vp.selected.push(vp.objects[idx]); vp.active = vp.objects[idx]; }
  vp.refreshSelection();
  const events = [];
  vp.on("*", e => events.push(JSON.parse(JSON.stringify(e))));
  return { vp, root, practice, events, c, lastMesh: [] };
}

/** 面の Newell ベクトルの長さ(面積の 2 倍。editmesh.js の faceNormal の正規化前) */
function newellLength(face) {
  let x = 0, y = 0, z = 0;
  const vs = face.v;
  for (let i = 0; i < vs.length; i++) {
    const a = vs[i].co, b = vs[(i + 1) % vs.length].co;
    x += (a.y - b.y) * (a.z + b.z);
    y += (a.z - b.z) * (a.x + b.x);
    z += (a.x - b.x) * (a.y + b.y);
  }
  return Math.sqrt(x * x + y * y + z * z);
}

/** 面積がほとんど 0 の面の法線は、丸めの誤差で向きが決まる。突き合わせでは、面積が 1e-4 未満の面は法線を比べない(null にする) */
const NORMAL_MIN_AREA = 1e-4;

function meshOf(obj) {
  const d = obj.userData.em.toData();
  return { v: d.v.map(p => p.map(num)), f: d.f, e: d.e, vs: d.vs, fs: d.fs, es: d.es };
}

/** 1 手順のあとの状態(C# も同じ形を作って比べる) */
export function snapshot(h, extra = {}) {
  const { vp, root, practice, events } = h;
  pumpFrame(vp);
  h.lastMesh.length = Math.min(h.lastMesh.length, vp.objects.length);
  const cam = vp.camera;
  const op = vp.op;
  const lc = vp.loopcut;
  const editObj = vp.editObj;
  const tools = {};
  for (const b of root.parts.tools) tools[b.dataset.tool] = { on: b.classList.contains("is-on"), disabled: !!b.disabled };
  const menu = vp.menu ? { title: vp.menu.children[0].textContent, items: vp.menu.children.slice(1).map(b => b.textContent) } : null;
  const npanel = root.parts[".practice-npanel"];
  const info = vp.mode === "edit" ? vp.editInfo() : null;
  return {
    objects: vp.objects.map((o, i) => {
      const e = o.rotation;
      // メッシュの中身は、前の手順から変わったものだけ出す(出力が大きくなりすぎるため)。省かれたものは「前の手順と同じ」
      const mesh = meshOf(o);
      const key = JSON.stringify(mesh);
      const same = !h.fullMesh && h.lastMesh[i] === key;
      h.lastMesh[i] = key;
      const outline = o.getObjectByName("outline");
      return {
        name: o.name, kind: o.userData.kind, rid: o.userData.rid, p: vec(o.position), q: [num(o.quaternion.x), num(o.quaternion.y), num(o.quaternion.z), num(o.quaternion.w)], s: vec(o.scale),
        euler: [num(e.x), num(e.y), num(e.z)], mirror: !!o.userData.mirror, outline: { visible: outline.visible, active: outline.visible && outline.material.color.getHex() === 0xffb766 }, ...(same ? {} : { mesh }),
      };
    }),
    selected: vp.selected.map(o => o.name),
    active: vp.active?.name ?? null,
    mode: vp.mode, selectMode: vp.selectMode, editObj: editObj?.name ?? null, xray: vp.xray, showStats: vp.showStats,
    view: { target: vec(vp.view.target), dist: vp.view.dist, theta: vp.view.theta, phi: vp.view.phi, name: vp.view.name },
    ortho: vp.ortho,
    camera: {
      position: vec(cam.position), up: vec(cam.up),
      right: vec(new THREE.Vector3().setFromMatrixColumn(cam.matrixWorld, 0)), camUp: vec(new THREE.Vector3().setFromMatrixColumn(cam.matrixWorld, 1)),
      dir: vec(cam.getWorldDirection(new THREE.Vector3())),
      ortho: [vp.orthoCam.left, vp.orthoCam.right, vp.orthoCam.top, vp.orthoCam.bottom],
    },
    status: vp.hud.status.textContent,
    numericHud: vp.hud.numeric.textContent,
    viewHud: vp.hud.view.textContent,
    modeHud: vp.hud.mode.textContent,
    statsHidden: vp.hud.stats.hidden, statsLines: vp.hud.stats.hidden ? null : linesOf(vp.hud.stats),
    toolsEditHidden: vp.hud.toolsEdit.hidden, tools,
    npanel: { hidden: npanel.hidden, name: root.parts.name.textContent, rows: root.parts.inputs.map(i => ({ prop: i.dataset.prop, axis: i.dataset.axis, value: i.value, disabled: !!i.disabled })) },
    op: op ? {
      kind: op.kind, axis: op.axis, exclude: op.exclude, numeric: op.numeric, edit: op.edit, fromExtrude: op.fromExtrude ?? null, fromDuplicate: op.fromDuplicate ?? null,
      normal: op.normal ? vec(op.normal) : null, limit: op.limit ?? null, cuts: op.cuts ?? null,
    } : null,
    loopcut: lc ? {
      cuts: lc.cuts,
      edge: lc.edge ? [editObj.userData.em.verts.indexOf(lc.edge.a), editObj.userData.em.verts.indexOf(lc.edge.b)] : null,
      preview: lc.preview ? Array.from(lc.preview.geometry.attributes.position.array) : null,
    } : null,
    menu,
    axisLine: vp.axisLine.visible,
    undoDepth: vp.undoStack.length, redoDepth: vp.redoStack.length,
    focused: document.activeElement === root,
    editInfo: info ? { faces: info.faces.map((f, i) => ({ center: vec(f.center), normal: newellLength([...(editObj ?? vp.active).userData.em.fsel][i]) >= NORMAL_MIN_AREA ? vec(f.normal) : null, sides: f.sides })), stats: info.stats } : null,
    room: vp.roomState(),
    tasks: practice ? { index: practice.taskIndex, done: [...practice.taskDone], state: { ...practice.state } } : null,
    events: events.splice(0),
    ...extra,
  };
}

/** 1 手順を、本物のハンドラーに渡す。戻り値は preventDefault を呼んだか */
export function applyStep(h, s) {
  const { vp, root, practice } = h;
  pumpFrame(vp); // 実際のページは、描画のたびに(イベントの合間に)カメラを更新している
  const e = {
    preventDefault() { this.defaultPrevented = true; }, defaultPrevented: false, target: { closest: () => null }, pointerId: 1,
    clientX: s.x ?? 0, clientY: s.y ?? 0, shiftKey: !!s.shift, ctrlKey: !!s.ctrl, altKey: !!s.alt, metaKey: !!s.meta, button: s.button ?? 0,
  };
  switch (s.e) {
    case "move": root.handlers.pointermove(e); break;
    case "down": root.handlers.pointerdown(e); break;
    case "up": root.handlers.pointerup(e); break;
    case "wheel": e.deltaY = s.dy; root.handlers.wheel(e); break;
    case "key": root.handlers.keydown(Object.assign(e, { target: root, code: s.code, key: s.key ?? "" })); break;
    case "keyup": root.handlers.keyup(e); break;
    case "tool": vp.hud.tools.handlers.click({ target: { closest: () => ({ dataset: { tool: s.tool } }) } }); break;
    case "nchange": { const input = root.parts.inputs.find(i => i.dataset.prop === s.prop && i.dataset.axis === s.axis); input.value = s.value; input.handlers.change(); break; }
    case "menu": vp.menu?.children[1 + s.i]?.handlers.click(); break;
    case "blur": document.activeElement = null; break;
    case "focus": root.focus(); break;
    case "resize": root.rect.width = s.w; root.rect.height = s.h; root.clientWidth = s.w; root.clientHeight = s.h; vp.resize(); break;
    case "reset": if (practice) practice.reset(); else vp.resetScene(); break;
    case "room": vp.applyRemote({ upsert: s.upsert ?? [], delete: s.delete ?? [] }, { keepUndo: !!s.keepUndo }); break;
    case "roomload": vp.loadRoomScene(s.objects); break;
    case "setview": vp.setView(s.view); break;
    case "orbit": vp.orbitBy(s.dTheta, s.dPhi ?? 0); break;
    default: throw new Error("unknown step " + s.e);
  }
  return !!e.defaultPrevented;
}

/** 画面の点からのレイ・ビュー平面との交点・ピック・画面への投影・1 ピクセルの長さ・マウスの下の点を、現行のメソッドで出す */
export function probe(vp, p) {
  const e = { clientX: p.x, clientY: p.y };
  const ndc = vp.ndc(e);
  const ray = new THREE.Raycaster();
  ray.setFromCamera(ndc, vp.camera);
  const pivot = new THREE.Vector3().fromArray(p.pivot ?? [0, 0, 0]);
  const hit = vp.pick(e);
  const world = new THREE.Vector3().fromArray(p.world ?? [0, 0, 0]);
  const scr = vp.toScreen(world);
  const at = vp.pointAt(e);
  return {
    ndc: [ndc.x, ndc.y],
    origin: vec(ray.ray.origin), dir: vec(ray.ray.direction),
    onPlane: vec(vp.unprojectOnPlane(ndc, pivot)),
    pick: hit ? hit.name : null,
    screen: [scr.x, scr.y],
    wpp: vp.worldPerPixel(world),
    pointAt: at ? vec(at) : null,
  };
}

export function runScenario(c) {
  const h = createHarness(c);
  const out = [snapshot(h)];
  for (const s of c.steps) {
    const prevented = applyStep(h, s);
    out.push(snapshot(h, { prevented }));
  }
  // 直接呼べる補助の結果(レイ・平面・投影・フレーミング)
  const probes = (c.probes ?? []).map(p => probe(h.vp, p));
  return { steps: out, probes };
}

/** お題 18 件を、与えた状態(practice.js の checkTasks が作る ctx と同じ形)で評価する */
function runTasks(cases) {
  const mk = o => ({ userData: { kind: o.kind }, position: { x: o.p[0], y: o.p[1], z: o.p[2] }, rotation: { x: o.r[0], y: o.r[1], z: o.r[2] }, scale: { x: o.s[0], y: o.s[1], z: o.s[2] } });
  return cases.map(c => {
    const objects = c.objects.map(mk);
    const ctx = {
      objects, selected: c.selected.map(i => objects[i]), viewName: c.viewName, ortho: c.ortho, mode: c.mode, selectMode: c.selectMode,
      faces: c.faces.map(f => ({ normal: { x: f[0], y: f[1], z: f[2] } })), ...c.flags,
    };
    return TASKS.map(t => !!t.check(ctx));
  });
}

/** checkTasks(本物のメソッド)を、状態の列で動かす。順に達成が進む(先のお題が先に満たされていても、前が済むまで進まない) */
function runTaskSequences(seqs) {
  return seqs.map(seq => {
    const mk = o => ({ userData: { kind: o.kind }, position: { x: o.p[0], y: o.p[1], z: o.p[2] }, rotation: { x: o.r[0], y: o.r[1], z: o.r[2] }, scale: { x: o.s[0], y: o.s[1], z: o.s[2] } });
    const practice = Object.create(Practice.prototype);
    practice.state = { undoUsed: false, sphereAdded: false, extruded: false, inset: false, loopcut: false, bevel: false };
    practice.taskIndex = 0;
    practice.taskDone = TASKS.map(() => false);
    practice.renderTasks = () => { };
    return seq.map(c => {
      const objects = c.objects.map(mk);
      Object.assign(practice.state, c.flags);
      practice.vp = {
        objects, selected: c.selected.map(i => objects[i]), view: { name: c.viewName }, ortho: c.ortho, mode: c.mode, selectMode: c.selectMode,
        editInfo: () => ({ faces: c.faces.map(f => ({ normal: { x: f[0], y: f[1], z: f[2] } })) }),
      };
      practice.checkTasks();
      return { index: practice.taskIndex, done: [...practice.taskDone] };
    });
  });
}

export function run(cases) {
  return {
    taskTexts: TASKS.map(t => t.text),
    shapes: Object.fromEntries(Object.keys(SHAPES).map(k => [k, shapeData(k)])),
    tasks: runTasks(cases.tasks ?? []),
    taskSequences: runTaskSequences(cases.taskSequences ?? []),
    scenarios: (cases.scenarios ?? []).map(runScenario),
  };
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url && process.argv[2] && process.argv[3]) {
  const cases = JSON.parse(readFileSync(process.argv[2], "utf8"));
  // ローカルの確認用: PRACTICE_TRIG_NOISE=<シード>:<割合 %> で、三角関数を 1 ulp ずらした JS の結果を出す(C# と突き合わせて、環境による libm の違いに耐えるかを見る)
  const noise = /^(\d+):(\d+)$/.exec(process.env.PRACTICE_TRIG_NOISE ?? "");
  writeFileSync(process.argv[3], JSON.stringify(noise ? withTrigNoise(Number(noise[1]), Number(noise[2]), () => run(cases)) : run(cases)));
}
