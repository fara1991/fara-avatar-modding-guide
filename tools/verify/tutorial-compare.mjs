// 記事内チュートリアルの Three.js 版(tutorial.js)と C# 版(?engine=cs)を、同じ操作で並べて、埋め込み全体(題・進み・吹き出し・3D + HUD・キー表示・ボタン)の画素と文字を比べる(Phase 3b S4)。  node tutorial-compare.mjs
//   NEW=http://127.0.0.1:8787  OUT=<画像の出力先>  ONLY=<シナリオ名の一部>  MAX_OVER_PCT=0  MAX_DIFF=8(差>8 の画素が 0 であること。S4 で、原因をすべて直したので厳しくした)  STEPS=<各シナリオで比べる手順の間隔。既定 1 = 全手順>
// 各シナリオ(Blender の 9 本)で、「次へ（スキップ）」を 1 手順ずつ押しながら、毎回、両方のスクリーンショットと文字(カウント・吹き出し・キー表示・HUD の文字・メッセージ)を比べる。最後は完了の画面。
// スキップは両エンジンの skip の処理(JS は scenarios.js、C# は TutorialScenarios)を通るので、再生した状態の見た目の一致も確かめる。
// 案内の点線の点滅は時計で決まるので、両方の performance.now を止める(同じ位相になる)。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch, newPage } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const OUT = process.env.OUT || "./tutorial-compare-out";
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT || 0);
const MAX_DIFF = Number(process.env.MAX_DIFF || 8);
const STEP = Number(process.env.STEPS || 1);
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const ARTICLES = {
  "navigation-mouse": "/learn/getting-started/navigation",
  "navigation-numpad": "/learn/getting-started/navigation",
  select: "/learn/basics/select",
  transform: "/learn/basics/transform",
  "edit-mode": "/learn/modeling/edit-mode",
  "extrude-inset-loopcut": "/learn/modeling/extrude-inset-loopcut",
  "bevel-merge": "/learn/modeling/bevel-knife-merge",
  mirror: "/learn/modeling/mirror-smooth",
  topology: "/learn/modeling/topology",
};

async function open(browser, name, cs) {
  const page = await newPage(browser, { width: 1280, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });   // CSS のアニメーション(マーカーの上下など)は、始まった時刻で位相が決まるので止める(同じ CSS なので、止めても比べる意味は変わらない)
  await page.context().addInitScript(() => { performance.now = () => 1000; });   // 点線の点滅を止める
  await page.addInitScript(() => { const st = document.createElement("style"); st.textContent = "#site-scene{display:none !important}"; document.addEventListener("DOMContentLoaded", () => document.head.appendChild(st)); });   // 背景の 3D(動く)が、半透明の部分に透けるので隠す
  await page.goto(`${NEW}${ARTICLES[name]}${cs ? "?engine=cs" : ""}`, { waitUntil: "load" });
  await page.waitForSelector(".mud-layout", { timeout: 90000 });
  const embed = page.locator(`.practice-embed[data-scenario="${name}"]`);
  await embed.waitFor({ timeout: 30000 });
  await page.waitForSelector(`.practice-embed[data-scenario="${name}"].is-ready`, { timeout: 30000 });
  await embed.scrollIntoViewIfNeeded();
  await embed.locator("canvas").first().waitFor({ timeout: 60000 });
  await sleep(1200);
  return { page, embed };
}

async function texts(embed) {
  return embed.evaluate((el) => {
    const t = (sel) => el.querySelector(sel)?.innerText?.trim() ?? null;
    return {
      count: t(".tut-count"), bubble: t(".tut-bubble .tut-text"), keys: el.querySelector(".tut-keys")?.hidden ? null : t(".tut-keys"),
      msg: el.querySelector(".tut-msg")?.hidden ? null : t(".tut-msg"), mode: t(".vp-mode"), view: t(".vp-view"), status: t(".vp-status"), numeric: t(".vp-numeric"),
      stats: el.querySelector(".vp-stats")?.hidden ? null : t(".vp-stats"), complete: el.querySelector(".tut-complete")?.hidden ? null : t(".tut-complete"),
      overlayHidden: el.querySelector(".tut-overlay")?.hidden, marker: el.querySelector(".vp-marker")?.hidden ? null : t(".vp-marker"),
      highlightedTools: [...el.querySelectorAll(".vp-tools .is-highlight")].map((b) => b.dataset.tool), viewHighlight: !!el.querySelector(".vp-view.is-highlight"),
      focused: el.classList.contains("is-focused"), toolsOn: [...el.querySelectorAll(".vp-tools .is-on")].map((b) => b.dataset.tool),
      progress: el.querySelector(".tut-progress span")?.style.width,
    };
  });
}

/** 埋め込みの内側(枠から 10 ピクセル内側)のスクリーンショット。外側は、ページの背景(動く)が透けるので含めない */
async function shot(page, embed) {
  const box = await embed.boundingBox();
  const inset = 10;
  return page.screenshot({ clip: { x: box.x + inset, y: box.y + inset, width: box.width - inset * 2, height: box.height - inset * 2 } });
}

function pixelDiff(a, b) {
  const diff = new PNG({ width: a.width, height: a.height });
  let over = 0, max = 0;
  for (let i = 0; i < a.data.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(a.data[i + c] - b.data[i + c]));
    max = Math.max(max, d);
    if (d > 8) over++;
    diff.data[i] = diff.data[i + 1] = diff.data[i + 2] = Math.min(255, d * 8); diff.data[i + 3] = 255;
  }
  return { diff, over: (over / (a.width * a.height)) * 100, max };
}

const browser = await launch();
let failed = 0, compared = 0;
const only = process.env.ONLY;
for (const name of Object.keys(ARTICLES)) {
  if (only && !name.includes(only)) continue;
  let a, b;
  try {
    a = await open(browser, name, false);
    b = await open(browser, name, true);
    const n = Number((await a.embed.locator(".tut-count").textContent()).split("/")[1]);
    let lastLine = "";
    for (let k = 0; k <= n; k++) {
      if (k % STEP === 0 || k === n) {
        for (const p of [a.page, b.page]) await p.mouse.move(2, 2);
        await sleep(350);
        const shotA = PNG.sync.read(await shot(a.page, a.embed));
        const shotB = PNG.sync.read(await shot(b.page, b.embed));
        const ta = await texts(a.embed), tb = await texts(b.embed);
        const textDiff = Object.keys(ta).filter((key) => JSON.stringify(ta[key]) !== JSON.stringify(tb[key]));
        let ok = true, note = "";
        if (shotA.width !== shotB.width || shotA.height !== shotB.height) { ok = false; note = `大きさが違う ${shotA.width}x${shotA.height} / ${shotB.width}x${shotB.height}`; }
        else {
          const { diff, over, max } = pixelDiff(shotA, shotB);
          note = `差>8: ${over.toFixed(3)}%  最大 ${max}`;
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
    const problems = [...a.page.problems, ...b.page.problems].filter((m) => !/Failed to load resource/.test(m));
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
