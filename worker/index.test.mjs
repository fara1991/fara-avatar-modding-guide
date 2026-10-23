// worker/index.js の入口（fetch）のテスト。  node --test worker/index.test.mjs
// index.js は Cloudflare の部品（cloudflare:workers）を読み込むので、Node では、その名前だけを空の実装に差し替えて読み込む。
// 確かめること: メンテナンスの状態の API・503・報告の受け口・静的ファイルの素通し（HTML を書き換えない）・知らない /api/* は 404。
import { test, before } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";

let worker;

before(async () => {
  // "cloudflare:workers" を、DurableObject だけを持つ空のモジュールにする
  const hooks = `
    export async function resolve(specifier, context, next) {
      if (specifier === "cloudflare:workers") return { url: "data:text/javascript,export class DurableObject { constructor(ctx, env) { this.ctx = ctx; this.env = env; } }", shortCircuit: true };
      return next(specifier, context);
    }`;
  register("data:text/javascript," + encodeURIComponent(hooks), import.meta.url);
  worker = (await import("./index.js")).default;
});

/** 静的ファイル（ASSETS）の代わり。呼ばれたパスを記録し、HTML をそのまま返す */
function assets(body = "<!doctype html><html><body><div id=\"app\">静的ページ</div></body></html>") {
  const calls = [];
  return { calls, fetch: async (request) => { calls.push(new URL(request.url).pathname); return new Response(body, { headers: { "Content-Type": "text/html; charset=utf-8" } }); } };
}

const call = (path, env, init) => worker.fetch(new Request(`https://avatar-wasm-dev.example${path}`, init), env);

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
  const same = await call("/api/rooms/abcdef12/ws", env, { headers: { Upgrade: "websocket", Origin: "https://avatar-wasm-dev.example" } });
  assert.equal(await same.text(), "room");
});
