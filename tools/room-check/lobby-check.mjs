// 練習の部屋の一覧・パスワード・Close・自動終了・作成の指定の実動作の検査（wrangler dev などの Worker に対して、2 人分の WebSocket で確かめる）。
// 部屋は常に一覧に出る（名前は Worker がランダムに決める）。パスワードは作成のとき 40 字の英数字（クライアントが作る形）。
//   node tools/room-check/lobby-check.mjs http://localhost:8787            … 一覧・鍵・Close・閉じた ID・作成の指定の無い入室・作成者の鍵の免除・入室の時間
//   node tools/room-check/lobby-check.mjs http://localhost:8787 60         … 上に加えて、自動終了（ROOM_IDLE_SECONDS=60 で起動した Worker に対して。60 秒ほど待つ）
// 確認用の Worker を立てるときは、wrangler dev --env dev --var ROOM_IDLE_SECONDS:60 のように、同じ秒数で起動する。
// 失敗が 1 つでもあれば終了コードが 1 になる。
// 注意: 入室の時間は、ローカル（workerd）で測ったもの。本番の Workers の CPU 時間とは一致しない。
import { randomBytes } from "node:crypto";

const base = process.argv[2] ?? "http://localhost:8787";
const idleSeconds = process.argv[3] ? Number(process.argv[3]) : null;
const wsBase = base.replace(/^http/, "ws");
const failures = [];
const ok = (cond, label) => {
  console.log(`${cond ? "ok  " : "NG  "} ${label}`);
  if (!cond) failures.push(label);
};
const rid = () => "lobby" + randomBytes(6).toString("hex");           // 10 字以上の小文字の英数字
const hexKey = () => randomBytes(16).toString("hex");                  // 作成者の鍵（16 進 32 字）
const passKey = () => randomBytes(20).toString("hex");                  // 部屋のパスワード（英数字 40 字。クライアントは C# の乱数で作る）
const ROOM_NAME = /^[0-9a-hjkmnp-tvwxyz]{8}$/;                          // 部屋の名前（Worker が作る。i・l・o・u は使わない）
const sleep = ms => new Promise(r => setTimeout(r, ms));

/** 接続 1 本。受け取ったメッセージは msgs に溜まり、close の code と reason は closed に入る。 */
function connect(room, body = {}) {
  const ws = new WebSocket(`${wsBase}/api/rooms/${room}/ws`);
  const c = { ws, msgs: [], closed: null, welcome: null, at: null };
  c.send = m => ws.send(JSON.stringify(m));
  ws.addEventListener("message", e => {
    const m = JSON.parse(String(e.data));
    c.msgs.push(m);
    if (m.t === "welcome") { c.welcome = m; c.welcomeAt = Date.now(); }
  });
  c.opened = new Promise((resolve, reject) => {
    ws.addEventListener("open", () => { c.helloAt = Date.now(); c.send({ t: "hello", ...body }); resolve(); });
    ws.addEventListener("error", () => reject(new Error("ws error")));
  });
  c.done = new Promise(resolve => ws.addEventListener("close", e => { c.closed = { code: e.code, reason: e.reason }; resolve(); }));
  return c;
}

/** welcome か close のどちらかまで待つ（最大 ms）。 */
async function settle(c, ms = 4000) {
  const start = Date.now();
  while (!c.welcome && c.closed === null && Date.now() - start < ms) await sleep(50);
  await sleep(100);
  return c;
}

async function listRooms() {
  const res = await fetch(`${base}/api/rooms`);
  return { status: res.status, cache: res.headers.get("cache-control"), body: await res.json() };
}

const owner = hexKey();
const room = rid();
const pass = passKey();

// 1. 作成（部屋は常に一覧に出る。名前は Worker が決める。クライアントの名前は使わない）
const a = connect(room, { name: "Alice", owner, create: { name: "クライアントの名前は無視", pass } });
await a.opened;
await settle(a);
ok(a.welcome?.you.owner === true, "作成者は owner: true で入る");
ok(ROOM_NAME.test(a.welcome?.room.name ?? "") && a.welcome?.room.locked === true, "welcome に、Worker が決めた名前（8 字）と鍵つき（locked）が出る");

// 2. 一覧（作成者が入ったあと）
let list = await listRooms();
ok(list.status === 200 && list.cache === "no-store", "GET /api/rooms は 200 で no-store");
const row = list.body.rooms.find(r => r.id === room);
ok(row && row.name === a.welcome?.room.name && row.count === 1 && row.max === 8 && row.locked === true, "一覧に、名前・人数 1/8・鍵つきで出る（名前は Worker が決めた値）");
ok(!("owner" in (row ?? {})) && !("pass" in (row ?? {})), "一覧には作成者の鍵や鍵の情報を出さない");

// 3. 鍵が無い・違う（中身は届かない）
const nokey = connect(room, { name: "Bob" });
await nokey.opened;
await settle(nokey);
ok(nokey.msgs.length === 1 && nokey.msgs[0].t === "needpass" && nokey.closed?.code === 4003, "鍵が無ければ needpass のあと 4003（中身は届かない）");
const wrong = connect(room, { name: "Bob", pass: "まちがい" });
await wrong.opened;
await settle(wrong);
ok(wrong.msgs.length === 1 && wrong.msgs[0].t === "wrongpass" && wrong.closed?.code === 4003, "違う鍵は wrongpass のあと 4003");

// 4. 正しい鍵で入る。作成者ではない（入室の時間も測る）
const b = connect(room, { name: "Bob", pass });
await b.opened;
await settle(b);
ok(b.welcome && b.welcome.you.owner === false, "正しい鍵で welcome。作成者ではない");
ok(b.welcome?.peers.some(p => p.name === "Alice"), "B の welcome に、先に入った A が出る");
const lockedMs = b.welcomeAt - b.helloAt;

