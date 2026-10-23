// みんなで練習（/practice/room?room=<ID>）。同じ部屋の人と 1 つの 3D ビューを共有する。
// 通信相手は worker/index.js の Durable Object（/api/rooms/<ID>/ws）。
//
// - 自分の変更は「確定したとき」に差分（追加・変形 = upsert、削除 = delete）で送る。取り消し（Ctrl+Z）なども差分で送られる
// - 操作中（G / R / S）は、動かしているオブジェクトをロックし、途中の変形を 80 ms ごとに送る（ほかの人にも動いて見える）
// - カーソル（3D ビュー上の位置）・選択・視点もほかの人に送る。「視点に合わせる」はその人の視点を追いかける
// - 編集モード（Tab）の変更は送らない（自分の画面だけ）
//
// WebAssembly 版(Blazor のページから呼ぶ)での変更点(旧 SSR 版の room.js との差):
//   ・読み込んだだけでは動かない。start(roomId) で部屋に入り、返った dispose() で退室と後始末をする(ページを離れるとき・別の部屋へ移るとき)
//   ・部屋の ID の検査と、ロビー(部屋を作る)・部屋の表示の切り替えは、Blazor の PracticeRoom.razor が持つ(roomId は検査済みで渡る)
//   ・タイマー(動き・ping・再接続)、WebSocket、イベント(pagehide・ボタンなど)は、dispose() で全部止める
import { Viewport } from "./viewport.js";

const NAME_KEY = "avatar-modding-guide.name";
const TICK_MS = 80;
const VIEW_MS = 250;

const $ = id => document.getElementById(id);
/** 表示名: 制御文字を除いて 20 文字まで（サーバーでも同じ処理をする） */
function cleanName(v) {
  return [...String(v).replace(/[\u0000-\u001f\u007f-\u009f\u200b-\u200f\u2028-\u202e\u2060-\u206f\ufeff]/g, "").trim()].slice(0, 20).join("");
}

function loadName() {
  try { const n = cleanName(localStorage.getItem(NAME_KEY) || ""); if (n) return n; } catch { /* 保存できない環境 */ }
  return `ゲスト${Math.floor(Math.random() * 90 + 10)}`;
}

class RoomClient {
  constructor(id) {
    this.id = id;
    this.abort = new AbortController();
    this.closing = false;
    this.peers = new Map(); // id → { id, name, color, cursor, selection, locks, view }
    this.me = null;
    this.server = new Map(); // rid → サーバーが持っている状態（JSON 文字列）。差分を作るのに使う
    this.locks = {}; // rid → 操作中の人の id
    this.lockHeld = false;
    this.follow = null;
    this.retries = 0;
    this.name = loadName();
    this.pendingCursor = undefined;
    this.lastPreview = "";
    this.previewed = new Set();
    this.lastView = "";
    this.lastViewAt = 0;

    $("room-url").value = location.href;
    $("room-name").value = this.name;
    this.vp = new Viewport($("room-viewport"), {
      scene: [],
      canTransform: objs => this.blockedBy(objs),
    });
    this.vp.setStatus("部屋に接続しています…");
    this.vp.on("*", e => this.onLocal(e));
    this.bind();
    this.connect();
    this.timer = setInterval(() => this.tick(), TICK_MS);
    // 接続が生きていることを知らせる（サーバーは 60 秒何も届かない接続を外す）
    this.pingTimer = setInterval(() => { if (this.ws?.readyState === WebSocket.OPEN) this.ws.send('{"t":"ping"}'); }, 20000);
    // ページを離れるときは、すぐに退出を知らせる
    addEventListener("pagehide", () => { this.closing = true; this.ws?.close(1000, "leave"); }, { signal: this.abort.signal });
  }

  /** 退室して、タイマー・WebSocket・イベント・3D ビューを全部解放する */
  dispose() {
    this.closing = true;
    this.abort.abort();
    clearInterval(this.timer);
    clearInterval(this.pingTimer);
    clearTimeout(this.retryTimer);
    clearTimeout(this.nameTimer);
    clearTimeout(this.copyTimer);
    this.ws?.close(1000, "leave");
    this.ws = null;
    this.peers.clear();
    this.vp.dispose();
  }

