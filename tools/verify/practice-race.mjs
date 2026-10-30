// 低速な読み込み中に「離れて戻る」(Phase 3b S3 のレビュー指摘)。  node practice-race.mjs   NEW=http://127.0.0.1:8787
// Practice・Render の wasm の応答を 3.5 秒遅らせ、/practice を開く → 1.2 秒後に /learn へ(画面内の移動)→ 0.5 秒後に /practice へ戻る。
// 期待: 二重読み込みの衝突('body stream already read' など)が出ず、コンソールのエラー・警告 0、戻ったあとに C# 版の canvas が 1 つ出て、描画が始まる。
import { launch, newPage } from "./lib.mjs";
const NEW = process.env.NEW || "http://127.0.0.1:8787";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let failed = 0;
const ok = (c, l, d = "") => { console.log(`${c ? "ok  " : "NG  "} ${l}${!c && d ? "  -> " + d : ""}`); if (!c) failed++; };
const browser = await launch();
const page = await newPage(browser, { width: 1280, height: 800 });
let delayed = 0;
await page.route(/FaraAvatarModdingGuide\.(Practice|Render)\..*\.wasm/, async (route) => { delayed++; await sleep(3500); await route.continue(); });
await page.goto(NEW + "/practice", { waitUntil: "load" });
await page.waitForSelector(".mud-layout", { timeout: 90000 });
await sleep(1200);
await page.evaluate(() => window.Blazor.navigateTo("/learn"));
await page.waitForSelector("h1", { timeout: 30000 });
await sleep(500);
await page.evaluate(() => window.Blazor.navigateTo("/practice"));
await page.waitForSelector(".practice-viewport canvas", { timeout: 60000 });
await sleep(1500);
ok(delayed === 2, "Practice・Render の wasm を 1 回ずつだけ読む(離れて戻っても、二重に読まない)", String(delayed));
ok((await page.locator(".practice-viewport canvas").count()) === 1, "canvas は 1 つ");
ok((await page.locator(".vp-mode").count()) === 1, "HUD が出ている");
ok(page.problems.length === 0, "コンソールのエラー・警告 0", page.problems.join(" | "));
// 描画が動いている(C# 版: ビューの状態が変わる = キー操作が効く)
await page.locator("#practice-viewport").click();
await page.keyboard.press("Tab");
await sleep(200);
await browser.close();
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
