// テーマが保存されていないときの既定（ダーク）のテスト。  node --test tests/js/theme-default.test.mjs
// theme-default.js は、kit の theme-init.js の直後に読まれ、保存が無い・読めないときだけ html の data-bs-theme を dark にする。
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const SOURCE = readFileSync(new URL("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/theme-default.js", import.meta.url), "utf8");
const KEY = "avatar-modding-guide.theme";

/** theme-init.js が light にした直後の html を用意して、theme-default.js を実行する */
function run(saved, { broken = false } = {}) {
    const attrs = new Map([["data-bs-theme", "light"]]);
    const store = new Map(saved === undefined ? [] : [[KEY, saved]]);
    const localStorage = broken
        ? { getItem() { throw new Error("denied"); } }
        : { getItem: (k) => (store.has(k) ? store.get(k) : null) };
    const document = { documentElement: { setAttribute: (k, v) => attrs.set(k, v), getAttribute: (k) => attrs.get(k) } };
    runInNewContext(SOURCE, { localStorage, document });
    return attrs.get("data-bs-theme");
}

test("保存が無いときは、SSR 版の既定のダーク", () => {
    assert.equal(run(undefined), "dark");
});

test("localStorage が使えない（例外）ときも、ダーク", () => {
    assert.equal(run(undefined, { broken: true }), "dark");
});

test("保存があるときは、触らない（ライトを選んだ人はライトのまま）", () => {
    assert.equal(run('{"Theme":"light"}'), "light");
    assert.equal(run('{"Theme":"dark"}'), "light");   // 属性は theme-init.js が決める。ここでは書き換えない
});
