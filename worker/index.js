// Cloudflare Worker。すべてのリクエストがまずここを通る（wrangler.toml の run_worker_first = true）。
// - /api/rooms/<部屋ID>/ws は共同練習の部屋（Durable Object）へ
// - /api/rooms は練習の部屋の一覧（RoomDirectory。Durable Object）。JSON。Worker の中で数秒だけ覚える（listCacheMs）
// - /api/maintenance はメンテナンスの状態（{state, message}）。WebAssembly のアプリが起動時に読んで、お知らせの帯を出す（HTML には注入しない）
// - それ以外は静的ファイル（wrangler の assets。dotnet publish の wwwroot に、Build が書き足したもの）。確認用の noindex は、Build が _headers に付ける
//
// 共同練習の部屋（/practice/room）: 部屋ごとに 1 つの Durable Object（Room）が WebSocket をまとめ、
// オブジェクトモードのシーン（立方体などの種類・名前・位置・回転・スケール）を全員で共有する。
// - 遅れて入った人には、Durable Object に保存したシーンを丸ごと送る
// - 同じオブジェクトへの変更は、サーバーに届いた順（後勝ち）。操作中のオブジェクトはロックできる
// - 受け取ったメッセージはすべてここで形式と値の範囲を確かめる（不正なものは捨てる）
// - 作成は、最初の hello の create だけ（一覧に出すか・鍵・作成者の鍵）。作成後は変えられない。create の無い hello で部屋が無ければ、作らずに閉じる
// - 作成の処理は、部屋ごとに直列にする（同時の hello で、作成者や鍵が上書きされないように）
// - 作成者の鍵が合う接続は、鍵を免除する。ほかの人は、鍵が合うまで部屋の中身（welcome・参加者・オブジェクト）を送らない（needpass・wrongpass・passlimit のあと 4003 で閉じる）
// - 成功した操作（オブジェクトの変更・ロック・解除・移動の適用）が 30 分（ROOM_IDLE_SECONDS で変えられる）無ければ部屋を閉じる。2 分前に idlewarn で知らせる
// - 作成者（作成者の鍵を持つ接続）だけが「部屋を閉じる」（close）できる。閉じた部屋の ID は 10 分、「閉じた」目印で拒否する
// - ping は Durable Object を起こさずに自動で返す（WebSocket Hibernation）。応答の時刻で、切れた接続を見つけて外す
import { DurableObject } from "cloudflare:workers";
// 報告の受け口とメンテナンスの判定は、共通部品（fara-blazor-component）の同じファイルを使う（各サイトに同じコピーを持たない）
import { handleReport } from "../external/fara-blazor-component/worker/report.js";
import { getMaintenance, maintenanceResponse } from "../external/fara-blazor-component/worker/maintenance.js";
// 報告の 1 日の送信回数の制限（送信元ごと 3 回まで）の Durable Object。バインディング REPORT_LIMITER（wrangler.toml）があるときだけ働く
export { ReportRateLimiter } from "../external/fara-blazor-component/worker/report-rate-limit.js";

// ---------------------------------------------------------------- 上限
export const LIMITS = {
  participants: 8,          // 1 部屋の最大人数
  objects: 200,             // 1 部屋のオブジェクト数
  messageBytes: 16 * 1024,  // 1 メッセージの大きさ
  ratePerSec: 40,           // 1 人が 1 秒に送れるメッセージ数（平均）
  rateBurst: 80,            // 一度に送れる数
  nameLength: 20,           // 表示名の長さ
  objectNameLength: 40,
  coord: 1000,              // 位置の絶対値の上限（m）
  scaleMin: 0.001,
  scaleMax: 1000,
  lockMs: 15000,            // ロックの有効時間（操作が終わらないまま放置されたとき用）
  lockMaxMs: 2 * 60 * 1000, // 同じ人が同じオブジェクトを続けて持てる上限。超えたら解除して、ほかの人が取れるようにする（lockexpired）
  lockWarnMs: 20 * 1000,    // 上限のこれだけ前から、持っている人に知らせる（lockwarn）
  lockGapMs: 10 * 1000,     // 取り直しの間隔がこれ以内なら、同じ保持の続きとみなす（unlock → lock の繰り返しで上限を回避させない）
  pendingMax: 3,            // 参加前（hello の前）の接続の上限。超えたら、いちばん古い参加前の接続を閉じる
  pendingMs: 10 * 1000,     // 参加前のまま、これより長く経った接続は閉じる
  idleMs: 60 * 1000,        // これだけ何も届かない接続は切れたものとみなす（クライアントは 20 秒ごとに ping）
  sweepMs: 60 * 1000,       // 誰かがいるあいだ、切れた接続を確かめる間隔
  invalidBeforeClose: 30,   // 不正なメッセージが、下の時間窓の中でこれだけ届いたら切断
  invalidWindowMs: 60 * 1000, // 不正なメッセージを数える時間窓（長く開いた接続が、昔の分で切れないように）
  lockIds: 50,              // 1 回のロックで押さえる数・プレビューの数
  rotationMax: 100,         // 回転の絶対値の上限（度）
  viewDistMax: 500,         // 視点の距離の上限
  viewAngleMax: 1000,       // 視点の角度（theta）の絶対値の上限
  viewPhiMax: 4,            // 視点の角度（phi）の絶対値の上限
  viewNameLength: 8,        // 視点の先生役の名前の長さ
  roomNameLength: 40,       // 部屋名の長さ
  roomIdleMs: 30 * 60 * 1000, // 成功した操作（オブジェクトの変更・ロック・解除・移動）が無いまま、これだけたつと部屋を閉じる。ROOM_IDLE_SECONDS で変えられる
  roomWarnMs: 2 * 60 * 1000,  // 閉じる前に、これだけ前から idlewarn で知らせる（部屋の時間が短いときは、その半分まで）
  roomPersistMs: 10 * 1000,   // 最後の操作の時刻を storage に書く間隔（書き込みを減らす。再起動のとき、この分だけ早く閉じることがある）
  closedMs: 10 * 60 * 1000,   // 閉じた部屋の ID を拒否する時間（閉じた直後に同じ ID で作り直されて混乱しないように）
  listGraceMs: 60 * 1000,     // 作成直後は、参加者がまだいなくても一覧に出す時間
  listTtlMs: 3 * 60 * 1000,   // 一覧の登録が、この時間のあいだ Room から更新されなければ消す（Room が消えたあとに残らないように）
  listMax: 50,                // 一覧に返す件数
  listStoreMax: 200,          // 一覧に登録できる件数（これを超えたら登録しない）
  listCacheMs: 5 * 1000,      // 一覧を Worker の中で覚える時間（一覧は参考情報。古くても問題にならない）
  passMax: 64,                // 鍵の長さ（文字）
  passIterations: 100000,     // PBKDF2-SHA256 の反復回数（既定。Workers の Web Crypto の上限。ROOM_PASS_ITERATIONS で 10000〜100000 に変えられる）
  passFailMax: 5,             // 鍵の間違い（接続元ごと・部屋ごと）。これだけ続けると拒否する
  passFailWindowMs: 10 * 60 * 1000, // 間違いを数える時間窓
  passBlockMs: 10 * 60 * 1000,      // 拒否する時間
};

