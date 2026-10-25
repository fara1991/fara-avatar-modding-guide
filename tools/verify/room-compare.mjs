// Phase 3b S6（共同練習の部屋の C# 版。docs/phase3b-design.md の §16）: /practice/room を、現行の room.js（JavaScript 版）と C# 版（?engine=cs）で、
// 同じ台本（もう一人の参加者を生の WebSocket で動かす）に通して比べる。
//   ・A（確かめる人）: 本物のマウス・キーで操作する。B（台本の人）: 生の WebSocket で、固定した文字を送る（worker/index.js の Durable Object と同じ）
//   ・毎回の場面で、A の 3D ビュー＋パネル（#room-main。部屋の URL の欄は、部屋の ID が違うので隠す）の画素を比べる（閾値 MAX_OVER_PCT=0 / MAX_DIFF=8）
//   ・A の DOM（状態・参加者の一覧・追従のボタン）の文字を比べる
//   ・A が送った文字（hello・name・select・lock・unlock・ops。timing で変わる cursor・preview・view は数だけ見る）を、JS と C# で完全に比べる
// 使い方: NEW=http://127.0.0.1:<port>（wrangler dev など。本番には向けないこと）。CHROME_PATH は lib.mjs の既定。OUT=<出力フォルダ>
// 失敗したら終了コード 1。画素の差の場面は OUT に、比べた 2 枚の画像を書く。
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { PNG } from "pngjs";
import { launch, newPage } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const OUT = process.env.OUT || path.join(process.cwd(), "room-compare-out");
const MAX_OVER_PCT = Number(process.env.MAX_OVER_PCT ?? 0);
const MAX_DIFF = Number(process.env.MAX_DIFF ?? 8);
mkdirSync(OUT, { recursive: true });

let failed = 0;
const ok = (cond, label, extra = "") => {
  console.log(`${cond ? "ok  " : "NG  "} ${label}${extra ? "  " + extra : ""}`);
  if (!cond) failed++;
};
const sleep = ms => new Promise(r => setTimeout(r, ms));
const roomUrl = (room, cs) => `${NEW}/practice/room?room=${room}${cs ? "&engine=cs" : ""}`;
const randomRoom = () => "cmp" + Math.random().toString(36).slice(2, 10);

/** 画素の比較: 1 画素で、どれかのチャンネルが MAX_DIFF を超えて違う画素の割合（%）と、最大の差。 */
function compare(a, b) {
  const pa = PNG.sync.read(a), pb = PNG.sync.read(b);
  if (pa.width !== pb.width || pa.height !== pb.height) return { over: 100, max: 255, size: `${pa.width}x${pa.height} / ${pb.width}x${pb.height}` };
  let over = 0, max = 0;
  for (let i = 0; i < pa.data.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 4; c++) d = Math.max(d, Math.abs(pa.data[i + c] - pb.data[i + c]));
    if (d > max) max = d;
    if (d > MAX_DIFF) over++;
  }
  return { over: (over / (pa.width * pa.height)) * 100, max, size: `${pa.width}x${pa.height}` };
}

/** 生の WebSocket の相手（台本）。受け取った文字をためる。 */
class Scripted {
  constructor(url) {
    this.url = url;
    this.msgs = [];
  }
  async open() {
    this.ws = new WebSocket(this.url);
    this.ws.addEventListener("message", e => this.msgs.push(JSON.parse(e.data)));
    await new Promise((resolve, reject) => {
      this.ws.addEventListener("open", resolve, { once: true });
      this.ws.addEventListener("error", reject, { once: true });
    });
    this.send({ t: "hello", name: "Carol" });
    await this.until(m => m.t === "welcome");
  }
  send(m) { this.ws.send(JSON.stringify(m)); }
  async until(pred, ms = 5000) {
    const end = Date.now() + ms;
    while (Date.now() < end) { const m = this.msgs.find(pred); if (m) return m; await sleep(25); }
    return null;
  }
  close() { this.ws.close(1000, "leave"); }
}

