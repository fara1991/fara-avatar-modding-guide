// practice-cases.json(練習の操作ロジックの突き合わせ用の入力)を作る。決まった乱数の種から作るので、何度作っても同じ。  node practice-cases.generate.mjs
// 入力の中身は、JS(practice-reference.mjs が現行の viewport.js・practice.js を動かす)と C#(FaraAvatarModdingGuide.Practice)の両方に同じものを渡す。
//   tasks          お題 18 件の判定を、いろいろな状態(境界の値を含む)で
//   taskSequences  checkTasks(順に進める)を、状態の列で
//   scenarios      3D ビューの操作: 決まった手順(選択・G/R/S の軸と数値・視点・ドラッグ・ホイールなど)と、乱数で作った操作列
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { makeRobust } from "./practice-fragile.mjs";

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
const r = rng(20261024);
const pick = a => a[Math.floor(r() * a.length)];
const between = (lo, hi) => lo + r() * (hi - lo);
const round = (x, d = 3) => Math.round(x * 10 ** d) / 10 ** d;

const DEG = Math.PI / 180;
const KINDS = ["plane", "cube", "sphere", "cylinder", "cone", "torus"];

// ---------------------------------------------------------------- お題
const FLAGS = ["undoUsed", "sphereAdded", "extruded", "inset", "loopcut", "bevel"];
const rawObject = (kind, p, rDeg, s) => ({ kind, p, r: rDeg.map(d => d * DEG), s });

