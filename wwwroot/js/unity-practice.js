// Unity の練習（/practice/unity）。unity.js の画面を 1 つ置き、お題を順にチェックする。
// 手順つきの案内は記事内チュートリアル（scenarios.js の "ma-outfit"）。
import { UnityEditor } from "./unity.js";

// お題。check(ed, state) で達成なら true
const TASKS = [
  { text: "Project の Hoodie を、Hierarchy の Avatar の上にドラッグして入れる", check: ed => !!ed.findByName("Avatar/Hoodie") },
  { text: "Hoodie を右クリック > Modular Avatar ▸ Setup Outfit", check: ed => !!ed.findByName("Avatar/Hoodie/Armature")?.components.some(c => c.type === "MAMergeArmature") },
  { text: "Shirt_Default を無効にして、Tag を EditorOnly にする", check: ed => { const n = ed.findByName("Avatar/Shirt_Default"); return !n || (!n.active && n.tag === "EditorOnly"); } },
  { text: "Hoodie_Mesh の Materials に Hoodie_lilToon（または Hoodie_Red）を設定する", check: ed => /lilToon|Red/.test(ed.findByName("Hoodie/Hoodie_Mesh")?.components.find(c => c.type === "SkinnedMeshRenderer")?.materials[0] ?? "") },
  { text: "Hoodie_Mesh に Add Component で MA Blendshape Sync を追加する", check: ed => !!ed.findByName("Hoodie/Hoodie_Mesh")?.components.some(c => c.type === "MABlendshapeSync") },
  { text: "VRChat SDK > Show Control Panel でランクを確認する", check: (ed, s) => s.sdk },
  { text: "Build & Test で、衣装がついてくることを確かめる", check: (ed, s) => s.builtMerged },
];

class UnityPractice {
  constructor(root) {
    this.list = document.getElementById("unity-tasks");
    this.done = document.getElementById("unity-tasks-done");
    this.ed = new UnityEditor(root, { scene: { instances: ["Avatar"] } });
    this.ed.on("*", e => {
      if (e.type === "sdk" && e.open) this.state.sdk = true;
      if (e.type === "build" && e.kind === "test" && e.merged) this.state.builtMerged = true;
      this.check();
    });
    document.getElementById("unity-reset").addEventListener("click", () => { this.reset(); root.focus(); });
    if (new URLSearchParams(location.search).get("debug") === "1") window.__unity = this.ed; // 確認用
    this.reset();
  }

  reset() {
    this.ed.resetScene();
    this.state = { sdk: false, builtMerged: false };
    this.index = 0;
    this.render();
  }

  check() {
    let changed = false;
    while (this.index < TASKS.length && TASKS[this.index].check(this.ed, this.state)) { this.index++; changed = true; }
    if (changed) this.render();
  }

  render() {
    this.list.innerHTML = "";
    TASKS.forEach((t, i) => {
      const li = document.createElement("li");
      li.textContent = t.text;
      if (i < this.index) li.className = "done";
      else if (i === this.index) li.className = "current";
      this.list.appendChild(li);
    });
    this.done.hidden = this.index < TASKS.length;
  }
}

const root = document.getElementById("unity-practice");
if (root) {
  try {
    new UnityPractice(root);
  } catch (err) {
    root.textContent = "画面を表示できません。WebGL が有効なブラウザで開いてください。";
    console.error(err);
  }
}
