// 共同練習の部屋（worker/index.js の Durable Object）を、WebSocket で直接つないで確かめる。
// 使い方（Node.js 22 以上。外部パッケージ不要）:
//   npx wrangler dev                       … 書き出した dist/ と worker/ を http://localhost:8787 で動かす
//   node tools/room-check/check.mjs http://localhost:8787
// 新しい部屋を作り、同期・遅れて入った人への全体の送信・ロック・入力の検査・人数の上限を確かめる。本番には向けないこと。
const base = process.argv[2] ?? "http://localhost:8787";
const room = "check" + Math.random().toString(36).slice(2, 10);
const wsUrl = `${base.replace(/^http/, "ws")}/api/rooms/${room}/ws`;
const sleep = ms => new Promise(r => setTimeout(r, ms));

let failed = 0;
function ok(cond, label) {
  console.log(`${cond ? "ok  " : "NG  "} ${label}`);
  if (!cond) failed++;
}

/** 1 人分の接続。受け取ったメッセージをためておく */
function client(name) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    const c = { ws, msgs: [], welcome: null, closed: null };
    c.send = m => ws.send(typeof m === "string" ? m : JSON.stringify(m));
    c.last = t => [...c.msgs].reverse().find(m => m.t === t);
    c.wait = async (pred, ms = 2000) => {
      const end = Date.now() + ms;
      while (Date.now() < end) { const m = c.msgs.find(pred); if (m) return m; await sleep(20); }
      return null;
    };
    ws.addEventListener("message", e => {
      const m = JSON.parse(e.data);
      c.msgs.push(m);
      if (m.t === "welcome") { c.welcome = m; resolve(c); }
    });
    ws.addEventListener("open", () => { if (name !== null) c.send({ t: "hello", name }); else resolve(c); });
    ws.addEventListener("close", e => { c.closed = e.code; resolve(c); });
    ws.addEventListener("error", reject);
  });
}

const cube = id => ({ id, kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] });

const a = await client("Alice");
ok(a.welcome?.objects.length === 1 && a.welcome.objects[0].id === "cube0", "新しい部屋には立方体が 1 つある");
const b = await client("Bob<script>\u0007");
ok(b.welcome?.you.name === "Bobscript", "表示名から制御文字と HTML の特殊文字を取り除く（クライアントでも文字として表示）");
ok(!!(await a.wait(m => m.t === "join" && m.peer.name === "Bobscript")), "参加をほかの人に知らせる");

// 追加・変形・削除
a.send({ t: "ops", upsert: [{ ...cube("s1"), kind: "sphere", name: "Sphere", p: [3, 0, 0] }], delete: [] });
ok(!!(await b.wait(m => m.t === "ops" && m.upsert?.some(o => o.id === "s1"))), "追加がほかの人に届く");
b.send({ t: "ops", upsert: [{ ...cube("cube0"), p: [0, 0, 2] }], delete: [] });
ok(!!(await a.wait(m => m.t === "ops" && m.upsert?.some(o => o.id === "cube0" && o.p[2] === 2))), "変形がほかの人に届く");
a.send({ t: "ops", upsert: [{ ...cube("n1"), name: "<img src=x onerror=alert(1)>" }], delete: [] });
const named = await b.wait(m => m.t === "ops" && m.upsert?.some(o => o.id === "n1"));
ok(named?.upsert.find(o => o.id === "n1").name === "img src=x onerror=alert(1)", "オブジェクト名から HTML の特殊文字を取り除く");
a.send({ t: "lock", ids: ["s1"] });
await b.wait(m => m.t === "locks" && m.locks.s1);
const mark = b.msgs.length;
a.send({ t: "ops", upsert: [], delete: ["s1", "n1"] });
ok(!!(await b.wait(m => m.t === "ops" && m.delete?.includes("s1"))), "削除がほかの人に届く");
ok(!!(await b.wait(m => m.t === "locks" && !m.locks.s1 && b.msgs.indexOf(m) >= mark)), "削除したもののロックが外れたことをほかの人に知らせる");

