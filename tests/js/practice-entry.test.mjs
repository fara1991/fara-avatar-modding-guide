// js/practice-entry.js(練習ページの入口)のテスト。  node --test tests/js/practice-entry.test.mjs
// Blazor のページ(PracticeHost.cs)が呼ぶ start / startArticle / dispose の決まり: 同時に 1 つだけ・自分の id でだけ解放・始める途中の解放も漏らさない・null はロビー。
// 3D 本体(Three.js)はブラウザが要るので、ここでは PC 用のスクリプトを模擬(fixtures/fake-pc.mjs)にして、入口の振る舞いだけを確かめる。
import { test } from "node:test";
import assert from "node:assert/strict";

let touch = false;
const queried = [];
const host = {
    hidden: true,
    embeds: 0,
    querySelector(selector) { return this.embeds > 0 && selector.includes("practice-embed") ? {} : null; },
    querySelectorAll(selector) { queried.push(["host", selector]); return []; },
};
const article = { querySelectorAll(selector) { queried.push(["article", selector]); return []; } };
globalThis.window = globalThis;
globalThis.matchMedia = () => ({ matches: touch, addEventListener() { } });
globalThis.document = {
    getElementById: (id) => (id === "touch-practice" ? host : null),
    querySelector: (selector) => (selector === ".article-body" ? article : null),
};
globalThis.location = { search: "" };
globalThis.IntersectionObserver = class { observe() { } disconnect() { } };
globalThis.ResizeObserver = class { observe() { } disconnect() { } };

const entry = await import("../../src/FaraAvatarModdingGuide.Web/wwwroot/js/practice-entry.js");
const FAKE = "../../../../tests/js/fixtures/fake-pc.mjs";   // `./${pcScript}` が、wwwroot/js から見たこの場所になる
const log = (globalThis.__fakePcLog ??= []);
const reset = () => { entry.dispose("a"); entry.dispose("b"); log.length = 0; queried.length = 0; touch = false; host.hidden = true; host.embeds = 0; };

test("PC: starts the script with the options and disposes it once, by the same id", async () => {
    reset();
    await entry.start("a", FAKE, "opt");
    assert.deepEqual(log, [["start", "opt"]]);
    entry.dispose("a");
    entry.dispose("a");
    assert.deepEqual(log, [["start", "opt"], ["dispose", "opt"]]);   // 2 回呼んでも、解放は 1 回
});

test("dispose with another id does nothing (a late dispose of the previous page must not stop the next page)", async () => {
    reset();
    await entry.start("a", FAKE, "opt");
    entry.dispose("b");
    assert.deepEqual(log, [["start", "opt"]]);
    entry.dispose("a");
    assert.equal(log.length, 2);
});

test("a new start disposes the previous one first, so only one runs at a time", async () => {
    reset();
    await entry.start("a", FAKE, "first");
    await entry.start("b", FAKE, "second");
    assert.deepEqual(log, [["start", "first"], ["dispose", "first"], ["start", "second"]]);
    entry.dispose("a");   // 前のページの解放が遅れて届いても、いまのものは止めない
    assert.equal(log.length, 3);
    entry.dispose("b");
    assert.deepEqual(log.at(-1), ["dispose", "second"]);
});

test("dispose while the script is still loading does not leak: it is started and then disposed", async () => {
    reset();
    const starting = entry.start("a", FAKE, "opt");   // import の途中
    entry.dispose("a");
    await starting;
    assert.deepEqual(log, [["start", "opt"], ["dispose", "opt"]]);
});

test("null options (the lobby of the shared room) starts no PC script", async () => {
    reset();
    await entry.start("a", FAKE, null);
    assert.deepEqual(log, []);
    entry.dispose("a");
});

test("touch-only devices show the guide and never load the PC script", async () => {
    reset();
    touch = true;
    await entry.start("a", FAKE, "opt");   // 埋め込みの無いページ(みんなで練習): 案内の文章だけ
    assert.equal(host.hidden, false);
    assert.deepEqual(log, []);
    entry.dispose("a");
});

test("touch-only devices with embeds mount the tutorials in the guide, and dispose them", async () => {
    reset();
    touch = true;
    host.embeds = 2;
    await entry.start("a", FAKE, "opt");
    assert.equal(host.hidden, false);
    assert.deepEqual(log, []);
    assert.deepEqual(queried, [["host", ".practice-embed[data-scenario]"]]);   // 案内の中の埋め込みに、tutorial.js を始めた
    entry.dispose("a");
});

test("startArticle mounts the tutorials of the article body and disposes by id", async () => {
    reset();
    await entry.startArticle("a", ".article-body");
    assert.deepEqual(queried, [["article", ".practice-embed[data-scenario]"]]);
    entry.dispose("b");   // 別の id では解放されない
    entry.dispose("a");
    entry.dispose("a");   // 2 回目は何もしない(例外にならない)
});

test("startArticle without an article body does nothing", async () => {
    reset();
    await entry.startArticle("a", ".no-such-element");
    entry.dispose("a");
});

test("a script that cannot be loaded shows a message in the empty frame and does not throw", async () => {
    reset();
    const frame = { childElementCount: 0, textContent: "" };
    const original = globalThis.document.querySelector;
    globalThis.document.querySelector = (selector) => (selector.includes("#practice-viewport") ? frame : original(selector));
    const consoleError = console.error;
    console.error = () => { };
    try {
        await entry.start("a", "no-such-file.mjs", "opt");
    } finally {
        console.error = consoleError;
        globalThis.document.querySelector = original;
    }
    assert.match(frame.textContent, /読み込めませんでした/);
    assert.deepEqual(log, []);
    entry.dispose("a");   // 始まっていなくても、解放は例外にならない
});
