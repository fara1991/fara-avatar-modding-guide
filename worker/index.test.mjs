// worker/index.js の入口（fetch）のテスト。  node --test worker/index.test.mjs
// index.js は Cloudflare の部品（cloudflare:workers）を読み込むので、Node では、その名前だけを空の実装に差し替えて読み込む。
// 確かめること: メンテナンスの状態の API・503・報告の受け口・静的ファイルの素通し（HTML を書き換えない）・知らない /api/* は 404。
import { test, before } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";

let worker, Room, RoomDirectory, parseCreate, cleanName, LIMITS, PROD_ITERATIONS, netKey, passIterationsOf;

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
  passIterationsOf = mod.passIterationsOf;
  parseCreate = mod.parseCreate;
  cleanName = mod.cleanName;
  LIMITS = mod.LIMITS;
  // 鍵の反復回数は本番では 100000（Workers の上限）。テストでは速くするため下げる（本番の値は PROD_ITERATIONS で確かめる）
  PROD_ITERATIONS = LIMITS.passIterations;
  LIMITS.passIterations = 1000;
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
  return { room: new Room(ctx, env), ctx, alarms, storage };
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

const hello = (name) => JSON.stringify({ t: "hello", name, owner: "0123456789abcdef0123456789abcdef", create: { name: "練習の部屋", listed: false } });
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
const ping = JSON.stringify({ t: "ping" });
const createHello = (name, { owner = OWNER, listed = false, pass } = {}) =>
  JSON.stringify({ t: "hello", name, owner, create: { name: "練習の部屋", listed, ...(pass === undefined ? {} : { pass }) } });
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
  return {
    m,
    get: async (k) => m.get(k),
    put: async (o, v) => { if (typeof o === "string") m.set(o, v); else for (const [k, x] of Object.entries(o)) m.set(k, x); },
    delete: async (keys) => { for (const k of [].concat(keys)) m.delete(k); },
    list: async ({ prefix = "" } = {}) => new Map([...m].filter(([k]) => k.startsWith(prefix))),
    setAlarm: async () => {},
    deleteAll: async () => m.clear(),
  };
}

test("作成: 一覧に出す（listed）と出さない（既定）を選べる。一覧の登録は、listed のときだけ、ID と部屋名つきで行う", async () => {
  const dir = fakeDirectory();
  const { room, ctx } = fakeRoom({ ROOM_DIRECTORY: dir });
  room.roomId = ROOM;
  const a = connect(ctx, "a1");
  await withClock(T0, async () => {
    await room.webSocketMessage(a, createHello("Alice", { listed: true, pass: "" }));
    assert.equal(a.sent.find(m => m.t === "welcome").room.name, "練習の部屋");
    assert.equal(a.sent.find(m => m.t === "welcome").you.owner, true);
    assert.deepEqual(dir.last(), { id: ROOM, name: "練習の部屋", count: 1, max: LIMITS.participants, locked: false, createdAt: T0 });
  });
  const dir2 = fakeDirectory();
  const h = fakeRoom({ ROOM_DIRECTORY: dir2 });
  h.room.roomId = "roomaaaa2222";
  const b = connect(h.ctx, "b1");
  await h.room.webSocketMessage(b, createHello("Bob", { listed: false }));
  assert.equal(dir2.calls.length, 0, "一覧に出さない部屋は、登録しない");
});

test("作成: 鍵つきは locked:true で登録し、作成者は鍵の入力なしで入れる（welcome を受け取る）", async () => {
  const dir = fakeDirectory();
  const { room, ctx } = fakeRoom({ ROOM_DIRECTORY: dir });
  room.roomId = ROOM;
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { listed: true, pass: "合言葉123" }));
  assert.ok(a.sent.some(m => m.t === "welcome"));
  assert.equal(dir.last().locked, true);
  assert.equal(a.closed, null);
});

test("鍵つき: 鍵が無ければ needpass で 4003 に閉じる。中身（welcome・参加者・オブジェクト）は一切送らない", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: "合言葉123" }));
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob"));
  assert.deepEqual(b.sent, [{ t: "needpass" }]);
  assert.equal(b.closed?.code, 4003);
});

test("鍵つき: 違う鍵は wrongpass で 4003。正しい鍵なら welcome を受け取る", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: "合言葉123" }));
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, joinHello("Bob", { pass: "まちがい" }));
  assert.deepEqual(b.sent, [{ t: "wrongpass" }]);
  assert.equal(b.closed?.code, 4003);
  const c = connect(ctx, "c1");
  await room.webSocketMessage(c, joinHello("Carol", { pass: "合言葉123" }));
  assert.equal(c.sent[0].t, "welcome");
  assert.equal(c.sent[0].room.locked, true);
  assert.equal(c.sent[0].peers.length, 1);
  assert.equal(c.sent[0].objects.length, 1);
});

