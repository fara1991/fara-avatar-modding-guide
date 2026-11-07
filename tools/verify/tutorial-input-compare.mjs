// 記事内チュートリアルを、Three.js 版と C# 版(?engine=cs)で、同じ「実際の入力」(マウス・キー)で操作して、毎回、画素と文字(と、Unity は状態)を比べる(Phase 3b S4・S5)。
//   node tutorial-input-compare.mjs      NEW=http://127.0.0.1:8787  OUT=<画像の出力先>  MAX_OVER_PCT=0  MAX_DIFF=8  ONLY=<シナリオ名の一部>
//   基準: GOLDEN_MODE=capture で Three.js 版（?engine=js）の各スナップショット（画像・文字・Unity の状態）を tests/visual/golden/tutorial-input-compare に保存する。既定の compare は、保存した基準と C# 版を比べる（golden.mjs）
// tutorial-compare.mjs は「次へ（スキップ）」で進めるので、手順の skip の処理を通る。ここは、本物のイベントの経路(ポインター・キー → gl.js / viewport.js / unity.js → 達成の判定 → 次の手順・メッセージ・キーの光り)を通す。
// 操作の列(シナリオごと):
//   transform    … 覆いをクリックして始める → 立方体をクリック → G → マウスを動かす → Esc → G Z 2 Enter → R Z 45 Enter → S 0.5 Enter → N → Ctrl+Z(23 のスナップショット)
//   unity-basics … ▶ → Body → F → Alt+ドラッグ → ホイール 2 回 → 中ボタンのドラッグ → Avatar → W → 矢印のドラッグ → Position X に 0 → E → R → Q(途中で、ヒントが出る誤った操作も)
//   ma-outfit    … Hoodie を Scene に落とす(ヒント)→ Ctrl+Z → Avatar へドラッグ → 右クリック → 別の行をクリック(失敗 → 最初からやり直し)→ 右クリック → Setup Outfit → Shirt_Default → 無効 → Tag →
//                   Hoodie_Mesh → マテリアルをドラッグ → Add Component → VRChat SDK → Control Panel → Build & Test
// 各操作のあとに、文字(進み・吹き出し・メッセージ・キー表示・HUD の文字)と、埋め込みの内側の画素(と Unity は状態)を比べる。
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch } from "./lib.mjs";
import { open, texts, shot, pixelDiff, sleep, realProblems, UNITY } from "./tutorial-lib.mjs";
import { drag, dragTo, handlePoint, stateOf, stateDiff } from "./unity-lib.mjs";
import { GOLDEN_MODE, loadGolden, saveGolden, assertCaptureTarget } from "./golden.mjs";
const GROUP = "tutorial-input-compare";
const capture = GOLDEN_MODE === "capture";

const OUT = process.env.OUT || "./tutorial-input-compare-out";
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT || 0);
const MAX_DIFF = Number(process.env.MAX_DIFF || 8);
mkdirSync(OUT, { recursive: true });

// ---------------------------------------------------------------- transform(Blender の 3D ビュー)
// [名前, 操作]。操作は ビューの中央からの相対座標。キーは Playwright の press。
const TRANSFORM = [
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
].map(([name, [kind, a, b]]) => [name, async (t) => {
  const vp = await t.embed.locator(".tut-vp").boundingBox();
  const cx = vp.x + vp.width / 2, cy = vp.y + vp.height / 2;
  if (kind === "click") await t.page.mouse.click(cx + a, cy + b);
  else if (kind === "move") await t.page.mouse.move(cx + a, cy + b, { steps: 4 });
  else if (kind === "press") await t.page.keyboard.press(a);
}]);

// ---------------------------------------------------------------- Unity 風の画面
const row = (name) => `.ue-row[data-node-name="${name}"]`;
const sceneAt = async (t, x, y) => { const b = await t.embed.locator(".ue-scene-view").boundingBox(); return [b.x + x, b.y + y]; };
const inEmbed = (t, s) => t.embed.locator(s);
const sel = (name, s) => `.practice-embed[data-scenario="${name}"] ${s}`;

