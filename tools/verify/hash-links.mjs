import { launch, newPage, csp } from "./lib.mjs";
const B = process.env.NEW || "http://127.0.0.1:8787";
const b = await launch();
let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } else console.log("ok  ", m); };
const pos = (p, id) => p.evaluate((id) => { const h = document.getElementById(id); const c = document.querySelector(".fbc-content"); const cs = getComputedStyle(c).overflowY; const top = h.getBoundingClientRect().top - (cs === "auto" ? c.getBoundingClientRect().top : 0); return Math.round(top); }, id);

for (const mobile of [false, true]) {
  const p = await newPage(b, mobile ? { width: 390, height: 844, mobile: true } : {});
  const tag = mobile ? "mobile" : "pc";
  // 直接開く(#見出し)
    await p.goto(B + "/learn/basics/transform", { waitUntil: "load" });
  await p.waitForSelector(".mud-layout");
  const ids = await p.evaluate(() => [...document.querySelectorAll(".article-body h2")].map((h) => h.id));
  const target = ids[3];
  await p.goto(B + "/learn/basics/transform#" + encodeURIComponent(target), { waitUntil: "load" });
  await p.waitForSelector(".mud-layout"); await p.waitForTimeout(900);
  const t1 = await pos(p, target);
  ok(t1 >= -4 && t1 < 120, `${tag}: direct load with #heading lands near the top (${t1}px)`);
  if (!mobile) {
    const cur = await p.locator(".article-toc a.is-current").allInnerTexts();
    const h2 = await p.evaluate((id) => document.getElementById(id).textContent.trim(), target);
    ok(cur.length === 1 && cur[0].trim() === h2, `${tag}: TOC highlights the target heading (${cur})`);
  }
  // アプリ内で目次のリンク(同じページの #)
  const other = ids[1];
  if (!mobile) {
    await p.locator(`.article-toc a[href$="#${encodeURIComponent(other)}"], .article-toc a[href$="#${other}"]`).first().click();
  } else {
    await p.locator(".article-toc-mobile summary").click();
    await p.locator(`.article-toc-mobile a[href$="#${encodeURIComponent(other)}"], .article-toc-mobile a[href$="#${other}"]`).first().click();
  }
  await p.waitForTimeout(700);
  const t2 = await pos(p, other);
  ok(t2 >= -4 && t2 < 120, `${tag}: in-app TOC link scrolls to the heading (${t2}px)`);
  // 別の記事から、見出し付きで移動(検索の結果など)
  await p.goto(B + "/links", { waitUntil: "load" }); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(300);
  await p.evaluate(() => { window.__marker = 1; });
  await p.evaluate((t) => { const a = document.createElement("a"); a.href = "/learn/basics/transform#" + t; a.id = "tmp-link"; a.textContent = "x"; document.querySelector(".fbc-content").appendChild(a); }, encodeURIComponent(target));
  await p.locator("#tmp-link").click();
  await p.waitForTimeout(900);
  ok((await p.evaluate(() => window.__marker)) === 1, `${tag}: in-app link with #heading, no reload`);
  const t3 = await pos(p, target);
  ok(t3 >= -4 && t3 < 120, `${tag}: navigation with #heading lands on the heading (${t3}px)`);
  // 用語集の #id (検索からの移動) — アプリ内
  await p.goto(B + "/links", { waitUntil: "load" }); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(300);
  await p.evaluate(() => { window.__marker = 1; const a = document.createElement("a"); a.href = "/glossary#vertex"; a.id = "tmp-link"; a.textContent = "x"; document.querySelector(".fbc-content").appendChild(a); });
  await p.locator("#tmp-link").click();
  await p.waitForTimeout(900);
  const g = await pos(p, "vertex");
  ok(g >= -4 && g < 200, `${tag}: /glossary#vertex lands on the item (${g}px)`);
  ok((await p.evaluate(() => window.__marker)) === 1, `${tag}: glossary link no reload`);
  // 戻る: 前のスクロール?  (戻る = 前のページ)
  await p.goBack(); await p.waitForTimeout(600);
  ok(p.url().endsWith("/links"), `${tag}: back works`);
  console.log(tag, "problems", p.problems, "csp", await csp(p));
  await p.context().close();
}
console.log(fails ? `${fails} FAILED` : "all ok");
await b.close();