function taskContext(over = {}) {
  return {
    objects: [rawObject("cube", [0, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [], viewName: "ユーザー", ortho: false, mode: "object", selectMode: "vert", faces: [],
    flags: Object.fromEntries(FLAGS.map(f => [f, false])), ...over,
  };
}

const tasks = [];
// 境界: near は 0.05 以内、nearAngle は 1° 以内(360° をまたぐ)
for (const dz of [1.94, 1.9501, 1.9499, 2, 2.0499, 2.0501, 2.06, 0, -2]) tasks.push(taskContext({ objects: [rawObject("cube", [0, 0, dz], [0, 0, 0], [1, 1, 1])] }));
for (const deg of [45, 44.01, 43.99, 46.01, 45.99, 405, -315, 765, 44.5, 225, -45, 0, 90, 1e-9]) tasks.push(taskContext({ objects: [rawObject("cube", [0, 0, 0], [0, 0, deg], [1, 1, 1])] }));
for (const [sx, sz] of [[2, 2], [2.05, 1.95], [2.0501, 2], [2, 1.9499], [1, 2], [2, 1], [0.5, 4]]) tasks.push(taskContext({ objects: [rawObject("cube", [0, 0, 0], [0, 0, 0], [sx, 1, sz])] }));
for (const x of [3, 2.96, 2.94, 3.05, 3.06]) tasks.push(taskContext({ objects: [rawObject("sphere", [x, 0, 0], [0, 0, 0], [1, 1, 1])] }));
for (const y of [3, 2.9, 3.1]) tasks.push(taskContext({ objects: [rawObject("cube", [0, 0, 0], [0, 0, 0], [1, 1, 1]), rawObject("cube", [1, y, 0], [0, 0, 0], [1, 1, 1])] }));
tasks.push(taskContext({ objects: [rawObject("cube", [0, 3, 0], [0, 0, 0], [1, 1, 1])] }));
for (const faces of [[], [[0, 0, 1]], [[0, 0, 0.9]], [[0, 0, 0.91]], [[0, 0, 1], [0, 0, 1]], [[0, 1, 0]]])
  for (const selectMode of ["vert", "face"]) tasks.push(taskContext({ mode: "edit", selectMode, faces }));
for (const f of FLAGS) tasks.push(taskContext({ flags: { ...Object.fromEntries(FLAGS.map(x => [x, false])), [f]: true } }));
tasks.push(taskContext({ viewName: "前" }), taskContext({ ortho: true }), taskContext({ mode: "edit" }));
tasks.push(taskContext({ objects: [rawObject("cube", [0, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [0] }));
tasks.push(taskContext({ objects: [rawObject("sphere", [0, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [0] }));
tasks.push(taskContext({ objects: [rawObject("sphere", [0, 0, 0], [0, 0, 0], [1, 1, 1])], flags: { ...Object.fromEntries(FLAGS.map(x => [x, false])), sphereAdded: true } }));
tasks.push(taskContext({ objects: [], flags: { ...Object.fromEntries(FLAGS.map(x => [x, false])), sphereAdded: true } }));
// 乱数
for (let i = 0; i < 250; i++) {
  const n = Math.floor(r() * 4);
  const objects = Array.from({ length: n }, () => rawObject(pick(KINDS.slice(0, 3)), [pick([0, 2, 3, round(between(-4, 4))]), pick([0, 3, round(between(-4, 4))]), pick([0, 2, round(between(-4, 4))])],
    [pick([0, 0, round(between(-90, 90))]), pick([0, 0, round(between(-60, 60))]), pick([0, 45, -45, 405, round(between(-720, 720))])], [pick([1, 2]), pick([1, 2]), pick([1, 2, round(between(0.2, 4))])]));
  tasks.push(taskContext({
    objects, selected: objects.map((_, k) => k).filter(() => r() < 0.4), viewName: pick(["前", "ユーザー", "右", "上"]), ortho: r() < 0.3,
    mode: pick(["object", "edit"]), selectMode: pick(["vert", "edge", "face"]), faces: r() < 0.4 ? [[0, 0, pick([1, 0.95, 0.5, -1])]] : [],
    flags: Object.fromEntries(FLAGS.map(f => [f, r() < 0.4])),
  }));
}

// 順に進める(checkTasks): 練習の通りに少しずつ満たしていく列と、先のお題だけ満たした列
const seq = (...steps) => steps;
const cube = (p = [0, 0, 0], rz = 0, s = [1, 1, 1]) => rawObject("cube", p, [0, 0, rz], s);
const flags = (...on) => Object.fromEntries(FLAGS.map(f => [f, on.includes(f)]));
const taskSequences = [
  seq(
    taskContext({ objects: [cube()] }),
    taskContext({ objects: [cube()], selected: [0] }),
    taskContext({ objects: [cube([0, 0, 2])], selected: [0] }),
    taskContext({ objects: [cube([0, 0, 2], 45)], selected: [0] }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2])], selected: [0] }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), rawObject("sphere", [0, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [1] }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), rawObject("sphere", [3, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [1] }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0]), rawObject("sphere", [3, 0, 0], [0, 0, 0], [1, 1, 1])], selected: [1] }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0]), rawObject("sphere", [3, 0, 0], [0, 0, 0], [1, 1, 1])], viewName: "前" }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0]), rawObject("sphere", [3, 0, 0], [0, 0, 0], [1, 1, 1])], viewName: "前", ortho: true }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0]), rawObject("sphere", [3, 0, 0], [0, 0, 0], [1, 1, 1])], viewName: "前", ortho: true, flags: flags("undoUsed", "sphereAdded") }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0])], viewName: "前", ortho: true, flags: flags("undoUsed", "sphereAdded") }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0])], viewName: "前", ortho: true, mode: "edit", flags: flags("undoUsed", "sphereAdded") }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0])], viewName: "前", ortho: true, mode: "edit", selectMode: "face", faces: [[0, 0, 1]], flags: flags("undoUsed", "sphereAdded") }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0])], viewName: "前", ortho: true, mode: "edit", selectMode: "face", faces: [[0, 0, 1]], flags: flags("undoUsed", "sphereAdded", "extruded", "inset", "loopcut", "bevel") }),
    taskContext({ objects: [cube([0, 0, 2], 45, [2, 2, 2]), cube([0, 3, 0])], viewName: "前", ortho: true, mode: "object", flags: flags("undoUsed", "sphereAdded", "extruded", "inset", "loopcut", "bevel") }),
  ),
  // 先のお題だけ満たしても、前が済むまで進まない
  seq(
    taskContext({ ortho: true, viewName: "前", flags: flags("undoUsed", "bevel"), mode: "object" }),
    taskContext({ selected: [0], ortho: true, viewName: "前", flags: flags("undoUsed", "bevel") }),
    taskContext({ objects: [cube([0, 0, 2])], selected: [0], ortho: true, viewName: "前" }),
  ),
];