const UNITY_BASICS = [
  ["expand-avatar", async (t) => inEmbed(t, `${row("Avatar")} .ue-fold`).click()],
  ["select-wrong-row", async (t) => inEmbed(t, row("Shirt_Default")).click()],   // 違う行(ヒントが出る)
  ["select-body", async (t) => inEmbed(t, row("Body")).click()],
  ["key-f", async (t) => { const [x, y] = await sceneAt(t, 200, 200); await t.page.mouse.move(x, y); await t.page.keyboard.press("f"); }],
  ["alt-orbit", async (t) => { const [x, y] = await sceneAt(t, 200, 200); await t.page.keyboard.down("Alt"); await drag(t.page, [x, y], [x + 40, y - 20]); await t.page.keyboard.up("Alt"); }],
  ["wheel-1", async (t) => { const [x, y] = await sceneAt(t, 200, 200); await t.page.mouse.move(x, y); await t.embed.locator(".tut-vp").evaluate((el) => el.focus({ preventScroll: true })); await t.page.mouse.wheel(0, 200); await sleep(100); }],
  ["wheel-2", async (t) => { const [x, y] = await sceneAt(t, 200, 200); await t.page.mouse.move(x, y); await t.page.mouse.wheel(0, 200); await sleep(100); }],
  ["middle-pan", async (t) => { const [x, y] = await sceneAt(t, 200, 200); await drag(t.page, [x, y], [x + 30, y + 20], 4, "middle"); }],
  ["select-avatar", async (t) => inEmbed(t, row("Avatar")).click()],
  ["key-w", async (t) => t.page.keyboard.press("w")],
  ["scene-click-empty", async (t) => { const [x, y] = await sceneAt(t, 40, 300); await t.page.mouse.click(x, y); }],   // 何もない所(選択が外れる)
  ["select-avatar-again", async (t) => inEmbed(t, row("Avatar")).click()],
  ["drag-red-arrow", async (t, ctx) => { await sleep(120); const h = await ctx.handle(0); const [x, y] = await sceneAt(t, h.x, h.y); await drag(t.page, [x, y], [x + 25, y - 6]); }],
  ["edit-wrong", async (t) => { await inEmbed(t, '[data-field="p"][data-axis="0"]').fill("0.25"); await t.page.keyboard.press("Enter"); }],
  ["edit-zero", async (t) => { await inEmbed(t, '[data-field="p"][data-axis="0"]').fill("0"); await t.page.keyboard.press("Enter"); await inEmbed(t, ".ue-name").click(); await t.embed.locator(".tut-vp").evaluate((el) => el.focus({ preventScroll: true })); }],
  ["key-e", async (t) => t.page.keyboard.press("e")],
  ["key-r", async (t) => t.page.keyboard.press("r")],
  ["key-q", async (t) => t.page.keyboard.press("q")],
];

const MA_OUTFIT = (name) => [
  ["drop-on-scene", async (t) => dragTo(t.page, sel(name, '.ue-asset[data-asset="Hoodie"]'), sel(name, ".ue-scene-view"))],   // ルートに置かれた(ヒント)
  ["undo-wrong", async (t) => { const [x, y] = await sceneAt(t, 40, 300); await t.page.mouse.click(x, y); await t.page.keyboard.press("Control+z"); }],
  ["drag-hoodie-onto-avatar", async (t) => dragTo(t.page, sel(name, '.ue-asset[data-asset="Hoodie"]'), sel(name, row("Avatar")))],
  ["context-hoodie", async (t) => inEmbed(t, row("Hoodie")).click({ button: "right" })],
  ["click-other-row", async (t) => inEmbed(t, row("Shirt_Default")).click()],   // メニューが閉じる(失敗 → 最初からやり直し)
  ["wait-replay", async () => sleep(700)],
  ["context-hoodie-again", async (t) => inEmbed(t, row("Hoodie")).click({ button: "right" })],
  ["setup-outfit", async (t) => inEmbed(t, '.ue-popup [data-ctx="setup-outfit"]').click()],
  ["select-shirt", async (t) => inEmbed(t, row("Shirt_Default")).click()],
  ["inactive-shirt", async (t) => inEmbed(t, ".ue-active").uncheck()],
  ["tag-wrong", async (t) => inEmbed(t, ".ue-tag").selectOption("Player")],
  ["tag-editoronly", async (t) => inEmbed(t, ".ue-tag").selectOption("EditorOnly")],
  ["expand-hoodie", async (t) => inEmbed(t, `${row("Hoodie")} .ue-fold`).click()],
  ["select-hoodie-mesh", async (t) => inEmbed(t, row("Hoodie_Mesh")).click()],
  ["drag-material", async (t) => dragTo(t.page, sel(name, '.ue-asset[data-asset="Hoodie_lilToon"]'), sel(name, '[data-slot="0"]'))],
  ["add-component-open", async (t) => inEmbed(t, ".ue-addcomp").click()],
  ["add-component-search", async (t) => inEmbed(t, ".ue-addsearch").fill("blend")],
  ["add-component-pick", async (t) => inEmbed(t, '[data-add="MABlendshapeSync"]').click()],
  ["menu-sdk", async (t) => inEmbed(t, '[data-menu="VRChat SDK"]').click()],
  ["menu-sdk-panel", async (t) => inEmbed(t, '[data-menuitem="sdk-panel"]').click()],
  // 押してから 0.9 秒で表示が変わる(ビルド中 → 完了)ので、ビルドが終わって、プレビューが消えるまで待つ
  ["build-test", async (t) => { await inEmbed(t, '[data-sdk="test"]').click(); await sleep(300); await t.page.waitForFunction((s) => { const pv = document.querySelector(s); return pv && pv.hidden; }, sel(name, ".ue-preview"), { timeout: 15000 }); await sleep(100); }],
];

