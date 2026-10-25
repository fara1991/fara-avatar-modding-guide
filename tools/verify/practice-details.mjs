// /practice の Three.js 版と C# 版(?engine=cs)で、画素に出にくい「見た目・操作の細部」を同じ操作で並べて、値で比べる(Phase 3b S4)。  node practice-details.mjs
//   NEW=http://127.0.0.1:8787  ONLY=<場面名の一部>
// 比べるもの: ビューの中央の下にある要素(クラス)・その要素のマウスカーソル・フォーカスのある要素・ビューの枠(outline・影)・ビューのクラス・
//   ボックス選択の枠(.practice-box の位置・大きさ)・メニュー(位置・大きさ・中身)・HUD の文字(モード・視点・ステータス・数値入力・統計・マーカー)・ツールの点灯・N パネルの入力値。
// 操作の途中(ドラッグ中・数値入力中・メニューを開いたまま)も比べる。値が違えば、その項目を出す。
import { launch } from "./lib.mjs";
import { GOLDEN_MODE, loadGolden, saveGolden } from "./golden.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const SCENES = [
  { name: "initial", steps: [] },
  { name: "hover-cube", steps: [["move", 0, -12]] },
  { name: "focused-click-empty", steps: [["click", 300, 120]] },
  { name: "select-cube", steps: [["click", 0, -12]] },
  { name: "g-moving", steps: [["click", 0, -12], ["key", "g"], ["move", 60, 10]] },
  { name: "g-axis-number", steps: [["click", 0, -12], ["key", "g"], ["key", "x"], ["key", "1"], ["key", "."], ["key", "5"]] },
  { name: "r-moving", steps: [["click", 0, -12], ["key", "r"], ["move", 80, -30]] },
  { name: "s-moving", steps: [["click", 0, -12], ["key", "s"], ["move", 90, 20]] },
  { name: "g-cancel", steps: [["click", 0, -12], ["key", "g"], ["move", 60, 10], ["key", "Escape"]] },
  { name: "add-menu", steps: [["key", "Shift+A"]] },
  { name: "add-menu-near-corner", steps: [["move", 300, 160], ["key", "Shift+A"]] },
  { name: "add-menu-hover", steps: [["key", "Shift+A"], ["moveto", 0, 0]] },
  { name: "delete-menu", steps: [["click", 0, -12], ["key", "x"]] },
  { name: "edit-face", steps: [["click", 0, -12], ["key", "Tab"], ["key", "3"], ["click", 0, -40]] },
  { name: "edit-merge-menu", steps: [["click", 0, -12], ["key", "Tab"], ["key", "a"], ["key", "m"]] },
  { name: "box-select-midway", steps: [["click", 0, -12], ["key", "Tab"], ["dragStart", -120, -90, 40, 30]] },
  { name: "box-select-done", steps: [["click", 0, -12], ["key", "Tab"], ["dragStart", -120, -90, 120, 90], ["dragEnd"]] },
  { name: "orbit-midway", steps: [["middleStart", 0, 0, 60, 20]] },
  { name: "orbit-done", steps: [["middleStart", 0, 0, 60, 20], ["middleEnd"]] },
  { name: "loopcut", steps: [["click", 0, -12], ["key", "Tab"], ["key", "Control+r"], ["move", 20, 10]] },
  { name: "npanel", steps: [["click", 0, -12], ["key", "n"]] },
  { name: "numpad-ortho", steps: [["click", 0, -12], ["key", "Numpad1"]] },
  { name: "tool-xray-stats", steps: [["click", 0, -12], ["key", "Tab"], ["tool", "xray"], ["tool", "stats"]] },
  { name: "undo", steps: [["click", 0, -12], ["key", "g"], ["key", "z"], ["key", "2"], ["key", "Enter"], ["key", "Control+z"]] },
  { name: "blur-after-focus", steps: [["click", 300, 120], ["blur"]] },
];

async function open(browser, url) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.problems = [];
  page.on("console", (m) => { if (["error", "warning"].includes(m.type())) page.problems.push(m.text().slice(0, 200)); });
  page.on("pageerror", (e) => page.problems.push("pageerror " + e.message.slice(0, 200)));
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto(NEW + url);
  await page.waitForSelector(".practice-viewport canvas", { timeout: 90000 });
  await sleep(800);
  return { ctx, page };
}

async function run(page, steps) {
  const box = await page.locator("#practice-viewport").boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
  for (const [kind, a, b, c, d] of steps) {
    if (kind === "click") await page.mouse.click(cx + a, cy + b);
    else if (kind === "move") await page.mouse.move(cx + a, cy + b, { steps: 4 });
    else if (kind === "moveto") await page.mouse.move(cx + a, cy + b);
    else if (kind === "key") await page.keyboard.press(a);
    else if (kind === "tool") await page.locator(`[data-tool="${a}"]`).click();
    else if (kind === "dragStart") { await page.mouse.move(cx + a, cy + b); await page.mouse.down(); await page.mouse.move(cx + c, cy + d, { steps: 6 }); }
    else if (kind === "dragEnd") await page.mouse.up();
    else if (kind === "middleStart") { await page.mouse.move(cx + a, cy + b); await page.mouse.down({ button: "middle" }); await page.mouse.move(cx + c, cy + d, { steps: 6 }); }
    else if (kind === "middleEnd") await page.mouse.up({ button: "middle" });
    else if (kind === "blur") await page.evaluate(() => document.activeElement?.blur());
    await sleep(150);
  }
  await sleep(400);
}