// ---------------------------------------------------------------- 3D ビューの操作
const ROOT = { left: 37, top: 61, width: 912, height: 529 };
const DEFAULT_VIEW = { target: [0, 0, 0], dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, ortho: false, name: "ユーザー" };
const center = { x: ROOT.left + ROOT.width / 2, y: ROOT.top + ROOT.height / 2 };
const mods = (shift, ctrl, alt) => ({ ...(shift ? { shift: true } : {}), ...(ctrl ? { ctrl: true } : {}), ...(alt ? { alt: true } : {}) });
const move = (x, y, shift, ctrl) => ({ e: "move", x, y, ...mods(shift, ctrl) });
const down = (x, y, button = 0, shift, ctrl, alt) => ({ e: "down", x, y, button, ...mods(shift, ctrl, alt) });
const up = (x, y, button = 0) => ({ e: "up", x, y, button });
const key = (code, k, shift, ctrl, alt, meta) => ({ e: "key", code, key: k ?? code, ...mods(shift, ctrl, alt), ...(meta ? { meta: true } : {}) });
const click = (x, y, shift) => [move(x, y, shift), down(x, y, 0, shift), up(x, y)];
const letter = c => key("Key" + c.toUpperCase(), c.toLowerCase());
const digit = d => key("Digit" + d, String(d));
const enter = () => key("Enter", "Enter");
const esc = () => key("Escape", "Escape");
const probes = [{ x: center.x, y: center.y, world: [1, 2, 3], pivot: [0, 0, 1] }, { x: ROOT.left + 100, y: ROOT.top + 80, world: [-2, 1, 0], pivot: [1, 1, 1] }, { x: ROOT.left + 800, y: ROOT.top + 450, world: [0, 0, 5], pivot: [0, 0, 0] }];

const cubeAt = (name, p, r, s) => ({ kind: "cube", name, ...(p ? { p } : {}), ...(r ? { r } : {}), ...(s ? { s } : {}) });
const scene1 = [cubeAt("Cube"), { kind: "sphere", name: "Sphere", p: [3, 0, 0] }];
const scenario = (name, o) => ({ name, root: ROOT, emulate: false, view: DEFAULT_VIEW, objects: [cubeAt("Cube")], selected: [], probes, ...o });

