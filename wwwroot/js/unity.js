// Unity エディターに似た練習用の画面（アバター作業向け）。Three.js で Scene ビューを描き、
// Hierarchy・Inspector・Project・VRChat SDK のパネルは HTML で作る。実際の Unity とは別物で、アバターに衣装を着せる流れを試すためのもの。
//
//   const ed = new UnityEditor(element, { scene: { instances: ["Avatar"] } });
//   ed.on("*", e => ...);   // 操作ごとにイベントが届く（viewport.js と同じ仕組み。tutorial.js からも使える）
//   ed.dispose();
//
// イベント（e.type と詳細）:
//   key {code, key, shift, ctrl, alt}   select {id, name}   expand {id, name, expanded}
//   instantiate {asset, id, name, parent, parentName}   reparent {id, name, parent, parentName}   delete {name}
//   contextmenu {id, name}   setupOutfit {id, name, ok, reason}   active {id, name, active}   tag {id, name, tag}
//   material {id, name, slot, material}   componentAdded {id, name, component}   edit {id, name, prop, axis, value}
//   tool {tool}   transform {id, name, kind, final}   view {action}   menu {menu}   sdk {open}
//   build {kind: "test" | "upload", merged, rank}   undo
//
// 人形は Three.js の基本形状で作った簡単なもの（実在のアバターのデータは使っていない）。
import * as THREE from "three";

const DEG = Math.PI / 180;

// ---------------------------------------------------------------- アセット（Project ウィンドウ）
export const ASSETS = {
  Avatar: { type: "prefab", label: "Avatar", note: "アバター（練習用の人形）" },
  Hoodie: { type: "prefab", label: "Hoodie", note: "衣装（パーカー）。Avatar 向け" },
  "Hoodie.fbx": { type: "model", label: "Hoodie.fbx", note: "衣装のモデル" },
  Hoodie_lilToon: { type: "material", label: "Hoodie_lilToon", color: 0x3c3f8f, shader: "lilToon" },
  Hoodie_Red: { type: "material", label: "Hoodie_Red", color: 0xb8323a, shader: "lilToon" },
  Hoodie_Standard: { type: "material", label: "Hoodie_Standard", color: 0xb9b9b9, shader: "Standard", hidden: true },
  Hoodie_Tex: { type: "texture", label: "Hoodie_Tex", note: "テクスチャ（2048×2048）" },
  Body_lilToon: { type: "material", label: "Body_lilToon", color: 0xf1d0bf, shader: "lilToon", hidden: true },
  Face_lilToon: { type: "material", label: "Face_lilToon", color: 0xf1d0bf, shader: "lilToon", hidden: true },
  Shirt_Default: { type: "material", label: "Shirt_Default", color: 0x8fb8de, shader: "lilToon", hidden: true },
};

// 「Add Component」で追加できるもの
export const COMPONENTS = [
  { type: "MABlendshapeSync", label: "MA Blendshape Sync", path: "Modular Avatar" },
  { type: "MAMergeArmature", label: "MA Merge Armature", path: "Modular Avatar" },
  { type: "MABoneProxy", label: "MA Bone Proxy", path: "Modular Avatar" },
  { type: "MAMeshSettings", label: "MA Mesh Settings", path: "Modular Avatar" },
  { type: "MAMenuItem", label: "MA Menu Item", path: "Modular Avatar" },
  { type: "VRCPhysBone", label: "VRC Phys Bone", path: "VRChat" },
  { type: "VRCContactReceiver", label: "VRC Contact Receiver", path: "VRChat" },
  { type: "BoxCollider", label: "Box Collider", path: "Physics" },
  { type: "AudioSource", label: "Audio Source", path: "Audio" },
  { type: "Light", label: "Light", path: "Rendering" },
];
const COMPONENT_LABEL = Object.fromEntries([
  ...COMPONENTS.map(c => [c.type, c.label]),
  ["Transform", "Transform"], ["SkinnedMeshRenderer", "Skinned Mesh Renderer"], ["Animator", "Animator"],
  ["VRCAvatarDescriptor", "VRC Avatar Descriptor"], ["PipelineManager", "Pipeline Manager"], ["Camera", "Camera"],
]);

// ---------------------------------------------------------------- 人形のボーン（Unity は Y が上、アバターは +Z を向く）
const BONES = [
  ["Hips", null, [0, 0.95, 0]],
  ["Spine", "Hips", [0, 0.1, 0]],
  ["Chest", "Spine", [0, 0.15, 0]],
  ["Neck", "Chest", [0, 0.2, 0]],
  ["Head", "Neck", [0, 0.08, 0]],
  ["LeftUpperArm", "Chest", [0.18, 0.15, 0]],
  ["LeftLowerArm", "LeftUpperArm", [0.26, 0, 0]],
  ["LeftHand", "LeftLowerArm", [0.24, 0, 0]],
  ["RightUpperArm", "Chest", [-0.18, 0.15, 0]],
  ["RightLowerArm", "RightUpperArm", [-0.26, 0, 0]],
  ["RightHand", "RightLowerArm", [-0.24, 0, 0]],
  ["LeftUpperLeg", "Hips", [0.09, -0.05, 0]],
  ["LeftLowerLeg", "LeftUpperLeg", [0, -0.42, 0]],
  ["LeftFoot", "LeftLowerLeg", [0, -0.4, 0]],
  ["RightUpperLeg", "Hips", [-0.09, -0.05, 0]],
  ["RightLowerLeg", "RightUpperLeg", [0, -0.42, 0]],
  ["RightFoot", "RightLowerLeg", [0, -0.4, 0]],
];
// 衣装のアーマチュアは上半身だけ（同じ名前のボーン）
const OUTFIT_BONES = ["Hips", "Spine", "Chest", "LeftUpperArm", "LeftLowerArm", "RightUpperArm", "RightLowerArm"];

/** メッシュの形（部品をボーンに付ける）。[ボーン名, 形, 大きさ, 位置, 向き] */
const MESH_PARTS = {
  body: [
    ["Head", "sphere", [0.1], [0, 0.1, 0.005]],
    ["Neck", "capsule", [0.04, 0.06], [0, 0.03, 0]],
    ["Chest", "capsule", [0.12, 0.12], [0, 0.05, 0]],
    ["Spine", "capsule", [0.11, 0.08], [0, 0.04, 0]],
    ["Hips", "capsule", [0.12, 0.06], [0, 0.01, 0]],
    ["LeftUpperArm", "capsule", [0.042, 0.18], [0.13, 0, 0], "x"],
    ["LeftLowerArm", "capsule", [0.036, 0.17], [0.12, 0, 0], "x"],
    ["LeftHand", "sphere", [0.045], [0.04, 0, 0]],
    ["RightUpperArm", "capsule", [0.042, 0.18], [-0.13, 0, 0], "x"],
    ["RightLowerArm", "capsule", [0.036, 0.17], [-0.12, 0, 0], "x"],
    ["RightHand", "sphere", [0.045], [-0.04, 0, 0]],
    ["LeftUpperLeg", "capsule", [0.06, 0.3], [0, -0.21, 0]],
    ["LeftLowerLeg", "capsule", [0.048, 0.3], [0, -0.2, 0]],
    ["LeftFoot", "box", [0.08, 0.05, 0.18], [0, -0.03, 0.04]],
    ["RightUpperLeg", "capsule", [0.06, 0.3], [0, -0.21, 0]],
    ["RightLowerLeg", "capsule", [0.048, 0.3], [0, -0.2, 0]],
    ["RightFoot", "box", [0.08, 0.05, 0.18], [0, -0.03, 0.04]],
    ["Head", "eye", [0.014], [0.035, 0.11, 0.092]],
    ["Head", "eye", [0.014], [-0.035, 0.11, 0.092]],
    ["Head", "hair", [0.108], [0, 0.125, -0.006]],
  ],
  shirt: [
    ["Chest", "capsule", [0.13, 0.12], [0, 0.05, 0]],
    ["Spine", "capsule", [0.122, 0.09], [0, 0.03, 0]],
    ["LeftUpperArm", "capsule", [0.05, 0.08], [0.07, 0, 0], "x"],
    ["RightUpperArm", "capsule", [0.05, 0.08], [-0.07, 0, 0], "x"],
  ],
  hoodie: [
    ["Chest", "capsule", [0.142, 0.13], [0, 0.05, 0]],
    ["Spine", "capsule", [0.134, 0.1], [0, 0.03, 0]],
    ["Hips", "capsule", [0.132, 0.04], [0, 0.02, 0]],
    ["Chest", "hood", [0.1], [0, 0.22, -0.07]],
    ["LeftUpperArm", "capsule", [0.056, 0.19], [0.13, 0, 0], "x"],
    ["LeftLowerArm", "capsule", [0.05, 0.17], [0.12, 0, 0], "x"],
    ["RightUpperArm", "capsule", [0.056, 0.19], [-0.13, 0, 0], "x"],
    ["RightLowerArm", "capsule", [0.05, 0.17], [-0.12, 0, 0], "x"],
  ],
};

// パフォーマンスランクの目安（PC）。Excellent / Good / Medium / Poor の上限
const RANKS = ["Excellent", "Good", "Medium", "Poor", "Very Poor"];
const LIMITS = {
  tris: [32000, 70000, 70000, 70000],
  materials: [4, 8, 16, 32],
  skinned: [1, 2, 8, 16],
  bones: [75, 150, 256, 400],
  physbones: [4, 8, 16, 32],
};

let nextId = 1;
const newId = () => `n${nextId++}`;
// 取り消し用の複製。3D のオブジェクト（obj / parts）は含めない（作り直す）
const clone = v => JSON.parse(JSON.stringify(v, (k, x) => (k === "obj" || k === "parts" ? undefined : x)));
const esc = s => String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

