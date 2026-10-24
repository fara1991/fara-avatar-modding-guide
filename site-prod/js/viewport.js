// Blender と同じキー操作で動かせる 3D ビュー（Three.js）。
// 操作の練習（practice.js）と記事内チュートリアル（tutorial.js）の両方から使う。1 ページに何個でも置ける。
// 対応している操作は Practice.razor の「使えるキー」の表のとおり。編集モードのメッシュ（頂点・辺・面と操作）は editmesh.js。
//
//   const vp = new Viewport(element, { scene: [{ kind: "cube" }] });
//   vp.on("confirm", e => ...);   // 操作のたびにイベントが届く（一覧は emit を呼んでいる箇所を参照）
//   vp.dispose();                 // WebGL コンテキストを解放する
//
// イベント（e.type と詳細）:
//   key {code, key, shift, ctrl, alt}       キーが押された（操作の前に届く）
//   select {source, selected, active, edit, counts, selectMode}
//                                            source: click / shift-click / box / all / none / invert / loop / ring / linked / grow / shrink
//                                            編集モードでは edit: true、counts: {verts, edges, faces}（選択数）
//   opstart {kind, edit}  axis {kind, axis, exclude}  numeric {kind, value}
//   confirm {kind, axis, exclude, value, edit, extrude, centered}   cancel {kind}   opfail {kind, reason}
//                                            kind: G / R / S / I（差し込み）/ B（ベベル）/ slide（ループカットの位置）/ loopcut
//   view {action, name, ortho, distance}     action: orbit / pan / zoom / axis / ortho / frame / home
//   menu {menu}  add {kind, name}  duplicate {names}  delete {names | edit, what}  undo  redo  npanel {open}  edit {prop, axis, value}
//   mode {mode, object}  selectmode {mode}  xray {on}  stats {on}  mirror {on, object}
//   extrude {type}  inset  bevel  cuts {cuts}  loopcut {cuts}  merge {how, removed}  fill {made}  separate {name}
//
// 共同練習（room.js）用: オブジェクトには部屋で共通の ID（userData.rid）があり、roomState() / applyRemote() /
// setRemotePeers()（ほかの人のカーソルと選択）/ getView() / setView() で同期する
import * as THREE from "three";
import { EditMesh, SHAPES } from "./editmesh.js";

export const DEG = Math.PI / 180;
const AXIS = { x: new THREE.Vector3(1, 0, 0), y: new THREE.Vector3(0, 1, 0), z: new THREE.Vector3(0, 0, 1) };
const AXIS_COLOR = { x: 0xff3352, y: 0x8bdc00, z: 0x2890ff, n: 0xffd84a };
/** 複数行の文字を <br> 区切りで表示する（中身は HTML として解釈しない） */
function setLines(el, lines) {
  el.replaceChildren(...lines.flatMap((t, i) => (i ? [document.createElement("br"), String(t)] : [String(t)])));
}

const DEFAULT_VIEW = { dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, name: "ユーザー" };
const SELECT_MODES = { vert: "頂点", edge: "辺", face: "面" };

// 追加できるメッシュ（Shift+A）。Blender の「メッシュ」メニューの順
export const PRIMITIVES = [
  { key: "plane", label: "平面" },
  { key: "cube", label: "立方体" },
  { key: "sphere", label: "UV球" },
  { key: "cylinder", label: "円柱" },
  { key: "cone", label: "円錐" },
  { key: "torus", label: "トーラス" },
];
const NAMES = { plane: "Plane", cube: "Cube", sphere: "Sphere", cylinder: "Cylinder", cone: "Cone", torus: "Torus" };

function randomId() {
  const a = new Uint8Array(8);
  crypto.getRandomValues(a);
  return [...a].map(b => (b % 36).toString(36)).join("");
}

export function near(a, b, eps = 0.05) { return Math.abs(a - b) <= eps; }
export function nearAngle(rad, deg, eps = 1) {
  const d = ((rad / DEG - deg) % 360 + 540) % 360 - 180;
  return Math.abs(d) <= eps;
}

// ビューポート内の HUD とサイドバー（N）。静的な HTML 側には空の要素を置くだけでよい
const HUD_HTML = `
  <div class="practice-hud">
    <div class="vp-mode">オブジェクトモード</div>
    <div class="vp-view">ユーザー・透視投影</div>
    <div class="vp-stats" hidden></div>
  </div>
  <div class="vp-tools" role="toolbar" aria-label="表示と選択モード">
    <span class="vp-tools-edit" hidden>
      <button type="button" data-tool="vert" title="頂点選択（1）">頂点</button><button type="button" data-tool="edge" title="辺選択（2）">辺</button><button type="button" data-tool="face" title="面選択（3）">面</button>
    </span>
    <button type="button" data-tool="xray" title="透過表示（Alt+Z）">透過</button>
    <button type="button" data-tool="stats" title="オーバーレイ > 統計">統計</button>
    <button type="button" data-tool="mirror" title="ミラー モディファイアー（X 方向）">ミラー</button>
  </div>
  <div class="practice-npanel" hidden>
    <h6>アイテム</h6>
    <div class="vp-npanel-name mb-1">—</div>
    ${[["position", "位置", "0.1"], ["rotation", "回転（°）", "1"], ["scale", "スケール", "0.1"]].map(([prop, label, step]) => `
    <h6>${label}</h6>
    <div class="row-3">
      ${["x", "y", "z"].map(a => `<span>${a.toUpperCase()}</span><input type="number" step="${step}" data-prop="${prop}" data-axis="${a}" />`).join("")}
    </div>`).join("")}
  </div>
  <div class="vp-marker" hidden><span class="vp-marker-label"></span></div>
  <div class="vp-remote"></div>
  <div class="practice-status">
    <span class="vp-status"></span>
    <span class="vp-numeric"></span>
  </div>`;

export class Viewport {
  /**
   * @param {HTMLElement} root ビューポートにする要素（中身はこのクラスが作る）
   * @param {object} options
   *   scene: 最初に置くオブジェクト [{ kind, name?, p?: [x,y,z], r?: [度,度,度], s?: [x,y,z] }]（既定は立方体 1 つ）
   *   emulateNumpad: () => boolean  数字キーをテンキーとして扱うか
   *   wheelNeedsFocus: boolean      true ならフォーカス中だけホイールでズームする（記事内でページのスクロールを邪魔しない）
   *   view: { dist?, target?: [x,y,z] }  最初の視点の距離と注視点（既定は Blender と同じ）
   */
  constructor(root, options = {}) {
    this.root = root;
    this.options = options;
    this.listeners = new Map();
    this.silent = false;
    this.abort = new AbortController();

    root.classList.add("practice-viewport");
    root.tabIndex = 0;
    root.setAttribute("aria-label", "3D ビューポート");
    root.insertAdjacentHTML("beforeend", HUD_HTML);
    const q = sel => root.querySelector(sel);
    this.hud = { mode: q(".vp-mode"), view: q(".vp-view"), stats: q(".vp-stats"), status: q(".vp-status"), numeric: q(".vp-numeric"), tools: q(".vp-tools"), toolsEdit: q(".vp-tools-edit") };
    this.npanel = q(".practice-npanel");
    this.marker = { el: q(".vp-marker"), label: q(".vp-marker-label"), target: null, at: null };

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    root.insertBefore(this.renderer.domElement, root.firstChild);

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x3d3d3d);
    this.persp = new THREE.PerspectiveCamera(50, 1, 0.1, 500);
    this.orthoCam = new THREE.OrthographicCamera(-1, 1, 1, -1, -500, 500);
    this.persp.up.set(0, 0, 1);
    this.orthoCam.up.set(0, 0, 1);
    this.ortho = false;
    // Blender の既定の視点（右前上から）。theta は Z 軸まわり、phi は Z 軸からの角度
    this.view = { target: new THREE.Vector3(), ...DEFAULT_VIEW };

    this.objects = [];
    this.selected = [];
    this.active = null;
    this.op = null;
    this.loopcut = null;
    this.mode = "object";
    this.editObj = null;
    this.selectMode = "vert";
    this.xray = false;
    this.showStats = false;
    this.undoStack = [];
    this.redoStack = [];
    const rect = root.getBoundingClientRect();
    this.mouse = { x: 0, y: 0, cx: rect.left + rect.width / 2, cy: rect.top + rect.height / 2 };
    this.box = null;
    this.menu = null;
    this.onScreen = true;

