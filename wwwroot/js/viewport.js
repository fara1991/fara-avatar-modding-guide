// Blender と同じキー操作で動かせる 3D ビュー（Three.js）。
// 操作の練習（practice.js）と記事内チュートリアル（tutorial.js）の両方から使う。1 ページに何個でも置ける。
// 対応している操作は Practice.razor の「使えるキー」の表のとおり。編集モードは未対応。
//
//   const vp = new Viewport(element, { scene: [{ kind: "cube" }] });
//   vp.on("confirm", e => ...);   // 操作のたびにイベントが届く（一覧は emit を呼んでいる箇所を参照）
//   vp.dispose();                 // WebGL コンテキストを解放する
//
// イベント（e.type と詳細）:
//   key {code, key, shift, ctrl, alt}       キーが押された（操作の前に届く）
//   select {source, selected, active}        source: click / shift-click / box / all / none
//   opstart {kind}  axis {kind, axis, exclude}  numeric {kind, value}
//   confirm {kind, axis, exclude, value}     cancel {kind}   opfail {kind, reason}
//   view {action, name, ortho, distance}     action: orbit / pan / zoom / axis / ortho / frame / home
//   menu {menu}  add {kind, name}  duplicate {names}  delete {names}  undo  redo  npanel {open}  edit {prop, axis, value}
import * as THREE from "three";

export const DEG = Math.PI / 180;
const AXIS = { x: new THREE.Vector3(1, 0, 0), y: new THREE.Vector3(0, 1, 0), z: new THREE.Vector3(0, 0, 1) };
const AXIS_COLOR = { x: 0xff3352, y: 0x8bdc00, z: 0x2890ff };
const DEFAULT_VIEW = { dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, name: "ユーザー" };

