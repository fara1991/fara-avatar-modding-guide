// 起動の前の静的な本文でスクロールしたあと起動しても、読んでいる位置が飛ばないこと(Phase 4)。 NEW=... node boot-scroll.mjs
// blazor.webassembly.js の読み込みを止めておき、静的な本文を window でスクロールしてから、読み込みを通す。起動の前後で、同じ見出しの画面上の位置が同じ(±2px)であること。
import { launch, newPage } from "./lib.mjs";
const NEW = (process.env.NEW || "http://127.0.0.1:8787").replace(/\/$/, "");
const PATH = process.argv[2] || "/learn/getting-started/install";
const b = await launch();
let bad = 0;
for (const [name, vp, y] of [["pc", { width: 1280, height: 800 }, 500], ["sp", { width: 390, height: 844, mobile: true }, 900]]) {
  const p = await newPage(b, vp);
  let release;
  const gate = new Promise((r) => { release = r; });
  await p.route("**/_framework/blazor.webassembly.js", async (route) => { await gate; await route.continue(); });
  await p.goto(NEW + PATH, { waitUntil: "commit" });
  await p.waitForSelector(".av-static");
  await p.evaluate(() => document.fonts && document.fonts.ready);
  await p.waitForTimeout(2500);   // 画像・フォントの読み込みで静的な本文の高さが変わり終わるのを待つ(変わると、スクロール位置も動く)
  await p.evaluate((yy) => window.scrollTo(0, yy), y);
  await p.waitForTimeout(1500);   // スクロールで読み込まれる画像(lazy)で高さが変わり終わるのを待つ
  // 画面の上のほうに見えている見出し(静的な本文で、y が 60px 以上の最初の h2/h3)を覚えて、起動の後に同じ見出しの位置を見る
  const pick = () => p.evaluate(() => { const h = [...document.querySelectorAll("#fbc-main h2, #fbc-main h3")].find((e) => e.getBoundingClientRect().y >= 60); return h ? h.id : null; });
  const at = (id) => p.evaluate((i) => { const h = document.getElementById(i); return h ? Math.round(h.getBoundingClientRect().y * 10) / 10 : null; }, id);
  const id = await pick();
  const probe = async () => ({ id, y: await at(id) });
  const before = await probe();
  const staticScroll = await p.evaluate(() => window.scrollY);
  release();
  await p.waitForSelector(".fbc-page", { timeout: 60000 });
  await p.waitForTimeout(1500);
  const after = await probe();
  const ok = before && after && before.id === after.id && Math.abs(before.y - after.y) <= 2 && staticScroll > 0;
  console.log(`${ok ? "ok" : "NG"} ${name}: 静的で window を ${staticScroll}px スクロール → 見出し ${before?.id} の位置 ${before?.y} → ${after?.y}`);
  if (!ok) bad++;
  await p.context().close();
}
await b.close();
process.exit(bad ? 1 : 0);