/** 1 回の実行（cs = C# 版か）。結果を返す。 */
async function run(browser, cs) {
  const label = cs ? "C#" : "JS";
  const room = randomRoom();
  const page = await newPage(browser, { width: 1280, height: 860, seed: { "avatar-modding-guide.name": "Alice" } });
  // A が送った文字を全部ためる（台本と比べる）
  await page.addInitScript(() => {
    window.__sent = [];
    const send = WebSocket.prototype.send;
    WebSocket.prototype.send = function (text) { window.__sent.push(String(text)); return send.call(this, text); };
  });
  await page.goto(roomUrl(room, cs), { waitUntil: "load" });
  // 背景の 3D（#site-scene）は、動いて毎回違うので隠す（tutorial-compare.mjs と同じ）
  await page.addStyleTag({ content: "#site-scene { display: none !important; }" });
  await page.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 60000 });
  const carol = new Scripted(`${NEW.replace(/^http/, "ws")}/api/rooms/${room}/ws`);
  await carol.open();
  const shots = [];
  const snap = async (name, { wait = 400 } = {}) => {
    await sleep(wait);
    const main = page.locator("#room-main");
    const buf = await main.screenshot({ animations: "disabled", mask: [page.locator("#room-url")] });
    const dom = await page.evaluate(() => ({
      status: document.querySelector("#room-status")?.textContent ?? "",
      peers: [...document.querySelectorAll("#room-peers li")].map(li => li.textContent.replace(/\s+/g, " ").trim()).join(" | "),
      follow: [...document.querySelectorAll("#room-peers [data-follow]")].map(b => b.textContent.trim()).join(","),
    }));
    shots.push({ name, buf, dom });
  };

  await carol.until(m => m.t === "join" && m.peer?.name === "Carol").catch(() => {});
  await page.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 15000 }).catch(() => {});

  // 1. ほかの人が cube0 を選ぶ（枠が付く）
  carol.send({ t: "select", ids: ["cube0"] });
  await snap("remote-select");

  // 2. ほかの人が cube0 を操作中（ロック）。こちらは選べるが、動かせない
  carol.send({ t: "lock", ids: ["cube0"] });
  await carol.until(m => m.t === "locks" && m.locks?.cube0);
  await sleep(300);   // A が、ほかの人のロックを受け取って反映するまで待つ
  await page.locator("#room-viewport").click();   // 中央（立方体）
  await page.keyboard.press("KeyG");
  await snap("remote-locked");
  // 3. 解除
  carol.send({ t: "unlock" });
  await carol.until(m => m.t === "locks" && !m.locks?.cube0);
  await sleep(300);
  await page.keyboard.press("Escape");

  // 4. G で「-」「.」と打つ（NaN で消えないこと）→ Escape でやめる
  await page.keyboard.press("KeyG");
  await page.keyboard.press("Minus");
  await page.keyboard.press("Period");
  await snap("minus-dot");
  await page.keyboard.press("Escape");

  // 5. ほかの人のカーソル（3D の点）
  carol.send({ t: "cursor", p: [1, 1, 0] });
  await snap("remote-cursor", { wait: 500 });

  // 6. ほかの人が球を足す（こちらに届く）
  carol.send({ t: "ops", upsert: [{ id: "sph1", kind: "sphere", name: "Sphere", p: [3, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }], delete: [] });
  await page.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 5000 }).catch(() => {});
  await snap("remote-add");

  // 7. こちらが cube0 を選び、G で動かして確定（lock・ops が送られる）
  await page.locator("#room-viewport").click();   // 中央（立方体）
  await page.keyboard.press("KeyG");
  const box = await page.locator("#room-viewport").boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
  await page.mouse.move(cx, cy);
  await page.mouse.move(cx + 60, cy + 40, { steps: 6 });
  await carol.until(m => m.t === "locks" && m.locks?.cube0, 3000);
  await snap("moving", { wait: 500 });
  await page.mouse.click(cx + 60, cy + 40);
  await carol.until(m => m.t === "ops" && m.upsert?.some(o => o.id === "cube0"), 5000);
  await snap("confirmed");

  // 8. 名前を変える（500 ms のあと送る）
  await page.locator("#room-name").fill("Alicia");
  await sleep(900);

  // 9. ほかの人が視点を送る → 「視点に合わせる」で、その視点に
  carol.send({ t: "view", view: { target: [1, 0, 0], dist: 5, theta: 0.5, phi: 1, ortho: false, name: "ユーザー" } });
  await page.waitForFunction(() => [...document.querySelectorAll("#room-peers [data-follow]")].some(b => b.textContent.includes("視点に合わせる")), null, { timeout: 5000 }).catch(() => {});
  await page.locator("#room-peers [data-follow]").first().click();
  await snap("follow", { wait: 600 });

  // 10. ほかの人が退室（参加者の一覧から消える）
  carol.close();
  await page.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 1, null, { timeout: 8000 }).catch(() => {});
  await snap("peer-left");

  // 送った文字: 台本と比べるものは、操作の順が決まっているもの（cursor・preview・view は timing で数が変わるので数だけ）
  const sent = await page.evaluate(() => window.__sent);
  const semantic = sent.filter(s => ["hello", "name", "select", "lock", "unlock", "ops"].includes(JSON.parse(s).t));
  const counts = sent.reduce((acc, s) => ({ ...acc, [JSON.parse(s).t]: (acc[JSON.parse(s).t] ?? 0) + 1 }), {});
  const errors = page.problems;
  await page.close();
  console.log(`${label}: 送った文字 ${sent.length} 件（種類ごと ${JSON.stringify(counts)}）、ページの問題 ${page.problems.length} 件`);
  return { label, shots, semantic, counts, errors };
}

