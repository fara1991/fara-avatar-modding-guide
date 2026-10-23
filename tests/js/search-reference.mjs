// 現行の検索（tests/js/fixtures/searchcore.reference.js）を、同じ索引・同じ検索語で動かして、結果を JSON で出す。C# の SearchEngine（Web.Tests の SearchParityTests）の正解データ。
//   node tests/js/search-reference.mjs <索引の JSON（data/search.json と同じ形）> <検索語の JSON（文字列の配列）> <結果を書く JSON>
// 各検索語について: 正規化した検索語・件数・全件の並び（種類・題名・URL・点数）・上位の題名/小見出しの強調 HTML・本文の抜き出しと強調。
import { readFileSync, writeFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const [indexFile, queriesFile, outFile] = process.argv.slice(2);
if (!indexFile || !queriesFile || !outFile) {
    console.error("usage: node tests/js/search-reference.mjs <index.json> <queries.json> <out.json>");
    process.exit(2);
}
const source = readFileSync(new URL("./fixtures/searchcore.reference.js", import.meta.url), "utf8");
const sandbox = { window: {}, console };
runInNewContext(source, sandbox);
const S = sandbox.window.faraSearch;

const index = JSON.parse(readFileSync(indexFile, "utf8")).map((e) => S.prepare({ type: e.type, title: e.title, subtitle: e.subtitle, url: e.url, text: e.text, reading: e.reading }));
const queries = JSON.parse(readFileSync(queriesFile, "utf8"));
const TOP = 12;   // 強調・抜き出しを比べる件数

const results = queries.map((query) => {
    const hits = S.search(index, query);
    return {
        query,
        norm: S.prepare({ title: query })._t,   // 検索語の正規化（searchcore.js の norm）
        romaji: S.romajiToHiragana(S.prepare({ title: query })._t),   // ローマ字→ひらがなの変換(変換できなければ null)
        loose: S.looseKana(query),
        count: hits.length,
        hits: hits.map((h) => ({ type: h.entry.type, title: h.entry.title, url: h.entry.url, score: h.score })),
        top: hits.slice(0, TOP).map((h) => ({
            title: S.highlight(h.entry.title, query),
            subtitle: h.entry.subtitle ? S.highlight(h.entry.subtitle, query) : "",
            snippet: S.snippet(h.entry.text || "", query),
            snippetShort: S.snippet(h.entry.text, query, 100),
            snippetHtml: S.highlight(S.snippet(h.entry.text || "", query), query),
        })),
    };
});
writeFileSync(outFile, JSON.stringify(results), "utf8");
