// 現行の SSR 版の書き出し（dotnet run -- --export）と、WebAssembly 移行版の静的 HTML で、本文のテキストを比べる（計画書 8 の「記事 50 件の本文が一致」の検証）。
//   node tools/compare/compare-text.mjs <SSR の dist> <移行版の出力> [--articles-only] [--verbose]
// 記事は .article-body のテキスト（空白・改行を除く）を比べる。そのほかのページは、本文（article.content / article.fsk-content）全体を比べ、
// 既知の差（完了の記録など、JavaScript が差し込む部分）を除いて表示する。
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const args = process.argv.slice(2);
const flags = new Set(args.filter((a) => a.startsWith("--")));
const [ssrDir, newDir] = args.filter((a) => !a.startsWith("--"));
if (!ssrDir || !newDir) { console.error("usage: compare-text.mjs <ssr-dist> <new-dist> [--articles-only] [--verbose]"); process.exit(2); }

function* walk(dir) {
    for (const name of readdirSync(dir)) {
        const p = join(dir, name);
        if (statSync(p).isDirectory()) yield* walk(p); else yield p;
    }
}

const decode = (s) => s
    .replace(/&nbsp;/g, " ").replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&#39;|&#x27;/g, "'")
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n))).replace(/&#x([0-9a-f]+);/gi, (_, n) => String.fromCodePoint(parseInt(n, 16)))
    .replace(/&amp;/g, "&");

/** class を持つ最初の要素の中身（入れ子の同名タグを数えて閉じを探す）。 */
function inner(html, tag, classPattern) {
    const open = new RegExp(`<${tag}\\b[^>]*class="[^"]*${classPattern}[^"]*"[^>]*>`, "i").exec(html);
    if (!open) return null;
    let depth = 1; let i = open.index + open[0].length; const re = new RegExp(`<(/?)${tag}\\b[^>]*>`, "gi"); re.lastIndex = i;
    for (let m; (m = re.exec(html)); ) { depth += m[1] ? -1 : 1; if (depth === 0) return html.slice(open.index + open[0].length, m.index); }
    return null;
}

function text(html) {
    return decode(html.replace(/<script\b[\s\S]*?<\/script>/gi, "").replace(/<style\b[\s\S]*?<\/style>/gi, "").replace(/<svg\b[\s\S]*?<\/svg>/gi, "")
        .replace(/<template\b[\s\S]*?<\/template>/gi, "").replace(/<noscript\b[\s\S]*?<\/noscript>/gi, "").replace(/<[^>]+>/g, " ")).replace(/\s+/g, "");
}

let articleCount = 0, articleDiff = 0, pageCount = 0, pageDiff = 0;
for (const file of walk(ssrDir)) {
    if (!file.endsWith(".html")) continue;
    const rel = relative(ssrDir, file).split(sep).join("/");
    if (rel === "404.html") continue;
    const isArticle = /^learn\/[^/]+\/[^/]+\.html$/.test(rel);
    if (flags.has("--articles-only") && !isArticle) continue;
    const a = readFileSync(file, "utf8");
    let b;
    try { b = readFileSync(join(newDir, rel), "utf8"); } catch { console.log(`MISSING ${rel}`); pageDiff++; continue; }
    let ta, tb;
    if (isArticle) {
        ta = inner(a, "div", "article-body"); tb = inner(b, "div", "article-body");
        if (ta === null || tb === null) { console.log(`NO-BODY ${rel}`); articleDiff++; continue; }
        ta = text(ta); tb = text(tb);
        articleCount++;
        if (ta !== tb) { articleDiff++; console.log(`DIFF article ${rel} (ssr ${ta.length} / new ${tb.length})`); }
    } else {
        ta = inner(a, "article", "content"); tb = inner(b, "article", "fsk-content");
        if (ta === null || tb === null) { console.log(`NO-BODY ${rel}`); pageDiff++; continue; }
        ta = text(ta); tb = text(tb);
        pageCount++;
        if (ta !== tb) {
            pageDiff++;
            let i = 0; while (i < ta.length && ta[i] === tb[i]) i++;
            console.log(`DIFF page ${rel} (ssr ${ta.length} / new ${tb.length}) 最初の差: ssr「${ta.slice(Math.max(0, i - 10), i + 40)}」 new「${tb.slice(Math.max(0, i - 10), i + 40)}」`);
        }
    }
}
console.log(`記事 ${articleCount} 件中、差あり ${articleDiff} 件 / その他のページ ${pageCount} 件中、差あり ${pageDiff} 件`);
process.exitCode = articleDiff > 0 ? 1 : 0;
