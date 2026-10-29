// worker/index.js の入口（fetch）のテスト。  node --test worker/index.test.mjs
// index.js は Cloudflare の部品（cloudflare:workers）を読み込むので、Node では、その名前だけを空の実装に差し替えて読み込む。
// 確かめること: メンテナンスの状態の API・503・報告の受け口・静的ファイルの素通し（HTML を書き換えない）・知らない /api/* は 404。
import { test, before } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";
import { nextJstMidnight } from "../external/fara-blazor-component/worker/report-rate-limit.js";

let worker, Room, RoomDirectory, parseCreate, cleanName, LIMITS, netKey, randomRoomName, roomCreateKeyOf, isLegacyRoom, perMinuteOf, perDayOf, maxTotalOf;

before(async () => {
  // "cloudflare:workers" を、DurableObject だけを持つ空のモジュールにする
  const hooks = `
    export async function resolve(specifier, context, next) {
      if (specifier === "cloudflare:workers") return { url: "data:text/javascript,export class DurableObject { constructor(ctx, env) { this.ctx = ctx; this.env = env; } }", shortCircuit: true };
      return next(specifier, context);
    }`;
  register("data:text/javascript," + encodeURIComponent(hooks), import.meta.url);
  // Durable Object の自動応答（ping → pong）の型も、Node には無いので空の実装にする
  globalThis.WebSocketRequestResponsePair ??= class { constructor(request, response) { this.request = request; this.response = response; } };
  const mod = await import("./index.js");
  worker = mod.default;
  Room = mod.Room;
  RoomDirectory = mod.RoomDirectory;
  netKey = mod.netKey;
  parseCreate = mod.parseCreate;
  randomRoomName = mod.randomRoomName;
  roomCreateKeyOf = mod.roomCreateKeyOf;
  isLegacyRoom = mod.isLegacyRoom;
  perMinuteOf = mod.perMinuteOf;
  perDayOf = mod.perDayOf;
  maxTotalOf = mod.maxTotalOf;
  cleanName = mod.cleanName;
  LIMITS = mod.LIMITS;
});

/** 静的ファイル（ASSETS）の代わり。呼ばれたパスを記録し、HTML をそのまま返す */
function assets(body = "<!doctype html><html><body><div id=\"app\">静的ページ</div></body></html>") {
  const calls = [];
  return { calls, fetch: async (request) => { calls.push(new URL(request.url).pathname); return new Response(body, { headers: { "Content-Type": "text/html; charset=utf-8" } }); } };
}

const call = (path, env, init) => worker.fetch(new Request(`https://avatar-dev.example${path}`, init), env);

test("/api/maintenance: 通常は off・空の文、キャッシュさせない", async () => {
  const res = await call("/api/maintenance", { ASSETS: assets() });
  assert.equal(res.status, 200);
  assert.match(res.headers.get("Content-Type"), /application\/json/);
  assert.equal(res.headers.get("Cache-Control"), "no-store");
  assert.deepEqual(await res.json(), { state: "off", message: "" });
});

test("/api/maintenance: MAINTENANCE_MODE=notice で、お知らせの状態と文を返す", async () => {
  const res = await call("/api/maintenance", { ASSETS: assets(), MAINTENANCE_MODE: "notice", MAINTENANCE_MESSAGE: "10月25日20:00ごろ、サイトを更新します" });
  assert.deepEqual(await res.json(), { state: "notice", message: "10月25日20:00ごろ、サイトを更新します" });
});

test("MAINTENANCE_MODE=true: すべてのリクエストが 503（静的ファイルは呼ばない）", async () => {
  const a = assets();
  for (const path of ["/", "/learn/basics", "/api/maintenance", "/api/report/config"]) {
    const res = await call(path, { ASSETS: a, MAINTENANCE_MODE: "true" });
    assert.equal(res.status, 503, path);
  }
  assert.deepEqual(a.calls, []);
});

test("旧名の MAINTENANCE だけが設定されている Worker は、通常動作（読まない。フォールバックなし）", async () => {
  const a = assets();
  const res = await call("/learn/basics", { ASSETS: a, MAINTENANCE: "true" });
  assert.equal(res.status, 200);
  assert.deepEqual(await (await call("/api/maintenance", { ASSETS: assets(), MAINTENANCE: "notice" })).json(), { state: "off", message: "" });
});

test("お知らせ（notice）のときも、静的ファイルの HTML は書き換えない（帯は WebAssembly が /api/maintenance を読んで出す）", async () => {
  const a = assets();
  const res = await call("/learn/basics", { ASSETS: a, MAINTENANCE_MODE: "notice", MAINTENANCE_MESSAGE: "更新します" });
  assert.equal(res.status, 200);
  const html = await res.text();
  assert.ok(!html.includes("maintenance-notice") && !html.includes("fbc-notice") && !html.includes("更新します"));
  assert.ok(html.includes("静的ページ"));
  assert.deepEqual(a.calls, ["/learn/basics"]);
});

test("静的ファイルはそのまま返す（ステータスもヘッダーも）", async () => {
  const env = { ASSETS: { fetch: async () => new Response("nf", { status: 404, headers: { "X-Test": "1" } }) } };
  const res = await call("/no-such-page", env);
  assert.equal(res.status, 404);
  assert.equal(res.headers.get("X-Test"), "1");
});

test("知らない /api/* は 404（静的ファイルに回さない）", async () => {
  const a = assets();
  const res = await call("/api/unknown", { ASSETS: a });
  assert.equal(res.status, 404);
  assert.deepEqual(a.calls, []);
});

test("/api/report/config: 報告の受け口（kit の report.js）につながっている。鍵が無ければ enabled:false", async () => {
  const res = await call("/api/report/config", { ASSETS: assets() });
  assert.equal(res.status, 200);
  const body = await res.json();
  assert.equal(body.enabled, false);
  assert.equal(body.siteKey, null);
  const configured = await call("/api/report/config", { ASSETS: assets(), TURNSTILE_SITE_KEY: "site", TURNSTILE_SECRET: "secret", DISCORD_WEBHOOK_URL: "https://discord.example/hook" });
  assert.deepEqual(await configured.json(), { enabled: true, siteKey: "site" });
});

test("共同練習の部屋: 部屋 ID の形が違えば 400、WebSocket でなければ 426、別のホストからは 403", async () => {
  const env = { ASSETS: assets(), ROOMS: { idFromName: (n) => n, get: () => ({ fetch: async () => new Response("room") }) } };
  assert.equal((await call("/api/rooms/BAD!/ws", env)).status, 400);
  assert.equal((await call("/api/rooms/abcdef1234/ws", env)).status, 426);
  const foreign = await call("/api/rooms/abcdef1234/ws", env, { headers: { Upgrade: "websocket", Origin: "https://evil.example" } });
  assert.equal(foreign.status, 403);
  const same = await call("/api/rooms/abcdef1234/ws", env, { headers: { Upgrade: "websocket", Origin: "https://avatar-dev.example" } });
  assert.equal(await same.text(), "room");
});

test("共同練習の部屋: 部屋 ID は 10 字以上。手で付けた 9 字以下の ID は 400（作成 UI の 10 字と既存の共有 URL は通る）", async () => {
  const env = { ASSETS: assets(), ROOMS: { idFromName: (n) => n, get: () => ({ fetch: async () => new Response("room") }) } };
  const ws = { headers: { Upgrade: "websocket", Origin: "https://avatar-dev.example" } };
  assert.equal((await call("/api/rooms/abcdef123/ws", env, ws)).status, 400);      // 9 字
  assert.equal((await call("/api/rooms/abcdef12/ws", env, ws)).status, 400);       // 8 字
  assert.equal(await (await call("/api/rooms/abcdef1234/ws", env, ws)).text(), "room"); // 10 字
});

// ---------------------------------------------------------------- 共同練習の部屋（Room を、偽の ctx と WebSocket で動かす）
/** Durable Object の ctx の最小限の偽物 */
function fakeRoom(env = {}) {
  const sockets = [];
  const storage = new Map();
  const alarms = [];   // setAlarm で渡された時刻（DO のアラームは 1 つで、呼ぶたびに上書きされる）
  const ctx = {
    storage: {
      list: async ({ prefix = "" } = {}) => new Map([...storage].filter(([k]) => k.startsWith(prefix))),
      get: async (k) => storage.get(k),
      put: async (o, v) => { if (typeof o === "string") storage.set(o, v); else for (const [k, x] of Object.entries(o)) storage.set(k, x); },
      delete: async (keys) => { for (const k of [].concat(keys)) storage.delete(k); },
      setAlarm: async (t) => { alarms.push(t); },
      deleteAll: async () => storage.clear(),
    },
    setWebSocketAutoResponse() {},
    getWebSocketAutoResponseTimestamp() { return null; },
    getWebSockets: () => sockets,
    acceptWebSocket(ws) { sockets.push(ws); },
  };
  return { room: new Room(ctx, { TURNSTILE_SECRET: "test-secret", ...env }), ctx, alarms, storage };
}

/** WebSocket 1 本の偽物。送られた JSON は sent に溜まる。close された理由は closed に入る */
function fakeSocket() {
  let attachment = null;
  return {
    readyState: 1, sent: [], closed: null,
    send(d) { this.sent.push(JSON.parse(d)); },
    close(code, reason) { this.closed = { code, reason }; this.readyState = 3; },
    accept() {},
    serializeAttachment(a) { attachment = a; },
    deserializeAttachment() { return attachment; },
  };
}

/** 接続 1 本（accept 済み・参加前）。 */
function connect(ctx, id) {
  const ws = fakeSocket();
  ctx.acceptWebSocket(ws);
  ws.serializeAttachment({ id, color: "#f97316", name: "", joined: false, at: Date.now() });
  return ws;
}

/**
 * Node には WebSocketPair と、101 の Response が無いので、fetch（接続の受け付け）を通すための代わりを入れる。
 * fn には、作られたサーバー側の接続の一覧（made）を渡す。終わったら元に戻す。
 */
async function withUpgradeStubs(fn) {
  const saved = { WebSocketPair: globalThis.WebSocketPair, Response: globalThis.Response };
  const made = [];
  globalThis.WebSocketPair = class { constructor() { this[0] = {}; this[1] = fakeSocket(); made.push(this[1]); } };
  globalThis.Response = class { constructor(body, init) { this.status = init?.status ?? 200; this.webSocket = init?.webSocket; } };
  try { return await fn(made); } finally {
    for (const [k, v] of Object.entries(saved)) { if (v === undefined) delete globalThis[k]; else globalThis[k] = v; }
  }
}