const scenarios = [
  scenario("クリックで選択・Shift で追加・外れで解除", {
    objects: scene1,
    steps: [...click(center.x, center.y), ...click(center.x + 200, center.y - 100), ...click(center.x, center.y, true), ...click(center.x, center.y, true), ...click(ROOT.left + 5, ROOT.top + 5), ...click(center.x, center.y)],
  }),
  scenario("G → Z → 2 → Enter(お題 2)", { selected: [0], steps: [move(center.x + 40, center.y - 10), letter("g"), move(center.x + 90, center.y - 60), letter("z"), digit(2), enter()] }),
  scenario("G の軸: X・Shift+Y(それ以外)・もう一度で解除・数値・Backspace・マイナス", {
    selected: [0],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 120, center.y + 60), letter("x"), move(center.x + 160, center.y + 20), key("KeyY", "Y", true), move(center.x - 50, center.y + 90), key("KeyY", "Y", true), letter("z"), letter("z"),
      digit(1), key("Period", "."), digit(5), key("Minus", "-"), key("Backspace"), key("Backspace"), key("Backspace"), key("Backspace"), digit(3), key("Minus", "-"), enter()],
  }),
  scenario("数値入力が 「-.」 のあいだは数値入力なし(G・R・S。NaN でオブジェクトが消えない)", {
    selected: [0],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 80, center.y + 20), key("Minus", "-"), key("Period", "."), move(center.x + 90, center.y + 25), digit(5), key("Backspace"), key("Backspace"), enter(),
      move(center.x - 120, center.y + 70), letter("r"), move(center.x + 10, center.y - 60), key("Minus", "-"), key("Period", "."), move(center.x - 40, center.y - 50), digit(3), enter(),
      move(center.x - 150, center.y + 100), letter("s"), move(center.x + 120, center.y + 10), key("Minus", "-"), key("Period", "."), move(center.x + 150, center.y + 10), key("Backspace"), key("Backspace"), enter()],
  }),
  scenario("G の Ctrl(刻み)と Shift(細かく)・キーの上げ下げ", {
    selected: [0],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 123, center.y + 47, false, true), move(center.x + 123, center.y + 47, true, true), key("ShiftLeft", "Shift", true, true), { e: "keyup", ctrl: true }, { e: "keyup" }, move(center.x - 77, center.y - 31, true), letter("x"), move(center.x - 177, center.y - 31, true, true), enter()],
  }),
  scenario("R → Z → 45 → Enter(お題 3)・R のマウス・刻み", {
    selected: [0],
    steps: [move(center.x + 60, center.y), letter("r"), move(center.x + 30, center.y - 80), move(center.x - 70, center.y - 20, false, true), letter("z"), digit(4), digit(5), enter(), letter("r"), move(center.x + 5, center.y + 100), key("KeyX", "x", true), move(center.x + 100, center.y + 100, true), esc()],
  }),
  scenario("R の数値(大きい角度・負の角度)と軸", {
    selected: [0, 1], objects: scene1,
    steps: [move(center.x, center.y), letter("r"), letter("y"), digit(8), digit(0), key("Minus", "-"), enter(), letter("r"), digit(4), digit(0), digit(5), enter(), letter("r"), key("Digit1", "1", false), key("Numpad2", "2"), key("NumpadDecimal", "."), digit(5), enter()],
  }),
  scenario("S → 2 → Enter(お題 4)・S のマウス・軸・それ以外", {
    selected: [0],
    steps: [move(center.x + 80, center.y + 10), letter("s"), move(center.x + 160, center.y + 10), letter("x"), move(center.x + 40, center.y + 70), key("KeyZ", "Z", true), move(center.x + 15, center.y + 10, false, true), digit(2), enter(), letter("s"), digit(2), enter()],
  }),
  scenario("複数選択の G / R / S(中心のまわり)", {
    objects: [cubeAt("Cube", [-2, 0, 0]), cubeAt("Cube.001", [2, 1, 0], [0, 0, 30], [1, 2, 1]), { kind: "cone", name: "Cone", p: [0, -3, 1] }], selected: [0, 1, 2],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 70, center.y + 30), enter(), letter("r"), move(center.x + 30, center.y - 60), enter(), letter("s"), move(center.x + 90, center.y - 60), enter(), letter("s"), digit(0), key("Period", "."), digit(5), key("KeyY", "y"), enter()],
  }),
  scenario("キャンセル: Esc・右クリック・もとの値に戻る", {
    objects: scene1, selected: [0, 1],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 200, center.y + 100), esc(), letter("r"), move(center.x + 10, center.y - 90), down(center.x + 10, center.y - 90, 2), up(center.x + 10, center.y - 90, 2), letter("s"), digit(3), esc(), letter("g"), down(center.x, center.y, 0), up(center.x, center.y, 0)],
  }),
  scenario("選択なしで G(失敗)・操作中に別のキー", { steps: [letter("g"), letter("r"), letter("s"), key("KeyQ", "q"), ...click(center.x, center.y), letter("g"), key("KeyQ", "q"), letter("a"), esc()] }),
  scenario("全選択・選択解除・反転", {
    objects: [cubeAt("Cube"), { kind: "plane", name: "Plane", p: [2, 2, 0] }, { kind: "torus", name: "Torus", p: [-3, 0, 1] }], selected: [1],
    steps: [letter("a"), key("KeyA", "a", false, false, true), letter("a"), key("KeyI", "i", false, true), key("KeyI", "i", false, true), key("KeyB", "b"), key("KeyG", "g", false, true), key("KeyH", "h")],
  }),
  scenario("ボックス選択(ドラッグ)・Shift で追加", {
    objects: [cubeAt("A", [-3, 0, 0]), cubeAt("B", [0, 0, 0]), cubeAt("C", [3, 0, 0]), cubeAt("D", [0, 4, 0])],
    steps: [move(ROOT.left + 20, ROOT.top + 20), down(ROOT.left + 20, ROOT.top + 20), move(ROOT.left + 22, ROOT.top + 22), move(ROOT.left + 500, ROOT.top + 400), up(ROOT.left + 500, ROOT.top + 400),
      down(ROOT.left + 600, ROOT.top + 100, 0, true), move(ROOT.left + 700, ROOT.top + 300, true), up(ROOT.left + 700, ROOT.top + 300), down(ROOT.left + 400, ROOT.top + 300), move(ROOT.left + 401, ROOT.top + 301), up(ROOT.left + 401, ROOT.top + 301),
      down(ROOT.left + 10, ROOT.top + 10), move(ROOT.left + 900, ROOT.top + 520), up(ROOT.left + 900, ROOT.top + 520)],
  }),
  scenario("視点: 中ボタンで回転・Shift で移動・Ctrl でズーム・Alt+左・ホイール", {
    objects: scene1,
    steps: [down(center.x, center.y, 1), move(center.x + 40, center.y + 25), move(center.x + 90, center.y - 60), up(center.x + 90, center.y - 60, 1),
      down(center.x, center.y, 1, true), move(center.x - 60, center.y + 30, true), move(center.x - 140, center.y - 30, true), up(center.x - 140, center.y - 30, 1),
      down(center.x, center.y, 1, false, true), move(center.x, center.y + 50, false, true), move(center.x, center.y - 20, false, true), up(center.x, center.y - 20, 1),
      down(center.x, center.y, 0, false, false, true), move(center.x + 5, center.y + 5), up(center.x + 5, center.y + 5), down(center.x, center.y, 0, false, false, true), up(center.x, center.y),
      { e: "wheel", dy: 100 }, { e: "wheel", dy: -100 }, { e: "wheel", dy: 0 }, ...Array.from({ length: 40 }, () => ({ e: "wheel", dy: 120 })), ...Array.from({ length: 80 }, () => ({ e: "wheel", dy: -120 }))],
  }),
  scenario("視点: 回転の上下の限界(phi のクランプ)", {
    steps: [down(center.x, center.y, 1), move(center.x, center.y + 400), move(center.x, center.y + 900), move(center.x + 30, center.y - 900), move(center.x + 30, center.y - 2500), up(center.x, center.y - 2500, 1)],
  }),
  scenario("テンキー: 1・3・7 と Ctrl・5・Decimal(フレーミング)・Home", {
    objects: scene1, selected: [1],
    steps: ["1", "3", "7", "5", "5"].map(k => key("Numpad" + k, k)).concat([key("Numpad1", "1", false, true), key("Numpad3", "3", false, true), key("Numpad7", "7", false, true), key("NumpadDecimal", "."), key("Numpad8", "8"), key("NumpadAdd", "+"), key("Home", "Home"), key("Numpad5", "5"),
      down(center.x, center.y, 1), move(center.x + 30, center.y + 30), up(center.x + 30, center.y + 30, 1)]),
  }),
  scenario("テンキーを模倣(Digit)・ピリオド", {
    emulate: true, objects: scene1,
    steps: [digit(1), digit(3), digit(7), digit(5), key("Period", "."), digit(8), key("KeyG", "g"), digit(2), key("Digit5", "5", false, true)],
  }),
  scenario("平行投影(前)での G / R / S とクリック", {
    view: { ...DEFAULT_VIEW, ortho: true, theta: -Math.PI / 2, phi: Math.PI / 2, name: "前" }, objects: scene1, selected: [0],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 100, center.y - 40), letter("x"), digit(2), enter(), ...click(center.x, center.y), letter("r"), move(center.x + 50, center.y + 50), enter(), letter("s"), move(center.x + 150, center.y + 5), enter(), ...click(center.x + 60, center.y - 10)],
  }),
  scenario("真上から(up が +Y になる)での操作", {
    view: { ...DEFAULT_VIEW, theta: -Math.PI / 2, phi: 0.001, name: "上", ortho: true }, objects: scene1, selected: [1],
    steps: [move(center.x, center.y), letter("g"), move(center.x + 100, center.y - 40), enter(), key("Numpad1", "1"), key("Numpad7", "7", false, true), letter("r"), move(center.x - 60, center.y + 70), enter()],
  }),
  scenario("取り消しの記録の上限(50 件。これを超えると古いものを捨てる)", {
    selected: [0], steps: [move(center.x + 31, center.y + 17), ...Array.from({ length: 56 }, (_, i) => [letter(i % 3 === 0 ? "g" : i % 3 === 1 ? "r" : "s"), move(center.x + (i % 7) * 20, center.y + (i % 5) * 10), enter()]).flat(), letter("g"), esc(), letter("r"), esc()],
  }),
  scenario("画面の外・大きな値", {
    objects: scene1, selected: [0],
    steps: [move(-500, -500), letter("g"), move(5000, 3000), move(center.x, center.y), digit(9), digit(9), digit(9), digit(9), digit(9), digit(9), enter(), letter("s"), digit(0), enter(), letter("s"), key("Minus", "-"), digit(2), enter(), ...click(-10, -10), ...click(5000, 5000)],
  }),
  scenario("回転済みの物体を選ぶ・フレーミング", {
    objects: [cubeAt("Cube", [1, 2, 3], [20, 30, 40], [1, 2, 3]), { kind: "cylinder", name: "Cylinder", p: [-4, 1, 0], r: [90, 0, 15], s: [1, 1, 2] }],
    steps: [...click(center.x, center.y), key("NumpadDecimal", "."), key("Home", "Home"), ...click(center.x + 30, center.y), letter("a"), key("NumpadDecimal", ".")],
  }),
];

