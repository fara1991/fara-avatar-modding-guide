import { launch, newPage, csp } from "./lib.mjs";
const B = process.env.NEW || "http://127.0.0.1:8787";
const b = await launch();
let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } else console.log("ok  ", m); };
const boot = async (p, path) => { await p.goto(B + path, { waitUntil: "load" }); await p.waitForSelector(".mud-layout", { timeout: 60000 }); await p.waitForTimeout(300); };

// ---- 記事: 目次の強調・サイドバー・完了の記録
{
  const p = await newPage(b, { seed: { "avatar-modding-guide.progress": JSON.stringify({ "basics/select": "2026-10-01T00:00:00.000Z", "x/unknown": "keep", "basics/zero": 0 }) } });
  await boot(p, "/learn/basics/transform");
  ok((await p.locator("a.mud-nav-link.active").count()) === 1, "sidebar: exactly one active link on an article page");
  ok((await p.locator("a.mud-nav-link.active").getAttribute("href")) === "learn/basics", "sidebar: active is the category");
  const badge = await p.locator(".fsk-sidebar a[href='learn/basics'] .category-progress").innerText();
  ok(/^1\/\d+$/.test(badge.trim()), "sidebar: category progress shows 1/N: " + badge);
  ok(!(await p.locator("header.article-header .done-mark").isVisible()), "article: not done mark hidden");
  // 完了にする
  await p.locator(".article-complete button").click();
  ok(await p.locator("header.article-header .done-mark").isVisible(), "article: done mark visible after click");
  const stored = JSON.parse(await p.evaluate(() => localStorage.getItem("avatar-modding-guide.progress")));
  ok(typeof stored["basics/transform"] === "string" && /^\d{4}-\d\d-\d\dT[\d:.]+Z$/.test(stored["basics/transform"]), "progress: ISO date written");
  ok(stored["x/unknown"] === "keep" && stored["basics/zero"] === 0 && stored["basics/select"] === "2026-10-01T00:00:00.000Z", "progress: unknown/old values preserved");
  const badge2 = await p.locator(".fsk-sidebar a[href='learn/basics'] .category-progress").innerText();
  ok(/^2\/\d+$/.test(badge2.trim()), "sidebar: category progress updated to 2/N: " + badge2);
  await p.reload(); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(300);
  ok(await p.locator("header.article-header .done-mark").isVisible(), "progress: persists after reload");
  await p.locator(".article-complete button").click();
  const stored2 = JSON.parse(await p.evaluate(() => localStorage.getItem("avatar-modding-guide.progress")));
  ok(!("basics/transform" in stored2) && stored2["x/unknown"] === "keep", "progress: undone removes only that id");
  // TOC 強調: 本文をスクロール
  const first = await p.locator(".article-toc a").nth(0).getAttribute("href");
  await p.evaluate(() => { const c = document.querySelector(".fsk-content"); c.scrollTop = 0; });
  await p.waitForTimeout(200);
  const cur0 = await p.locator(".article-toc a.is-current").count();
  await p.evaluate(() => { const h = document.querySelectorAll(".article-body h2")[3]; h.scrollIntoView({ block: "start" }); });
  await p.waitForTimeout(400);
  const cur = await p.locator(".article-toc a.is-current").allInnerTexts();
  const h2 = await p.evaluate(() => document.querySelectorAll(".article-body h2")[3].textContent.trim());
  ok(cur.length === 1 && cur[0].trim() === h2, `toc: current heading follows scroll (${cur0} at top; ${cur} / ${h2})`);
  ok((await p.locator(".article-toc a.is-current").getAttribute("aria-current")) === "location", "toc: aria-current=location");
  // 一番下 → 最後の見出し
  await p.evaluate(() => { const c = document.querySelector(".fsk-content"); c.scrollTop = c.scrollHeight; });
  await p.waitForTimeout(400);
  const last = await p.locator(".article-toc a.is-current").allInnerTexts();
  const lastH = await p.evaluate(() => { const hs = document.querySelectorAll(".article-body h2"); return hs[hs.length - 1].textContent.trim(); });
  ok(last.length === 1 && last[0].trim() === lastH, "toc: bottom of the page selects the last heading");
  // 記事 → 記事 (次へ): ページ再読み込みなし、スクロールが戻る、目次が作り直される
  await p.evaluate(() => { window.__marker = 1; });
  await p.locator(".article-nav a.btn-primary").click();
  await p.waitForTimeout(500);
  ok((await p.evaluate(() => window.__marker)) === 1, "nav: next article without a page reload");
  ok(p.url().includes("/learn/") && !p.url().endsWith("/transform"), "nav: url changed " + p.url());
  ok((await p.evaluate(() => document.querySelector(".fsk-content").scrollTop)) === 0, "nav: scroll reset to top");
  const th = await p.title();
  ok(/Avatar Modding Guide$/.test(th) && !th.startsWith("移動"), "nav: title updated: " + th);
  ok((await p.locator("a.mud-nav-link.active").count()) === 1, "sidebar: still one active after navigation");
  // 練習へ → 再読み込み
  await p.locator(".fsk-sidebar a[href='practice']").click();
  await p.waitForLoadState("load");
  await p.waitForTimeout(500);
  ok(p.url().endsWith("/practice") && (await p.evaluate(() => window.__marker)) === undefined, "nav: practice is a full page load");
  await p.context().close();
}
console.log(fails ? `${fails} FAILED` : "all ok");
await b.close();
