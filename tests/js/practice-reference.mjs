// 練習の操作ロジック(C# の FaraAvatarModdingGuide.Practice)の突き合わせ用の「正解」を、現行の JavaScript(wwwroot/js/viewport.js・practice.js・editmesh.js)を
// そのまま Node で動かして作る。SearchParityTests(検索)と同じ方式。 使い方: node practice-reference.mjs <cases.json> <out.json>
//
// viewport.js の Viewport は WebGL の描画器と DOM が要るので new しない。Object.create(Viewport.prototype) で本物のメソッドを借り、
// 描画だけに関わるもの(輪郭・パネル・メニュー・取り消しの記録)だけを空の代用品にする。ポインター・ホイール・キーは、bind() が root に登録する
// 本物のハンドラーに、模擬のイベントを渡して動かす(つまりクリックの選択・ドラッグの視点操作・G/R/S・数値入力・テンキーの視点は現行のコードそのまま)。
//   - three.js は wwwroot/lib/three/three.module.js(ブラウザでは importmap が指す同じファイル)
//   - practice.js の TASKS は、読み込みのフック(practice-hooks.mjs)が export を足して取り出す(配信するファイルは書き換えない)
// 入力(cases.json)は tests/js/practice-cases.json(practice-cases.generate.mjs が作る固定の入力)。C# 側も同じファイルを読む。
import { register } from "node:module";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(here, "../../src/FaraAvatarModdingGuide.Web/wwwroot");
register("./practice-hooks.mjs", import.meta.url, { data: { threeUrl: pathToFileURL(path.join(wwwroot, "lib/three/three.module.js")).href } });

const THREE = await import("three");
const { Viewport } = await import(pathToFileURL(path.join(wwwroot, "js/viewport.js")).href);
const { EditMesh, SHAPES } = await import(pathToFileURL(path.join(wwwroot, "js/editmesh.js")).href);
const { TASKS, Practice } = await import(pathToFileURL(path.join(wwwroot, "js/practice.js")).href);

const DEG = Math.PI / 180;

// onDrag の箱選択が DOM の要素を作る。見えないダミーを返す
globalThis.document = { createElement: () => ({ style: {}, className: "", remove() { } }), activeElement: null };

const text = () => ({ textContent: "", hidden: false, classList: { toggle() { } } });

/** 基本の形(editmesh.js の SHAPES)の頂点・面・三角形(viewport.js の rebuild が作るジオメトリと同じ順) */
export function shapeData(kind) {
  const data = SHAPES[kind]();
  const em = EditMesh.fromData(data);
  const { pos } = em.triangles();
  return { v: data.v, f: data.f, tri: pos };
}

function makeMesh(kind, d) {
  const em = EditMesh.fromData(SHAPES[kind]());
  const { pos } = em.triangles();
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.Float32BufferAttribute(pos, 3));
  g.computeBoundingSphere();
  g.computeBoundingBox();
  const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ side: THREE.DoubleSide }));
  mesh.name = d.name ?? kind;
  mesh.userData.kind = kind;
  // viewport.js の makeObject と同じ子: 輪郭(同じ頂点の線)と、左右反転の mirror(同じジオメトリ・scale.x = -1。非表示でも Box3.expandByObject は数える)
  const outline = new THREE.LineSegments(g, new THREE.LineBasicMaterial());
  outline.name = "outline";
  mesh.add(outline);
  const mirror = new THREE.Mesh(g, mesh.material);
  mirror.scale.x = -1;
  mirror.name = "mirror";
  mirror.visible = false;
  mesh.add(mirror);
  if (d.p) mesh.position.fromArray(d.p);
  if (d.r) mesh.rotation.set(d.r[0] * DEG, d.r[1] * DEG, d.r[2] * DEG);
  if (d.s) mesh.scale.fromArray(d.s);
  mesh.updateMatrixWorld(true);
  return mesh;
}

/** 本物の Viewport のメソッドを借りた、描画のない Viewport。bind() が登録するハンドラーを handlers に集める */
function makeViewport(c) {
  const handlers = {};
  const rect = c.root; // { left, top, width, height }
  const root = {
    clientWidth: rect.width, clientHeight: rect.height,
    getBoundingClientRect: () => ({ left: rect.left, top: rect.top, width: rect.width, height: rect.height }),
    addEventListener: (type, fn) => { handlers[type] = fn; },
    focus() { }, setPointerCapture() { }, appendChild() { }, classList: { add() { } },
  };
  const vp = Object.create(Viewport.prototype);
  Object.assign(vp, {
    root, options: { emulateNumpad: () => !!c.emulate }, listeners: new Map(), silent: false, abort: new AbortController(),
    hud: { mode: text(), view: text(), stats: text(), status: text(), numeric: text(), tools: { addEventListener() { } }, toolsEdit: text() },
    npanel: { hidden: true, querySelectorAll: () => [] },
    persp: new THREE.PerspectiveCamera(50, 1, 0.1, 500),
    orthoCam: new THREE.OrthographicCamera(-1, 1, 1, -1, -500, 500),
    ortho: !!c.view.ortho,
    view: { target: new THREE.Vector3().fromArray(c.view.target), dist: c.view.dist, theta: c.view.theta, phi: c.view.phi, name: c.view.name },
    objects: [], selected: [], active: null, op: null, loopcut: null, mode: "object", editObj: null, selectMode: "vert", xray: false,
    undoStack: [], redoStack: [], box: null, menu: null, onScreen: true,
    mouse: { x: 0, y: 0, cx: rect.left + rect.width / 2, cy: rect.top + rect.height / 2 },
    axisLine: { visible: false, position: new THREE.Vector3(), quaternion: new THREE.Quaternion(), material: { color: { set() { } } } },
  });
  vp.persp.up.set(0, 0, 1);
  vp.orthoCam.up.set(0, 0, 1);
  // 描画・パネル・メニュー・取り消しの記録だけの代用品
  for (const name of ["refreshSelection", "updateTools", "updateStats", "updateNPanel", "updateGuide", "closeMenu", "rebuild", "editChanged"]) vp[name] = () => { };
  vp.pushUndo = () => { vp.undoStack.push(null); if (vp.undoStack.length > 50) vp.undoStack.shift(); }; // 本物の pushUndo の記録の数だけ(上限 50)
  // resize() の、サイズからの縦横比だけ
  vp.aspect = rect.width / rect.height;
  vp.persp.aspect = vp.aspect;
  vp.persp.updateProjectionMatrix();
  vp.bind();
  vp.updateCamera();
  for (const d of c.objects) {
    const m = makeMesh(d.kind, d);
    vp.objects.push(m);
  }
  const events = [];
  vp.on("*", e => events.push(JSON.parse(JSON.stringify(e))));
  return { vp, handlers, events };
}