/** fetch で接続を 1 本受け付けた結果の、サーバー側の接続（満員で閉じられたものも含む） */
async function accept(room, made) {
  const before = made.length;
  const res = await room.fetch();
  assert.equal(res.status, 101);
  return made[before];
}

const hello = (name) => JSON.stringify({ t: "hello", name, owner: "0123456789abcdef0123456789abcdef", create: { pass: PASS32 } });
const T0 = 1_000_000_000_000;

/** Date.now を仮想の時計にする（終わったら元に戻す） */
async function withClock(start, fn) {
  const real = Date.now;
  let now = start;
  Date.now = () => now;
  try { return await fn((ms) => { now += ms; }); } finally { Date.now = real; }
}

test("共同練習: 二度目の hello では join を二重に知らせない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b2");
  await room.webSocketMessage(a, hello("Alice"));
  await room.webSocketMessage(b, hello("Bob"));
  await room.webSocketMessage(b, hello("Bob"));
  assert.equal(a.sent.filter(m => m.t === "join").length, 1);
  assert.equal(b.sent.filter(m => m.t === "welcome").length, 1);
});

test("共同練習: upsert が無い ops でも delete は反映する（無い・null は空）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, hello("Alice"));
  await room.webSocketMessage(a, JSON.stringify({ t: "ops", delete: ["cube0"] }));
  assert.equal(room.objects.has("cube0"), false);
  assert.equal(a.sent.some(m => m.t === "error"), false);
  const sent = connect(ctx, "b2");
  await room.webSocketMessage(sent, hello("Bob"));
  await room.webSocketMessage(sent, JSON.stringify({ t: "ops", upsert: null, delete: [] }));
  assert.equal(sent.sent.some(m => m.t === "error"), false);
});

test("共同練習: upsert が配列でない ops は、delete も含めて丸ごと捨てる（黙って空にしない）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, hello("Alice"));
  await room.webSocketMessage(a, JSON.stringify({ t: "ops", upsert: "x", delete: ["cube0"] }));
  assert.equal(room.objects.has("cube0"), true);
  assert.deepEqual(a.sent.at(-1), { t: "error", msg: "bad ops" });
});

test("共同練習: 不正なメッセージは直近 1 分の中で数える（古いものは数えない）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(1_000_000_000_000, async (tick) => {
    await room.webSocketMessage(a, hello("Alice"));
    // 61 秒おきに 35 回: どの 1 分の窓にも 30 回は入らないので、切れない
    for (let i = 0; i < 35; i++) {
      tick(61_000);
      await room.webSocketMessage(a, JSON.stringify({ t: "bogus" }));
    }
    assert.equal(a.closed, null);
    // 1 秒の中で 30 回: 切れる
    const b = connect(ctx, "b2");
    await room.webSocketMessage(b, hello("Bob"));
    for (let i = 0; i < 30; i++) await room.webSocketMessage(b, JSON.stringify({ t: "bogus" }));
    assert.equal(b.closed?.code, 1008);
  });
});

test("共同練習: ロックの期限が切れたら、アラームでほかの人へ locks を送る（古いロックを見せ続けない）", async () => {
  const { room, ctx, alarms } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b2");
  await withClock(1_000_000_000_000, async (tick) => {
    await room.webSocketMessage(a, hello("Alice"));
    await room.webSocketMessage(b, hello("Bob"));
    await room.webSocketMessage(a, JSON.stringify({ t: "lock", ids: ["cube0"] }));
    // 次のアラームは、ロックの期限（1 分ごとの確認より前）に設定されている
    const lockUntil = room.locks.get("cube0").until;
    assert.ok(alarms.at(-1) <= lockUntil, `アラーム ${alarms.at(-1)} がロックの期限 ${lockUntil} より後`);
    // 期限を過ぎてアラームが鳴ると、ほかの人の画面からロックが外れる（locks が空で届く）
    tick(lockUntil - Date.now() + 1);
    await room.alarm();
    assert.deepEqual(b.sent.at(-1), { t: "locks", locks: {} });
    assert.equal(room.locks.size, 0);
  });
});

test("表示名: HTML の特殊文字と制御文字を除く（クライアントの C# と同じ）", () => {
  assert.equal(cleanName('<a>&"\'b'), "ab");
  assert.equal(cleanName("  Ali\u0007ce  "), "Alice");
});

// ---------------------------------------------------------------- 人数・参加前の接続・初期化の書き込み・ロックの上限
test("共同練習: 参加前の接続は人数に数えない（参加前が上限を超えても、参加する人は入れる）", async () => {
  const { room } = fakeRoom();
  await withUpgradeStubs(async (made) => {
    for (let i = 0; i < 8; i++) await accept(room, made);   // 参加前の接続を 8 本つなぐ（上限を超えたぶんは閉じられる）
    const open = made.filter(ws => ws.closed === null);
    assert.equal(open.length, LIMITS.pendingMax);
    const ws = open[0];
    await room.webSocketMessage(ws, hello("Alice"));
    assert.equal(ws.closed, null);
    assert.equal(ws.sent.at(-1).t, "locks");
    assert.ok(ws.sent.some(m => m.t === "welcome"));
    assert.equal(room.peers().length, 1);
  });
});

test("共同練習: 満員は、参加（hello）した人の数で決まる。参加前の接続は、満員の判定に入らない", async () => {
  const { room } = fakeRoom();
  await withUpgradeStubs(async (made) => {
    for (let i = 0; i < LIMITS.participants; i++) {
      const ws = await accept(room, made);
      await room.webSocketMessage(ws, hello(`P${i}`));
    }
    assert.equal(room.peers().length, LIMITS.participants);
    // 9 本目の接続は、参加前でも満員で閉じる（理由つき）
    const ninth = await accept(room, made);
    assert.deepEqual(ninth.closed, { code: 4001, reason: "full" });
  });
});

test("共同練習: 満員のあと、参加前のまま残っていた接続が hello をしても入れない（4001）", async () => {
  const { room } = fakeRoom();
  await withUpgradeStubs(async (made) => {
    for (let i = 0; i < LIMITS.participants - 1; i++) {
      const ws = await accept(room, made);
      await room.webSocketMessage(ws, hello(`P${i}`));
    }
    const a = await accept(room, made);
    const b = await accept(room, made);   // 残りは 1 席。a が入る
    await room.webSocketMessage(a, hello("A"));
    assert.equal(a.closed, null);
    await room.webSocketMessage(b, hello("B"));   // 満員になったあとの hello
    assert.deepEqual(b.closed, { code: 4001, reason: "full" });
    assert.equal(room.peers().length, LIMITS.participants);
  });
});

test("共同練習: 参加前の接続は pendingMax まで。超えたら、いちばん古いものを 4002 で閉じる", async () => {
  const { room } = fakeRoom();
  await withUpgradeStubs(async (made) => {
    for (let i = 0; i < LIMITS.pendingMax + 1; i++) await accept(room, made);
    assert.deepEqual(made[0].closed, { code: 4002, reason: "too many pending" });
    for (const ws of made.slice(1)) assert.equal(ws.closed, null);
  });
});

test("共同練習: 参加前のまま pendingMs たった接続は 4000 で閉じる（ping では延命できない）", async () => {
  const { room } = fakeRoom();
  await withClock(T0, async (tick) => {
    await withUpgradeStubs(async (made) => {
      const first = await accept(room, made);
      tick(LIMITS.pendingMs - 1000);
      await accept(room, made);
      assert.equal(first.closed, null, "まだ期限の内");
      tick(2000);
      await accept(room, made);
      assert.deepEqual(first.closed, { code: 4000, reason: "timeout" });
    });
  });
});

test("共同練習: 接続だけでは storage に書かず、アラームも設定しない（最初の hello のあとで初期化する）", async () => {
  const { room, storage, alarms } = fakeRoom();
  await withUpgradeStubs(async (made) => {
    const ws = await accept(room, made);
    await room.webSocketMessage(ws, JSON.stringify({ t: "bogus" }));        // hello の前の不正なメッセージ
    await room.webSocketMessage(ws, JSON.stringify({ t: "ops", delete: [] })); // hello の前の ops
    assert.equal(storage.size, 0, "接続だけでは何も保存しない");
    assert.equal(alarms.length, 0, "接続だけではアラームも設定しない");
    assert.equal(room.objects, null);
    await room.webSocketMessage(ws, hello("Alice"));
    assert.equal(storage.get("init"), true);
    assert.ok(storage.has("o:cube0"));
    assert.ok(alarms.length > 0);
  });
});

test("共同練習: 同じ人が同じオブジェクトを続けて持てるのは lockMaxMs まで。超えたら lockexpired で解除し、ほかの人がすぐ取れる", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b2");
  const lock = (ws) => room.webSocketMessage(ws, JSON.stringify({ t: "lock", ids: ["cube0"] }));
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, hello("Alice"));
    await room.webSocketMessage(b, hello("Bob"));
    // 5 秒ごとに持ち直す（操作が続くあいだ）。上限の前の 20 秒から、lockwarn が届く
    for (let t = 0; t < LIMITS.lockMaxMs; t += 5000) { await lock(a); tick(5000); }
    const warns = a.sent.filter(m => m.t === "lockwarn");
    assert.ok(warns.length > 0, "上限の前に lockwarn が届く");
    assert.ok(warns.at(-1).ms <= LIMITS.lockWarnMs);
    assert.equal(a.sent.some(m => m.t === "lockexpired"), false, "上限の手前ではまだ解除しない");
    // 上限に達した: lockexpired で知らせ、ロックは外れる
    await lock(a);
    assert.equal(a.sent.at(-1).t, "lockexpired");
    assert.equal(room.locks.size, 0);
    // 取り直しても（すぐでも）上限のまま。続きとして数える
    tick(5000);
    await lock(a);
    assert.equal(a.sent.at(-1).t, "lockexpired");
    assert.equal(room.locks.size, 0);
    // ほかの人は、すぐ取れる
    await lock(b);
    assert.equal(room.locks.get("cube0")?.peer, "b2");
  });
});

test("共同練習: 持ち直しの間隔が lockGapMs より空くと、続きが切れて新しく数える", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  const lock = () => room.webSocketMessage(a, JSON.stringify({ t: "lock", ids: ["cube0"] }));
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, hello("Alice"));
    for (let t = 0; t < LIMITS.lockMaxMs; t += 5000) { await lock(); tick(5000); }
    await lock();
    assert.equal(a.sent.at(-1).t, "lockexpired");
    // 手を止めて、取り直しの間隔が lockGapMs より空いたあとは、新しい保持として取れる
    tick(LIMITS.lockGapMs + 1000);
    await lock();
    assert.equal(room.locks.get("cube0")?.peer, "a1");
    assert.equal(a.sent.at(-1).t, "locks");
  });
});