  // ---------------------------------------------------------------- 画面
  bind() {
    const signal = this.abort.signal;
    $("room-copy").addEventListener("click", async () => {
      try { await navigator.clipboard.writeText(location.href); }
      catch { $("room-url").select(); document.execCommand?.("copy"); }
      $("room-copy").textContent = "コピーしました";
      clearTimeout(this.copyTimer);
      this.copyTimer = setTimeout(() => { $("room-copy").textContent = "コピー"; }, 1500);
    }, { signal });
    $("room-name").addEventListener("input", () => {
      clearTimeout(this.nameTimer);
      this.nameTimer = setTimeout(() => {
        const n = cleanName($("room-name").value);
        if (!n || n === this.name) return;
        this.name = n;
        try { localStorage.setItem(NAME_KEY, n); } catch { /* 保存できなくても続ける */ }
        this.send({ t: "name", name: n });
      }, 500);
    }, { signal });
    $("room-peers").addEventListener("click", e => {
      const id = e.target.closest("[data-follow]")?.dataset.follow;
      if (!id) return;
      this.follow = this.follow === id ? null : id;
      const v = this.peers.get(id)?.view;
      if (this.follow && v) this.vp.setView(v);
      this.renderPeers();
    }, { signal });
    const root = $("room-viewport");
    root.addEventListener("pointermove", e => { const p = this.vp.pointAt(e); this.pendingCursor = p ? p.toArray().map(n => Math.round(n * 1000) / 1000) : null; }, { signal });
    root.addEventListener("pointerleave", () => { this.pendingCursor = null; }, { signal });
  }

  status(text, kind = "") {
    const el = $("room-status");
    el.textContent = text;
    el.className = `room-status small ${kind ? `is-${kind}` : ""}`;
  }

  renderPeers() {
    const ul = $("room-peers");
    ul.replaceChildren();
    const list = [this.me && { ...this.me, self: true }, ...this.peers.values()].filter(Boolean);
    for (const p of list) {
      const li = document.createElement("li");
      li.style.setProperty("--peer", p.color);
      const sw = document.createElement("span");
      sw.className = "swatch";
      const name = document.createElement("span");
      name.className = "peer-name flex-grow-1";
      name.textContent = p.self ? `${p.name}（あなた）` : p.name; // 表示名は文字としてだけ扱う
      li.append(sw, name);
      if (!p.self) {
        const b = document.createElement("button");
        b.type = "button";
        b.className = `btn btn-sm ${this.follow === p.id ? "btn-primary" : "btn-outline-secondary"}`;
        b.dataset.follow = p.id;
        b.textContent = this.follow === p.id ? "合わせるのをやめる" : "視点に合わせる";
        li.append(b);
      }
      ul.append(li);
    }
    this.vp.setRemotePeers([...this.peers.values()]);
  }

  /** ほかの人がロックしているオブジェクトを動かそうとしたら、理由を返す（Viewport の canTransform） */
  blockedBy(objs) {
    for (const o of objs) {
      const who = this.locks[o.userData.rid];
      if (who && who !== this.me?.id) return `${this.peers.get(who)?.name ?? "ほかの人"}さんが操作中のため、終わるまで動かせません`;
    }
    return null;
  }

  // ---------------------------------------------------------------- 通信
  connect() {
    if (this.closing) return;
    const proto = location.protocol === "https:" ? "wss:" : "ws:";
    this.status("接続中…");
    const ws = new WebSocket(`${proto}//${location.host}/api/rooms/${this.id}/ws`);
    this.ws = ws;
    ws.addEventListener("open", () => ws.send(JSON.stringify({ t: "hello", name: this.name })));
    ws.addEventListener("message", e => {
      let msg;
      try { msg = JSON.parse(e.data); } catch { return; }
      this.onMessage(msg);
    });
    ws.addEventListener("close", e => {
      if (this.ws !== ws || this.closing) return;
      this.me = null;
      this.peers.clear();
      this.renderPeers();
      if (e.code === 4001) { this.status("満員です（8 人まで）", "warn"); this.vp.setStatus("この部屋は満員です。別の部屋を作ってください"); return; }
      if (e.code === 1008) { this.status("切断されました", "warn"); return; }
      // 4000（しばらく通信がなかった）などは、つなぎ直す
      if (this.retries >= 6) { this.status("接続できません。ページを再読み込みしてください", "warn"); return; }
      const wait = Math.min(15000, 1000 * 2 ** this.retries++);
      this.status(this.retries === 1 && e.code === 1006 && !this.everJoined ? "接続できません（この環境では部屋を使えない可能性があります）" : "再接続しています…", "warn");
      this.retryTimer = setTimeout(() => this.connect(), wait);
    });
  }