test("鍵: 間違いが passFailMax 回続くと、正しい鍵でも passlimit で拒否し、時間が過ぎると入れる（接続元ごと）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(T0, async (tick) => {
    await room.webSocketMessage(a, createHello("Alice", { pass: "合言葉123" }));
    for (let i = 0; i < LIMITS.passFailMax; i++) {
      const b = connect(ctx, `w${i}`);
      await room.webSocketMessage(b, joinHello("Bob", { pass: "x" }));
      assert.equal(b.sent[0].t, "wrongpass");
    }
    const blocked = connect(ctx, "blocked");
    await room.webSocketMessage(blocked, joinHello("Bob", { pass: "合言葉123" }));
    assert.equal(blocked.sent[0].t, "passlimit");
    assert.ok(blocked.sent[0].ms > 0 && blocked.sent[0].ms <= LIMITS.passBlockMs);
    assert.equal(blocked.closed?.code, 4003);
    tick(LIMITS.passBlockMs + 1);
    const ok = connect(ctx, "ok");
    await room.webSocketMessage(ok, joinHello("Bob", { pass: "合言葉123" }));
    assert.equal(ok.sent[0].t, "welcome");
  });
});

test("鍵の保存: 平文は残さない（ソルト・反復回数・ハッシュだけ）。本番の反復回数は Workers の上限 100000", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: "秘密のパス123" }));
  const meta = storage.get("meta");
  assert.equal(meta.pass.salt.length, 32);
  assert.equal(meta.pass.hash.length, 64);
  assert.equal(meta.pass.iter, 1000);   // テストの反復回数（LIMITS.passIterations を下げている）
  assert.equal(PROD_ITERATIONS, 100000, "本番の反復回数は Workers の PBKDF2 の上限（100000）");
  assert.equal(JSON.stringify([...storage.entries()]).includes("秘密のパス123"), false);
});

test("作成は最初の hello だけ。既にある部屋では、create・owner・鍵を上書きできない", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { owner: OWNER, listed: false, pass: "一つ目の鍵" }));
  const before = storage.get("meta");
  const b = connect(ctx, "b1");
  await room.webSocketMessage(b, createHello("Mallory", { owner: OTHER, listed: true, pass: "" }));
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
  await room.webSocketMessage(other, joinHello("Bob", { owner: OTHER }));
  assert.equal(other.sent.find(m => m.t === "welcome").you.owner, false);
});

test("Close: 作成者だけが閉じられる。作成者でない人の close は、閉じずに not owner を返す", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b1");
  await room.webSocketMessage(a, createHello("Alice", { listed: true }));
  await room.webSocketMessage(b, joinHello("Bob"));
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
    await room.webSocketMessage(a, createHello("Alice", { listed: true }));
    await room.webSocketMessage(b, joinHello("Bob"));
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
    await room.webSocketMessage(b, joinHello("Bob"));
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
    await room.webSocketMessage(a, createHello("Alice", { listed: true }));
    assert.equal(dir.last().count, 1);
    const b = connect(ctx, "b1");
    await room.webSocketMessage(b, joinHello("Bob"));
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
  await room.webSocketMessage(a, createHello("Alice", { listed: true }));
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

test("部屋の作成の指定: 部屋名は 40 字・HTML の特殊文字を除き、空なら既定の名前。鍵は 64 字まで。作成者の鍵は 16 進 32 字", () => {
  assert.equal(parseCreate({ name: "  <b>練習</b>  ", listed: true }, OWNER).name, "b練習/b");
  assert.equal(parseCreate({ name: "", listed: false }, OWNER).name, "練習の部屋");
  assert.equal(parseCreate({ name: "あ".repeat(60), listed: false }, OWNER).name.length, 40);
  assert.equal(parseCreate({ listed: false, pass: "a".repeat(64) }, OWNER).pass.length, 64);
  assert.equal(parseCreate({ listed: false, pass: "a".repeat(65) }, OWNER), null, "65 字は作れない");
  assert.equal(parseCreate({ listed: "yes" }, OWNER), null);
  assert.equal(parseCreate({ listed: false }, "short"), null, "作成者の鍵の形が違えば作れない");
  assert.equal(parseCreate({ listed: false, pass: "" }, OWNER).pass, null, "空の鍵は鍵なし");
});

test("作成の指定が不正なら bad create（部屋は作らない）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice", owner: OWNER, create: { listed: "yes" } }));
  assert.deepEqual(a.sent.at(-1), { t: "error", msg: "bad create" });
  assert.equal(storage.has("init"), false);
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
      await room.webSocketMessage(a, joinHello("Alice"));   // 作成の指定は無い（既存の部屋には入れる）
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
  await room.webSocketMessage(a, joinHello("Alice"));
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
    room.webSocketMessage(b, createHello("Mallory", { owner: OTHER, listed: true })),
  ]);
  const owners = [a, b].filter(s => s.sent.find(m => m.t === "welcome")?.you.owner === true);
  assert.equal(owners.length, 1, "作成者は 1 人");
  const winner = owners[0] === a ? OWNER : OTHER;
  const { createHash } = await import("node:crypto");
  assert.equal(storage.get("meta").ownerHash, createHash("sha256").update(winner).digest("hex"));
  assert.equal(storage.get("meta").listed, false, "後から来た listed:true は効かない");
});

