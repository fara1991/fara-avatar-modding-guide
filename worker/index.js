// Cloudflare Worker。すべてのリクエストがまずここを通る（wrangler.toml の run_worker_first = true）。
// - /api/rooms/<部屋ID>/ws は共同練習の部屋（Durable Object）へ
// - /api/maintenance はメンテナンスの状態（{state, message}）。WebAssembly のアプリが起動時に読んで、お知らせの帯を出す（HTML には注入しない）
// - それ以外は静的ファイル（wrangler の assets。dotnet publish の wwwroot に、Build が書き足したもの）。確認用の noindex は、Build が _headers に付ける
//
// 共同練習の部屋（/practice/room）: 部屋ごとに 1 つの Durable Object（Room）が WebSocket をまとめ、
// オブジェクトモードのシーン（立方体などの種類・名前・位置・回転・スケール）を全員で共有する。
// - 遅れて入った人には、Durable Object に保存したシーンを丸ごと送る
// - 同じオブジェクトへの変更は、サーバーに届いた順（後勝ち）。操作中のオブジェクトはロックできる
// - 受け取ったメッセージはすべてここで形式と値の範囲を確かめる（不正なものは捨てる）
// - 最後の変更から 24 時間たつと、アラームで部屋の内容を消す
// - ping は Durable Object を起こさずに自動で返す（WebSocket Hibernation）。応答の時刻で、切れた接続を見つけて外す
import { DurableObject } from "cloudflare:workers";
// 報告の受け口とメンテナンスの判定は、共通部品（fara-site-kit）の同じファイルを使う（各サイトに同じコピーを持たない）
import { handleReport } from "../external/fara-site-kit/worker/report.js";
import { getMaintenance, maintenanceResponse } from "../external/fara-site-kit/worker/maintenance.js";

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
  expireMs: 24 * 60 * 60 * 1000, // 最後の変更から部屋を消すまで
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
};

const KINDS = new Set(["plane", "cube", "sphere", "cylinder", "cone", "torus"]);
const COLORS = ["#f97316", "#22c55e", "#3b82f6", "#e879f9", "#facc15", "#14b8a6", "#ef4444", "#a78bfa"];
const ROOM_ID = /^[a-z0-9]{6,32}$/;
const OBJ_ID = /^[a-z0-9]{1,24}$/;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    // メンテナンス（Worker の変数 MAINTENANCE_MODE。external/fara-site-kit/worker/maintenance.js）。true の間は、すべて 503 のメンテナンス画面
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
      return stub.fetch(request);
    }
    // 作者への報告の受け口（external/fara-site-kit/worker/report.js）
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

/** Origin ヘッダーのホスト。"null" など URL として読めないものは null（どのホストとも一致しない） */
function originHost(origin) {
  try { return new URL(origin).host; } catch { return null; }
}

const encoder = new TextEncoder();

// ---------------------------------------------------------------- 値の確認
const isNum = (v, lim) => typeof v === "number" && Number.isFinite(v) && Math.abs(v) <= lim;
const vec3 = (v, check) => Array.isArray(v) && v.length === 3 && v.every(check);