// 5. 作成者ではない人は閉じられない
b.send({ t: "close" });
await sleep(300);
ok(b.msgs.at(-1)?.t === "error" && b.msgs.at(-1)?.msg === "not owner", "作成者でない人の close は not owner（閉じない）");
await sleep(5200);   // 一覧は Worker が 5 秒覚える（listCacheMs）
list = await listRooms();
ok(list.body.rooms.find(r => r.id === room)?.count === 2, "一覧の人数が 2 になる");

// 6. 作成者が再接続すると、鍵なしで入れる（免除）。同じ作成者の鍵
a.ws.close();
await sleep(200);
const a2 = connect(room, { name: "Alice", owner });
await a2.opened;
await settle(a2);
ok(a2.welcome?.you.owner === true, "作成者の鍵が合えば、鍵なしで入れる（免除）");

// 7. 作成者が閉じる
a2.send({ t: "close" });
await Promise.all([a2.done, b.done]);
ok(a2.msgs.at(-1)?.t === "closed" && a2.msgs.at(-1)?.reason === "owner" && a2.closed?.code === 4004, "作成者が閉じると、作成者も closed（owner）で 4004");
ok(b.msgs.at(-1)?.t === "closed" && b.msgs.at(-1)?.reason === "owner" && b.closed?.code === 4004, "ほかの人も、closed（owner）で 4004 に切断される");
await sleep(5200);
list = await listRooms();
ok(!list.body.rooms.some(r => r.id === room), "閉じた部屋は一覧から外れる");

// 8. 閉じた ID は、入室も作り直しも拒否（目印の期限内）
const late = connect(room, { name: "Carol", pass });
await late.opened;
await settle(late);
ok(late.msgs.at(-1)?.t === "closed" && late.closed?.code === 4004, "閉じた ID には入れない（closed・4004）");
const recreate = connect(room, { name: "Dan", owner: hexKey(), create: { pass } });
await recreate.opened;
await settle(recreate);
ok(recreate.msgs.at(-1)?.t === "closed" && !recreate.welcome, "閉じた ID は、作成の指定があっても作り直せない");

// 9. 作成の指定の無い入室は、部屋が無ければ作らない（古い URL）
const orphan = rid();
const noCreate = connect(orphan, { name: "Eve" });
await noCreate.opened;
await settle(noCreate);
ok(noCreate.msgs.length === 1 && noCreate.msgs[0].t === "closed" && noCreate.msgs[0].reason === "missing", "作成の指定が無く、部屋も無い入室は、部屋を作らずに閉じる（missing）");
const orphanAgain = connect(orphan, { name: "Eve", owner: hexKey(), create: { pass: passKey() } });
await orphanAgain.opened;
await settle(orphanAgain);
ok(orphanAgain.welcome?.you.owner === true, "作成の指定がある最初の入室なら、同じ ID でも部屋を作る");

// 10. 入室の時間（鍵つき。ローカルの計測）・旧形式の部屋は作れない（鍵が無い作成は bad create）
const timedRoom = rid();
const timedPass = passKey();
const t0 = connect(timedRoom, { name: "Timer", owner: hexKey(), create: { pass: timedPass } });
await t0.opened;
await settle(t0);
const plain = connect(timedRoom, { name: "Plain", pass: timedPass });
await plain.opened;
await settle(plain);
const plainMs = plain.welcomeAt - plain.helloAt;
console.log(`      （入室の時間（hello → welcome）: 鍵つき ${plainMs} ms。ローカル）`);
ok(Number.isFinite(lockedMs) && Number.isFinite(plainMs), "入室の時間を計った");
const noPass = connect(rid(), { name: "NoPass", owner: hexKey(), create: {} });
await noPass.opened;
await settle(noPass);
ok(noPass.msgs.at(-1)?.t === "error" && noPass.msgs.at(-1)?.msg === "bad create" && !noPass.welcome, "鍵の無い作成は bad create（部屋は作らない）");

// 11. 自動終了（ROOM_IDLE_SECONDS の Worker に対してだけ）
if (idleSeconds !== null) {
  const idleRoom = rid();
  const c = connect(idleRoom, { name: "Dave", owner: hexKey(), create: { pass: passKey() } });
  await c.opened;
  await settle(c);
  const start = Date.now();
  // 操作が無いまま待つ。ping だけ送る（ping は操作に数えない）。警告が出るまで、ping を続ける
  const warnAt = idleSeconds - Math.min(120, Math.floor(idleSeconds / 2));
  while (!c.msgs.some(m => m.t === "idlewarn") && c.closed === null && Date.now() - start < (idleSeconds + 20) * 1000) {
    c.send({ t: "ping" });
    await sleep(2000);
  }
  const warned = c.msgs.find(m => m.t === "idlewarn");
  ok(warned && typeof warned.ms === "number" && Date.now() - start >= (warnAt - 5) * 1000, `ping だけでは、寿命の前（約 ${warnAt} 秒）に警告が出る（ms ${warned?.ms}）`);
  while (c.closed === null && Date.now() - start < (idleSeconds + 20) * 1000) {
    c.send({ t: "ping" });
    await sleep(2000);
  }
  ok(c.msgs.at(-1)?.t === "closed" && c.msgs.at(-1)?.reason === "idle" && c.closed?.code === 4004, `操作が無いまま ${idleSeconds} 秒で idle の閉じ（4004）`);
  console.log(`      （idle の閉じまで ${Math.round((Date.now() - start) / 1000)} 秒）`);
}

console.log(failures.length === 0 ? "\n全部 ok" : `\n失敗 ${failures.length} 件: ${failures.join(" / ")}`);
process.exit(failures.length === 0 ? 0 : 1);