// ---------------------------------------------------------------- 練習の部屋の一覧・鍵・Close・自動終了
const OWNER = "0123456789abcdef0123456789abcdef";   // 作成者の鍵（16 進 32 字）
const OTHER = "fedcba9876543210fedcba9876543210";
const ROOM = "roomaaaa1111";
const PASS32 = "Kq7mZ2xR9tV4bN8wL3pY6hJ1cF5sD0gA";   // 鍵（作成のとき。英数字 32 字）
const ping = JSON.stringify({ t: "ping" });
const createHello = (name, { owner = OWNER, pass = PASS32 } = {}) =>
  JSON.stringify({ t: "hello", name, owner, create: { pass } });
const joinHello = (name, { owner, pass } = {}) => JSON.stringify({ t: "hello", name, ...(owner ? { owner } : {}), ...(pass === undefined ? {} : { pass }) });

/** 一覧（RoomDirectory の代わり）の偽物。upsert・remove の呼び出しを記録する */
function fakeDirectory() {
  const calls = [];
  const stub = {
    upsert: async (e) => { calls.push({ op: "upsert", e }); return true; },
    remove: async (id) => { calls.push({ op: "remove", id }); },
    list: async () => [],
  };
  return { calls, idFromName: (n) => n, get: () => stub, last: () => calls.filter(c => c.op === "upsert").at(-1)?.e };
}

/** 偽の storage（RoomDirectory の単体テスト用） */
function fakeStorage() {
  const m = new Map();
  let alarm = null;   // 掃除の予約（1 つだけ）
  return {
    m,
    get: async (k) => m.get(k),
    put: async (o, v) => { if (typeof o === "string") m.set(o, v); else for (const [k, x] of Object.entries(o)) m.set(k, x); },
    delete: async (keys) => { for (const k of [].concat(keys)) m.delete(k); },
    list: async ({ prefix = "" } = {}) => new Map([...m].filter(([k]) => k.startsWith(prefix))),
    setAlarm: async (t) => { alarm = t; },
    getAlarm: async () => alarm,
    deleteAll: async () => m.clear(),
  };
}

test("作成: 部屋は常に一覧に出る。名前は Worker が決めるランダムな字（クライアントの名前は使わない）。登録は ID と名前・鍵つき（locked）", async () => {
  const dir = fakeDirectory();
  const { room, ctx } = fakeRoom({ ROOM_DIRECTORY: dir });
  room.roomId = ROOM;
  const a = connect(ctx, "a1");
  await withClock(T0, async () => {
    await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice", owner: OWNER, create: { name: "悪い<名前>", pass: PASS32 } }));
    const name = a.sent.find(m => m.t === "welcome").room.name;
    assert.match(name, /^[0-9a-hjkmnp-tvwxyz]{8}$/, "部屋の名前は 8 字のランダムな字（紛らわしい i・l・o・u は使わない）");
    assert.equal(a.sent.find(m => m.t === "welcome").you.owner, true);
    assert.deepEqual(dir.last(), { id: ROOM, name, count: 1, max: LIMITS.participants, locked: true, createdAt: T0 });
  });
});

test("作成: 鍵の無い作成（pass が無い・空・短い・長い・英数字でない）は bad create（部屋は作らない）", async () => {
  for (const pass of ["合言葉".repeat(12), "a".repeat(65), "a b".padEnd(40, "c")]) {
    const { room, ctx, storage } = fakeRoom();
    const a = connect(ctx, "a1");
    await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice", owner: OWNER, create: { pass } }));
    assert.deepEqual(a.sent.at(-1), { t: "error", msg: "bad create" }, String(pass));
    assert.equal(storage.has("init"), false);
  }
});

test("作成: 作成者は鍵の入力なしで入れる（welcome を受け取る）。welcome には鍵が含まれない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice"));
  assert.ok(a.sent.some(m => m.t === "welcome"));
  assert.equal(a.closed, null);
  assert.equal(JSON.stringify(a.sent).includes(PASS32), false, "鍵はサーバーから送らない（作成者の画面は、作るときに端末で持った鍵を使う）");
});

test("鍵つき: 鍵が無ければ needpass で 4003 に閉じる。中身（welcome・参加者・オブジェクト）は一切送らない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: PASS32 }));
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob"));
  assert.deepEqual(b.sent, [{ t: "needpass" }]);
  assert.equal(b.closed?.code, 4003);
});

test("鍵つき: 違う鍵は wrongpass で 4003。正しい鍵なら welcome を受け取る", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: PASS32 }));
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob", { pass: "まちがい" }));
  assert.deepEqual(b.sent, [{ t: "wrongpass" }]);
  assert.equal(b.closed?.code, 4003);
  const c = connect(ctx, "c1");
  await room.webSocketMessage(c, joinHello("Carol", { pass: PASS32 }));
  assert.equal(c.sent[0].t, "welcome");
  assert.equal(c.sent[0].room.locked, true);
  assert.equal(c.sent[0].peers.length, 1);
  assert.equal(c.sent[0].objects.length, 1);
});

test("鍵: 間違いが passFailMax 回続くと、正しい鍵でも passlimit で拒否し、時間が過ぎると入れる（接続元ごと）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, createHello("Alice", { pass: PASS32 }));
    for (let i = 0; i < LIMITS.passFailMax; i++) {
      const b = connect(ctx, `w${i}`);
      await room.webSocketMessage(b, joinHello("Bob", { pass: "x" }));
      assert.equal(b.sent[0].t, "wrongpass");
    }
    const blocked = connect(ctx, "blocked");
    await room.webSocketMessage(blocked, joinHello("Bob", { pass: PASS32 }));
    assert.equal(blocked.sent[0].t, "passlimit");
    assert.ok(blocked.sent[0].ms > 0 && blocked.sent[0].ms <= LIMITS.passBlockMs);
    assert.equal(blocked.closed?.code, 4003);
    tick(LIMITS.passBlockMs + 1);
    const ok = connect(ctx, "ok");
    await room.webSocketMessage(ok, joinHello("Bob", { pass: PASS32 }));
    assert.equal(ok.sent[0].t, "welcome");
  });
});

test("鍵の保存: 平文は残さない。保存は { v: 2, ソルト 16 バイト, SHA-256(ソルト || 鍵) } だけ（反復は無い）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: PASS32 }));
  const meta = storage.get("meta");
  assert.equal(meta.pass.v, 2);
  assert.match(meta.pass.salt, /^[0-9a-f]{32}$/);
  assert.match(meta.pass.hash, /^[0-9a-f]{64}$/);
  assert.equal("iter" in meta.pass, false, "PBKDF2 の反復回数は持たない");
  assert.equal(JSON.stringify([...storage.entries()]).includes(PASS32), false);
  // 同じ鍵でも、ソルトが違えばハッシュも違う（同じ鍵の部屋どうしで、ハッシュの一致から鍵を推測させない）
  const { room: r2, ctx: c2, storage: s2 } = fakeRoom();
  await r2.webSocketMessage(connect(c2, "x1"), createHello("Bob", { pass: PASS32 }));
  assert.notEqual(s2.get("meta").pass.salt, meta.pass.salt);
  assert.notEqual(s2.get("meta").pass.hash, meta.pass.hash);
});

test("鍵の保存: 正しい鍵（ソルトつき SHA-256）だけが合い、1 字違えば合わない", async () => {
  const { room, ctx } = fakeRoom();
  await room.webSocketMessage(connect(ctx, "a1"), createHello("Alice", { pass: PASS32 }));
  const ok = connect(ctx, "b1");
  await room.webSocketMessage(ok, joinHello("Bob", { pass: PASS32 }));
  assert.equal(ok.sent[0].t, "welcome");
  const ng = connect(ctx, "c1");
  await room.webSocketMessage(ng, joinHello("Carol", { pass: PASS32.slice(0, -1) + "B" }));
  assert.equal(ng.sent[0].t, "wrongpass");
});

test("作成は最初の hello だけ。既にある部屋では、create・owner・鍵を上書きできない", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { owner: OWNER, pass: PASS32 }));
  const before = storage.get("meta");
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, createHello("Mallory", { owner: OTHER }));
  assert.equal(b.sent.find(m => m.t === "welcome")?.you.owner, undefined, "後から来た人は作成者にならない");
  assert.equal(b.closed?.code, 4003, "鍵つきの部屋なので、鍵が無い hello は入れない");
  assert.deepEqual(storage.get("meta"), before, "作成の指定は書き換わらない");
});

test("作成者の鍵: 再接続（タブを開き直した・回線が切れた）でも、同じ鍵の接続が作成者に戻る。違う鍵は作成者でない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice"));
  await room.webSocketClose(a, 1000);
  const again = connect(ctx, "a2");
  await room.webSocketMessage(again, joinHello("Alice", { owner: OWNER }));
  assert.equal(again.sent.find(m => m.t === "welcome").you.owner, true);
  const other = connect(ctx, "b1");
  await room.webSocketMessage(other, joinHello("Bob", { owner: OTHER, pass: PASS32 }));
  assert.equal(other.sent.find(m => m.t === "welcome").you.owner, false);
});

test("作成者の鍵（同じ端末の別タブ）: 作成したタブが開いたままでも、別の接続は鍵を送らず owner だけで入れる（免除）", async () => {
  const { room, ctx } = fakeRoom();
  const creator = connect(ctx, "a1");
  await room.webSocketMessage(creator, createHello("Alice"));
  const tab = connect(ctx, "a2");
  await room.webSocketMessage(tab, joinHello("Alice", { owner: OWNER }));   // pass は無い（クライアントは一覧からの入室で、保存の鍵だけを送る）
  assert.equal(tab.sent.find(m => m.t === "welcome")?.you.owner, true);
  assert.equal(tab.sent.some(m => m.t === "needpass" || m.t === "wrongpass"), false);
});

test("作成者の鍵の免除は、その部屋だけ: 別の人の鍵（pass なし）は needpass で 4003 に閉じる", async () => {
  const { room, ctx } = fakeRoom();
  const creator = connect(ctx, "a1");
  await room.webSocketMessage(creator, createHello("Alice"));
  const other = connect(ctx, "b1");
  await room.webSocketMessage(other, joinHello("Bob", { owner: OTHER }));
  assert.deepEqual(other.sent, [{ t: "needpass" }]);
  assert.equal(other.closed?.code, 4003);
});

