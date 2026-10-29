// /practice/room の C# 版(?engine=cs)の JavaScript 側。規則(部屋の同期・表示の中身)は C# の RoomSession が持つ。ここは、ブラウザにしか無いものだけ:
//   WebSocket（worker/index.js の Durable Object。/api/rooms/<ID>/ws）・setTimeout / setInterval・localStorage・ポインターの位置・クリップボード・確認用の window.__room
// 通知は、C# が setHandlers で渡した関数に、接続の番号つきで届ける（onOpen(id) / onMessage(id, text) / onClose(id, code)）。
// 使い方の決まり(room.js と同じ): 同時に 1 つの部屋だけ。C# が Dispose で退室し、close の通知は C# 側で無視される。
const NAME_KEY = "avatar-modding-guide.name";
// 作った部屋の作成者の鍵とパスワード { 部屋の ID: { k, p } }（この端末で作った、閉じていない部屋だけ。新しい順に最大 20 件。閉じたら消す）
const ROOMS_KEY = "avatar-modding-guide.rooms";
const ROOMS_MAX = 20;

let handlers = null;
const sockets = new Map();
let nextSocket = 1;
const timers = new Map();
let nextTimer = 1;
const attachments = new Map();
let nextAttachment = 1;

/** 接続・メッセージ・切断の通知先（C# の RoomCsEngine）。null で止める。 */
export function setHandlers(onOpen, onMessage, onClose) {
  handlers = onOpen && onMessage && onClose ? { onOpen, onMessage, onClose } : null;
}

/** 部屋の接続を 1 本開く。返すのは接続の番号。 */
export function open(roomId) {
  const id = nextSocket++;
  const proto = location.protocol === "https:" ? "wss:" : "ws:";
  const ws = new WebSocket(`${proto}//${location.host}/api/rooms/${roomId}/ws`);
  sockets.set(id, ws);
  ws.addEventListener("open", () => handlers?.onOpen(id));
  ws.addEventListener("message", e => handlers?.onMessage(id, String(e.data)));
  ws.addEventListener("close", e => {
    sockets.delete(id);
    handlers?.onClose(id, e.code);
  });
  return id;
}

/** 開いている接続に文字を送る（開いていなければ何もしない）。 */
export function send(id, text) {
  const ws = sockets.get(id);
  if (ws && ws.readyState === WebSocket.OPEN) ws.send(text);
}

/** 接続を閉じる（code・reason は WebSocket の close）。 */
export function close(id, code, reason) {
  sockets.get(id)?.close(code, reason);
}

/** タイマー。repeat なら ms ごとに、そうでなければ 1 回だけ cb を呼ぶ。返すのは取り消しの番号。 */
export function timer(ms, repeat, cb) {
  const id = nextTimer++;
  const handle = repeat
    ? setInterval(() => cb(), ms)
    : setTimeout(() => { timers.delete(id); cb(); }, ms);
  timers.set(id, { handle, repeat });
  return id;
}

/** タイマーを取り消す（止まったものは無視する）。 */
export function cancelTimer(id) {
  const t = timers.get(id);
  if (!t) return;
  timers.delete(id);
  if (t.repeat) clearInterval(t.handle);
  else clearTimeout(t.handle);
}

/** performance.now()（ms）。視点の送信の間隔に使う（room.js と同じ時計）。 */
export function now() {
  return performance.now();
}

/** 保存された表示名（無ければ null。保存できない環境でも例外にしない）。 */
export function loadName() {
  try { return localStorage.getItem(NAME_KEY); } catch { return null; }
}

/** 表示名を保存する（保存できなくても続ける）。 */
export function saveName(name) {
  try { localStorage.setItem(NAME_KEY, name); } catch { /* 保存できなくても続ける */ }
}

/**
 * 保存の形: [{ id: 部屋の ID, k: 作成者の鍵, p: 部屋のパスワード（無いこともある）, at: 保存した時刻 }]（古いものから順）。
 * 配列にするのは、部屋の ID が 10 桁の数字だけのとき、オブジェクトのキーは整数として並び、「新しい順」が崩れるため。
 * 以前の形（{ 部屋の ID: 作成者の鍵 } と { 部屋の ID: { k, p } }）も読み替える（並びは、読み替えた順）。
 */
