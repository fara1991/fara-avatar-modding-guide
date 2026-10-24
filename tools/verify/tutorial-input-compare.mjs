// 記事内チュートリアル(transform)を、Three.js 版と C# 版(?engine=cs)で、同じ「実際の入力」(マウス・キー)で操作して、毎回、画素と文字を比べる(Phase 3b S4)。  node tutorial-input-compare.mjs
//   NEW=http://127.0.0.1:8787  OUT=<画像の出力先>  MAX_OVER_PCT=0  MAX_DIFF=8
// tutorial-compare.mjs は「次へ（スキップ）」で進めるので、手順の skip の処理を通る。ここは、本物のイベントの経路(ポインター・キー → gl.js / viewport.js → 達成の判定 → 次の手順・メッセージ・キーの光り)を通す。
// 操作: 覆いをクリックして始める → 立方体をクリック → G → マウスを動かす → Esc → G Z 2 Enter → R Z 45 Enter → S 0.5 Enter → N → Ctrl+Z。
// 各操作のあとに、文字(進み・吹き出し・メッセージ・キー表示・HUD の文字)と、埋め込みの内側の画素を比べる(23 のスナップショット)。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch } from "./lib.mjs";
import { open, texts, shot, pixelDiff, sleep, realProblems } from "./tutorial-lib.mjs";

const OUT = process.env.OUT || "./tutorial-input-compare-out";
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT || 0);
const MAX_DIFF = Number(process.env.MAX_DIFF || 8);
mkdirSync(OUT, { recursive: true });

// [名前, 操作]。操作は ビューの中央からの相対座標。キーは Playwright の press、文字は type。
const ACTIONS = [
  ["click-cube", ["click", 0, -12]],
  ["g", ["press", "g"]],
  ["move-1", ["move", 40, 10]],
  ["move-2", ["move", 80, 20]],
  ["escape", ["press", "Escape"]],
  ["g-again", ["press", "g"]],
  ["z", ["press", "z"]],
  ["digit-2", ["press", "2"]],
  ["enter-move", ["press", "Enter"]],
  ["r", ["press", "r"]],
  ["z-rot", ["press", "z"]],
  ["digit-4", ["press", "4"]],
  ["digit-5", ["press", "5"]],
  ["enter-rotate", ["press", "Enter"]],
  ["s", ["press", "s"]],
  ["digit-0", ["press", "0"]],
  ["period", ["press", "."]],
  ["digit-5-scale", ["press", "5"]],
  ["enter-scale", ["press", "Enter"]],
  ["n", ["press", "n"]],
  ["ctrl-z", ["press", "Control+z"]],
];

async function act(t, action) {
  const vp = await t.embed.locator(".tut-vp").boundingBox();
  const cx = vp.x + vp.width / 2, cy = vp.y + vp.height / 2;
  const [kind, a, b] = action;
  if (kind === "click") await t.page.mouse.click(cx + a, cy + b);
  else if (kind === "move") await t.page.mouse.move(cx + a, cy + b, { steps: 4 });
  else if (kind === "press") await t.page.keyboard.press(a);
}

const browser = await launch();
let failed = 0, compared = 0;
const a = await open(browser, "transform", false);
const b = await open(browser, "transform", true);
try {
  const snapshot = async (name) => {
    await sleep(900);   // キーの光り(250 ms)・できましたの丸(0.7 秒)が終わり、「できました！」(1.5 秒)がまだ出ている間
    const shotA = PNG.sync.read(await shot(a.page, a.embed));
    const shotB = PNG.sync.read(await shot(b.page, b.embed));
    const ta = await texts(a.embed), tb = await texts(b.embed);
    const textDiff = Object.keys(ta).filter((k) => JSON.stringify(ta[k]) !== JSON.stringify(tb[k]));
    let ok = true, note;
    if (shotA.width !== shotB.width || shotA.height !== shotB.height) { ok = false; note = "大きさが違う"; }
    else {
      const { diff, over, max } = pixelDiff(shotA, shotB);
      note = `差>8: ${over.toFixed(3)}%  最大 ${max}`;
      if (over > MAX_OVER_PCT || max > MAX_DIFF) {
        ok = false;
        const triple = new PNG({ width: shotA.width * 3, height: shotA.height });
        [shotA, shotB, diff].forEach((img, i) => PNG.bitblt(img, triple, 0, 0, img.width, img.height, i * img.width, 0));
        writeFileSync(`${OUT}/${String(compared).padStart(2, "0")}-${name}.png`, PNG.sync.write(triple));
      }
    }
    if (textDiff.length) { ok = false; note += `  文字の差: ${textDiff.map((k) => `${k}[JS=${JSON.stringify(ta[k])} / C#=${JSON.stringify(tb[k])}]`).join(" ")}`; }
    compared++;
    if (!ok) { failed++; console.log(`NG  ${name}: ${note}`); }
    return ta.count;
  };

  await snapshot("initial");
  // 覆いをクリックして始める(フォーカスが入る)
  for (const t of [a, b]) await t.embed.locator(".tut-overlay").click();
  await snapshot("after-overlay-click");
  let last = "";
  for (const [name, action] of ACTIONS) {
    for (const t of [a, b]) await act(t, action);
    last = await snapshot(name);
  }
  console.log(`${failed ? "NG" : "ok"}  transform: 実入力 ${ACTIONS.length} 操作 + 2 = ${compared} スナップショットを比べた(最後の進み: ${last})`);
  const problems = [...realProblems(a.page), ...realProblems(b.page)];
  if (problems.length) { failed++; console.log(`NG  ${problems.join(" | ")}`); }
} finally {
  await a.page.context().close();
  await b.page.context().close();
  await browser.close();
}
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
