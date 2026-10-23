// 現行の SSR 版の書き出し(dotnet run -- --export)と、WebAssembly 移行版の静的 HTML で、本文を比べる(計画書 8: 記事 50 件の本文が一致)。
//   node tools/compare/compare-text.mjs <SSR の書き出し> <移行版の出力> [--articles-only] [--verbose] [--known tools/compare/known-differences.json]
//
// SSR の書き出しは、移行版とは別のコードで作ったものを使うこと(同じコミットの SSR は、記事を読み込む部分(src/FaraAvatarModdingGuide.Content)を移行版と共有しているため、
// 食い違いが見えない)。develop の SSR を別に取得して、Content/ だけを移行版のものに差し替えて書き出す(CI: .github/workflows/deploy.yml の「Export the SSR site from develop」)。
//
// 比べるもの:
//   ・記事(50 件): .article-body の文字(空白・改行を除く)と、本文の中の属性(href・src・alt・title・target・rel)。href・src は <base href="/"> に従って絶対 URL にしてから比べる
//   ・そのほかのページ: 本文(article.content / article.fsk-content)の文字と、同じ属性。既知の差(known-differences.json)だけを除いて比べる
// 既知の差は、ページのパスごとに、SSR 側・移行版側の文字の断片(ssr / new)と属性(ssrAttributes / newAttributes)、理由(reason)を書く。
// 本文が JavaScript で描かれていて、静的 HTML と作りが違うページ(練習・問い合わせ)は、skip: true(理由つき)で、その移行のフェーズまで比べない。
// 断片を取り除いたら一致することを確かめ、断片が見つからなければ(古くなった記述)失敗にする。
// 既知の差に書いていない差、記事が 50 件に満たない、移行版にページが無い、のどれかがあれば、終了コード 1。
import { readFileSync, readdirSync, statSync, existsSync } from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const flags = new Set(args.filter((a) => a.startsWith("--") && a !== "--known"));
const knownIndex = args.indexOf("--known");
const knownFile = knownIndex >= 0 ? args[knownIndex + 1] : join(dirname(fileURLToPath(import.meta.url)), "known-differences.json");
const positional = args.filter((a, i) => !a.startsWith("--") && !(knownIndex >= 0 && i === knownIndex + 1));
const [ssrDir, newDir] = positional;
if (!ssrDir || !newDir) { console.error("usage: compare-text.mjs <ssr-dist> <new-dist> [--articles-only] [--verbose] [--known <file>]"); process.exit(2); }
const known = existsSync(knownFile) ? JSON.parse(readFileSync(knownFile, "utf8")) : {};

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

/** class を持つ最初の要素の中身(入れ子の同名タグを数えて閉じを探す)。 */
function innerOf(html, tag, classPattern) {
    const open = new RegExp(`<${tag}\\b[^>]*class="[^"]*${classPattern}[^"]*"[^>]*>`, "i").exec(html);
    if (!open) return null;
    let depth = 1; const start = open.index + open[0].length; const re = new RegExp(`<(/?)${tag}\\b[^>]*>`, "gi"); re.lastIndex = start;
    for (let m; (m = re.exec(html)); ) { depth += m[1] ? -1 : 1; if (depth === 0) return html.slice(start, m.index); }
    return null;
}

/** 文字(空白を除く)。スクリプト・スタイル・SVG・template・noscript・コメントは除く。 */
function textOf(html) {
    return decode(html.replace(/<script\b[\s\S]*?<\/script>/gi, "").replace(/<style\b[\s\S]*?<\/style>/gi, "").replace(/<svg\b[\s\S]*?<\/svg>/gi, "")
        .replace(/<template\b[\s\S]*?<\/template>/gi, "").replace(/<noscript\b[\s\S]*?<\/noscript>/gi, "").replace(/<!--[\s\S]*?-->/g, "").replace(/<[^>]+>/g, " ")).replace(/\s+/g, "");
}

/** ページの <base href> に従って href・src を絶対(サイトのルートからの)URL にしたうえで、(タグ 属性=値)の並びにする。alt・title・target・rel はそのまま。 */
function attributesOf(html, fullHtml, pagePath) {
    const baseHref = /<base\b[^>]*href="([^"]*)"/i.exec(fullHtml)?.[1] ?? "/";
    const base = new URL(baseHref, `http://site.test${pagePath}`);
    const out = [];
    const tagRe = /<(a|img|source|video|link)\b([^>]*)>/gi;
    for (let m; (m = tagRe.exec(html)); ) {
        const attrs = m[2];
        for (const name of ["href", "src", "alt", "title", "target", "rel"]) {
            const v = new RegExp(`\\s${name}="([^"]*)"`, "i").exec(attrs)?.[1];
            if (v === undefined) continue;
            const value = decode(v);
            const shown = name === "href" || name === "src" ? new URL(value, base).href.replace("http://site.test", "") : value;
            out.push(`${m[1].toLowerCase()} ${name}=${shown}`);
        }
    }
    return out;
}