function normalizeEntry(id, v) {
  if (typeof v === "string") return { id, k: v, p: null, at: 0 };
  if (v && typeof v === "object" && typeof v.k === "string") return { id, k: v.k, p: typeof v.p === "string" ? v.p : null, at: Number.isFinite(v.at) ? v.at : 0 };
  return null;
}

/** 保存の表（読めなければ空。保存できない環境でも例外にしない）。 */
function readRooms() {
  try {
    const v = JSON.parse(localStorage.getItem(ROOMS_KEY) ?? "[]");
    if (Array.isArray(v)) return v.map(e => (e && typeof e.id === "string" ? normalizeEntry(e.id, e) : null)).filter(Boolean);
    if (v && typeof v === "object") return Object.entries(v).map(([id, val]) => normalizeEntry(id, val)).filter(Boolean);
    return [];
  } catch {
    return [];
  }
}

function writeRooms(list) {
  try { localStorage.setItem(ROOMS_KEY, JSON.stringify(list)); } catch { /* 保存できなくても続ける */ }
}

/** この部屋の作成者の鍵（この端末で作った部屋のもの。無ければ null）。 */
export function loadOwnerKey(roomId) {
  return readRooms().find(e => e.id === roomId)?.k ?? null;
}

/** この部屋のパスワード（この端末で作った部屋のもの。無ければ null。作成者の画面で、確認・コピーに使う）。 */
export function loadOwnerPass(roomId) {
  return readRooms().find(e => e.id === roomId)?.p ?? null;
}

/** 作成者の鍵とパスワードを保存する（部屋を作ったとき）。同じ部屋は作り直し、古いものから消して、ROOMS_MAX 件に保つ。 */
export function saveOwnerKey(roomId, key, pass = null) {
  const list = readRooms().filter(e => e.id !== roomId);
  list.push({ id: roomId, k: key, p: typeof pass === "string" ? pass : null, at: Date.now() });
  writeRooms(list.slice(-ROOMS_MAX));
}

/** 作成者の鍵を消す（部屋が閉じたとき）。 */
export function forgetOwnerKey(roomId) {
  const list = readRooms();
  const next = list.filter(e => e.id !== roomId);
  if (next.length !== list.length) writeRooms(next);
}

/**
 * ビュー（rootId）のポインターの移動・離れたこと、と、ページを離れること（pagehide）を通知する。返すのは取り消しの番号。
 * room.js の bind()・pagehide と同じ（pointermove は clientX / clientY）。
 */
export function attach(rootId, onMove, onLeave, onHide) {
  const ac = new AbortController();
  const { signal } = ac;
  const root = document.getElementById(rootId);
  root?.addEventListener("pointermove", e => onMove(e.clientX, e.clientY), { signal });
  root?.addEventListener("pointerleave", () => onLeave(), { signal });
  addEventListener("pagehide", () => onHide(), { signal });
  const id = nextAttachment++;
  attachments.set(id, ac);
  return id;
}

/** attach の取り消し。 */
export function detach(id) {
  attachments.get(id)?.abort();
  attachments.delete(id);
}

/**
 * ほかの人のカーソルを置く（room.js の updateRemoteCursors）。json は [{ id, hidden, x, y }]。
 * boxId の中の .vp-remote-cursor（data-peer = id）の表示と位置だけを変える（Blazor は、この transform と hidden に触らない）。
 */
export function placeCursors(boxId, json) {
  const box = document.getElementById(boxId);
  if (!box) return;
  for (const c of JSON.parse(json)) {
    const el = [...box.children].find(x => x.dataset.peer === c.id);
    if (!el) continue;
    if (c.hidden) { el.hidden = true; continue; }
    el.hidden = false;
    el.style.transform = `translate(${c.x}px, ${c.y}px)`;
  }
}

/** URL をコピーする。失敗したら、入力欄（urlInputId）の文字を選んでコピーする（room.js と同じ）。 */
export async function copy(text, urlInputId) {
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    const input = document.getElementById(urlInputId);
    input?.select();
    document.execCommand?.("copy");
  }
}

/** 確認用（?debug=1）: window.__room を、JS 版の room.js と同じ形（vp.objects[].userData.kind）で出す。 */
export function setDebug(json) {
  window.__room = JSON.parse(json);
}

export function clearDebug() {
  delete window.__room;
}
