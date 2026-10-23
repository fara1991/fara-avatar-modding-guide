// 検索語の正規化（src/FaraAvatarModdingGuide.Web/wwwroot/js/search-norm.js の avatarSearch.norm。C# から呼ぶ）が、現行の searchcore.js の norm() と同じ結果かを確かめる。
//   node --test tests/js/search-norm-fn.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const core = { window: {}, console };
runInNewContext(readFileSync(new URL("../../wwwroot/js/searchcore.js", import.meta.url), "utf8"), core);
const browser = { window: {}, console };
runInNewContext(readFileSync(new URL("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/search-norm.js", import.meta.url), "utf8"), browser);

/** searchcore.js の norm() は外に出ていないので、prepare（題名に norm を掛ける）の結果で比べる */
const reference = (s) => core.window.faraSearch.prepare({ title: s })._t;
const norm = browser.window.avatarSearch.norm;

const queries = JSON.parse(readFileSync(new URL("./search-queries.json", import.meta.url), "utf8"));

test("検索語 264 件ほどの正規化が、searchcore.js と一致する", () => {
    assert.ok(queries.length >= 100);
    for (const q of queries) assert.equal(norm(q), reference(q), JSON.stringify(q));
});

test("全角・半角カナ・全角チルダ・カタカナ・大文字・結合文字・サロゲートペアも一致する", () => {
    const samples = ["Ｂｌｅｎｄｅｒ", "ｳｪｲﾄ", "ヴ", "～", "~", "1～2", "ァ", "ヶ", "ヷ", "ABC", "é", "é", "😀", "\ud800", "ﬁ", "㌔", "①", "Ⅷ", "　", ""];
    for (const s of samples) assert.equal(norm(s), reference(s), JSON.stringify(s));
});

test("文字以外が渡されても落ちない（String に直す）", () => {
    assert.equal(norm(undefined), reference(undefined));
    assert.equal(norm(12), reference(12));
});
