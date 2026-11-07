// SSR 版のテーマの保存形式（文字列 light / dark）を、kit の形式（JSON）に直す処理のテスト。  node --test tests/js/storage-migrate.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const SOURCE = readFileSync(new URL("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/storage-migrate.js", import.meta.url), "utf8");
const KEY = "avatar-modding-guide.theme";

function run(initial, { broken = false } = {}) {
    const store = new Map(initial === undefined ? [] : [[KEY, initial]]);
    const localStorage = broken
        ? { getItem() { throw new Error("denied"); }, setItem() { throw new Error("denied"); } }
        : { getItem: (k) => (store.has(k) ? store.get(k) : null), setItem: (k, v) => store.set(k, String(v)) };
    runInNewContext(SOURCE, { localStorage, JSON });
    return store.get(KEY);
}

test("SSR 版の文字列（light / dark）は、同じ向きの JSON に直す", () => {
    assert.equal(run("light"), '{"Theme":"light"}');
    assert.equal(run("dark"), '{"Theme":"dark"}');
});

test("何も保存されていなければ、SSR 版の既定のダーク", () => {
    assert.equal(run(undefined), '{"Theme":"dark"}');
});

test("すでに JSON のとき・読めない値のときは、書き換えない（ユーザーの設定を壊さない）", () => {
    assert.equal(run('{"Theme":"light","Other":1}'), '{"Theme":"light","Other":1}');
    assert.equal(run("something-else"), "something-else");
    assert.equal(run(""), "");
});

test("localStorage が使えなくても例外にしない", () => {
    assert.doesNotThrow(() => run(undefined, { broken: true }));
});

test("2 回実行しても同じ（冪等）", () => {
    const first = run("light");
    assert.equal(run(first), first);
});
