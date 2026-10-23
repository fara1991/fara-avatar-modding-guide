// Phase 3b S2 の画像比較: 現行の three.js 版(/ref.html)と、C# + 薄い WebGL 層のレンダーラボ(/lab/)に、同じシーン・同じ操作をして、画素を比べる。  node gl-compare.mjs
//   ORIGIN=http://127.0.0.1:8790(gl-serve.mjs)  OUT=<画像の出力先。既定 ./gl-compare-out>  CHROME_PATH=…  WIDTH=960(ビューの幅。390 でスマホ幅)  ONLY=<シーン名の一部>
// 各シーンで: ① 面の明るさ(指定した面の中心 11×11 ピクセルの平均。three.js と ±3 以内が条件)② 画素の差(どれかのチャンネルが 8 を超える画素の割合・最大の差)。
// 比べるのは「見た目」だけ。ジオメトリ・色・行列の一致は Render.Tests(Node で本物の three.js のシーンと比較)が確かめている。
// 3 枚組(three.js | C# | 差)を OUT に書く。WebGL はソフトウェア描画(SwiftShader)。面の位置は、各シーンの faces に決まった座標で書いてある(幅 960 のとき)。
import { mkdirSync } from "node:fs";
import { PNG } from "pngjs";
import { launch } from "./lib.mjs";

const ORIGIN = process.env.ORIGIN || "http://127.0.0.1:8790";
const OUT = process.env.OUT || "./gl-compare-out";
const WIDTH = Number(process.env.WIDTH || 960);
const HEIGHT = Math.round((WIDTH * 9) / 16);
const FACE_TOLERANCE = Number(process.env.FACE_TOLERANCE || 3);
mkdirSync(OUT, { recursive: true });

const q = (o) => encodeURIComponent(JSON.stringify(o));
const SCENES = [
  // 既定の立方体(上・左・右の 3 面の明るさが、設計メモ §3 の判断基準 (a))
  { name: "cube-default", scene: [{ kind: "cube" }], faces: { top: [480, 222], left: [440, 300], right: [520, 300] } },
  // 3 つのオブジェクト(球・円柱・円錐)と、選択の輪郭(全選択)・3D カーソル
  { name: "three-objects-selected", scene: [{ kind: "sphere", p: [-3, 0, 0] }, { kind: "cylinder", p: [0, 1, 0] }, { kind: "cone", p: [3, 0, 0], r: [0, 0, 30] }], steps: [["key", "KeyA"]], view: { dist: 14 } },
  // 面ごとの陰影が見えるように、回した立方体(上 4 面)と平面
  { name: "rotated-cube-plane", scene: [{ kind: "cube", r: [20, 35, 15] }, { kind: "plane", p: [0, 0, -1.2], s: [3, 3, 1] }] },
  // 編集モード(頂点選択): 点・線・全選択
  { name: "edit-vert-all", scene: [{ kind: "cube" }], steps: [["click", 480, 270], ["key", "Tab"], ["key", "KeyA"]] },
  // 編集モード(面選択): 面を 1 つ選ぶ(半透明の面)
  { name: "edit-face-one", scene: [{ kind: "cube" }], steps: [["click", 480, 270], ["key", "Tab"], ["key", "Digit3"], ["click", 480, 228]] },
  // 透過表示(X 線): 編集モードで Alt+Z
  { name: "edit-xray", scene: [{ kind: "sphere" }], view: { dist: 5 }, steps: [["click", 480, 270], ["key", "Tab"], ["key", "Digit3"], ["click", 520, 300], ["key", "KeyZ", { alt: true }]] },
  // 透過表示でない編集モード(球。近くから)
  { name: "edit-face-sphere", scene: [{ kind: "sphere" }], view: { dist: 5 }, steps: [["click", 480, 270], ["key", "Tab"], ["key", "Digit3"], ["click", 520, 300]] },
  // ミラー(X 方向): 立方体を右にずらして、鏡像を出す
  { name: "mirror", scene: [{ kind: "cube", p: [2.5, 0, 0], s: [1, 0.6, 0.6] }], steps: [["click", 600, 285], ["tool", "mirror"]] },
  // 軸固定の移動(G → X): 軸線
  { name: "move-x-axis-line", scene: [{ kind: "cube" }], steps: [["click", 480, 270], ["move", 520, 300], ["key", "KeyG"], ["key", "KeyX"], ["move", 600, 300]] },
  // 平行投影(テンキー 1: 前)
  { name: "ortho-front", scene: [{ kind: "cube" }, { kind: "sphere", p: [3, 0, 0] }], steps: [["click", 480, 270], ["key", "Numpad1"]] },
  // ループカットのプレビュー(Ctrl+R。黄色の線)
  { name: "loopcut-preview", scene: [{ kind: "cube" }], steps: [["click", 480, 270], ["key", "Tab"], ["key", "KeyR", { ctrl: true }], ["move", 480, 250]] },
];

