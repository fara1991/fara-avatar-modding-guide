// ページ移動（wwwroot/js/nav.js）のテスト。  node --test tests/js/nav.test.mjs
// 画面の動きは Edge で確認する（ここでは、判定の関数と、ページ専用 JS の登録の仕方を確かめる）。
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const JS = new URL("../../wwwroot/js/", import.meta.url);
const ORIGIN = "https://avatar.fara-labs.com";

// document がない環境（node）で読み込むと、判定の関数と register だけが作られる
const sandbox = { window: {}, console };
runInNewContext(readFileSync(new URL("nav.js", JS), "utf8"), sandbox);
const nav = sandbox.window.faraNav;

const routable = (href) => nav.isRoutable(new URL(href), ORIGIN);

test("同じサイトのページ（拡張子なし・.html）は、ページ移動の対象になる", () => {
  assert.equal(routable(`${ORIGIN}/`), true);
  assert.equal(routable(`${ORIGIN}/learn/basics/01-select`), true);
  assert.equal(routable(`${ORIGIN}/learn/basics`), true);
  assert.equal(routable(`${ORIGIN}/report.html?from=/speed`), true);
  assert.equal(routable(`${ORIGIN}/search?q=abc`), true);
});

test("画像・JSON・API・別のサイトは、ブラウザに任せる（対象外）", () => {
  assert.equal(routable(`${ORIGIN}/learn/basics/images/modifiers-panel.png`), false);
  assert.equal(routable(`${ORIGIN}/search-index.json`), false);
  assert.equal(routable(`${ORIGIN}/api/rooms/abcdef/ws`), false);
  assert.equal(routable(`${ORIGIN}/_framework/blazor.web.js`), false);
  assert.equal(routable("https://example.com/learn/basics"), false);
});

test("register は、読み込み中でなければ、その場で 1 回呼ぶ", () => {
  let calls = 0;
  nav.register(() => { calls++; });
  assert.equal(calls, 1);
});

test("register で例外が出ても、ほかの登録は動く（ページ移動の途中で止まらない）", () => {
  const original = console.error;
  console.error = () => {};
  try {
    let after = 0;
    nav.register(() => { throw new Error("boom"); });
    nav.register(() => { after++; });
    assert.equal(after, 1);
  } finally {
    console.error = original;
  }
});

// ページ専用の JS は、移動のあとにもう一度呼ばれる。登録の書き方を確かめる（呼び方は、読み込みの仕組みを見る）
const PAGE_SCRIPTS = ["toc.js", "tipsfilter.js", "shortcuts.js", "search.js", "report.js", "quicksearch.js", "progress.js"];

for (const file of PAGE_SCRIPTS) {
  test(`${file} は faraNav.register で初期化を登録し、読み込みの仕方に頼らない`, () => {
    const src = readFileSync(new URL(file, JS), "utf8");
    assert.match(src, /window\.faraNav\.register\(/, `${file} に register がない`);
    assert.doesNotMatch(src, /document\.readyState === "loading"/, `${file} に readyState の分岐が残っている`);
  });
}

test("ヘッダーの検索欄（ページが替わっても残る）は、二重に付けない", () => {
  const src = readFileSync(new URL("quicksearch.js", JS), "utf8");
  assert.match(src, /hasAttribute\("data-qs-ready"\)/);
  assert.match(src, /globalBound/);
});

test("nav.js は、ページ移動の対象外（3D・WebSocket のページ）を読み込み直す判定を持つ", () => {
  const src = readFileSync(new URL("nav.js", JS), "utf8");
  assert.match(src, /script\[type="module"\], script\[type="importmap"\]/);
  assert.match(src, /history\.scrollRestoration = "manual"/);
});
