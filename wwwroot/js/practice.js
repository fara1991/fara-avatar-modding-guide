// 操作の練習（/practice）。Blender と同じキー操作で立方体を動かせる 3D ビューを Three.js で作る。
// 対応している操作は Practice.razor の「使えるキー」の表のとおり。編集モードは未対応。
import * as THREE from "three";

const DEG = Math.PI / 180;
const AXIS = { x: new THREE.Vector3(1, 0, 0), y: new THREE.Vector3(0, 1, 0), z: new THREE.Vector3(0, 0, 1) };
const AXIS_COLOR = { x: 0xff3352, y: 0x8bdc00, z: 0x2890ff };

// 追加できるメッシュ（Shift+A）。Blender の「メッシュ」メニューの順
const PRIMITIVES = [
  { key: "plane", label: "平面", make: () => new THREE.PlaneGeometry(2, 2) },
  { key: "cube", label: "立方体", make: () => new THREE.BoxGeometry(2, 2, 2) },
  { key: "sphere", label: "UV球", make: () => new THREE.SphereGeometry(1, 32, 16) },
  { key: "cylinder", label: "円柱", make: () => new THREE.CylinderGeometry(1, 1, 2, 32).rotateX(Math.PI / 2) },
  { key: "cone", label: "円錐", make: () => new THREE.ConeGeometry(1, 2, 32).rotateX(Math.PI / 2) },
  { key: "torus", label: "トーラス", make: () => new THREE.TorusGeometry(1, 0.25, 12, 48) },
];
const NAMES = { plane: "Plane", cube: "Cube", sphere: "Sphere", cylinder: "Cylinder", cone: "Cone", torus: "Torus" };

// お題。check は現在の状態を受け取って達成なら true
const TASKS = [
  { text: "立方体をクリックして選択する", check: s => s.selected.some(o => o.userData.kind === "cube") },
  { text: "G → Z → 2 → Enter で、立方体を上に 2 m 動かす", check: s => s.objects.some(o => o.userData.kind === "cube" && near(o.position.z, 2)) },
  { text: "R → Z → 45 → Enter で、立方体を 45° 回転する", check: s => s.objects.some(o => o.userData.kind === "cube" && nearAngle(o.rotation.z, 45)) },
  { text: "S → 2 → Enter で、立方体を 2 倍にする", check: s => s.objects.some(o => o.userData.kind === "cube" && near(o.scale.x, 2) && near(o.scale.z, 2)) },
  { text: "Shift+A で UV 球を追加する", check: s => s.objects.some(o => o.userData.kind === "sphere") },
  { text: "球を選んで G → X → 3 → Enter で、X 方向に 3 m 動かす", check: s => s.objects.some(o => o.userData.kind === "sphere" && near(o.position.x, 3)) },
  { text: "立方体を選んで Shift+D で複製し、Y 方向に 3 m 離す", check: s => s.objects.filter(o => o.userData.kind === "cube").length >= 2 && s.objects.some(o => o.userData.kind === "cube" && near(o.position.y, 3)) },
  { text: "Numpad 1 で正面から見る（テンキーがなければ「テンキーを模倣」をオンにして 1）", check: s => s.viewName === "前" },
  { text: "Numpad 5 で平行投影に切り替える", check: s => s.ortho },
  { text: "Ctrl+Z で直前の操作を元に戻す", check: s => s.undoUsed },
  { text: "球を選んで X で削除する", check: s => s.sphereAdded && !s.objects.some(o => o.userData.kind === "sphere") },
];

function near(a, b, eps = 0.05) { return Math.abs(a - b) <= eps; }
function nearAngle(rad, deg, eps = 1) {
  let d = ((rad / DEG - deg) % 360 + 540) % 360 - 180;
  return Math.abs(d) <= eps;
}