/** 画面に出ている細部を、両エンジンで同じ形の値にして取り出す */
async function collect(page) {
  const box = await page.locator("#practice-viewport").boundingBox();
  const probes = [[0, 0], [0, -12], [-250, -150], [250, 150]];
  return page.evaluate(([bx, by, bw, bh, probes]) => {
    const root = document.getElementById("practice-viewport");
    const rr = root.getBoundingClientRect();
    const r2 = (n) => Math.round(n * 100) / 100;
    const rect = (el) => { if (!el) return null; const b = el.getBoundingClientRect(); return [r2(b.x - rr.x), r2(b.y - rr.y), r2(b.width), r2(b.height)]; };
    const q = (s) => root.querySelector(s);
    const text = (s) => { const e = q(s); return e && !e.hidden ? e.innerText.trim() : null; };
    const cs = getComputedStyle(root);
    const under = probes.map(([dx, dy]) => {
      const e = document.elementFromPoint(bx + bw / 2 + dx, by + bh / 2 + dy);
      return e ? { cls: (e.tagName + "." + [...e.classList].join(".")).replace(/\.$/, ""), cursor: getComputedStyle(e).cursor } : null;
    });
    const a = document.activeElement;
    const box = q(".practice-box");
    const menu = q(".practice-menu");
    return {
      under,
      active: a ? (a === root ? "root" : a.tagName + "." + [...a.classList].join(".")) : null,
      rootClasses: [...root.classList].sort().join(" "),
      rootOutline: [cs.outlineStyle, cs.outlineWidth, cs.outlineColor, cs.outlineOffset].join(" "),
      rootShadow: cs.boxShadow,
      rootBorder: [cs.borderTopWidth, cs.borderTopColor].join(" "),
      tabindex: root.getAttribute("tabindex"),
      box: box ? { rect: rect(box), display: getComputedStyle(box).display } : null,
      menu: menu ? { rect: rect(menu), text: menu.innerText.trim(), style: menu.getAttribute("style") } : null,
      mode: text(".vp-mode"), view: text(".vp-view"), status: text(".vp-status"), numeric: text(".vp-numeric"), stats: text(".vp-stats"), marker: text(".vp-marker"),
      toolsOn: [...root.querySelectorAll(".vp-tools .is-on")].map((b) => b.dataset.tool).join(","),
      toolsHl: [...root.querySelectorAll(".vp-tools .is-highlight")].map((b) => b.dataset.tool).join(","),
      toolsDisabled: [...root.querySelectorAll(".vp-tools [disabled]")].map((b) => b.dataset.tool).join(","),
      npanel: [...root.querySelectorAll(".vp-npanel input, .vp-npanel select")].map((i) => i.value).join("|"),
      npanelVisible: !!q(".vp-npanel") && !q(".vp-npanel").hidden && getComputedStyle(q(".vp-npanel")).display !== "none",
    };
  }, [box.x, box.y, box.width, box.height, probes]);
}

function diffKeys(a, b) {
  const out = [];
  for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) if (JSON.stringify(a[k]) !== JSON.stringify(b[k])) out.push(`${k}[JS=${JSON.stringify(a[k])} / C#=${JSON.stringify(b[k])}]`);
  return out;
}

const browser = await launch();
let failed = 0;
const only = process.env.ONLY;
const GROUP = "practice-details";
for (const s of SCENES) {
  if (only && !s.name.includes(only)) continue;
  const a = GOLDEN_MODE === "capture" ? await open(browser, "/practice?engine=js") : null;   // 基準の作成は、three.js の版（?engine=js）で
  const b = await open(browser, "/practice?engine=cs");
  try {
    if (a) await run(a.page, s.steps);
    await run(b.page, s.steps);
    const da = a ? await collect(a.page) : JSON.parse(loadGolden(GROUP, s.name, "json"));
    if (a) saveGolden(GROUP, s.name, "json", JSON.stringify(da, null, 1));
    const db = await collect(b.page);
    const diffs = diffKeys(da, db);
    const problems = [...(a ? a.page.problems : []), ...b.page.problems];
    if (diffs.length || problems.length) { failed++; console.log(`NG  ${s.name}: ${[...diffs, ...problems].join("  ")}`); } else console.log(`ok  ${s.name}`);
  } finally {
    if (a) await a.ctx.close();
    await b.ctx.close();
  }
}
await browser.close();
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
