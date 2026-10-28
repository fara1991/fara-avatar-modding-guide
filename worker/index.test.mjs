// worker/index.js の入口（fetch）のテスト。  node --test worker/index.test.mjs
// index.js は Cloudflare の部品（cloudflare:workers）を読み込むので、Node では、その名前だけを空の実装に差し替えて読み込む。
// 確かめること: メンテナンスの状態の API・503・報告の受け口・静的ファイルの素通し（HTML を書き換えない）・知らない /api/* は 404。
import { test, before } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";

let worker, Room, cleanName, LIMITS;

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
  assert.ok(!html.includes("maintenance-notice") && !html.includes("fsk-notice") && !html.includes("更新します"));
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
function fakeRoom() {
  const sockets = [];
  const storage = new Map();
  const alarms = [];   // setAlarm で渡された時刻（DO のアラームは 1 つで、呼ぶたびに上書きされる）
  const ctx = {
    storage: {
      list: async () => new Map(),
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
  return { room: new Room(ctx, {}), ctx, alarms, storage };
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

const hello = (name) => JSON.stringify({ t: "hello", name });
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
  await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice" }));
  await room.webSocketMessage(b, JSON.stringify({ t: "hello", name: "Bob" }));
  await room.webSocketMessage(b, JSON.stringify({ t: "hello", name: "Bob" }));
  assert.equal(a.sent.filter(m => m.t === "join").length, 1);
  assert.equal(b.sent.filter(m => m.t === "welcome").length, 1);
});

test("共同練習: upsert が無い ops でも delete は反映する（無い・null は空）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice" }));
  await room.webSocketMessage(a, JSON.stringify({ t: "ops", delete: ["cube0"] }));
  assert.equal(room.objects.has("cube0"), false);
  assert.equal(a.sent.some(m => m.t === "error"), false);
  const sent = connect(ctx, "b2");
  await room.webSocketMessage(sent, JSON.stringify({ t: "hello", name: "Bob" }));
  await room.webSocketMessage(sent, JSON.stringify({ t: "ops", upsert: null, delete: [] }));
  assert.equal(sent.sent.some(m => m.t === "error"), false);
});

test("共同練習: upsert が配列でない ops は、delete も含めて丸ごと捨てる（黙って空にしない）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice" }));
  await room.webSocketMessage(a, JSON.stringify({ t: "ops", upsert: "x", delete: ["cube0"] }));
  assert.equal(room.objects.has("cube0"), true);
  assert.deepEqual(a.sent.at(-1), { t: "error", msg: "bad ops" });
});

test("共同練習: 不正なメッセージは直近 1 分の中で数える（古いものは数えない）", async () => {
  const { room, ctx } = fakeRoom();
  const a = connect(ctx, "a1");
  await withClock(1_000_000_000_000, async (tick) => {
    await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice" }));
    // 61 秒おきに 35 回: どの 1 分の窓にも 30 回は入らないので、切れない
    for (let i = 0; i < 35; i++) {
      tick(61_000);
      await room.webSocketMessage(a, JSON.stringify({ t: "bogus" }));
    }
    assert.equal(a.closed, null);
    // 1 秒の中で 30 回: 切れる
    const b = connect(ctx, "b2");
    await room.webSocketMessage(b, JSON.stringify({ t: "hello", name: "Bob" }));
    for (let i = 0; i < 30; i++) await room.webSocketMessage(b, JSON.stringify({ t: "bogus" }));
    assert.equal(b.closed?.code, 1008);
  });
});

test("共同練習: ロックの期限が切れたら、アラームでほかの人へ locks を送る（古いロックを見せ続けない）", async () => {
  const { room, ctx, alarms } = fakeRoom();
  const a = connect(ctx, "a1");
  const b = connect(ctx, "b2");
  await withClock(1_000_000_000_000, async (tick) => {
    await room.webSocketMessage(a, JSON.stringify({ t: "hello", name: "Alice" }));
    await room.webSocketMessage(b, JSON.stringify({ t: "hello", name: "Bob" }));
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