class Practice {
  constructor(root) {
    this.root = root;
    this.hud = { mode: document.getElementById("hud-mode"), view: document.getElementById("hud-view"), status: document.getElementById("hud-status"), numeric: document.getElementById("hud-numeric") };
    this.npanel = document.getElementById("npanel");
    this.taskList = document.getElementById("practice-tasks");
    this.tasksDone = document.getElementById("practice-tasks-done");
    this.emulateNumpad = document.getElementById("practice-emulate-numpad");

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
    this.view = { target: new THREE.Vector3(), dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, name: "ユーザー" };

    this.objects = [];
    this.selected = [];
    this.active = null;
    this.op = null;
    this.undoStack = [];
    this.redoStack = [];
    this.state = { undoUsed: false, sphereAdded: false };
    this.taskIndex = 0;
    this.taskDone = TASKS.map(() => false);
    const rect = root.getBoundingClientRect();
    this.mouse = { x: 0, y: 0, cx: rect.left + rect.width / 2, cy: rect.top + rect.height / 2 };
    this.box = null;
    this.menu = null;

    this.buildScene();
    this.resetScene();
    this.bind();
    this.renderTasks();
    this.resize();
    new ResizeObserver(() => this.resize()).observe(root);
    this.animate();
  }

  // ---------------------------------------------------------------- シーン
  buildScene() {
    const grid = new THREE.GridHelper(40, 40, 0x4e4e4e, 0x484848);
    grid.rotation.x = Math.PI / 2;
    this.scene.add(grid);
    const axis = (color, from, to) => {
      const g = new THREE.BufferGeometry().setFromPoints([from, to]);
      this.scene.add(new THREE.Line(g, new THREE.LineBasicMaterial({ color })));
    };
    axis(0x9a3a4a, new THREE.Vector3(-20, 0, 0), new THREE.Vector3(20, 0, 0));
    axis(0x5a8a2a, new THREE.Vector3(0, -20, 0), new THREE.Vector3(0, 20, 0));

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
    if (kind === "sphere") this.state.sphereAdded = true;
    return mesh;
  }

  removeObject(mesh) {
    this.world.remove(mesh);
    this.objects = this.objects.filter(o => o !== mesh);
    this.selected = this.selected.filter(o => o !== mesh);
    if (this.active === mesh) this.active = this.selected[this.selected.length - 1] ?? null;
  }

  resetScene() {
    for (const o of [...this.objects]) this.removeObject(o);
    this.addObject("cube");
    this.selected = [];
    this.active = null;
    this.op = null;
    this.undoStack = [];
    this.redoStack = [];
    this.state = { undoUsed: false, sphereAdded: false };
    this.taskIndex = 0;
    this.taskDone = TASKS.map(() => false);
    this.ortho = false;
    Object.assign(this.view, { dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: 63 * DEG, name: "ユーザー" });
    this.view.target.set(0, 0, 0);
    this.refreshSelection();
    this.setStatus("クリックして選択。G / R / S で動かす");
    this.renderTasks();
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
    if (!snap) return;
    this.redoStack.push(this.snapshot());
    this.restore(snap);
    this.state.undoUsed = true;
    this.setStatus("元に戻す");
  }