  send(msg) {
    if (this.ws?.readyState === WebSocket.OPEN && this.me) this.ws.send(JSON.stringify(msg));
  }

  onMessage(msg) {
    switch (msg.t) {
      case "welcome":
        this.me = msg.you;
        this.everJoined = true;
        this.retries = 0;
        this.peers = new Map(msg.peers.map(p => [p.id, { ...p, cursor: null, selection: [], locks: [] }]));
        this.vp.loadRoomScene(msg.objects);
        this.server = new Map(this.vp.roomState().map(o => [o.id, JSON.stringify(o)]));
        this.status("接続しました", "ok");
        this.vp.setStatus("クリックして選択。G / R / S で動かす（変更はほかの人にも見えます）");
        this.renderPeers();
        this.sendSelection();
        break;
      case "join":
        this.peers.set(msg.peer.id, { ...msg.peer, cursor: null, selection: [], locks: [] });
        this.renderPeers();
        this.lastView = ""; // 新しい人にも自分の視点を送る
        break;
      case "peer": {
        if (msg.peer.id === this.me?.id) { this.me.name = msg.peer.name; this.renderPeers(); break; }
        const p = this.peers.get(msg.peer.id);
        if (p) { p.name = msg.peer.name; this.renderPeers(); }
        break;
      }
      case "leave":
        this.peers.delete(msg.id);
        if (this.follow === msg.id) this.follow = null;
        this.renderPeers();
        break;
      case "cursor": {
        const p = this.peers.get(msg.from);
        if (p) p.cursor = msg.p;
        break;
      }
      case "select": {
        const p = this.peers.get(msg.from);
        if (p) { p.selection = msg.ids; this.vp.updateRemoteSelection(); }
        break;
      }
      case "locks":
        this.locks = msg.locks;
        for (const p of this.peers.values()) p.locks = Object.keys(msg.locks).filter(id => msg.locks[id] === p.id);
        this.vp.updateRemoteSelection();
        break;
      case "lockdenied":
        // 同時に動かし始めたときなど: こちらの操作をやめる
        if (this.vp.op && !this.vp.op.edit) {
          this.vp.cancelOp();
          this.vp.setStatus("ほかの人が操作中のため、動かせませんでした");
        }
        this.lockHeld = false;
        break;
      case "preview":
        for (const d of msg.objs) {
          const o = this.vp.objects.find(x => x.userData.rid === d.id);
          if (!o || this.vp.op?.start?.some(s => s.o === o)) continue;
          o.position.fromArray(d.p);
          o.rotation.set(d.r[0], d.r[1], d.r[2]);
          o.scale.fromArray(d.s);
        }
        break;
      case "ops":
        this.vp.applyRemote({ upsert: msg.upsert ?? [], delete: msg.delete ?? [] });
        for (const o of msg.upsert ?? []) this.server.set(o.id, JSON.stringify(this.vp.roomState().find(x => x.id === o.id) ?? o));
        for (const id of msg.delete ?? []) this.server.delete(id);
        if (msg.rejected) this.vp.setStatus("ほかの人の操作と重なったため、一部の変更は反映されませんでした");
        break;
      case "view": {
        const p = this.peers.get(msg.from);
        if (!p) break;
        p.view = msg.view;
        if (this.follow === msg.from) this.vp.setView(msg.view);
        break;
      }
      case "error":
        if (msg.msg === "too many objects") this.vp.setStatus("この部屋のオブジェクトは 200 個までです");
        else console.warn("room:", msg.msg);
        break;
    }
  }