// ---------------------------------------------------------------- Prefab（ノードの一覧を作る）
function node(name, parent, extra = {}) {
  return { id: newId(), name, parent, children: [], active: true, tag: "Untagged", expanded: false, p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1], components: [], ...extra };
}

function armatureNodes(list, parentId, boneNames) {
  const arm = node("Armature", parentId);
  list.push(arm);
  const byName = {};
  for (const [name, parent, p] of BONES) {
    if (!boneNames.includes(name)) continue;
    const n = node(name, parent ? byName[parent].id : arm.id, { p: [...p], bone: true });
    byName[name] = n;
    list.push(n);
  }
  return arm;
}

function smr(materials, blendshapes, tris) {
  return { type: "SkinnedMeshRenderer", materials, blendshapes, tris };
}

const PREFABS = {
  Avatar: () => {
    const list = [];
    const root = node("Avatar", null, {
      prefab: "Avatar", expanded: false,
      components: [
        { type: "Animator", avatar: "AvatarAvatar（Humanoid）" },
        { type: "VRCAvatarDescriptor", view: [0, 1.52, 0.07] },
        { type: "PipelineManager", blueprintId: "" },
      ],
    });
    list.push(root);
    armatureNodes(list, root.id, BONES.map(b => b[0]));
    list.push(node("Body", root.id, { mesh: "body", components: [smr(["Body_lilToon", "Face_lilToon"], { Breast_big: 0, Shrink_Upper: 0 }, 22000)] }));
    list.push(node("Shirt_Default", root.id, { mesh: "shirt", components: [smr(["Shirt_Default"], {}, 4800)] }));
    return list;
  },
  Hoodie: () => {
    const list = [];
    const root = node("Hoodie", null, { prefab: "Hoodie" });
    list.push(root);
    armatureNodes(list, root.id, OUTFIT_BONES);
    list.push(node("Hoodie_Mesh", root.id, { mesh: "hoodie", components: [smr(["Hoodie_Standard"], { Breast_big: 0 }, 9600)] }));
    return list;
  },
};

/** Unity の新しいシーンと同じく、カメラとライトを置く */
function defaultSceneNodes() {
  return [
    node("Main Camera", null, { p: [0, 1, -10], icon: "camera", components: [{ type: "Camera" }] }),
    node("Directional Light", null, { p: [0, 3, 0], r: [50, -30, 0], icon: "light", components: [{ type: "Light" }] }),
  ];
}