// 乱数で作った操作列: 見えている範囲にオブジェクト・クリック・ドラッグ・キー操作を混ぜる
function randomScenario(i) {
  const n = 1 + Math.floor(r() * 4);
  const objects = Array.from({ length: n }, (_, k) => ({
    kind: pick(KINDS), name: `${pick(["Cube", "Obj"])}.${k}`, p: [round(between(-4, 4)), round(between(-4, 4)), round(between(-2, 3))],
    r: [pick([0, round(between(-180, 180))]), pick([0, round(between(-90, 90))]), pick([0, round(between(-180, 180))])], s: [pick([1, round(between(0.3, 3))]), pick([1, round(between(0.3, 3))]), pick([1, round(between(0.3, 3))])],
  }));
  const view = {
    target: [round(between(-2, 2)), round(between(-2, 2)), round(between(-1, 2))], dist: round(between(4, 30)), theta: round(between(-Math.PI, Math.PI), 4), phi: round(between(0.05, Math.PI - 0.05), 4),
    ortho: r() < 0.3, name: "ユーザー",
  };
  const selected = objects.map((_, k) => k).filter(() => r() < 0.5);
  const at = () => ({ x: round(ROOT.left + between(-0.05, 1.05) * ROOT.width, 1), y: round(ROOT.top + between(-0.05, 1.05) * ROOT.height, 1) });
  let numeric = "";
  let inOp = false, op = null;
  const steps = [];
  for (let s = 0; s < 45; s++) {
    const t = r();
    const m = { shift: r() < 0.15, ctrl: r() < 0.15 };
    if (inOp) {
      const choice = r();
      if (choice < 0.35) { const p = at(); steps.push(move(p.x, p.y, m.shift, m.ctrl)); }
      else if (choice < 0.5) { steps.push(key("Key" + pick(["X", "Y", "Z"]), "x", m.shift)); }
      else if (choice < 0.75) {
        const k = pick(["digit", "digit", "digit", "period", "minus", "back"]);
        let next = numeric;
        if (k === "digit") next += "5";
        else if (k === "period") next = numeric.includes(".") ? numeric : numeric + ".";
        else if (k === "minus") next = numeric.startsWith("-") ? numeric.slice(1) : "-" + numeric;
        else next = numeric.slice(0, -1);
        numeric = next;
        if (k === "digit") steps.push(digit(pick([0, 1, 2, 3, 4, 5, 6, 7, 8, 9])));
        else if (k === "period") steps.push(key("Period", "."));
        else if (k === "minus") steps.push(key("Minus", "-"));
        else steps.push(key("Backspace"));
      }
      else if (choice < 0.85) { steps.push(enter()); inOp = false; numeric = ""; }
      else if (choice < 0.92) { steps.push(esc()); inOp = false; numeric = ""; }
      else if (choice < 0.96) { const p = at(); steps.push(down(p.x, p.y, 0)); inOp = false; numeric = ""; }
      else { const p = at(); steps.push(down(p.x, p.y, 2)); inOp = false; numeric = ""; }
      continue;
    }
    if (t < 0.25) { const p = at(); steps.push(...click(p.x, p.y, m.shift)); }
    else if (t < 0.4) { op = pick(["g", "r", "s"]); const p = at(); steps.push(move(p.x, p.y), letter(op)); inOp = true; numeric = ""; }
    else if (t < 0.5) { const a = at(), b = at(); steps.push(down(a.x, a.y, 0, m.shift), move(b.x, b.y, m.shift), up(b.x, b.y)); }
    else if (t < 0.65) { const a = at(), b = at(); const kind = pick([[1, false, false, false], [1, true, false, false], [1, false, true, false], [0, false, false, true]]); steps.push(down(a.x, a.y, kind[0], kind[1], kind[2], kind[3]), move((a.x + b.x) / 2, (a.y + b.y) / 2), move(b.x, b.y), up(b.x, b.y, kind[0])); }
    else if (t < 0.75) steps.push({ e: "wheel", dy: pick([120, -120, 100, -53, 0]) });
    else if (t < 0.87) steps.push(key("Numpad" + pick(["1", "3", "7", "5", "Decimal", "8"]).replace("Decimal", "Decimal"), "1", false, m.ctrl));
    else if (t < 0.92) steps.push(key("Home", "Home"));
    else if (t < 0.96) steps.push(key("KeyA", "a", false, false, r() < 0.5));
    else steps.push(key("KeyI", "i", false, true));
  }
  if (inOp) steps.push(enter());
  return { name: `乱数 ${i}`, root: ROOT, emulate: r() < 0.3, view, objects, selected, probes: [{ ...at(), world: [1, 2, 3], pivot: [0, 0, 1] }, { ...at(), world: [-1, 0, 2], pivot: [1, 1, 1] }], steps };
}
for (let i = 0; i < 60; i++) scenarios.push(randomScenario(i));

// 三角関数の 1 ulp の誤差に強い列だけを残す(practice-fragile.mjs)
makeRobust(scenarios);

const out = path.join(path.dirname(fileURLToPath(import.meta.url)), "practice-cases.json");
writeFileSync(out, JSON.stringify({ tasks, taskSequences, scenarios }));
console.log(`tasks ${tasks.length}, taskSequences ${taskSequences.length}, scenarios ${scenarios.length}, steps ${scenarios.reduce((a, s) => a + s.steps.length, 0)}`);
