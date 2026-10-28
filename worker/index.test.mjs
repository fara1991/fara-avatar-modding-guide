// worker/index.js の入口（fetch）のテスト。  node --test worker/index.test.mjs
// index.js は Cloudflare の部品（cloudflare:workers）を読み込むので、Node では、その名前だけを空の実装に差し替えて読み込む。
// 確かめること: メンテナンスの状態の API・503・報告の受け口・静的ファイルの素通し（HTML を書き換えない）・知らない /api/* は 404。
import { test, before } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";

let worker, Room, cleanName;

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
  assert.equal((await call("/api/rooms/abcdef12/ws", env)).status, 426);
  const foreign = await call("/api/rooms/abcdef12/ws", env, { headers: { Upgrade: "websocket", Origin: "https://evil.example" } });
  assert.equal(foreign.status, 403);
  const same = await call("/api/rooms/abcdef12/ws", env, { headers: { Upgrade: "websocket", Origin: "https://avatar-dev.example" } });
  assert.equal(await same.text(), "room");
});

// ---------------------------------------------------------------- 共同練習の部屋（Room を、偽の ctx と WebSocket で動かす）
/** Durable Object の ctx の最小限の偽物 */
function fakeRoom() {
  const sockets = [];
  const storage = new Map();
  const ctx = {
    storage: {
      list: async () => new Map(),
      get: async (k) => storage.get(k),
      put: async (o, v) => { if (typeof o === "string") storage.set(o, v); else for (const [k, x] of Object.entries(o)) storage.set(k, x); },
      delete: async (keys) => { for (const k of [].concat(keys)) storage.delete(k); },
      setAlarm: async () => {},
      deleteAll: async () => storage.clear(),
    },
    setWebSocketAutoResponse() {},
    getWebSocketAutoResponseTimestamp() { return null; },
    getWebSockets: () => sockets,
    acceptWebSocket(ws) { sockets.push(ws); },
  };
  return { room: new Room(ctx, {}), ctx };
}

/** 接続 1 本。送られた JSON は sent に溜まる */
function connect(ctx, id) {
  let attachment = null;
  const ws = {
    readyState: 1, sent: [], closed: null,
    send(d) { this.sent.push(JSON.parse(d)); },
    close(code, reason) { this.closed = { code, reason }; this.readyState = 3; },
    serializeAttachment(a) { attachment = a; },
    deserializeAttachment() { return attachment; },
  };
  ctx.acceptWebSocket(ws);
  ws.serializeAttachment({ id, color: "#f97316", name: "", joined: false, at: Date.now() });
  return ws;
}

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

test("表示名: HTML の特殊文字と制御文字を除く（クライアントの C# と同じ）", () => {
  assert.equal(cleanName('<a>&"\'b'), "ab");
  assert.equal(cleanName("  Ali\u0007ce  "), "Alice");
});
