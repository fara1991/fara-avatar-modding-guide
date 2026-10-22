// サイト内検索（wwwroot/js/searchcore.js）の読みの判定のテスト。  node --test tests/js/search.test.mjs
// 題名の読み（reading）でローマ字・かなから探せること、既存の一致（題名・小見出し・本文）の点数が変わらないこと、
// 2 文字のかなは完全・前方一致だけ、長音の揺れを同じに扱うことを確かめる。
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const SRC = readFileSync(new URL("../../wwwroot/js/searchcore.js", import.meta.url), "utf8");
const sandbox = { window: {}, console };
runInNewContext(SRC, sandbox);
const S = sandbox.window.faraSearch;
const indexOf = (list) => list.map(S.prepare);

/** テスト用の索引（実際の索引と同じ形。reading は題名の読み） */
function index() {
  return [
    { type: "page", title: "用語集", subtitle: "", url: "/glossary", text: "Blender・Unity・VRChat の用語の意味", reading: "ようごしゅう" },
    { type: "category", title: "モデリング", subtitle: "Modeling", url: "/learn/modeling", text: "編集モードで頂点・辺・面を操作して形を作る。 Modeling", reading: "もでりんぐ" },
    { type: "article", title: "準備：アバターを Blender に読み込む", subtitle: "はじめに", url: "/learn/avatar/import", text: "Blender へ取り込む手順", reading: "じゅんびあばたーをぶれんだーによみこむ" },
    { type: "article", title: "聖人の話", subtitle: "基本", url: "/learn/a/seijin", text: "本文", reading: "せいじんのはなし" },
    { type: "article", title: "世人の話", subtitle: "基本", url: "/learn/a/sejin", text: "本文", reading: "せじんのはなし" },
    { type: "article", title: "ほんぎゅうの話", subtitle: "基本", url: "/learn/a/hon", text: "本文", reading: "ほんぎゅうのはなし" },
    { type: "article", title: "漢字", subtitle: "基本", url: "/learn/a/kanji", text: "本文", reading: "ほんぎゅう" },
    { type: "article", title: "冷やす", subtitle: "基本", url: "/learn/a/hie", text: "ひょうきの説明", reading: "ひやす" },
    { type: "article", title: "氷気", subtitle: "基本", url: "/learn/a/hyoki", text: "本文", reading: "ひょうき" },
    { type: "glossary", title: "頂点", subtitle: "vertex", url: "/glossary#vertex", text: "ちょうてん 頂点は点", reading: "ちょうてん" },
  ];
}
const urls = (hits) => Array.from(hits, (h) => h.entry.url);

test("ローマ字（ヘボン式）の入力で、題名の読みに当たる", () => {
  assert.deepEqual(urls(S.search(indexOf(index()), "yougo")), ["/glossary"]);
  assert.equal(S.romajiToHiragana("yougo"), "ようご");
});

test("ローマ字（訓令式）でも探せる。modeling は英語の小見出し、moderingu は読み", () => {
  assert.equal(urls(S.search(indexOf(index()), "moderingu"))[0], "/learn/modeling");
  assert.equal(urls(S.search(indexOf(index()), "modeling"))[0], "/learn/modeling");
});

test("英語の題名は、そのまま一致する", () => {
  assert.equal(urls(S.search(indexOf(index()), "blender"))[0], "/learn/avatar/import");
});

test("ひらがな・カタカナ・全角英字の入力でも探せる", () => {
  assert.deepEqual(urls(S.search(indexOf(index()), "ようご")), ["/glossary"]);
  assert.deepEqual(urls(S.search(indexOf(index()), "ヨウゴ")), ["/glossary"]);
  assert.deepEqual(urls(S.search(indexOf(index()), "ｙｏｕｇｏ")), ["/glossary"]);
});

test("2 文字のかなは、完全一致と前方一致だけ（「ぎゅ」で「ほんぎゅう」に当てない）", () => {
  // 「漢字」は読み「ほんぎゅう」だけで当たる項目
  assert.ok(urls(S.search(indexOf(index()), "ほん")).includes("/learn/a/kanji"));
  assert.ok(!urls(S.search(indexOf(index()), "ぎゅ")).includes("/learn/a/kanji"));
});

test("せいじんは、聖人だけに当たる（えい→えは正規化しない）", () => {
  assert.deepEqual(urls(S.search(indexOf(index()), "せいじん")), ["/learn/a/seijin"]);
  assert.equal(S.looseKana("せいじん"), "せいじん");
});

test("長音の揺れ（ひょうき・ひょき）は同じに扱う", () => {
  assert.equal(S.looseKana("ひょうき"), "ひょき");
  assert.equal(S.looseKana("ひょき"), "ひょき");
  assert.ok(urls(S.search(indexOf(index()), "ひょき")).includes("/learn/a/hyoki"));
  assert.ok(urls(S.search(indexOf(index()), "ひょうき")).includes("/learn/a/hyoki"));
});

test("ローマ字の nn は「ん」1 つ（kennri → けんり）", () => {
  assert.equal(S.romajiToHiragana("kennri"), "けんり");
  assert.equal(S.romajiToHiragana("konna"), "こんな");
  assert.equal(S.romajiToHiragana("kann"), "かん");
});

test("漢字・英語の入力は、読みを使わない（読みだけの項目は出ない）", () => {
  const withReading = urls(S.search(indexOf(index()), "用語"));
  const noReading = indexOf(index().map((e) => ({ ...e, reading: undefined })));
  assert.deepEqual(withReading, urls(S.search(noReading, "用語")));
  assert.deepEqual(urls(S.search(indexOf(index()), "用語")), ["/glossary"]);
});

test("読みを消した索引と比べて、既存の一致（題名・小見出し・本文）の順位は変わらない", () => {
  const queries = ["Blender", "モデリング", "頂点", "用語", "本文", "Modeling", "取り込む", "ひやす"];
  const withReading = indexOf(index());
  const without = indexOf(index().map((e) => ({ ...e, reading: undefined })));
  for (const q of queries) {
    const before = urls(S.search(without, q));
    const after = urls(S.search(withReading, q));
    // 読みを足したことで増えるものはあっても、読みを消した結果どうしの順は、そのまま残る
    const kept = after.filter((u) => before.includes(u));
    assert.deepEqual(kept, before, `順位の変化: ${q}`);
  }
});
