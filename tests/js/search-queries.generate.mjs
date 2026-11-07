// 検索の突き合わせ（SearchParityTests）の検索語 tests/js/search-queries.json を作る。  node tests/js/search-queries.generate.mjs <data/search.json>
// 手で選んだ語（ローマ字・かな・全角・半角カナ・複数語・記号・結果なし など）と、索引から決まった規則で拾った語（題名の先頭・読み・小見出し・本文の一部）。
// 一度作って JSON をコミットする（索引の内容が変わっても、検索語は変えない。変えたいときだけ、このスクリプトで作り直す）。
import { readFileSync, writeFileSync } from "node:fs";

const index = JSON.parse(readFileSync(process.argv[2], "utf8"));

const handmade = [
    // #79 の読み（ローマ字・かな）の確認
    "yougo", "moderingu", "you", "name", "mode", "kami", "kennri", "konna", "kann", "matte", "ra-men", "burenda", "uxeito", "weito", "adoon", "adon",
    "ようご", "ヨウゴ", "ｙｏｕｇｏ", "モデリング", "もでりんぐ", "ほん", "ぎゅ", "ひょうき", "ひょき", "せいじん", "ほんぎゅう", "ぎゅう",
    // 全角・半角・カタカナ
    "Ｂｌｅｎｄｅｒ", "ＵＶ", "ｳｪｲﾄ", "ウェイト", "ウエイト", "ｱﾄﾞｵﾝ", "アドオン", "あどおん", "ユニティ", "ゆにてぃ", "ｂｌｅｎｄｅｒ　ｕｖ",
    // 英語・キー・記号
    "blender", "Blender", "BLENDER", "unity", "vrchat", "uv", "UV", "ctrl+r", "Ctrl+R", "ctrl r", "shift+d", "Shift+D", "F3", "tab", "Tab", "g", "x", "a", "あ", "頂", "3d", "2022.3", "4.5",
    "ctrl+", "(", "[", "*", "?", "\\", "+", ".", "&", "<script>", "\"", "'", "a&b", "<b>", "...", "…", "〜", "～", "~", "1〜2", "1～2",
    // 複数語・並びの違い
    "押し出し", "押し出し ループ", "ループ 押し出し", "ウェイト ペイント", "ウェイト　ペイント", "modular avatar", "modular  avatar", "  blender  ", " ", "", "\t", "weight paint", "shape key", "ピンク", "ピンク unity",
    // 結果なし・長い語
    "zzzzqx", "存在しない言葉", "あああああああ", "asdfghjklqwertyuiopzxcvbnm1234567890", "ブレンダーでアバターを改変するときにウェイトを転送する方法について詳しく",
    // サロゲートペア・結合文字・特殊な空白
    "😀", "𠮷", "é", "e\u0301", "\u00a0unity", "unity\u3000blender", "\ufeffunity",
];

/** 索引から、決まった規則で拾う（乱数は使わない）。 */
function sample() {
    const out = [];
    const seen = new Set(handmade);
    const add = (q) => { q = String(q).trim(); if (q && !seen.has(q)) { seen.add(q); out.push(q); } };
    const byType = (t) => index.filter((e) => e.type === t);
    for (const t of ["article", "glossary", "howto", "tip", "qa", "category", "link", "addon", "shortcut", "page"]) {
        const list = byType(t);
        const step = Math.max(1, Math.floor(list.length / 4));   // 種類ごとに 4 件ほど
        for (let i = 0; i < list.length && i < step * 4; i += step) {
            const e = list[i];
            add(e.title);                                  // 題名そのまま
            add([...e.title].slice(0, 2).join(""));          // 題名の先頭 2 文字
            if (e.reading) add([...e.reading].slice(0, 3).join(""));   // 読みの先頭 3 文字
            if (e.subtitle) add(e.subtitle);               // 小見出し
            const words = (e.text || "").split(/\s+/).filter((w) => w.length >= 3);
            if (words.length > 5) add(words[5].slice(0, 6));   // 本文の途中の語
        }
    }
    return out;
}

/** ローマ字変換表の全キー(searchcore.js の add("...") から取り出す)を、3 回ずつ並べた語と、全部つなげた語。変換表が 1 つでも違えば、突き合わせで見つかる */
function romajiKeys() {
    const source = readFileSync(new URL("./fixtures/searchcore.reference.js", import.meta.url), "utf8");
    const keys = [...new Set([...source.matchAll(/add\("([^"]+)"/g)].flatMap((m) => m[1].split(" ")))];
    return [...keys.map((k) => k.repeat(3)), keys.join(""), "ka-ki-ku", "kka", "ttsu", "nna", "nnya", "n'a", "xtsu", "ltu"];
}

const queries = [...new Set([...handmade, ...sample(), ...romajiKeys()])];
writeFileSync(new URL("./search-queries.json", import.meta.url), JSON.stringify(queries, null, 1) + "\n", "utf8");
console.log(`検索語 ${queries.length} 件（手で選んだ ${handmade.length} 件 + 索引から ${queries.length - handmade.length} 件）`);
