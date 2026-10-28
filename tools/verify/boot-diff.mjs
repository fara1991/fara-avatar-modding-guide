// 起動の前後の見た目の差(計画書 4.10)。 NEW=http://127.0.0.1:8787 node boot-diff.mjs <出力フォルダ> [パス...]
//   前 = WebAssembly(blazor.webassembly.js)を止めて、静的 HTML のまま描いた画面(起動までの利用者の目に映るもの)
//   後 = 通常どおり起動して、.mud-layout が描かれた画面
// 各ページ・PC 1280x800 / スマホ 390x844・ダーク / ライトで、次を測る。
//   ・ピクセル差の割合(pixelmatch。canvas・動的な部分(チュートリアルの枠・進捗の印・フッターの SNS)は両方で隠す)
//   ・本文(見出し・最初の段落・パンくず)の位置の差(getBoundingClientRect。起動の前後。CLS の API は、前後で「同じ要素」でないと数えない = Blazor は #app の中身を置き換えるので、
//     数値が 0 でも位置は動いている。そのため位置を直接測る)
//   ・起動の瞬間の本文の動き: 毎フレーム見出しの位置を記録し、静的 → 起動後に切り替わる 1 フレームでの移動(px)を出す(Web フォントの読み込みによる再配置は、起動と関係ないので数えない)
//   ・サイドバーの「いまのページ」の強調の計算済みスタイル(color・background-color)が、静的と起動後で同じ(PC。スマホはサイドバーが閉じていて、静的は出さない)
//   ・CLS(layout-shift の合計。参考)と、読み込み表示が本文を覆う最大の割合(画面に対する %。右下の小さな表示なので 2% 以下であること)
// 環境変数: SIDEBAR_HIDDEN=1 で、PC のサイドバーを隠した設定で測る。HIDE_OVERLAY=1 で、前の画面から起動中のオーバーレイを隠す(オーバーレイを除いた「枠のそろい」だけを測る)。
//   MAX_RATIO(既定 0.5 = %)・MAX_SHIFT(既定 1 = px)を超えたら、終了コード 1。
import { launch, newPage } from "./lib.mjs";
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import pixelmatch from "pixelmatch";

const NEW = (process.env.NEW || "http://127.0.0.1:8787").replace(/\/$/, "");
const HIDE_OVERLAY = process.env.HIDE_OVERLAY !== "0";   // 既定: 前の画面から、右下の「読み込み中…」を隠す(固定位置の表示で、本文の位置には関係しない。覆っていないことは別に確かめる)
const SIDEBAR_HIDDEN = process.env.SIDEBAR_HIDDEN === "1";   // PC のサイドバーを隠す設定(細い列)で測る
const SEED = SIDEBAR_HIDDEN ? { "avatar-modding-guide.sidebar-hidden": "1" } : {};
const MAX_RATIO = Number(process.env.MAX_RATIO ?? 0.5);
const MAX_SHIFT = Number(process.env.MAX_SHIFT ?? 1);
const out = process.argv[2] || "boot-diff";
const DEFAULT_PATHS = ["/learn/getting-started/install", "/learn/basics/transform", "/learn/modeling/edit-mode", "/learn/unity/modular-avatar-outfit", "/learn/vrchat/upload", "/", "/search"];
const paths = process.argv.slice(3).length ? process.argv.slice(3) : DEFAULT_PATHS;
mkdirSync(out, { recursive: true });

// 動的な部分は、前後で内容が違って当然なので、両方で隠す(visibility のまま場所は残す)。動くものは止める
const MASK_CSS = `
input, canvas, #site-scene, .practice-embed, .category-progress, .fbc-footer-socials, .done-badge, .done-mark, .article-complete, .roadmap-progress, [data-progress-page] .badge-done { visibility: hidden !important; }
input { border-color: transparent !important; box-shadow: none !important; outline: none !important; }
*, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }
`;