    this.buildScene();
    this.bind();
    this.resetScene();
    this.resize();
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(root);
    // 画面外にあるあいだは描画しない
    this.visibility = new IntersectionObserver(entries => { this.onScreen = entries[entries.length - 1].isIntersecting; });
    this.visibility.observe(root);
    this.animate();
  }

  // ---------------------------------------------------------------- イベント
  /** type に "*" を渡すとすべてのイベントを受け取る。戻り値を呼ぶと解除 */
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

  /** WebGL コンテキストとイベントを解放し、要素を空に戻す */
  dispose() {
    cancelAnimationFrame(this.raf);
    this.abort.abort();
    this.resizeObserver.disconnect();
    this.visibility.disconnect();
    this.scene.traverse(o => { o.geometry?.dispose(); o.material?.dispose?.(); });
    this.renderer.dispose();
    this.renderer.forceContextLoss();
    this.root.replaceChildren();
    this.listeners.clear();
  }

  // ---------------------------------------------------------------- シーン
  buildScene() {
    const grid = new THREE.GridHelper(40, 40, 0x4e4e4e, 0x484848);
    grid.rotation.x = Math.PI / 2;
    this.scene.add(grid);
    // 長い線は短い区間に分けて作る。1 本の長い線分がカメラの後ろにまたがると、環境によっては丸ごと描画されないため
    const longLine = (half, dir = AXIS.x) => new THREE.BufferGeometry().setFromPoints(
      Array.from({ length: half * 2 + 1 }, (_, i) => dir.clone().multiplyScalar(i - half)));
    const axis = (color, dir) => {
      const line = new THREE.Line(longLine(20, dir), new THREE.LineBasicMaterial({ color }));
      line.position.z = 0.002; // グリッドの中心線と重なって消えないように少し浮かせる
      this.scene.add(line);
    };
    axis(0x9a3a4a, AXIS.x);
    axis(0x5a8a2a, AXIS.y);

    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x444444, 1.1));
    const key = new THREE.DirectionalLight(0xffffff, 1.2);
    key.position.set(4, -6, 8);
    this.scene.add(key);
    this.world = new THREE.Group();
    this.scene.add(this.world);

    // 3D カーソル（原点に固定）
    const ring = new THREE.Mesh(new THREE.RingGeometry(0.12, 0.17, 24), new THREE.MeshBasicMaterial({ color: 0xffffff, side: THREE.DoubleSide, depthTest: false }));
    ring.renderOrder = 10;
    this.cursor = ring;
    this.scene.add(ring);

    // 軸固定中に表示する線（Blender と同じく軸の色）と、チュートリアルで軸を示す点線
    this.axisLine = new THREE.Line(longLine(40), new THREE.LineBasicMaterial({ color: 0xffffff }));
    this.axisLine.visible = false;
    this.scene.add(this.axisLine);
    this.guideLine = new THREE.Line(longLine(40), new THREE.LineDashedMaterial({ color: 0xffffff, dashSize: 0.3, gapSize: 0.2, transparent: true, depthTest: false }));
    this.guideLine.computeLineDistances();
    this.guideLine.renderOrder = 9;
    this.guideLine.visible = false;
    this.scene.add(this.guideLine);
  }

  /** 線を原点で X 方向に作ってあるので、向きと位置を合わせる。axis は "x" / "y" / "z" か Vector3 */
  placeAxisLine(line, axis, at, color = AXIS_COLOR[axis]) {
    line.position.copy(at);
    line.quaternion.setFromUnitVectors(AXIS.x, typeof axis === "string" ? AXIS[axis] : axis.clone().normalize());
    line.material.color.set(color ?? AXIS_COLOR.n);
  }

  makeObject(kind, data) {
    const fresh = !data;
    data ??= SHAPES[kind]();
    const mesh = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshStandardMaterial({
      color: 0x9c9c9c, roughness: 0.75, metalness: 0.0, side: THREE.DoubleSide, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
    }));
    mesh.userData.kind = kind;
    mesh.userData.em = EditMesh.fromData(data);
    if (fresh) mesh.userData.em.selectAll("vert"); // 追加したばかりのメッシュは、編集モードで全体が選択されている（Blender と同じ）
    mesh.userData.mirror = false;
    mesh.userData.rid = randomId(); // 共同練習の部屋で、同じオブジェクトを指す ID
    mesh.name = this.uniqueName(NAMES[kind]);
    const outline = new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xff9d2b }));
    outline.visible = false;
    outline.name = "outline";
    mesh.add(outline);
    const mirror = new THREE.Mesh(mesh.geometry, mesh.material);
    mirror.scale.x = -1;
    mirror.name = "mirror";
    mirror.visible = false;
    mesh.add(mirror);
    this.rebuild(mesh);
    return mesh;
  }

  uniqueName(base) {
    if (!this.objects.some(o => o.name === base)) return base;
    for (let i = 1; ; i++) {
      const n = `${base}.${String(i).padStart(3, "0")}`;
      if (!this.objects.some(o => o.name === n)) return n;
    }
  }

  addObject(kind, at = this.cursor.position, data) {
    const mesh = this.makeObject(kind, data);
    mesh.position.copy(at);
    this.world.add(mesh);
    this.objects.push(mesh);
    return mesh;
  }

  removeObject(mesh) {
    if (mesh === this.editObj) this.setMode("object", true);
    this.world.remove(mesh);
    this.objects = this.objects.filter(o => o !== mesh);
    this.selected = this.selected.filter(o => o !== mesh);
    if (this.active === mesh) this.active = this.selected[this.selected.length - 1] ?? null;
  }

  find(name) { return this.objects.find(o => o.name === name) ?? null; }

  /** 最初の状態に戻す。scene を省略するとコンストラクタで渡したもの */
  resetScene(scene = this.options.scene ?? [{ kind: "cube" }]) {
    this.closeMenu();
    this.op = null;
    this.endLoopCut();
    this.setMode("object", true);
    for (const o of [...this.objects]) this.removeObject(o);
    for (const d of scene) {
      const m = this.addObject(d.kind);
      if (d.name) m.name = d.name;
      if (d.p) m.position.fromArray(d.p);
      if (d.r) m.rotation.set(d.r[0] * DEG, d.r[1] * DEG, d.r[2] * DEG);
      if (d.s) m.scale.fromArray(d.s);
    }
    this.selected = [];
    this.active = null;
    this.undoStack = [];
    this.redoStack = [];
    this.ortho = false;
    this.selectMode = "vert";
    this.setXray(false, true);
    this.setStats(false, true);
    Object.assign(this.view, DEFAULT_VIEW, this.options.view?.dist ? { dist: this.options.view.dist } : {});
    this.view.target.fromArray(this.options.view?.target ?? [0, 0, 0]);
    this.npanel.hidden = true;
    this.hud.numeric.textContent = "";
    this.axisLine.visible = false;
    this.refreshSelection();
    this.updateTools();
    this.setStatus("クリックして選択。G / R / S で動かす");
  }

  // ---------------------------------------------------------------- メッシュの描画
  /** em（メッシュの中身）から、表示用のジオメトリ・輪郭・編集モードの点と線を作り直す */
  rebuild(obj) {
    const em = obj.userData.em;
    const { pos, nor, triFace } = em.triangles();
    const g = new THREE.BufferGeometry();
    g.setAttribute("position", new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute("normal", new THREE.Float32BufferAttribute(nor, 3));
    g.computeBoundingSphere();
    g.computeBoundingBox();
    const old = obj.geometry;
    obj.geometry = g;
    obj.userData.triFace = triFace;
    obj.getObjectByName("mirror").geometry = g;
    old.dispose();
    const outline = obj.getObjectByName("outline");
    outline.geometry.dispose();
    outline.geometry = new THREE.EdgesGeometry(g, 20);
    const remoteSel = obj.getObjectByName("remote-sel");
    if (remoteSel) remoteSel.geometry = outline.geometry;
    obj.getObjectByName("mirror").visible = obj.userData.mirror;
    if (obj === this.editObj) this.rebuildOverlay(obj);
    this.updateStats();
  }

  /** 編集モードの頂点（点）・辺（線）・選択した面 */
  rebuildOverlay(obj) {
    const em = obj.userData.em;
    let overlay = obj.getObjectByName("edit-overlay");
    if (!overlay) {
      overlay = new THREE.Group();
      overlay.name = "edit-overlay";
      overlay.add(new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ vertexColors: true })));
      overlay.add(new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ size: 6, sizeAttenuation: false, vertexColors: true })));
      overlay.add(new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial({
        color: 0xff9d2b, transparent: true, opacity: 0.28, side: THREE.DoubleSide, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })));
      obj.add(overlay);
    }
    const [lines, points, faces] = overlay.children;
    const SEL = new THREE.Color(0xffa22b), EDGE = new THREE.Color(0x101010), VERT = new THREE.Color(0x101010);
    const lp = [], lc = [];
    for (const e of em.edges()) {
      lp.push(e.a.co.x, e.a.co.y, e.a.co.z, e.b.co.x, e.b.co.y, e.b.co.z);
      const c = em.esel.has(e.key) ? SEL : EDGE;
      lc.push(c.r, c.g, c.b, c.r, c.g, c.b);
    }
    const pp = [], pc = [];
    for (const v of em.verts) {
      pp.push(v.co.x, v.co.y, v.co.z);
      const c = em.vsel.has(v) ? SEL : VERT;
      pc.push(c.r, c.g, c.b);
    }
    const fp = [];
    for (const f of em.fsel) for (let i = 1; i < f.v.length - 1; i++) for (const v of [f.v[0], f.v[i], f.v[i + 1]]) fp.push(v.co.x, v.co.y, v.co.z);
    const set = (geo, attrs) => {
      const ng = new THREE.BufferGeometry();
      for (const [name, arr] of Object.entries(attrs)) ng.setAttribute(name, new THREE.Float32BufferAttribute(arr, 3));
      ng.computeBoundingSphere();
      geo.geometry.dispose();
      geo.geometry = ng;
    };
    set(lines, { position: lp, color: lc });
    set(points, { position: pp, color: pc });
    set(faces, { position: fp });
    points.visible = this.selectMode === "vert";
    for (const m of [lines.material, points.material, faces.material]) m.depthTest = !this.xray;
    lines.renderOrder = points.renderOrder = 2;
  }

  removeOverlay(obj) {
    const overlay = obj?.getObjectByName("edit-overlay");
    if (!overlay) return;
    overlay.traverse(o => { o.geometry?.dispose(); o.material?.dispose?.(); });
    obj.remove(overlay);
  }

  updateStats() {
    const el = this.hud.stats;
    el.hidden = !this.showStats;
    if (!this.showStats) return;
    if (this.mode === "edit" && this.editObj) {
      const em = this.editObj.userData.em, s = em.stats(), c = em.counts();
      // オブジェクト名は部屋のほかの参加者も変えられるので、HTML ではなく文字として表示する
      setLines(el, [this.editObj.name, `頂点 ${c.verts}/${s.verts}`, `辺 ${c.edges}/${s.edges}`, `面 ${c.faces}/${s.faces}`, `三角形 ${s.tris}`]);
    } else {
      const sum = { verts: 0, edges: 0, faces: 0, tris: 0 };
      for (const o of this.objects) { const s = o.userData.em.stats(); for (const k in sum) sum[k] += s[k]; }
      setLines(el, [`オブジェクト ${this.selected.length}/${this.objects.length}`, `頂点 ${sum.verts}`, `辺 ${sum.edges}`, `面 ${sum.faces}`, `三角形 ${sum.tris}`]);
    }
  }

  updateTools() {
    const t = this.hud.tools;
    this.hud.toolsEdit.hidden = this.mode !== "edit";
    for (const b of t.querySelectorAll("[data-tool]")) {
      const k = b.dataset.tool;
      const on = k === this.selectMode ? this.mode === "edit" : k === "xray" ? this.xray : k === "stats" ? this.showStats : k === "mirror" ? !!this.active?.userData.mirror : false;
      b.classList.toggle("is-on", on);
      b.setAttribute("aria-pressed", on ? "true" : "false");
    }
    t.querySelector('[data-tool="mirror"]').disabled = !this.active;
  }

  setXray(on = !this.xray, quiet = false) {
    this.xray = on;
    for (const o of this.objects) {
      o.material.transparent = on;
      o.material.opacity = on ? 0.35 : 1;
      o.material.depthWrite = !on;
      o.material.needsUpdate = true;
    }
    if (this.editObj) this.rebuildOverlay(this.editObj);
    this.updateTools();
    if (!quiet) { this.setStatus(on ? "透過表示: オン（裏側も選択できる）" : "透過表示: オフ"); this.emit("xray", { on }); }
  }

  setStats(on = !this.showStats, quiet = false) {
    this.showStats = on;
    this.updateStats();
    this.updateTools();
    if (!quiet) this.emit("stats", { on });
  }

  /** アクティブなオブジェクトに X 方向のミラーを付ける / 外す（ミラー モディファイアーの代わり） */
  setMirror(on, obj = this.active) {
    if (!obj) { this.setStatus("先にオブジェクトを選択してください"); return; }
    on = on ?? !obj.userData.mirror;
    obj.userData.mirror = on;
    obj.getObjectByName("mirror").visible = on;
    this.updateTools();
    this.setStatus(on ? `${obj.name} にミラーを追加（X 方向）` : "ミラーを外しました");
    this.emit("mirror", { on, object: obj.name });
  }

  // ---------------------------------------------------------------- 取り消し
  snapshot() {
    return {
      mode: this.mode, edit: this.editObj?.name ?? null,
      objects: this.objects.map(o => ({
        kind: o.userData.kind, name: o.name, rid: o.userData.rid, p: o.position.toArray(), r: o.rotation.toArray().slice(0, 3), s: o.scale.toArray(),
        sel: this.selected.includes(o), act: this.active === o, mesh: o.userData.em.toData(), mirror: o.userData.mirror,
      })),
    };
  }

  restore(snap) {
    this.setMode("object", true);
    for (const o of [...this.objects]) this.removeObject(o);
    for (const d of snap.objects) {
      const m = this.addObject(d.kind, undefined, d.mesh);
      m.name = d.name;
      m.position.fromArray(d.p);
      m.rotation.set(d.r[0], d.r[1], d.r[2]);
      m.scale.fromArray(d.s);
      m.userData.mirror = d.mirror;
      if (d.rid) m.userData.rid = d.rid;
      m.getObjectByName("mirror").visible = d.mirror;
      if (d.sel) this.selected.push(m);
      if (d.act) this.active = m;
    }
    this.setXray(this.xray, true);
    if (snap.mode === "edit" && snap.edit) { this.active = this.find(snap.edit) ?? this.active; this.setMode("edit", true); }
    this.refreshSelection();
    this.updateTools();
  }

  pushUndo() {
    this.undoStack.push(this.snapshot());
    if (this.undoStack.length > 50) this.undoStack.shift();
    this.redoStack = [];
  }

  undo() {
    const snap = this.undoStack.pop();
    if (!snap) { this.setStatus("これ以上元に戻せません"); return; }
    this.redoStack.push(this.snapshot());
    this.restore(snap);
    this.setStatus("元に戻す");
    this.emit("undo");
  }

  redo() {
    const snap = this.redoStack.pop();
    if (!snap) return;
    this.undoStack.push(this.snapshot());
    this.restore(snap);
    this.setStatus("やり直し");
    this.emit("redo");
  }

  // ---------------------------------------------------------------- モード
  /** "object" / "edit"。quiet ならイベントを出さない（内部の切り替え用） */
  setMode(mode, quiet = false) {
    if (mode === this.mode) return true;
    if (mode === "edit") {
      const obj = this.active;
      if (!obj) { if (!quiet) { this.setStatus("先にオブジェクトを選択してください"); this.emit("opfail", { kind: "Tab", reason: "noselection" }); } return false; }
      this.mode = "edit";
      this.editObj = obj;
      if (!this.selected.includes(obj)) this.selected.push(obj);
      obj.userData.em.flush(this.selectMode);
      this.rebuildOverlay(obj);
    } else {
      this.removeOverlay(this.editObj);
      this.mode = "object";
      this.editObj = null;
    }
    this.hud.mode.textContent = this.mode === "edit" ? "編集モード" : "オブジェクトモード";
    this.refreshSelection();
    this.updateTools();
    this.updateStats();
    if (!quiet) {
      this.setStatus(this.mode === "edit" ? `編集モード（${SELECT_MODES[this.selectMode]}選択）。1 / 2 / 3 で頂点・辺・面` : "オブジェクトモード");
      this.emit("mode", { mode: this.mode, object: (this.editObj ?? this.active)?.name ?? null });
    }
    return true;
  }

  toggleEditMode(on = this.mode !== "edit") { return this.setMode(on ? "edit" : "object"); }

  setSelectMode(mode) {
    if (this.mode !== "edit") return;
    this.selectMode = mode;
    const em = this.editObj.userData.em;
    em.flush(mode);
    this.rebuildOverlay(this.editObj);
    this.updateTools();
    this.updateStats();
    this.setStatus(`${SELECT_MODES[mode]}選択モード`);
    this.emit("selectmode", { mode });
  }

  // ---------------------------------------------------------------- 選択（オブジェクト）
  refreshSelection() {
    for (const o of this.objects) {
      const outline = o.getObjectByName("outline");
      outline.visible = this.selected.includes(o) && o !== this.editObj;
      outline.material.color.set(o === this.active ? 0xffb766 : 0xe37d2b);
    }
    this.updateNPanel();
    this.updateStats();
    this.updateTools();
  }

  select(obj, extend = false) {
    if (!extend) this.selected = [];
    if (obj) {
      if (extend && this.selected.includes(obj)) {
        this.selected = this.selected.filter(o => o !== obj);
        this.active = this.selected[this.selected.length - 1] ?? null;
      } else {
        this.selected.push(obj);
        this.active = obj;
      }
    } else if (!extend) {
      this.active = null;
    }
    this.refreshSelection();
  }

  emitSelect(source) {
    const detail = { source, selected: this.selected.map(o => o.name), active: this.active?.name ?? null };
    if (this.mode === "edit") Object.assign(detail, { edit: true, counts: this.editObj.userData.em.counts(), selectMode: this.selectMode });
    this.emit("select", detail);
  }

  /** 名前で選択する（チュートリアルのスキップ用） */
  selectByName(name, extend = false) {
    this.select(this.find(name), extend);
    this.emitSelect(extend ? "shift-click" : "click");
  }

  selectAll(all = true) {
    if (this.mode === "edit") {
      this.editObj.userData.em.selectAll(this.selectMode, all);
      this.editChanged();
    } else if (all) {
      this.selected = [...this.objects];
      this.active = this.selected[this.selected.length - 1] ?? null;
    } else {
      this.selected = [];
      this.active = null;
    }
    this.refreshSelection();
    this.setStatus(all ? "全選択" : "選択解除");
    this.emitSelect(all ? "all" : "none");
  }

  invertSelection() {
    if (this.mode === "edit") {
      this.editObj.userData.em.invert(this.selectMode);
      this.editChanged();
    } else {
      this.selected = this.objects.filter(o => !this.selected.includes(o));
      this.active = this.selected[this.selected.length - 1] ?? null;
      this.refreshSelection();
    }
    this.setStatus("選択を反転");
    this.emitSelect("invert");
  }

  // ---------------------------------------------------------------- 選択（編集モード）
  /** 編集中のメッシュを変えたあとに呼ぶ（表示の更新） */
  editChanged() {
    if (!this.editObj) return;
    this.rebuild(this.editObj);
    this.updateStats();
  }

  worldOf(v, obj = this.editObj) { return v.co.clone().applyMatrix4(obj.matrixWorld); }

  /** p（ワールド座標）が手前の面に隠れていなければ true。透過表示中は常に true */
  isVisible(p, faceHint = null) {
    if (this.xray) return true;
    const ray = new THREE.Raycaster();
    const ndc = p.clone().project(this.camera);
    ray.setFromCamera(new THREE.Vector2(ndc.x, ndc.y), this.camera);
    const hits = ray.intersectObjects(this.objects, false);
    if (!hits.length) return true;
    const d = ray.ray.origin.distanceTo(p);
    if (faceHint && hits[0].object === this.editObj && this.editObj.userData.triFace[hits[0].faceIndex] === faceHint) return true;
    return hits[0].distance >= d - Math.max(0.002, d * 0.002);
  }

  pickVert(px, radius = 14) {
    const obj = this.editObj; obj.updateMatrixWorld();
    let best = null, bestD = radius;
    for (const v of obj.userData.em.verts) {
      const w = this.worldOf(v), s = this.toScreen(w), d = s.distanceTo(px);
      if (d < bestD && this.isVisible(w)) { best = v; bestD = d; }
    }
    return best;
  }

  pickEdge(px, radius = 12) {
    const obj = this.editObj; obj.updateMatrixWorld();
    let best = null, bestD = radius;
    for (const e of obj.userData.em.edges()) {
      const wa = this.worldOf(e.a), wb = this.worldOf(e.b);
      const sa = this.toScreen(wa), sb = this.toScreen(wb);
      const ab = sb.clone().sub(sa), len2 = ab.lengthSq();
      const t = len2 ? THREE.MathUtils.clamp(px.clone().sub(sa).dot(ab) / len2, 0, 1) : 0;
      const d = sa.clone().addScaledVector(ab, t).distanceTo(px);
      if (d < bestD && this.isVisible(wa.clone().lerp(wb, THREE.MathUtils.clamp(t, 0.1, 0.9)))) { best = e; bestD = d; }
    }
    return best;
  }

  pickFace(e) {
    const ray = new THREE.Raycaster();
    ray.setFromCamera(this.ndc(e), this.camera);
    const hits = ray.intersectObject(this.editObj, false);
    return hits.length ? this.editObj.userData.triFace[hits[0].faceIndex] : null;
  }

  /** クリックでの選択（頂点・辺・面のモードに合わせる） */
  editClick(e, extend) {
    const em = this.editObj.userData.em;
    const px = this.pixel(e);
    const item = this.selectMode === "vert" ? this.pickVert(px) : this.selectMode === "edge" ? this.pickEdge(px)?.key : this.pickFace(e);
    if (!item) {
      if (!extend) em.selectAll(this.selectMode, false);
    } else if (extend) {
      em.setSelected(this.selectMode, item);
    } else {
      em.selectAll(this.selectMode, false);
      em.setSelected(this.selectMode, item, true);
    }
    this.editChanged();
    const c = em.counts();
    this.setStatus(item ? `選択: 頂点 ${c.verts} / 辺 ${c.edges} / 面 ${c.faces}` : "選択解除");
    this.emitSelect(extend ? "shift-click" : "click");
  }

  editBox(x0, x1, y0, y1, extend) {
    const obj = this.editObj, em = obj.userData.em;
    obj.updateMatrixWorld();
    const inside = w => { const s = this.toScreen(w); return s.x >= x0 && s.x <= x1 && s.y >= y0 && s.y <= y1; };
    if (!extend) em.selectAll(this.selectMode, false);
    if (this.selectMode === "vert") {
      for (const v of em.verts) { const w = this.worldOf(v); if (inside(w) && this.isVisible(w)) em.vsel.add(v); }
    } else if (this.selectMode === "edge") {
      for (const e of em.edges()) {
        const wa = this.worldOf(e.a), wb = this.worldOf(e.b);
        if (inside(wa) && inside(wb) && this.isVisible(wa.clone().lerp(wb, 0.5))) em.esel.add(e.key);
      }
    } else {
      for (const f of em.faces) { const w = em.faceCenter(f).applyMatrix4(obj.matrixWorld); if (inside(w) && this.isVisible(w, f)) em.fsel.add(f); }
    }
    em.flush(this.selectMode);
    this.editChanged();
    const c = em.counts();
    this.setStatus(`選択: 頂点 ${c.verts} / 辺 ${c.edges} / 面 ${c.faces}`);
    this.emitSelect("box");
  }

  /** Alt+クリック（ループ）/ Ctrl+Alt+クリック（リング） */
  editLoop(e, ring, extend) {
    const edge = this.pickEdge(this.pixel(e), 25);
    if (!edge) { this.setStatus("辺の近くで Alt+クリックしてください"); return; }
    this.selectLoop(edge, ring, extend);
  }

  selectLoop(edge, ring = false, extend = false) {
    const em = this.editObj.userData.em;
    if (!extend) em.selectAll(this.selectMode, false);
    if (this.selectMode === "face") {
      for (const f of em.edgeRing(edge).faces) em.fsel.add(f);
      em.flush("face");
    } else {
      const edges = ring ? em.edgeRing(edge).ring.map(r => r.e) : em.edgeLoop(edge);
      if (this.selectMode === "edge") { for (const x of edges) em.esel.add(x.key); em.flush("edge"); }
      else if (ring) { for (const x of edges) { em.vsel.add(x.a); em.vsel.add(x.b); } em.flush("vert"); }
      else em.selectVerts("vert", edges.flatMap(x => [x.a, x.b]), true);
    }
    this.editChanged();
    const c = em.counts();
    this.setStatus(`${ring ? "辺リング" : "ループ"}を選択: 頂点 ${c.verts} / 辺 ${c.edges} / 面 ${c.faces}`);
    this.emitSelect(ring ? "ring" : "loop");
  }

  /** チュートリアル用: 条件に合う辺（ワールド座標の中点 c、向き d）でループ / リングを選ぶ */
  selectLoopWhere(fn, ring = false) {
    const edge = this.findEdge(fn);
    if (edge) this.selectLoop(edge, ring);
  }

  findEdge(fn) {
    const obj = this.editObj; obj.updateMatrixWorld();
    return obj.userData.em.edges().find(e => {
      const a = this.worldOf(e.a), b = this.worldOf(e.b);
      return fn({ c: a.clone().lerp(b, 0.5), d: b.clone().sub(a).normalize(), a, b });
    }) ?? null;
  }

  /** チュートリアル用: 条件に合う要素を選ぶ。face: {center, normal} / edge: {c, d} / vert: {co}（いずれもワールド座標） */
  editSelectWhere(fn, extend = false) {
    const obj = this.editObj, em = obj.userData.em;
    obj.updateMatrixWorld();
    if (!extend) em.selectAll(this.selectMode, false);
    const nm = new THREE.Matrix3().getNormalMatrix(obj.matrixWorld);
    if (this.selectMode === "face") for (const f of em.faces) { if (fn({ center: em.faceCenter(f).applyMatrix4(obj.matrixWorld), normal: em.faceNormal(f).applyMatrix3(nm).normalize() })) em.fsel.add(f); }
    else if (this.selectMode === "edge") for (const e of em.edges()) { const a = this.worldOf(e.a), b = this.worldOf(e.b); if (fn({ c: a.clone().lerp(b, 0.5), d: b.clone().sub(a).normalize() })) em.esel.add(e.key); }
    else for (const v of em.verts) { if (fn({ co: this.worldOf(v) })) em.vsel.add(v); }
    em.flush(this.selectMode);
    this.editChanged();
    this.emitSelect("click");
  }

  /** チュートリアルの判定用: 編集中のメッシュの選択した面（ワールド座標の中心と法線）と、全頂点の位置 */
  editInfo() {
    const obj = this.editObj ?? this.active;
    if (!obj) return { faces: [], verts: [], stats: null };
    obj.updateMatrixWorld();
    const em = obj.userData.em, nm = new THREE.Matrix3().getNormalMatrix(obj.matrixWorld);
    return {
      faces: [...em.fsel].map(f => ({ center: em.faceCenter(f).applyMatrix4(obj.matrixWorld), normal: em.faceNormal(f).applyMatrix3(nm).normalize(), sides: f.v.length })),
      verts: em.verts.map(v => this.worldOf(v, obj)),
      stats: em.stats(),
    };
  }

  /** L（マウスの下）/ Ctrl+L（今の選択から）でつながった部分を選ぶ */
  selectLinked(underMouse) {
    const em = this.editObj.userData.em;
    let seeds;
    if (underMouse) {
      const px = this.mousePx();
      const fake = { clientX: this.mouse.cx, clientY: this.mouse.cy };
      const v = this.pickVert(px, 30);
      const e = v ? null : this.pickEdge(px, 30);
      const f = v || e ? null : this.pickFace(fake);
      seeds = v ? [v] : e ? [e.a, e.b] : f ? f.v : [];
      if (!seeds.length) { this.setStatus("メッシュの上にマウスを置いて L を押してください"); return; }
    } else {
      seeds = [...em.vsel];
      if (!seeds.length) { this.setStatus("先に一部を選択してください"); return; }
    }
    em.selectVerts(this.selectMode, em.linked(seeds), true);
    this.editChanged();
    const c = em.counts();
    this.setStatus(`つながった部分を選択: 頂点 ${c.verts}`);
    this.emitSelect("linked");
  }

  growShrink(grow) {
    const em = this.editObj.userData.em;
    if (grow) em.grow(this.selectMode); else em.shrink(this.selectMode);
    this.editChanged();
    this.emitSelect(grow ? "grow" : "shrink");
  }

  // ---------------------------------------------------------------- カメラ
  get camera() { return this.ortho ? this.orthoCam : this.persp; }

  updateCamera() {
    const v = this.view;
    const dir = new THREE.Vector3(Math.sin(v.phi) * Math.cos(v.theta), Math.sin(v.phi) * Math.sin(v.theta), Math.cos(v.phi));
    const pos = v.target.clone().addScaledVector(dir, v.dist);
    // 真上・真下から見るときは up が視線と平行になるので、画面の上を +Y にする
    const up = v.phi < 0.02 || v.phi > Math.PI - 0.02 ? new THREE.Vector3(0, 1, 0) : new THREE.Vector3(0, 0, 1);
    for (const cam of [this.persp, this.orthoCam]) {
      cam.up.copy(up);
      cam.position.copy(pos);
      cam.lookAt(v.target);
      cam.updateMatrixWorld();
    }
    const aspect = this.aspect || 1;
    const h = v.dist * 0.42;
    this.orthoCam.left = -h * aspect; this.orthoCam.right = h * aspect; this.orthoCam.top = h; this.orthoCam.bottom = -h;
    this.orthoCam.updateProjectionMatrix();
    this.hud.view.textContent = `${v.name}・${this.ortho ? "平行投影" : "透視投影"}`;
  }

  emitView(action, extra = {}) {
    this.emit("view", { action, name: this.view.name, ortho: this.ortho, ...extra });
  }

  setViewAxis(name, theta, phi) {
    this.view.theta = theta;
    this.view.phi = phi;
    this.view.name = name;
    this.ortho = true;
    this.updateCamera();
    this.emitView("axis");
  }

  /** テンキーの視点切り替え。key は "1" / "3" / "7" / "5" / "Decimal" */
  numpadView(key, ctrl = false) {
    switch (key) {
      case "1": this.setViewAxis(ctrl ? "後" : "前", ctrl ? Math.PI / 2 : -Math.PI / 2, Math.PI / 2); return true;
      case "3": this.setViewAxis(ctrl ? "左" : "右", ctrl ? Math.PI : 0, Math.PI / 2); return true;
      case "7": this.setViewAxis(ctrl ? "下" : "上", -Math.PI / 2, ctrl ? Math.PI - 0.001 : 0.001); return true;
      case "5": this.ortho = !this.ortho; this.emitView("ortho"); return true;
      case "Decimal": this.frame(this.frameTargets()); this.emitView("frame"); return true;
      default: return false;
    }
  }

  frameTargets() {
    if (this.mode === "edit") {
      const em = this.editObj.userData.em;
      if (em.vsel.size) return [...em.vsel].map(v => this.worldOf(v));
    }
    return this.selected.length ? this.selected : this.objects;
  }

  /** 視点を回す（ラジアン）。チュートリアルのスキップ用 */
  orbitBy(dTheta, dPhi = 0) {
    this.view.theta += dTheta;
    this.view.phi = THREE.MathUtils.clamp(this.view.phi + dPhi, 0.01, Math.PI - 0.01);
    this.view.name = "ユーザー";
    this.ortho = false;
  }

  frame(items) {
    if (items.length === 0) return;
    const box = new THREE.Box3();
    for (const o of items) { if (o.isVector3) box.expandByPoint(o); else box.expandByObject(o); }
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3()).length();
    this.view.target.copy(center);
    this.view.dist = Math.max(3, size * 1.6);
  }

  resize() {
    const w = this.root.clientWidth, h = this.root.clientHeight;
    if (w === 0 || h === 0) return;
    this.aspect = w / h;
    this.renderer.setSize(w, h, false);
    this.persp.aspect = this.aspect;
    this.persp.updateProjectionMatrix();
  }

  animate() {
    this.raf = requestAnimationFrame(() => this.animate());
    if (!this.onScreen) return;
    this.updateCamera();
    if (this.guideLine.visible) this.guideLine.material.opacity = 0.55 + 0.35 * Math.sin(performance.now() / 250);
    this.renderer.render(this.scene, this.camera);
    this.updateMarker();
    this.updateRemoteCursors();
  }

  // ---------------------------------------------------------------- 共同練習（room.js）
  /** 部屋で共有する形: [{ id, kind, name, p, r（ラジアン）, s }]。編集モードで変えたメッシュの形は含まない */
  roomState() {
    const r = n => Math.round(n * 10000) / 10000;
    return this.objects.map(o => ({
      id: o.userData.rid, kind: o.userData.kind, name: o.name,
      p: o.position.toArray().map(r), r: [o.rotation.x, o.rotation.y, o.rotation.z].map(r), s: o.scale.toArray().map(r),
    }));
  }

  /**
   * ほかの人の変更を反映する（イベントは出さない）。upsert は追加か変形、delete は削除。
   * 取り消しの記録は、ほかの人の変更と食い違うので消す
   */
  applyRemote({ upsert = [], delete: del = [] }, { keepUndo = false } = {}) {
    const silent = this.silent;
    this.silent = true;
    for (const d of upsert) {
      let o = this.objects.find(x => x.userData.rid === d.id);
      if (!o) {
        o = this.addObject(d.kind);
        o.userData.rid = d.id;
      }
      o.name = d.name;
      o.position.fromArray(d.p);
      o.rotation.set(d.r[0], d.r[1], d.r[2]);
      o.scale.fromArray(d.s);
    }
    for (const id of del) {
      const o = this.objects.find(x => x.userData.rid === id);
      if (o) this.removeObject(o);
    }
    if (upsert.length) this.setXray(this.xray, true);
    if (!keepUndo && (upsert.length || del.length)) { this.undoStack = []; this.redoStack = []; }
    this.refreshSelection();
    this.updateRemoteSelection();
    this.silent = silent;
  }

  /** 部屋に入ったとき: シーンを丸ごと置き換える */
  loadRoomScene(objects) {
    const silent = this.silent;
    this.silent = true;
    this.resetScene([]);
    this.applyRemote({ upsert: objects });
    this.silent = silent;
  }

  /**
   * ほかの参加者の表示。peers: [{ id, name, color, cursor: [x,y,z] | null, selection: [rid], locks: [rid] }]
   * カーソルは名前付きの点、選択と操作中のオブジェクトはその人の色の枠で表示する
   */
  setRemotePeers(peers) {
    this.remotePeers = peers;
    const box = this.root.querySelector(".vp-remote");
    const keep = new Set(peers.map(p => p.id));
    for (const el of [...box.children]) if (!keep.has(el.dataset.peer)) el.remove();
    for (const p of peers) {
      let el = box.querySelector(`[data-peer="${p.id}"]`);
      if (!el) {
        el = document.createElement("div");
        el.className = "vp-remote-cursor";
        el.dataset.peer = p.id;
        el.innerHTML = '<span class="dot"></span><span class="name"></span>';
        box.appendChild(el);
      }
      el.style.setProperty("--peer", p.color);
      el.querySelector(".name").textContent = p.name; // 表示名は文字としてだけ扱う
    }
    this.updateRemoteSelection();
    this.updateRemoteCursors();
  }

  updateRemoteCursors() {
    const box = this.root.querySelector(".vp-remote");
    if (!box || !this.remotePeers) return;
    for (const p of this.remotePeers) {
      const el = box.querySelector(`[data-peer="${p.id}"]`);
      if (!el) continue;
      if (!p.cursor) { el.hidden = true; continue; }
      const v = new THREE.Vector3(...p.cursor);
      const ndc = v.clone().project(this.camera);
      if (ndc.z > 1 || Math.abs(ndc.x) > 1.2 || Math.abs(ndc.y) > 1.2) { el.hidden = true; continue; }
      const s = this.toScreen(v);
      el.hidden = false;
      el.style.transform = `translate(${s.x}px, ${s.y}px)`;
    }
  }

  /** ほかの人が選択・操作しているオブジェクトに、その人の色の枠を付ける */
  updateRemoteSelection() {
    const color = new Map();
    for (const p of this.remotePeers ?? []) {
      for (const id of p.selection ?? []) if (!color.has(id)) color.set(id, p.color);
      for (const id of p.locks ?? []) color.set(id, p.color); // 操作中の人を優先
    }
    for (const o of this.objects) {
      let line = o.getObjectByName("remote-sel");
      const c = color.get(o.userData.rid);
      if (!c) { if (line) line.visible = false; continue; }
      const outline = o.getObjectByName("outline");
      if (!line) {
        line = new THREE.LineSegments(outline.geometry, new THREE.LineBasicMaterial({ color: c, depthTest: false, transparent: true, opacity: 0.9 }));
        line.name = "remote-sel";
        line.renderOrder = 8;
        o.add(line);
      }
      line.geometry = outline.geometry;
      line.material.color.set(c);
      line.visible = true;
    }
  }

  /** マウスの下のワールド座標（オブジェクトの表面か、床 Z = 0）。どちらにも当たらなければ null */
  pointAt(e) {
    const ray = new THREE.Raycaster();
    ray.setFromCamera(this.ndc(e), this.camera);
    const hit = ray.intersectObjects(this.objects, false)[0];
    if (hit) return hit.point;
    const p = ray.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), 0), new THREE.Vector3());
    return p && p.length() < 500 ? p : null;
  }

  getView() {
    const v = this.view;
    return { target: v.target.toArray(), dist: v.dist, theta: v.theta, phi: v.phi, ortho: this.ortho, name: v.name };
  }

  setView(v) {
    this.view.target.fromArray(v.target);
    Object.assign(this.view, { dist: v.dist, theta: v.theta, phi: v.phi, name: v.name || "ユーザー" });
    this.ortho = v.ortho;
  }

  // ---------------------------------------------------------------- チュートリアル用のハイライト
  /**
   * 案内中の対象を目立たせる。省略したものは消える。
   *   object: オブジェクト名（上に吹き出しを出す） label: 吹き出しの文字（省略時はオブジェクト名）
   *   at: [x, y, z]（ワールド座標の点に吹き出しを出す。object より優先）
   *   axis: "x" / "y" / "z"（object を通る点線）  hud: "view"（左上の視点表示を光らせる）
   *   tool: "vert" / "edge" / "face" / "xray" / "stats" / "mirror"（上のボタンを光らせる）
   */
  setHighlight({ object = null, label = "", axis = null, hud = null, at = null, tool = null } = {}) {
    this.marker.target = object;
    this.marker.at = at ? new THREE.Vector3(...at) : null;
    this.marker.label.textContent = label || object || "ここ";
    this.marker.el.hidden = !object && !at;
    this.guideAxis = axis;
    this.guideTarget = object;
    this.hud.view.classList.toggle("is-highlight", hud === "view");
    for (const b of this.hud.tools.querySelectorAll("[data-tool]")) b.classList.toggle("is-highlight", b.dataset.tool === tool);
    this.updateGuide();
  }

  updateGuide() {
    const axis = this.guideAxis;
    // 軸固定中はその線（実線）が出るので、案内の点線は出さない
    if (!axis || (this.op && this.op.axis === axis && !this.op.exclude)) { this.guideLine.visible = false; return; }
    const target = (this.guideTarget && this.find(this.guideTarget)) || this.active;
    this.placeAxisLine(this.guideLine, axis, target ? target.position : new THREE.Vector3());
    this.guideLine.visible = true;
  }

  updateMarker() {
    const m = this.marker;
    if (!m.target && !m.at) return;
    let top;
    if (m.at) top = m.at.clone();
    else {
      const o = this.find(m.target);
      if (!o) { m.el.hidden = true; return; }
      top = o.position.clone();
      top.z += 1.1 * o.scale.z;
    }
    const p = this.toScreen(top);
    m.el.hidden = false;
    m.el.style.transform = `translate(${p.x}px, ${p.y}px)`;
  }

  // ---------------------------------------------------------------- 座標変換
  ndc(e) {
    const r = this.root.getBoundingClientRect();
    return new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
  }

  pixel(e) {
    const r = this.root.getBoundingClientRect();
    return new THREE.Vector2(e.clientX - r.left, e.clientY - r.top);
  }

  /** 最後にマウスがあった位置（ビューポート内のピクセル）。まだ入っていなければ中央 */
  mousePx() {
    const p = this.pixel({ clientX: this.mouse.cx, clientY: this.mouse.cy });
    const inside = Number.isFinite(p.x) && Number.isFinite(p.y) && p.x >= 0 && p.y >= 0 && p.x <= this.root.clientWidth && p.y <= this.root.clientHeight;
    return inside ? p : new THREE.Vector2(this.root.clientWidth / 2, this.root.clientHeight / 2);
  }

  toScreen(v) {
    const p = v.clone().project(this.camera);
    const r = this.root.getBoundingClientRect();
    return new THREE.Vector2((p.x + 1) / 2 * r.width, (1 - p.y) / 2 * r.height);
  }

  /** p の奥行きで、画面の 1 ピクセルがワールドで何 m か */
  worldPerPixel(p) {
    const h = this.root.clientHeight || 1;
    if (this.ortho) return (this.orthoCam.top - this.orthoCam.bottom) / h;
    const d = this.persp.position.distanceTo(p);
    return (2 * d * Math.tan((this.persp.fov * DEG) / 2)) / h;
  }

  /** 画面上の点を、pivot を通るビュー平面上のワールド座標にする */
  unprojectOnPlane(ndc, pivot) {
    const cam = this.camera;
    const normal = cam.getWorldDirection(new THREE.Vector3());
    const plane = new THREE.Plane().setFromNormalAndCoplanarPoint(normal, pivot);
    const ray = new THREE.Raycaster();
    ray.setFromCamera(ndc, cam);
    const out = new THREE.Vector3();
    return ray.ray.intersectPlane(plane, out) ?? pivot.clone();
  }

  pick(e) {
    const ray = new THREE.Raycaster();
    ray.setFromCamera(this.ndc(e), this.camera);
    const hits = ray.intersectObjects(this.objects, false);
    return hits.length > 0 ? hits[0].object : null;
  }

  // ---------------------------------------------------------------- 操作（G / R / S）
  /**
   * opts.noPush: 取り消しの記録をしない（押し出しなど、直前に記録済みのとき）
   * opts.normal: 最初から固定する方向（押し出しの法線。ワールド座標）
   * opts.fromExtrude / fromDuplicate: キャンセルしても複製したものは残す
   */
  startOp(kind, opts = {}) {
    let start, pivot;
    if (this.mode === "edit") {
      const obj = this.editObj, em = obj.userData.em;
      obj.updateMatrixWorld();
      if (em.vsel.size === 0) {
        this.setStatus("先に頂点・辺・面を選択してください");
        this.emit("opfail", { kind, reason: "noselection" });
        return;
      }
      start = [...em.vsel].map(v => ({ v, w: this.worldOf(v) }));
      pivot = new THREE.Vector3();
      for (const s of start) pivot.add(s.w);
      pivot.divideScalar(start.length);
    } else {
      if (this.selected.length === 0) {
        this.setStatus("先にオブジェクトを選択してください");
        this.emit("opfail", { kind, reason: "noselection" });
        return;
      }
      // 共同練習: ほかの人が操作中のオブジェクトは動かせない
      const blocked = this.options.canTransform?.(this.selected);
      if (blocked) {
        this.setStatus(blocked);
        this.emit("opfail", { kind, reason: blocked });
        return;
      }
      start = this.selected.map(o => ({ o, p: o.position.clone(), q: o.quaternion.clone(), s: o.scale.clone() }));
      pivot = new THREE.Vector3();
      for (const o of this.selected) pivot.add(o.position);
      pivot.divideScalar(this.selected.length);
    }
    if (!opts.noPush) this.pushUndo();
    this.op = {
      kind, axis: opts.normal ? "n" : null, exclude: false, numeric: "", pivot, start, edit: this.mode === "edit",
      normal: opts.normal ?? null, fromExtrude: !!opts.fromExtrude, fromDuplicate: !!opts.fromDuplicate,
      inv: this.mode === "edit" ? this.editObj.matrixWorld.clone().invert() : null,
      startNdc: new THREE.Vector2(this.mouse.x, this.mouse.y),
      startPx: this.mousePx(),
    };
    this.applyOp();
    this.emit("opstart", { kind, edit: this.op.edit, extrude: this.op.fromExtrude });
  }

  /** 差し込み（I）・ベベル（B）・ループカットの位置（slide）。moves の頂点を動かす操作 */
  startMoveOp(kind, extra) {
    const obj = this.editObj;
    obj.updateMatrixWorld();
    const pivot = new THREE.Vector3();
    const pts = extra.moves ?? extra.slide;
    for (const m of pts) pivot.add((m.base ?? m.v.co).clone().applyMatrix4(obj.matrixWorld));
    pivot.divideScalar(Math.max(1, pts.length));
    this.op = {
      kind, axis: null, exclude: false, numeric: "", pivot, edit: true, ...extra,
      startNdc: new THREE.Vector2(this.mouse.x, this.mouse.y), startPx: this.mousePx(),
    };
    this.applyOp();
    this.emit("opstart", { kind, edit: true });
  }

  endOp() {
    this.op = null;
    this.hud.numeric.textContent = "";
    this.axisLine.visible = false;
    this.updateGuide();
    this.updateNPanel();
  }

  cancelOp() {
    const op = this.op;
    if (!op) return;
    if (op.kind === "slide") {
      // ループカットの位置調整をやめると、ちょうど真ん中に入る（Blender と同じ）
      op.numeric = "0";
      this.applyOp();
      this.endOp();
      this.setStatus("ループカット（中央）");
      this.emit("confirm", { kind: "slide", value: "0", edit: true, centered: true });
      return;
    }
    if (op.kind === "I" || op.kind === "B") {
      this.endOp();
      this.restore(this.undoStack.pop());
    } else if (op.edit) {
      for (const s of op.start) s.v.co.copy(s.w.clone().applyMatrix4(op.inv));
      this.editObj.userData.em.changed();
      if (!op.fromExtrude) this.undoStack.pop();
      this.endOp();
      this.editChanged();
    } else {
      for (const s of op.start) { s.o.position.copy(s.p); s.o.quaternion.copy(s.q); s.o.scale.copy(s.s); }
      if (!op.fromDuplicate) this.undoStack.pop();
      this.endOp();
    }
    this.setStatus(op.fromExtrude ? "キャンセル（押し出した面は同じ位置に残っています。Ctrl+Z で取り消し）" : "キャンセル");
    this.emit("cancel", { kind: op.kind, edit: op.edit, extrude: op.fromExtrude });
  }

  confirmOp() {
    const op = this.op;
    if (!op) return;
    this.endOp();
    if (op.edit) this.editChanged();
    this.setStatus("確定");
    this.emit("confirm", { kind: op.kind, axis: op.axis, exclude: op.exclude, value: op.numeric, edit: op.edit, extrude: op.fromExtrude, centered: op.kind === "slide" && (op.numeric === "0") });
  }

  setAxis(axis, exclude = this.keys.shift) {
    const op = this.op;
    if (!op || !["G", "R", "S"].includes(op.kind)) return;
    if (op.axis === axis && op.exclude === exclude) {
      // 押し出しは、軸を外すと法線方向に戻る
      op.axis = op.normal ? "n" : null; op.exclude = false;
    } else { op.axis = axis; op.exclude = exclude; }
    this.applyOp();
    this.emit("axis", { kind: op.kind, axis: op.axis, exclude: op.exclude });
  }

  /** 操作中の数値入力。text は 1 文字（数字 / "." / "-"）か "Backspace" */
  typeNumeric(text) {
    const op = this.op;
    if (!op) return;
    if (text === "Backspace") op.numeric = op.numeric.slice(0, -1);
    else if (text === "-") op.numeric = op.numeric.startsWith("-") ? op.numeric.slice(1) : "-" + op.numeric;
    else if (text === ".") { if (!op.numeric.includes(".")) op.numeric += "."; }
    else op.numeric += text;
    this.applyOp();
    this.emit("numeric", { kind: op.kind, value: op.numeric });
  }

  axisVec(a) { return a === "n" ? this.op.normal : AXIS[a]; }

  /** マウスの位置・軸固定・数値入力から、選択物のトランスフォームを更新する */
  applyOp() {
    const op = this.op;
    if (!op) return;
    const cam = this.camera;
    const snap = this.keys.ctrl, fine = this.keys.shift;
    // 入力が空・"-"・"."・"-." のように数値にならないあいだは、数値入力なし(マウスの動きを使う)。"-." が NaN になってオブジェクトが消えるのを防ぐ
    const typed = parseFloat(op.numeric);
    const numeric = Number.isNaN(typed) ? null : typed;
    const cur = new THREE.Vector2(this.mouse.x, this.mouse.y);
    const curPx = this.mousePx();
    const axisName = a => (a === "n" ? "法線" : a.toUpperCase());
    let text = "";
    let move = null; // ワールド座標の点を動かす関数（編集モード用）

    if (op.kind === "G") {
      let delta = this.unprojectOnPlane(cur, op.pivot).sub(this.unprojectOnPlane(op.startNdc, op.pivot));
      if (fine) delta.multiplyScalar(0.1);
      if (op.axis && !op.exclude) {
        const a = this.axisVec(op.axis);
        delta = a.clone().multiplyScalar(delta.dot(a));
      } else if (op.axis && op.exclude) {
        delta[op.axis] = 0;
      }
      if (numeric !== null) {
        const a = op.axis && !op.exclude ? op.axis : "x";
        delta = this.axisVec(a).clone().multiplyScalar(numeric);
      } else if (snap) {
        const step = fine ? 0.1 : 1;
        if (op.axis === "n") delta = op.normal.clone().multiplyScalar(Math.round(delta.dot(op.normal) / step) * step);
        else delta.set(Math.round(delta.x / step) * step, Math.round(delta.y / step) * step, Math.round(delta.z / step) * step);
      }
      if (op.edit) move = w => w.clone().add(delta);
      else for (const s of op.start) s.o.position.copy(s.p).add(delta);
      const label = op.axis ? (op.exclude ? `${op.axis.toUpperCase()} 以外` : axisName(op.axis)) : "";
      text = `${op.fromExtrude ? "押し出し" : "移動"} ${label}: ${op.axis === "n" ? `${delta.dot(op.normal).toFixed(3)} m` : this.fmtVec(delta)}`;
    } else if (op.kind === "R") {
      const pivotPx = this.toScreen(op.pivot);
      const a0 = Math.atan2(op.startPx.y - pivotPx.y, op.startPx.x - pivotPx.x);
      const a1 = Math.atan2(curPx.y - pivotPx.y, curPx.x - pivotPx.x);
      let angle = -(a1 - a0);
      if (fine) angle *= 0.1;
      const axis = op.axis && !op.exclude ? this.axisVec(op.axis).clone() : cam.getWorldDirection(new THREE.Vector3()).negate();
      if (numeric !== null) angle = numeric * DEG;
      else if (snap) angle = Math.round(angle / (5 * DEG)) * 5 * DEG;
      const q = new THREE.Quaternion().setFromAxisAngle(axis, angle);
      if (op.edit) move = w => w.clone().sub(op.pivot).applyQuaternion(q).add(op.pivot);
      else for (const s of op.start) {
        s.o.quaternion.copy(q).multiply(s.q);
        s.o.position.copy(s.p).sub(op.pivot).applyQuaternion(q).add(op.pivot);
      }
      text = `回転 ${op.axis && !op.exclude ? axisName(op.axis) : ""}: ${(angle / DEG).toFixed(2)}°`;
    } else if (op.kind === "S") {
      const pivotPx = this.toScreen(op.pivot);
      const d0 = Math.max(1, op.startPx.distanceTo(pivotPx));
      const d1 = curPx.distanceTo(pivotPx);
      let f = d1 / d0;
      if (fine) f = 1 + (f - 1) * 0.1;
      if (numeric !== null) f = numeric;
      else if (snap) f = Math.round(f * 10) / 10;
      const factor = new THREE.Vector3(1, 1, 1);
      const ax = op.axis === "n" ? null : op.axis;
      if (ax && !op.exclude) factor[ax] = f;
      else if (ax && op.exclude) { factor.set(f, f, f); factor[ax] = 1; }
      else factor.set(f, f, f);
      if (op.edit) move = w => w.clone().sub(op.pivot).multiply(factor).add(op.pivot);
      else for (const s of op.start) {
        s.o.scale.copy(s.s).multiply(factor);
        s.o.position.copy(s.p).sub(op.pivot).multiply(factor).add(op.pivot);
      }
      text = `スケール ${ax ? (op.exclude ? `${ax.toUpperCase()} 以外` : ax.toUpperCase()) : ""}: ${this.fmtVec(factor, 3, "")}`;
    } else if (op.kind === "I" || op.kind === "B") {
      // 差し込み: マウスを中心に近づけるほど厚く / ベベル: 中心から離すほど幅が広い
      const pivotPx = this.toScreen(op.pivot);
      const d0 = op.startPx.distanceTo(pivotPx), d1 = curPx.distanceTo(pivotPx);
      const wpp = this.worldPerPixel(op.pivot);
      let t = numeric ?? Math.max(0, (op.kind === "I" ? d0 - d1 : d1 - d0) * wpp * (fine ? 0.1 : 1));
      if (numeric === null && snap) t = Math.round(t * 10) / 10;
      t = THREE.MathUtils.clamp(t, 0, op.limit);
      for (const m of op.moves) m.v.co.copy(m.base).addScaledVector(m.dir, t);
      this.editObj.userData.em.changed();
      text = op.kind === "I" ? `差し込み  厚さ: ${t.toFixed(3)} m` : `ベベル  幅: ${t.toFixed(3)} m  セグメント: 1`;
    } else if (op.kind === "slide") {
      // ループカットの位置。辺に沿ったマウスの動きで、-1（片側）〜 1（反対側）
      const first = op.slide[0], m = this.editObj.matrixWorld;
      const sa = this.toScreen(first.a.co.clone().applyMatrix4(m)), sb = this.toScreen(first.b.co.clone().applyMatrix4(m));
      const dir = sb.clone().sub(sa), len = Math.max(1, dir.length());
      const span = 1 / (op.cuts + 1);
      let s = numeric ?? curPx.clone().sub(op.startPx).dot(dir.normalize()) / len / span;
      s = THREE.MathUtils.clamp(s, -0.98, 0.98);
      for (const it of op.slide) it.v.co.copy(it.a.co).lerp(it.b.co, it.t + s * span);
      this.editObj.userData.em.changed();
      text = `辺をスライド: ${s.toFixed(3)}（右クリックで中央）`;
    }
    if (move) {
      for (const s of op.start) s.v.co.copy(move(s.w).applyMatrix4(op.inv));
      this.editObj.userData.em.changed();
    }
    if (op.edit) this.rebuild(this.editObj);
    // 軸固定中は、その軸の線を操作の中心に表示する
    this.axisLine.visible = !!(op.axis && !op.exclude && ["G", "R", "S"].includes(op.kind) && !(op.kind === "S" && op.axis === "n"));
    if (this.axisLine.visible) this.placeAxisLine(this.axisLine, op.axis === "n" ? op.normal : op.axis, op.pivot, AXIS_COLOR[op.axis]);
    this.updateGuide();
    const hint = ["G", "R", "S"].includes(op.kind) && !op.axis ? "　（X / Y / Z で軸固定）" : "";
    this.setStatus(text + hint);
    this.hud.numeric.textContent = op.numeric ? `入力: ${op.numeric}` : "";
    this.updateNPanel();
  }

  fmtVec(v, digits = 3, unit = " m") {
    return `X ${v.x.toFixed(digits)}${unit}  Y ${v.y.toFixed(digits)}${unit}  Z ${v.z.toFixed(digits)}${unit}`;
  }

  // ---------------------------------------------------------------- 編集モードの操作
  requireEdit(what) {
    if (this.mode === "edit") return true;
    this.setStatus(`${what}は編集モードの操作です（Tab で編集モードへ）`);
    this.emit("opfail", { kind: what, reason: "objectmode" });
    return false;
  }

  /** E: 押し出してから移動 */
  extrude() {
    if (!this.requireEdit("押し出し")) return;
    const obj = this.editObj, em = obj.userData.em;
    if (!em.vsel.size) { this.setStatus("先に頂点・辺・面を選択してください"); this.emit("opfail", { kind: "E", reason: "noselection" }); return; }
    this.pushUndo();
    const res = em.extrude(this.selectMode);
    if (!res) { this.undoStack.pop(); return; }
    this.editChanged();
    this.emit("extrude", { type: res.type });
    obj.updateMatrixWorld();
    const normal = res.normal ? res.normal.clone().applyMatrix3(new THREE.Matrix3().getNormalMatrix(obj.matrixWorld)).normalize() : null;
    this.startOp("G", { noPush: true, normal, fromExtrude: true });
  }

  /** I: 面の差し込み */
  inset() {
    if (!this.requireEdit("差し込み")) return;
    const em = this.editObj.userData.em;
    if (!em.fsel.size) { this.setStatus("差し込む面を選択してください（3 で面選択モード）"); this.emit("opfail", { kind: "I", reason: "noselection" }); return; }
    this.pushUndo();
    const res = em.inset(this.selectMode);
    this.editChanged();
    this.emit("inset");
    this.startMoveOp("I", { moves: res.moves, limit: res.limit });
  }

  /** Ctrl+B: 辺のベベル（分割数 1） */
  bevel() {
    if (!this.requireEdit("ベベル")) return;
    const em = this.editObj.userData.em;
    this.pushUndo();
    const res = em.bevel(this.selectMode);
    if (typeof res === "string") {
      this.restore(this.undoStack.pop());
      this.setStatus(res);
      this.emit("opfail", { kind: "B", reason: res });
      return;
    }
    this.editChanged();
    this.emit("bevel");
    this.startMoveOp("B", { moves: res.moves, limit: res.limit });
  }

  /** Ctrl+R: ループカット。マウスを辺に重ねると黄色い線、ホイールで本数、クリックで確定して位置調整へ */
  startLoopCut() {
    if (!this.requireEdit("ループカット")) return;
    this.loopcut = { cuts: 1, edge: null, preview: null };
    this.setStatus("ループカット: 辺の上にマウスを置く（ホイールで本数、クリックで確定、右クリックでやめる）");
    this.updateLoopPreview();
    this.emit("opstart", { kind: "loopcut", edit: true });
  }

  endLoopCut() {
    const lc = this.loopcut;
    if (!lc) return;
    if (lc.preview) { lc.preview.geometry.dispose(); lc.preview.removeFromParent(); }
    this.loopcut = null;
  }

  /** edge を省略するとマウスの下の辺。チュートリアルの再現では辺を指定する */
  updateLoopPreview(edge = this.loopcut && this.pickEdge(this.mousePx(), 40)) {
    const lc = this.loopcut;
    if (!lc) return;
    lc.edge = edge;
    if (lc.preview) { lc.preview.geometry.dispose(); lc.preview.removeFromParent(); lc.preview = null; }
    if (!edge) return;
    const em = this.editObj.userData.em;
    const { ring, faces } = em.edgeRing(edge);
    if (!faces.length) { lc.edge = null; this.setStatus("ここにはループカットを入れられません（四角形の面がありません）"); return; }
    const pts = [];
    faces.forEach((_, j) => {
      const r0 = ring[j], r1 = ring[(j + 1) % ring.length];
      for (let m = 1; m <= lc.cuts; m++) {
        const t = m / (lc.cuts + 1);
        pts.push(r0.a.co.clone().lerp(r0.b.co, t), r1.a.co.clone().lerp(r1.b.co, t));
      }
    });
    const line = new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: 0xffe14a, depthTest: false }));
    line.renderOrder = 11;
    this.editObj.add(line);
    lc.preview = line;
    this.setStatus(`ループカット: ${lc.cuts} 本（ホイールで本数、クリックで確定）`);
  }

  setCuts(n) {
    if (!this.loopcut) return;
    this.loopcut.cuts = THREE.MathUtils.clamp(n, 1, 20);
    this.updateLoopPreview(this.loopcut.edge ?? undefined);
    this.emit("cuts", { cuts: this.loopcut.cuts });
  }

  /** プレビュー中の辺でカットし、位置調整（slide）を始める */
  commitLoopCut(edge = this.loopcut?.edge) {
    const cuts = this.loopcut?.cuts ?? 1;
    this.endLoopCut();
    if (!edge) { this.setStatus("ループカットをやめました"); this.emit("cancel", { kind: "loopcut" }); return; }
    this.pushUndo();
    const res = this.editObj.userData.em.loopCut(edge, cuts, this.selectMode);
    if (!res) { this.undoStack.pop(); return; }
    this.editChanged();
    this.emit("loopcut", { cuts });
    this.startMoveOp("slide", { slide: res.slide, cuts });
  }

  /** チュートリアル用: 条件に合う辺の上にマウスを置いたときと同じプレビューを出す */
  previewLoopCut(fn) {
    if (!this.loopcut) this.startLoopCut();
    if (this.loopcut) this.updateLoopPreview(this.findEdge(fn));
  }

  /** チュートリアル用: 条件に合う辺でループカットを入れて、中央で確定する */
  loopCutWhere(fn, cuts = 1) {
    const edge = this.findEdge(fn);
    if (!edge) return;
    this.loopcut = { cuts, edge, preview: null };
    this.commitLoopCut(edge);
    this.cancelOp();
  }

  deleteEdit(what) {
    const em = this.editObj.userData.em;
    if (!em.vsel.size) return;
    this.pushUndo();
    em.remove(what);
    this.editChanged();
    const labels = { vert: "頂点", edge: "辺", face: "面", dissolve: "辺を溶解" };
    this.setStatus(`${labels[what]}${what === "dissolve" ? "" : "を削除"}`);
    this.emit("delete", { edit: true, what, names: [] });
  }

  mergeEdit(how) {
    const em = this.editObj.userData.em;
    this.pushUndo();
    const cursor = this.cursor.position.clone().applyMatrix4(this.editObj.matrixWorld.clone().invert());
    const removed = em.merge(how, cursor);
    if (!removed) this.undoStack.pop();
    this.editChanged();
    this.setStatus(`${removed} 個の頂点を削除`);
    this.emit("merge", { how, removed });
  }

  fillEdit() {
    if (!this.requireEdit("面を張る")) return;
    const em = this.editObj.userData.em;
    this.pushUndo();
    const made = em.fill(this.selectMode);
    if (!made) { this.undoStack.pop(); this.setStatus("面を張れません（頂点を 2 つ以上選んでください）"); return; }
    this.editChanged();
    this.setStatus(made === "face" ? "面を張りました" : "辺を作りました");
    this.emit("fill", { made });
  }

  separateEdit() {
    const em = this.editObj.userData.em;
    if (!em.fsel.size) { this.setStatus("分離する面を選択してください"); return; }
    this.pushUndo();
    const src = this.editObj;
    const part = em.separate();
    this.editChanged();
    const m = this.addObject(src.userData.kind, src.position, part.toData());
    m.quaternion.copy(src.quaternion);
    m.scale.copy(src.scale);
    this.setXray(this.xray, true);
    this.setStatus(`${m.name} に分離しました`);
    this.emit("separate", { name: m.name });
  }

  // ---------------------------------------------------------------- メニュー
  showMenu(title, items, px) {
    this.closeMenu();
    const menu = document.createElement("div");
    menu.className = "practice-menu";
    const head = document.createElement("div");
    head.className = "title";
    head.textContent = title;
    menu.appendChild(head);
    for (const it of items) {
      const b = document.createElement("button");
      b.type = "button";
      b.textContent = it.label;
      b.addEventListener("click", () => { this.closeMenu(); it.run(); this.root.focus({ preventScroll: true }); });
      menu.appendChild(b);
    }
    this.root.appendChild(menu);
    // ビューからはみ出さないように、実際の大きさで位置を決める（下のステータスバーの分も空ける）
    menu.style.left = `${Math.max(0, Math.min(px.x, this.root.clientWidth - menu.offsetWidth - 4))}px`;
    menu.style.top = `${Math.max(0, Math.min(px.y, this.root.clientHeight - menu.offsetHeight - 30))}px`;
    this.menu = menu;
  }

  closeMenu() {
    if (this.menu) { this.menu.remove(); this.menu = null; }
  }

  showAddMenu() {
    if (this.mode === "edit") { this.setStatus("編集モードでは追加できません（Tab でオブジェクトモードへ）"); return; }
    this.showMenu("追加 > メッシュ", PRIMITIVES.map(p => ({ label: p.label, run: () => this.addPrimitive(p.key) })), this.mousePx());
    this.emit("menu", { menu: "add" });
  }

  showDeleteMenu() {
    if (this.mode === "edit") {
      if (!this.editObj.userData.em.vsel.size) { this.setStatus("先に頂点・辺・面を選択してください"); return; }
      this.showMenu("削除", [
        { label: "頂点", run: () => this.deleteEdit("vert") },
        { label: "辺", run: () => this.deleteEdit("edge") },
        { label: "面", run: () => this.deleteEdit("face") },
        { label: "辺を溶解", run: () => this.deleteEdit("dissolve") },
      ], this.mousePx());
      this.emit("menu", { menu: "delete", edit: true });
      return;
    }
    if (!this.selected.length) return;
    this.showMenu("削除", [{ label: "削除", run: () => this.deleteSelected() }], this.mousePx());
    this.emit("menu", { menu: "delete" });
  }

  showMergeMenu() {
    if (!this.requireEdit("マージ")) return;
    if (!this.editObj.userData.em.vsel.size) { this.setStatus("先に頂点を選択してください"); return; }
    this.showMenu("マージ", [
      { label: "中心に", run: () => this.mergeEdit("center") },
      { label: "カーソル位置に", run: () => this.mergeEdit("cursor") },
      { label: "距離で", run: () => this.mergeEdit("distance") },
    ], this.mousePx());
    this.emit("menu", { menu: "merge" });
  }

  showSeparateMenu() {
    if (!this.requireEdit("分離")) return;
    this.showMenu("分離", [{ label: "選択", run: () => this.separateEdit() }], this.mousePx());
    this.emit("menu", { menu: "separate" });
  }

  // ---------------------------------------------------------------- HUD / サイドバー
  setStatus(text) { this.hud.status.textContent = text; }

  toggleNPanel(open = this.npanel.hidden) {
    this.npanel.hidden = !open;
    this.updateNPanel();
    this.emit("npanel", { open });
  }

  updateNPanel() {
    if (this.npanel.hidden) return;
    const o = this.active;
    this.npanel.querySelector(".vp-npanel-name").textContent = o ? o.name : "—";
    for (const input of this.npanel.querySelectorAll("input")) {
      if (!o) { input.value = ""; input.disabled = true; continue; }
      input.disabled = false;
      if (document.activeElement === input) continue;
      const v = o[input.dataset.prop][input.dataset.axis];
      input.value = input.dataset.prop === "rotation" ? (v / DEG).toFixed(1) : v.toFixed(3);
    }
  }

  // ---------------------------------------------------------------- 入力
  bind() {
    const root = this.root;
    const signal = this.abort.signal;
    this.keys = { shift: false, ctrl: false };
    root.addEventListener("contextmenu", e => e.preventDefault(), { signal });

    root.addEventListener("pointermove", e => {
      const n = this.ndc(e);
      this.mouse.x = n.x; this.mouse.y = n.y; this.mouse.cx = e.clientX; this.mouse.cy = e.clientY;
      this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey;
      if (this.loopcut) { this.updateLoopPreview(); return; }
      if (this.op) { this.applyOp(); return; }
      if (this.drag) this.onDrag(e);
    }, { signal });

    root.addEventListener("pointerdown", e => {
      if (e.target.closest("input, button")) return; // サイドバーやメニューの操作
      root.focus({ preventScroll: true });
      this.closeMenu();
      if (this.loopcut) {
        if (e.button === 0) { this.updateLoopPreview(); this.commitLoopCut(); }
        else if (e.button === 2) { this.endLoopCut(); this.setStatus("ループカットをやめました"); this.emit("cancel", { kind: "loopcut" }); }
        return;
      }
      if (this.op) {
        if (e.button === 0) this.confirmOp();
        else if (e.button === 2) this.cancelOp();
        return;
      }
      if (e.button === 0 && e.altKey && this.mode === "edit") {
        // Alt+クリックはループ選択。動かしたら視点を回す
        this.drag = { kind: "altclick", start: this.pixel(e), last: this.pixel(e), distance: 0, ring: e.ctrlKey, shift: e.shiftKey, ev: e };
        root.setPointerCapture(e.pointerId);
        e.preventDefault();
      } else if (e.button === 1 || (e.button === 0 && e.altKey)) {
        this.drag = { kind: e.shiftKey ? "pan" : e.ctrlKey ? "zoom" : "orbit", last: this.pixel(e), distance: 0 };
        root.setPointerCapture(e.pointerId);
        e.preventDefault();
      } else if (e.button === 0) {
        this.drag = { kind: "box", start: this.pixel(e), shift: e.shiftKey, moved: false };
        root.setPointerCapture(e.pointerId);
      }
    }, { signal });

    root.addEventListener("pointerup", e => {
      const d = this.drag;
      this.drag = null;
      if (!d) return;
      if (d.kind === "altclick") {
        this.editLoop(e, d.ring, d.shift);
      } else if (d.kind === "box") {
        if (d.moved) {
          this.finishBox(d, e);
        } else if (this.mode === "edit") {
          this.editClick(e, d.shift);
        } else {
          const hit = this.pick(e);
          this.select(hit, d.shift);
          this.setStatus(hit ? `${hit.name} を選択` : "選択解除");
          this.emitSelect(d.shift ? "shift-click" : "click");
        }
      } else if (d.distance > 0) {
        this.emitView(d.kind, { distance: d.distance });
      }
      if (this.box) { this.box.remove(); this.box = null; }
    }, { signal });

    root.addEventListener("wheel", e => {
      if (this.options.wheelNeedsFocus && document.activeElement !== root) return; // ページのスクロールを優先
      e.preventDefault();
      if (this.loopcut) { this.setCuts(this.loopcut.cuts + (e.deltaY < 0 ? 1 : -1)); return; }
      if (this.op?.kind === "B") { this.setStatus("このビューのベベルは分割数 1 だけです（Blender ではホイールで分割数が増えます）"); return; }
      this.view.dist *= e.deltaY > 0 ? 1.12 : 1 / 1.12;
      this.view.dist = THREE.MathUtils.clamp(this.view.dist, 0.5, 200);
      this.emitView("zoom", { delta: Math.sign(e.deltaY) });
    }, { passive: false, signal });

    root.addEventListener("keydown", e => this.onKey(e), { signal });
    root.addEventListener("keyup", e => { this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey; if (this.op) this.applyOp(); }, { signal });

    for (const input of this.npanel.querySelectorAll("input")) {
      input.addEventListener("change", () => {
        const o = this.active;
        if (!o) return;
        const v = parseFloat(input.value);
        if (Number.isNaN(v)) return;
        this.pushUndo();
        o[input.dataset.prop][input.dataset.axis] = input.dataset.prop === "rotation" ? v * DEG : v;
        this.emit("edit", { prop: input.dataset.prop, axis: input.dataset.axis, value: v });
      }, { signal });
      input.addEventListener("keydown", e => e.stopPropagation(), { signal });
    }

    this.hud.tools.addEventListener("click", e => {
      const t = e.target.closest("[data-tool]")?.dataset.tool;
      if (!t || this.op || this.loopcut) return;
      if (t === "vert" || t === "edge" || t === "face") this.setSelectMode(t);
      else if (t === "xray") this.setXray();
      else if (t === "stats") this.setStats();
      else if (t === "mirror") this.setMirror();
      root.focus({ preventScroll: true });
    }, { signal });
  }

  onDrag(e) {
    const d = this.drag;
    const p = this.pixel(e);
    if (d.kind === "altclick") {
      if (p.distanceTo(d.start) < 5) return;
      d.kind = "orbit"; // 動かしたので視点の回転にする
    }
    if (d.kind !== "box") d.distance += p.distanceTo(d.last);
    if (d.kind === "orbit") {
      const dx = p.x - d.last.x, dy = p.y - d.last.y;
      // 平行投影のまま回すと Blender では透視に戻る（自動透視）
      this.orbitBy(-dx * 0.008, -dy * 0.008);
    } else if (d.kind === "pan") {
      const dx = p.x - d.last.x, dy = p.y - d.last.y;
      const cam = this.camera;
      const right = new THREE.Vector3().setFromMatrixColumn(cam.matrixWorld, 0);
      const up = new THREE.Vector3().setFromMatrixColumn(cam.matrixWorld, 1);
      const k = this.view.dist * 0.0016;
      this.view.target.addScaledVector(right, -dx * k).addScaledVector(up, dy * k);
    } else if (d.kind === "zoom") {
      const dy = p.y - d.last.y;
      this.view.dist = THREE.MathUtils.clamp(this.view.dist * (1 + dy * 0.01), 0.5, 200);
    } else if (d.kind === "box") {
      if (!d.moved && p.distanceTo(d.start) < 4) return;
      d.moved = true;
      if (!this.box) { this.box = document.createElement("div"); this.box.className = "practice-box"; this.root.appendChild(this.box); }
      const x0 = Math.min(p.x, d.start.x), y0 = Math.min(p.y, d.start.y);
      Object.assign(this.box.style, { left: `${x0}px`, top: `${y0}px`, width: `${Math.abs(p.x - d.start.x)}px`, height: `${Math.abs(p.y - d.start.y)}px` });
      return;
    }
    d.last = p;
  }

  finishBox(d, e) {
    const p = this.pixel(e);
    const x0 = Math.min(p.x, d.start.x), x1 = Math.max(p.x, d.start.x), y0 = Math.min(p.y, d.start.y), y1 = Math.max(p.y, d.start.y);
    if (this.mode === "edit") { this.editBox(x0, x1, y0, y1, d.shift); return; }
    if (!d.shift) this.selected = [];
    for (const o of this.objects) {
      const s = this.toScreen(o.position);
      if (s.x >= x0 && s.x <= x1 && s.y >= y0 && s.y <= y1 && !this.selected.includes(o)) { this.selected.push(o); this.active = o; }
    }
    if (this.selected.length === 0) this.active = null;
    this.refreshSelection();
    this.setStatus(`${this.selected.length} 個を選択`);
    this.emitSelect("box");
  }

  onKey(e) {
    if (e.target !== this.root) return;
    this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey;
    const code = e.code;
    this.emit("key", { code, key: e.key, shift: e.shiftKey, ctrl: e.ctrlKey || e.metaKey, alt: e.altKey });
    const emulate = this.options.emulateNumpad?.() ?? false;
    const numpad = code.startsWith("Numpad") ? code.slice(6) : emulate && code.startsWith("Digit") ? code.slice(5) : emulate && e.key === "." ? "Decimal" : null;
    const mod = e.ctrlKey || e.metaKey;

    // ループカットのプレビュー中
    if (this.loopcut) {
      if (code === "Escape") { this.endLoopCut(); this.setStatus("ループカットをやめました"); this.emit("cancel", { kind: "loopcut" }); }
      else if (code === "Enter" || code === "NumpadEnter") this.commitLoopCut();
      e.preventDefault();
      return;
    }

    // 操作中のキー
    if (this.op) {
      if (code === "Escape") this.cancelOp();
      else if (code === "Enter" || code === "NumpadEnter") this.confirmOp();
      else if (code === "KeyX" || code === "KeyY" || code === "KeyZ") this.setAxis(code.slice(3).toLowerCase());
      else if (/^(Digit|Numpad)[0-9]$/.test(code)) this.typeNumeric(code.slice(-1));
      else if (e.key === "." || code === "NumpadDecimal") this.typeNumeric(".");
      else if (e.key === "-" || code === "NumpadSubtract") this.typeNumeric("-");
      else if (code === "Backspace") this.typeNumeric("Backspace");
      else if (code === "ShiftLeft" || code === "ShiftRight" || code === "ControlLeft" || code === "ControlRight") this.applyOp();
      e.preventDefault();
      return;
    }

    // 視点
    if (numpad !== null) {
      if (this.mode === "edit" && mod && (numpad === "Add" || numpad === "Subtract")) { this.growShrink(numpad === "Add"); e.preventDefault(); return; }
      if (this.numpadView(numpad, e.ctrlKey)) e.preventDefault();
      return;
    }
    if (code === "Home") { e.preventDefault(); this.frame(this.objects); this.emitView("home"); return; }

    if (mod) {
      let handled = true;
      if (code === "KeyZ") e.shiftKey ? this.redo() : this.undo();
      else if (code === "KeyI") this.invertSelection();
      else if (code === "KeyR" && this.mode === "edit") this.startLoopCut();
      else if (code === "KeyB" && this.mode === "edit") this.bevel();
      else if (code === "KeyL" && this.mode === "edit") this.selectLinked(false);
      else handled = false;
      if (handled) e.preventDefault();
      return;
    }

    if (code === "Tab") { e.preventDefault(); this.toggleEditMode(); return; }
    if (code === "KeyZ" && e.altKey) { e.preventDefault(); this.setXray(); return; }

    if (this.mode === "edit") {
      if (/^Digit[1-3]$/.test(code)) { this.setSelectMode(["vert", "edge", "face"][+code.slice(5) - 1]); e.preventDefault(); return; }
      switch (code) {
        case "KeyE": this.extrude(); break;
        case "KeyI": this.inset(); break;
        case "KeyF": this.fillEdit(); break;
        case "KeyM": this.showMergeMenu(); break;
        case "KeyP": this.showSeparateMenu(); break;
        case "KeyL": this.selectLinked(true); break;
        case "KeyB": this.setStatus("ボックス選択: ドラッグで囲む（Shift+ドラッグで追加）"); break;
        case "KeyD": if (e.shiftKey) this.setStatus("編集モードの複製（Shift+D）には対応していません"); else return; break;
        case "KeyK": this.setStatus("ナイフ（K）には対応していません"); break;
        case "KeyH": this.setStatus("隠す（H）には対応していません"); break;
        default: return this.commonKey(e);
      }
      e.preventDefault();
      return;
    }
    this.commonKey(e);
  }

  /** オブジェクトモードと編集モードで共通のキー */
  commonKey(e) {
    switch (e.code) {
      case "KeyG": this.startOp("G"); break;
      case "KeyR": this.startOp("R"); break;
      case "KeyS": this.startOp("S"); break;
      case "KeyA":
        if (e.shiftKey) this.showAddMenu();
        else this.selectAll(!e.altKey);
        break;
      case "KeyD":
        if (e.shiftKey) this.duplicate();
        break;
      case "KeyX": this.showDeleteMenu(); break;
      case "Delete": if (this.mode === "edit") this.showDeleteMenu(); else this.deleteSelected(); break;
      case "KeyN": this.toggleNPanel(); break;
      case "KeyB": this.setStatus("ボックス選択: ドラッグで囲む"); break;
      case "Escape": this.closeMenu(); break;
      default: return;
    }
    e.preventDefault();
  }

  addPrimitive(kind) {
    const p = PRIMITIVES.find(x => x.key === kind);
    this.pushUndo();
    const m = this.addObject(kind);
    this.setXray(this.xray, true);
    this.select(m);
    this.setStatus(`${p.label}を追加`);
    this.emit("add", { kind, name: m.name });
  }

  duplicate() {
    if (this.selected.length === 0) return;
    this.pushUndo();
    const copies = this.selected.map(o => {
      const m = this.addObject(o.userData.kind, o.position, o.userData.em.toData());
      m.quaternion.copy(o.quaternion);
      m.scale.copy(o.scale);
      m.userData.mirror = o.userData.mirror;
      this.rebuild(m);
      return m;
    });
    this.setXray(this.xray, true);
    this.selected = copies;
    this.active = copies[copies.length - 1];
    this.refreshSelection();
    this.emit("duplicate", { names: copies.map(o => o.name) });
    this.startOp("G");
    // 複製と移動を 1 回の取り消しにまとめる。キャンセルしても複製は元の位置に残る（Blender と同じ）
    this.undoStack.pop();
    this.op.fromDuplicate = true;
    this.setStatus("複製。マウスで位置を決めてクリック");
  }

  deleteSelected() {
    if (this.selected.length === 0) return;
    this.pushUndo();
    const names = this.selected.map(o => o.name);
    for (const o of [...this.selected]) this.removeObject(o);
    this.refreshSelection();
    this.setStatus("削除");
    this.emit("delete", { names });
  }
}
