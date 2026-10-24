// /practice の Three.js 版と C# 版(?engine=cs)を、同じ操作で並べて、ビューポート(3D + HUD)の画素を比べる(Phase 3b S3)。  node practice-compare.mjs
//   NEW=http://127.0.0.1:8787(wrangler dev など、サイトの配信)  OUT=<画像の出力先>  WIDTH=1280  MAX_OVER_PCT=0.001  MAX_DIFF=8
// 比べるのは、ビューポートの要素のスクリーンショット(3D の描画 + HUD の文字・ボタン)。3 枚組(three.js | C# | 差)を書く。画素の差(>8 の割合・最大)に閾値を付けて合否に含める。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const OUT = process.env.OUT || "./practice-compare-out";
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT || 0.001);
const MAX_DIFF = Number(process.env.MAX_DIFF || 8);
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const SCENES = [
  { name: "default", steps: [] },
  { name: "select-cube", steps: [["click", 0, -12]] },
  { name: "move-z", steps: [["click", 0, -12], ["key", "g"], ["key", "z"], ["key", "2"], ["key", "Enter"]] },
  { name: "add-sphere-menu", steps: [["key", "Shift+A"]] },
  { name: "edit-face", steps: [["click", 0, -12], ["key", "Tab"], ["key", "3"], ["click", 0, -40]] },
  { name: "edit-xray-stats", steps: [["click", 0, -12], ["key", "Tab"], ["tool", "xray"], ["tool", "stats"]] },
  { name: "npanel", steps: [["click", 0, -12], ["key", "n"]] },
  { name: "ortho-front", steps: [["click", 0, -12], ["key", "Numpad1"]] },
  { name: "box-select-drag", steps: [["click", 0, -12], ["key", "Tab"], ["drag", -120, -90, 120, 90]] },
];

async function open(browser, url) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.problems = [];
  page.on("console", (m) => { if (["error", "warning"].includes(m.type())) page.problems.push(m.text().slice(0, 200)); });
  page.on("pageerror", (e) => page.problems.push("pageerror " + e.message.slice(0, 200)));
  await page.goto(NEW + url);
  await page.waitForSelector(".practice-viewport canvas", { timeout: 90000 });
  // ビューの縁(半透明の枠・影)の下に、ページの背景(#site-scene。動く)が透けて見えるので、隠す(画素の差が、描画ではなく背景の動きになる)
  await page.addStyleTag({ content: "#site-scene{display:none !important}" });
  await sleep(800);
  return { ctx, page };
}

async function run(page, steps) {
  const box = await page.locator("#practice-viewport").boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
  for (const [kind, a, b, c, d] of steps) {
    if (kind === "click") { await page.mouse.click(cx + a, cy + b); }
    else if (kind === "key") await page.keyboard.press(a);
    else if (kind === "tool") await page.locator(`[data-tool="${a}"]`).click();
    else if (kind === "drag") { await page.mouse.move(cx + a, cy + b); await page.mouse.down(); await page.mouse.move(cx + c, cy + d, { steps: 6 }); }
    await sleep(120);
  }
  await sleep(500);
  // マウスは同じ場所に(HUD の状態に影響しない位置)
  await page.mouse.move(box.x + box.width - 5, box.y + box.height - 5);
  await sleep(200);
}

const browser = await launch();
let failed = 0;
const only = process.env.ONLY;
for (const s of SCENES) {
  if (only && !s.name.includes(only)) continue;
  const a = await open(browser, "/practice");
  const b = await open(browser, "/practice?engine=cs");
  try {
    await run(a.page, s.steps);
    await run(b.page, s.steps);
    const shotA = PNG.sync.read(await a.page.locator("#practice-viewport").screenshot());
    const shotB = PNG.sync.read(await b.page.locator("#practice-viewport").screenshot());
    const diff = new PNG({ width: shotA.width, height: shotA.height });
    let over = 0, max = 0;
    if (shotA.width !== shotB.width || shotA.height !== shotB.height) { console.log(`NG  ${s.name}: 大きさが違う ${shotA.width}x${shotA.height} / ${shotB.width}x${shotB.height}`); failed++; continue; }
    for (let i = 0; i < shotA.data.length; i += 4) {
      let d = 0;
      for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(shotA.data[i + c] - shotB.data[i + c]));
      max = Math.max(max, d);
      if (d > 8) over++;
      diff.data[i] = diff.data[i + 1] = diff.data[i + 2] = Math.min(255, d * 8); diff.data[i + 3] = 255;
    }
    const pct = (over / (shotA.width * shotA.height)) * 100;
    const triple = new PNG({ width: shotA.width * 3, height: shotA.height });
    [shotA, shotB, diff].forEach((img, k) => PNG.bitblt(img, triple, 0, 0, img.width, img.height, k * img.width, 0));
    writeFileSync(`${OUT}/${s.name}.png`, PNG.sync.write(triple));
    const problems = [...a.page.problems, ...b.page.problems];
    const ok = pct <= MAX_OVER_PCT && max <= MAX_DIFF && problems.length === 0;
    console.log(`${ok ? "ok  " : "NG  "}${s.name.padEnd(18)} 差>8: ${pct.toFixed(3)}%  最大 ${max}${problems.length ? "  " + problems.join(" | ") : ""}`);
    if (!ok) failed++;
  } finally {
    await a.ctx.close();
    await b.ctx.close();
  }
}
await browser.close();
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
