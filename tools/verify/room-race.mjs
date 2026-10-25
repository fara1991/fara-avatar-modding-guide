// Phase 3b S6 のレビュー M1（共有ルームの C# 版で、読み込み中に部屋を替える）: Practice の wasm の読み込みを遅らせ、
// 読み込みの途中で、部屋 A から部屋 B へ画面内で移る。A の開始が遅れて終わっても、残るのは B だけであること
//   ・開いている WebSocket は、B の 1 本だけ（A の接続が残っていない）
//   ・#room-viewport の canvas は 1 つ（二つの WebGL が重ならない）
//   ・B の状態（接続しました）と参加者が見える
// 使い方: NEW=http://127.0.0.1:<port>（wrangler dev など）。失敗したら終了コード 1。
import { launch, newPage } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const DELAY_MS = Number(process.env.DELAY_MS || 4000);
let failed = 0;
const ok = (cond, label, extra = "") => {
  console.log(`${cond ? "ok  " : "NG  "} ${label}${extra ? "  " + extra : ""}`);
  if (!cond) failed++;
};
const sleep = ms => new Promise(r => setTimeout(r, ms));

const browser = await launch();
try {
  const page = await newPage(browser, { seed: { "avatar-modding-guide.name": "Race" } });
  // 開いた WebSocket を、いま開いているものとして数える（閉じたら外す）
  await page.addInitScript(() => {
    window.__openSockets = new Map();
    const WS = window.WebSocket;
    window.WebSocket = class extends WS {
      constructor(url, ...rest) {
        super(url, ...rest);
        window.__openSockets.set(this, url);
        this.addEventListener("close", () => window.__openSockets.delete(this));
      }
    };
  });
  // Practice（3D の本体）の読み込みを遅らせる。ここが遅いあいだに、部屋を替える
  await page.route(/FaraAvatarModdingGuide\.Practice[^/]*\.wasm/, async route => {
    await sleep(DELAY_MS);
    await route.continue();
  });

  const roomA = "raca" + Math.random().toString(36).slice(2, 8);
  const roomB = "racb" + Math.random().toString(36).slice(2, 8);
  await page.goto(`${NEW}/practice/room?room=${roomA}&engine=cs`, { waitUntil: "load" });
  await sleep(800);   // A の 3D の読み込みが始まったころ
  await page.evaluate(id => window.Blazor.navigateTo(`/practice/room?room=${id}&engine=cs`), roomB);
  await page.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 60000 });
  // A の読み込みが終わり、A の開始が後から届くまで待つ
  await sleep(DELAY_MS + 3000);

  const sockets = await page.evaluate(() => [...window.__openSockets.values()]);
  ok(sockets.length === 1, "開いている WebSocket は 1 本だけ（B）", JSON.stringify(sockets.map(u => u.split("/api/")[1])));
  ok(sockets.length === 1 && sockets[0].includes(roomB), "その 1 本は部屋 B のもの");
  const canvases = await page.locator("#room-viewport canvas").count();
  ok(canvases === 1, "3D の canvas は 1 つ", `canvas ${canvases}`);
  ok((await page.locator("#room-status").textContent()).includes("接続しました"), "B の状態は「接続しました」のまま");
  ok(page.problems.filter(p => !/maintenance/.test(p)).length === 0, "ページのエラー・想定外の警告が無い", page.problems.slice(0, 3).join(" / "));
} finally {
  await browser.close();
}
console.log(failed === 0 ? "すべて ok" : `NG ${failed} 件`);
process.exit(failed === 0 ? 0 : 1);