const browser = await launch();
try {
  const js = await run(browser, false);
  const cs = await run(browser, true);

  // 1) 台本への反応（C# 版が JS 版と同じ文字を送るか）
  ok(JSON.stringify(js.semantic) === JSON.stringify(cs.semantic), "送った文字（hello・name・select・lock・unlock・ops）が JS と C# で完全に一致",
    `${js.semantic.length} 件`);
  for (const k of ["lock", "unlock", "ops", "select"]) ok((js.counts[k] ?? 0) === (cs.counts[k] ?? 0), `${k} の件数が同じ`, `${js.counts[k] ?? 0} / ${cs.counts[k] ?? 0}`);
  ok((cs.counts.cursor ?? 0) > 0 && (cs.counts.view ?? 0) > 0, "C# 版もカーソル・視点を送っている", `cursor ${cs.counts.cursor ?? 0} / view ${cs.counts.view ?? 0}`);

  // 2) 場面ごとの画素と文字
  for (const [i, a] of js.shots.entries()) {
    const b = cs.shots[i];
    const r = compare(a.buf, b.buf);
    const pass = r.over <= MAX_OVER_PCT && r.max <= MAX_DIFF;
    ok(pass, `場面 ${a.name}: 画素（JS と C#）`, `${r.over.toFixed(3)} % / 最大 ${r.max}（閾値 ${MAX_OVER_PCT} % / ${MAX_DIFF}）${r.size}`);
    if (!pass) {
      writeFileSync(path.join(OUT, `${a.name}-js.png`), a.buf);
      writeFileSync(path.join(OUT, `${a.name}-cs.png`), b.buf);
    }
    ok(JSON.stringify(a.dom) === JSON.stringify(b.dom), `場面 ${a.name}: 状態・参加者・追従の文字が一致`, a.dom.status);
  }
  ok(cs.errors.length === 0, "C# 版: ページのエラー・想定外の警告が無い", cs.errors.slice(0, 3).join(" / "));
  ok(js.errors.length === 0, "JS 版: ページのエラー・想定外の警告が無い", js.errors.slice(0, 3).join(" / "));
} finally {
  await browser.close();
}
console.log(failed === 0 ? "すべて ok" : `NG ${failed} 件`);
process.exit(failed === 0 ? 0 : 1);