const num = v => (Object.is(v, -0) ? 0 : v);
const vec = v => [num(v.x), num(v.y), num(v.z)];

function snapshot(vp, events) {
  const cam = vp.camera;
  vp.updateCamera();
  const op = vp.op;
  return {
    objects: vp.objects.map(o => {
      o.updateMatrixWorld(true);
      const e = o.rotation;
      return { name: o.name, p: vec(o.position), q: [num(o.quaternion.x), num(o.quaternion.y), num(o.quaternion.z), num(o.quaternion.w)], s: vec(o.scale), euler: [num(e.x), num(e.y), num(e.z)] };
    }),
    selected: vp.selected.map(o => o.name),
    active: vp.active?.name ?? null,
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
    op: op ? { kind: op.kind, axis: op.axis, exclude: op.exclude, numeric: op.numeric } : null,
    axisLine: vp.axisLine.visible,
    undoDepth: vp.undoStack.length,
    events: events.splice(0),
  };
}

export function runScenario(c) {
  const { vp, handlers, events } = makeViewport(c);
  for (const idx of c.selected ?? []) { vp.selected.push(vp.objects[idx]); vp.active = vp.objects[idx]; }
  const base = { preventDefault() { }, target: { closest: () => null }, pointerId: 1, shiftKey: false, ctrlKey: false, altKey: false, metaKey: false, button: 0, clientX: 0, clientY: 0 };
  const out = [snapshot(vp, events)];
  for (const s of c.steps) {
    vp.updateCamera(); // 実際のページは、描画のたびに(イベントの合間に)カメラを更新している
    const e = { ...base, clientX: s.x ?? 0, clientY: s.y ?? 0, shiftKey: !!s.shift, ctrlKey: !!s.ctrl, altKey: !!s.alt, metaKey: !!s.meta, button: s.button ?? 0 };
    switch (s.e) {
      case "move": handlers.pointermove(e); break;
      case "down": handlers.pointerdown(e); break;
      case "up": handlers.pointerup(e); break;
      case "wheel": handlers.wheel({ ...e, deltaY: s.dy }); break;
      case "key": handlers.keydown({ ...e, target: vp.root, code: s.code, key: s.key ?? "" }); break;
      case "keyup": handlers.keyup(e); break;
      default: throw new Error("unknown step " + s.e);
    }
    out.push(snapshot(vp, events));
  }
  // 直接呼べる補助の結果(レイ・平面・投影・フレーミング)
  const probes = (c.probes ?? []).map(p => probe(vp, p));
  return { steps: out, probes };
}

/** 画面の点からのレイ・ビュー平面との交点・ピック・画面への投影・1 ピクセルの長さを、現行のメソッドで出す */
function probe(vp, p) {
  const e = { clientX: p.x, clientY: p.y };
  const ndc = vp.ndc(e);
  const ray = new THREE.Raycaster();
  ray.setFromCamera(ndc, vp.camera);
  const pivot = new THREE.Vector3().fromArray(p.pivot ?? [0, 0, 0]);
  const hit = vp.pick(e);
  const world = new THREE.Vector3().fromArray(p.world ?? [0, 0, 0]);
  const scr = vp.toScreen(world);
  return {
    ndc: [ndc.x, ndc.y],
    origin: vec(ray.ray.origin), dir: vec(ray.ray.direction),
    onPlane: vec(vp.unprojectOnPlane(ndc, pivot)),
    pick: hit ? hit.name : null,
    screen: [scr.x, scr.y],
    wpp: vp.worldPerPixel(world),
  };
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
    tasks: runTasks(cases.tasks),
    taskSequences: runTaskSequences(cases.taskSequences),
    scenarios: cases.scenarios.map(runScenario),
  };
}

if (process.argv[2]) {
  const cases = JSON.parse(readFileSync(process.argv[2], "utf8"));
  writeFileSync(process.argv[3], JSON.stringify(run(cases)));
}