test("作成者の鍵の免除は、別の部屋には効かない: 同じ作成者の鍵でも、別の部屋は needpass（pass なし）", async () => {
  const { room: roomA, ctx: ctxA } = fakeRoom();
  const creatorA = connect(ctxA, "a1");
  await roomA.webSocketMessage(creatorA, createHello("Alice"));
  // 別の部屋（ID は違うが、同じ作成者の鍵を持つ人が居る）。その部屋の作成者は別の鍵
  const { room: roomB, ctx: ctxB } = fakeRoom();
  const creatorB = connect(ctxB, "c1");
  await roomB.webSocketMessage(creatorB, createHello("Carol", { owner: OTHER }));
  const sameKeyElsewhere = connect(ctxB, "c2");
  await roomB.webSocketMessage(sameKeyElsewhere, joinHello("Alice", { owner: OWNER }));
  assert.deepEqual(sameKeyElsewhere.sent, [{ t: "needpass" }]);
  assert.equal(sameKeyElsewhere.closed?.code, 4003);
});

test("Close: 作成者だけが閉じられる。作成者でない人の close は、閉じずに not owner を返す", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b1");
  await room.webSocketMessage(a, createHello("Alice"));
  await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
  await room.webSocketMessage(b, JSON.stringify({ t: "close" }));
  assert.deepEqual(b.sent.at(-1), { t: "error", msg: "not owner" });
  assert.equal(b.closed, null);
  assert.equal(room.gone, false);
});

test("Close: 作成者が閉じると、全員に closed（reason owner）を送って切断し、部屋の中身と一覧の登録を消す", async () => {
  const dir = fakeDirectory();
  const { room, ctx, storage, alarms } = fakeRoom({ ROOM_DIRECTORY: dir });
  room.roomId = ROOM;
  await withClock(T0, async (tick) => {
    const a = connect(ctx, "a1");
    const b = connect(ctx, "b1");
    await room.webSocketMessage(a, createHello("Alice"));
    await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
    tick(1000);
    await room.webSocketMessage(a, JSON.stringify({ t: "close" }));
    for (const s of [a, b]) {
      assert.deepEqual(s.sent.at(-1), { t: "closed", reason: "owner" });
      assert.equal(s.closed?.code, 4004);
    }
    assert.ok(dir.calls.some(c => c.op === "remove" && c.id === ROOM), "一覧から外す");
    assert.equal(storage.has("o:cube0"), false, "オブジェクトを消す");
    assert.equal(storage.has("init"), false);
    assert.equal(storage.has("meta"), false);
    assert.deepEqual(storage.get("closed"), { at: T0 + 1000, reason: "owner" });
    assert.equal(alarms.at(-1), T0 + 1000 + LIMITS.closedMs, "閉じた目印は 10 分あとに消すアラーム");
  });
});

test("閉じた ID は、閉じた直後の作成を拒否する（4004。理由つき）。目印の期限が過ぎると、同じ ID でも新しい部屋になる", async () => {
  const { room, ctx, storage } = fakeRoom();
  await withClock(T0, async (tick) => {
    const a = connect(ctx, "a1");
    await room.webSocketMessage(a, createHello("Alice"));
    await room.webSocketMessage(a, JSON.stringify({ t: "close" }));
    tick(60_000);
    await withUpgradeStubs(async (made) => {
      const refused = await accept(room, made);
      assert.deepEqual(refused.sent, [{ t: "closed", reason: "owner" }]);
      assert.equal(refused.closed?.code, 4004);
    });
    tick(LIMITS.closedMs);
    await room.alarm();
    assert.equal(storage.has("closed"), false, "目印は期限のあと消す");
    const b = connect(ctx, "b1");
    await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
    assert.deepEqual(b.sent, [{ t: "closed", reason: "missing" }], "作成の指定の無い hello では作り直さない");
    const c = connect(ctx, "c1");
    await room.webSocketMessage(c, createHello("Carol"));
  });
});

test("30 分の自動終了: 成功した操作（ops の適用）が無いと、2 分前に idlewarn、30 分で idle として閉じる。ping では延びない", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, createHello("Alice"));
    const minute = async () => { tick(60_000); await room.webSocketMessage(a, ping); await room.alarm(); };
    for (let i = 0; i < 27; i++) await minute();          // 27 分。まだ警告の前
    assert.equal(a.sent.some(m => m.t === "idlewarn"), false);
    tick(90_000); await room.webSocketMessage(a, ping); await room.alarm();   // 28.5 分: 2 分前
    const warn = a.sent.filter(m => m.t === "idlewarn");
    assert.equal(warn.length, 1);
    assert.ok(warn[0].ms > 0 && warn[0].ms <= 90_000, `残り ${warn[0].ms} ms`);
    // 操作（unlock）で警告は取り消され、時刻が延びる
    await room.webSocketMessage(a, JSON.stringify({ t: "ops", upsert: [{ id: "x1", kind: "cube", name: "x", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }] }));
    assert.deepEqual(a.sent.at(-1), { t: "idlewarn", ms: null });
    // ping だけでは延びない: 操作のあと 27 分でもう一度警告し、30 分で閉じる
    for (let i = 0; i < 27; i++) await minute();
    assert.equal(a.sent.filter(m => m.t === "idlewarn").length, 2, "操作のあと、もう一度 2 分前に警告");
    for (let i = 0; i < 3; i++) await minute();
    assert.deepEqual(a.sent.at(-1), { t: "closed", reason: "idle" });
    assert.equal(a.closed?.code, 4004);
    assert.equal(storage.has("o:cube0"), false);
  });
});

test("30 分の自動終了: ROOM_IDLE_SECONDS で短くでき、範囲外（60 秒未満・30 分超・数でない）は 30 分のまま", async () => {
  const short = fakeRoom({ ROOM_IDLE_SECONDS: "120" });
  const a = connect(short.ctx, "a1");
  await withClock(T0, async (tick) => {
    await short.room.webSocketMessage(a, createHello("Alice"));
    tick(60_000);
    await short.room.alarm();   // 120 秒の部屋は、残り 60 秒（半分）から警告する
    assert.ok(a.sent.some(m => m.t === "idlewarn"));
    tick(60_000);
    await short.room.alarm();
    assert.deepEqual(a.sent.at(-1), { t: "closed", reason: "idle" });
  });
  for (const bad of ["10", "99999", "abc", undefined]) {
    assert.equal(fakeRoom({ ROOM_IDLE_SECONDS: bad }).room.idleMs(), LIMITS.roomIdleMs, String(bad));
  }
});

test("一覧: 参加者の増減に合わせて登録の人数を更新し、誰もいなくなって猶予が過ぎたら外す", async () => {
  const dir = fakeDirectory();
  const { room, ctx } = fakeRoom({ ROOM_DIRECTORY: dir });
  room.roomId = ROOM;
  await withClock(T0, async (tick) => {
    const a = connect(ctx, "a1");
    await room.webSocketMessage(a, createHello("Alice"));
    assert.equal(dir.last().count, 1);
    const b = connect(ctx, "b1");
    await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
    assert.equal(dir.last().count, 2);
    await room.webSocketClose(b, 1000);
    assert.equal(dir.last().count, 1);
    tick(LIMITS.listGraceMs + 1000);
    await room.webSocketClose(a, 1000);
    assert.equal(dir.calls.at(-1).op, "remove", "猶予が過ぎて誰もいなければ外す");
  });
});

test("一覧: 一覧の登録がうまくいかなくても、部屋は動く（例外を外に出さない）", async () => {
  const broken = { idFromName: () => "x", get: () => ({ upsert: async () => { throw new Error("down"); }, remove: async () => { throw new Error("down"); } }) };
  const { room, ctx } = fakeRoom({ ROOM_DIRECTORY: broken });
  room.roomId = ROOM;
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice"));
  assert.equal(a.sent[0].t, "welcome");
});

test("RoomDirectory: 登録・一覧（新しい順）・外す。参加者がいない部屋は、猶予のあとは出ない", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  const now = T0;
  assert.equal(await dir.upsert({ id: "roomaaaa1111", name: "古い", count: 2, max: 8, locked: true, createdAt: now - 5000 }, now), true);
  assert.equal(await dir.upsert({ id: "roombbbb2222", name: "新しい", count: 1, max: 8, locked: false, createdAt: now - 1000 }, now), true);
  assert.equal(await dir.upsert({ id: "roomcccc3333", name: "空", count: 0, max: 8, locked: false, createdAt: now - LIMITS.listGraceMs - 1 }, now), true);
  const rooms = await dir.list(now);
  assert.deepEqual(rooms.map(r => r.name), ["新しい", "古い"], "参加者 0 人で猶予が過ぎた部屋は出ない。新しい順");
  assert.deepEqual(Object.keys(rooms[0]).sort(), ["count", "createdAt", "id", "locked", "max", "name"], "公開するのは一覧の項目だけ");
  await dir.remove("roomaaaa1111");
  assert.deepEqual((await dir.list(now)).map(r => r.id), ["roombbbb2222"]);
});

test("RoomDirectory: 登録の期限（listTtlMs のあいだ更新が無い）は一覧から消す。保存の上限（listStoreMax）と一覧の上限（listMax）", async () => {
  const storage = fakeStorage();
  const dir = new RoomDirectory({ storage }, {});
  const now = T0;
  await dir.upsert({ id: "roomaaaa1111", name: "止まった", count: 1, max: 8, locked: false, createdAt: now - 60_000 }, now - LIMITS.listTtlMs - 1);
  assert.deepEqual(await dir.list(now), []);
  assert.equal(storage.m.has("r:roomaaaa1111"), false, "期限の切れた登録は、読むときに消す");
  for (let i = 0; i < LIMITS.listStoreMax; i++) {
    const id = `room${String(i).padStart(8, "0")}`;
    assert.equal(await dir.upsert({ id, name: "n", count: 1, max: 8, locked: false, createdAt: now - i }, now), true);
  }
  assert.equal(await dir.upsert({ id: "roomzzzz9999", name: "あふれ", count: 1, max: 8, locked: false, createdAt: now }, now), false, "保存の上限を超える登録は断る");
  assert.equal((await dir.list(now)).length, LIMITS.listMax, "一覧は上限の件数まで");
});

