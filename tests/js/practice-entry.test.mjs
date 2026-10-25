// js/practice-entry.js（練習ページの入口）のテスト。  node --test tests/js/practice-entry.test.mjs
// Blazor のページ（PracticeHost.cs）が呼ぶ start / startArticle / dispose の決まり: PC では何もしない（3D は C# 版）・タッチだけの端末では案内と「画像で見る」・
// 同時に 1 つだけ・自分の id でだけ解放・始める途中の解放も漏らさない。three.js は使わない（S7）。
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
const reset = () => { entry.dispose("a"); entry.dispose("b"); queried.length = 0; touch = false; host.hidden = true; host.embeds = 0; };

test("PC: start does nothing (the 3D is the C# version, started by Blazor)", async () => {
    reset();
    await entry.start("a");
    assert.equal(host.hidden, true);
    assert.deepEqual(queried, []);
    entry.dispose("a");
});

test("touch-only devices show the guide on the practice page (no embeds: the text and links only)", async () => {
    reset();
    touch = true;
    await entry.start("a");
    assert.equal(host.hidden, false);
    assert.deepEqual(queried, []);
    entry.dispose("a");
});

test("touch-only devices with embeds mount the tutorials in the guide, and dispose them", async () => {
    reset();
    touch = true;
    host.embeds = 2;
    await entry.start("a");
    assert.equal(host.hidden, false);
    assert.deepEqual(queried, [["host", ".practice-embed[data-scenario]:not([data-cs])"]]);   // 案内の中の埋め込みに、画像で見る（tutorial.js）を始めた
    entry.dispose("a");
});

test("startArticle mounts the C# tutorials and the image tutorials of the article body, and disposes by id", async () => {
    reset();
    await entry.startArticle("a", ".article-body");
    assert.deepEqual(queried, [["article", ".practice-embed[data-scenario]:not([data-cs])"], ["article", ".practice-embed[data-scenario]:not([data-cs])"]]);   // C#（practice-cs）と tutorial.js が探す
    entry.dispose("b");   // 別の id では解放されない
    entry.dispose("a");
    entry.dispose("a");   // 2 回目は何もしない（例外にならない）
});

test("startArticle on a touch-only device mounts the image tutorials only (no C# 3D)", async () => {
    reset();
    touch = true;
    await entry.startArticle("a", ".article-body");
    assert.deepEqual(queried, [["article", ".practice-embed[data-scenario]:not([data-cs])"]]);
    entry.dispose("a");
});

test("startArticle without an article body does nothing", async () => {
    reset();
    await entry.startArticle("a", ".no-such-element");
    entry.dispose("a");
});
