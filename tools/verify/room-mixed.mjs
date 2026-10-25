// Phase 3b S6（共同練習の部屋）: 同じ部屋に、現行の room.js（JavaScript 版）と C# 版（?engine=cs）の二人を入れて、互いに届くことを確かめる（通信の文字は変えていないこと）。
//   A = JavaScript 版、B = C# 版。A が立方体を動かす → B に届く、B が動かす → A に届く。毎回、二人の 3D ビューの画素を比べる（0 % / 8）。
// 使い方: NEW=http://127.0.0.1:<port>（wrangler dev など）。失敗したら終了コード 1。
import path from "node:path";
import { mkdirSync, writeFileSync } from "node:fs";
import { PNG } from "pngjs";
import { launch, newPage } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const OUT = process.env.OUT || path.join(process.cwd(), "room-mixed-out");
mkdirSync(OUT, { recursive: true });
let failed = 0;
const ok = (cond, label, extra = "") => {
  console.log(`${cond ? "ok  " : "NG  "} ${label}${extra ? "  " + extra : ""}`);
  if (!cond) failed++;
};
const sleep = ms => new Promise(r => setTimeout(r, ms));

function diff(a, b) {
  const pa = PNG.sync.read(a), pb = PNG.sync.read(b);
  if (pa.width !== pb.width || pa.height !== pb.height) return { over: 100, max: 255 };
  let over = 0, max = 0;
  for (let i = 0; i < pa.data.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 4; c++) d = Math.max(d, Math.abs(pa.data[i + c] - pb.data[i + c]));
    max = Math.max(max, d);
    if (d > 8) over++;
  }
  return { over: (over / (pa.width * pa.height)) * 100, max };
}

const browser = await launch();
try {
  const room = "mix" + Math.random().toString(36).slice(2, 9);
  const a = await newPage(browser, { seed: { "avatar-modding-guide.name": "Jsu" } });
  await a.goto(`${NEW}/practice/room?room=${room}`, { waitUntil: "load" });
  await a.addStyleTag({ content: "#site-scene { display: none !important; }" });
  await a.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 60000 });
  const b = await newPage(browser, { seed: { "avatar-modding-guide.name": "Csu" } });
  await b.goto(`${NEW}/practice/room?room=${room}&engine=cs`, { waitUntil: "load" });
  await b.addStyleTag({ content: "#site-scene { display: none !important; }" });
  await b.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 60000 });
  await a.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 15000 }).catch(() => {});
  await b.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 15000 }).catch(() => {});
  const names = async p => (await p.locator("#room-peers .peer-name").allTextContents()).map(t => t.replace("（あなた）", "").trim()).sort();
  ok(JSON.stringify(await names(a)) === JSON.stringify(await names(b)), "二人の参加者の一覧が一致（JS 版と C# 版）", JSON.stringify(await names(a)));

  // 3D ビューの上のほう（ステータスの行を除く）。ほかの人のカーソル（.vp-remote）と、自分の状態の文字は、人によって違うので隠す
  const view = async (p, name) => {
    await sleep(500);
    await p.evaluate(() => document.activeElement?.blur());   // フォーカスの枠は人によって違う（A だけ押したあとは付く）
    const box = await p.locator("#room-viewport").boundingBox();
    const buf = await p.screenshot({ animations: "disabled", clip: { x: Math.round(box.x), y: Math.round(box.y), width: Math.round(box.width), height: Math.round(box.height) - 36 }, mask: [p.locator(".vp-remote")] });
    writeFileSync(path.join(OUT, `${name}.png`), buf);
    return buf;
  };
  const moveCube = async (p, dx, dy) => {
    const box = await p.locator("#room-viewport").boundingBox();
    const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
    await p.locator("#room-viewport").click();   // 立方体を選ぶ
    await p.keyboard.press("KeyG");
    await p.mouse.move(cx, cy);
    await p.mouse.move(cx + dx, cy + dy, { steps: 6 });
    await p.mouse.click(cx + dx, cy + dy);
    await p.keyboard.press("Alt+KeyA");   // 選択を外す（自分の選択の枠は色が違うので、比べる前に消す）
    await sleep(600);
  };

  // A（JS）が動かす → B（C#）に届く
  await moveCube(a, 70, 30);
  const r1 = diff(await view(a, "a-after-a"), await view(b, "b-after-a"));
  ok(r1.over === 0 && r1.max <= 8, "A（JS）が動かした立方体が、B（C#）の画面でも同じ位置・同じ見た目", `${r1.over.toFixed(3)} % / 最大 ${r1.max}`);

  // B（C#）が動かす → A（JS）に届く
  await moveCube(b, -40, -60);
  const r2 = diff(await view(a, "a-after-b"), await view(b, "b-after-b"));
  ok(r2.over === 0 && r2.max <= 8, "B（C#）が動かした立方体が、A（JS）の画面でも同じ位置・同じ見た目", `${r2.over.toFixed(3)} % / 最大 ${r2.max}`);

  const noisy = p => p.filter(x => !/maintenance/.test(x));
  ok(noisy(a.problems).length === 0 && noisy(b.problems).length === 0, "二人のページに、想定外のエラー・警告が無い", `${noisy(a.problems).length} / ${noisy(b.problems).length}`);
} finally {
  await browser.close();
}
console.log(failed === 0 ? "すべて ok" : `NG ${failed} 件`);
process.exit(failed === 0 ? 0 : 1);
