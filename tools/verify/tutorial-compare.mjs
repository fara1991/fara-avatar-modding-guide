// 記事内チュートリアルの Three.js 版(tutorial.js)と C# 版(?engine=cs)を、同じ操作で並べて、埋め込み全体(題・進み・吹き出し・3D + HUD・キー表示・ボタン)の画素と文字を比べる(Phase 3b S4)。  node tutorial-compare.mjs
//   GPU=1(実機の GPU で描く。lib.mjs)  GPU_TOLERANT=1(実 GPU 用: Unity の案内の枠の中の画素の差を無視。通常の SwiftShader の厳しさは変えない)  MOTION=1(動きを減らす設定にしない)
//   NEW=http://127.0.0.1:8787  OUT=<画像の出力先>  ONLY=<シナリオ名の一部>  MAX_OVER_PCT=0  MAX_DIFF=8(差>8 の画素が 0 であること。S4 で、原因をすべて直したので厳しくした)  STEPS=<各シナリオで比べる手順の間隔。既定 1 = 全手順>
// 各シナリオ(Blender の 9 本)で、「次へ（スキップ）」を 1 手順ずつ押しながら、毎回、両方のスクリーンショットと文字(カウント・吹き出し・キー表示・HUD の文字・メッセージ)を比べる。最後は完了の画面。
// スキップは両エンジンの skip の処理(JS は scenarios.js、C# は TutorialScenarios)を通るので、再生した状態の見た目の一致も確かめる。
// 案内の点線の点滅は時計で決まるので、両方の performance.now を止める(同じ位相になる)。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch } from "./lib.mjs";
import { ARTICLES, UNITY, open, texts, shot, pixelDiff, sleep, realProblems, rendererOf, GPU_TOLERANT, guideRects, settleBuild } from "./tutorial-lib.mjs";
const OUT = process.env.OUT || "./tutorial-compare-out";
// GPU_TOLERANT=1(実 GPU・DPR が 1 でないとき用)は、閾値の既定を 0.2 % / 40 に緩める(通常の SwiftShader・DPR 1 は 0 % / 8 のまま)。MAX_OVER_PCT・MAX_DIFF を指定すればそれが優先
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT ?? (GPU_TOLERANT ? 0.2 : 0));
const MAX_DIFF = Number(process.env.MAX_DIFF ?? (GPU_TOLERANT ? 40 : 8));
const STEP = Number(process.env.STEPS || 1);
mkdirSync(OUT, { recursive: true });

const browser = await launch();
let failed = 0, compared = 0, warned = false;
const only = process.env.ONLY;
for (const name of Object.keys(ARTICLES)) {
  if (only && !name.includes(only)) continue;
  let a, b;
  try {
    a = await open(browser, name, false);
    b = await open(browser, name, true);
    if (!warned) {
      warned = true;
      const r = await rendererOf(a.page);
      const software = /SwiftShader|llvmpipe|Software/i.test(r);
      console.log(`描画: ${r}${software ? "" : "  ← 実 GPU。厳しい比較(0 % / 8)は SwiftShader が前提。実 GPU では、案内の枠のある Unity の手順で、文字のアンチエイリアスが違い NG になることがある(既知。README・設計書 §15.5)。GPU_TOLERANT=1 で、枠の中だけ画素の差を無視する(DOM の文字は比べる)"}`);
    }
    const n = Number((await a.embed.locator(".tut-count").textContent()).split("/")[1]);
    let lastLine = "";
    for (let k = 0; k <= n; k++) {
      if (k % STEP === 0 || k === n) {
        for (const p of [a.page, b.page]) await p.mouse.move(2, 2);
        await sleep(350);
        if (UNITY.includes(name)) await Promise.all([settleBuild(a.embed), settleBuild(b.embed)]);
        const shotA = PNG.sync.read(await shot(a.page, a.embed));
        const shotB = PNG.sync.read(await shot(b.page, b.embed));
        const ta = await texts(a.embed), tb = await texts(b.embed);
        const textDiff = Object.keys(ta).filter((key) => JSON.stringify(ta[key]) !== JSON.stringify(tb[key]));
        let ok = true, note = "";
        if (shotA.width !== shotB.width || shotA.height !== shotB.height) { ok = false; note = `大きさが違う ${shotA.width}x${shotA.height} / ${shotB.width}x${shotB.height}`; }
        else {
          const { diff, over, max, ignored } = pixelDiff(shotA, shotB, GPU_TOLERANT && UNITY.includes(name) ? await guideRects(a.embed) : []);
          note = `差>8: ${over.toFixed(3)}%  最大 ${max}${ignored ? `  (GPU_TOLERANT: 案内の枠の中の ${ignored} 画素は無視)` : ""}`;
          if (over > MAX_OVER_PCT || max > MAX_DIFF) ok = false;
          if (!ok || process.env.ALL_IMAGES) {
            const triple = new PNG({ width: shotA.width * 3, height: shotA.height });
            [shotA, shotB, diff].forEach((img, i) => PNG.bitblt(img, triple, 0, 0, img.width, img.height, i * img.width, 0));
            writeFileSync(`${OUT}/${name}-${String(k).padStart(2, "0")}.png`, PNG.sync.write(triple));
          }
        }
        if (textDiff.length) { ok = false; note += `  文字の差: ${textDiff.map((key) => `${key}[JS=${JSON.stringify(ta[key])} / C#=${JSON.stringify(tb[key])}]`).join(" ")}`; }
        compared++;
        if (!ok) { failed++; console.log(`NG  ${name} 手順 ${k}/${n}: ${note}`); }
        lastLine = `${name}: ${k}/${n} まで比べた(${note})`;
      }
      if (k === 0) {
        // 最初の画面(覆いが乗っている)を比べたら、覆いをクリックして始め、両方で同じ位置(ビューの中央)にマウスを動かす。
        // viewport.js は、最後のマウス位置をクライアント座標で覚えるので、マウスが一度も入っていないと、メニューの位置がページのスクロール位置に左右される(C# 版は中央にする)。
        // 覆いのあいだはマウスがビューに届かないので、始めたあとに動かす
        for (const t of [a, b]) {
          await t.embed.locator(".tut-overlay").click();
          const vp = await t.embed.locator(".tut-vp").boundingBox();
          await t.page.mouse.move(vp.x + vp.width / 2, vp.y + vp.height / 2);
        }
        await sleep(300);
      }
      if (k < n) {
        await a.embed.locator('button:has-text("次へ（スキップ）")').click();
        await b.embed.locator('button:has-text("次へ（スキップ）")').click();
        await sleep(250);
      }
    }
    const problems = [...realProblems(a.page), ...realProblems(b.page)];
    if (problems.length) { failed++; console.log(`NG  ${name}: ${problems.join(" | ")}`); }
    console.log(`${problems.length ? "NG" : "ok"}  ${lastLine}`);
  } finally {
    await a?.page.context().close();
    await b?.page.context().close();
  }
}
await browser.close();
console.log(failed ? `\n${failed} 件 NG(比べた ${compared} 件)` : `\nすべて ok(比べた ${compared} 件)`);
process.exit(failed ? 1 : 0);