// 追加できるメッシュ（Shift+A）。Blender の「メッシュ」メニューの順
export const PRIMITIVES = [
  { key: "plane", label: "平面", make: () => new THREE.PlaneGeometry(2, 2) },
  { key: "cube", label: "立方体", make: () => new THREE.BoxGeometry(2, 2, 2) },
  { key: "sphere", label: "UV球", make: () => new THREE.SphereGeometry(1, 32, 16) },
  { key: "cylinder", label: "円柱", make: () => new THREE.CylinderGeometry(1, 1, 2, 32).rotateX(Math.PI / 2) },
  { key: "cone", label: "円錐", make: () => new THREE.ConeGeometry(1, 2, 32).rotateX(Math.PI / 2) },
  { key: "torus", label: "トーラス", make: () => new THREE.TorusGeometry(1, 0.25, 12, 48) },
];
const NAMES = { plane: "Plane", cube: "Cube", sphere: "Sphere", cylinder: "Cylinder", cone: "Cone", torus: "Torus" };

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
    this.hud = { mode: q(".vp-mode"), view: q(".vp-view"), status: q(".vp-status"), numeric: q(".vp-numeric") };
    this.npanel = q(".practice-npanel");
    this.marker = { el: q(".vp-marker"), label: q(".vp-marker-label"), target: null };

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

  /** 線を原点で X 方向に作ってあるので、向きと位置を合わせる */
  placeAxisLine(line, axis, at) {
    line.position.copy(at);
    line.quaternion.setFromUnitVectors(AXIS.x, AXIS[axis]);
    line.material.color.set(AXIS_COLOR[axis]);
  }

  makeObject(kind) {
    const def = PRIMITIVES.find(p => p.key === kind);
    const mesh = new THREE.Mesh(def.make(), new THREE.MeshStandardMaterial({ color: 0x9c9c9c, roughness: 0.75, metalness: 0.0, side: kind === "plane" ? THREE.DoubleSide : THREE.FrontSide }));
    mesh.userData.kind = kind;
    mesh.name = this.uniqueName(NAMES[kind]);
    const edges = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, 20), new THREE.LineBasicMaterial({ color: 0xff9d2b }));
    edges.visible = false;
    edges.name = "outline";
    mesh.add(edges);
    return mesh;
  }

  uniqueName(base) {
    if (!this.objects.some(o => o.name === base)) return base;
    for (let i = 1; ; i++) {
      const n = `${base}.${String(i).padStart(3, "0")}`;
      if (!this.objects.some(o => o.name === n)) return n;
    }
  }

  addObject(kind, at = this.cursor.position) {
    const mesh = this.makeObject(kind);
    mesh.position.copy(at);
    this.world.add(mesh);
    this.objects.push(mesh);
    return mesh;
  }

  removeObject(mesh) {
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
    Object.assign(this.view, DEFAULT_VIEW);
    this.view.target.set(0, 0, 0);
    this.npanel.hidden = true;
    this.hud.numeric.textContent = "";
    this.axisLine.visible = false;
    this.refreshSelection();
    this.setStatus("クリックして選択。G / R / S で動かす");
  }

  // ---------------------------------------------------------------- 取り消し
  snapshot() {
    return this.objects.map(o => ({ kind: o.userData.kind, name: o.name, p: o.position.toArray(), r: o.rotation.toArray().slice(0, 3), s: o.scale.toArray(), sel: this.selected.includes(o), act: this.active === o }));
  }

  restore(snap) {
    for (const o of [...this.objects]) this.removeObject(o);
    for (const d of snap) {
      const m = this.addObject(d.kind);
      m.name = d.name;
      m.position.fromArray(d.p);
      m.rotation.set(d.r[0], d.r[1], d.r[2]);
      m.scale.fromArray(d.s);
      if (d.sel) this.selected.push(m);
      if (d.act) this.active = m;
    }
    this.refreshSelection();
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

  // ---------------------------------------------------------------- 選択
  refreshSelection() {
    for (const o of this.objects) {
      const outline = o.getObjectByName("outline");
      outline.visible = this.selected.includes(o);
      outline.material.color.set(o === this.active ? 0xffb766 : 0xe37d2b);
    }
    this.updateNPanel();
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
    this.emit("select", { source, selected: this.selected.map(o => o.name), active: this.active?.name ?? null });
  }

  /** 名前で選択する（チュートリアルのスキップ用） */
  selectByName(name, extend = false) {
    this.select(this.find(name), extend);
    this.emitSelect(extend ? "shift-click" : "click");
  }

  selectAll(all = true) {
    if (all) {
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
    this.emitView("axis");
  }

  /** テンキーの視点切り替え。key は "1" / "3" / "7" / "5" / "Decimal" */
  numpadView(key, ctrl = false) {
    switch (key) {
      case "1": this.setViewAxis(ctrl ? "後" : "前", ctrl ? Math.PI / 2 : -Math.PI / 2, Math.PI / 2); return true;
      case "3": this.setViewAxis(ctrl ? "左" : "右", ctrl ? Math.PI : 0, Math.PI / 2); return true;
      case "7": this.setViewAxis(ctrl ? "下" : "上", -Math.PI / 2, ctrl ? Math.PI - 0.001 : 0.001); return true;
      case "5": this.ortho = !this.ortho; this.emitView("ortho"); return true;
      case "Decimal": this.frame(this.selected.length ? this.selected : this.objects); this.emitView("frame"); return true;
      default: return false;
    }
  }

  /** 視点を回す（ラジアン）。チュートリアルのスキップ用 */
  orbitBy(dTheta, dPhi = 0) {
    this.view.theta += dTheta;
    this.view.phi = THREE.MathUtils.clamp(this.view.phi + dPhi, 0.01, Math.PI - 0.01);
    this.view.name = "ユーザー";
    this.ortho = false;
  }

  frame(objs) {
    if (objs.length === 0) return;
    const box = new THREE.Box3();
    for (const o of objs) box.expandByObject(o);
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
  }

  // ---------------------------------------------------------------- チュートリアル用のハイライト
  /**
   * 案内中の対象を目立たせる。省略したものは消える。
   *   object: オブジェクト名（上に吹き出しを出す） label: 吹き出しの文字（省略時はオブジェクト名）
   *   axis: "x" / "y" / "z"（object を通る点線）  hud: "view"（左上の視点表示を光らせる）
   */
  setHighlight({ object = null, label = "", axis = null, hud = null } = {}) {
    this.marker.target = object;
    this.marker.label.textContent = label || object || "";
    this.marker.el.hidden = !object;
    this.guideAxis = axis;
    this.guideTarget = object;
    this.hud.view.classList.toggle("is-highlight", hud === "view");
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
    if (!m.target) return;
    const o = this.find(m.target);
    if (!o) { m.el.hidden = true; return; }
    const top = o.position.clone();
    top.z += 1.1 * o.scale.z;
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
  startOp(kind) {
    if (this.selected.length === 0) {
      this.setStatus("先にオブジェクトを選択してください");
      this.emit("opfail", { kind, reason: "noselection" });
      return;
    }
    this.pushUndo();
    const pivot = new THREE.Vector3();
    for (const o of this.selected) pivot.add(o.position);
    pivot.divideScalar(this.selected.length);
    this.op = {
      kind, axis: null, exclude: false, numeric: "", pivot,
      start: this.selected.map(o => ({ o, p: o.position.clone(), q: o.quaternion.clone(), s: o.scale.clone() })),
      startNdc: new THREE.Vector2(this.mouse.x, this.mouse.y),
      startPx: this.mousePx(),
    };
    this.applyOp();
    this.emit("opstart", { kind });
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
    for (const s of op.start) { s.o.position.copy(s.p); s.o.quaternion.copy(s.q); s.o.scale.copy(s.s); }
    if (!op.fromDuplicate) this.undoStack.pop();
    this.endOp();
    this.setStatus("キャンセル");
    this.emit("cancel", { kind: op.kind });
  }

  confirmOp() {
    const op = this.op;
    if (!op) return;
    this.endOp();
    this.setStatus("確定");
    this.emit("confirm", { kind: op.kind, axis: op.axis, exclude: op.exclude, value: op.numeric });
  }

  setAxis(axis, exclude = this.keys.shift) {
    if (!this.op) return;
    if (this.op.axis === axis && this.op.exclude === exclude) { this.op.axis = null; this.op.exclude = false; }
    else { this.op.axis = axis; this.op.exclude = exclude; }
    this.applyOp();
    this.emit("axis", { kind: this.op.kind, axis: this.op.axis, exclude: this.op.exclude });
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

  /** マウスの位置・軸固定・数値入力から、選択物のトランスフォームを更新する */
  applyOp() {
    const op = this.op;
    if (!op) return;
    const cam = this.camera;
    const snap = this.keys.ctrl, fine = this.keys.shift;
    const numeric = op.numeric === "" || op.numeric === "-" || op.numeric === "." ? null : parseFloat(op.numeric);
    const cur = new THREE.Vector2(this.mouse.x, this.mouse.y);
    let text = "";

    if (op.kind === "G") {
      let delta = this.unprojectOnPlane(cur, op.pivot).sub(this.unprojectOnPlane(op.startNdc, op.pivot));
      if (fine) delta.multiplyScalar(0.1);
      if (op.axis && !op.exclude) {
        const a = AXIS[op.axis];
        delta = a.clone().multiplyScalar(delta.dot(a));
      } else if (op.axis && op.exclude) {
        delta[op.axis] = 0;
      }
      if (numeric !== null) {
        const a = op.axis && !op.exclude ? op.axis : "x";
        delta = AXIS[a].clone().multiplyScalar(numeric);
      } else if (snap) {
        const step = fine ? 0.1 : 1;
        delta.set(Math.round(delta.x / step) * step, Math.round(delta.y / step) * step, Math.round(delta.z / step) * step);
      }
      for (const s of op.start) s.o.position.copy(s.p).add(delta);
      const label = op.axis ? (op.exclude ? `${op.axis.toUpperCase()} 以外` : op.axis.toUpperCase()) : "";
      text = `移動 ${label}: ${this.fmtVec(delta)}`;
    } else if (op.kind === "R") {
      const pivotPx = this.toScreen(op.pivot);
      const curPx = this.mousePx();
      const a0 = Math.atan2(op.startPx.y - pivotPx.y, op.startPx.x - pivotPx.x);
      const a1 = Math.atan2(curPx.y - pivotPx.y, curPx.x - pivotPx.x);
      let angle = -(a1 - a0);
      if (fine) angle *= 0.1;
      const axis = op.axis && !op.exclude ? AXIS[op.axis].clone() : cam.getWorldDirection(new THREE.Vector3()).negate();
      if (numeric !== null) angle = numeric * DEG;
      else if (snap) angle = Math.round(angle / (5 * DEG)) * 5 * DEG;
      const q = new THREE.Quaternion().setFromAxisAngle(axis, angle);
      for (const s of op.start) {
        s.o.quaternion.copy(q).multiply(s.q);
        s.o.position.copy(s.p).sub(op.pivot).applyQuaternion(q).add(op.pivot);
      }
      text = `回転 ${op.axis && !op.exclude ? op.axis.toUpperCase() : ""}: ${(angle / DEG).toFixed(2)}°`;
    } else if (op.kind === "S") {
      const pivotPx = this.toScreen(op.pivot);
      const curPx = this.mousePx();
      const d0 = Math.max(1, op.startPx.distanceTo(pivotPx));
      const d1 = curPx.distanceTo(pivotPx);
      let f = d1 / d0;
      if (fine) f = 1 + (f - 1) * 0.1;
      if (numeric !== null) f = numeric;
      else if (snap) f = Math.round(f * 10) / 10;
      const factor = new THREE.Vector3(1, 1, 1);
      if (op.axis && !op.exclude) factor[op.axis] = f;
      else if (op.axis && op.exclude) { factor.set(f, f, f); factor[op.axis] = 1; }
      else factor.set(f, f, f);
      for (const s of op.start) {
        s.o.scale.copy(s.s).multiply(factor);
        s.o.position.copy(s.p).sub(op.pivot).multiply(factor).add(op.pivot);
      }
      text = `スケール ${op.axis ? (op.exclude ? `${op.axis.toUpperCase()} 以外` : op.axis.toUpperCase()) : ""}: ${this.fmtVec(factor, 3, "")}`;
    }
    // 軸固定中は、その軸の線を操作の中心に表示する
    this.axisLine.visible = !!(op.axis && !op.exclude);
    if (this.axisLine.visible) this.placeAxisLine(this.axisLine, op.axis, op.pivot);
    this.updateGuide();
    this.setStatus(text + (op.axis ? "" : "　（X / Y / Z で軸固定）"));
    this.hud.numeric.textContent = op.numeric ? `入力: ${op.numeric}` : "";
    this.updateNPanel();
  }

  fmtVec(v, digits = 3, unit = " m") {
    return `X ${v.x.toFixed(digits)}${unit}  Y ${v.y.toFixed(digits)}${unit}  Z ${v.z.toFixed(digits)}${unit}`;
  }

  // ---------------------------------------------------------------- メニュー
  showMenu(title, items, px) {
    this.closeMenu();
    const menu = document.createElement("div");
    menu.className = "practice-menu";
    menu.innerHTML = `<div class="title">${title}</div>`;
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
    this.showMenu("追加 > メッシュ", PRIMITIVES.map(p => ({ label: p.label, run: () => this.addPrimitive(p.key) })), this.mousePx());
    this.emit("menu", { menu: "add" });
  }

  showDeleteMenu() {
    if (!this.selected.length) return;
    this.showMenu("削除", [{ label: "削除", run: () => this.deleteSelected() }], this.mousePx());
    this.emit("menu", { menu: "delete" });
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
      if (this.op) { this.applyOp(); return; }
      if (this.drag) this.onDrag(e);
    }, { signal });

    root.addEventListener("pointerdown", e => {
      if (e.target.closest("input, button")) return; // サイドバーやメニューの操作
      root.focus({ preventScroll: true });
      this.closeMenu();
      if (this.op) {
        if (e.button === 0) this.confirmOp();
        else if (e.button === 2) this.cancelOp();
        return;
      }
      if (e.button === 1 || (e.button === 0 && e.altKey)) {
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
      if (d.kind === "box") {
        if (d.moved) {
          this.finishBox(d, e);
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
  }

  onDrag(e) {
    const d = this.drag;
    const p = this.pixel(e);
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
      if (this.numpadView(numpad, e.ctrlKey)) e.preventDefault();
      return;
    }
    if (code === "Home") { e.preventDefault(); this.frame(this.objects); this.emitView("home"); return; }

    const mod = e.ctrlKey || e.metaKey;
    if (mod && code === "KeyZ") { e.preventDefault(); e.shiftKey ? this.redo() : this.undo(); return; }
    if (mod) return;

    switch (code) {
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
      case "Delete": this.deleteSelected(); break;
      case "KeyN": this.toggleNPanel(); break;
      case "Escape": this.closeMenu(); break;
      case "Tab": this.setStatus("編集モードには対応していません"); return; // フォーカス移動は止めない
      default: return;
    }
    e.preventDefault();
  }

  addPrimitive(kind) {
    const p = PRIMITIVES.find(x => x.key === kind);
    this.pushUndo();
    const m = this.addObject(kind);
    this.select(m);
    this.setStatus(`${p.label}を追加`);
    this.emit("add", { kind, name: m.name });
  }

  duplicate() {
    if (this.selected.length === 0) return;
    this.pushUndo();
    const copies = this.selected.map(o => {
      const m = this.addObject(o.userData.kind, o.position);
      m.quaternion.copy(o.quaternion);
      m.scale.copy(o.scale);
      return m;
    });
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