const KINDS = new Set(["plane", "cube", "sphere", "cylinder", "cone", "torus"]);
const COLORS = ["#f97316", "#22c55e", "#3b82f6", "#e879f9", "#facc15", "#14b8a6", "#ef4444", "#a78bfa"];
// 部屋の ID は 10〜32 字（作成 UI の乱数は 10 字）。手で付けた短い ID（推測しやすい・ほかの人と重なりやすい）は受け付けない
const ROOM_ID = /^[a-z0-9]{10,32}$/;
const OBJ_ID = /^[a-z0-9]{1,24}$/;
// 作成者の鍵（クライアントが作る 128 ビットの乱数。16 進 32 字）
const OWNER_KEY = /^[0-9a-f]{32}$/;
const DIRECTORY_NAME = "main";
// 一覧の覚え（Worker の isolate ごと。env.ROOM_DIRECTORY の束縛をキーにする）
const listCache = new WeakMap();

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    // メンテナンス（Worker の変数 MAINTENANCE_MODE。external/fara-blazor-component/worker/maintenance.js）。true の間は、すべて 503 のメンテナンス画面
    const maintenance = getMaintenance(env);
    if (maintenance.state === "on") return maintenanceResponse(request, maintenance, "Avatar Modding Guide");
    // メンテナンスの状態。アプリ（WebAssembly）が起動時に 1 回読む。キャッシュさせない（MAINTENANCE_MODE を変えたら、すぐ反映するため）
    if (url.pathname === "/api/maintenance") {
      return new Response(JSON.stringify({ state: maintenance.state, message: maintenance.message }), {
        headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
      });
    }
    const m = url.pathname.match(/^\/api\/rooms\/([^/]+)\/ws$/);
    if (m) {
      if (!ROOM_ID.test(m[1])) return new Response("bad room id", { status: 400 });
      if (request.headers.get("Upgrade") !== "websocket") return new Response("expected websocket", { status: 426 });
      // 別のサイトのページから部屋につながれないように、同じホストからの接続だけ受け付ける
      const origin = request.headers.get("Origin");
      if (origin && originHost(origin) !== url.host) return new Response("forbidden", { status: 403 });
      const stub = env.ROOMS.get(env.ROOMS.idFromName(m[1]));
      // 部屋の ID を Durable Object に渡す（一覧への登録・取り消しに使う）
      const forwarded = new Request(request);
      forwarded.headers.set("X-Room-Id", m[1]);
      return stub.fetch(forwarded);
    }
    // 練習の部屋の一覧（JSON。公開の情報だけ）。同じホストからの GET だけ
    if (url.pathname === "/api/rooms") {
      if (request.method !== "GET") return new Response("method not allowed", { status: 405, headers: { Allow: "GET" } });
      const origin = request.headers.get("Origin");
      if (origin && originHost(origin) !== url.host) return new Response("forbidden", { status: 403 });
      const rooms = await roomList(env);
      return new Response(JSON.stringify({ rooms, limit: LIMITS.listMax }), {
        headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
      });
    }
    // 作者への報告の受け口（external/fara-blazor-component/worker/report.js）
    if (url.pathname === "/api/report" || url.pathname === "/api/report/config") {
      try {
        return await handleReport(request, env, "Avatar Modding Guide");
      } catch {
        return new Response("internal error", { status: 500 });
      }
    }
    if (url.pathname.startsWith("/api/")) return new Response("not found", { status: 404 });
    // 静的ファイル。お知らせ（MAINTENANCE_MODE=notice）の帯は、アプリ側が /api/maintenance を読んで出す（HTML の書き換えはしない）
    return env.ASSETS.fetch(request);
  },
};

/** 一覧（RoomDirectory）。LIMITS.listCacheMs のあいだは覚えたものを返す（DO への負荷を抑える）。束縛が無ければ空。 */
async function roomList(env) {
  const dir = env.ROOM_DIRECTORY;
  if (!dir) return [];
  const hit = listCache.get(dir);
  if (hit && Date.now() - hit.at < LIMITS.listCacheMs) return hit.rooms;
  const rooms = await dir.get(dir.idFromName(DIRECTORY_NAME)).list();
  listCache.set(dir, { at: Date.now(), rooms });
  return rooms;
}

/** Origin ヘッダーのホスト。"null" など URL として読めないものは null（どのホストとも一致しない） */
function originHost(origin) {
  try { return new URL(origin).host; } catch { return null; }
}

/**
 * 理由を付けて閉じる接続（満員・閉じた部屋など）。いったん受け付けてから閉じる（ブラウザでは理由が分かるように）。
 * message があれば、閉じる前に送る（閉じた理由などを、close の理由より詳しく伝える）
 */
function refuse(code, reason, message) {
  const pair = new WebSocketPair();
  pair[1].accept();
  if (message) { try { pair[1].send(JSON.stringify(message)); } catch { /* 送れなくても閉じる */ } }
  pair[1].close(code, reason);
  return new Response(null, { status: 101, webSocket: pair[0] });
}

const encoder = new TextEncoder();

// ---------------------------------------------------------------- 値の確認
const isNum = (v, lim) => typeof v === "number" && Number.isFinite(v) && Math.abs(v) <= lim;
const vec3 = (v, check) => Array.isArray(v) && v.length === 3 && v.every(check);

/**
 * 表示名・オブジェクト名・部屋名: 制御文字と HTML の特殊文字（< > & " '）を除き、前後の空白を取り、長さを切る。
 * クライアントは textContent で表示するので HTML としては使わないが、念のためここでも取り除いておく
 */