export class UnityEditor {
  /**
   * @param {HTMLElement} root
   * @param {object} options
   *   scene: { instances: ["Avatar", ...] }  最初に置く Prefab（既定は Avatar）
   *   wheelNeedsFocus: boolean
   */
  constructor(root, options = {}) {
    this.root = root;
    this.options = options;
    this.listeners = new Map();
    this.silent = false;
    this.abort = new AbortController();
    this.nodes = new Map();
    this.roots = [];
    this.selected = null;
    this.tool = "move";
    this.undoStack = [];
    this.highlight = {};
    this.view = { target: new THREE.Vector3(0, 0.95, 0), dist: 3.2, yaw: 0.45, pitch: 0.18 };
    this.mouse = { x: 0, y: 0 };

    root.classList.add("unity-editor");
    root.tabIndex = 0;
    root.setAttribute("aria-label", "Unity エディター（練習）");
    root.innerHTML = `
      <div class="ue-menubar">${["File", "Edit", "Assets", "GameObject", "Component", "VRChat SDK", "Tools", "Window"].map(m => `<button type="button" data-menu="${m}">${m}</button>`).join("")}</div>
      <div class="ue-toolbar">
        ${[["hand", "Q", "bi-hand-index"], ["move", "W", "bi-arrows-move"], ["rotate", "E", "bi-arrow-repeat"], ["scale", "R", "bi-arrows-angle-expand"]].map(([t, k, i]) =>
          `<button type="button" data-tool="${t}" title="${k}"><i class="bi ${i}" aria-hidden="true"></i><span class="visually-hidden">${t}</span></button>`).join("")}
        <span class="ue-toolbar-sep"></span><span class="ue-toolbar-label">Pivot</span><span class="ue-toolbar-label">Global</span>
        <span class="ue-play"><i class="bi bi-play-fill"></i><i class="bi bi-pause-fill"></i><i class="bi bi-skip-end-fill"></i></span>
      </div>
      <section class="ue-panel ue-hierarchy"><div class="ue-tab"><i class="bi bi-list-nested"></i> Hierarchy</div><div class="ue-hier-list" role="tree"></div></section>
      <section class="ue-panel ue-scene"><div class="ue-tab"><i class="bi bi-grid-3x3"></i> Scene</div><div class="ue-scene-view"><div class="ue-scene-info"></div><div class="ue-preview" hidden></div></div></section>
      <section class="ue-panel ue-inspector"><div class="ue-tab"><i class="bi bi-info-circle"></i> Inspector</div><div class="ue-insp-body"></div></section>
      <section class="ue-panel ue-project"><div class="ue-tab"><i class="bi bi-folder2"></i> Project</div><div class="ue-proj-path">Assets</div><div class="ue-proj-grid"></div></section>
      <div class="ue-status"></div>
      <div class="ue-popup" hidden></div>
      <div class="ue-sdk" hidden></div>
      <div class="ue-hl-label" hidden></div>`;
    const q = s => root.querySelector(s);
    this.el = {
      hier: q(".ue-hier-list"), scene: q(".ue-scene-view"), insp: q(".ue-insp-body"), proj: q(".ue-proj-grid"),
      status: q(".ue-status"), popup: q(".ue-popup"), sdk: q(".ue-sdk"), hlLabel: q(".ue-hl-label"), info: q(".ue-scene-info"), preview: q(".ue-preview"),
    };

    // Scene ビュー
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.autoClear = false;
    this.el.scene.prepend(this.renderer.domElement);
    this.scene = new THREE.Scene();
    this.scene.background = skyTexture();
    this.camera = new THREE.PerspectiveCamera(50, 1, 0.05, 200);
    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x5a5a5a, 1.3));
    const sun = new THREE.DirectionalLight(0xffffff, 1.4);
    sun.position.set(2, 4, 3);
    this.scene.add(sun);
    const grid = new THREE.GridHelper(20, 20, 0x8a8a8a, 0x6f6f6f);
    grid.material.transparent = true;
    grid.material.opacity = 0.55;
    this.scene.add(grid);
    this.world = new THREE.Group();
    this.scene.add(this.world);
    this.gizmoScene = new THREE.Scene();
    this.gizmo = new THREE.Group();
    this.gizmoScene.add(this.gizmo);

    this.bind();
    this.renderProject();
    this.resetScene(options.scene);
    this.resize();
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(this.el.scene);
    this.onScreen = true;
    this.visibility = new IntersectionObserver(es => { this.onScreen = es[es.length - 1].isIntersecting; });
    this.visibility.observe(root);
    this.animate();
  }

  // ---------------------------------------------------------------- イベント
  on(type, fn) {
    if (!this.listeners.has(type)) this.listeners.set(type, new Set());
    this.listeners.get(type).add(fn);
    return () => this.listeners.get(type).delete(fn);
  }

  emit(type, detail = {}) {
    if (this.silent) return;
    const e = { type, ...detail };
    for (const fn of [...(this.listeners.get(type) ?? []), ...(this.listeners.get("*") ?? [])]) fn(e, this);
  }

  dispose() {
    cancelAnimationFrame(this.raf);
    clearTimeout(this.previewTimer);
    this.abort.abort();
    this.resizeObserver.disconnect();
    this.visibility.disconnect();
    for (const s of [this.scene, this.gizmoScene]) s.traverse(o => { o.geometry?.dispose(); o.material?.dispose?.(); });
    this.scene.background?.dispose?.();
    this.renderer.dispose();
    this.renderer.forceContextLoss();
    this.root.replaceChildren();
    this.root.classList.remove("unity-editor");
    this.listeners.clear();
  }

  setStatus(text) { this.el.status.textContent = text; }

  // ---------------------------------------------------------------- シーン（ノード）
  resetScene(scene = this.options.scene ?? { instances: ["Avatar"] }) {
    this.closePopup();
    this.closeSDK(true);
    this.stopPreview();
    this.nodes = new Map();
    this.roots = [];
    for (const n of defaultSceneNodes()) this.addNode(n);
    for (const name of scene.instances ?? []) this.addNodes(PREFABS[name]());
    this.selected = null;
    this.undoStack = [];
    this.tool = "move";
    Object.assign(this.view, { dist: 3.2, yaw: 0.45, pitch: 0.18 });
    this.view.target.set(0, 0.95, 0);
    this.rebuild();
    this.render();
    this.setStatus("Hierarchy や Scene でクリックして選択。Project からドラッグして追加");
  }

  addNode(n) {
    this.nodes.set(n.id, n);
    if (n.parent) this.nodes.get(n.parent).children.push(n.id);
    else this.roots.push(n.id);
  }

  addNodes(list) { for (const n of list) this.addNode(n); return list[0]; }

  get(id) { return this.nodes.get(id) ?? null; }

  /** 名前で探す（上から順に最初に見つかったもの）。"Hoodie/Armature" のように親の名前で絞れる */
  findByName(path) {
    const parts = path.split("/");
    const walk = ids => {
      for (const id of ids) {
        const n = this.get(id);
        if (n.name === parts[parts.length - 1] && this.pathMatches(n, parts)) return n;
        const c = walk(n.children);
        if (c) return c;
      }
      return null;
    };
    return walk(this.roots);
  }

  pathMatches(n, parts) {
    for (let i = parts.length - 2, p = this.get(n.parent); i >= 0; i--, p = p && this.get(p.parent)) {
      if (!p || p.name !== parts[i]) return false;
    }
    return true;
  }

  ancestors(n) {
    const out = [];
    for (let p = this.get(n.parent); p; p = this.get(p.parent)) out.push(p);
    return out;
  }

  /** アバターのルート（VRC Avatar Descriptor を持つもの） */
  avatarRootOf(n) {
    return [n, ...this.ancestors(n)].find(x => x.components.some(c => c.type === "VRCAvatarDescriptor")) ?? null;
  }

  isEffectivelyActive(n) { return n.active && this.ancestors(n).every(a => a.active); }

  snapshot() { return { nodes: clone([...this.nodes.values()]), roots: [...this.roots], selected: this.selected }; }

  pushUndo() {
    this.undoStack.push(this.snapshot());
    if (this.undoStack.length > 50) this.undoStack.shift();
  }

  undo() {
    const s = this.undoStack.pop();
    if (!s) { this.setStatus("これ以上元に戻せません"); return; }
    this.nodes = new Map(s.nodes.map(n => [n.id, n]));
    this.roots = s.roots;
    this.selected = this.nodes.has(s.selected) ? s.selected : null;
    this.rebuild();
    this.render();
    this.setStatus("元に戻す（Undo）");
    this.emit("undo");
  }

  // ---------------------------------------------------------------- 3D（ノードから作り直す）
  rebuild() {
    this.world.traverse(o => { o.geometry?.dispose(); o.material?.dispose?.(); });
    this.world.clear();
    const build = (id, parentObj) => {
      const n = this.get(id);
      const g = new THREE.Group();
      g.name = n.name;
      g.userData.nodeId = n.id;
      this.applyTransform(n, g);
      parentObj.add(g);
      n.obj = g;
      for (const c of n.children) build(c, g);
    };
    for (const id of this.roots) build(id, this.world);
    for (const n of this.nodes.values()) {
      if (!n.mesh) continue;
      const comp = n.components.find(c => c.type === "SkinnedMeshRenderer");
      const color = ASSETS[comp?.materials[0]]?.color ?? 0xff00ff;
      n.parts = [];
      for (const [bone, shape, size, pos, axis] of MESH_PARTS[n.mesh]) {
        const b = this.boneFor(n, bone);
        if (!b) continue;
        const m = makePart(shape, size, shape === "eye" ? 0x2b2b3a : shape === "hair" ? 0x5b3b2b : color);
        m.position.fromArray(pos);
        if (axis === "x") m.rotation.z = Math.PI / 2;
        m.userData.nodeId = n.id;
        b.obj.add(m);
        n.parts.push(m);
      }
    }
    this.updateVisibility();
    this.updateGizmo();
  }

  /** メッシュが使うボーン: 同じ親の下の Armature から名前で探す */
  boneFor(meshNode, boneName) {
    const parent = this.get(meshNode.parent);
    const armId = parent?.children.find(id => this.get(id).name === "Armature");
    if (!armId) return null;
    const walk = id => {
      const n = this.get(id);
      if (n.name === boneName && n.bone) return n;
      for (const c of n.children) { const r = walk(c); if (r) return r; }
      return null;
    };
    return walk(armId);
  }

  applyTransform(n, obj = n.obj) {
    obj.position.fromArray(n.p);
    obj.rotation.set(n.r[0] * DEG, n.r[1] * DEG, n.r[2] * DEG, "YXZ");
    obj.scale.fromArray(n.s);
  }

  readTransform(n) {
    const r = n => Math.round(n * 1000) / 1000;
    n.p = n.obj.position.toArray().map(r);
    const e = new THREE.Euler().setFromQuaternion(n.obj.quaternion, "YXZ");
    n.r = [e.x, e.y, e.z].map(a => Math.round(a / DEG * 100) / 100);
    n.s = n.obj.scale.toArray().map(r);
  }

  updateVisibility() {
    for (const n of this.nodes.values()) {
      if (!n.parts) continue;
      const on = this.isEffectivelyActive(n);
      for (const p of n.parts) p.visible = on;
    }
  }

  // ---------------------------------------------------------------- 操作（チュートリアルのスキップからも呼ぶ）
  select(id, source = "hierarchy") {
    this.selected = id;
    const n = this.get(id);
    // 選んだものが見えるように、親を開く
    if (n) for (const a of this.ancestors(n)) a.expanded = true;
    this.closePopup();
    this.render();
    this.updateGizmo();
    this.emit("select", { id, name: n?.name ?? null, source });
  }

  selectByName(path) { const n = this.findByName(path); if (n) this.select(n.id); }

  setExpanded(id, expanded) {
    const n = this.get(id);
    if (!n || !n.children.length) return;
    n.expanded = expanded;
    this.keepFocus(() => this.renderHierarchy());
    this.emit("expand", { id, name: n.name, expanded });
  }

  expandByName(path, expanded = true) { const n = this.findByName(path); if (n) this.setExpanded(n.id, expanded); }

  /** Prefab を置く（parentId を省くとルート） */
  instantiate(asset, parentId = null) {
    if (!PREFABS[asset]) return null;
    this.pushUndo();
    const list = PREFABS[asset]();
    const root = list[0];
    root.parent = parentId;
    const siblings = (parentId ? this.get(parentId).children : this.roots).map(id => this.get(id).name);
    if (siblings.includes(root.name)) root.name = `${root.name} (${siblings.filter(s => s.startsWith(root.name)).length})`;
    this.addNodes(list);
    if (parentId) this.get(parentId).expanded = true;
    this.rebuild();
    this.select(root.id);
    const parent = this.get(parentId);
    this.setStatus(`${root.name} を${parent ? `「${parent.name}」の中に` : ""}追加しました`);
    this.emit("instantiate", { asset, id: root.id, name: root.name, parent: parentId, parentName: parent?.name ?? null });
    return root;
  }

  /** 親を変える（ワールドでの位置はそのまま。Unity と同じ） */
  reparent(id, parentId) {
    const n = this.get(id);
    if (!n || n.parent === parentId || id === parentId) return;
    if (parentId && [this.get(parentId), ...this.ancestors(this.get(parentId))].some(a => a.id === id)) {
      this.setStatus("自分の子の中には入れられません");
      return;
    }
    this.pushUndo();
    const list = n.parent ? this.get(n.parent).children : this.roots;
    list.splice(list.indexOf(id), 1);
    n.parent = parentId;
    (parentId ? this.get(parentId).children : this.roots).push(id);
    (parentId ? this.get(parentId).obj : this.world).attach(n.obj);
    this.readTransform(n);
    if (parentId) this.get(parentId).expanded = true;
    this.rebuild();
    this.render();
    const parent = this.get(parentId);
    this.setStatus(`${n.name} を${parent ? `「${parent.name}」の子に` : "ルートに"}移しました`);
    this.emit("reparent", { id, name: n.name, parent: parentId, parentName: parent?.name ?? null });
  }

  deleteNode(id) {
    const n = this.get(id);
    if (!n) return;
    this.pushUndo();
    const drop = x => { for (const c of this.get(x).children) drop(c); this.nodes.delete(x); };
    const list = n.parent ? this.get(n.parent).children : this.roots;
    list.splice(list.indexOf(id), 1);
    drop(id);
    if (!this.nodes.has(this.selected)) this.selected = null;
    this.rebuild();
    this.render();
    this.setStatus(`${n.name} を削除しました`);
    this.emit("delete", { name: n.name });
  }

  setActive(id, active) {
    const n = this.get(id);
    if (!n || n.active === active) return;
    this.pushUndo();
    n.active = active;
    this.updateVisibility();
    this.render();
    this.setStatus(`${n.name} を${active ? "有効" : "無効"}にしました`);
    this.emit("active", { id, name: n.name, active });
  }

  setTag(id, tag) {
    const n = this.get(id);
    if (!n || n.tag === tag) return;
    this.pushUndo();
    n.tag = tag;
    this.render();
    this.setStatus(tag === "EditorOnly" ? `${n.name} はアップロードに含まれなくなります（EditorOnly）` : `Tag: ${tag}`);
    this.emit("tag", { id, name: n.name, tag });
  }

  assignMaterial(id, slot, material) {
    const n = this.get(id);
    const comp = n?.components.find(c => c.type === "SkinnedMeshRenderer");
    if (!comp || ASSETS[material]?.type !== "material") return;
    this.pushUndo();
    comp.materials[slot] = material;
    this.rebuild();
    this.render();
    this.setStatus(`${n.name} の Element ${slot} に ${material} を設定しました`);
    this.emit("material", { id, name: n.name, slot, material });
  }

  addComponent(id, type) {
    const n = this.get(id);
    if (!n) return;
    if (n.components.some(c => c.type === type)) { this.setStatus(`${COMPONENT_LABEL[type]} はもう付いています`); return; }
    this.pushUndo();
    const comp = { type };
    if (type === "MABlendshapeSync") {
      // 同じ名前のブレンドシェイプを、アバターの Body から同期する
      const own = Object.keys(n.components.find(c => c.type === "SkinnedMeshRenderer")?.blendshapes ?? {});
      const avatar = this.avatarRootOf(n);
      const body = avatar && this.findByName(`${avatar.name}/Body`);
      const src = Object.keys(body?.components.find(c => c.type === "SkinnedMeshRenderer")?.blendshapes ?? {});
      comp.bindings = own.filter(b => src.includes(b)).map(b => ({ from: "Body", blendshape: b }));
    }
    if (type === "VRCPhysBone") Object.assign(comp, { root: n.name, pull: 0.2, spring: 0.2 });
    n.components.push(comp);
    this.closePopup();
    this.render();
    this.setStatus(`${n.name} に ${COMPONENT_LABEL[type]} を追加しました`);
    this.emit("componentAdded", { id, name: n.name, component: type });
  }

  /** Modular Avatar > Setup Outfit（衣装のアーマチュアに MA Merge Armature を付ける） */
  setupOutfit(id) {
    const n = this.get(id);
    this.closePopup();
    if (!n) return;
    const fail = reason => { this.setStatus(reason); this.emit("setupOutfit", { id, name: n.name, ok: false, reason }); };
    const avatar = this.avatarRootOf(this.get(n.parent) ?? n);
    if (!avatar || avatar.id === n.id) return fail("衣装をアバターの中（子）に入れてから Setup Outfit を実行してください");
    const armId = n.children.find(c => this.get(c).name === "Armature");
    if (!armId) return fail(`${n.name} の中に Armature が見つかりません（衣装の Prefab のルートで実行してください）`);
    const target = this.findByName(`${avatar.name}/Armature`);
    this.pushUndo();
    const arm = this.get(armId);
    if (!arm.components.some(c => c.type === "MAMergeArmature")) arm.components.push({ type: "MAMergeArmature", target: `${avatar.name}/Armature`, targetId: target?.id, prefix: "", suffix: "" });
    if (!n.components.some(c => c.type === "MAMeshSettings")) n.components.push({ type: "MAMeshSettings" });
    this.render();
    this.setStatus(`Setup Outfit: ${n.name}/Armature に MA Merge Armature を追加しました`);
    this.emit("setupOutfit", { id, name: n.name, ok: true });
  }

  setTool(tool) {
    this.tool = tool;
    this.updateGizmo();
    this.renderToolbar();
    this.emit("tool", { tool });
  }

  /** fromInput: Inspector の欄からの入力（表示はそのままでよいので作り直さない。続けて入力できるように） */
  setTransformValue(id, prop, axis, value, fromInput = false) {
    const n = this.get(id);
    if (!n || !Number.isFinite(value)) return;
    this.pushUndo();
    n[prop][axis] = value;
    this.applyTransform(n);
    this.updateGizmo();
    if (!fromInput) this.keepFocus(() => this.renderInspector());
    this.emit("edit", { id, name: n.name, prop, axis, value });
  }

  frameSelected() {
    const n = this.get(this.selected);
    const box = new THREE.Box3();
    if (n?.parts?.length) for (const p of n.parts) box.expandByObject(p);
    else if (n) box.setFromObject(n.obj);
    if (box.isEmpty()) {
      if (!n) { this.setStatus("先に Hierarchy で選択してください"); return; }
      box.setFromCenterAndSize(n.obj.getWorldPosition(new THREE.Vector3()), new THREE.Vector3(0.5, 0.5, 0.5));
    }
    this.view.target.copy(box.getCenter(new THREE.Vector3()));
    this.view.dist = Math.max(0.6, box.getSize(new THREE.Vector3()).length() * 1.4);
    this.setStatus(`${n.name} を表示（Frame Selected）`);
    this.emit("view", { action: "frame" });
  }

  orbitBy(dyaw, dpitch = 0) {
    this.view.yaw += dyaw;
    this.view.pitch = THREE.MathUtils.clamp(this.view.pitch + dpitch, -1.4, 1.4);
  }

  // ---------------------------------------------------------------- VRChat SDK（シミュレーション）
  openSDK() {
    this.closePopup();
    this.el.sdk.hidden = false;
    this.renderSDK();
    this.setStatus("VRChat SDK > Show Control Panel");
    this.emit("sdk", { open: true });
  }

  closeSDK(quiet = false) {
    if (this.el.sdk.hidden) return;
    this.keepFocus(() => { this.el.sdk.hidden = true; });
    if (!quiet) this.emit("sdk", { open: false });
  }

  /** アップロードされるアバターの集計（EditorOnly は除く。無効なだけのものは含む = VRChat と同じ） */
  stats() {
    const avatar = this.roots.map(id => this.get(id)).find(n => n.components.some(c => c.type === "VRCAvatarDescriptor"));
    if (!avatar) return null;
    const included = [];
    const walk = (id, editorOnly) => {
      const n = this.get(id);
      const eo = editorOnly || n.tag === "EditorOnly";
      if (!eo) included.push(n);
      for (const c of n.children) walk(c, eo);
    };
    walk(avatar.id, false);
    const smrs = included.map(n => n.components.find(c => c.type === "SkinnedMeshRenderer")).filter(Boolean);
    const merged = this.outfitMerged(avatar);
    // 統合される衣装のボーンは、同じ名前のアバターのボーンにまとまるので数えない
    const bones = included.filter(n => n.bone && !(merged.has(this.armatureOf(n)) && this.findByName(`${avatar.name}/Armature`) && this.avatarBoneNamed(avatar, n.name))).length;
    const values = {
      tris: smrs.reduce((s, c) => s + c.tris, 0),
      materials: smrs.reduce((s, c) => s + c.materials.length, 0),
      skinned: smrs.length,
      bones,
      physbones: included.reduce((s, n) => s + n.components.filter(c => c.type === "VRCPhysBone").length, 0),
    };
    const rankOf = (k, v) => { const i = LIMITS[k].findIndex(lim => v <= lim); return i < 0 ? 4 : i; };
    const ranks = Object.fromEntries(Object.entries(values).map(([k, v]) => [k, rankOf(k, v)]));
    const overall = Math.max(...Object.values(ranks));
    // 確認のメッセージ
    const notes = [];
    for (const n of included) {
      if (n.components.some(c => c.type === "SkinnedMeshRenderer") && !this.isEffectivelyActive(n)) {
        notes.push({ level: "info", text: `${n.name} は無効ですが、アップロードに含まれ、ランクの計算にも入ります。外すなら Tag を EditorOnly にしてください。` });
      }
      const mat = n.components.find(c => c.type === "SkinnedMeshRenderer")?.materials ?? [];
      if (mat.some(m => ASSETS[m]?.shader === "Standard")) notes.push({ level: "info", text: `${n.name} のマテリアルが Standard シェーダーです（VRChat では lilToon などのトゥーンシェーダーがよく使われます）。` });
    }
    for (const outfit of this.outfitsIn(avatar)) {
      if (!merged.has(this.get(outfit.children.find(c => this.get(c).name === "Armature")))) {
        notes.push({ level: "warn", text: `${outfit.name} のボーンがアバターに統合されません。衣装を右クリック > Modular Avatar > Setup Outfit を実行してください。` });
      }
    }
    return { avatar, values, ranks, overall, notes, merged };
  }

  /** アバターの中の衣装（Armature を持つ、アバター以外の Prefab） */
  outfitsIn(avatar) {
    const out = [];
    const walk = id => { const n = this.get(id); if (n.id !== avatar.id && n.children.some(c => this.get(c).name === "Armature")) out.push(n); for (const c of n.children) walk(c); };
    walk(avatar.id);
    return out;
  }

  /** MA Merge Armature でアバターに統合される衣装のアーマチュア */
  outfitMerged(avatar) {
    const set = new Set();
    for (const outfit of this.outfitsIn(avatar)) {
      const arm = this.get(outfit.children.find(c => this.get(c).name === "Armature"));
      if (arm?.components.some(c => c.type === "MAMergeArmature") && this.isEffectivelyActive(outfit)) set.add(arm);
    }
    return set;
  }

  armatureOf(bone) { return this.ancestors(bone).find(a => a.name === "Armature") ?? null; }

  avatarBoneNamed(avatar, name) {
    const arm = this.findByName(`${avatar.name}/Armature`);
    if (!arm) return null;
    const walk = id => { const n = this.get(id); if (n.bone && n.name === name) return n; for (const c of n.children) { const r = walk(c); if (r) return r; } return null; };
    return walk(arm.id);
  }

  /** Build & Test / Build and Upload（実際には何も送らない） */
  build(kind) {
    const st = this.stats();
    if (!st) { this.setStatus("VRC Avatar Descriptor の付いたアバターがありません"); return; }
    this.closeSDK(true);
    const label = kind === "test" ? "Build & Test" : "Build and Upload";
    this.showPreview(`${label}: ビルドしています…`, true);
    clearTimeout(this.previewTimer);
    this.previewTimer = setTimeout(() => {
      this.startPose(st);
      const followText = st.merged.size ? "衣装はアバターと一緒に動いています（Modular Avatar で統合済み）" : "衣装が体についてきていません（Setup Outfit をしていない）";
      this.showPreview(kind === "test"
        ? `テスト用に読み込みました（VRChat の「アバター > その他」）。ランク: ${RANKS[st.overall]}。${followText}`
        : `アップロードしました（練習なので実際には送信していません）。ランク: ${RANKS[st.overall]}`);
      this.setStatus(`${label} が完了しました（ランク: ${RANKS[st.overall]}）`);
      this.emit("build", { kind, merged: st.merged.size > 0, rank: RANKS[st.overall] });
      this.previewTimer = setTimeout(() => this.stopPreview(), 4500);
    }, 900);
  }

  showPreview(text, busy = false) {
    const p = this.el.preview;
    p.hidden = false;
    p.innerHTML = `<div class="ue-preview-box">${busy ? '<div class="ue-progress"><span></span></div>' : '<i class="bi bi-check-circle-fill"></i>'}<div>${esc(text)}</div></div>`;
  }

  /** 腕を振るポーズ。統合された衣装のボーンはアバターのボーンについていく */
  startPose(st) {
    this.pose = { start: performance.now(), avatar: st.avatar, merged: st.merged };
    this.updateGizmo();
  }

  stopPreview() {
    clearTimeout(this.previewTimer);
    this.el.preview.hidden = true;
    if (this.pose) { this.pose = null; this.rebuild(); }
  }

  updatePose() {
    const pose = this.pose;
    if (!pose) return;
    const t = (performance.now() - pose.start) / 1000;
    const swing = Math.sin(t * 3) * 0.6 + 0.5;
    const set = (name, z) => { const b = this.avatarBoneNamed(pose.avatar, name); if (b) b.obj.rotation.z = z; };
    set("LeftUpperArm", swing);
    set("RightUpperArm", -swing);
    set("LeftLowerArm", swing * 0.5);
    set("RightLowerArm", -swing * 0.5);
    // MA Merge Armature: 衣装のボーンを、同じ名前のアバターのボーンのワールド変換に合わせる
    this.world.updateMatrixWorld(true);
    for (const arm of pose.merged) {
      const walk = id => {
        const n = this.get(id);
        if (n.bone) {
          const src = this.avatarBoneNamed(pose.avatar, n.name);
          if (src) {
            const m = src.obj.matrixWorld.clone();
            const parentInv = n.obj.parent.matrixWorld.clone().invert();
            m.premultiply(parentInv).decompose(n.obj.position, n.obj.quaternion, n.obj.scale);
            n.obj.updateMatrixWorld(true);
          }
        }
        for (const c of n.children) walk(c);
      };
      walk(arm.id);
    }
  }

  // ---------------------------------------------------------------- 描画（パネル）
  render() {
    this.keepFocus(() => {
      this.renderToolbar();
      this.renderHierarchy();
      this.renderInspector();
      if (!this.el.sdk.hidden) this.renderSDK();
    });
    this.applyHighlight();
  }

  /** 中身を作り直すと、フォーカスのあったボタンなどが消えて画面の外に外れるので、エディターに戻す */
  keepFocus(fn) {
    const had = this.root.contains(document.activeElement);
    fn();
    const a = document.activeElement;
    // 消えた要素や、隠した要素（SDK の画面など）にフォーカスが残った場合も戻す
    if (had && (!this.root.contains(a) || (a !== this.root && a.offsetParent === null))) this.root.focus({ preventScroll: true });
  }

  renderToolbar() {
    for (const b of this.root.querySelectorAll("[data-tool]")) b.classList.toggle("is-on", b.dataset.tool === this.tool);
  }

  renderHierarchy() {
    const rows = [];
    const walk = (id, depth) => {
      const n = this.get(id);
      const hasKids = n.children.length > 0;
      const icon = n.icon === "camera" ? "bi-camera-video" : n.icon === "light" ? "bi-sun" : n.prefab ? "bi-box-fill" : n.mesh ? "bi-person" : n.bone ? "bi-dot" : "bi-box";
      rows.push(`<div class="ue-row${n.id === this.selected ? " is-selected" : ""}${this.isEffectivelyActive(n) ? "" : " is-inactive"}${n.prefab ? " is-prefab" : ""}"
        role="treeitem" aria-selected="${n.id === this.selected}" ${hasKids ? `aria-expanded="${n.expanded}"` : ""}
        data-node="${n.id}" data-node-name="${esc(n.name)}" style="padding-left:${4 + depth * 14}px">
        <span class="ue-fold" data-fold="${n.id}">${hasKids ? `<i class="bi ${n.expanded ? "bi-caret-down-fill" : "bi-caret-right-fill"}"></i>` : ""}</span>
        <i class="bi ${icon} ue-row-icon" aria-hidden="true"></i><span class="ue-row-name">${esc(n.name)}</span>
        ${n.tag === "EditorOnly" ? '<span class="ue-row-tag">EditorOnly</span>' : ""}</div>`);
      if (hasKids && n.expanded) for (const c of n.children) walk(c, depth + 1);
    };
    for (const id of this.roots) walk(id, 0);
    this.el.hier.innerHTML = `<div class="ue-scene-row"><i class="bi bi-unity"></i> SampleScene</div>${rows.join("")}<div class="ue-hier-empty" data-drop-root></div>`;
    this.applyHighlight();
  }

  renderInspector() {
    const n = this.get(this.selected);
    const box = this.el.insp;
    if (!n) { box.innerHTML = '<div class="ue-insp-empty">Hierarchy でオブジェクトを選ぶと、ここに設定が表示されます</div>'; this.applyHighlight(); return; }
    const num = (prop, axis, v) => `<label class="ue-num"><span>${"XYZ"[axis]}</span><input type="number" step="${prop === "r" ? 1 : 0.01}" value="${v}" data-field="${prop}" data-axis="${axis}" data-hl="${prop}-${axis}" /></label>`;
    const vec = (label, prop) => `<div class="ue-field"><span class="ue-field-label">${label}</span><div class="ue-vec">${n[prop].map((v, i) => num(prop, i, v)).join("")}</div></div>`;
    const comp = c => {
      let body = "";
      if (c.type === "SkinnedMeshRenderer") {
        body = `<div class="ue-field"><span class="ue-field-label">Materials</span><span class="ue-muted">Size ${c.materials.length}</span></div>` +
          c.materials.map((m, i) => `<div class="ue-field ue-slot" data-slot="${i}" data-hl="material-slot-${i}"><span class="ue-field-label">Element ${i}</span>
            <span class="ue-objfield"><i class="bi bi-circle-fill" style="color:#${(ASSETS[m]?.color ?? 0xff00ff).toString(16).padStart(6, "0")}"></i> ${esc(m)}</span></div>`).join("") +
          `<div class="ue-field"><span class="ue-field-label">BlendShapes</span><span class="ue-muted">${Object.keys(c.blendshapes).map(esc).join(", ") || "なし"}</span></div>` +
          `<div class="ue-field"><span class="ue-field-label">Triangles</span><span class="ue-muted">${c.tris.toLocaleString()}</span></div>`;
      } else if (c.type === "VRCAvatarDescriptor") {
        body = `<div class="ue-field"><span class="ue-field-label">View Position</span><span class="ue-muted">${c.view.join(", ")}</span></div><div class="ue-field"><span class="ue-field-label">Lip Sync</span><span class="ue-muted">Viseme Blend Shape</span></div>`;
      } else if (c.type === "PipelineManager") {
        body = `<div class="ue-field"><span class="ue-field-label">Blueprint ID</span><span class="ue-muted">${c.blueprintId || "（未アップロード）"}</span></div>`;
      } else if (c.type === "MAMergeArmature") {
        body = `<div class="ue-field"><span class="ue-field-label">Merge Target</span><span class="ue-objfield"><i class="bi bi-diagram-3"></i> ${esc(c.target ?? "None")}</span></div>
          <div class="ue-field"><span class="ue-field-label">Prefix</span><span class="ue-muted">${esc(c.prefix || "（なし）")}</span></div><div class="ue-field"><span class="ue-field-label">Suffix</span><span class="ue-muted">${esc(c.suffix || "（なし）")}</span></div>`;
      } else if (c.type === "MABlendshapeSync") {
        body = (c.bindings?.length ? c.bindings.map(b => `<div class="ue-field"><span class="ue-field-label">${esc(b.from)}</span><span class="ue-muted">${esc(b.blendshape)}</span></div>`).join("") : '<div class="ue-muted">同期するブレンドシェイプを追加してください</div>');
      } else if (c.type === "VRCPhysBone") {
        body = `<div class="ue-field"><span class="ue-field-label">Root Transform</span><span class="ue-objfield">${esc(c.root)}</span></div><div class="ue-field"><span class="ue-field-label">Pull / Spring</span><span class="ue-muted">${c.pull} / ${c.spring}</span></div>`;
      } else if (c.type === "Animator") {
        body = `<div class="ue-field"><span class="ue-field-label">Avatar</span><span class="ue-objfield">${esc(c.avatar)}</span></div>`;
      }
      return `<div class="ue-comp" data-comp="${c.type}"><div class="ue-comp-head"><i class="bi bi-caret-down-fill"></i> <input type="checkbox" checked disabled /> <strong>${esc(COMPONENT_LABEL[c.type] ?? c.type)}</strong></div>${body ? `<div class="ue-comp-body">${body}</div>` : ""}</div>`;
    };
    box.innerHTML = `
      <div class="ue-insp-head">
        <input type="checkbox" class="ue-active" ${n.active ? "checked" : ""} data-hl="active" aria-label="有効" title="有効 / 無効" />
        <input type="text" class="ue-name" value="${esc(n.name)}" aria-label="名前" readonly />
      </div>
      <div class="ue-field ue-tagrow"><span class="ue-field-label">Tag</span>
        <select class="ue-tag" data-hl="tag">${["Untagged", "EditorOnly", "MainCamera", "Player"].map(t => `<option ${t === n.tag ? "selected" : ""}>${t}</option>`).join("")}</select></div>
      <div class="ue-comp" data-comp="Transform"><div class="ue-comp-head"><i class="bi bi-caret-down-fill"></i> <strong>Transform</strong></div>
        <div class="ue-comp-body">${vec("Position", "p")}${vec("Rotation", "r")}${vec("Scale", "s")}</div></div>
      ${n.components.map(comp).join("")}
      <div class="ue-addcomp-wrap"><button type="button" class="ue-addcomp" data-hl="add-component">Add Component</button></div>`;
    this.applyHighlight();
  }

  renderProject() {
    const icon = a => ({ prefab: "bi-box-fill", model: "bi-boxes", material: "bi-circle-fill", texture: "bi-image" }[a.type]);
    this.el.proj.innerHTML = Object.entries(ASSETS).filter(([, a]) => !a.hidden).map(([key, a]) =>
      `<div class="ue-asset" data-asset="${esc(key)}" title="${esc(a.note ?? a.shader ?? "")}"><i class="bi ${icon(a)}" ${a.color ? `style="color:#${a.color.toString(16).padStart(6, "0")}"` : ""}></i><span>${esc(a.label)}</span></div>`).join("");
  }

  renderSDK() {
    const st = this.stats();
    const sdk = this.el.sdk;
    if (!st) { sdk.innerHTML = '<div class="ue-sdk-body">アバターが見つかりません</div>'; return; }
    const row = (label, k) => `<tr><td>${label}</td><td>${st.values[k].toLocaleString()}</td><td><span class="ue-rank ue-rank-${st.ranks[k]}">${RANKS[st.ranks[k]]}</span></td></tr>`;
    sdk.innerHTML = `
      <div class="ue-sdk-head"><span>VRChat SDK</span><button type="button" data-sdk="close" aria-label="閉じる">×</button></div>
      <div class="ue-sdk-tabs"><span>Authentication</span><span class="is-on">Builder</span><span>Settings</span></div>
      <div class="ue-sdk-body">
        <div class="ue-sdk-avatar"><i class="bi bi-person-badge"></i> ${esc(st.avatar.name)} <span class="ue-muted">（練習用アカウントでログイン中）</span></div>
        ${st.notes.map(n => `<div class="ue-note ue-note-${n.level}">${n.level === "warn" ? "⚠" : "ℹ"} ${esc(n.text)}</div>`).join("")}
        <table class="ue-perf"><thead><tr><th>Performance</th><th></th><th><span class="ue-rank ue-rank-${st.overall}">${RANKS[st.overall]}</span></th></tr></thead><tbody>
          ${row("Triangles", "tris")}${row("Material Slots", "materials")}${row("Skinned Meshes", "skinned")}${row("Bones", "bones")}${row("PhysBone Components", "physbones")}
        </tbody></table>
        <div class="ue-sdk-buttons"><button type="button" data-sdk="test" data-hl="build-test">Build &amp; Test</button><button type="button" data-sdk="upload" data-hl="build-upload">Build and Upload</button></div>
        <div class="ue-muted small">この画面は練習用のシミュレーションです。数値は目安で、実際には何もアップロードしません。</div>
      </div>`;
    this.applyHighlight();
  }

  // ---------------------------------------------------------------- チュートリアル用のハイライト
  /**
   * 案内中の場所を光らせる。
   *   hierarchy: "名前" | project: "アセット名" | inspector: "active" | "tag" | "add-component" | "material-slot-0"
   *   menu: "VRChat SDK" | sdk: "build-test" | scene: true | label: 吹き出しの文字
   */
  setHighlight(h = {}) {
    this.highlight = h;
    this.applyHighlight();
  }

  applyHighlight() {
    if (!this.root.isConnected) return;
    for (const el of this.root.querySelectorAll(".ue-hl")) el.classList.remove("ue-hl");
    const h = this.highlight ?? {};
    let target = null;
    if (h.hierarchy) target = this.el.hier.querySelector(`[data-node-name="${CSS.escape(h.hierarchy)}"]`);
    else if (h.project) target = this.el.proj.querySelector(`[data-asset="${CSS.escape(h.project)}"]`);
    else if (h.inspector) target = this.el.insp.querySelector(`[data-hl="${CSS.escape(h.inspector)}"]`);
    else if (h.menu) target = this.root.querySelector(`[data-menu="${CSS.escape(h.menu)}"]`);
    else if (h.sdk) target = this.el.sdk.querySelector(`[data-hl="${CSS.escape(h.sdk)}"]`);
    else if (h.tool) target = this.root.querySelector(`[data-tool="${CSS.escape(h.tool)}"]`);
    else if (h.popup) target = this.el.popup.querySelector(`[data-hl="${CSS.escape(h.popup)}"]`);
    else if (h.scene) target = this.el.scene;
    const label = this.el.hlLabel;
    if (!target || target.offsetParent === null) { label.hidden = true; return; }
    target.classList.add("ue-hl");
    if (!h.label) { label.hidden = true; return; }
    const r = target.getBoundingClientRect(), base = this.root.getBoundingClientRect();
    label.textContent = h.label;
    label.hidden = false;
    label.style.left = `${Math.max(4, Math.min(r.left - base.left + r.width / 2, base.width - 60))}px`;
    label.style.top = `${Math.max(2, r.top - base.top - 4)}px`;
  }

  // ---------------------------------------------------------------- ポップアップ（メニュー・右クリック・Add Component）
  showPopup(html, x, y) {
    const p = this.el.popup;
    p.innerHTML = html;
    p.hidden = false;
    const base = this.root.getBoundingClientRect();
    p.style.left = `${Math.max(0, Math.min(x - base.left, base.width - p.offsetWidth - 4))}px`;
    p.style.top = `${Math.max(0, Math.min(y - base.top, base.height - p.offsetHeight - 4))}px`;
    this.applyHighlight();
  }

  closePopup() {
    if (this.el.popup.hidden) return;
    this.keepFocus(() => { this.el.popup.hidden = true; this.el.popup.innerHTML = ""; });
    this.applyHighlight();
  }

  openContextMenu(id, x, y) {
    const n = this.get(id);
    if (!n) return;
    this.selected = id;
    this.render();
    this.showPopup(`<div class="ue-menu">
      <div class="ue-menu-title">${esc(n.name)}</div>
      <button type="button" data-ctx="setup-outfit" data-hl="setup-outfit"><i class="bi bi-person-plus"></i> Modular Avatar ▸ Setup Outfit</button>
      <button type="button" data-ctx="delete"><i class="bi bi-trash"></i> Delete</button>
      <button type="button" disabled>Rename</button><button type="button" disabled>Duplicate</button>
    </div>`, x, y);
    this.ctxNode = id;
    this.emit("contextmenu", { id, name: n.name });
  }

  /** チュートリアルのスキップ用: 名前のオブジェクトで右クリックメニューを開く */
  contextMenuByName(path) {
    const n = this.findByName(path);
    if (!n) return;
    const row = this.el.hier.querySelector(`[data-node="${n.id}"]`);
    const r = (row ?? this.el.hier).getBoundingClientRect();
    this.openContextMenu(n.id, r.left + 40, r.bottom);
  }

  openAddComponent() {
    const n = this.get(this.selected);
    if (!n) return;
    const btn = this.el.insp.querySelector(".ue-addcomp");
    const r = btn.getBoundingClientRect();
    this.showPopup(`<div class="ue-menu ue-addmenu">
      <input type="search" class="ue-addsearch" placeholder="Search" aria-label="コンポーネントを検索" />
      <div class="ue-addlist"></div></div>`, r.left, r.bottom);
    const input = this.el.popup.querySelector(".ue-addsearch");
    const list = this.el.popup.querySelector(".ue-addlist");
    const draw = () => {
      const q = input.value.trim().toLowerCase();
      const hits = COMPONENTS.filter(c => !q || c.label.toLowerCase().includes(q) || c.path.toLowerCase().includes(q));
      list.innerHTML = hits.map(c => `<button type="button" data-add="${c.type}" data-hl="add-${c.type}"><span class="ue-muted">${esc(c.path)} /</span> ${esc(c.label)}</button>`).join("") || '<div class="ue-muted p-2">見つかりません</div>';
      this.applyHighlight();
    };
    input.addEventListener("input", draw);
    input.addEventListener("keydown", e => {
      e.stopPropagation();
      if (e.key === "Enter") { const first = list.querySelector("[data-add]"); if (first) this.addComponent(this.selected, first.dataset.add); }
      if (e.key === "Escape") this.closePopup();
    });
    draw();
    input.focus();
    this.emit("menu", { menu: "add-component" });
  }

  openMenu(name, x, y) {
    if (name === "VRChat SDK") {
      this.showPopup(`<div class="ue-menu"><button type="button" data-menuitem="sdk-panel" data-hl="sdk-panel">Show Control Panel</button><button type="button" disabled>Utilities</button></div>`, x, y);
    } else {
      this.showPopup(`<div class="ue-menu"><div class="ue-muted p-2">練習では使えません</div></div>`, x, y);
    }
    this.emit("menu", { menu: name });
  }

  // ---------------------------------------------------------------- 入力
  bind() {
    const signal = this.abort.signal;
    const root = this.root;
    root.addEventListener("contextmenu", e => e.preventDefault(), { signal });

    root.addEventListener("click", e => {
      const t = e.target;
      const tool = t.closest("[data-tool]");
      if (tool) { this.setTool(tool.dataset.tool); return; }
      const menu = t.closest("[data-menu]");
      if (menu) { const r = menu.getBoundingClientRect(); this.openMenu(menu.dataset.menu, r.left, r.bottom); return; }
      const mi = t.closest("[data-menuitem]");
      if (mi?.dataset.menuitem === "sdk-panel") { this.openSDK(); return; }
      const ctx = t.closest("[data-ctx]");
      if (ctx) {
        if (ctx.dataset.ctx === "setup-outfit") this.setupOutfit(this.ctxNode);
        else if (ctx.dataset.ctx === "delete") { this.closePopup(); this.deleteNode(this.ctxNode); }
        return;
      }
      const add = t.closest("[data-add]");
      if (add) { this.addComponent(this.selected, add.dataset.add); return; }
      if (t.closest(".ue-addcomp")) { this.openAddComponent(); return; }
      const sdk = t.closest("[data-sdk]");
      if (sdk) {
        if (sdk.dataset.sdk === "close") this.closeSDK();
        else this.build(sdk.dataset.sdk);
        return;
      }
      if (!t.closest(".ue-popup")) this.closePopup();
    }, { signal });

    // Inspector の入力
    root.addEventListener("change", e => {
      const t = e.target;
      const n = this.get(this.selected);
      if (!n) return;
      if (t.classList.contains("ue-active")) this.setActive(n.id, t.checked);
      else if (t.classList.contains("ue-tag")) this.setTag(n.id, t.value);
      else if (t.dataset.field) this.setTransformValue(n.id, t.dataset.field, +t.dataset.axis, parseFloat(t.value), true);
    }, { signal });

    // Hierarchy: クリックで選択、▶ で開閉、右クリックでメニュー
    this.el.hier.addEventListener("pointerdown", e => {
      const fold = e.target.closest("[data-fold]");
      const row = e.target.closest("[data-node]");
      if (e.button === 2 && row) { e.preventDefault(); this.openContextMenu(row.dataset.node, e.clientX, e.clientY); return; }
      if (e.button !== 0) return;
      if (fold && fold.innerHTML) { const n = this.get(fold.dataset.fold); this.setExpanded(n.id, !n.expanded); return; }
      if (row) { this.beginDrag(e, { kind: "node", id: row.dataset.node, label: this.get(row.dataset.node).name }); return; }
      if (e.target.closest(".ue-hier-list")) { this.selected = null; this.render(); this.updateGizmo(); }
    }, { signal });
    this.el.hier.addEventListener("dblclick", e => { const row = e.target.closest("[data-node]"); if (row) { this.select(row.dataset.node); this.frameSelected(); } }, { signal });

    // Project: ドラッグで追加・マテリアルの設定
    this.el.proj.addEventListener("pointerdown", e => {
      const a = e.target.closest("[data-asset]");
      if (a && e.button === 0) this.beginDrag(e, { kind: "asset", asset: a.dataset.asset, label: ASSETS[a.dataset.asset].label });
    }, { signal });

    // Scene ビュー
    const sv = this.el.scene;
    sv.addEventListener("pointerdown", e => this.onScenePointerDown(e), { signal });
    sv.addEventListener("pointermove", e => this.onScenePointerMove(e), { signal });
    sv.addEventListener("pointerup", e => this.onScenePointerUp(e), { signal });
    sv.addEventListener("wheel", e => {
      if (this.options.wheelNeedsFocus && !root.contains(document.activeElement)) return;
      e.preventDefault();
      this.view.dist = THREE.MathUtils.clamp(this.view.dist * (e.deltaY > 0 ? 1.12 : 1 / 1.12), 0.3, 50);
      this.emit("view", { action: "zoom" });
    }, { passive: false, signal });

    root.addEventListener("keydown", e => this.onKey(e), { signal });
  }

  onKey(e) {
    const t = e.target;
    if (t.matches?.("input, select, textarea")) return;
    this.emit("key", { code: e.code, key: e.key, shift: e.shiftKey, ctrl: e.ctrlKey || e.metaKey, alt: e.altKey });
    const mod = e.ctrlKey || e.metaKey;
    if (mod && e.code === "KeyZ") { e.preventDefault(); this.undo(); return; }
    if (mod) return;
    switch (e.code) {
      case "KeyQ": this.setTool("hand"); break;
      case "KeyW": this.setTool("move"); break;
      case "KeyE": this.setTool("rotate"); break;
      case "KeyR": this.setTool("scale"); break;
      case "KeyF": this.frameSelected(); break;
      case "Delete": if (this.selected) this.deleteNode(this.selected); break;
      case "Escape": this.closePopup(); this.closeSDK(); break;
      default: return;
    }
    e.preventDefault();
  }

  // ---------------------------------------------------------------- ドラッグ（Hierarchy・Project → Hierarchy / Inspector / Scene）
  beginDrag(e, payload) {
    const start = { x: e.clientX, y: e.clientY };
    let ghost = null;
    const move = ev => {
      if (!ghost && Math.hypot(ev.clientX - start.x, ev.clientY - start.y) < 5) return;
      if (!ghost) {
        ghost = document.createElement("div");
        ghost.className = "ue-ghost";
        ghost.textContent = payload.label;
        this.root.appendChild(ghost);
      }
      const base = this.root.getBoundingClientRect();
      ghost.style.left = `${ev.clientX - base.left + 10}px`;
      ghost.style.top = `${ev.clientY - base.top + 6}px`;
      for (const el of this.root.querySelectorAll(".ue-drop")) el.classList.remove("ue-drop");
      this.dropTarget(ev)?.el?.classList.add("ue-drop");
    };
    const up = ev => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      for (const el of this.root.querySelectorAll(".ue-drop")) el.classList.remove("ue-drop");
      if (!ghost) {
        // ドラッグしなかった = クリック
        if (payload.kind === "node") this.select(payload.id);
        else this.setStatus(`${ASSETS[payload.asset].label}: ${ASSETS[payload.asset].note ?? ASSETS[payload.asset].shader ?? ""}（ドラッグして使う）`);
        return;
      }
      ghost.remove();
      this.drop(payload, this.dropTarget(ev), ev);
    };
    window.addEventListener("pointermove", move, { signal: this.abort.signal });
    window.addEventListener("pointerup", up, { signal: this.abort.signal });
  }

  dropTarget(ev) {
    const el = document.elementFromPoint(ev.clientX, ev.clientY);
    if (!el || !this.root.contains(el)) return null;
    const row = el.closest("[data-node]");
    if (row) return { kind: "node", id: row.dataset.node, el: row };
    if (el.closest(".ue-hierarchy")) return { kind: "root", el: this.el.hier };
    const slot = el.closest("[data-slot]");
    if (slot) return { kind: "slot", slot: +slot.dataset.slot, el: slot };
    if (el.closest(".ue-inspector")) return { kind: "inspector", el: this.el.insp };
    if (el.closest(".ue-scene-view")) return { kind: "scene", el: this.el.scene };
    return null;
  }

  drop(payload, target, ev) {
    if (!target) return;
    if (payload.kind === "node") {
      if (target.kind === "node") this.reparent(payload.id, target.id);
      else if (target.kind === "root") this.reparent(payload.id, null);
      return;
    }
    const asset = ASSETS[payload.asset];
    if (asset.type === "prefab") {
      if (target.kind === "node") this.instantiate(payload.asset, target.id);
      else if (target.kind === "root" || target.kind === "scene") this.instantiate(payload.asset, null);
      return;
    }
    if (asset.type === "material") {
      if (target.kind === "slot") this.assignMaterial(this.selected, target.slot, payload.asset);
      else if (target.kind === "scene") {
        const hit = this.pickMesh(ev);
        if (hit) this.assignMaterial(hit, 0, payload.asset);
        else this.setStatus("マテリアルはメッシュの上か、Inspector の Materials の欄で離してください");
      } else this.setStatus("マテリアルは Inspector の Materials の欄（Element 0）にドラッグしてください");
      return;
    }
    this.setStatus(asset.type === "model" ? "FBX ではなく、衣装の Prefab（Hoodie）を使ってください" : "テクスチャはマテリアルに設定して使います（この練習では省略）");
  }

  // ---------------------------------------------------------------- Scene ビューのマウス
  ndc(e) {
    const r = this.el.scene.getBoundingClientRect();
    return new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
  }

  pickMesh(e) {
    const ray = new THREE.Raycaster();
    ray.setFromCamera(this.ndc(e), this.camera);
    const parts = [...this.nodes.values()].flatMap(n => (n.parts ?? []).filter(p => p.visible));
    const hit = ray.intersectObjects(parts, false)[0];
    return hit?.object.userData.nodeId ?? null;
  }

  onScenePointerDown(e) {
    if (e.target.closest(".ue-preview")) return;
    this.root.focus({ preventScroll: true });
    this.closePopup();
    const p = { x: e.clientX, y: e.clientY };
    if ((e.button === 0 && e.altKey) || (e.button === 0 && this.tool === "hand" && !e.altKey)) {
      this.drag = { kind: e.altKey ? "orbit" : "pan", last: p, moved: 0 };
    } else if (e.button === 1) {
      this.drag = { kind: "pan", last: p, moved: 0 };
      e.preventDefault();
    } else if (e.button === 2) {
      this.drag = { kind: "look", last: p, moved: 0 };
    } else if (e.button === 0) {
      const handle = this.pickHandle(e);
      if (handle) {
        const n = this.get(this.selected);
        this.pushUndo();
        n.obj.updateMatrixWorld();
        this.drag = {
          kind: "gizmo", handle, last: p, start: p, moved: 0,
          worldPos: n.obj.getWorldPosition(new THREE.Vector3()), worldQuat: n.obj.getWorldQuaternion(new THREE.Quaternion()), scale: n.obj.scale.clone(),
        };
      } else {
        this.drag = { kind: "click", last: p, moved: 0 };
      }
    } else return;
    this.el.scene.setPointerCapture(e.pointerId);
  }

  onScenePointerMove(e) {
    const r = this.el.scene.getBoundingClientRect();
    this.mouse = { x: e.clientX - r.left, y: e.clientY - r.top };
    const d = this.drag;
    if (!d) return;
    const dx = e.clientX - d.last.x, dy = e.clientY - d.last.y;
    d.moved += Math.abs(dx) + Math.abs(dy);
    d.last = { x: e.clientX, y: e.clientY };
    const v = this.view;
    if (d.kind === "orbit") { this.orbitBy(-dx * 0.008, dy * 0.008); }
    else if (d.kind === "pan") {
      const k = v.dist * 0.0018;
      const right = new THREE.Vector3().setFromMatrixColumn(this.camera.matrixWorld, 0);
      const up = new THREE.Vector3().setFromMatrixColumn(this.camera.matrixWorld, 1);
      v.target.addScaledVector(right, -dx * k).addScaledVector(up, dy * k);
    } else if (d.kind === "look") {
      // 右ドラッグ: カメラの位置はそのままで向きを変える
      const pos = this.camera.position.clone();
      this.orbitBy(-dx * 0.005, dy * 0.005);
      const dir = this.viewDir();
      v.target.copy(pos).addScaledVector(dir, -v.dist);
    } else if (d.kind === "gizmo") this.dragGizmo(e, d);
  }

  onScenePointerUp(e) {
    const d = this.drag;
    this.drag = null;
    if (!d) return;
    if (d.kind === "click") {
      const id = this.pickMesh(e);
      if (id) this.select(id, "scene");
      else { this.selected = null; this.render(); this.updateGizmo(); this.emit("select", { id: null, name: null, source: "scene" }); }
    } else if (d.kind === "gizmo") {
      const n = this.get(this.selected);
      if (d.moved < 2) { this.undoStack.pop(); return; }
      this.readTransform(n);
      this.renderInspector();
      const kind = d.handle.kind;
      this.setStatus(`${n.name}: ${kind === "move" ? "移動" : kind === "rotate" ? "回転" : "拡大縮小"}しました`);
      this.emit("transform", { id: n.id, name: n.name, kind, axis: d.handle.axis, final: true });
    } else if (d.moved > 2) {
      this.emit("view", { action: d.kind });
    }
  }

  // ---------------------------------------------------------------- ギズモ（W 移動 / E 回転 / R 拡大縮小。Unity と同じ色）
  updateGizmo() {
    this.gizmo.traverse(o => { o.geometry?.dispose(); o.material?.dispose?.(); });
    this.gizmo.clear();
    this.handles = [];
    const n = this.get(this.selected);
    if (!n || this.tool === "hand" || this.pose) return;
    const colors = { x: 0xdb3b3b, y: 0x8fd14f, z: 0x3a7bd5 };
    const dirs = { x: new THREE.Vector3(1, 0, 0), y: new THREE.Vector3(0, 1, 0), z: new THREE.Vector3(0, 0, 1) };
    const mat = c => new THREE.MeshBasicMaterial({ color: c, depthTest: false, transparent: true, opacity: 0.95 });
    const hitMat = new THREE.MeshBasicMaterial({ visible: false });
    for (const axis of ["x", "y", "z"]) {
      const g = new THREE.Group();
      g.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dirs[axis]);
      if (this.tool === "move" || this.tool === "scale") {
        const line = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 1, 8).translate(0, 0.5, 0), mat(colors[axis]));
        const tip = this.tool === "move"
          ? new THREE.Mesh(new THREE.ConeGeometry(0.06, 0.2, 16).translate(0, 1.05, 0), mat(colors[axis]))
          : new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.1, 0.1).translate(0, 1, 0), mat(colors[axis]));
        const hit = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 1.2, 8).translate(0, 0.6, 0), hitMat.clone());
        hit.userData.handle = { kind: this.tool, axis };
        g.add(line, tip, hit);
        this.handles.push(hit);
      } else {
        const ring = new THREE.Mesh(new THREE.TorusGeometry(0.9, 0.012, 8, 64).rotateX(Math.PI / 2), mat(colors[axis]));
        const hit = new THREE.Mesh(new THREE.TorusGeometry(0.9, 0.07, 8, 32).rotateX(Math.PI / 2), hitMat.clone());
        hit.userData.handle = { kind: "rotate", axis };
        g.add(ring, hit);
        this.handles.push(hit);
      }
      this.gizmo.add(g);
    }
    if (this.tool === "scale") {
      const c = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.13, 0.13), mat(0xcccccc));
      const hit = new THREE.Mesh(new THREE.BoxGeometry(0.25, 0.25, 0.25), hitMat.clone());
      hit.userData.handle = { kind: "scale", axis: "all" };
      this.gizmo.add(c, hit);
      this.handles.push(hit);
    }
  }

  placeGizmo() {
    const n = this.get(this.selected);
    if (!n || !this.gizmo.children.length) { this.gizmo.visible = false; return; }
    this.gizmo.visible = true;
    n.obj.updateMatrixWorld();
    const pos = n.obj.getWorldPosition(new THREE.Vector3());
    this.gizmo.position.copy(pos);
    this.gizmo.scale.setScalar(this.camera.position.distanceTo(pos) * 0.14);
  }

  pickHandle(e) {
    if (!this.handles?.length || !this.gizmo.visible) return null;
    this.gizmo.updateMatrixWorld(true);
    const ray = new THREE.Raycaster();
    ray.setFromCamera(this.ndc(e), this.camera);
    return ray.intersectObjects(this.handles, false)[0]?.object.userData.handle ?? null;
  }

  dragGizmo(e, d) {
    const n = this.get(this.selected);
    const { kind, axis } = d.handle;
    const total = { x: e.clientX - d.start.x, y: e.clientY - d.start.y };
    const dirs = { x: new THREE.Vector3(1, 0, 0), y: new THREE.Vector3(0, 1, 0), z: new THREE.Vector3(0, 0, 1) };
    // 軸の画面上の向き
    const screenDir = v => {
      const a = d.worldPos.clone().project(this.camera), b = d.worldPos.clone().add(v).project(this.camera);
      const r = this.el.scene.getBoundingClientRect();
      const s = new THREE.Vector2((b.x - a.x) * r.width / 2, -(b.y - a.y) * r.height / 2);
      return { dir: s.clone().normalize(), len: Math.max(1, s.length()) };
    };
    if (kind === "move") {
      const { dir, len } = screenDir(dirs[axis]);
      const amount = (total.x * dir.x + total.y * dir.y) / len;
      const world = d.worldPos.clone().addScaledVector(dirs[axis], amount);
      n.obj.position.copy(n.obj.parent.worldToLocal(world));
    } else if (kind === "rotate") {
      const angle = (total.x - total.y) * 0.6 * DEG;
      const q = new THREE.Quaternion().setFromAxisAngle(dirs[axis], angle).multiply(d.worldQuat);
      const parentQ = n.obj.parent.getWorldQuaternion(new THREE.Quaternion()).invert();
      n.obj.quaternion.copy(parentQ.multiply(q));
    } else if (kind === "scale") {
      const v = axis === "all" ? new THREE.Vector3(1, 1, 0).normalize() : dirs[axis];
      const { dir } = screenDir(v);
      const f = Math.max(0.05, 1 + (axis === "all" ? (total.x - total.y) / 150 : (total.x * dir.x + total.y * dir.y) / 150));
      n.obj.scale.copy(d.scale);
      if (axis === "all") n.obj.scale.multiplyScalar(f); else n.obj.scale[axis] *= f;
    }
    this.readTransform(n);
    this.renderInspector();
  }

  // ---------------------------------------------------------------- カメラ・描画
  viewDir() {
    const v = this.view;
    return new THREE.Vector3(Math.sin(v.yaw) * Math.cos(v.pitch), Math.sin(v.pitch), Math.cos(v.yaw) * Math.cos(v.pitch));
  }

  resize() {
    const w = this.el.scene.clientWidth, h = this.el.scene.clientHeight;
    if (!w || !h) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  animate() {
    this.raf = requestAnimationFrame(() => this.animate());
    if (!this.onScreen) return;
    const v = this.view;
    this.camera.position.copy(v.target).addScaledVector(this.viewDir(), v.dist);
    this.camera.lookAt(v.target);
    this.camera.updateMatrixWorld();
    this.updatePose();
    this.placeGizmo();
    this.renderer.clear();
    this.renderer.render(this.scene, this.camera);
    this.renderer.clearDepth();
    this.renderer.render(this.gizmoScene, this.camera);
    const n = this.get(this.selected);
    this.el.info.textContent = n ? `${n.name}  ·  ${{ hand: "Hand", move: "Move", rotate: "Rotate", scale: "Scale" }[this.tool]} Tool` : `${{ hand: "Hand", move: "Move", rotate: "Rotate", scale: "Scale" }[this.tool]} Tool`;
  }
}

