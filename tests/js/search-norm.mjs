// data/search.json（Build が書く検索の索引）の正規化済みの文字列が、tests/js/fixtures/searchcore.reference.js の norm() と同じ結果かを確かめる。
//   node tests/js/search-norm.mjs <data/search.json>
// 違う項目があれば一覧を出して終了コード 1。Build.Tests（SiteBuilderTests）からも呼ぶ。
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const file = process.argv[2];
if (!file) {
    console.error("usage: node tests/js/search-norm.mjs <data/search.json>");
    process.exit(2);
}

const source = readFileSync(new URL("./fixtures/searchcore.reference.js", import.meta.url), "utf8");
const sandbox = { window: {}, console };
runInNewContext(source, sandbox);
const search = sandbox.window.faraSearch;

const entries = JSON.parse(readFileSync(file, "utf8"));
const problems = [];
for (const entry of entries) {
    // searchcore.js の prepare は _t 題名・_s 小見出し・_x 本文・_r 読み を付ける
    const js = search.prepare({ title: entry.title, subtitle: entry.subtitle, text: entry.text, reading: entry.reading });
    for (const [jsKey, key] of [["_t", "titleNorm"], ["_s", "subtitleNorm"], ["_x", "textNorm"], ["_r", "readingNorm"]]) {
        if (js[jsKey] !== entry[key]) problems.push(`${entry.type} ${entry.url} ${key}`);
    }
}
if (problems.length > 0) {
    console.error(`正規化が searchcore.js と違う項目が ${problems.length} 件あります:\n${problems.slice(0, 20).join("\n")}`);
    process.exit(1);
}
console.log(`正規化が searchcore.js と一致しました（${entries.length} 件 × 4 項目）`);