// 遅れて入った人にはシーン全体
const c = await client("Carol");
ok(c.welcome?.objects.length === 1 && c.welcome.objects[0].p[2] === 2 && c.welcome.peers.length === 2, "遅れて入った人に、今のシーンと参加者が届く");

// ロック
a.send({ t: "lock", ids: ["cube0"] });
await b.wait(m => m.t === "locks" && m.locks.cube0);
b.send({ t: "lock", ids: ["cube0"], seq: 1 });
ok(!!(await b.wait(m => m.t === "lockdenied")), "ほかの人がロック中のものはロックできない");
b.send({ t: "ops", upsert: [{ ...cube("cube0"), p: [9, 9, 9] }], delete: [] });
const rej = await b.wait(m => m.t === "ops" && m.rejected);
ok(rej && rej.upsert[0].p[0] === 0, "ロック中のものへの変更は反映されず、正しい状態が返る");
a.send({ t: "preview", objs: [{ id: "cube0", p: [1, 0, 2], r: [0, 0, 0], s: [1, 1, 1] }] });
ok(!!(await b.wait(m => m.t === "preview")), "操作中の変形（プレビュー）が届く");
a.send({ t: "unlock" });
ok(!!(await b.wait(m => m.t === "locks" && !m.locks.cube0)), "ロックを外すとほかの人に知らせる");

// 入力の検査
const errs = () => b.msgs.filter(m => m.t === "error").length;
let before = errs();
b.send({ t: "ops", upsert: [{ ...cube("x1"), p: [1e9, 0, 0] }], delete: [] });
b.send({ t: "ops", upsert: [{ ...cube("x2"), kind: "teapot" }], delete: [] });
b.send({ t: "ops", upsert: [{ ...cube("BAD ID") }], delete: [] });
b.send({ t: "ops", upsert: [{ ...cube("x3"), s: [0, 1, 1] }], delete: [] });
b.send({ t: "cursor", p: ["a", 0, 0] });
b.send({ t: "nope" });
b.send("not json");
b.send("x".repeat(20000));
b.send(JSON.stringify({ t: "name", name: "あ".repeat(6000) })); // 文字数は 16K 未満でも UTF-8 では 16KB を超える
await sleep(300);
ok(errs() - before === 9, "範囲外・不明な種類・不正な ID・0 のスケール・不正なカーソル・不明な型・JSON 以外・大きすぎるメッセージ（バイト数）を拒否");
ok(!a.msgs.some(m => m.t === "ops" && m.upsert?.some(o => /^x/.test(o.id))), "拒否したものはほかの人に届かない");

// オブジェクト数の上限（200）
const many = Array.from({ length: 205 }, (_, i) => cube(`m${i}`));
b.send({ t: "ops", upsert: many.slice(0, 150), delete: [] });
b.send({ t: "ops", upsert: many.slice(150), delete: [] });
await sleep(500);
const d = await client("Dave");
ok(d.welcome?.objects.length === 200, `オブジェクトは 200 個まで（${d.welcome?.objects.length}）`);

// 頻度の制限
before = b.msgs.filter(m => m.t === "error" && m.msg === "rate limited").length;
for (let i = 0; i < 200; i++) b.send({ t: "cursor", p: [0, 0, 0] });
await sleep(300);
ok(b.msgs.filter(m => m.t === "error" && m.msg === "rate limited").length > before, "送りすぎると制限される");

// 人数の上限（8 人）
const extra = [];
for (let i = 0; i < 4; i++) extra.push(await client(`P${i}`)); // これで 8 人
const ninth = await client("Ninth");
ok(ninth.closed === 4001 && !ninth.welcome, "9 人目は満員として切断される");

// 退出
for (const x of [...extra, c, d]) x.ws.close();
b.ws.close();
ok(!!(await a.wait(m => m.t === "leave")), "退出をほかの人に知らせる");
a.ws.close();

console.log(failed ? `\n${failed} 件失敗` : "\nすべて成功");
process.exit(failed ? 1 : 0);