export function cleanName(v, max = LIMITS.nameLength) {
  if (typeof v !== "string") return "";
  return [...v.replace(/[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u2028-\u202e\u2060-\u206f\ufeff<>&"']/g, "").trim()].slice(0, max).join("");
}

/** オブジェクト 1 つ。不正なら null */
export function cleanObject(o) {
  if (!o || typeof o !== "object") return null;
  if (typeof o.id !== "string" || !OBJ_ID.test(o.id)) return null;
  if (!KINDS.has(o.kind)) return null;
  const p = o.p, r = o.r, s = o.s;
  if (!vec3(p, x => isNum(x, LIMITS.coord))) return null;
  if (!vec3(r, x => isNum(x, LIMITS.rotationMax))) return null;
  if (!vec3(s, x => isNum(x, LIMITS.scaleMax) && Math.abs(x) >= LIMITS.scaleMin)) return null;
  const name = cleanName(o.name, LIMITS.objectNameLength) || o.kind;
  return { id: o.id, kind: o.kind, name, p, r, s };
}

/** 変形だけ（操作中のプレビュー）。不正なら null */
function cleanTransform(o) {
  if (!o || typeof o.id !== "string" || !OBJ_ID.test(o.id)) return null;
  if (!vec3(o.p, x => isNum(x, LIMITS.coord)) || !vec3(o.r, x => isNum(x, LIMITS.rotationMax)) || !vec3(o.s, x => isNum(x, LIMITS.scaleMax) && Math.abs(x) >= LIMITS.scaleMin)) return null;
  return { id: o.id, p: o.p, r: o.r, s: o.s };
}

function cleanIds(ids, max = LIMITS.objects) {
  if (!Array.isArray(ids) || ids.length > max) return null;
  return ids.every(id => typeof id === "string" && OBJ_ID.test(id)) ? [...new Set(ids)] : null;
}

/** 視点（先生役の視点に合わせる用） */
function cleanView(v) {
  if (!v || typeof v !== "object") return null;
  if (!vec3(v.target, x => isNum(x, LIMITS.coord)) || !isNum(v.dist, LIMITS.viewDistMax) || v.dist <= 0) return null;
  if (!isNum(v.theta, LIMITS.viewAngleMax) || !isNum(v.phi, LIMITS.viewPhiMax) || typeof v.ortho !== "boolean") return null;
  return { target: v.target, dist: v.dist, theta: v.theta, phi: v.phi, ortho: v.ortho, name: cleanName(v.name, LIMITS.viewNameLength) };
}

/**
 * 作成の指定（最初の hello の create）。不正なら null。
 * owner（作成者の鍵）は hello の owner で渡し、ここでは形だけ確かめる。pass は空なら鍵なし。
 */
export function parseCreate(c, owner) {
  if (!c || typeof c !== "object" || typeof c.listed !== "boolean") return null;
  if (typeof owner !== "string" || !OWNER_KEY.test(owner)) return null;
  if (c.pass !== undefined && c.pass !== null && c.pass !== "" && (typeof c.pass !== "string" || [...c.pass].length > LIMITS.passMax)) return null;
  return {
    name: cleanName(c.name, LIMITS.roomNameLength) || "練習の部屋",
    listed: c.listed,
    pass: typeof c.pass === "string" && c.pass !== "" ? c.pass : null,
    owner,
  };
}

// ---------------------------------------------------------------- 暗号（Web Crypto）
const hex = bytes => [...bytes].map(b => b.toString(16).padStart(2, "0")).join("");
const unhex = s => Uint8Array.from(s.match(/../g).map(h => parseInt(h, 16)));

async function sha256Hex(text) {
  return hex(new Uint8Array(await crypto.subtle.digest("SHA-256", encoder.encode(text))));
}

/** PBKDF2-SHA256（ソルトつき）で鍵を導く。16 進の文字。 */
async function derivePass(pass, saltHex, iterations) {
  const key = await crypto.subtle.importKey("raw", encoder.encode(pass), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: unhex(saltHex), iterations }, key, 256);
  return hex(new Uint8Array(bits));
}

/** 鍵の保存形 { salt, iter, hash }。平文は残さない。 */
async function hashPass(pass, iterations) {
  const salt = hex(crypto.getRandomValues(new Uint8Array(16)));
  return { salt, iter: iterations, hash: await derivePass(pass, salt, iterations) };
}

/** 鍵が合うか（保存形と同じ条件で導き、定数時間で比べる） */
async function verifyPass(pass, stored) {
  return sameText(await derivePass(pass, stored.salt, stored.iter), stored.hash);
}

/** 反復回数（ROOM_PASS_ITERATIONS。10000〜100000 の整数だけ受け付け、それ以外は既定の LIMITS.passIterations） */
export function passIterationsOf(env) {
  const n = Number(env?.ROOM_PASS_ITERATIONS);
  return Number.isInteger(n) && n >= 10000 && n <= 100000 ? n : LIMITS.passIterations;
}

/** 16 進の文字の比較（長さが違えば false。途中で打ち切らない） */
function sameText(a, b) {
  if (typeof a !== "string" || typeof b !== "string" || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

/**
 * 接続元の単位。IPv4 はそのまま。IPv6 は先頭 64 ビット（/64）にまとめる（同じ回線の中で下の 64 ビットを替えて、試行の数を増やせないように）。
 * 形の違うもの（"::1"・省略した形）も、先頭の 4 つの段に展開してから数える。
 */
export function netKey(ip) {
  if (!ip.includes(":")) return ip;
  const [head, tail] = ip.includes("::") ? ip.split("::") : [ip, null];
  const hs = head ? head.split(":") : [];
  const ts = tail ? tail.split(":") : [];
  const groups = tail === null ? hs : [...hs, ...Array(Math.max(0, 8 - hs.length - ts.length)).fill("0"), ...ts];
  return groups.slice(0, 4).map(g => g.padStart(4, "0")).join(":") + "::/64";
}

/** 接続元の鍵（netKey の SHA-256 の先頭 16 字）。IP そのものは残さない。IP が無ければ "-" */
export async function ipKeyOf(request) {
  const ip = request?.headers?.get?.("CF-Connecting-IP");
  return ip ? (await sha256Hex(netKey(ip))).slice(0, 16) : "-";
}

// ---------------------------------------------------------------- 部屋
export class Room extends DurableObject {
  constructor(ctx, env) {
    super(ctx, env);
    this.objects = null; // Map<id, obj>（保存してあるシーン。読み込みが済んだら入る）
    this.locks = new Map(); // id → { peer, until }
    this.holds = new Map(); // id → { peer, since, last }。同じ人が続けて持っている時間（上限 lockMaxMs の判定）。解除しても、lockGapMs の間は続きとみなす
    this.rate = new Map(); // WebSocket → { tokens, at, invalid }
    this.seen = new Map(); // WebSocket → 最後にメッセージが届いた時刻
    this.helloing = new Set(); // hello の処理の途中の接続（同じ接続の二重の hello を、待たずに捨てる）
    this.chain = Promise.resolve(); // 部屋の作成・鍵の確認を直列にする（同時の hello で、作成者や鍵が上書きされないように）
    this.loading = null;   // 読み込みの途中（同時に 2 回読まない）
    this.inited = false;   // 部屋を作った（作成の指定を受けた hello のあと）
    this.meta = null;      // { name, listed, createdAt, pass: {salt, iter, hash} | null, ownerHash } 作成のときに決まる
    this.opAt = 0;         // 最後の成功した操作（オブジェクトの変更・ロック・解除・移動の適用）の時刻
    this.opPersisted = 0;  // storage に書いた opAt
    this.warnFor = null;   // idlewarn を出したときの opAt。opAt と同じなら、その操作の区間では警告を出し済み
    this.roomId = null;    // 部屋の ID（X-Room-Id。一覧への登録に使う）
    this.gone = false;     // この部屋は閉じた（以後は何もしない）
    this.goneReason = null; // 閉じた理由（閉じたあとに届いた文字への返事に使う）
    // {"t":"ping"} には、Durable Object を起こさずに {"t":"pong"} を返す
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair('{"t":"ping"}', '{"t":"pong"}'));
  }

  /** 直列に実行する（前の処理が終わってから。失敗しても次の処理は進める） */
  serial(fn) {
    const run = this.chain.then(fn);
    this.chain = run.catch(() => {});
    return run;
  }

  async load() {
    if (this.objects) return;
    this.loading ??= this.readStorage().finally(() => { this.loading = null; });
    return this.loading;
  }

  async readStorage() {
    const stored = await this.ctx.storage.list({ prefix: "o:" });
    const objects = new Map([...stored.values()].map(o => [o.id, o]));
    const inited = Boolean(await this.ctx.storage.get("init"));
    const meta = (await this.ctx.storage.get("meta")) ?? null;
    let opAt = await this.ctx.storage.get("opAt");
    if (inited && (opAt === null || opAt === undefined)) {
      // デプロイ前に作られた部屋（opAt が無い）は、旧版の最後の変更（changed）から数える。無ければ今から。0 にすると、すぐ閉じてしまう
      opAt = (await this.ctx.storage.get("changed")) ?? Date.now();
      await this.ctx.storage.put("opAt", opAt);
    }
    this.opAt = opAt ?? 0;
    this.opPersisted = this.opAt;
    this.warnFor = (await this.ctx.storage.get("warnFor")) ?? null;
    this.roomId = this.roomId ?? (await this.ctx.storage.get("roomId")) ?? null;
    this.meta = meta;
    this.inited = inited;
    this.objects = objects;   // 最後に入れる（これが入るまでは読み込み済みとみなさない）
  }

  /** 部屋の寿命（ROOM_IDLE_SECONDS で短くできる。確認用の動作確認に使う。60 秒から 30 分の間だけ受け付ける） */
  idleMs() {
    const s = Number(this.env?.ROOM_IDLE_SECONDS);
    return Number.isFinite(s) && s >= 60 && s <= 1800 ? s * 1000 : LIMITS.roomIdleMs;
  }

  /** 警告を出すまでの時間（閉じる前の 2 分。部屋の寿命が短いときは、その半分） */
  warnLead() {
    return Math.min(LIMITS.roomWarnMs, Math.floor(this.idleMs() / 2));
  }

  /** 閉じた部屋の目印（期限内なら返す。期限が切れていれば、ここで部屋の中身を消して null）。書き込みを伴う。 */
  async closedRecord() {
    const c = await this.ctx.storage.get("closed");
    if (!c) return null;
    if (Date.now() - c.at < LIMITS.closedMs) return c;
    await this.deleteAllStorage();
    this.reset();
    return null;
  }

  /** 閉じた目印の読み取りだけ（書かない。接続の受け付けに使う） */
  async closedNow() {
    const c = await this.ctx.storage.get("closed");
    return c && Date.now() - c.at < LIMITS.closedMs ? c : null;
  }

  /** 閉じたあとの掃除（期限が切れた目印を消したあと、新しい部屋として使える状態に戻す） */
  reset() {
    this.objects = null;
    this.locks.clear();
    this.holds.clear();
    this.inited = false;
    this.meta = null;
    this.opAt = 0;
    this.opPersisted = 0;
    this.warnFor = null;
    this.gone = false;
  }

  /** 保存を削除する（一度に 128 件まで。DO の上限）。 */
  async deleteAllStorage() {
    await this.ctx.storage.deleteAll();
  }

  /** 保存を書く（一度に 128 件まで。大きい束は分けて書く） */
  async putChunked(obj) {
    const entries = Object.entries(obj);
    for (let i = 0; i < entries.length; i += 128) await this.ctx.storage.put(Object.fromEntries(entries.slice(i, i + 128)));
  }

  async deleteChunked(keys) {
    for (let i = 0; i < keys.length; i += 128) await this.ctx.storage.delete(keys.slice(i, i + 128));
  }

  /** 鍵の試行の行（fail:*）のうち、期限の切れたものを消す（行が増え続けないように。間違いのときだけ調べる） */
  async sweepFails(now) {
    const rows = await this.ctx.storage.list({ prefix: "fail:" });
    const stale = [...rows].filter(([, r]) => (r.until ?? 0) <= now && now - r.first >= LIMITS.passFailWindowMs).map(([k]) => k);
    if (stale.length) await this.deleteChunked(stale);
  }

  /**
   * 部屋を作る（作成の指定を受けた最初の hello のとき。serial の中で呼ぶ）。立方体を 1 つ置き、作成の指定（meta）を保存する。
   * 作成者の鍵は SHA-256 のハッシュだけ、鍵は PBKDF2 のハッシュだけを残す。
   */
  async initRoom(create) {
    const now = Date.now();
    // Blender の初期状態と同じく立方体を 1 つ置く
    const cube = { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] };
    const pass = create.pass ? await hashPass(create.pass, passIterationsOf(this.env)) : null;
    const ownerHash = await sha256Hex(create.owner);
    this.objects = new Map([[cube.id, cube]]);
    this.meta = { name: create.name, listed: create.listed, createdAt: now, pass, ownerHash };
    this.opAt = now;
    this.opPersisted = now;
    this.warnFor = null;
    await this.ctx.storage.put({ init: true, "o:cube0": cube, meta: this.meta, opAt: now, roomId: this.roomId });
    this.inited = true;
    await this.schedule();
  }

  /**
   * 次のアラームを決める（1 つしか持てないので、毎回ここで全部を計算し直す）。
   * 誰かいるあいだは 1 分ごと（切れた接続の確認と一覧の更新）、警告・終了の時刻（操作のたびに延びる）、ロックの期限、閉じた目印の期限のうち、いちばん早いもの。
   */
  async schedule() {
    const now = Date.now();
    let at = Infinity;
    const closed = await this.ctx.storage.get("closed");
    if (closed) {
      at = closed.at + LIMITS.closedMs;
    } else if (this.inited) {
      if (this.open().length) at = now + LIMITS.sweepMs;
      const end = this.opAt + this.idleMs();
      at = Math.min(at, end);
      if (this.warnFor !== this.opAt) at = Math.min(at, end - this.warnLead());
    }
    for (const l of this.locks.values()) at = Math.min(at, l.until);
    if (at !== Infinity) await this.ctx.storage.setAlarm(at);
  }

  async alarm() {
    await this.load();
    const now = Date.now();
    const pruned = this.prune();
    // 期限の切れたロックは、ほかの人にも知らせる（locksMessage は期限切れを除くので、送らないと古いロックが見え続ける）
    if (this.pruneLocks()) this.broadcast(this.locksMessage());
    const closed = await this.closedRecord();
    if (closed) { await this.schedule(); return; }
    if (!this.inited || this.gone) return;
    const end = this.opAt + this.idleMs();
    if (now >= end) return this.closeRoom("idle");
    // 閉じる前の警告（その操作の区間で 1 回だけ。成功した操作があると、opAt が進んで、また出せるようになる）
    if (this.warnFor !== this.opAt && now >= end - this.warnLead()) {
      this.warnFor = this.opAt;
      await this.ctx.storage.put("warnFor", this.opAt);
      this.broadcast({ t: "idlewarn", ms: end - now });
    }
    // 切れた接続を外したとき・誰かいるときは、一覧の登録を更新する（外したあとに空なら、一覧から消える）
    if (pruned || this.peers().length) await this.syncDirectory();
    await this.schedule();
  }

  /**
   * 成功した操作（オブジェクトの変更・ロック・解除・移動の適用）があった。時刻を進め、出ていた警告は取り消す（全員に ms: null）。
   * 拒否された操作・不正なメッセージ・ping は、ここを通らない。
   */
  async noteOp() {
    const now = Date.now();
    if (this.warnFor !== null && this.warnFor === this.opAt) this.broadcast({ t: "idlewarn", ms: null });
    this.opAt = now;
    if (now - this.opPersisted >= LIMITS.roomPersistMs) {
      this.opPersisted = now;
      await this.ctx.storage.put("opAt", now);
    }
  }

  /**
   * 部屋を閉じる（作成者の close・成功した操作が 30 分無い）。
   * 順序: 閉じた印を先に書く（途中で落ちても、部屋は閉じたままになる）→ 一覧から外す → 中身を消す → アラーム。
   * 閉じた印（closed）だけは残し、LIMITS.closedMs のあいだ同じ ID を拒否する（アラームで消す）。
   */
  async closeRoom(reason) {
    const now = Date.now();
    const closed = { at: now, reason };
    this.gone = true;
    this.goneReason = reason;
    const msg = JSON.stringify({ t: "closed", reason });
    for (const ws of this.ctx.getWebSockets()) {
      try { ws.send(msg); } catch { /* 切断済み */ }
      try { ws.close(4004, "closed"); } catch { /* 閉じ済み */ }
    }
    await this.ctx.storage.put("closed", closed);
    await this.unlist();
    const keys = [...(await this.ctx.storage.list()).keys()].filter(k => k !== "closed");
    await this.deleteChunked(keys);
    this.locks.clear();
    this.holds.clear();
    this.objects = null;
    this.inited = false;
    this.meta = null;
    this.warnFor = null;
    await this.ctx.storage.setAlarm(now + LIMITS.closedMs);
  }

  /** 開いている接続 */
  open() {
    return this.ctx.getWebSockets().filter(ws => ws.readyState === WebSocket.OPEN);
  }

  lastSeen(ws) {
    const auto = this.ctx.getWebSocketAutoResponseTimestamp(ws)?.getTime() ?? 0;
    return Math.max(auto, this.seen.get(ws) ?? 0, ws.deserializeAttachment()?.at ?? 0);
  }

  /**
   * しばらく何も届いていない接続（ブラウザが落ちた・回線が切れた）を外す。外した数を返す。
   * 参加前のまま pendingMs たった接続も外す（ping は返るので lastSeen では延命できない。接続した時刻で決める）。
   */
  prune() {
    const now = Date.now();
    let n = 0;
    for (const ws of this.open()) {
      const a = ws.deserializeAttachment();
      const stalePending = !a?.joined && now - (a?.at ?? 0) > LIMITS.pendingMs;
      if (!stalePending && now - this.lastSeen(ws) <= LIMITS.idleMs) continue;
      this.leave(ws);
      n++;
      try { ws.close(4000, "timeout"); } catch { /* 閉じ済み */ }
    }
    return n;
  }

  peers() {
    return this.open().map(ws => ws.deserializeAttachment()).filter(a => a?.joined && !a.left);
  }

  /**
   * 接続の受け付け。ここでは書き込みをしない（部屋の作成・保存・アラームは、作成の指定を受けた hello のあと）。
   * 人数は hello 済みの人だけを数える。参加前の接続は別枠（pendingMax）で、超えたら、いちばん古いものを閉じる。
   * 閉じた部屋の ID は、理由つきで閉じる（4004）。
   */
  async fetch(request) {
    this.roomId = request?.headers?.get?.("X-Room-Id") ?? this.roomId;
    const closed = await this.closedNow();
    if (closed || this.gone) return refuse(4004, "closed", { t: "closed", reason: closed?.reason ?? "closed" });
    this.prune();
    if (this.peers().length >= LIMITS.participants) return refuse(4001, "full");
    const pending = this.open().filter(ws => !ws.deserializeAttachment()?.joined);
    while (pending.length >= LIMITS.pendingMax) {
      try { pending.shift().close(4002, "too many pending"); } catch { /* 閉じ済み */ }
    }
    const pair = new WebSocketPair();
    const id = crypto.randomUUID().replace(/-/g, "").slice(0, 10);
    this.ctx.acceptWebSocket(pair[1]);
    // 色は hello のときに決める（参加している人の色と重ならないように）。ipKey は接続元の数え方（鍵の間違い）に使う
    pair[1].serializeAttachment({ id, color: null, name: "", joined: false, at: Date.now(), ipKey: await ipKeyOf(request) });
    return new Response(null, { status: 101, webSocket: pair[0] });
  }

  send(ws, msg) {
    try { ws.send(JSON.stringify(msg)); } catch { /* 切断済み */ }
  }

  broadcast(msg, except = null) {
    const data = JSON.stringify(msg);
    for (const ws of this.open()) {
      const a = ws.deserializeAttachment();
      if (ws === except || !a?.joined || a.left) continue;
      try { ws.send(data); } catch { /* 切断済み */ }
    }
  }

  /** 1 人あたりの送信の頻度を制限する（トークンバケツ）。超えたら false */
  allow(ws) {
    const now = Date.now();
    const r = this.rate.get(ws) ?? { tokens: LIMITS.rateBurst, at: now };
    r.tokens = Math.min(LIMITS.rateBurst, r.tokens + ((now - r.at) / 1000) * LIMITS.ratePerSec);
    r.at = now;
    this.rate.set(ws, r);
    if (r.tokens < 1) return false;
    r.tokens -= 1;
    return true;
  }

  /** 不正なメッセージを返す。直近の時間窓の中で invalidBeforeClose 回になったら切断（古いものは数えない） */
  invalid(ws, why) {
    const now = Date.now();
    const r = this.rate.get(ws) ?? { tokens: LIMITS.rateBurst, at: now };
    r.invalidAt = (r.invalidAt ?? []).filter(t => now - t < LIMITS.invalidWindowMs);
    r.invalidAt.push(now);
    this.rate.set(ws, r);
    this.send(ws, { t: "error", msg: why });
    if (r.invalidAt.length >= LIMITS.invalidBeforeClose) ws.close(1008, "too many invalid messages");
  }

  /** ロックの確認。ほかの人が持っている ID の一覧 */
  lockedByOthers(ids, peer) {
    const now = Date.now();
    return ids.filter(id => { const l = this.locks.get(id); return l && l.peer !== peer && l.until > now; });
  }

  locksMessage() {
    const now = Date.now();
    const out = {};
    for (const [id, l] of this.locks) if (l.until > now) out[id] = l.peer;
    return { t: "locks", locks: out };
  }

  /** 期限の切れたロックを外す。外したものがあれば true */
  pruneLocks() {
    const now = Date.now();
    let removed = false;
    for (const [id, l] of this.locks) if (l.until <= now) { this.locks.delete(id); removed = true; }
    return removed;
  }

  releaseLocks(peer) {
    let changed = false;
    for (const [id, l] of this.locks) if (l.peer === peer) { this.locks.delete(id); changed = true; }
    return changed;
  }

  async webSocketMessage(ws, data) {
    // 閉じた部屋: 閉じたことを伝えて切断する（黙って捨てると、接続が開いたまま残る）
    if (this.gone) {
      this.send(ws, { t: "closed", reason: this.goneReason ?? "closed" });
      try { ws.close(4004, "closed"); } catch { /* 閉じ済み */ }
      return;
    }
    // バイナリは受け付けない。大きさは UTF-8 のバイト数で比べる（1 文字は最大 3 バイトなので、短いものはエンコードせずに通す）
    if (typeof data !== "string") return this.invalid(ws, "binary not supported");
    if (data.length > LIMITS.messageBytes || (data.length * 3 > LIMITS.messageBytes && encoder.encode(data).length > LIMITS.messageBytes)) return this.invalid(ws, "message too large");
    this.seen.set(ws, Date.now());
    if (!this.allow(ws)) return this.send(ws, { t: "error", msg: "rate limited" });
    let msg;
    try { msg = JSON.parse(data); } catch { return this.invalid(ws, "bad json"); }
    if (!msg || typeof msg !== "object" || typeof msg.t !== "string") return this.invalid(ws, "bad message");
    const me = ws.deserializeAttachment();
    if (!me.joined) {
      if (msg.t !== "hello") return this.invalid(ws, "say hello first");
      if (this.helloing.has(ws)) return;   // 同じ接続の hello が処理中（二重に入らないように）
      this.helloing.add(ws);
      try {
        await this.load();
        return await this.join(ws, ws.deserializeAttachment(), msg);
      } finally {
        this.helloing.delete(ws);
      }
    }
    await this.load();

    switch (msg.t) {
      case "hello":
        // 二度目の hello は無視する（join を二重に知らせない）
        return;
      case "name": {
        const name = cleanName(msg.name);
        if (!name) return this.invalid(ws, "bad name");
        me.name = name;
        ws.serializeAttachment(me);
        this.broadcast({ t: "peer", peer: { id: me.id, name, color: me.color } });
        return;
      }
      case "cursor": {
        // 3D ビュー上のマウスの位置（ワールド座標）。null で非表示。操作には数えない
        if (msg.p !== null && !vec3(msg.p, x => isNum(x, LIMITS.coord))) return this.invalid(ws, "bad cursor");
        this.broadcast({ t: "cursor", from: me.id, p: msg.p }, ws);
        return;
      }
      case "select": {
        const ids = cleanIds(msg.ids);
        if (!ids) return this.invalid(ws, "bad selection");
        this.broadcast({ t: "select", from: me.id, ids }, ws);
        return;
      }
      case "view": {
        const view = cleanView(msg.view);
        if (!view) return this.invalid(ws, "bad view");
        this.broadcast({ t: "view", from: me.id, view }, ws);
        return;
      }
      case "lock": {
        const ids = cleanIds(msg.ids, LIMITS.lockIds);
        if (!ids) return this.invalid(ws, "bad lock");
        const busy = this.lockedByOthers(ids, me.id);
        // ほかの人が持っているものは、取れない（拒否した操作は、寿命を延ばさない）
        if (busy.length) { this.send(ws, { t: "lockdenied", ids: busy, seq: msg.seq ?? null }); return; }
        const now = Date.now();
        // 同じ人が同じものを続けて持っている時間（since）。取り直しの間隔が lockGapMs 以内なら、同じ保持の続きとみなす
        const held = ids.map(id => {
          const h = this.holds.get(id);
          const since = h && h.peer === me.id && now - h.last <= LIMITS.lockGapMs ? h.since : now;
          this.holds.set(id, { peer: me.id, since, last: now });
          return { id, since };
        });
        const changed = this.releaseLocks(me.id);
        if (held.some(h => now - h.since >= LIMITS.lockMaxMs)) {
          // 上限に達した: 持っている全部を解除して知らせる（この人の取り直しは、上の続きとして扱うので通らない）。寿命は延ばさない
          if (changed) this.broadcast(this.locksMessage());
          this.send(ws, { t: "lockexpired", ids });
          await this.schedule();
          return;
        }
        for (const h of held) this.locks.set(h.id, { peer: me.id, until: Math.min(now + LIMITS.lockMs, h.since + LIMITS.lockMaxMs) });
        await this.noteOp();   // 成功したロック（取り直しを含む）は操作
        this.broadcast(this.locksMessage());
        await this.schedule();   // 期限が切れたときに起きて、ほかの人へ知らせる
        // 上限が近いときは、持っている人に知らせる（操作が途中で外れないように、残りの時間を渡す）
        const left = Math.min(...held.map(h => h.since + LIMITS.lockMaxMs - now));
        if (left <= LIMITS.lockWarnMs) this.send(ws, { t: "lockwarn", ids, ms: left });
        return;
      }
      case "unlock": {
        // 解除は、実際に持っていたものを外したときだけ操作に数える
        if (this.releaseLocks(me.id)) {
          await this.noteOp();
          this.broadcast(this.locksMessage());
        }
        return;
      }
      case "preview": {
        // 操作中の変形（保存しない）。ほかの人がロックしているものは送らない。適用されたものがあるときだけ操作に数える
        if (!Array.isArray(msg.objs) || msg.objs.length > LIMITS.lockIds) return this.invalid(ws, "bad preview");
        const objs = msg.objs.map(cleanTransform);
        if (objs.some(o => !o)) return this.invalid(ws, "bad preview");
        const ok = objs.filter(o => this.objects.has(o.id) && !this.lockedByOthers([o.id], me.id).length);
        if (ok.length) {
          await this.noteOp();
          this.broadcast({ t: "preview", from: me.id, objs: ok }, ws);
        }
        return;
      }
      case "ops": {
        // 確定した変更: upsert（追加・変形）と delete。どちらも、無い・null は空。配列でなければ不正
        const upsert = msg.upsert ?? [];
        const del = cleanIds(msg.delete ?? []);
        if (!Array.isArray(upsert) || upsert.length > LIMITS.objects || !del) return this.invalid(ws, "bad ops");
        const objs = upsert.map(cleanObject);
        if (objs.some(o => !o)) return this.invalid(ws, "bad object");
        const busy = this.lockedByOthers([...objs.map(o => o.id), ...del], me.id);
        const applied = { upsert: [], delete: [] };
        const puts = {};
        for (const o of objs) {
          if (busy.includes(o.id)) continue;
          if (!this.objects.has(o.id) && this.objects.size >= LIMITS.objects) { this.send(ws, { t: "error", msg: "too many objects" }); continue; }
          this.objects.set(o.id, o);
          puts[`o:${o.id}`] = o;
          applied.upsert.push(o);
        }
        const removes = [];
        let unlocked = false;
        for (const id of del) {
          if (busy.includes(id) || !this.objects.has(id)) continue;
          this.objects.delete(id);
          if (this.locks.delete(id)) unlocked = true;
          removes.push(`o:${id}`);
          applied.delete.push(id);
        }
        if (Object.keys(puts).length) await this.putChunked(puts);
        if (removes.length) await this.deleteChunked(removes);
        if (applied.upsert.length || applied.delete.length) {
          await this.noteOp();   // 適用された変更だけが操作
          this.broadcast({ t: "ops", from: me.id, ...applied }, ws);
        }
        // 消したオブジェクトのロックも外れたことを全員に知らせる
        if (unlocked) this.broadcast(this.locksMessage());
        // 反映できなかったもの（ロック中・上限）は、送った人に正しい状態を返す
        const rejected = [...objs.filter(o => !applied.upsert.includes(o)).map(o => o.id), ...del.filter(id => !applied.delete.includes(id))];
        if (rejected.length) {
          this.send(ws, {
            t: "ops", from: null, rejected: true,
            upsert: rejected.map(id => this.objects.get(id)).filter(Boolean),
            delete: rejected.filter(id => !this.objects.has(id)),
          });
        }
        return;
      }
      case "close":
        // 部屋を閉じる（作成者だけ）。作成者でなければ、何もしないで理由だけ返す
        if (!me.owner) return this.send(ws, { t: "error", msg: "not owner" });
        return this.closeRoom("owner");
      case "ping":
        this.send(ws, { t: "pong" });
        return;
      default:
        return this.invalid(ws, "unknown type");
    }
  }

  /**
   * 鍵の確認（鍵つきの部屋で、作成者でない人だけ）。合わなければ理由を送って 4003 で閉じ、false を返す。
   * 直列に行う（同時の試行で、間違いの数を取りこぼさないように）。
   */
  async passOk(ws, me, msg) {
    return this.serial(async () => {
      const now = Date.now();
      const key = `fail:${me.ipKey ?? "-"}`;
      const f = (await this.ctx.storage.get(key)) ?? null;
      if (f && f.until > now) {
        this.send(ws, { t: "passlimit", ms: f.until - now });
        ws.close(4003, "pass limit");
        return false;
      }
      if (typeof msg.pass !== "string") {
        this.send(ws, { t: "needpass" });
        ws.close(4003, "need pass");
        return false;
      }
      const ok = [...msg.pass].length <= LIMITS.passMax && await verifyPass(msg.pass, this.meta.pass);
      if (ok) {
        if (f) await this.ctx.storage.delete(key);
        return true;
      }
      // 間違い: 時間窓の中で数える。passFailMax 回で拒否（passBlockMs のあいだ）
      const cur = !f || now - f.first >= LIMITS.passFailWindowMs ? { n: 0, first: now, until: 0 } : f;
      cur.n += 1;
      if (cur.n >= LIMITS.passFailMax) { cur.until = now + LIMITS.passBlockMs; cur.n = 0; }
      await this.ctx.storage.put(key, cur);
      await this.sweepFails(now);
      this.send(ws, { t: "wrongpass" });
      ws.close(4003, "wrong pass");
      return false;
    });
  }

  /** この接続が作成者か（作成者の鍵の SHA-256 が、部屋の記録と一致するか） */
  async isOwner(key) {
    if (!this.meta?.ownerHash || typeof key !== "string" || !OWNER_KEY.test(key)) return false;
    return sameText(await sha256Hex(key), this.meta.ownerHash);
  }

  /**
   * 参加（hello）。閉じた部屋・作成・作成者（鍵の免除）・鍵・満員を確かめ、色を決めて、参加を知らせる。
   * 作成の指定が無く、部屋も無いときは作らない（閉じた部屋の ID が消えた後に、古い URL から鍵なしの部屋が作られないように）。
   */
  async join(ws, me, msg) {
    if (me.joined) return;
    const closed = await this.closedRecord();
    if (closed || this.gone) { this.send(ws, { t: "closed", reason: closed?.reason ?? "closed" }); ws.close(4004, "closed"); return; }
    // 作成・部屋の有無は、直列の中で決める（同時の hello で 2 回作らない）
    const hasCreate = msg.create !== undefined && msg.create !== null;
    const outcome = await this.serial(async () => {
      if (this.inited) return "joined";
      if (!hasCreate) return "missing";
      const create = parseCreate(msg.create, msg.owner);
      if (!create) return "bad";
      await this.initRoom(create);
      return "created";
    });
    if (outcome === "missing") { this.send(ws, { t: "closed", reason: "missing" }); ws.close(4004, "closed"); return; }
    if (outcome === "bad") return this.invalid(ws, "bad create");
    if (me.joined) return;
    // 作成者は鍵を免除する（作成者の鍵が合う接続。作成したばかりの人も含む）
    const owner = outcome === "created" || await this.isOwner(msg.owner);
    if (!owner && this.meta?.pass && !(await this.passOk(ws, me, msg))) return;
    // ここから同期の処理（await の後に人数を確かめ、joined を立てるまで await を挟まない。同時に来た hello で人数を超えないように）
    if (me.joined) return;
    if (this.peers().length >= LIMITS.participants) { ws.close(4001, "full"); return; }
    me.name = cleanName(msg.name) || "ゲスト";
    const used = new Set(this.peers().map(p => p.color));
    me.color = COLORS.find(c => !used.has(c)) ?? COLORS[this.peers().length % COLORS.length];
    me.owner = owner;
    me.joined = true;
    ws.serializeAttachment(me);
    await this.schedule();
    this.send(ws, {
      t: "welcome", you: { id: me.id, name: me.name, color: me.color, owner: me.owner },
      room: { name: this.meta?.name ?? "", locked: Boolean(this.meta?.pass) },
      peers: this.peers().filter(p => p.id !== me.id).map(({ id, name, color }) => ({ id, name, color })),
      objects: [...this.objects.values()], limits: { participants: LIMITS.participants, objects: LIMITS.objects },
    });
    this.send(ws, this.locksMessage());
    // 警告の途中で入った人にも、残りの時間を知らせる
    if (this.warnFor === this.opAt) this.send(ws, { t: "idlewarn", ms: Math.max(0, this.opAt + this.idleMs() - Date.now()) });
    this.broadcast({ t: "join", peer: { id: me.id, name: me.name, color: me.color } }, ws);
    await this.syncDirectory();
  }

  async webSocketClose(ws, code) {
    await this.load();
    this.leave(ws);
    await this.syncDirectory();
    try { ws.close(code === 1005 ? 1000 : code, "bye"); } catch { /* 閉じ済み */ }
  }

  async webSocketError(ws) {
    await this.load();
    this.leave(ws);
    await this.syncDirectory();
  }

  leave(ws) {
    const me = ws.deserializeAttachment();
    this.rate.delete(ws);
    this.seen.delete(ws);
    if (!me?.joined || me.left || this.gone) return;
    me.left = true; // 二度知らせない
    try { ws.serializeAttachment(me); } catch { /* 閉じ済み */ }
    if (this.releaseLocks(me.id)) this.broadcast(this.locksMessage(), ws);
    this.broadcast({ t: "leave", id: me.id }, ws);
  }

  // ---------------------------------------------------------------- 一覧（RoomDirectory）
  /** 一覧に出す部屋は、人数が変わったときに登録を更新する。作成直後の猶予が過ぎて誰もいなければ外す。失敗しても部屋は続ける。 */
  async syncDirectory() {
    if (!this.env?.ROOM_DIRECTORY || !this.roomId || !this.meta?.listed || this.gone) return;
    try {
      const stub = this.env.ROOM_DIRECTORY.get(this.env.ROOM_DIRECTORY.idFromName(DIRECTORY_NAME));
      const count = this.peers().length;
      if (count === 0 && Date.now() - this.meta.createdAt >= LIMITS.listGraceMs) return this.unlist();
      await stub.upsert({ id: this.roomId, name: this.meta.name, count, max: LIMITS.participants, locked: Boolean(this.meta.pass), createdAt: this.meta.createdAt });
    } catch { /* 一覧が無くても部屋は動かす */ }
  }

  /** 一覧から外す（閉じたとき・誰もいなくなって猶予が過ぎたとき）。失敗は無視する。 */
  async unlist() {
    if (!this.env?.ROOM_DIRECTORY || !this.roomId || !this.meta?.listed) return;
    try {
      await this.env.ROOM_DIRECTORY.get(this.env.ROOM_DIRECTORY.idFromName(DIRECTORY_NAME)).remove(this.roomId);
    } catch { /* 一覧が無くても部屋は動かす */ }
  }
}

/**
 * 練習の部屋の一覧（固定名の 1 つの Durable Object）。Room が、作成・参加者の変化・閉じたとき・定期の更新で登録を書き換える。
 * 一覧に出すのは、参加者が 1 人以上いる部屋（作成直後の猶予を除く）。登録が listTtlMs のあいだ更新されなければ、一覧から消す。
 */
export class RoomDirectory extends DurableObject {
  /** 登録（作成・更新）。件数の上限を超えるときは登録しない（false） */
  async upsert(entry, now = Date.now()) {
    const e = cleanEntry(entry);
    if (!e) return false;
    const key = `r:${e.id}`;
    if (!(await this.ctx.storage.get(key))) {
      const all = await this.ctx.storage.list({ prefix: "r:" });
      if (all.size >= LIMITS.listStoreMax) return false;
    }
    await this.ctx.storage.put(key, { ...e, updatedAt: now });
    return true;
  }

  /** 一覧から外す */
  async remove(id) {
    if (typeof id === "string" && ROOM_ID.test(id)) await this.ctx.storage.delete(`r:${id}`);
  }

  /** 一覧（新しい順・listMax まで）。期限の切れた登録は、ここで消す。 */
  async list(now = Date.now()) {
    const all = await this.ctx.storage.list({ prefix: "r:" });
    const stale = [];
    const rooms = [];
    for (const [key, e] of all) {
      if (now - e.updatedAt > LIMITS.listTtlMs) { stale.push(key); continue; }
      if (e.count >= 1 || now - e.createdAt < LIMITS.listGraceMs) {
        rooms.push({ id: e.id, name: e.name, count: e.count, max: e.max, locked: e.locked, createdAt: e.createdAt });
      }
    }
    if (stale.length) await this.ctx.storage.delete(stale);
    rooms.sort((a, b) => b.createdAt - a.createdAt);
    return rooms.slice(0, LIMITS.listMax);
  }
}

/** 登録の形の確認。不正なら null */
function cleanEntry(e) {
  if (!e || typeof e !== "object") return null;
  if (typeof e.id !== "string" || !ROOM_ID.test(e.id)) return null;
  if (typeof e.name !== "string" || e.name.length > 200) return null;
  if (!Number.isInteger(e.count) || e.count < 0 || e.count > LIMITS.participants) return null;
  if (e.max !== LIMITS.participants || typeof e.locked !== "boolean") return null;
  if (!Number.isFinite(e.createdAt)) return null;
  return { id: e.id, name: e.name, count: e.count, max: e.max, locked: e.locked, createdAt: e.createdAt };
}
