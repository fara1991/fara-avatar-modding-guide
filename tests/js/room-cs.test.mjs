// 共同練習の部屋（src/FaraAvatarModdingGuide.Web/wwwroot/js/room-cs.js）の、作成者の鍵の保存（localStorage）のテスト。  node --test tests/js/room-cs.test.mjs
// WebSocket・画面の部分は読まない（ブラウザでだけ動く）。保存の決まり（新しい順に 20 件・壊れた値・保存できない環境）を確かめる。
import { test } from "node:test";
import assert from "node:assert/strict";

/** localStorage の偽物。throwing が true なら、どの操作も例外にする（プライベートブラウズなど） */
function fakeLocalStorage({ throwing = false } = {}) {
  const map = new Map();
  const fail = () => { if (throwing) throw new Error("denied"); };
  return {
    map,
    getItem: (k) => { fail(); return map.has(k) ? map.get(k) : null; },
    setItem: (k, v) => { fail(); map.set(k, String(v)); },
    removeItem: (k) => { fail(); map.delete(k); },
  };
}

const KEY = "avatar-modding-guide.rooms";
const load = () => import("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/room-cs.js?owner-keys");

test("作成者の鍵: 保存したものを読める。知らない部屋は null", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  room.saveOwnerKey("roomaaaa1111", "0123456789abcdef0123456789abcdef");
  assert.equal(room.loadOwnerKey("roomaaaa1111"), "0123456789abcdef0123456789abcdef");
  assert.equal(room.loadOwnerKey("roombbbb2222"), null);
});

test("作成者の鍵: 同じ部屋の鍵は上書きし、件数は 1 つのまま", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  room.saveOwnerKey("roomaaaa1111", "aaaa");
  room.saveOwnerKey("roomaaaa1111", "bbbb");
  assert.equal(room.loadOwnerKey("roomaaaa1111"), "bbbb");
  assert.deepEqual(JSON.parse(globalThis.localStorage.getItem(KEY)).map(e => e.id), ["roomaaaa1111"]);
});

test("作成者の鍵: 20 件を超えたら、いちばん古いものから消す", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  for (let i = 0; i < 22; i++) room.saveOwnerKey(`room${String(i).padStart(8, "0")}`, `key${i}`);
  const ids = JSON.parse(globalThis.localStorage.getItem(KEY)).map(e => e.id);
  assert.equal(ids.length, 20);
  assert.equal(ids[0], "room00000002", "最初の 2 件は消えている");
  assert.equal(room.loadOwnerKey("room00000000"), null);
  assert.equal(room.loadOwnerKey("room00000021"), "key21");
});

test("作成者の鍵: 消すと読めなくなる。知らない部屋を消しても何も起きない", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  room.saveOwnerKey("roomaaaa1111", "aaaa");
  room.saveOwnerKey("roombbbb2222", "bbbb");
  room.forgetOwnerKey("roomaaaa1111");
  room.forgetOwnerKey("roomcccc3333");
  assert.equal(room.loadOwnerKey("roomaaaa1111"), null);
  assert.equal(room.loadOwnerKey("roombbbb2222"), "bbbb");
});

test("作成者の鍵: 壊れた値（JSON でない・配列・数）は空として扱い、そのまま保存し直せる", async () => {
  for (const broken of ["{not json", "[1,2]", "42", "null"]) {
    globalThis.localStorage = fakeLocalStorage();
    globalThis.localStorage.setItem(KEY, broken);
    const room = await load();
    assert.equal(room.loadOwnerKey("roomaaaa1111"), null, broken);
    room.saveOwnerKey("roomaaaa1111", "aaaa");
    assert.equal(room.loadOwnerKey("roomaaaa1111"), "aaaa", broken);
  }
});

test("作成者の鍵: localStorage が使えなくても例外にしない（読めないものは null）", async () => {
  globalThis.localStorage = fakeLocalStorage({ throwing: true });
  const room = await load();
  assert.doesNotThrow(() => room.saveOwnerKey("roomaaaa1111", "aaaa"));
  assert.doesNotThrow(() => room.forgetOwnerKey("roomaaaa1111"));
  assert.equal(room.loadOwnerKey("roomaaaa1111"), null);
});

test("作成者の鍵とパスワード: 両方を保存して読める。パスワードは作成者の端末の保存だけ（閉じたら消す）", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  room.saveOwnerKey("roomaaaa1111", "owner-key", "Kq7mZ2xR9tV4bN8wL3pY6hJ1cF5sD0gA");
  assert.equal(room.loadOwnerKey("roomaaaa1111"), "owner-key");
  assert.equal(room.loadOwnerPass("roomaaaa1111"), "Kq7mZ2xR9tV4bN8wL3pY6hJ1cF5sD0gA");
  room.forgetOwnerKey("roomaaaa1111");
  assert.equal(room.loadOwnerKey("roomaaaa1111"), null);
  assert.equal(room.loadOwnerPass("roomaaaa1111"), null, "閉じたら、鍵もパスワードも消える");
});

test("作成者の鍵: 以前の形（作成者の鍵の文字だけ）も読める。パスワードは無い（null）", async () => {
  globalThis.localStorage = fakeLocalStorage();
  globalThis.localStorage.setItem(KEY, JSON.stringify({ roomaaaa1111: "old-key" }));
  const room = await load();
  assert.equal(room.loadOwnerKey("roomaaaa1111"), "old-key");
  assert.equal(room.loadOwnerPass("roomaaaa1111"), null);
});

test("作成者の鍵: 部屋の ID が 10 桁の数字だけでも、新しい順（保存の順）で消す（オブジェクトの整数キーの並びに頼らない）", async () => {
  globalThis.localStorage = fakeLocalStorage();
  const room = await load();
  room.saveOwnerKey("9000000000", "oldest");          // 整数として大きい ID を最初に保存する
  for (let i = 0; i < 20; i++) room.saveOwnerKey(`100000000${i}`, `key${i}`);
  assert.equal(room.loadOwnerKey("9000000000"), null, "いちばん古い（最初に保存した）部屋が消える");
  assert.equal(room.loadOwnerKey("1000000000"), "key0");
});

test("作成者の鍵: 以前のオブジェクトの形（{ ID: { k, p } }）も読み替える", async () => {
  globalThis.localStorage = fakeLocalStorage();
  globalThis.localStorage.setItem(KEY, JSON.stringify({ roomaaaa1111: { k: "kk", p: "pp" } }));
  const room = await load();
  assert.equal(room.loadOwnerKey("roomaaaa1111"), "kk");
  assert.equal(room.loadOwnerPass("roomaaaa1111"), "pp");
});
