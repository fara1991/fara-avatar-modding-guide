// /practice/unity の Three.js 版（基準）と C# 版（?engine=cs）を、同じ本物のマウス・キーで操作して、毎回、画素・DOM の骨格・文字を比べる（Phase 3b S5・S7）。  node unity-compare.mjs
//   NEW=http://127.0.0.1:8787  OUT=<画像の出力先>  MAX_OVER_PCT=0  MAX_DIFF=8  ONLY=<操作の名前の一部>
//   基準: GOLDEN_MODE=capture で Three.js 版（?engine=js）の各場面（画像・DOM・お題・文字・状態）を tests/visual/golden/unity-compare に保存する（golden.mjs）。既定の compare は、保存した基準と C# 版を比べる（three.js が無くても動く）
// 操作: Hierarchy の ▶・行のクリック・Scene のクリック・F・Alt+左ドラッグ・ホイールボタン・右ドラッグ・ホイール・ギズモのドラッグ（移動・回転・拡大縮小）・Project からのドラッグ（Hoodie を Avatar へ）・
//   右クリックのメニュー（Setup Outfit）・Inspector（有効・Tag・マテリアルのドラッグ・Position の入力・Add Component の検索）・VRChat SDK のメニュー・Control Panel・Build & Test・Ctrl+Z。
// 比べるもの: エディター全体のスクリーンショット（背景の 3D を隠し、CSS のアニメーションを止める）・DOM の骨格・お題の一覧・ステータスの文字。
// 腕を振るポーズ・案内は時計で決まるので、performance.now を止める（同じ位相になる）。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch, newPage } from "./lib.mjs";
import { skeleton, pixelDiff, sleep, realProblems } from "./tutorial-lib.mjs";
import { drag, handlePoint, dragTo, stateOf, stateDiff } from "./unity-lib.mjs";
import { GOLDEN_MODE, loadGolden, saveGolden } from "./golden.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const OUT = process.env.OUT || "./unity-compare-out";
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT || 0);
const MAX_DIFF = Number(process.env.MAX_DIFF || 8);
const GROUP = "unity-compare";
const capture = GOLDEN_MODE === "capture";
mkdirSync(OUT, { recursive: true });

