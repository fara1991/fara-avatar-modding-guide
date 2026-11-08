// レンダーラボを、本番と同じ CSP(HEADERS=dist/_headers を渡した gl-serve.mjs)で開き、操作して、CSP 違反・コンソールのエラーと警告・失敗したリクエストが 0 件であること、
// PC(1000)とスマホ(390)の幅で描画が始まる(キャンバスが大きさどおりで、描いた枚数が増える)ことを確かめる。  node gl-csp.mjs
import { launch, newPage, csp } from "./lib.mjs";
const ORIGIN = process.env.ORIGIN || "http://127.0.0.1:8790";
const browser = await launch();
let failed = 0;
const ok = (c, l, d = "") => { console.log(`${c ? "ok  " : "NG  "} ${l}${!c && d ? "  -> " + d : ""}`); if (!c) failed++; };
for (const [label, width, height, mobile] of [["PC 1280", 1280, 800, false], ["スマホ 390", 390, 800, true]]) {
  const page = await newPage(browser, { width, height, mobile });
  const bad = [];
  page.on("response", (r) => { if (r.status() >= 400) bad.push(r.status() + " " + r.url()); });
  await page.goto(`${ORIGIN}/lab/`);
  await page.waitForSelector("#viewport canvas", { timeout: 90000 });
  await page.waitForFunction(() => window.__glDiag && JSON.parse(window.__glDiag()).drawn > 3, null, { timeout: 60000 });
  const box = await page.locator("#viewport").boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down(); await page.mouse.up();
  await page.keyboard.press("Tab"); await page.keyboard.press("KeyA"); await page.keyboard.press("KeyG");
  await page.mouse.move(box.x + box.width / 2 + 40, box.y + box.height / 2 + 20, { steps: 4 });
  await page.keyboard.press("Enter");
  await page.mouse.wheel(0, -200);
  await page.waitForTimeout(500);
  const d = JSON.parse(await page.evaluate(() => window.__glDiag()));
  const scrolled = await page.evaluate(() => window.scrollY);
  ok(d.canvas.cw === Math.round(box.width) && d.canvas.w === Math.floor(d.canvas.cw * d.pixelRatio), `${label}: キャンバスの大きさ ${d.canvas.w}x${d.canvas.h}(表示 ${d.canvas.cw}x${d.canvas.ch})`);
  ok(d.drawn > 10 && !d.lost, `${label}: 描いた枚数 ${d.drawn}`);
  ok(scrolled === 0, `${label}: ホイールでページがスクロールしない`);
  await page.screenshot({ path: `${process.env.OUT || "."}/csp-${width}.png`, clip: { x: 0, y: 0, width: Math.min(width, 960), height: Math.ceil(box.height) } });
  const v = await csp(page);
  ok(v.length === 0, `${label}: CSP 違反 ${v.length} 件`, v.join(" | "));
  const problems = page.problems.filter((m) => !/Failed to load resource/.test(m)).concat(bad);
  ok(problems.length === 0, `${label}: コンソールのエラー/警告・失敗したリクエスト ${problems.length} 件`, problems.join(" | "));
  await page.context().close();
}
await browser.close();
process.exit(failed ? 1 : 0);