const SCENARIOS = {
  transform: TRANSFORM,
  "unity-basics": UNITY_BASICS,
  "ma-outfit": MA_OUTFIT("ma-outfit"),
};

if (GOLDEN_MODE === "capture") await assertCaptureTarget(process.env.NEW || "http://127.0.0.1:8787");   // 基準を作る前に、three.js の版が配られているかを確かめる
const browser = await launch();
let failed = 0, compared = 0;
const only = process.env.ONLY;
for (const [name, actions] of Object.entries(SCENARIOS)) {
  if (only && !name.includes(only)) continue;
  const unity = UNITY.includes(name);
  const a = capture ? await open(browser, name, false) : null;   // 基準（three.js の版）
  const b = await open(browser, name, true);
  let snap = 0;
  try {
    const snapshot = async (label) => {
      await sleep(900);   // キーの光り(250 ms)・できましたの丸(0.7 秒)が終わり、「できました！」(1.5 秒)がまだ出ている間
      for (const t of [a, b].filter(Boolean)) await t.page.mouse.move(2, 2);
      // 「できました！」の吹き出し(1.5 秒で消える)と丸・吹き出しのアニメーション(約 1 秒)が、両方で終わってから撮る(途中で撮ると、始まった時刻のずれで画素が違う)
      for (const t of [a, b].filter(Boolean)) await t.embed.evaluate((el) => new Promise((res) => { const tick = () => (!el.querySelector(".tut-msg.is-ok") && el.getAnimations({ subtree: true }).length === 0 ? res() : setTimeout(tick, 50)); tick(); setTimeout(res, 4000); }));
      await sleep(200);
      const key = `${name}-${String(snap++).padStart(2, "0")}-${label}`;
      const tb = await texts(b.embed);
      const shotB = PNG.sync.read(await shot(b.page, b.embed));
      const sb = unity ? await stateOf(b.page, true) : null;
      let shotA, ta, sa;
      if (a) {
        const buf = await shot(a.page, a.embed);
        ta = await texts(a.embed);
        sa = unity ? await stateOf(a.page, false) : null;
        saveGolden(GROUP, key, "png", buf);
        saveGolden(GROUP, key, "json", JSON.stringify({ texts: ta, state: sa }, null, 1));
        shotA = PNG.sync.read(buf);
      } else {
        shotA = PNG.sync.read(loadGolden(GROUP, key, "png"));
        const g = JSON.parse(loadGolden(GROUP, key, "json"));
        ta = g.texts; sa = g.state;
      }
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
          writeFileSync(`${OUT}/${key}.png`, PNG.sync.write(triple));
        }
      }
      if (textDiff.length) { ok = false; note += `  文字の差: ${textDiff.map((k) => `${k}[基準=${JSON.stringify(ta[k])} / C#=${JSON.stringify(tb[k])}]`).join(" ")}`; }
      if (unity) {
        const sd = stateDiff(sa, sb);
        if (sd.length) { ok = false; note += "  状態の違い " + sd.join(" | "); }
      }
      compared++;
      if (!ok) { failed++; console.log(`NG  ${name} / ${label}: ${note}`); } else if (process.env.VERBOSE) console.log(`ok  ${name} / ${label}: ${note}  [${tb.count}]`);
      return tb.count;
    };

    await snapshot("initial");
    // 覆いをクリックして始める（フォーカスが入る）
    for (const t of [a, b].filter(Boolean)) await t.embed.locator(".tut-overlay").click();
    await snapshot("after-overlay-click");
    let last = "";
    // ギズモの位置は JS 版（window.__unity）から求める。基準の作成では求めて保存し、比べるときは保存したものを読む（C# 版には window.__unity が無い）
    let curLabel = "";
    const ctx = { handle: async (i) => { const key = `${name}-handle-${curLabel}-${i}`; if (a) { const h = await handlePoint(a.page, i); saveGolden(GROUP, key, "json", JSON.stringify(h)); return h; } return JSON.parse(loadGolden(GROUP, key, "json")); } };
    for (const [label, action] of actions) {
      curLabel = label;
      if (a) await action(a, ctx);   // 基準の版でも、同じ入力を入れる（基準の画像は、ここで撮ったもの）
      await action(b, ctx);
      last = await snapshot(label);
    }
    console.log(`${failed ? "NG" : "ok"}  ${name}: 実入力 ${actions.length} 操作 + 2 = ${actions.length + 2} スナップショットを比べた(最後の進み: ${last})`);
    const problems = [...(a ? realProblems(a.page) : []), ...realProblems(b.page)];
    if (problems.length) { failed++; console.log(`NG  ${name}: ${problems.join(" | ")}`); }
  } finally {
    if (a) await a.page.context().close();
    await b.page.context().close();
  }
}
await browser.close();
console.log(failed ? `\n${failed} 件 NG(比べた ${compared} 件)` : `\nすべて ok(比べた ${compared} 件)`);
process.exit(failed ? 1 : 0);