async function open(browser, cs) {
  const page = await newPage(browser, { width: 1280, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.context().addInitScript(() => { performance.now = () => 1000; });
  await page.addInitScript(() => { const st = document.createElement("style"); st.textContent = "#site-scene{display:none !important}"; document.addEventListener("DOMContentLoaded", () => document.head.appendChild(st)); });
  await page.goto(`${NEW}/practice/unity?debug=1&engine=${cs ? "cs" : "js"}`, { waitUntil: "load" });
  await page.waitForSelector("#unity-practice canvas", { timeout: 90000 });
  await page.locator("#unity-practice").scrollIntoViewIfNeeded();
  await sleep(1500);
  // 確かめ用: Three.js 版のジオメトリを、インデックス無しにする(C# 版と同じ描き方)
  if (!cs && process.env.NONINDEXED === "1") {
    await page.evaluate(() => {
      const ed = window.__unity;
      const orig = ed.rebuild.bind(ed);
      const fix = () => { for (const n of ed.nodes.values()) for (const part of n.parts ?? []) part.geometry = part.geometry.toNonIndexed(); };
      ed.rebuild = () => { orig(); fix(); };
      fix();
    });
  }
  return page;
}

const ROW = (name) => `#unity-practice .ue-row[data-node-name="${name}"]`;
const scene = async (p) => p.locator("#unity-practice .ue-scene-view").boundingBox();
const at = async (p, x, y) => { const b = await scene(p); return [b.x + x, b.y + y]; };

const ACTIONS = [
  ["expand-avatar", async (p) => p.click(`${ROW("Avatar")} .ue-fold`)],
  ["select-body", async (p) => p.click(ROW("Body"))],
  ["scene-click-empty", async (p) => { const [x, y] = await at(p, 40, 300); await p.mouse.click(x, y); }],
  ["key-f", async (p) => p.keyboard.press("f")],
  ["alt-orbit", async (p) => { const [x, y] = await at(p, 200, 200); await p.keyboard.down("Alt"); await drag(p, [x, y], [x + 40, y - 20]); await p.keyboard.up("Alt"); }],
  ["wheel", async (p) => { const [x, y] = await at(p, 200, 200); await p.mouse.move(x, y); await p.mouse.wheel(0, 200); }],
  ["middle-pan", async (p) => { const [x, y] = await at(p, 200, 200); await drag(p, [x, y], [x + 30, y + 20], 4, "middle"); }],
  ["right-look", async (p) => { const [x, y] = await at(p, 200, 200); await drag(p, [x, y], [x - 30, y + 10], 4, "right"); }],
  ["select-avatar", async (p) => p.click(ROW("Avatar"))],
  ["key-f-avatar", async (p) => { const [x, y] = await at(p, 40, 300); await p.mouse.click(x, y); await p.click(ROW("Avatar")); await p.keyboard.press("f"); }],
  ["tool-move-gizmo", async (p, ctx) => { await p.keyboard.press("w"); await sleep(120); const h = await ctx.handle("move", 0); const [x, y] = await at(p, h.x, h.y); await drag(p, [x, y], [x + 25, y - 6]); }],
  ["edit-position-x", async (p) => { await p.fill('#unity-practice [data-field="p"][data-axis="0"]', "0.25"); await p.keyboard.press("Enter"); await p.locator("#unity-practice .ue-name").click(); }],
  ["undo", async (p) => { const [x, y] = await at(p, 40, 300); await p.mouse.click(x, y); await p.keyboard.press("Control+z"); }],
  ["tool-rotate-gizmo", async (p, ctx) => { await p.click(ROW("Avatar")); await p.keyboard.press("e"); await sleep(120); const h = await ctx.handle("rotate", 1); const [x, y] = await at(p, h.x, h.y); await drag(p, [x, y], [x + 30, y - 10]); }],
  ["tool-scale-gizmo", async (p, ctx) => { await p.keyboard.press("r"); await sleep(120); const h = await ctx.handle("scale", 0); const [x, y] = await at(p, h.x, h.y); await drag(p, [x, y], [x + 25, y]); }],
  ["tool-hand-pan", async (p) => { await p.keyboard.press("q"); const [x, y] = await at(p, 200, 200); await drag(p, [x, y], [x + 20, y + 10]); }],
  ["undo-all", async (p) => { for (let i = 0; i < 6; i++) await p.keyboard.press("Control+z"); await p.keyboard.press("w"); }],
  ["drag-hoodie-onto-avatar", async (p) => { await dragTo(p, '#unity-practice .ue-asset[data-asset="Hoodie"]', ROW("Avatar"), true); }],
  ["context-hoodie", async (p) => { await p.click(ROW("Hoodie"), { button: "right" }); }],
  ["setup-outfit", async (p) => { await p.click('#unity-practice .ue-popup [data-ctx="setup-outfit"]'); }],
  ["select-shirt", async (p) => p.click(ROW("Shirt_Default"))],
  ["inactive-shirt", async (p) => p.uncheck("#unity-practice .ue-active")],
  ["tag-editoronly", async (p) => p.selectOption("#unity-practice .ue-tag", "EditorOnly")],
  ["expand-hoodie", async (p) => p.click(`${ROW("Hoodie")} .ue-fold`)],
  ["select-hoodie-mesh", async (p) => p.click(ROW("Hoodie_Mesh"))],
  ["drag-material", async (p) => { await dragTo(p, '#unity-practice .ue-asset[data-asset="Hoodie_lilToon"]', '#unity-practice [data-slot="0"]'); }],
  ["add-component-open", async (p) => p.click("#unity-practice .ue-addcomp")],
  ["add-component-search", async (p) => p.fill("#unity-practice .ue-addsearch", "blend")],
  ["add-component-pick", async (p) => p.click('#unity-practice [data-add="MABlendshapeSync"]')],
  ["menu-sdk", async (p) => p.click('#unity-practice [data-menu="VRChat SDK"]')],
  ["menu-sdk-panel", async (p) => p.click('#unity-practice [data-menuitem="sdk-panel"]')],
  // 押した直後の「ビルドしています…」は、押してから 0.9 秒で変わるので、画素は比べない(文字だけ。両方がビルド中になるのを待つ)
  ["build-test", async (p) => { await p.click('#unity-practice [data-sdk="test"]'); await p.waitForFunction(() => document.querySelector(".ue-preview .ue-progress") !== null, null, { timeout: 3000 }); }, { noSnapshot: true }],
  ["build-done", async (p) => p.waitForFunction(() => { const pv = document.querySelector(".ue-preview"); return !pv.hidden && !pv.querySelector(".ue-progress"); }, null, { timeout: 8000 })],
  ["preview-gone", async (p) => p.waitForFunction(() => document.querySelector(".ue-preview").hidden, null, { timeout: 10000 })],
  ["scene-pick-hoodie", async (p) => { const [x, y] = await at(p, 175, 190); await p.mouse.click(x, y); }],
  ["delete-key", async (p) => { await p.keyboard.press("Delete"); }],
  ["undo-delete", async (p) => { await p.keyboard.press("Control+z"); }],
  // Hierarchy の行を Scene へドラッグして離す(何も起きない。ドラッグの影・ドロップ先の枠が残らない)
  ["drag-row-to-scene", async (p) => { await dragTo(p, ROW("Hoodie"), "#unity-practice .ue-scene-view"); }],
  // 親に EditorOnly を付けたときの SDK の集計(子も除かれる。UI から起こせる操作。レビュー L1)
  ["select-hoodie-row", async (p) => p.click(ROW("Hoodie"))],
  ["tag-parent-editoronly", async (p) => p.selectOption("#unity-practice .ue-tag", "EditorOnly")],
  ["menu-sdk-again", async (p) => p.click('#unity-practice [data-menu="VRChat SDK"]')],
  ["sdk-panel-parent-editoronly", async (p) => p.click('#unity-practice [data-menuitem="sdk-panel"]')],
  ["sdk-close", async (p) => p.click('#unity-practice [data-sdk="close"]')],
];

async function snapshotOf(p, cs) {
  const box = await p.locator("#unity-practice").boundingBox();
  const png = PNG.sync.read(await p.screenshot({ clip: { x: box.x, y: box.y, width: box.width, height: box.height } }));
  const sk = await p.locator("#unity-practice").evaluate(skeleton);
  const tasks = await p.evaluate(() => [...document.querySelectorAll("#unity-tasks li")].map((li) => `${li.className}:${li.textContent}`).concat([document.querySelector("#unity-tasks-done")?.hidden ? "tasks-not-done" : "tasks-done"]));
  const text = await p.locator("#unity-practice").evaluate((el) => ({ status: el.querySelector(".ue-status")?.textContent, info: el.querySelector(".ue-scene-info")?.textContent, preview: el.querySelector(".ue-preview")?.hidden ? null : el.querySelector(".ue-preview")?.innerText, active: document.activeElement === el ? "root" : document.activeElement?.className }));
  return { png, sk, tasks, text, state: await stateOf(p, cs) };
}

/** 基準（three.js の版）の場面を保存する / 保存した基準を読む（JSON と PNG）。PNG の元データは保存するので、比べるときは PNG から読み直す */
const snapshotFromGolden = (key) => {
  const g = JSON.parse(loadGolden(GROUP, key, "json"));
  return { ...g, png: PNG.sync.read(loadGolden(GROUP, key, "png")) };
};

const browser = await launch();
let failed = 0, compared = 0;
const js = capture ? await open(browser, false) : null;   // 基準（three.js の版）。比べるだけのときは開かない
const cs = await open(browser, true);
try {
  // ギズモの矢印・輪の画面上の位置は、JS 版の状態から求める。基準の作成では求めて保存し、比べるときは保存したものを読む（C# 版には window.__unity が無い）
  const ctx = {
    handle: async (tool, i) => {
      const key = `handle-${tool}-${i}-${curName}`;
      if (js) { const h = await handlePoint(js, i); saveGolden(GROUP, key, "json", JSON.stringify(h)); return h; }
      return JSON.parse(loadGolden(GROUP, key, "json"));
    },
  };
  let curName = "";
  const only = process.env.ONLY;
  const run = async (name, fn, opts = {}) => {
    curName = name;
    if (js) await fn(js, ctx);
    await fn(cs, ctx);
    if (opts.noSnapshot) { compared++; return; }
    if (js) await js.mouse.move(2, 2);
    await cs.mouse.move(2, 2);
    await sleep(name === "build-test" ? 200 : 400);
    // 両方の描画が終わるのを待つ（ドラッグの影・ドロップ先の枠が消え、2 フレーム進む。C# 版は Razor の描き直しがあとから来る）
    for (const p of [js, cs].filter(Boolean)) await p.waitForFunction(() => !document.querySelector("#unity-practice .ue-ghost, #unity-practice .ue-drop"), null, { timeout: 3000 }).catch(() => {});
    for (const p of [js, cs].filter(Boolean)) await p.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(r, 50)))));
    const b = await snapshotOf(cs, true);
    let a;
    if (js) {
      a = await snapshotOf(js, false);
      saveGolden(GROUP, name, "png", PNG.sync.write(a.png));
      saveGolden(GROUP, name, "json", JSON.stringify({ sk: a.sk, tasks: a.tasks, text: a.text, state: a.state }, null, 1));
    } else a = snapshotFromGolden(name);
    let ok = true, note = "";
    if (a.png.width !== b.png.width || a.png.height !== b.png.height) { ok = false; note = "大きさが違う"; }
    else {
      const { diff, over, max } = pixelDiff(a.png, b.png);
      note = `差>8: ${over.toFixed(3)}%  最大 ${max}`;
      if (over > MAX_OVER_PCT || max > MAX_DIFF) {
        ok = false;
        const triple = new PNG({ width: a.png.width * 3, height: a.png.height });
        [a.png, b.png, diff].forEach((img, i) => PNG.bitblt(img, triple, 0, 0, img.width, img.height, i * img.width, 0));
        writeFileSync(`${OUT}/${String(compared).padStart(2, "0")}-${name}.png`, PNG.sync.write(triple));
      }
    }
    const skDiff = [];
    for (let i = 0; i < Math.max(a.sk.length, b.sk.length); i++) if (a.sk[i] !== b.sk[i]) skDiff.push(`  基準: ${a.sk[i]}\n  C#: ${b.sk[i]}`);
    if (skDiff.length) { ok = false; note += `  DOM の違い ${skDiff.length} 行\n${skDiff.slice(0, 4).join("\n")}`; }
    const sd = stateDiff(a.state, b.state);
    if (sd.length) { ok = false; note += "  状態の違い " + sd.join(" | "); }
    if (JSON.stringify(a.tasks) !== JSON.stringify(b.tasks)) { ok = false; note += `  お題の違い 基準=${JSON.stringify(a.tasks)} C#=${JSON.stringify(b.tasks)}`; }
    for (const k of Object.keys(a.text)) if (JSON.stringify(a.text[k]) !== JSON.stringify(b.text[k])) { ok = false; note += `  文字の違い ${k}[基準=${JSON.stringify(a.text[k])} / C#=${JSON.stringify(b.text[k])}]`; }
    compared++;
    if (!ok) { failed++; console.log(`NG  ${name}: ${note}`); } else if (process.env.VERBOSE) console.log(`ok  ${name}: ${note}`);
  };
  // 最初の画面
  await run("initial", async () => { });
  for (const [name, fn, opts] of ACTIONS) {
    if (only && !name.includes(only)) continue;
    await run(name, fn, opts);
  }
  const problems = [...(js ? realProblems(js) : []), ...realProblems(cs)];
  if (problems.length) { failed++; console.log(`NG  ${problems.join(" | ")}`); }
} finally {
  if (js) await js.context().close();
  await cs.context().close();
  await browser.close();
}
console.log(failed ? `\n${failed} 件 NG(比べた ${compared} 件)` : `\nすべて ok(比べた ${compared} 件)`);
process.exit(failed ? 1 : 0);
