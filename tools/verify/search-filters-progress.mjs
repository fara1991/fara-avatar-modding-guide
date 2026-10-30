import { launch, newPage, csp } from "./lib.mjs";
const B = process.env.NEW || "http://127.0.0.1:8787";
const b = await launch();
let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } else console.log("ok  ", m); };
const boot = async (p, path) => { await p.goto(B + path, { waitUntil: "load" }); await p.waitForSelector(".mud-layout", { timeout: 60000 }); await p.waitForTimeout(300); };

// ---- ヘッダーの検索
{
  const p = await newPage(b);
  await boot(p, "/links");
  await p.keyboard.press("Control+k");
  ok(await p.evaluate(() => document.activeElement && document.activeElement.classList.contains("av-search-input")), "search: Ctrl+K focuses the header search");
  await p.keyboard.type("yougo");
  await p.waitForSelector(".av-search-item");
  const first = await p.locator(".av-search-item").first().innerText();
  ok(/用語集/.test(first), "search: yougo -> first hit is 用語集: " + first.replace(/\s+/g, " "));
  /* marks: see below */
  ok((await p.locator(".av-search-input").getAttribute("aria-expanded")) === "true", "search: aria-expanded");
  await p.keyboard.press("ArrowDown");
  ok((await p.locator(".av-search-item.is-active").count()) === 1, "search: ArrowDown selects first");
  await p.evaluate(() => { window.__marker = 7; });
  await p.keyboard.press("Enter");
  await p.waitForTimeout(500);
  ok(p.url().endsWith("/glossary"), "search: Enter on the first hit opens /glossary (in app): " + p.url());
  ok((await p.evaluate(() => window.__marker)) === 7, "search: no reload");
  // Esc で閉じる / で移る
  await p.locator("body").click({ position: { x: 600, y: 300 } });
  await p.keyboard.press("/");
  ok(await p.evaluate(() => document.activeElement.classList.contains("av-search-input")), "search: slash focuses");
  await p.keyboard.type("blender");
  await p.waitForSelector(".av-search-item");
  ok((await p.locator(".av-search-item mark").count()) > 0, "search: marks present for blender");
  await p.keyboard.press("Escape");
  ok((await p.locator(".av-search-list").count()) === 0, "search: Esc closes list");
  // Enter だけ → /search?q=
  await p.locator(".av-search-input").fill("");
  await p.keyboard.type("ウェイト ペイント");
  await p.waitForSelector(".av-search-item");
  await p.keyboard.press("Enter");
  await p.waitForTimeout(600);
  ok(/\/search\?q=/.test(p.url()), "search: Enter without selection goes to /search?q=: " + decodeURIComponent(p.url()));
  const cnt = await p.locator(".search-result").count();
  ok(cnt > 0, "search page: results rendered (" + cnt + ")");
  ok(/\d+ 件/.test(await p.locator("p.text-body-secondary").first().innerText()), "search page: count text");
  // 検索ページ内で再検索
  await p.locator(".fbc-content input[name=q]").fill("modeling");
  await p.keyboard.press("Enter");
  await p.waitForTimeout(500);
  ok(p.url().endsWith("q=modeling"), "search page: form submit -> ?q=modeling (in app): " + p.url());
  console.log("problems", p.problems, "csp", await csp(p));
  await p.context().close();
}