test("RoomDirectory: 不正な登録（ID の形・人数・max・鍵の型）は保存しない", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  const base = { id: "roomaaaa1111", name: "n", count: 1, max: 8, locked: false, createdAt: T0 };
  for (const bad of [{ id: "BAD" }, { count: -1 }, { count: 9 }, { max: 3 }, { locked: "yes" }, { createdAt: "x" }, { name: 5 }]) {
    assert.equal(await dir.upsert({ ...base, ...bad }, T0), false, JSON.stringify(bad));
  }
});

test("一覧の API（GET /api/rooms）: 一覧の JSON を返す（キャッシュさせない）。POST は 405。別のホストからは 403", async () => {
  const dir = fakeDirectory();
  dir.get = () => ({ list: async () => [{ id: "roomaaaa1111", name: "n", count: 1, max: 8, locked: false, createdAt: T0 }] });
  const env = { ASSETS: assets(), ROOM_DIRECTORY: dir };
  const res = await call("/api/rooms", env);
  assert.equal(res.status, 200);
  assert.equal(res.headers.get("Cache-Control"), "no-store");
  const body = await res.json();
  assert.equal(body.rooms[0].name, "n");
  assert.equal(body.limit, LIMITS.listMax);
  assert.equal((await call("/api/rooms", env, { method: "POST" })).status, 405);
  assert.equal((await call("/api/rooms", env, { headers: { Origin: "https://evil.example" } })).status, 403);
  assert.deepEqual(await (await call("/api/rooms", { ASSETS: assets() })).json(), { rooms: [], limit: LIMITS.listMax }, "一覧の束縛が無ければ空");
});

test("部屋の WebSocket: 部屋 ID を X-Room-Id で Durable Object に渡す（一覧の登録に使う）", async () => {
  const seen = [];
  const env = { ASSETS: assets(), ROOMS: { idFromName: (n) => n, get: () => ({ fetch: async (req) => { seen.push(req.headers.get("X-Room-Id")); return new Response("room"); } }) } };
  await call("/api/rooms/roomaaaa1111/ws", env, { headers: { Upgrade: "websocket", Origin: "https://avatar-dev.example" } });
  assert.deepEqual(seen, ["roomaaaa1111"]);
});

test("部屋の作成の指定: 鍵は英数字 32〜64 字。作成者の鍵は 16 進 32 字。部屋の名前は受け取らない", () => {
  assert.equal(parseCreate({ pass: "a".repeat(32) }, OWNER).pass.length, 32, "32 字は作れる（下限）");
  assert.equal(parseCreate({ pass: "a".repeat(64) }, OWNER).pass.length, 64, "64 字は作れる（上限）");
  assert.equal(parseCreate({ pass: "a".repeat(31) }, OWNER), null, "31 字は作れない");
  assert.equal(parseCreate({ pass: "a".repeat(65) }, OWNER), null, "65 字は作れない");
  assert.equal(parseCreate({ pass: "合言葉合言葉合言葉合言葉合言葉合言葉合言葉合言葉合言葉合言葉" }, OWNER), null, "英数字以外は作れない");
  assert.equal(parseCreate({ pass: "" }, OWNER), null, "空の鍵は作れない（鍵なしの部屋は無い）");
  assert.equal(parseCreate({}, OWNER), null);
  assert.equal(parseCreate({ pass: PASS32 }, "short"), null, "作成者の鍵の形が違えば作れない");
  const named = parseCreate({ name: "悪い名", pass: PASS32 }, OWNER);
  assert.deepEqual(Object.keys(named).sort(), ["owner", "pass"], "名前は受け取らない（Worker が作る）");
});

test("部屋の名前: ランダムな 8 字（紛らわしい i・l・o・u を使わない）。何度作っても同じにならない", () => {
  const names = new Set();
  for (let i = 0; i < 200; i++) {
    const n = randomRoomName();
    assert.match(n, /^[0-9a-hjkmnp-tvwxyz]{8}$/);
    names.add(n);
  }
  assert.ok(names.size > 190, "ほぼ重ならない");
});

test("旧形式の判定: 鍵の保存形が v2 でない部屋（PBKDF2 の時代・鍵なし）は旧形式", () => {
  assert.equal(isLegacyRoom({ pass: { v: 2, salt: "a", hash: "b" } }), false);
  assert.equal(isLegacyRoom({ pass: { salt: "a", iter: 100000, hash: "b" } }), true);
  assert.equal(isLegacyRoom({ pass: null }), true);
  assert.equal(isLegacyRoom(null), false, "部屋の記録が無いのは旧形式ではない（まだ作られていない）");
});

test("作成の指定が不正なら bad create（部屋は作らない）。鍵の型や作成者の鍵の形が違う場合", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice", owner: "short", create: { pass: PASS32 } }));
  assert.deepEqual(a.sent.at(-1), { t: "error", msg: "bad create" });
  assert.equal(storage.has("init"), false);
});

test("古い版の作成（鍵が無い・短い）は outdated で 4006 に閉じる（再読み込みを案内する。接続を開いたままにしない）", async () => {
  for (const create of [{ name: "練習の部屋", listed: true }, { pass: "" }, { pass: "a".repeat(31) }]) {
    const { room, ctx, storage } = fakeRoom();
    const a = connect(ctx, "a1");
    await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice", owner: OWNER, create }));
    assert.deepEqual(a.sent.at(-1), { t: "closed", reason: "outdated" }, JSON.stringify(create));
    assert.equal(a.closed?.code, 4006);
    assert.equal(storage.has("init"), false);
  }
});

// ---------------------------------------------------------------- 監査の指摘への対応（既存の部屋・作成の順序・同時の処理・IPv6・免除・成功した操作・一覧の覚え）
test("既存の部屋（デプロイ前に作られ、opAt が無い）: 旧版の changed から数え、デプロイ直後に閉じない。changed も無ければ今から", async () => {
  const cube = { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] };
  for (const changed of [T0 - 10 * 60_000, undefined]) {
    const label = changed === undefined ? "changed なし" : "changed あり";
    const { room, ctx, storage } = fakeRoom();
    storage.set("init", true);
    storage.set("o:cube0", cube);
    if (changed !== undefined) storage.set("changed", changed);
    await withClock(T0, async (tick) => {
      const a = connect(ctx, "a1");
      await room.webSocketMessage(a, joinHello("Alice", { pass: PASS32 }));   // 作成の指定は無い（既存の部屋には入れる）
      assert.equal(a.sent[0].t, "welcome", label);
      assert.equal(a.sent[0].objects.length, 1, label);
      await room.alarm();
      assert.equal(room.gone, false, label);
      assert.equal(a.sent.some(m => m.t === "closed"), false, label);
      if (changed !== undefined) {
        // changed から 30 分たてば閉じる（ping だけでは延びない）
        for (let i = 0; i < 20; i++) { tick(60_000); await room.webSocketMessage(a, ping); await room.alarm(); }
        assert.deepEqual(a.sent.at(-1), { t: "closed", reason: "idle" }, label);
      }
    });
  }
});

test("作成の指定が無く、部屋も無い hello は、部屋を作らずに閉じる（closed: missing・4004）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, joinHello("Alice", { pass: PASS32 }));
  assert.deepEqual(a.sent, [{ t: "closed", reason: "missing" }]);
  assert.equal(a.closed?.code, 4004);
  assert.equal(storage.has("init"), false);
  assert.equal(storage.has("o:cube0"), false);
});

test("同時の作成: 2 本の hello が同時に来ても、部屋を作るのは 1 回だけ。作成者は先の 1 本だけで、作成の指定も 1 つ", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b1");
  await Promise.all([
    room.webSocketMessage(a, createHello("Alice", { owner: OWNER })),
    room.webSocketMessage(b, createHello("Mallory", { owner: OTHER })),
  ]);
  const owners = [a, b].filter(s => s.sent.find(m => m.t === "welcome")?.you.owner === true);
  assert.equal(owners.length, 1, "作成者は 1 人");
  const winner = owners[0] === a ? OWNER : OTHER;
  const { createHash } = await import("node:crypto");
  assert.equal(storage.get("meta").ownerHash, createHash("sha256").update(winner).digest("hex"));
  assert.equal(storage.get("meta").pass.v, 2, "後から来た作成の指定は、鍵を置き換えない");
});

test("同時の参加: 9 人が同時に入っても、8 人までしか入らない（人数の確認と参加の印の間に await を挟まない）", async () => {
  const { room, ctx } = fakeRoom();
  const first = connect(ctx, "c0");
  await room.webSocketMessage(first, createHello("Owner"));
  const socks = Array.from({ length: 8 }, (_, i) => connect(ctx, `j${i}`));
  await Promise.all(socks.map((s, i) => room.webSocketMessage(s, joinHello(`P${i}`, { pass: PASS32 }))));
  const all = [first, ...socks];
  assert.equal(all.filter(s => s.sent.some(m => m.t === "welcome")).length, LIMITS.participants);
  assert.equal(all.filter(s => s.closed?.code === 4001).length, 1, "あふれた 1 人は満員で閉じる");
});

test("鍵の免除: 作成者の鍵が合う接続は、鍵が無くても入れる（鍵を忘れた作成者でも入れる）。ほかの人は鍵が要る", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: PASS32 }));
  await room.webSocketClose(a, 1000);
  const again = connect(ctx, "a2");
  await room.webSocketMessage(again, joinHello("Alice", { owner: OWNER }));
  assert.equal(again.sent[0].t, "welcome");
  assert.equal(again.sent[0].you.owner, true);
  const other = connect(ctx, "b1");
  await room.webSocketMessage(other, joinHello("Bob", { owner: OTHER }));
  assert.deepEqual(other.sent, [{ t: "needpass" }]);
});

test("接続元（IPv6 は /64・IPv4 はそのまま）: 同じ /64 は同じ単位。展開の形（::・省略）も同じに数える", () => {
  assert.equal(netKey("2001:db8:0:1::5"), netKey("2001:db8:0:1:ffff:0:0:9"));
  assert.equal(netKey("2001:db8:0:1::5"), netKey("2001:0db8:0000:0001:aaaa::1"));
  assert.notEqual(netKey("2001:db8:0:1::5"), netKey("2001:db8:0:2::5"));
  assert.equal(netKey("192.0.2.1"), "192.0.2.1");
  assert.equal(netKey("::1"), "0000:0000:0000:0000::/64");
});

