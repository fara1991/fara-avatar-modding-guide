// アプリ内の移動のあとに head を合わせる部品(src/FaraAvatarModdingGuide.Web/wwwroot/js/head-sync.js)のテスト。  node --test tests/js/head-sync.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const SOURCE = readFileSync(new URL("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/head-sync.js", import.meta.url), "utf8");

const PAGES = {
    "/": { title: "ホーム - Site", description: "ホームの説明", ogType: "website" },
    "/learn/basics/select": { title: "選択 - Site", description: "選択の説明", ogType: "article" },
    "/report": { title: "問い合わせ - Site", description: "報告の説明", ogType: "website", noindex: true },
};

/** 最小の head の代用(タグを 1 つずつ持つ。robots は最初は無い) */
function setup({ path, pages = PAGES, fetchOk = true, bare = false }) {
    const make = (attrs) => ({
        attrs: { ...attrs },
        getAttribute(k) { return this.attrs[k] ?? null; },
        setAttribute(k, v) { this.attrs[k] = v; },
        parentNode: null,
    });
    const head = { children: [], appendChild(c) { c.parentNode = head; head.children.push(c); }, removeChild(c) { head.children.splice(head.children.indexOf(c), 1); } };
    const add = (attrs) => { const el = make(attrs); el.parentNode = head; head.children.push(el); return el; };
    if (!bare) {
        add({ rel: "canonical", href: "https://avatar.fara-labs.com" + (path === "/" ? "/" : path) });
        add({ name: "description", content: "最初の説明" });
        add({ property: "og:description", content: "最初の説明" });
        add({ property: "og:title", content: "最初の題名" });
        add({ property: "og:type", content: "website" });
        add({ property: "og:url", content: "https://avatar.fara-labs.com" + path });
    }
    const matches = (el, sel) => {
        const m = sel.match(/^(\w+)\[(\w+)="([^"]+)"\]$/);
        return m ? el.attrs[m[2]] === m[3] : false;
    };
    const doc = {
        head,
        querySelector: (sel) => head.children.find((c) => matches(c, sel)) ?? null,
        createElement: () => make({}),
    };
    const location = { pathname: path, href: "https://avatar.fara-labs.com" + path, origin: "https://avatar.fara-labs.com" };
    let fetched = 0;
    const sandbox = {
        window: {}, document: doc, location, URL, Promise, Object,
        fetch: async () => { fetched++; return { ok: fetchOk, status: fetchOk ? 200 : 500, json: async () => pages }; },
    };
    runInNewContext(SOURCE, sandbox);
    const tags = (sel) => head.children.filter((c) => matches(c, sel));
    return { sync: sandbox.window.avatarHead.sync, location, doc, tags, fetched: () => fetched, head };
}

test("移った先の値に、既存のタグを書き換える(タグは増えない)", async () => {
    const t = setup({ path: "/" });
    t.location.pathname = "/learn/basics/select";
    await t.sync("/learn/basics/select");
    assert.equal(t.tags('link[rel="canonical"]').length, 1);
    assert.equal(t.tags('link[rel="canonical"]')[0].attrs.href, "https://avatar.fara-labs.com/learn/basics/select");
    assert.equal(t.tags('meta[name="description"]')[0].attrs.content, "選択の説明");
    assert.equal(t.tags('meta[property="og:description"]')[0].attrs.content, "選択の説明");
    assert.equal(t.tags('meta[property="og:title"]')[0].attrs.content, "選択 - Site");
    assert.equal(t.tags('meta[property="og:type"]')[0].attrs.content, "article");
    assert.equal(t.tags('meta[property="og:url"]')[0].attrs.content, "https://avatar.fara-labs.com/learn/basics/select");
    assert.equal(t.head.children.length, 6);
});

test("noindex のページへ移ると robots を 1 つ足し、そこから出ると外す(増やさない)", async () => {
    const t = setup({ path: "/" });
    t.location.pathname = "/report";
    await t.sync("/report");
    await t.sync("/report");
    assert.equal(t.tags('meta[name="robots"]').length, 1);
    assert.equal(t.tags('meta[name="robots"]')[0].attrs.content, "noindex");
    t.location.pathname = "/";
    await t.sync("/");
    assert.equal(t.tags('meta[name="robots"]').length, 0);
});

test("末尾のスラッシュは外して引く。pages.json に無いパス(404 など)では何もしない", async () => {
    const t = setup({ path: "/" });
    t.location.pathname = "/learn/basics/select/";
    await t.sync("/learn/basics/select/");
    assert.equal(t.tags('link[rel="canonical"]')[0].attrs.href, "https://avatar.fara-labs.com/learn/basics/select");
    const before = JSON.stringify(t.head.children.map((c) => c.attrs));
    t.location.pathname = "/no-such-page";
    await t.sync("/no-such-page");
    assert.equal(JSON.stringify(t.head.children.map((c) => c.attrs)), before);
});

test("待っているあいだに別のページへ移っていたら、古い結果で上書きしない。pages.json は 1 回だけ読む", async () => {
    const t = setup({ path: "/" });
    t.location.pathname = "/report";
    const stale = t.sync("/learn/basics/select");      // 読み込みの最中に、すでに /report にいる
    await stale;
    assert.equal(t.tags('meta[name="description"]')[0].attrs.content, "最初の説明");
    await t.sync("/");
    assert.equal(t.fetched(), 1);
});

test("pages.json が読めなくても例外にしない(何も変えず、次の移動でもう一度読む)", async () => {
    const t = setup({ path: "/", fetchOk: false });
    t.location.pathname = "/report";
    await t.sync("/report");
    assert.equal(t.tags('meta[name="robots"]').length, 0);
    await t.sync("/report");
    assert.equal(t.fetched(), 2);
});

test("404.html から移ったとき(head に canonical・description・OGP が無い)は、固定のタグを 1 つずつ作る。2 回移っても増えない", async () => {
    const t = setup({ path: "/no-such-page", bare: true });
    t.location.pathname = "/learn/basics/select";
    await t.sync("/learn/basics/select");
    await t.sync("/learn/basics/select");
    assert.equal(t.tags('link[rel="canonical"]').length, 1);
    assert.equal(t.tags('link[rel="canonical"]')[0].attrs.href, "https://avatar.fara-labs.com/learn/basics/select");
    for (const [attr, key, value] of [["name", "description", "選択の説明"], ["property", "og:description", "選択の説明"], ["property", "og:title", "選択 - Site"], ["property", "og:type", "article"], ["property", "og:url", "https://avatar.fara-labs.com/learn/basics/select"]]) {
        const found = t.tags(`meta[${attr}="${key}"]`);
        assert.equal(found.length, 1, key);
        assert.equal(found[0].attrs.content, value, key);
    }
    assert.equal(t.head.children.length, 6);
});