/** 毎フレームの見出しの位置と、本文が見えているかを記録する(ページの先頭で仕掛ける) */
const RECORDER = () => {
  window.__frames = [];
  const t0 = performance.now();
  // 本文が見えているか: 読み込み表示(#boot-indicator)が画面の 20% 以上を覆っていなければ、見えている
  const visible = () => {
    const o = document.getElementById("boot-indicator");
    if (!o) return true;
    const cs = getComputedStyle(o);
    if (cs.display === "none" || cs.visibility === "hidden") return true;
    const r = o.getBoundingClientRect();
    const covered = (Math.max(0, Math.min(r.right, innerWidth) - Math.max(r.left, 0)) * Math.max(0, Math.min(r.bottom, innerHeight) - Math.max(r.top, 0))) / (innerWidth * innerHeight);
    window.__maxCover = Math.max(window.__maxCover || 0, covered);
    return covered < 0.2 || (Number(cs.opacity) < 0.15);
  };
  const loop = () => {
    const h = document.querySelector("#fbc-main h1, main h1, h1");
    if (h) {
      const r = h.getBoundingClientRect();
      window.__frames.push({ t: Math.round(performance.now() - t0), x: r.x, y: r.y, w: r.width, vis: visible(), mud: !!document.querySelector(".mud-layout") });
    }
    requestAnimationFrame(loop);
  };
  requestAnimationFrame(loop);
  window.__cls = 0;
  try { new PerformanceObserver((l) => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cls += e.value; }).observe({ type: "layout-shift", buffered: true }); } catch { /* 古いブラウザ */ }
};

const settle = async (p) => {
  await p.evaluate(() => document.fonts && document.fonts.ready);
  // 画像: 読み込む指定(lazy でないもの)だけ待つ。lazy の画像は、画面の外では読まれないので待たない。最大 5 秒
  await p.evaluate(() => Promise.race([
    Promise.all([...document.images].filter((i) => i.loading !== "lazy").map((i) => (i.complete ? 1 : new Promise((r) => { i.onload = i.onerror = r; })))),
    new Promise((r) => setTimeout(r, 5000)),
  ]));
  await p.addStyleTag({ content: MASK_CSS });
  await p.waitForTimeout(300);
};

const rects = (p) => p.evaluate(() => {
  const r = (el) => { if (!el) return null; const b = el.getBoundingClientRect(); return { x: Math.round(b.x * 10) / 10, y: Math.round(b.y * 10) / 10, w: Math.round(b.width * 10) / 10 }; };
  const main = document.querySelector("#fbc-main") || document.querySelector("main");
  const active = document.querySelector(".fbc-sidebar .mud-nav-link.active");
  const acs = active && getComputedStyle(active);
  return {
    // サイドバーの「いまのページ」の強調(計算済みの色・背景)。静的と起動後で同じであること(位置・ピクセル差の閾値では見つからない小さな領域)
    activeStyle: acs ? `${acs.color} | ${acs.backgroundColor}` : null,
    h1: r(document.querySelector("#fbc-main h1, main h1, h1")),
    crumb: r(main && main.querySelector(".breadcrumb, nav ol")),
    para: r(main && main.querySelector("p")),
    h2: r(main && main.querySelector("h2")),
  };
});