test("鍵の間違いは接続元ごとに数える（IPv6 は /64 でまとめる。同じ /64 の別の住所では回避できない）", async () => {
  const { room, ctx } = fakeRoom();
  const owner = connect(ctx, "a1");
  await room.webSocketMessage(owner, createHello("Alice", { pass: PASS32 }));
  // 接続元の IP を付けて接続し、hello を送る（withUpgradeStubs で、接続の受け付けの形を作る）
  const from = (ip, pass) => withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": ip } }));
    const ws = made.at(-1);
    await room.webSocketMessage(ws, joinHello("Bob", { pass }));
    return ws;
  });
  for (let i = 0; i < LIMITS.passFailMax; i++) assert.equal((await from("2001:db8:0:1::1", "まちがい")).sent[0].t, "wrongpass");
  assert.equal((await from("2001:db8:0:1:ffff::9", PASS32)).sent[0].t, "passlimit", "同じ /64 の別の住所も拒否");
  assert.equal((await from("192.0.2.7", PASS32)).sent[0].t, "welcome", "別の接続元（IPv4）は影響を受けない");
});

test("鍵の間違いの行: 期限の切れた行は、次の間違いのときに消す（行が増え続けない）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const owner = connect(ctx, "a1");
  await room.webSocketMessage(owner, createHello("Alice", { pass: PASS32 }));
  await withClock(T0, async (tick) => {
    const w1 = connect(ctx, "w1");
    await room.webSocketMessage(w1, joinHello("Bob", { pass: "x" }));
    assert.equal(storage.has("fail:-"), true);
    tick(LIMITS.passFailWindowMs + 1000);
    const w2 = connect(ctx, "w2");
    w2.serializeAttachment({ ...w2.deserializeAttachment(), ipKey: "other" });
    await room.webSocketMessage(w2, joinHello("Carol", { pass: "x" }));
    assert.equal(storage.has("fail:-"), false, "期限の切れた行は消す");
    assert.equal(storage.has("fail:other"), true);
  });
});

test("閉じる処理の途中で落ちても、閉じた印は残る（閉じた部屋が、作り直せる状態に戻らない）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice"));
  ctx.storage.delete = async () => { throw new Error("disk"); };
  await assert.rejects(room.webSocketMessage(a, JSON.stringify({ t: "close" })), /disk/);
  assert.ok(storage.has("closed"), "閉じた印は先に書いてある");
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, createHello("Bob"));
  assert.deepEqual(b.sent, [{ t: "closed", reason: "owner" }], "閉じた部屋は作り直せない");
});

test("操作の数え方: 拒否された・適用されなかった操作（存在しない対象の preview・不正な ops）は、寿命を延ばさない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, createHello("Alice"));
    const minute = async () => { tick(60_000); await room.webSocketMessage(a, ping); await room.alarm(); };
    for (let i = 0; i < 28; i++) await minute();
    tick(30_000);
    await room.webSocketMessage(a, JSON.stringify({ t: "preview", objs: [{ id: "nope", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }] }));
    await room.webSocketMessage(a, JSON.stringify({ t: "ops", upsert: "x" }));
    await room.alarm();
    assert.equal(a.sent.filter(m => m.t === "idlewarn" && typeof m.ms === "number").length, 1, "数えていないので、2 分前に警告が出ている");
    for (let i = 0; i < 3; i++) await minute();
    assert.deepEqual(a.sent.at(-1), { t: "closed", reason: "idle" });
  });
});

test("既存の部屋の寿命の警告: 警告の後に成功した操作があれば、取り消して、もう一度 2 分前に出す", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, createHello("Alice"));
    tick(29 * 60_000);
    await room.webSocketMessage(a, ping);
    await room.alarm();
    assert.equal(a.sent.filter(m => m.t === "idlewarn").length, 1);
    await room.webSocketMessage(a, JSON.stringify({ t: "ops", upsert: [{ id: "x2", kind: "cube", name: "y", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] }] }));
    assert.deepEqual(a.sent.at(-1), { t: "idlewarn", ms: null });
    tick(60_000);
    await room.webSocketMessage(a, ping);
    await room.alarm();
    assert.equal(a.sent.filter(m => m.t === "idlewarn" && typeof m.ms === "number").length, 1, "延びたので、まだ警告は出ない");
  });
});

test("一覧の API: Worker の中で LIMITS.listCacheMs のあいだ覚える（DO への問い合わせを減らす）。過ぎたら取り直す", async () => {
  let calls = 0;
  const dir = { idFromName: n => n, get: () => ({ list: async () => { calls++; return [{ id: "roomaaaa1111", name: "n", count: 1, max: 8, locked: false, createdAt: T0 }]; } }) };
  const env = { ASSETS: assets(), ROOM_DIRECTORY: dir };
  await withClock(T0, async (tick) => {
    await call("/api/rooms", env);
    await call("/api/rooms", env);
    assert.equal(calls, 1);
    tick(LIMITS.listCacheMs + 1);
    const res = await call("/api/rooms", env);
    assert.equal(res.headers.get("Cache-Control"), "no-store", "覚えていても、応答は no-store のまま");
    assert.equal(calls, 2);
  });
});

test("旧形式の部屋（PBKDF2 の時代の保存形）は、入ろうとした人に expired で閉じる。部屋の中身は消す（読めないまま残さない）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const cube = { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] };
  storage.set("init", true);
  storage.set("meta", { name: "旧い部屋", createdAt: T0, pass: { salt: "00".repeat(16), iter: 100000, hash: "11".repeat(32) }, ownerHash: "22".repeat(32) });
  storage.set("opAt", T0);
  storage.set("o:cube0", cube);
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
  assert.deepEqual(b.sent, [{ t: "closed", reason: "expired" }]);
  assert.equal(b.closed?.code, 4004);
  assert.equal(storage.has("o:cube0"), false, "中身は消す");
  assert.equal(storage.has("init"), false);
});

test("鍵なしの旧い部屋（pass が null）も、期限切れとして閉じる", async () => {
  const { room, ctx, storage } = fakeRoom();
  storage.set("init", true);
  storage.set("meta", { name: "鍵なし", createdAt: T0, pass: null, ownerHash: "22".repeat(32) });
  storage.set("opAt", T0);
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob"));
  assert.deepEqual(b.sent, [{ t: "closed", reason: "expired" }]);
});

// ---------------------------------------------------------------- 部屋の作成の回数（接続元ごと・1 分 1 部屋・JST の 1 日 3 部屋）
/** 作成の回数の数え先（RoomDirectory の本物を使い、storage は偽物）。 */
function directoryFor() {
  const dirStorage = fakeStorage();
  const dir = new RoomDirectory({ storage: dirStorage }, {});
  return { dir, dirStorage, env: () => ({ idFromName: (n) => n, get: () => dir }) };
}

/** 接続元 ip から、部屋を 1 つ作る（hello の create）。作られた接続（作成の結果を sent で見る）を返す。 */
async function createFrom(ip, env, roomId = "roomaaaa" + Math.random().toString(36).slice(2, 8).padEnd(6, "x")) {
  const { room } = fakeRoom(env);
  room.roomId = roomId;
  return withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": ip } }));
    const ws = made.at(-1);
    await room.webSocketMessage(ws, createHello("Alice"));
    return ws;
  });
}

const limited = (ws) => ws.sent.some(m => m.t === "closed" && m.reason === "limit") && ws.closed?.code === 4005;
const created = (ws) => ws.sent.some(m => m.t === "welcome");

test("部屋の作成の回数: 同じ接続元は 1 分に 1 部屋まで。2 部屋目は 4005 で閉じる（理由は rate limited・closed: limit）", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  await withClock(T0, async () => {
    for (let i = 0; i < LIMITS.createPerMinute; i++) assert.ok(created(await createFrom("203.0.113.5", e)), `${i + 1} 部屋目は作れる`);
    const sixth = await createFrom("203.0.113.5", e);
    assert.ok(limited(sixth), "6 部屋目は作れない");
    assert.equal(sixth.closed.reason, "rate limited");
    assert.equal(created(sixth), false);
  });
});

test("部屋の作成の回数: IPv6 は /64 でまとめる（同じ /64 の別の住所では回避できない）。別の /64 は別に数える", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  await withClock(T0, async () => {
    for (let i = 0; i < LIMITS.createPerMinute; i++) await createFrom(`2001:db8:0:1:${(i + 1).toString(16)}::5`, e);
    assert.ok(limited(await createFrom("2001:db8:0:1:ffff::9", e)), "同じ /64 の別の住所は、同じ単位");
    assert.ok(created(await createFrom("2001:db8:0:2::5", e)), "別の /64 は別");
  });
});

test("部屋の作成の回数: 別の接続元は別に数える（IPv4 どうし）", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  await withClock(T0, async () => {
    for (let i = 0; i < LIMITS.createPerMinute; i++) await createFrom("203.0.113.5", e);
    assert.ok(limited(await createFrom("203.0.113.5", e)));
    assert.ok(created(await createFrom("203.0.113.6", e)));
  });
});

test("部屋の作成の回数: 1 分たつと戻る（窓は 1 分。古い作成は数えない）", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  await withClock(T0, async (tick) => {
    for (let i = 0; i < LIMITS.createPerMinute; i++) await createFrom("203.0.113.7", e);
    assert.ok(limited(await createFrom("203.0.113.7", e)));
    tick(LIMITS.createWindowMs - 1000);
    assert.ok(limited(await createFrom("203.0.113.7", e)), "1 分まで少し足りないあいだは、まだ閉じる");
    tick(2000);
    assert.ok(created(await createFrom("203.0.113.7", e)), "1 分たったら、もう一度作れる");
  });
});

test("部屋の作成の回数: 1 日 3 部屋まで（JST の暦日）。日が変わると戻る", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  const day = nextJstMidnight(new Date(T0)).getTime() - 24 * 60 * 60 * 1000;   // 同じ JST の日の中
  await withClock(day + 60_000, async (tick) => {
    for (let round = 0; round < LIMITS.createPerDay / LIMITS.createPerMinute; round++) {
      for (let i = 0; i < LIMITS.createPerMinute; i++) assert.ok(created(await createFrom("203.0.113.8", e)));
      tick(LIMITS.createWindowMs + 1000);
    }
    const over = await createFrom("203.0.113.8", e);
    assert.ok(limited(over), "31 部屋目は、同じ日の中では作れない");
  });
  await withClock(nextJstMidnight(new Date(day + 60_000)).getTime() + 1000, async () => {
    assert.ok(created(await createFrom("203.0.113.8", e)), "次の日は、また作れる");
  });
});

