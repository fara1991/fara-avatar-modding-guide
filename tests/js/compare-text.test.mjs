// tools/compare/compare-text.mjs が、文字が同じでも「構造」が違うものを見つけることの確認。  node --test tests/js/compare-text.test.mjs
// 見出し id の書き換え・ol→ul・table→div・属性(href)の違いが、検出される。同じものは通る。
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

const TOOL = new URL("../../tools/compare/compare-text.mjs", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");

const page = (body) => `<html><head><base href="/"></head><body><div class="article-body">${body}</div></body></html>`;
const BASE = '<h2 id="a">見出し</h2><ol><li>一</li><li>二</li></ol><table><tr><td>x</td></tr></table><p><a href="/learn/x">リンク</a></p>';

/** 50 件の記事(同じ中身)を、SSR と移行版に作り、移行版の 1 件だけ変える */
function run(mutate) {
    const root = mkdtempSync(join(tmpdir(), "cmp-"));
    try {
        for (const side of ["ssr", "new"]) {
            mkdirSync(join(root, side, "learn", "c"), { recursive: true });
            for (let i = 0; i < 50; i++) writeFileSync(join(root, side, "learn", "c", `a${i}.html`), page(side === "new" && i === 0 ? mutate(BASE) : BASE));
        }
        const r = spawnSync("node", [TOOL, join(root, "ssr"), join(root, "new"), "--articles-only"], { encoding: "utf8" });
        return { code: r.status, out: r.stdout };
    } finally { rmSync(root, { recursive: true, force: true }); }
}

test("同じ本文は一致する", () => assert.equal(run((h) => h).code, 0));
test("見出しの id の書き換えを見つける", () => { const r = run((h) => h.replace('id="a"', 'id="b"')); assert.equal(r.code, 1); assert.match(r.out, /structure/); });
test("ol → ul を見つける", () => { const r = run((h) => h.replace("<ol>", "<ul>").replace("</ol>", "</ul>")); assert.equal(r.code, 1); assert.match(r.out, /structure/); });
test("table → div を見つける", () => { const r = run((h) => h.replace("<table><tr><td>x</td></tr></table>", "<div><div>x</div></div>")); assert.equal(r.code, 1); assert.match(r.out, /structure/); });
test("href の違いを見つける", () => { const r = run((h) => h.replace("/learn/x", "/learn/y")); assert.equal(r.code, 1); assert.match(r.out, /attributes/); });
test("文字の違いを見つける", () => { const r = run((h) => h.replace("リンク", "リング")); assert.equal(r.code, 1); assert.match(r.out, /text/); });
