// SSR(現行・develop)と WebAssembly 版の見た目の比較。 node shots.mjs <出力フォルダ> [パス...]
// PC 1280x800・スマホ 390x844 × ダーク・ライト。各ページの 3 枚組(SSR | 新 | 差)を作り、差の割合を出す。
import { launch, newPage, csp } from "./lib.mjs";
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import pixelmatch from "pixelmatch";

const SSR = process.env.SSR || "http://127.0.0.1:5000";
const NEW = process.env.NEW || "http://127.0.0.1:8787";
const out = process.argv[2] || "shots";
const paths = process.argv.slice(3).length ? process.argv.slice(3) : [
  "/", "/learn", "/learn/basics", "/learn/basics/transform", "/learn/unity/modular-avatar-outfit", "/shortcuts", "/glossary", "/howto", "/tips", "/qa", "/addons", "/links", "/changelog", "/search?q=blender", "/privacy", "/report", "/practice", "/practice/unity", "/practice/room",
];
mkdirSync(out, { recursive: true });
const b = await launch();
const results = [];
for (const [vpName, vp] of [["pc", { width: 1280, height: 800 }], ["sp", { width: 390, height: 844, mobile: true }]]) {
  for (const theme of ["dark", "light"]) {
    for (const path of paths) {
      const shots = {};
      for (const [kind, base] of [["ssr", SSR], ["new", NEW]]) {
        const p = await newPage(b, { ...vp, theme });
        await p.goto(base + path, { waitUntil: "load" });
        if (kind === "new") await p.waitForSelector(".mud-layout", { timeout: 60000 });
        await p.evaluate(() => document.fonts && document.fonts.ready);
        await p.waitForTimeout(kind === "new" ? 800 : 400);
        // 背景の 3D(bgscene.js)・練習の 3D(WebGL の canvas)は、動いていて(アニメーションの位相が違う)比べられないので隠す。動くものも止めて比べやすくする
        await p.addStyleTag({ content: "canvas { display: none !important; } *, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }" });
        await p.waitForTimeout(100);
        shots[kind] = await p.screenshot();
        if (kind === "new") { const v = await csp(p); if (v.length || p.problems.length) console.log("  !", vpName, theme, path, v, p.problems); }
        await p.context().close();
      }
      const a = PNG.sync.read(shots.ssr), c = PNG.sync.read(shots.new);
      const w = Math.min(a.width, c.width), h = Math.min(a.height, c.height);
      const crop = (img) => { const o = new PNG({ width: w, height: h }); PNG.bitblt(img, o, 0, 0, w, h, 0, 0); return o; };
      const A = crop(a), C = crop(c), D = new PNG({ width: w, height: h });
      const diff = pixelmatch(A.data, C.data, D.data, w, h, { threshold: 0.15 });
      const sheet = new PNG({ width: w * 3 + 20, height: h, colorType: 6 });
      sheet.data.fill(128);
      PNG.bitblt(A, sheet, 0, 0, w, h, 0, 0); PNG.bitblt(C, sheet, 0, 0, w, h, w + 10, 0); PNG.bitblt(D, sheet, 0, 0, w, h, 2 * w + 20, 0);
      const name = `${vpName}-${theme}-${path.replace(/[^a-z0-9]+/gi, "_")}`;
      writeFileSync(`${out}/${name}.png`, PNG.sync.write(sheet));
      results.push({ name, ratio: diff / (w * h) });
    }
  }
}
await b.close();
results.sort((x, y) => y.ratio - x.ratio);
for (const r of results) console.log((r.ratio * 100).toFixed(1).padStart(5) + "%  " + r.name);