test("部屋の作成の回数: 既存の部屋への参加（create が無い hello）は数えない。制限に達した接続元からも、鍵を入れて入れる", async () => {
  const { env } = directoryFor();
  const e = { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() };
  const { room } = fakeRoom(e);
  room.roomId = "roomzzzz0001";
  await withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.9" } }));
    await room.webSocketMessage(made.at(-1), createHello("Alice"));
  });
  // 同じ接続元から、制限まで作る（作成の数は使い切る）
  for (let i = 1; i < LIMITS.createPerMinute; i++) await createFrom("203.0.113.9", e);
  assert.ok(limited(await createFrom("203.0.113.9", e)));
  // その接続元から、同じ部屋に鍵を入れて入る（作成ではないので、数えない・通る）
  const joined = await withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.9" } }));
    const ws = made.at(-1);
    await room.webSocketMessage(ws, joinHello("Bob", { pass: PASS32 }));
    return ws;
  });
  assert.equal(joined.sent[0].t, "welcome");
});

test("部屋の作成の回数: 秘密鍵が無いとき・接続元が無いときは数えずに通す（fail-open。ログに秘密鍵を出さない）", async () => {
  const { env } = directoryFor();
  const logs = [];
  const saved = console.error;
  console.error = (...args) => logs.push(args.join(" "));
  try {
    // 秘密鍵が無い（fakeRoom の既定の秘密鍵を打ち消す）
    for (let i = 0; i < LIMITS.createPerMinute + 3; i++) assert.ok(created(await createFrom("203.0.113.10", { TURNSTILE_SECRET: undefined, ROOM_DIRECTORY: env() })), "秘密鍵が無ければ通す");
    // 秘密鍵はあるが、接続元が無い（IP が無い）
    const { room, ctx } = fakeRoom({ TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: env() });
    room.roomId = "roomnoip0001";
    const ws = connect(ctx, "n1");
    await room.webSocketMessage(ws, createHello("Alice"));
    assert.ok(created(ws), "接続元が無ければ通す");
  } finally {
    console.error = saved;
  }
  assert.ok(logs.length >= 1, "秘密鍵が無いことは、ログに残す");
  assert.equal(logs.join("\n").includes("test-secret"), false);
});

test("部屋の作成の回数: 数える側（RoomDirectory）が例外を出しても、作成は通す（fail-open。ログは要約だけで IP を出さない）", async () => {
  const broken = { idFromName: (n) => n, get: () => ({ reserveRoom: async () => { throw new Error("storage down"); } }) };
  const logs = [];
  const saved = console.error;
  console.error = (...args) => logs.push(args.join(" "));
  try {
    const ws = await createFrom("203.0.113.11", { TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: broken });
    assert.ok(created(ws), "数えられなくても作れる");
  } finally {
    console.error = saved;
  }
  assert.ok(logs.some(l => l.includes("storage down")), "要約は残す");
  assert.equal(logs.join("\n").includes("203.0.113.11"), false, "IP は出さない");
  // 数えた接続元の識別子（HMAC のハッシュ）も、ログには出さない
  const hashKey = await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "203.0.113.11" }) }, { TURNSTILE_SECRET: "test-secret" });
  assert.ok(hashKey && !logs.join("\n").includes(hashKey), "接続元の識別子（ハッシュ）は出さない");
});

test("部屋の作成の回数: 接続元の識別子は秘密鍵つきの HMAC（IP から作ったものでも、秘密鍵が違えば別の値。IP そのものは含まない）", async () => {
  const a = await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "203.0.113.12" }) }, { TURNSTILE_SECRET: "one-secret" });
  const b = await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "203.0.113.12" }) }, { TURNSTILE_SECRET: "two-secret" });
  assert.match(a, /^[0-9a-f]{32}$/);
  assert.notEqual(a, b, "秘密鍵が違えば別の値");
  assert.equal(a.includes("203"), false);
  assert.equal(await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "2001:db8:0:1::5" }) }, { TURNSTILE_SECRET: "one-secret" }),
    await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "2001:db8:0:1:ffff::9" }) }, { TURNSTILE_SECRET: "one-secret" }), "IPv6 は /64 で同じ");
  assert.equal(await roomCreateKeyOf({ headers: new Headers({ "CF-Connecting-IP": "203.0.113.12" }) }, {}), null, "秘密鍵が無ければ null");
});

test("RoomDirectory の掃除（alarm）: 前の日の行と、1 分の窓の外の行を消す。今日の行は残す", async () => {
  const dirStorage = fakeStorage();
  const dir = new RoomDirectory({ storage: dirStorage }, {});
  const t0 = T0;
  await withClock(t0, async () => { await dir.hitCreate("a".repeat(32)); });
  await withClock(t0 + 60_000, async () => { await dir.alarm(); });
  assert.equal(dirStorage.m.has(`c:${"a".repeat(32)}`), true, "今日の行は残す（1 日の数えに使う）");
  await withClock(nextJstMidnight(new Date(t0)).getTime() + 1000, async () => { await dir.alarm(); });
  assert.equal(dirStorage.m.has(`c:${"a".repeat(32)}`), false, "次の日の 0 時に消す");
});

test("RoomDirectory: 不正な識別子（16 進 32 字でない）は数えず、許可しない", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  assert.equal((await dir.hitCreate("203.0.113.5")).allowed, false);
  assert.equal((await dir.hitCreate("a".repeat(31))).allowed, false);
});

test("鍵の間違いの数え方: 同時の 5 回の間違いも取りこぼさない（直列に数える）。5 回目で拒否に入る", async () => {
  const { room, ctx } = fakeRoom();
  const owner = connect(ctx, "a1");
  await room.webSocketMessage(owner, createHello("Alice", { pass: PASS32 }));
  const wrongs = await withUpgradeStubs(async (made) => {
    const socks = [];
    for (let i = 0; i < LIMITS.passFailMax; i++) {
      await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.9" } }));
      socks.push(made.at(-1));
    }
    await Promise.all(socks.map(ws => room.webSocketMessage(ws, joinHello("Bob", { pass: "まちがい" }))));
    return socks;
  });
  assert.ok(wrongs.every(ws => ws.sent[0]?.t === "wrongpass"));
  const next = await withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.9" } }));
    const ws = made.at(-1);
    await room.webSocketMessage(ws, joinHello("Bob", { pass: PASS32 }));
    return ws;
  });
  assert.equal(next.sent[0].t, "passlimit", "同時でも 5 回として数え、正しい鍵でも拒否する");
});

// ---------------------------------------------------------------- レビューの指摘への対応（旧形式の部屋・一覧の上限・鍵の間違いの鍵・同時の作成の数え方）
test("旧形式の部屋に参加者がいても、第三者の hello で参加者は閉じられない（入ろうとした接続だけ expired）。部屋のデータは消えない", async () => {
  const { room, ctx, storage } = fakeRoom();
  storage.set("init", true);
  storage.set("meta", { name: "旧い部屋", createdAt: T0, pass: { salt: "00".repeat(16), iter: 100000, hash: "11".repeat(32) }, ownerHash: "22".repeat(32) });
  storage.set("opAt", T0);
  storage.set("o:cube0", { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] });
  const peer = connect(ctx, "p1");
  peer.serializeAttachment({ id: "p1", color: "#f97316", name: "Peer", joined: true, at: T0 });
  const stranger = connect(ctx, "s1");
  await room.webSocketMessage(stranger, joinHello("Mallory", { pass: PASS32 }));
  assert.deepEqual(stranger.sent, [{ t: "closed", reason: "expired" }]);
  assert.equal(stranger.closed?.code, 4004);
  assert.equal(peer.sent.some(m => m.t === "closed"), false, "参加者は閉じられない");
  assert.equal(peer.closed, null);
  assert.equal(room.gone, false);
  assert.equal(storage.has("o:cube0"), true, "部屋のデータは消えない");
});

test("旧形式の部屋で参加者が 0 人なら、入ろうとした人が閉じ、部屋の中身も消す", async () => {
  const { room, ctx, storage } = fakeRoom();
  storage.set("init", true);
  storage.set("meta", { name: "旧", createdAt: T0, pass: null, ownerHash: "22".repeat(32) });
  storage.set("opAt", T0);
  storage.set("o:cube0", { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] });
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob", { pass: PASS32 }));
  assert.deepEqual(b.sent, [{ t: "closed", reason: "expired" }]);
  assert.equal(storage.has("o:cube0"), false);
});

test("鍵の間違い: 秘密鍵が無いときは数えない（fail-open。合っていれば入れる）。秘密鍵があるときは、接続元の鍵は秘密鍵つき HMAC（32 字）で、IP の SHA-256 ではない", async () => {
  const { createHash } = await import("node:crypto");
  const dir = directoryFor();
  // 秘密鍵が無い: 何度間違えても passlimit にならない
  const noKey = fakeRoom({ TURNSTILE_SECRET: undefined, ROOM_DIRECTORY: dir.env() });
  noKey.room.roomId = ROOM;
  await withUpgradeStubs(async (made) => {
    await noKey.room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.20" } }));
    await noKey.room.webSocketMessage(made.at(-1), createHello("Alice", { pass: PASS32 }));
  });
  for (let i = 0; i < LIMITS.passFailMax + 2; i++) {
    const w = await withUpgradeStubs(async (made) => {
      await noKey.room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.20" } }));
      await noKey.room.webSocketMessage(made.at(-1), joinHello("Bob", { pass: "まちがい" }));
      return made.at(-1);
    });
    assert.equal(w.sent[0].t, "wrongpass", `試行 ${i + 1}: 数えないので passlimit にならない`);
  }
  // 秘密鍵がある: 行の鍵は HMAC（32 字）。IP から作った素の SHA-256 の行は無い
  const withKey = fakeRoom({ TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: dir.env() });
  withKey.room.roomId = ROOM;
  await withUpgradeStubs(async (made) => {
    await withKey.room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.21" } }));
    await withKey.room.webSocketMessage(made.at(-1), createHello("Alice", { pass: PASS32 }));
    await withKey.room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": "203.0.113.21" } }));
    await withKey.room.webSocketMessage(made.at(-1), joinHello("Bob", { pass: "まちがい" }));
  });
  const failKeys = [...withKey.storage.keys()].filter(k => k.startsWith("fail:"));
  assert.equal(failKeys.length, 1);
  assert.match(failKeys[0], /^fail:[0-9a-f]{32}$/);
  const unkeyed = createHash("sha256").update(netKey("203.0.113.21")).digest("hex").slice(0, 16);
  assert.equal(failKeys.includes(`fail:${unkeyed}`), false, "秘密鍵の無い SHA-256 の鍵は使わない");
});