// ---------------------------------------------------------------- 部品の形
function makePart(shape, size, color) {
  let geo;
  if (shape === "sphere" || shape === "eye") geo = new THREE.SphereGeometry(size[0], 24, 16);
  else if (shape === "capsule") geo = new THREE.CapsuleGeometry(size[0], size[1], 6, 16);
  else if (shape === "box") geo = new THREE.BoxGeometry(size[0], size[1], size[2]);
  else if (shape === "hair") geo = new THREE.SphereGeometry(size[0], 24, 16, 0, Math.PI * 2, 0, Math.PI * 0.55);
  else if (shape === "hood") geo = new THREE.SphereGeometry(size[0], 20, 12, Math.PI * 0.15, Math.PI * 1.7, 0.2, Math.PI * 0.6);
  const mat = new THREE.MeshStandardMaterial({ color, roughness: 0.8, side: shape === "hood" || shape === "hair" ? THREE.DoubleSide : THREE.FrontSide });
  return new THREE.Mesh(geo, mat);
}

/** Unity の既定の空に近いグラデーション */
function skyTexture() {
  const c = document.createElement("canvas");
  c.width = 2; c.height = 256;
  const g = c.getContext("2d");
  const grad = g.createLinearGradient(0, 0, 0, 256);
  grad.addColorStop(0, "#5f7ea3");
  grad.addColorStop(0.5, "#a9b8c8");
  grad.addColorStop(0.52, "#8a8580");
  grad.addColorStop(1, "#5a5652");
  g.fillStyle = grad;
  g.fillRect(0, 0, 2, 256);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