// ---- 用語集・TIPS の絞り込みと #id
{
  const p = await newPage(b);
  await boot(p, "/tips");
  const total = await p.locator("[data-tip-item]").count();
  const visible = async () => p.evaluate(() => [...document.querySelectorAll("[data-tip-item]")].filter((e) => !e.hidden).length);
  ok((await visible()) === total, "tips: all visible at start (" + total + ")");
  ok((await p.locator(".tips-filter p").innerText()).trim() === total + " 件", "tips: count text");
  await p.locator(".tips-filter input").fill("ピンク");
  await p.waitForTimeout(200);
  const v1 = await visible();
  ok(v1 > 0 && v1 < total, "tips: text filter narrows (" + v1 + ")");
  ok((await p.locator(".tips-filter p").innerText()).trim() === `${total} 件中 ${v1} 件`, "tips: count text with filter");
  await p.locator(".tips-filter input").fill("");
  await p.locator(".tips-filter button", { hasText: /^Unity$/ }).click();
  await p.waitForTimeout(200);
  ok(p.url().includes("tag=Unity"), "tips: tag in url " + p.url());
  const v2 = await visible();
  ok(v2 > 0 && v2 < total, "tips: tag filter narrows (" + v2 + ")");
  ok((await p.locator(".tips-filter button.btn-primary").innerText()).trim() === "Unity", "tips: pressed state");
  await p.locator(".tips-filter button", { hasText: /^すべて$/ }).click();
  await p.waitForTimeout(100);
  ok(!p.url().includes("tag="), "tips: all clears tag from url");
  await boot(p, "/tips?tag=Blender");
  ok((await p.locator(".tips-filter button.btn-primary").innerText()).trim() === "Blender", "tips: ?tag= on load");
  const id = await p.evaluate(() => { const items = [...document.querySelectorAll("[data-tip-item]")]; const hidden = items.find((e) => e.hidden); return hidden ? hidden.querySelector("article").id : null; });
  ok(!!id, "tips: has a hidden item under the tag filter: " + id);
  await p.goto(B + "/tips?tag=Blender#" + id, { waitUntil: "load" });
  await p.waitForSelector(".mud-layout"); await p.waitForTimeout(600);
  const state = await p.evaluate((id) => { const a = document.getElementById(id); const r = a.getBoundingClientRect(); const c = document.querySelector(".fbc-content").getBoundingClientRect(); return { hidden: a.closest("[data-tip-item]").hidden, open: !!a.querySelector("details.tip-body")?.open, hasDetails: !!a.querySelector("details.tip-body"), top: r.top - c.top, ch: c.height }; }, id);
  ok(!state.hidden, "tips: #id un-hides the item");
  ok(!state.hasDetails || state.open, "tips: #id opens the details");
  ok(state.top >= -2 && state.top < state.ch, "tips: #id scrolled into view (top " + Math.round(state.top) + ")");
  await p.context().close();
}
{
  const p = await newPage(b);
  await boot(p, "/glossary");
  const n = await p.locator("[data-tip-item]").count();
  await p.locator(".tips-filter input").fill("weight");
  await p.waitForTimeout(200);
  const v = await p.evaluate(() => [...document.querySelectorAll("[data-tip-item]")].filter((e) => !e.hidden).length);
  ok(v > 0 && v < n, "glossary: weight narrows " + v + "/" + n);
  await p.locator(".tips-filter input").fill("ｗｅｉｇｈｔ");
  await p.waitForTimeout(200);
  const v2 = await p.evaluate(() => [...document.querySelectorAll("[data-tip-item]")].filter((e) => !e.hidden).length);
  ok(v2 === v, "glossary: full-width input gives the same result");
  await p.context().close();
}

// ---- ショートカットの絞り込み
{
  const p = await newPage(b);
  await boot(p, "/shortcuts");
  const rows = await p.locator("#shortcut-rows tr").count();
  const cats = await p.locator("#shortcut-filters a").allInnerTexts();
  await p.locator("#shortcut-filters a", { hasText: cats[1] }).click();
  await p.waitForTimeout(200);
  const shown = await p.evaluate(() => [...document.querySelectorAll("#shortcut-rows tr")].filter((e) => !e.hidden).length);
  ok(shown > 0 && shown < rows && p.url().includes("category="), `shortcuts: category filter (${shown}/${rows}) ${decodeURIComponent(p.url())}`);
  await p.goBack(); await p.waitForTimeout(300);
  const shown2 = await p.evaluate(() => [...document.querySelectorAll("#shortcut-rows tr")].filter((e) => !e.hidden).length);
  ok(shown2 === rows, "shortcuts: back restores all rows");
  await p.context().close();
}

// ---- 目次・ホーム・カテゴリの進捗
{
  const seed = { "avatar-modding-guide.progress": JSON.stringify({ "basics/select": "2026-10-01T00:00:00.000Z", "basics/transform": "2026-10-02T00:00:00.000Z" }) };
  const p = await newPage(b, { seed });
  await boot(p, "/learn");
  ok(/50 ページ中 2 ページ完了/.test(await p.locator(".progress-summary").innerText()), "learn: summary 2/50");
  ok((await p.locator(".progress-summary a.btn-primary").count()) === 1, "learn: next link present");
  const nextHref = await p.locator(".progress-summary a.btn-primary").getAttribute("href");
  ok(/^\/learn\//.test(nextHref), "learn: next href " + nextHref);
  ok((await p.locator(".roadmap-articles li.is-done").count()) === 2, "learn: 2 done items");
  ok(await p.locator(".roadmap-articles li.is-done .done-mark").first().isVisible(), "learn: done check visible");
  const prog = await p.locator(".roadmap-body .category-progress:not([hidden])").allInnerTexts();
  ok(prog.length === 1 && /^2\/5$/.test(prog[0].trim()), "learn: category progress " + prog);
  await boot(p, "/learn/basics");
  ok((await p.locator(".article-card.is-done").count()) === 2, "category: 2 done cards");
  ok(await p.locator("h1 .category-progress").isVisible(), "category: h1 progress visible");
  await boot(p, "/");
  ok(await p.locator(".progress-summary").isVisible(), "home: summary visible when progress exists");
  p.once("dialog", (d) => d.accept());
  await boot(p, "/learn");
  await p.locator(".progress-summary button").click();
  await p.waitForTimeout(200);
  ok((await p.evaluate(() => localStorage.getItem("avatar-modding-guide.progress"))) === "{}", "learn: reset writes {}");
  await p.context().close();
  const p2 = await newPage(b);
  await boot(p2, "/");
  ok(!(await p2.locator(".progress-summary").isVisible()), "home: summary hidden when no progress");
  await p2.context().close();
}
console.log(fails ? `${fails} FAILED` : "all ok");
await b.close();