test("IPv6 の大文字と小文字は同じ接続元（netKey は小文字にそろえる）", () => {
  assert.equal(netKey("2001:DB8:0:1::5"), netKey("2001:db8:0:1::5"));
});

test("同時の 6 本の作成（hitCreate）: 1 分の上限の分だけ通り、残りは弾かれる（直列に数える）", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  const hash = "a".repeat(32);
  const results = await withClock(T0, async () => Promise.all(Array.from({ length: 6 }, () => dir.hitCreate(hash, T0))));
  assert.equal(results.filter(r => r.allowed).length, LIMITS.createPerMinute);
  assert.equal(results.filter(r => !r.allowed).length, 6 - LIMITS.createPerMinute);
});

test("一覧の上限: 一覧に出る登録（1 人以上・猶予内）で上限に達したら、新しい登録は入らない。期限切れの行や 0 人の古い行は数えず、消す", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  const now = T0;
  const entry = (i, count, createdAt, updatedAt = now) => ({ id: `room${String(i).padStart(6, "0")}`, name: "n", count, max: LIMITS.participants, locked: true, createdAt, updatedAt });
  // 1 人以上の行で上限まで埋める
  for (let i = 0; i < LIMITS.listStoreMax; i++) assert.equal(await dir.upsert(entry(i, 1, now - 1000), now), true);
  assert.equal(await dir.upsert(entry(900, 1, now - 1000), now), false, "一覧に出る行で上限なら、新しい登録は入らない");
  // 期限切れの行（更新が途絶えた）は、上限の数に入らず消える
  const dir2 = new RoomDirectory({ storage: fakeStorage() }, {});
  for (let i = 0; i < LIMITS.listStoreMax; i++) await dir2.upsert(entry(i, 1, now - 1000, now - LIMITS.listTtlMs - 1), now - LIMITS.listTtlMs - 1);
  assert.equal(await dir2.upsert(entry(900, 1, now - 1000), now), true, "期限切れの行で埋まっていれば、登録できる");
  // 0 人で猶予を過ぎた行は、一覧に出ないので上限に数えない
  const dir3 = new RoomDirectory({ storage: fakeStorage() }, {});
  for (let i = 0; i < LIMITS.listStoreMax; i++) await dir3.upsert(entry(i, 0, now - LIMITS.listGraceMs - 1), now);
  assert.equal(await dir3.upsert(entry(900, 1, now - 1000), now), true, "一覧に出ない行は数えない");
});

// ---------------------------------------------------------------- 1 分 1 部屋・1 日 3 部屋、全体で最大 8 部屋（ユーザー決定）
/** 接続元 ip から部屋を 1 つ作り、その部屋（Room）と接続を返す。 */
async function openAt(ip, env, roomId = "roomaaaa" + Math.random().toString(36).slice(2, 8).padEnd(6, "x")) {
  const { room, ctx } = fakeRoom(env);
  room.roomId = roomId;
  const ws = await withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": ip } }));
    const w = made.at(-1);
    await room.webSocketMessage(w, createHello("Alice"));
    return w;
  });
  return { room, ws, ctx };
}
const fullRoom = (ws) => ws.sent.some(m => m.t === "closed" && m.reason === "full") && ws.closed?.code === 4007;
const roomEnv = (dir, extra = {}) => ({ TURNSTILE_SECRET: "test-secret", ROOM_DIRECTORY: dir.env(), ...extra });

test("全体の上限: 同時に存在する部屋は 8 部屋まで。9 部屋目は full（4007）で閉じる（接続元が違えば、枠の数だけ作れる）", async () => {
  const dir = directoryFor();
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) assert.ok(created(await openAt("203.0.113." + (30 + i), roomEnv(dir)).then(r => r.ws)), i + 1 + " 部屋目は作れる");
  const ninth = (await openAt("203.0.113.90", roomEnv(dir))).ws;
  assert.ok(fullRoom(ninth), "9 部屋目は作れない");
  assert.equal(ninth.sent.at(-1).reason, "full");
});

test("全体の上限: 部屋が閉じると枠が空き、9 部屋目が作れる（閉じた部屋の予約は消える）", async () => {
  const dir = directoryFor();
  const first = [];
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) first.push(await openAt("203.0.113." + (40 + i), roomEnv(dir)));
  assert.ok(fullRoom((await openAt("203.0.113.99", roomEnv(dir))).ws));
  // 1 つ目の部屋を、作成者が閉じる（作成者の鍵の接続）
  first[0].ws.sent.length = 0;
  await first[0].room.webSocketMessage(first[0].ws, JSON.stringify({ t: "close" }));
  assert.ok(created(await openAt("203.0.113.100", roomEnv(dir)).then(r => r.ws)), "枠が空いたので作れる");
});

test("全体の上限: TTL（listTtlMs のあいだ更新が無い）で消えた予約は数えない", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) assert.equal((await dir.reserveRoom("roomaaaa" + String(i).padStart(2, "0"), null, T0)).allowed, true);
  assert.equal((await dir.reserveRoom("roombbbb0000", null, T0)).reason, "full");
  assert.equal((await dir.reserveRoom("roombbbb0000", null, T0 + LIMITS.listTtlMs + 1)).allowed, true, "更新が途絶えた予約は消えているので、作れる");
});

test("全体の上限: 予約を更新し続けている部屋は、TTL を過ぎても数える（touchRoom）", async () => {
  const dir = new RoomDirectory({ storage: fakeStorage() }, {});
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) await dir.reserveRoom("roomaaaa" + String(i).padStart(2, "0"), null, T0);
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) await dir.touchRoom("roomaaaa" + String(i).padStart(2, "0"), T0 + LIMITS.listTtlMs);
  assert.equal((await dir.reserveRoom("roombbbb0000", null, T0 + LIMITS.listTtlMs + 1000)).reason, "full");
});

test("全体の上限: 同時に 9 本の作成が来ても、作られる部屋は 8 つだけ（予約は直列）", async () => {
  const dir = directoryFor();
  // 接続は 1 本ずつ受け付け（WebSocketPair の試験用の差し替えを、同時に使わない）、作成の送信だけを同時にする
  const rooms = [];
  for (let i = 0; i < LIMITS.roomMaxTotal + 1; i++) rooms.push(await openAtPending("203.0.113." + (120 + i), roomEnv(dir)).accept());
  const results = await Promise.all(rooms.map(r => r.go()));
  assert.equal(results.filter(w => created(w)).length, LIMITS.roomMaxTotal);
  assert.equal(results.filter(w => fullRoom(w)).length, 1);
});

/** 接続を用意しておき、go() で create を送る（同時の試行のため）。 */
function openAtPending(ip, env) {
  const { room, ctx } = fakeRoom(env);
  room.roomId = "roomaaaa" + Math.random().toString(36).slice(2, 8).padEnd(6, "x");
  let ws = null;
  return {
    accept: async () => {
      ws = await withUpgradeStubs(async (made) => {
        await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": ip } }));
        return made.at(-1);
      });
      return { go: async () => { await room.webSocketMessage(ws, createHello("Alice")); return ws; } };
    },
    ctx,
  };
}

test("順序: 全体の上限で弾かれたときは、接続元の枠を消費しない（満員のあとで空いたら、すぐ作り直せる）", async () => {
  const dir = directoryFor();
  const filled = [];
  for (let i = 0; i < LIMITS.roomMaxTotal; i++) filled.push(await openAt("203.0.113." + (150 + i), roomEnv(dir)));
  const x = "203.0.113.200";
  assert.ok(fullRoom((await openAt(x, roomEnv(dir))).ws), "全体で満員");
  await filled[0].room.webSocketMessage(filled[0].ws, JSON.stringify({ t: "close" }));
  assert.ok(created((await openAt(x, roomEnv(dir))).ws), "同じ接続元の 1 分の枠は消費されていないので、すぐ作れる");
});

test("1 分 1 部屋: 2 部屋目は 1 分以内なら弾かれ（limit）、1 分後に通る", async () => {
  const dir = directoryFor();
  await withClock(T0, async (tick) => {
    assert.ok(created((await openAt("203.0.113.210", roomEnv(dir))).ws));
    assert.ok(limited((await openAt("203.0.113.210", roomEnv(dir))).ws), "1 分以内は弾く");
    tick(LIMITS.createWindowMs - 1000);
    assert.ok(limited((await openAt("203.0.113.210", roomEnv(dir))).ws), "1 分まで足りないあいだは、まだ弾く");
    tick(2000);
    assert.ok(created((await openAt("203.0.113.210", roomEnv(dir))).ws), "1 分たったら通る");
  });
});

test("1 日 3 部屋: 4 部屋目は、1 分の枠が空いていても日の上限で弾かれる（JST の暦日）", async () => {
  const dir = directoryFor();
  const day = nextJstMidnight(new Date(T0)).getTime() - 24 * 60 * 60 * 1000;
  await withClock(day + 60_000, async (tick) => {
    for (let i = 0; i < LIMITS.createPerDay; i++) {
      assert.ok(created((await openAt("203.0.113.220", roomEnv(dir))).ws), (i + 1) + " 部屋目は通る");
      tick(LIMITS.createWindowMs + 1000);
    }
    assert.ok(limited((await openAt("203.0.113.220", roomEnv(dir))).ws), "4 部屋目は日の上限で弾く");
  });
});

test("env の上限: ROOM_CREATE_PER_MINUTE・ROOM_CREATE_PER_DAY・ROOM_MAX_TOTAL で変えられる。範囲外・数でない値は既定", () => {
  assert.equal(perMinuteOf({ ROOM_CREATE_PER_MINUTE: "3" }), 3);
  assert.equal(perMinuteOf({ ROOM_CREATE_PER_MINUTE: "0" }), LIMITS.createPerMinute);
  assert.equal(perMinuteOf({ ROOM_CREATE_PER_MINUTE: "61" }), LIMITS.createPerMinute);
  assert.equal(perMinuteOf({ ROOM_CREATE_PER_MINUTE: "1.5" }), LIMITS.createPerMinute);
  assert.equal(perDayOf({ ROOM_CREATE_PER_DAY: "10" }), 10);
  assert.equal(perDayOf({ ROOM_CREATE_PER_DAY: "x" }), LIMITS.createPerDay);
  assert.equal(maxTotalOf({ ROOM_MAX_TOTAL: "50" }), 50);
  assert.equal(maxTotalOf({ ROOM_MAX_TOTAL: "51" }), LIMITS.roomMaxTotal);
  assert.equal(maxTotalOf({}), 8);
});