  // ---------------------------------------------------------------- 自分の操作
  onLocal(e) {
    if (!this.me) return;
    switch (e.type) {
      case "opstart":
        if (!e.edit && this.vp.op) {
          this.lockHeld = true;
          this.send({ t: "lock", ids: this.vp.op.start.map(s => s.o.userData.rid) });
        }
        break;
      case "confirm":
      case "cancel":
        // 途中経過を送ったものは、キャンセルで元に戻った場合も含めて最終の状態を必ず送る
        this.syncOut(this.previewed);
        this.previewed = new Set();
        if (this.lockHeld) { this.send({ t: "unlock" }); this.lockHeld = false; }
        this.lastPreview = "";
        break;
      case "select":
        if (!e.edit) this.sendSelection();
        break;
      case "view":
        // 自分で視点を動かしたら「視点に合わせる」をやめる
        if (this.follow) { this.follow = null; this.renderPeers(); }
        break;
      case "add": case "delete": case "duplicate": case "undo": case "redo": case "edit": case "separate": case "mode":
        this.syncOut();
        if (e.type !== "mode") this.sendSelection();
        break;
    }
  }

  sendSelection() {
    this.send({ t: "select", ids: this.vp.selected.map(o => o.userData.rid) });
  }

  /** サーバーの状態との差分を送る。操作中のオブジェクトは確定してから送る */
  syncOut(force = new Set()) {
    if (!this.me) return;
    const moving = new Set(this.vp.op && !this.vp.op.edit ? this.vp.op.start.map(s => s.o.userData.rid) : []);
    const upsert = [];
    const seen = new Set();
    for (const o of this.vp.roomState()) {
      seen.add(o.id);
      if (moving.has(o.id)) continue;
      const j = JSON.stringify(o);
      if (this.server.get(o.id) !== j || force.has(o.id)) { upsert.push(o); this.server.set(o.id, j); }
    }
    const del = [...this.server.keys()].filter(id => !seen.has(id) && !moving.has(id));
    for (const id of del) this.server.delete(id);
    if (upsert.length || del.length) this.send({ t: "ops", upsert, delete: del });
  }

  /** 80 ms ごと: 操作中の変形・カーソル・視点を、変わっていれば送る */
  tick() {
    if (!this.me) return;
    const op = this.vp.op;
    if (op && !op.edit && this.lockHeld) {
      const ids = new Set(op.start.map(s => s.o.userData.rid));
      const objs = this.vp.roomState().filter(o => ids.has(o.id)).map(({ id, p, r, s }) => ({ id, p, r, s }));
      const j = JSON.stringify(objs);
      if (j !== this.lastPreview && objs.length) {
        this.lastPreview = j;
        for (const o of objs) this.previewed.add(o.id);
        this.send({ t: "preview", objs });
      }
    }
    if (this.pendingCursor !== undefined) {
      this.send({ t: "cursor", p: this.pendingCursor });
      this.pendingCursor = undefined;
    }
    const now = performance.now();
    if (this.peers.size && now - this.lastViewAt > VIEW_MS) {
      const v = this.vp.getView();
      const view = { ...v, target: v.target.map(n => Math.round(n * 1000) / 1000), dist: Math.round(v.dist * 1000) / 1000, theta: Math.round(v.theta * 10000) / 10000, phi: Math.round(v.phi * 10000) / 10000 };
      const j = JSON.stringify(view);
      if (j !== this.lastView) { this.lastView = j; this.lastViewAt = now; this.send({ t: "view", view }); }
    }
  }
}

// ---------------------------------------------------------------- 起動
/** 部屋に入る。roomId は検査済み(小文字の英数字 6〜32 字)。返した dispose() を、ページを離れるときに呼ぶ */
export function start(roomId) {
  let client = null;
  try {
    client = new RoomClient(roomId);
    if (new URLSearchParams(location.search).get("debug") === "1") window.__room = client; // 確認用
  } catch (err) {
    console.error(err);
    $("room-status").textContent = "3D ビューを表示できません（WebGL が必要です）";
  }
  return {
    dispose() {
      if (!client) return;
      if (window.__room === client) delete window.__room;
      client.dispose();
      client = null;
    },
  };
}
