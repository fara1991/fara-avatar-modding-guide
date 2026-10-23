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

// ---- 既知の差(known-differences.json)で、同じ種類が並ぶ中の N 番目を指す(li@N)
const wrapPage = (cls, body) => `<html><head><base href="/"></head><body><article class="${cls}">${body}</article></body></html>`;

/** 記事 50 件(同じ)+ /privacy(SSR は項目が 1 つ多い)を作り、既知の差のファイルを渡して比べる */
function runKnown(known) {
    const root = mkdtempSync(join(tmpdir(), "cmp-"));
    try {
        for (const side of ["ssr", "new"]) {
            mkdirSync(join(root, side, "learn", "c"), { recursive: true });
            for (let i = 0; i < 50; i++) writeFileSync(join(root, side, "learn", "c", `a${i}.html`), page(BASE));
        }
        writeFileSync(join(root, "ssr", "privacy.html"), wrapPage("content", "<h2>保存</h2><ul><li>テーマ</li></ul><h2>外部</h2><ul><li>Cloudflare</li><li>jsDelivr</li><li>GitHub</li></ul>"));
        writeFileSync(join(root, "new", "privacy.html"), wrapPage("fsk-content", "<h2>保存</h2><ul><li>テーマ</li></ul><h2>外部</h2><ul><li>Cloudflare</li><li>GitHub</li></ul>"));
        const knownFile = join(root, "known.json");
        writeFileSync(knownFile, JSON.stringify(known));
        const r = spawnSync("node", [TOOL, join(root, "ssr"), join(root, "new"), "--known", knownFile], { encoding: "utf8" });
        return { code: r.status, out: r.stdout };
    } finally { rmSync(root, { recursive: true, force: true }); }
}

test("既知の差: 'li@3' で、3 番目の li(jsDelivr の項目)だけを SSR から除けば一致する", () => {
    const r = runKnown({ "/privacy": { reason: "jsDelivr を外した", ssr: ["jsDelivr"], ssrStructure: ["li@3"] } });
    assert.equal(r.code, 0, r.out);
});

test("既知の差: 'li'(最初の li)だと、位置が違うので、構造の差として見つかる", () => {
    const r = runKnown({ "/privacy": { reason: "最初の li は別のもの", ssr: ["jsDelivr"], ssrStructure: ["li"] } });
    assert.equal(r.code, 1);
    assert.match(r.out, /page structure/);
});

test("既知の差: 'li@9'(無い位置)は、古くなった記述として失敗にする", () => {
    const r = runKnown({ "/privacy": { reason: "ない", ssr: ["jsDelivr"], ssrStructure: ["li@3", "li@9"] } });
    assert.equal(r.code, 1);
    assert.match(r.out, /PROBLEM 既知の差が見つからない/);
});

test("既知の差が無ければ、本文の差(jsDelivr の項目)として失敗する", () => {
    const r = runKnown({});
    assert.equal(r.code, 1);
    assert.match(r.out, /page text/);
});
