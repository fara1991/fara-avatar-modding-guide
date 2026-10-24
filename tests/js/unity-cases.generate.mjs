// tests/js/unity-cases.json(Unity 風の画面の突き合わせの入力)を作る。使い方: node tests/js/unity-cases.generate.mjs
// 台本の操作列(チュートリアル 2 本の手順・失敗・取り消し・ポップアップ・ドロップ)と、ギズモのドラッグ(ギズモの矢印・輪の画面上の位置を、本物の unity.js を動かして求める)、
// 視点の操作(Alt+左・中・右ドラッグ・ホイール)、乱数(固定の種)による操作列。JS を動かして作るので、数十秒かかる。
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { createHarness, apply } from "./unity-reference.mjs";
const THREE = await import("three");   // unity-reference.mjs が登録した読み込みのフックで、同梱の three.js を読む

const here = path.dirname(fileURLToPath(import.meta.url));
const RECT = { left: 100, top: 50, width: 711.5, height: 400.25 };
const CLIENT = { w: 711, h: 400 };

function rng(seed) {
  let a = seed >>> 0;
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}

const PATHS = ["Main Camera", "Directional Light", "Avatar", "Avatar/Armature", "Avatar/Armature/Hips", "Avatar/Armature/Hips/Spine", "Avatar/Armature/Hips/Spine/Chest",
  "Avatar/Armature/Hips/Spine/Chest/LeftUpperArm", "Avatar/Body", "Avatar/Shirt_Default", "Hoodie", "Avatar/Hoodie", "Avatar/Hoodie/Armature", "Avatar/Hoodie/Hoodie_Mesh", "Hoodie/Hoodie_Mesh",
  "Hoodie/Armature", "Avatar/Hoodie (1)", "Hoodie (1)", "Armature", "Hips", "Body", "Hoodie_Mesh", "Chest", "Nothing"];
const KEYS = ["KeyQ", "KeyW", "KeyE", "KeyR", "KeyF", "Delete", "Escape", "KeyZ", "KeyA", "Enter"];
const MATERIALS = ["Hoodie_lilToon", "Hoodie_Red", "Hoodie_Standard", "Body_lilToon", "Hoodie_Tex", "Hoodie.fbx"];
const COMPS = ["MABlendshapeSync", "MAMergeArmature", "MABoneProxy", "MAMeshSettings", "VRCPhysBone", "BoxCollider", "Light"];
const ASSETS = ["Avatar", "Hoodie", "Hoodie.fbx", "Hoodie_lilToon", "Hoodie_Red", "Hoodie_Tex"];
const TAGS = ["Untagged", "EditorOnly", "MainCamera", "Player"];

const cases = [];