const b = await launch();
const rows = [];
for (const [vpName, vp] of [["pc", { width: 1280, height: 800 }], ["sp", { width: 390, height: 844, mobile: true }]]) {
  for (const theme of ["dark", "light"]) {
    for (const path of paths) {
      const name = `${vpName}-${theme}-${path.replace(/[^a-z0-9]+/gi, "_")}`;
      // --- 前(WebAssembly を止める) ---
      const a = await newPage(b, { ...vp, theme, seed: SEED });
      await a.route("**/_framework/blazor.webassembly.js", (r) => r.abort());
      await a.goto(NEW + path, { waitUntil: "load" });
      if (HIDE_OVERLAY) await a.addStyleTag({ content: "#boot-indicator{display:none!important}" });
      await settle(a);
      const shotBefore = await a.screenshot();
      const rectBefore = await rects(a);
      await a.context().close();
      // --- 後(通常の起動)。起動のあいだの動きも記録する ---
      const c = await newPage(b, { ...vp, theme, seed: SEED });
      await c.addInitScript(RECORDER);
      await c.goto(NEW + path, { waitUntil: "load" });
      await c.waitForSelector(".mud-layout", { timeout: 60000 });
      await c.waitForTimeout(600);
      const frames = await c.evaluate(() => window.__frames);
      const cls = await c.evaluate(() => window.__cls);
      const cover = await c.evaluate(() => window.__maxCover || 0);
      await settle(c);
      const shotAfter = await c.screenshot();
      const rectAfter = await rects(c);
      await c.context().close();

      const A = PNG.sync.read(shotBefore), C = PNG.sync.read(shotAfter);
      const w = Math.min(A.width, C.width), h = Math.min(A.height, C.height);
      const crop = (img) => { const o = new PNG({ width: w, height: h }); PNG.bitblt(img, o, 0, 0, w, h, 0, 0); return o; };
      const A2 = crop(A), C2 = crop(C), D = new PNG({ width: w, height: h });
      const diff = pixelmatch(A2.data, C2.data, D.data, w, h, { threshold: 0.1 });
      const sheet = new PNG({ width: w * 3 + 20, height: h, colorType: 6 });
      sheet.data.fill(128);
      PNG.bitblt(A2, sheet, 0, 0, w, h, 0, 0); PNG.bitblt(C2, sheet, 0, 0, w, h, w + 10, 0); PNG.bitblt(D, sheet, 0, 0, w, h, 2 * w + 20, 0);
      writeFileSync(`${out}/${name}.png`, PNG.sync.write(sheet));

      // 本文の位置の差(前 → 後)
      const d = (k) => (rectBefore[k] && rectAfter[k] ? Math.max(Math.abs(rectBefore[k].x - rectAfter[k].x), Math.abs(rectBefore[k].y - rectAfter[k].y)) : null);
      const activeBefore = rectBefore.activeStyle, activeAfter = rectAfter.activeStyle;
      const activeOk = activeBefore === activeAfter;
      const shifts = ["h1", "crumb", "para", "h2"].map(d).filter((v) => v !== null);
      const shift = shifts.length ? Math.max(...shifts) : null;
      // 起動のあいだ(本文が見えているフレームのうち、.mud-layout が出る前後)で、見出しがどれだけ飛んだか
      // 静的 → 起動後への切り替えの 1 フレームでの見出しの移動。mud が出た最初のフレームと、その 1 つ前の差(ウェブフォントの読み込みによる再配置など、起動と関係ない動きは数えない)
      let jump = 0;
      const bootIndex = frames.findIndex((f) => f.mud);
      if (bootIndex > 0) jump = Math.hypot(frames[bootIndex].x - frames[bootIndex - 1].x, frames[bootIndex].y - frames[bootIndex - 1].y);
      const firstVisible = frames.find((f) => f.vis);
      const visibleBeforeBoot = frames.filter((f) => f.vis && !f.mud).length;
      rows.push({ name, activeOk, activeBefore, activeAfter, cover: cover * 100, ratio: (diff / (w * h)) * 100, shift, jump, cls, visibleBeforeBoot, frames: frames.length, firstVisibleMs: firstVisible ? firstVisible.t : null });
    }
  }
}
await b.close();

rows.sort((x, y) => y.ratio - x.ratio);
console.log("ピクセル差(%)  位置の差(px)  起動中の最大ジャンプ(px)  CLS  読み込み表示が覆う最大(%)  本文が見えていたフレーム(起動前)  名前");
for (const r of rows) {
  console.log(`${r.ratio.toFixed(2).padStart(10)}  ${(r.shift ?? NaN).toFixed(1).padStart(12)}  ${r.jump.toFixed(1).padStart(24)}  ${r.cls.toFixed(3)}  ${r.cover.toFixed(1).padStart(8)}  ${String(r.visibleBeforeBoot).padStart(8)}/${r.frames}  ${r.name}`);
}
writeFileSync(`${out}/summary.json`, JSON.stringify(rows, null, 1));
const activeBad = rows.filter((r) => !r.activeOk);
for (const r of activeBad) console.log(`強調の色が違う: ${r.name} 静的「${r.activeBefore}」/ 起動後「${r.activeAfter}」`);
const bad = rows.filter((r) => !r.activeOk || r.ratio > MAX_RATIO || (r.shift ?? 0) > MAX_SHIFT || r.jump > MAX_SHIFT || r.cover > 2);
console.log(bad.length ? `\n閾値超え ${bad.length} / ${rows.length}(ピクセル差 ${MAX_RATIO}% ・位置 ${MAX_SHIFT}px)` : `\n全 ${rows.length} 件が閾値以下(ピクセル差 ${MAX_RATIO}% ・位置 ${MAX_SHIFT}px)`);
process.exit(bad.length ? 1 : 0);
