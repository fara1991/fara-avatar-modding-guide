// 操作の練習（/practice）。viewport.js の 3D ビューを 1 つ置き、お題を順にチェックする。
// 記事内の手順つきチュートリアルは tutorial.js / scenarios.js。
import { Viewport, near, nearAngle } from "./viewport.js";

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

class Practice {
  constructor(root) {
    this.taskList = document.getElementById("practice-tasks");
    this.tasksDone = document.getElementById("practice-tasks-done");
    const emulate = document.getElementById("practice-emulate-numpad");
    this.vp = new Viewport(root, { emulateNumpad: () => emulate.checked });
    this.vp.on("*", e => {
      if (e.type === "add" && e.kind === "sphere") this.state.sphereAdded = true;
      if (e.type === "undo") this.state.undoUsed = true;
      this.checkTasks();
    });
    document.getElementById("practice-reset").addEventListener("click", () => { this.reset(); root.focus(); });
    this.reset();
  }

  reset() {
    this.vp.resetScene();
    this.state = { undoUsed: false, sphereAdded: false };
    this.taskIndex = 0;
    this.taskDone = TASKS.map(() => false);
    this.renderTasks();
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
    const vp = this.vp;
    const ctx = { objects: vp.objects, selected: vp.selected, viewName: vp.view.name, ortho: vp.ortho, ...this.state };
    let changed = false;
    while (this.taskIndex < TASKS.length && TASKS[this.taskIndex].check(ctx)) {
      this.taskDone[this.taskIndex] = true;
      this.taskIndex++;
      changed = true;
    }
    if (changed) this.renderTasks();
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