/**
 * 表示名・オブジェクト名: 制御文字と HTML の特殊文字（< > & " '）を除き、前後の空白を取り、長さを切る。
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

// ---------------------------------------------------------------- 部屋
export class Room extends DurableObject {
  constructor(ctx, env) {
    super(ctx, env);
    this.objects = null; // Map<id, obj>（保存してあるシーン。最初に使うときに読み込む）
    this.locks = new Map(); // id → { peer, until }
    this.rate = new Map(); // WebSocket → { tokens, at, invalid }
    this.seen = new Map(); // WebSocket → 最後にメッセージが届いた時刻
    // {"t":"ping"} には、Durable Object を起こさずに {"t":"pong"} を返す
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair('{"t":"ping"}', '{"t":"pong"}'));
  }

  async load() {
    if (this.objects) return;
    const stored = await this.ctx.storage.list({ prefix: "o:" });
    this.objects = new Map([...stored.values()].map(o => [o.id, o]));
    if (!(await this.ctx.storage.get("init"))) {
      // 新しい部屋: Blender の初期状態と同じく立方体を 1 つ置く
      const cube = { id: "cube0", kind: "cube", name: "Cube", p: [0, 0, 0], r: [0, 0, 0], s: [1, 1, 1] };
      this.objects.set(cube.id, cube);
      await this.ctx.storage.put({ init: true, "o:cube0": cube });
      await this.touch();
    }
  }

  /** 変更したことを記録して、アラームを付け直す */
  async touch() {
    await this.ctx.storage.put("changed", Date.now());
    await this.schedule();
  }

  /**
   * 次のアラームを決める（1 つしか持てないので、毎回ここで全部を計算し直す）。
   * 誰かいるあいだは 1 分ごと（切れた接続の確認）、いなければ最後の変更の 24 時間後（部屋を消す）。
   * ロックの期限が先に来るなら、その時刻にも起きる（切れたロックを、ほかの人の画面から外すため）。
   */
  async schedule() {
    const changed = (await this.ctx.storage.get("changed")) ?? Date.now();
    let at = this.open().length ? Date.now() + LIMITS.sweepMs : changed + LIMITS.expireMs;
    for (const l of this.locks.values()) at = Math.min(at, l.until);
    await this.ctx.storage.setAlarm(at);
  }

  async alarm() {
    this.prune();
    // 期限の切れたロックは、ほかの人にも知らせる（locksMessage は期限切れを除くので、送らないと古いロックが見え続ける）
    if (this.pruneLocks()) this.broadcast(this.locksMessage());
    const changed = (await this.ctx.storage.get("changed")) ?? 0;
    if (!this.open().length && Date.now() - changed >= LIMITS.expireMs) {
      await this.ctx.storage.deleteAll();
      this.objects = null;
      return;
    }
    await this.schedule();
  }

  /** 開いている接続 */
  open() {
    return this.ctx.getWebSockets().filter(ws => ws.readyState === WebSocket.OPEN);
  }

  lastSeen(ws) {
    const auto = this.ctx.getWebSocketAutoResponseTimestamp(ws)?.getTime() ?? 0;
    return Math.max(auto, this.seen.get(ws) ?? 0, ws.deserializeAttachment()?.at ?? 0);
  }

  /** しばらく何も届いていない接続（ブラウザが落ちた・回線が切れた）を外す */
  prune() {
    const now = Date.now();
    for (const ws of this.open()) {
      if (now - this.lastSeen(ws) <= LIMITS.idleMs) continue;
      this.leave(ws);
      try { ws.close(4000, "timeout"); } catch { /* 閉じ済み */ }
    }
  }

  peers() {
    return this.open().map(ws => ws.deserializeAttachment()).filter(a => a?.joined && !a.left);
  }

  async fetch(request) {
    this.prune();
    const sockets = this.open();
    if (sockets.length >= LIMITS.participants) {
      // 満員: いったん受け付けて、理由を付けて閉じる（ブラウザでは理由が分かるように）
      const pair = new WebSocketPair();
      pair[1].accept();
      pair[1].close(4001, "full");
      return new Response(null, { status: 101, webSocket: pair[0] });
    }
    await this.load();
    const used = new Set(sockets.map(ws => ws.deserializeAttachment()?.color));
    const color = COLORS.find(c => !used.has(c)) ?? COLORS[sockets.length % COLORS.length];
    const pair = new WebSocketPair();
    const id = crypto.randomUUID().replace(/-/g, "").slice(0, 10);
    this.ctx.acceptWebSocket(pair[1]);
    pair[1].serializeAttachment({ id, color, name: "", joined: false, at: Date.now() });
    await this.schedule();
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
    // バイナリは受け付けない。大きさは UTF-8 のバイト数で比べる（1 文字は最大 3 バイトなので、短いものはエンコードせずに通す）
    if (typeof data !== "string") return this.invalid(ws, "binary not supported");
    if (data.length > LIMITS.messageBytes || (data.length * 3 > LIMITS.messageBytes && encoder.encode(data).length > LIMITS.messageBytes)) return this.invalid(ws, "message too large");
    this.seen.set(ws, Date.now());
    if (!this.allow(ws)) return this.send(ws, { t: "error", msg: "rate limited" });
    let msg;
    try { msg = JSON.parse(data); } catch { return this.invalid(ws, "bad json"); }
    if (!msg || typeof msg !== "object" || typeof msg.t !== "string") return this.invalid(ws, "bad message");
    await this.load();
    const me = ws.deserializeAttachment();

    if (msg.t === "hello") {
      // 二度目の hello は無視する（join を二重に知らせない）
      if (me.joined) return;
      me.name = cleanName(msg.name) || "ゲスト";
      me.joined = true;
      ws.serializeAttachment(me);
      this.send(ws, {
        t: "welcome", you: { id: me.id, name: me.name, color: me.color },
        peers: this.peers().filter(p => p.id !== me.id).map(({ id, name, color }) => ({ id, name, color })),
        objects: [...this.objects.values()], limits: { participants: LIMITS.participants, objects: LIMITS.objects },
      });
      this.send(ws, this.locksMessage());
      this.broadcast({ t: "join", peer: { id: me.id, name: me.name, color: me.color } }, ws);
      return;
    }
    if (!me.joined) return this.invalid(ws, "say hello first");

    switch (msg.t) {
      case "name": {
        const name = cleanName(msg.name);
        if (!name) return this.invalid(ws, "bad name");
        me.name = name;
        ws.serializeAttachment(me);
        this.broadcast({ t: "peer", peer: { id: me.id, name, color: me.color } });
        return;
      }
      case "cursor": {
        // 3D ビュー上のマウスの位置（ワールド座標）。null で非表示
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
        if (busy.length) { this.send(ws, { t: "lockdenied", ids: busy, seq: msg.seq ?? null }); return; }
        this.releaseLocks(me.id);
        const until = Date.now() + LIMITS.lockMs;
        for (const id of ids) this.locks.set(id, { peer: me.id, until });
        this.broadcast(this.locksMessage());
        await this.schedule();   // 期限が切れたときに起きて、ほかの人へ知らせる
        return;
      }
      case "unlock": {
        if (this.releaseLocks(me.id)) this.broadcast(this.locksMessage());
        return;
      }
      case "preview": {
        // 操作中の変形（保存しない）。ほかの人がロックしているものは送らない
        if (!Array.isArray(msg.objs) || msg.objs.length > LIMITS.lockIds) return this.invalid(ws, "bad preview");
        const objs = msg.objs.map(cleanTransform);
        if (objs.some(o => !o)) return this.invalid(ws, "bad preview");
        const ok = objs.filter(o => this.objects.has(o.id) && !this.lockedByOthers([o.id], me.id).length);
        if (ok.length) this.broadcast({ t: "preview", from: me.id, objs: ok }, ws);
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
        if (Object.keys(puts).length) await this.ctx.storage.put(puts);
        if (removes.length) await this.ctx.storage.delete(removes);
        if (applied.upsert.length || applied.delete.length) {
          await this.touch();
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
      case "ping":
        this.send(ws, { t: "pong" });
        return;
      default:
        return this.invalid(ws, "unknown type");
    }
  }

  async webSocketClose(ws, code) {
    this.leave(ws);
    try { ws.close(code === 1005 ? 1000 : code, "bye"); } catch { /* 閉じ済み */ }
  }

  async webSocketError(ws) {
    this.leave(ws);
  }

  leave(ws) {
    const me = ws.deserializeAttachment();
    this.rate.delete(ws);
    this.seen.delete(ws);
    if (!me?.joined || me.left) return;
    me.left = true; // 二度知らせない
    try { ws.serializeAttachment(me); } catch { /* 閉じ済み */ }
    if (this.releaseLocks(me.id)) this.broadcast(this.locksMessage(), ws);
    this.broadcast({ t: "leave", id: me.id }, ws);
  }
}