  redo() {
    const snap = this.redoStack.pop();
    if (!snap) return;
    this.undoStack.push(this.snapshot());
    this.restore(snap);
    this.setStatus("やり直し");
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

  setViewAxis(name, theta, phi) {
    this.view.theta = theta;
    this.view.phi = phi;
    this.view.name = name;
    this.ortho = true;
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
    requestAnimationFrame(() => this.animate());
    this.updateCamera();
    this.renderer.render(this.scene, this.camera);
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
    if (!Number.isFinite(p.x) || !Number.isFinite(p.y)) return new THREE.Vector2(this.root.clientWidth / 2, this.root.clientHeight / 2);
    return p;
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
    if (this.selected.length === 0) { this.setStatus("先にオブジェクトを選択してください"); return; }
    this.pushUndo();
    const pivot = new THREE.Vector3();
    for (const o of this.selected) pivot.add(o.position);
    pivot.divideScalar(this.selected.length);
    this.op = {
      kind, axis: null, exclude: false, numeric: "", pivot,
      start: this.selected.map(o => ({ o, p: o.position.clone(), q: o.quaternion.clone(), s: o.scale.clone() })),
      startNdc: new THREE.Vector2(this.mouse.x, this.mouse.y),
      startPx: this.mousePx(),
      lastAngle: null, accum: 0,
    };
    this.applyOp();
  }

  cancelOp() {
    if (!this.op) return;
    for (const s of this.op.start) { s.o.position.copy(s.p); s.o.quaternion.copy(s.q); s.o.scale.copy(s.s); }
    this.op = null;
    this.undoStack.pop();
    this.setStatus("キャンセル");
    this.hud.numeric.textContent = "";
    this.updateNPanel();
  }

  confirmOp() {
    if (!this.op) return;
    this.op = null;
    this.hud.numeric.textContent = "";
    this.setStatus("確定");
    this.updateNPanel();
    this.checkTasks();
  }

  setAxis(axis) {
    if (!this.op) return;
    const exclude = this.keys.shift;
    if (this.op.axis === axis && this.op.exclude === exclude) { this.op.axis = null; this.op.exclude = false; }
    else { this.op.axis = axis; this.op.exclude = exclude; }
    this.applyOp();
  }

  /** マウスの位置・軸固定・数値入力から、選択物のトランスフォームを更新する */
  applyOp() {
    const op = this.op;
    if (!op) return;
    const cam = this.camera;
    const snap = this.keys.ctrl, fine = this.keys.shift;
    const numeric = op.numeric === "" || op.numeric === "-" ? null : parseFloat(op.numeric);
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
      let axis = op.axis && !op.exclude ? AXIS[op.axis].clone() : cam.getWorldDirection(new THREE.Vector3()).negate();
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
    menu.style.left = `${Math.min(px.x, this.root.clientWidth - 200)}px`;
    menu.style.top = `${Math.min(px.y, this.root.clientHeight - items.length * 28 - 40)}px`;
    menu.innerHTML = `<div class="title">${title}</div>`;
    for (const it of items) {
      const b = document.createElement("button");
      b.type = "button";
      b.textContent = it.label;
      b.addEventListener("click", () => { this.closeMenu(); it.run(); this.root.focus(); });
      menu.appendChild(b);
    }
    this.root.appendChild(menu);
    this.menu = menu;
  }

  closeMenu() {
    if (this.menu) { this.menu.remove(); this.menu = null; }
  }

  // ---------------------------------------------------------------- HUD / サイドバー
  setStatus(text) { this.hud.status.textContent = text; }

  updateNPanel() {
    if (this.npanel.hidden) return;
    const o = this.active;
    document.getElementById("npanel-name").textContent = o ? o.name : "—";
    for (const input of this.npanel.querySelectorAll("input")) {
      if (!o) { input.value = ""; input.disabled = true; continue; }
      input.disabled = false;
      if (document.activeElement === input) continue;
      const v = o[input.dataset.prop][input.dataset.axis];
      input.value = input.dataset.prop === "rotation" ? (v / DEG).toFixed(1) : v.toFixed(3);
    }
  }

  renderTasks() {
    this.taskList.innerHTML = "";
    TASKS.forEach((t, i) => {
      const li = document.createElement("li");
      li.textContent = t.text;
      if (this.taskDone[i]) li.className = "done";
      else if (i === this.taskIndex) li.className = "current";
      this.taskList.appendChild(li);
    });
    this.tasksDone.hidden = this.taskIndex < TASKS.length;
  }

  checkTasks() {
    const ctx = { objects: this.objects, selected: this.selected, viewName: this.view.name, ortho: this.ortho, ...this.state };
    let changed = false;
    while (this.taskIndex < TASKS.length && TASKS[this.taskIndex].check(ctx)) {
      this.taskDone[this.taskIndex] = true;
      this.taskIndex++;
      changed = true;
    }
    if (changed) this.renderTasks();
  }

  // ---------------------------------------------------------------- 入力
  bind() {
    const root = this.root;
    this.keys = { shift: false, ctrl: false };
    root.addEventListener("contextmenu", e => e.preventDefault());

    root.addEventListener("pointermove", e => {
      const n = this.ndc(e);
      this.mouse.x = n.x; this.mouse.y = n.y; this.mouse.cx = e.clientX; this.mouse.cy = e.clientY;
      this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey;
      if (this.op) { this.applyOp(); return; }
      if (this.drag) this.onDrag(e);
    });

    root.addEventListener("pointerdown", e => {
      if (e.target.closest("input, button")) return; // サイドバーやメニューの操作
      root.focus();
      this.closeMenu();
      if (this.op) {
        if (e.button === 0) this.confirmOp();
        else if (e.button === 2) this.cancelOp();
        return;
      }
      if (e.button === 1 || (e.button === 0 && e.altKey)) {
        this.drag = { kind: e.shiftKey ? "pan" : e.ctrlKey ? "zoom" : "orbit", last: this.pixel(e) };
        root.setPointerCapture(e.pointerId);
        e.preventDefault();
      } else if (e.button === 0) {
        this.drag = { kind: "box", start: this.pixel(e), shift: e.shiftKey, moved: false };
        root.setPointerCapture(e.pointerId);
      }
    });

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
        }
        this.checkTasks();
      } else if (d.kind === "orbit") {
        this.view.name = "ユーザー";
        this.ortho = this.orthoLocked ?? this.ortho;
      }
      if (this.box) { this.box.remove(); this.box = null; }
    });

    root.addEventListener("wheel", e => {
      e.preventDefault();
      this.view.dist *= e.deltaY > 0 ? 1.12 : 1 / 1.12;
      this.view.dist = THREE.MathUtils.clamp(this.view.dist, 0.5, 200);
    }, { passive: false });

    root.addEventListener("keydown", e => this.onKey(e));
    root.addEventListener("keyup", e => { this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey; if (this.op) this.applyOp(); });

    for (const input of this.npanel.querySelectorAll("input")) {
      input.addEventListener("change", () => {
        const o = this.active;
        if (!o) return;
        const v = parseFloat(input.value);
        if (Number.isNaN(v)) return;
        this.pushUndo();
        o[input.dataset.prop][input.dataset.axis] = input.dataset.prop === "rotation" ? v * DEG : v;
        this.checkTasks();
      });
      input.addEventListener("keydown", e => e.stopPropagation());
    }
    document.getElementById("practice-reset").addEventListener("click", () => { this.resetScene(); root.focus(); });
  }

  onDrag(e) {
    const d = this.drag;
    const p = this.pixel(e);
    if (d.kind === "orbit") {
      const dx = p.x - d.last.x, dy = p.y - d.last.y;
      this.view.theta -= dx * 0.008;
      this.view.phi = THREE.MathUtils.clamp(this.view.phi - dy * 0.008, 0.01, Math.PI - 0.01);
      this.view.name = "ユーザー";
      // 平行投影のまま回すと Blender では透視に戻る（自動透視）
      this.ortho = false;
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
  }

  onKey(e) {
    if (e.target !== this.root) return;
    this.keys.shift = e.shiftKey; this.keys.ctrl = e.ctrlKey;
    const code = e.code;
    const emulate = this.emulateNumpad.checked;
    const numpad = code.startsWith("Numpad") ? code.slice(6) : emulate && code.startsWith("Digit") ? code.slice(5) : emulate && e.key === "." ? "Decimal" : null;

    // 操作中のキー
    if (this.op) {
      if (code === "Escape") this.cancelOp();
      else if (code === "Enter" || code === "NumpadEnter") this.confirmOp();
      else if (code === "KeyX" || code === "KeyY" || code === "KeyZ") this.setAxis(code.slice(3).toLowerCase());
      else if (/^(Digit|Numpad)[0-9]$/.test(code)) { this.op.numeric += code.slice(-1); this.applyOp(); }
      else if (e.key === "." || code === "NumpadDecimal") { if (!this.op.numeric.includes(".")) this.op.numeric += "."; this.applyOp(); }
      else if (e.key === "-" || code === "NumpadSubtract") { this.op.numeric = this.op.numeric.startsWith("-") ? this.op.numeric.slice(1) : "-" + this.op.numeric; this.applyOp(); }
      else if (code === "Backspace") { this.op.numeric = this.op.numeric.slice(0, -1); this.applyOp(); }
      else if (code === "ShiftLeft" || code === "ShiftRight" || code === "ControlLeft" || code === "ControlRight") this.applyOp();
      e.preventDefault();
      return;
    }

    // 視点
    if (numpad !== null) {
      e.preventDefault();
      const c = e.ctrlKey;
      switch (numpad) {
        case "1": this.setViewAxis(c ? "後" : "前", c ? Math.PI / 2 : -Math.PI / 2, Math.PI / 2); break;
        case "3": this.setViewAxis(c ? "左" : "右", c ? Math.PI : 0, Math.PI / 2); break;
        case "7": this.setViewAxis(c ? "下" : "上", -Math.PI / 2, c ? Math.PI - 0.001 : 0.001); break;
        case "5": this.ortho = !this.ortho; break;
        case "Decimal": this.frame(this.selected.length ? this.selected : this.objects); break;
        default: return;
      }
      this.checkTasks();
      return;
    }
    if (code === "Home") { e.preventDefault(); this.frame(this.objects); return; }

    const mod = e.ctrlKey || e.metaKey;
    if (mod && code === "KeyZ") { e.preventDefault(); e.shiftKey ? this.redo() : this.undo(); this.checkTasks(); return; }
    if (mod) return;

    switch (code) {
      case "KeyG": this.startOp("G"); break;
      case "KeyR": this.startOp("R"); break;
      case "KeyS": this.startOp("S"); break;
      case "KeyA":
        if (e.altKey) { this.select(null); this.setStatus("選択解除"); }
        else if (e.shiftKey) this.showAddMenu();
        else { this.selected = [...this.objects]; this.active = this.selected[this.selected.length - 1] ?? null; this.refreshSelection(); this.setStatus("全選択"); }
        break;
      case "KeyD":
        if (e.shiftKey) this.duplicate();
        break;
      case "KeyX":
        if (this.selected.length) this.showMenu("削除", [{ label: "削除", run: () => this.deleteSelected() }], this.mousePx());
        break;
      case "Delete": this.deleteSelected(); break;
      case "KeyN": this.npanel.hidden = !this.npanel.hidden; this.updateNPanel(); break;
      case "Tab": this.setStatus("編集モードは次の段階で対応予定です"); break;
      default: return;
    }
    e.preventDefault();
    this.checkTasks();
  }

  showAddMenu() {
    this.showMenu("追加 > メッシュ", PRIMITIVES.map(p => ({ label: p.label, run: () => {
      this.pushUndo();
      const m = this.addObject(p.key);
      this.select(m);
      this.setStatus(`${p.label}を追加`);
      this.checkTasks();
    } })), this.mousePx());
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
    this.setStatus("複製。マウスで位置を決めてクリック");
    this.startOp("G");
    this.undoStack.pop(); // 複製と移動を 1 回の取り消しにまとめる
  }

  deleteSelected() {
    if (this.selected.length === 0) return;
    this.pushUndo();
    for (const o of [...this.selected]) this.removeObject(o);
    this.refreshSelection();
    this.setStatus("削除");
    this.checkTasks();
  }
}

const root = document.getElementById("practice-viewport");
if (root) {
  try {
    new Practice(root);
  } catch (err) {
    // WebGL が使えない環境（古いブラウザ・GPU 無効など）
    const msg = document.createElement("div");
    msg.className = "practice-hud";
    msg.style.top = "50%";
    msg.textContent = "3D ビューを表示できません。WebGL が有効なブラウザで開いてください。";
    root.appendChild(msg);
    console.error(err);
  }
}
