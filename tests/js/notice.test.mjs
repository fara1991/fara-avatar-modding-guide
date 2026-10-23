// 静的 HTML のページに出す「更新予定」の帯（src/FaraAvatarModdingGuide.Web/wwwroot/js/notice.js）のテスト。  node --test tests/js/notice.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const SOURCE = readFileSync(new URL("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/notice.js", import.meta.url), "utf8");

/** 最小の DOM の代用（作った要素・入れた位置を見られればよい） */
function fakeDom({ withHost = true } = {}) {
    const make = (tag) => ({
        tag, className: "", textContent: "", attrs: {}, children: [],
        setAttribute(k, v) { this.attrs[k] = v; },
        appendChild(c) { this.children.push(c); return c; },
        insertBefore(c, ref) { this.children.splice(ref ? this.children.indexOf(ref) : this.children.length, 0, c); return c; },
        get firstChild() { return this.children[0] ?? null; },
        querySelector(sel) { return sel === ".fsk-notice" ? this.children.find((c) => c.className === "fsk-notice") ?? null : null; },
    });
    const host = make("div");
    host.children.push(make("header"));
    return { host, doc: { createElement: make, querySelector: (sel) => (sel === ".av-static" && withHost ? host : null) } };
}

function load(extra = {}) {
    const sandbox = { window: {}, JSON, ...extra };
    runInNewContext(SOURCE, sandbox);
    return sandbox.window.avatarNotice;
}

test("parse: notice のときだけ文を返す。文が空なら既定の文", () => {
    const { parse } = load();
    assert.equal(parse('{"state":"notice","message":" 10月25日に更新します "}').message, "10月25日に更新します");
    assert.equal(parse('{"state":"NOTICE","message":""}').message, "まもなくメンテナンスを行います。");
    assert.equal(parse('{"state":"off","message":""}'), null);
    assert.equal(parse('{"state":"on","message":"x"}'), null);     // 503 は Worker が全部に返すので、ここには来ない
    assert.equal(parse("<!doctype html><html>"), null);          // Worker が無く HTML が返ってきたとき
    assert.equal(parse(""), null);
    assert.equal(parse("null"), null);
});

test("render: 見出しと文を別の要素で、ページの枠の先頭に入れる。文は textContent（HTML として扱わない）", () => {
    const { render } = load();
    const { doc, host } = fakeDom();
    const band = render(doc, { message: "<img src=x onerror=alert(1)>更新します" });
    assert.equal(host.children[0], band);                         // 先頭（ヘッダーより上）
    assert.equal(band.className, "fsk-notice");
    assert.equal(band.attrs.role, "status");
    assert.equal(band.children[0].textContent, "【お知らせ】");
    assert.equal(band.children[1].textContent, "<img src=x onerror=alert(1)>更新します");
    assert.equal(band.children[1].tag, "span");
});

test("render: 2 回呼んでも帯は 1 つ。枠が無いページ（WebAssembly のページ）では何もしない", () => {
    const { render } = load();
    const { doc, host } = fakeDom();
    render(doc, { message: "a" });
    assert.equal(render(doc, { message: "b" }), null);
    assert.equal(host.children.filter((c) => c.className === "fsk-notice").length, 1);
    assert.equal(render(fakeDom({ withHost: false }).doc, { message: "a" }), null);
});

test("読み込み: 枠のあるページで /api/maintenance を 1 回読み、notice なら帯を出す", async () => {
    const { doc, host } = fakeDom();
    const calls = [];
    const fetchStub = async (url, init) => { calls.push([url, init.cache]); return { ok: true, text: async () => '{"state":"notice","message":"更新します"}' }; };
    load({ document: doc, fetch: fetchStub, AbortController, setTimeout, clearTimeout });
    await new Promise((r) => setTimeout(r, 20));
    assert.deepEqual(calls, [["/api/maintenance", "no-store"]]);
    assert.equal(host.children[0].className, "fsk-notice");
});

test("読み込み: 通信に失敗・応答が通常のときは、何も出さず、例外にしない", async () => {
    for (const fetchStub of [async () => { throw new Error("offline"); }, async () => ({ ok: false, text: async () => "" }), async () => ({ ok: true, text: async () => '{"state":"off","message":""}' })]) {
        const { doc, host } = fakeDom();
        load({ document: doc, fetch: fetchStub, AbortController, setTimeout, clearTimeout });
        await new Promise((r) => setTimeout(r, 20));
        assert.equal(host.children.length, 1);
    }
});

test("読み込み: 枠が無いページ（WebAssembly のページ）では通信しない", async () => {
    const { doc } = fakeDom({ withHost: false });
    let called = false;
    load({ document: doc, fetch: async () => { called = true; return { ok: true, text: async () => "" }; }, AbortController, setTimeout, clearTimeout });
    await new Promise((r) => setTimeout(r, 20));
    assert.equal(called, false);
});