test("同時の参加: 9 人が同時に入っても、8 人までしか入らない（人数の確認と参加の印の間に await を挟まない）", async () => {
  const { room, ctx } = fakeRoom();
  const first = connect(ctx, "c0");
  await room.webSocketMessage(first, createHello("Owner"));
  const socks = Array.from({ length: 8 }, (_, i) => connect(ctx, `j${i}`));
  await Promise.all(socks.map((s, i) => room.webSocketMessage(s, joinHello(`P${i}`))));
  const all = [first, ...socks];
  assert.equal(all.filter(s => s.sent.some(m => m.t === "welcome")).length, LIMITS.participants);
  assert.equal(all.filter(s => s.closed?.code === 4001).length, 1, "あふれた 1 人は満員で閉じる");
});

test("鍵の免除: 作成者の鍵が合う接続は、鍵が無くても入れる（鍵を忘れた作成者でも入れる）。ほかの人は鍵が要る", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, createHello("Alice", { pass: "合言葉123" }));
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
  await room.webSocketMessage(owner, createHello("Alice", { pass: "合言葉123" }));
  // 接続元の IP を付けて接続し、hello を送る（withUpgradeStubs で、接続の受け付けの形を作る）
  const from = (ip, pass) => withUpgradeStubs(async (made) => {
    await room.fetch(new Request("https://avatar-dev.example/", { headers: { "CF-Connecting-IP": ip } }));
    const ws = made.at(-1);
    await room.webSocketMessage(ws, joinHello("Bob", { pass }));
    return ws;
  });
  for (let i = 0; i < LIMITS.passFailMax; i++) assert.equal((await from("2001:db8:0:1::1", "まちがい")).sent[0].t, "wrongpass");
  assert.equal((await from("2001:db8:0:1:ffff::9", "合言葉123")).sent[0].t, "passlimit", "同じ /64 の別の住所も拒否");
  assert.equal((await from("192.0.2.7", "合言葉123")).sent[0].t, "welcome", "別の接続元（IPv4）は影響を受けない");
});

test("鍵の間違いの行: 期限の切れた行は、次の間違いのときに消す（行が増え続けない）", async () => {
  const { room, ctx, storage } = fakeRoom();
  const owner = connect(ctx, "a1");
  await room.webSocketMessage(owner, createHello("Alice", { pass: "合言葉123" }));
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

test("鍵の反復回数: ROOM_PASS_ITERATIONS は 10000〜100000 の整数だけ受け付け、それ以外は既定", () => {
  assert.equal(passIterationsOf({}), LIMITS.passIterations);
  assert.equal(passIterationsOf({ ROOM_PASS_ITERATIONS: "50000" }), 50000);
  assert.equal(passIterationsOf({ ROOM_PASS_ITERATIONS: "5" }), LIMITS.passIterations);
  assert.equal(passIterationsOf({ ROOM_PASS_ITERATIONS: "200000" }), LIMITS.passIterations);
  assert.equal(passIterationsOf({ ROOM_PASS_ITERATIONS: "12.5" }), LIMITS.passIterations);
});

test("鍵の間違いの数え方: 同時の 5 回の間違いも取りこぼさない（直列に数える）。5 回目で拒否に入る", async () => {
  const { room, ctx } = fakeRoom();
  const owner = connect(ctx, "a1");
  await room.webSocketMessage(owner, createHello("Alice", { pass: "合言葉123" }));
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
    await room.webSocketMessage(ws, joinHello("Bob", { pass: "合言葉123" }));
    return ws;
  });
  assert.equal(next.sent[0].t, "passlimit", "同時でも 5 回として数え、正しい鍵でも拒否する");
});