function firstDiff(a, b) {
    let i = 0; while (i < a.length && i < b.length && a[i] === b[i]) i++;
    return `最初の差: SSR「${a.slice(Math.max(0, i - 10), i + 40)}」 / 移行版「${b.slice(Math.max(0, i - 10), i + 40)}」`;
}

/** 既知の差の断片を取り除く。見つからない断片(古くなった記述)は problems に積む。 */
function removeKnown(text, snippets, path, side, problems) {
    let out = text;
    for (const s of snippets ?? []) {
        const normalized = s.replace(/\s+/g, "");
        if (!out.includes(normalized)) { problems.push(`既知の差が見つからない(古くなった記述): ${path} ${side}「${s}」`); continue; }
        out = out.replace(normalized, "");
    }
    return out;
}

const MIN_ARTICLES = 50;
let articleCount = 0, pageCount = 0, failures = 0;
const problems = [];
const skipped = [];
const seenKnown = new Set();
for (const file of walk(ssrDir)) {
    if (!file.endsWith(".html")) continue;
    const rel = relative(ssrDir, file).split(sep).join("/");
    if (rel === "404.html") continue;
    const isArticle = /^learn\/[^/]+\/[^/]+\.html$/.test(rel);
    if (flags.has("--articles-only") && !isArticle) continue;
    const pagePath = rel === "index.html" ? "/" : "/" + rel.replace(/\.html$/, "");
    const ssrFull = readFileSync(file, "utf8");
    const newFile = join(newDir, rel);
    if (!existsSync(newFile)) { console.log(`MISSING ${rel}`); failures++; continue; }
    const newFull = readFileSync(newFile, "utf8");

    if (isArticle) {
        const sa = innerOf(ssrFull, "div", "article-body"), sb = innerOf(newFull, "div", "article-body");
        if (sa === null || sb === null) { console.log(`NO-BODY ${rel}`); failures++; continue; }
        articleCount++;
        const ta = textOf(sa), tb = textOf(sb);
        if (ta !== tb) { failures++; console.log(`DIFF article text ${rel} (SSR ${ta.length} / 移行版 ${tb.length}) ${firstDiff(ta, tb)}`); }
        const aa = attributesOf(sa, ssrFull, pagePath), ab = attributesOf(sb, newFull, pagePath);
        if (aa.join("\n") !== ab.join("\n")) {
            failures++;
            const i = aa.findIndex((x, k) => x !== ab[k]);
            console.log(`DIFF article attributes ${rel} (SSR ${aa.length} / 移行版 ${ab.length}) 最初の差: SSR「${aa[i]}」 / 移行版「${ab[i]}」`);
        }
        if (flags.has("--verbose")) console.log(`ok ${rel} 文字 ${ta.length} 属性 ${aa.length}`);
    } else {
        const sa = innerOf(ssrFull, "article", "content"), sb = innerOf(newFull, "article", "fsk-content");
        if (sa === null || sb === null) { console.log(`NO-BODY ${rel}`); failures++; continue; }
        pageCount++;
        const entry = known[pagePath];
        if (entry) seenKnown.add(pagePath);
        if (entry?.skip) { skipped.push(`${pagePath}: ${entry.reason}`); continue; }
        const ta = removeKnown(textOf(sa), entry?.ssr, pagePath, "SSR", problems), tb = removeKnown(textOf(sb), entry?.new, pagePath, "移行版", problems);
        if (ta !== tb) { failures++; console.log(`DIFF page text ${rel} (SSR ${ta.length} / 移行版 ${tb.length}) ${firstDiff(ta, tb)}`); }
        const aa = attributesOf(sa, ssrFull, pagePath), ab = attributesOf(sb, newFull, pagePath);
        for (const [list, side, drop] of [[aa, "SSR", entry?.ssrAttributes], [ab, "移行版", entry?.newAttributes]]) {
            for (const d of drop ?? []) {
                const at = list.indexOf(d);
                if (at < 0) problems.push(`既知の差が見つからない(古くなった記述): ${pagePath} ${side} 属性「${d}」`); else list.splice(at, 1);
            }
        }
        if (aa.join("\n") !== ab.join("\n")) {
            failures++;
            const i = aa.findIndex((x, k) => x !== ab[k]);
            console.log(`DIFF page attributes ${rel} (SSR ${aa.length} / 移行版 ${ab.length}) 最初の差: SSR「${aa[i]}」 / 移行版「${ab[i]}」`);
        }
    }
}
for (const key of Object.keys(known)) {
    if (!flags.has("--articles-only") && !seenKnown.has(key)) problems.push(`既知の差の記述のページが、比べたページの中に無い: ${key}`);
}
for (const p of problems) console.log("PROBLEM " + p);
for (const sk of skipped) console.log("SKIP " + sk);
console.log(`記事 ${articleCount} 件・その他のページ ${pageCount} 件を比べた。差あり/問題 ${failures + problems.length} 件`);
// 記事が 50 件に満たないときも失敗にする(比べた件数が 0 でも成功してしまわないように)
if (articleCount < MIN_ARTICLES) { console.log(`比べた記事が ${articleCount} 件（${MIN_ARTICLES} 件以上が必要）`); failures++; }
process.exitCode = failures + problems.length > 0 ? 1 : 0;
