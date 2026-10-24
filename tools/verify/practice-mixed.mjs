// Unity と Blender の記事内チュートリアルが同じページにあるときの、WebGL の切り替えの確認(Phase 3b S5 のレビュー M2)。  node practice-mixed.mjs
//   NEW=http://127.0.0.1:8787
// /learn/unity/modular-avatar-outfit(Unity の ma-outfit)の本文に、Blender の transform の埋め込みを足したページ(data/content.json を書き換えて作る。実際の記事は変えない)を、?engine=cs で開き、
// 2 つを交互に始めて、そのたびに「WebGL の canvas は 1 つだけ・始めたほうにだけ 3D ビューがある・Unity の DOM（.ue-menubar）は Unity が持っているときだけ」を確かめる。
// WebGL を使うのはページ内で 1 つだけ(gl.js の start() が前のものを壊す)なので、「アクティブなビュー」の登録が Unity と Blender で別だと、前のものが手放されず、DOM だけが残る。
import { launch, newPage } from "./lib.mjs";
import { realProblems, sleep } from "./tutorial-lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const UNITY = '.practice-embed[data-scenario="ma-outfit"]';
const BLENDER = '.practice-embed[data-scenario="transform"]';
let failed = 0;
const check = (ok, label, detail = "") => { console.log(`${ok ? "ok  " : "NG  "} ${label}${ok ? "" : "  " + detail}`); if (!ok) failed++; };

const browser = await launch();
const page = await newPage(browser, { width: 1280, height: 900 });
await page.route("**/data/content.json", async (route) => {
  const res = await route.fetch();
  const text = await res.text();
  const unity = '<div class=\\"practice-embed\\" data-scenario=\\"ma-outfit\\"></div>';
  const patched = text.replaceAll(unity, unity + '<div class=\\"practice-embed\\" data-scenario=\\"transform\\"></div>');
  await route.fulfill({ response: res, body: patched });
});
await page.goto(`${NEW}/learn/unity/modular-avatar-outfit?engine=cs`, { waitUntil: "load" });
await page.waitForSelector(".mud-layout", { timeout: 90000 });
await page.waitForSelector(`${UNITY}.is-ready`, { timeout: 60000 });
await page.waitForSelector(`${BLENDER}.is-ready`, { timeout: 60000 });
check(true, "Unity と Blender の埋め込みが同じページに出る");

/** 状態: 全体の canvas の数・各埋め込みの canvas・Unity の DOM */
const state = () => page.evaluate(([u, b]) => {
  const q = (s, sel) => document.querySelector(s)?.querySelectorAll(sel).length ?? -1;
  return { total: document.querySelectorAll(".practice-embed canvas").length, unityCanvas: q(u, "canvas"), blenderCanvas: q(b, "canvas"), unityDom: q(u, ".ue-menubar"), blenderHud: q(b, ".practice-hud") };
}, [UNITY, BLENDER]);

const start = async (sel) => {
  const el = page.locator(sel);
  await el.scrollIntoViewIfNeeded();
  await el.locator(".tut-overlay").click();
  await el.locator("canvas").first().waitFor({ timeout: 60000 });
  await sleep(600);
};

// 画面に入ったとき自動で始まる(観察の順で、どちらかが先)ので、ここでは「いまの状態が一貫しているか」から確かめる
await sleep(1500);
let s = await state();
check(s.total <= 1, "最初: WebGL の canvas は多くて 1 つ", JSON.stringify(s));
check(s.unityDom === 0 || s.unityCanvas === 1, "最初: Unity の DOM があるなら、その canvas もある", JSON.stringify(s));

for (const [name, sel, other] of [["Unity", UNITY, "blender"], ["Blender", BLENDER, "unity"], ["Unity", UNITY, "blender"], ["Blender", BLENDER, "unity"], ["Unity", UNITY, "blender"]]) {
  await start(sel);
  s = await state();
  const mineOk = name === "Unity" ? s.unityCanvas === 1 && s.unityDom === 1 && s.blenderCanvas === 0 : s.blenderCanvas === 1 && s.unityDom === 0 && s.unityCanvas === 0;
  check(s.total === 1 && mineOk, `${name} を始める: canvas は 1 つ（${name} の中）・もう一方に 3D ビューが残らない`, JSON.stringify(s));
  // 始めたほうが操作できる(Unity: Hierarchy が出ている・Blender: HUD が出ている)
  const alive = name === "Unity" ? await page.locator(`${UNITY} .ue-row`).count() > 0 : await page.locator(`${BLENDER} .practice-hud`).count() > 0;
  check(alive, `${name} の画面が描かれている`);
}

const problems = realProblems(page);
check(problems.length === 0, "コンソールのエラー・CSP 違反がない", problems.join(" | "));
await page.context().close();
await browser.close();
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
