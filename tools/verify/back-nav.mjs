// 戻る/進むでスクロール位置が復元されること(PC は本文の領域 .fsk-content、スマホは window)。
import { launch, newPage, csp } from "./lib.mjs";
const B = process.env.NEW || "http://127.0.0.1:8787";
const b = await launch();
let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } else console.log("ok  ", m); };
const top = (p) => p.evaluate(() => { const c = document.querySelector(".fsk-content"); return Math.round(/^(auto|scroll)$/.test(getComputedStyle(c).overflowY) ? c.scrollTop : window.scrollY); });
const setTop = (p, y) => p.evaluate((y) => { const c = document.querySelector(".fsk-content"); if (/^(auto|scroll)$/.test(getComputedStyle(c).overflowY)) c.scrollTop = y; else window.scrollTo(0, y); }, y);
for (const mobile of [false, true]) {
  const tag = mobile ? "mobile" : "pc";
  const p = await newPage(b, mobile ? { width: 390, height: 844, mobile: true } : {});
  await p.goto(B + "/learn/basics/transform", { waitUntil: "load" }); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(500);
  await setTop(p, 700); await p.waitForTimeout(300);
  const before = await top(p);
  ok(before > 500, `${tag}: scrolled down (${before})`);
  await p.evaluate(() => document.querySelector(".article-nav a.btn-primary").click()); await p.waitForTimeout(700);
  ok((await top(p)) === 0, `${tag}: forward navigation starts at the top`);
  await setTop(p, 300); await p.waitForTimeout(300);
  await p.goBack(); await p.waitForTimeout(1200);
  ok(p.url().endsWith("/transform"), `${tag}: back to the first article`);
  const after = await top(p);
  ok(Math.abs(after - before) <= 2, `${tag}: back restores the scroll position (${before} -> ${after})`);
  await p.goForward(); await p.waitForTimeout(1200);
  const fwd = await top(p);
  ok(Math.abs(fwd - 300) <= 2, `${tag}: forward restores the position (${fwd})`);
  console.log(tag, "problems", p.problems, "csp", await csp(p));
  await p.context().close();
}
console.log(fails ? `${fails} FAILED` : "all ok");
await b.close();