const only = process.env.ONLY;
const browser = await launch();
let failed = 0;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function open(url, label) {
  const ctx = await browser.newContext({ viewport: { width: Math.max(WIDTH, 390), height: HEIGHT + 40 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.problems = [];
  page.on("console", (m) => { if (["error", "warning"].includes(m.type())) page.problems.push(`${label} ${m.type()}: ${m.text().slice(0, 200)}`); });
  page.on("pageerror", (e) => page.problems.push(`${label} pageerror: ${e.message.slice(0, 200)}`));
  await page.goto(url);
  await page.waitForSelector("#viewport canvas", { timeout: 90000 });
  await page.waitForFunction(() => !window.__glDiag || JSON.parse(window.__glDiag()).drawn > 2, null, { timeout: 90000 });
  return { ctx, page };
}

const xy = (p, s) => [Math.round((p * WIDTH) / 960), Math.round((s * WIDTH) / 960)];

async function act(page, steps, isLab) {
  const box = await page.locator("#viewport").boundingBox();
  const at = (x, y) => [box.x + (x * WIDTH) / 960, box.y + (y * WIDTH) / 960];
  for (const [kind, a, b] of steps ?? []) {
    if (kind === "click") { const [x, y] = at(a, b); await page.mouse.move(x, y); await page.mouse.down(); await page.mouse.up(); }
    else if (kind === "move") { const [x, y] = at(a, b); await page.mouse.move(x, y, { steps: 3 }); }
    else if (kind === "key") {
      const m = b ?? {};
      if (m.ctrl) await page.keyboard.down("Control");
      if (m.alt) await page.keyboard.down("Alt");
      await page.keyboard.press(a);
      if (m.alt) await page.keyboard.up("Alt");
      if (m.ctrl) await page.keyboard.up("Control");
    } else if (kind === "tool") {
      await page.evaluate(([name, lab]) => (lab ? window.__labControl("tool:" + name, 0) : window.__vp.setMirror()), [a, isLab]);
    }
    await sleep(60);
  }
  await sleep(500);
}

function patchMean(png, cx, cy, r = 5) {
  const sum = [0, 0, 0]; let n = 0;
  for (let y = cy - r; y <= cy + r; y++) for (let x = cx - r; x <= cx + r; x++) {
    const i = (y * png.width + x) * 4;
    for (let c = 0; c < 3; c++) sum[c] += png.data[i + c];
    n++;
  }
  return sum.map((s) => s / n);
}

for (const s of SCENES) {
  if (only && !s.name.includes(only)) continue;
  const query = `?scene=${q(s.scene)}${s.view ? `&view=${q(s.view)}` : ""}`;
  const a = await open(`${ORIGIN}/ref.html${query}`, "three");
  const b = await open(`${ORIGIN}/lab/${query}`, "lab");
  try {
    await act(a.page, s.steps, false);
    await act(b.page, s.steps, true);
    const clip = { x: 0, y: 0, width: WIDTH, height: HEIGHT };
    const ref = PNG.sync.read(await a.page.screenshot({ clip }));
    const got = PNG.sync.read(await b.page.screenshot({ clip }));
    const diff = new PNG({ width: ref.width, height: ref.height });
    let over = 0, max = 0, sum = 0;
    for (let i = 0; i < ref.data.length; i += 4) {
      let d = 0;
      for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(ref.data[i + c] - got.data[i + c]));
      max = Math.max(max, d); sum += d;
      if (d > 8) over++;
      diff.data[i] = Math.min(255, d * 8); diff.data[i + 1] = Math.min(255, d * 8); diff.data[i + 2] = Math.min(255, d * 8); diff.data[i + 3] = 255;
    }
    const total = ref.width * ref.height;
    const triple = new PNG({ width: ref.width * 3, height: ref.height });
    [ref, got, diff].forEach((img, k) => PNG.bitblt(img, triple, 0, 0, img.width, img.height, k * img.width, 0));
    const file = `${OUT}/${s.name}-${WIDTH}.png`;
    (await import("node:fs")).writeFileSync(file, PNG.sync.write(triple));
    let line = `${s.name.padEnd(24)} 差>8: ${((over / total) * 100).toFixed(3)}%  最大 ${max}  平均 ${(sum / total).toFixed(3)}`;
    let ok = true;
    for (const [face, [fx, fy]] of Object.entries(s.faces ?? {})) {
      const [x, y] = xy(fx, fy);
      const m1 = patchMean(ref, x, y), m2 = patchMean(got, x, y);
      const dd = Math.max(...m1.map((v, c) => Math.abs(v - m2[c])));
      line += `\n    面 ${face}: three=${m1.map((v) => v.toFixed(1)).join(",")}  C#=${m2.map((v) => v.toFixed(1)).join(",")}  差 ${dd.toFixed(2)}`;
      if (dd > FACE_TOLERANCE) ok = false;
    }
    const problems = [...a.page.problems, ...b.page.problems].filter((p) => !/404/.test(p));
    if (problems.length) { line += "\n    " + problems.join("\n    "); ok = false; }
    console.log(`${ok ? "ok  " : "NG  "}${line}`);
    if (!ok) failed++;
  } finally {
    await a.ctx.close();
    await b.ctx.close();
  }
}
await browser.close();
process.exit(failed ? 1 : 0);