/** 台本 */
const frame = (dt = 16) => ({ k: "frame", dt });
cases.push({ name: "ma-outfit-run", practice: true, ops: [
  { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, frame(), { k: "ctxByName", path: "Avatar/Hoodie" }, { k: "ctxItem", item: "setup-outfit" }, frame(),
  { k: "select", path: "Avatar/Shirt_Default" }, { k: "active", path: "Avatar/Shirt_Default", on: false }, { k: "tag", path: "Avatar/Shirt_Default", tag: "EditorOnly" }, frame(),
  { k: "select", path: "Hoodie/Hoodie_Mesh" }, { k: "material", path: "Hoodie/Hoodie_Mesh", slot: 0, material: "Hoodie_lilToon" }, { k: "addComponent", path: "Hoodie/Hoodie_Mesh", type: "MABlendshapeSync" }, frame(),
  { k: "openMenu", name: "VRChat SDK" }, { k: "openSdk" }, { k: "build", kind: "test" }, { k: "advance", ms: 500 }, frame(), { k: "advance", ms: 400 }, frame(), frame(250), frame(250), { k: "advance", ms: 4600 }, frame(),
] });
cases.push({ name: "ma-outfit-replay-no-frames", practice: true, ops: [
  { k: "reset" }, { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, { k: "ctxByName", path: "Avatar/Hoodie" }, { k: "setupOutfit", path: "Avatar/Hoodie" },
  { k: "select", path: "Avatar/Shirt_Default" }, { k: "active", path: "Avatar/Shirt_Default", on: false }, { k: "tag", path: "Avatar/Shirt_Default", tag: "EditorOnly" },
  { k: "select", path: "Hoodie/Hoodie_Mesh" }, { k: "frameSel" }, { k: "material", path: "Hoodie/Hoodie_Mesh", slot: 0, material: "Hoodie_lilToon" }, { k: "addComponent", path: "Hoodie/Hoodie_Mesh", type: "MABlendshapeSync" },
  { k: "openMenu", name: "VRChat SDK" }, { k: "openSdk" }, { k: "build", kind: "test" }, { k: "advance", ms: 900 }, frame(), frame(), { k: "advance", ms: 4500 }, frame(),
] });
cases.push({ name: "unity-basics-replay", ops: [
  { k: "reset" }, { k: "expand", path: "Avatar", on: true }, { k: "select", path: "Avatar/Body" }, { k: "frameSel" }, { k: "orbit", dyaw: 0.6 }, { k: "viewDistMul", f: 0.8 }, { k: "viewTargetX", dx: 0.2 },
  { k: "select", path: "Avatar" }, { k: "tool", tool: "move" }, { k: "edit", path: "Avatar", prop: "p", axis: 0, value: 0.5 }, { k: "edit", path: "Avatar", prop: "p", axis: 0, value: 0 },
  { k: "tool", tool: "rotate" }, { k: "tool", tool: "scale" }, { k: "tool", tool: "hand" }, frame(), { k: "pick", x: 355, y: 200 }, frame(),
] });
cases.push({ name: "setup-outfit-failures", practice: true, ops: [
  { k: "instantiate", asset: "Hoodie", parent: null }, { k: "setupOutfit", path: "Hoodie" }, { k: "select", path: "Avatar" }, { k: "setupOutfit", path: "Avatar" },
  { k: "setupOutfit", path: "Avatar/Body" }, { k: "reparent", path: "Hoodie", parent: "Avatar" }, { k: "reparent", path: "Avatar/Hoodie", parent: "Avatar/Hoodie" },
  { k: "reparent", path: "Avatar", parent: "Avatar/Hoodie" }, { k: "setupOutfit", path: "Avatar/Hoodie" }, { k: "setupOutfit", path: "Avatar/Hoodie" }, { k: "undo" }, { k: "undo" }, { k: "undo" }, { k: "undo" }, { k: "undo" }, { k: "undo" },
] });
cases.push({ name: "popups", ops: [
  { k: "select", path: "Avatar/Body" }, { k: "openAdd" }, { k: "addSearch", text: "blend" }, { k: "addEnter" }, { k: "openAdd" }, { k: "addSearch", text: "zzz" }, { k: "addEnter" }, { k: "addSearch", text: "  Phys " }, { k: "addEscape" },
  { k: "openAdd" }, { k: "addSearch", text: "vrchat" }, { k: "closePopup" }, { k: "openMenu", name: "File" }, { k: "openMenu", name: "VRChat SDK" }, { k: "select", path: "Avatar" }, { k: "ctxByName", path: "Avatar/Body" }, { k: "ctxItem", item: "delete" }, { k: "undo" },
  { k: "ctxByName", path: "Avatar/Body" }, { k: "key", code: "KeyW" }, { k: "select", path: "Avatar" }, { k: "openSdk" }, { k: "key", code: "Escape" }, { k: "openSdk" }, { k: "closeSdk" }, { k: "openSdk" }, { k: "openMenu", name: "Tools" }, { k: "closeSdk", quiet: true },
] });
cases.push({ name: "drops", practice: true, ops: [
  { k: "dropAsset", asset: "Hoodie", target: { kind: "node", path: "Avatar" } }, { k: "dropAsset", asset: "Hoodie", target: { kind: "root" } }, { k: "dropAsset", asset: "Avatar", target: { kind: "scene" } }, { k: "dropAsset", asset: "Hoodie.fbx", target: { kind: "root" } },
  { k: "dropAsset", asset: "Hoodie_Tex", target: { kind: "scene" } }, { k: "select", path: "Hoodie/Hoodie_Mesh" }, { k: "dropAsset", asset: "Hoodie_Red", target: { kind: "slot", slot: 0 } }, { k: "dropAsset", asset: "Hoodie_Red", target: { kind: "inspector" } },
  { k: "material", path: "Avatar/Body", slot: 1, material: "Hoodie_Red" }, { k: "dropAsset", asset: "Hoodie_lilToon", target: { kind: "scene" }, x: 355, y: 200 }, frame(), { k: "dropAsset", asset: "Hoodie_lilToon", target: { kind: "scene" }, x: 355, y: 200 },
  { k: "dropNode", path: "Hoodie (1)", target: { kind: "node", path: "Avatar" } }, { k: "dropNode", path: "Avatar/Hoodie (1)", target: { kind: "root" } }, { k: "dropNode", path: "Avatar/Hoodie", target: { kind: "inspector" } }, { k: "clickAsset", asset: "Hoodie" },
  { k: "clearSelection" }, { k: "dblclick", path: "Avatar/Body" }, frame(), { k: "dblclick", path: "Nothing" },
] });
cases.push({ name: "transform-edits", ops: [
  { k: "select", path: "Avatar" }, { k: "edit", path: "Avatar", prop: "r", axis: 1, value: 90 }, { k: "edit", path: "Avatar", prop: "s", axis: 2, value: 2 }, { k: "edit", path: "Avatar", prop: "p", axis: 1, value: 0.25, fromInput: true },
  { k: "edit", path: "Avatar", prop: "p", axis: 1, value: Number.NaN }, frame(), { k: "instantiate", asset: "Hoodie", parent: null }, { k: "reparent", path: "Hoodie", parent: "Avatar" }, frame(),
  { k: "reparent", path: "Avatar/Hoodie", parent: "Avatar/Armature/Hips" }, { k: "reparent", path: "Avatar/Armature/Hips/Hoodie", parent: null }, { k: "undo" }, frame(), { k: "delete", path: "Avatar/Armature" }, frame(), { k: "undo" },
] });
// SDK の集計(レビュー L1): 親が EditorOnly なら子も除く伝播・skinned の数え方・PhysBone の数え方(無効でも数える・EditorOnly の下は数えない)・アバターが無いとき
cases.push({ name: "sdk-editoronly-propagation", practice: true, ops: [
  { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, { k: "setupOutfit", path: "Avatar/Hoodie" }, { k: "expand", path: "Avatar", on: true }, { k: "expand", path: "Avatar/Hoodie", on: true },
  { k: "addComponent", path: "Avatar/Hoodie", type: "VRCPhysBone" }, { k: "addComponent", path: "Avatar/Hoodie/Hoodie_Mesh", type: "VRCPhysBone" },
  { k: "addComponent", path: "Avatar/Body", type: "VRCPhysBone" }, { k: "addComponent", path: "Avatar/Armature/Hips", type: "VRCPhysBone" }, { k: "openSdk" }, frame(),
  { k: "tag", path: "Avatar/Hoodie", tag: "EditorOnly" }, frame(),
  { k: "tag", path: "Avatar/Hoodie", tag: "Untagged" }, { k: "tag", path: "Avatar/Hoodie/Hoodie_Mesh", tag: "EditorOnly" }, frame(),
  { k: "tag", path: "Avatar/Hoodie/Hoodie_Mesh", tag: "Untagged" }, { k: "active", path: "Avatar/Hoodie", on: false }, frame(),
  { k: "tag", path: "Avatar/Armature/Hips", tag: "EditorOnly" }, frame(), { k: "tag", path: "Avatar/Armature/Hips", tag: "Untagged" },
  { k: "tag", path: "Avatar/Armature/Hips/Spine", tag: "EditorOnly" }, frame(), { k: "tag", path: "Avatar/Armature", tag: "EditorOnly" }, frame(),
  { k: "tag", path: "Avatar/Armature", tag: "Untagged" }, { k: "tag", path: "Avatar/Armature/Hips/Spine", tag: "Untagged" },
  { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, { k: "tag", path: "Avatar/Hoodie (1)", tag: "EditorOnly" }, { k: "tag", path: "Avatar/Body", tag: "MainCamera" }, frame(),
  { k: "tag", path: "Avatar", tag: "EditorOnly" }, frame(), { k: "tag", path: "Avatar", tag: "Untagged" },
  { k: "build", kind: "test" }, { k: "advance", ms: 900 }, frame(), { k: "advance", ms: 4500 }, frame(),
  { k: "delete", path: "Avatar" }, { k: "openSdk" }, { k: "build", kind: "upload" }, frame(), { k: "undo" }, { k: "openSdk" }, frame(),
] });
// Undo の上限(50。51 個目で最も古いものを捨てる)と、AssignMaterial の番号の境目(枚数ちょうどは末尾に足す)
cases.push({ name: "undo-limit", detail: "light", ops: [
  ...Array.from({ length: 55 }, (_, i) => ({ k: "active", path: "Avatar/Body", on: i % 2 === 1 })),
  ...Array.from({ length: 53 }, () => ({ k: "undo" })),
] });
cases.push({ name: "material-slot-boundary", ops: [
  { k: "select", path: "Avatar/Body" }, { k: "material", path: "Avatar/Body", slot: 1, material: "Hoodie_Red" }, { k: "material", path: "Avatar/Body", slot: 2, material: "Hoodie_lilToon" },
  { k: "material", path: "Avatar/Body", slot: 3, material: "Hoodie_Red" }, { k: "material", path: "Avatar/Body", slot: 0, material: "Hoodie_Standard" }, { k: "frame", dt: 16, full: true },
  { k: "undo" }, { k: "undo" }, { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, { k: "select", path: "Avatar/Hoodie/Hoodie_Mesh" },
  { k: "material", path: "Avatar/Hoodie/Hoodie_Mesh", slot: 1, material: "Hoodie_Red" }, { k: "material", path: "Avatar/Hoodie/Hoodie_Mesh", slot: 0, material: "Hoodie_Tex" },
  { k: "material", path: "Avatar/Hoodie/Hoodie_Mesh", slot: 2, material: "Nothing" }, { k: "dropAsset", asset: "Hoodie_Red", target: { kind: "slot", slot: 2 } }, { k: "dropAsset", asset: "Hoodie_Red", target: { kind: "slot", slot: 3 } },
] });
// 視点・ギズモの確かめの共通の準備
function scratch(setup) {
  const h = createHarness({ name: "scratch", ops: [], rect: RECT, client: CLIENT });
  for (const op of setup) apply(h, op);
  return h;
}
const THREE_PROJECT = (h, world) => {
  const v = world.clone().project(h.ed.camera);
  return { x: (v.x + 1) / 2 * RECT.width, y: (1 - v.y) / 2 * RECT.height };
};
function handleScreen(h) {
  const ed = h.ed;
  const camPos = ed.camera.position;
  return ed.handles.map(hit => {
    hit.geometry.computeBoundingSphere();
    let world;
    if (hit.userData.handle.kind === "rotate") {
      // 輪の中心線(半径 0.9 の円。ローカルの XZ 平面)の上の、カメラにいちばん近い点
      let best = null;
      for (let k = 0; k < 16; k++) {
        const u = (k / 16) * Math.PI * 2;
        const w = new THREE.Vector3(0.9 * Math.cos(u), 0, 0.9 * Math.sin(u)).applyMatrix4(hit.matrixWorld);
        if (!best || w.distanceTo(camPos) < best.distanceTo(camPos)) best = w;
      }
      world = best;
    } else world = hit.geometry.boundingSphere.center.clone().applyMatrix4(hit.matrixWorld);
    return { handle: hit.userData.handle, ...THREE_PROJECT(h, world) };
  });
}
const VARIANTS = [
  (x, y) => [{ k: "sceneDown", button: 0, x, y }, { k: "sceneMove", x: x + 3, y: y + 1 }, { k: "sceneMove", x: x + 20, y: y - 8 }, { k: "sceneMove", x: x + 45, y: y + 12 }, { k: "sceneUp", x: x + 45, y: y + 12 }, frame()],
  (x, y) => [{ k: "sceneDown", button: 0, x, y }, { k: "sceneMove", x: x + 0.5, y: y + 0.5 }, { k: "sceneUp", x: x + 0.5, y: y + 0.5 }, frame(), { k: "undo" }, frame()],
  (x, y) => [{ k: "sceneDown", button: 0, x, y }, { k: "sceneMove", x: x - 60, y: y + 30 }, { k: "sceneUp", x: x - 60, y: y + 30 }, frame(), { k: "undo" }, frame()],
];
/** ギズモの矢印・輪の 1 つずつ(ハンドルの数 × 操作の 3 通り)を、別のケースにする(ドラッグで対象が動くと、次のハンドルの位置が変わるため) */
function gizmoCases(name, nodePath, tool, extraSetup = []) {
  const setup = [{ k: "reset" }, ...extraSetup, { k: "select", path: nodePath }, { k: "tool", tool }, { k: "frameSel" }, frame()];
  const pts = handleScreen(scratch(setup));
  const out = [];
  pts.forEach((p, i) => VARIANTS.forEach((v, j) => out.push({ name: `${name}-h${i}-v${j}`, detail: "light", ops: [...setup, { k: "pick", x: p.x, y: p.y }, ...v(p.x, p.y), { k: "pick", x: p.x, y: p.y }, { k: "frame", dt: 16, full: true }] })));
  return out;
}
for (const tool of ["move", "rotate", "scale"]) {
  cases.push(...gizmoCases(`gizmo-${tool}-avatar`, "Avatar", tool));
  cases.push(...gizmoCases(`gizmo-${tool}-bone`, "Avatar/Armature/Hips/Spine", tool, [{ k: "expand", path: "Avatar", on: true }]));
}
cases.push(...gizmoCases("gizmo-move-child-after-parent-edit", "Avatar/Body", "move", [{ k: "edit", path: "Avatar", prop: "r", axis: 1, value: 40 }, { k: "edit", path: "Avatar", prop: "s", axis: 0, value: 1.5 }]));
cases.push(...gizmoCases("gizmo-rotate-child-of-rotated-parent", "Avatar/Armature/Hips/Spine/Chest", "rotate", [{ k: "edit", path: "Avatar", prop: "r", axis: 1, value: 30 }, { k: "edit", path: "Avatar/Armature/Hips", prop: "r", axis: 2, value: 20 }]));
// 視点の操作
cases.push({ name: "view-drags", wheelNeedsFocus: true, ops: [
  { k: "select", path: "Avatar" }, frame(),
  { k: "sceneDown", button: 0, alt: true, x: 300, y: 200 }, { k: "sceneMove", x: 330, y: 190 }, { k: "sceneMove", x: 400, y: 150 }, { k: "sceneUp", x: 400, y: 150 }, frame(),
  { k: "sceneDown", button: 1, x: 300, y: 200 }, { k: "sceneMove", x: 320, y: 210 }, { k: "sceneMove", x: 360, y: 260 }, { k: "sceneUp", x: 360, y: 260, button: 1 }, frame(),
  { k: "sceneDown", button: 2, x: 300, y: 200 }, { k: "sceneMove", x: 280, y: 190 }, { k: "sceneMove", x: 200, y: 120 }, { k: "sceneUp", x: 200, y: 120, button: 2 }, frame(),
  { k: "tool", tool: "hand" }, { k: "sceneDown", button: 0, x: 300, y: 200 }, { k: "sceneMove", x: 330, y: 230 }, { k: "sceneUp", x: 330, y: 230 }, frame(),
  { k: "sceneDown", button: 0, x: 300, y: 200 }, { k: "sceneMove", x: 301, y: 200 }, { k: "sceneUp", x: 301, y: 200 }, frame(),
  { k: "wheel", deltaY: 100, focused: false }, { k: "wheel", deltaY: 100, focused: true }, { k: "wheel", deltaY: -100, focused: true }, { k: "wheel", deltaY: -100, focused: true }, frame(),
  { k: "sceneDown", button: 3, x: 10, y: 10 }, { k: "sceneUp", x: 10, y: 10, button: 3 }, { k: "sceneDown", button: 0, x: 300, y: 200 }, { k: "sceneUp", x: 300, y: 200 }, frame(),
  { k: "resize", rect: { left: 20, top: 10, width: 500.5, height: 300.75 }, client: { w: 500, h: 301 } }, frame(), { k: "pick", x: 250, y: 150 }, { k: "sceneDown", button: 0, x: 250, y: 150 }, { k: "sceneUp", x: 250, y: 150 }, frame(),
  { k: "resize", rect: { left: 20, top: 10, width: 0, height: 0 }, client: { w: 0, h: 0 } }, frame(),
] });
// 画面のクリックで選ぶ(格子状に)
{
  const ops = [{ k: "expand", path: "Avatar", on: true }, { k: "instantiate", asset: "Hoodie", parent: "Avatar" }, { k: "frameSel" }, frame()];
  for (let y = 40; y < 400; y += 60) for (let x = 60; x < 700; x += 80) ops.push({ k: "sceneDown", button: 0, x, y }, { k: "sceneUp", x, y }, { k: "pick", x: x + 7, y: y + 3 });
  ops.push({ k: "active", path: "Avatar/Body", on: false }, frame());
  for (let y = 100; y < 400; y += 100) for (let x = 200; x < 520; x += 80) ops.push({ k: "pick", x, y });
  ops.push({ k: "frame", dt: 16, full: true });
  cases.push({ name: "scene-click-grid", detail: "light", ops });
}

/** 乱数 */
function randomWalk(seed, length) {
  const r = rng(seed);
  const pick = list => list[Math.floor(r() * list.length)];
  const ops = [];
  const maybeParent = () => (r() < 0.2 ? null : pick(PATHS));
  const xy = () => ({ x: Math.round(r() * 711), y: Math.round(r() * 400) });
  for (let i = 0; i < length; i++) {
    const t = r();
    if (t < 0.12) ops.push({ k: "select", path: pick(PATHS) });
    else if (t < 0.18) ops.push({ k: "expand", path: pick(PATHS), on: r() < 0.7 });
    else if (t < 0.23) ops.push({ k: "instantiate", asset: pick(["Hoodie", "Hoodie", "Avatar"]), parent: maybeParent() });
    else if (t < 0.28) ops.push({ k: "reparent", path: pick(PATHS), parent: maybeParent() });
    else if (t < 0.31) ops.push({ k: "delete", path: pick(PATHS) });
    else if (t < 0.35) ops.push({ k: "active", path: pick(PATHS), on: r() < 0.5 });
    else if (t < 0.39) ops.push({ k: "tag", path: pick(PATHS), tag: pick(TAGS) });
    else if (t < 0.42) ops.push({ k: "material", path: pick(PATHS), slot: 0, material: pick(MATERIALS) });
    else if (t < 0.46) ops.push({ k: "addComponent", path: pick(PATHS), type: pick(COMPS) });
    else if (t < 0.5) ops.push({ k: "setupOutfit", path: pick(PATHS) });
    else if (t < 0.53) ops.push({ k: "tool", tool: pick(["hand", "move", "rotate", "scale"]) });
    else if (t < 0.57) ops.push({ k: "edit", path: pick(PATHS), prop: pick(["p", "r", "s"]), axis: Math.floor(r() * 3), value: pick([0, 0.5, 1, -1, 45, 2.5, 90, 0.001234, 123.456]), fromInput: r() < 0.3 });
    else if (t < 0.6) ops.push({ k: "frameSel" });
    else if (t < 0.62) ops.push({ k: "orbit", dyaw: pick([0.3, -0.5, 1.2]), dpitch: pick([0, 0.2, -2, 2]) });
    else if (t < 0.65) ops.push({ k: pick(["openSdk", "closeSdk", "openSdk"]) });
    else if (t < 0.68) { ops.push({ k: "build", kind: pick(["test", "upload"]) }, { k: "advance", ms: pick([100, 900, 1000]) }, frame(), { k: "advance", ms: pick([100, 4500]) }); }
    else if (t < 0.72) ops.push({ k: "undo" });
    else if (t < 0.76) ops.push({ k: "key", code: pick(KEYS), ctrl: r() < 0.2, shift: r() < 0.1 });
    else if (t < 0.79) { ops.push({ k: "ctxByName", path: pick(PATHS) }, { k: "ctxItem", item: pick(["setup-outfit", "delete", "setup-outfit"]) }); }
    else if (t < 0.81) { ops.push({ k: "openAdd" }, { k: "addSearch", text: pick(["", "ma", "phys", "x", "light"]) }, { k: pick(["addEnter", "closePopup", "addEscape"]) }); }
    else if (t < 0.83) ops.push({ k: "openMenu", name: pick(["VRChat SDK", "File", "Tools"]) });
    else if (t < 0.86) ops.push({ k: "dropAsset", asset: pick(ASSETS), target: pick([{ kind: "root" }, { kind: "scene" }, { kind: "inspector" }, { kind: "slot", slot: 0 }, { kind: "node", path: pick(PATHS) }]), ...xy() });
    else if (t < 0.88) ops.push({ k: "dropNode", path: pick(PATHS), target: pick([{ kind: "root" }, { kind: "inspector" }, { kind: "node", path: pick(PATHS) }]) });
    else if (t < 0.9) ops.push({ k: "dblclick", path: pick(PATHS) });
    else if (t < 0.95) {
      const a = xy(), b = xy();
      ops.push({ k: "sceneDown", button: pick([0, 0, 0, 1, 2]), alt: r() < 0.2, ...a }, { k: "sceneMove", ...b }, { k: "sceneUp", ...b }, { k: "pick", ...xy() });
    } else if (t < 0.97) ops.push({ k: "wheel", deltaY: pick([100, -100]), focused: r() < 0.7 });
    else ops.push(frame());
  }
  ops.push({ k: "frame", dt: 16, full: true });
  return ops;
}
for (let i = 0; i < 12; i++) cases.push({ name: `random-${i + 1}`, detail: "light", wheelNeedsFocus: i % 3 === 0, practice: i % 3 !== 0, ops: randomWalk(1000 + i * 7919, 60) });

writeFileSync(path.join(here, "unity-cases.json"), JSON.stringify(cases));
console.log(`${cases.length} ケース、${cases.reduce((s, c) => s + c.ops.length, 0)} 操作`);
