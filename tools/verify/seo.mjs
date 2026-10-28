// SEO の検査(Phase 4)。 NEW=http://127.0.0.1:8787 node seo.mjs [--no-browser]
// 配信された(wrangler dev など)サイトに対して、JavaScript を使わない取得(fetch)と、ブラウザ(Playwright)で確かめる。
//  1. 全 76 ページ + 404.html: 本文入りで返る(200・main の文字数)・title/description/canonical が 1 つずつ・OGP が揃っている・hreflang/JSON-LD が無い
//  2. 内部リンク全件: 各ページの a[href]・img[src]・link[href]・script[src] を <base href="/"> に対して解決し、同じサイトのものが 200 で返る。#見出し は、行き先の静的 HTML に id がある
//  3. 旧 URL 3 件は 301、知らない URL は 404(404.html・noindex)、末尾スラッシュは外れる、/search-index.json は JSON
//  4. 確認用(develop)は全 URL に X-Robots-Tag: noindex(HTML・404・JSON・画像)。本番の _headers には付かない(Build.Tests が確かめる)
//  5. ブラウザ: 起動の後も title/description/canonical/OGP/robots が 1 つずつ。アプリ内の移動(記事 → 記事)のあとの値(題名が替わること・重複が増えないこと・古い値が残らないこと)
// 終了コードは、失敗があれば 1。
import { launch, newPage } from "./lib.mjs";

const NEW = (process.env.NEW || "http://127.0.0.1:8787").replace(/\/$/, "");
const ORIGIN_EXPECT = process.env.ORIGIN_EXPECT || NEW;          // canonical の出どころ(確認用は NEW と違うことがあるので指定できる)
const NO_BROWSER = process.argv.includes("--no-browser");
const EXPECT_NOINDEX_HEADER = process.env.EXPECT_NOINDEX_HEADER !== "0";   // 確認用(develop)は 1(既定)。本番相当は 0
const FIXED = ["/", "/learn", "/shortcuts", "/addons", "/practice", "/practice/room", "/practice/unity", "/glossary", "/howto", "/tips", "/qa", "/links", "/changelog", "/report", "/privacy", "/search"];
const NOINDEX = new Set(["/report", "/search"]);
const fails = [];
const fail = (m) => { fails.push(m); console.log("  NG " + m); };
const ok = (m) => console.log("  ok " + m);
const get = async (path, init) => fetch(NEW + path, { redirect: "manual", ...init });
const text = async (path) => { const r = await get(path); return { r, body: await r.text() }; };

// --- 全ページの一覧: content.json のカテゴリ・記事 + 固定 16 ---
const content = await (await get("/data/content.json")).json();
const catIds = content.categories.map((c) => c.id);
const articleUrls = content.articles.map((a) => a.url);
const pages = [...FIXED, ...catIds.map((c) => `/learn/${c}`), ...articleUrls];
console.log(`ページ ${pages.length} 本(固定 ${FIXED.length} + カテゴリ ${catIds.length} + 記事 ${articleUrls.length})`);
if (pages.length !== 76) fail(`ページ数が 76 ではない: ${pages.length}`);

const count = (html, re) => (html.match(re) || []).length;
const one = (html, re) => (html.match(re) || [])[1];
const headOf = (html) => html.slice(0, html.indexOf("</head>"));
const bodyText = (html) => { const m = html.match(/<main[\s\S]*?<\/main>/); return m ? m[0].replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim() : ""; };

console.log("\n[1] 静的 HTML(JS 無し)");
const htmlByPath = new Map();
const MIN_TEXT = 200;
for (const path of pages) {
  const { r, body } = await text(path);
  htmlByPath.set(path, body);
  const head = headOf(body);
  const label = path;
  if (r.status !== 200) { fail(`${label}: ${r.status}`); continue; }
  if (!/text\/html/.test(r.headers.get("content-type") || "")) fail(`${label}: content-type ${r.headers.get("content-type")}`);
  // noindex の 2 ページ(問い合わせ・検索)は、フォームと検索欄だけで本文が短い。ほかは本文入りであること
  if (bodyText(body).length < MIN_TEXT && !NOINDEX.has(path)) fail(`${label}: 本文が短い(${bodyText(body).length} 文字)`);
  if (count(head, /<title>/g) !== 1) fail(`${label}: title が ${count(head, /<title>/g)} 個`);
  if (count(head, /<meta name="description"/g) !== 1) fail(`${label}: description が ${count(head, /<meta name="description"/g)} 個`);
  if (count(head, /<link rel="canonical"/g) !== 1) fail(`${label}: canonical が ${count(head, /<link rel="canonical"/g)} 個`);
  const canonical = one(head, /<link rel="canonical" href="([^"]+)"/);
  if (canonical !== `${ORIGIN_EXPECT}${path === "/" ? "/" : path}`) fail(`${label}: canonical=${canonical}`);
  for (const k of ["og:site_name", "og:locale", "og:type", "og:title", "og:description", "og:url"]) if (count(head, new RegExp(`<meta property="${k}"`, "g")) !== 1) fail(`${label}: ${k} が ${count(head, new RegExp(`<meta property="${k}"`, "g"))} 個`);
  const type = one(head, /<meta property="og:type" content="([^"]+)"/);
  const wantType = /^\/learn\/[^/]+\/[^/]+$/.test(path) ? "article" : "website";
  if (type !== wantType) fail(`${label}: og:type=${type}(期待 ${wantType})`);
  if (one(head, /<title>(.*?)<\/title>/) !== one(head, /<meta property="og:title" content="([^"]*)"/)) fail(`${label}: title と og:title が違う`);
  if (one(head, /<meta name="description" content="([^"]*)"/) !== one(head, /<meta property="og:description" content="([^"]*)"/)) fail(`${label}: description と og:description が違う`);
  if (count(head, /<meta name="robots" content="noindex"/g) !== (NOINDEX.has(path) ? 1 : 0)) fail(`${label}: noindex の meta が期待と違う`);
  if (/hreflang|application\/ld\+json/.test(head)) fail(`${label}: hreflang か JSON-LD がある`);
  if (/<script(?![^>]*\bsrc=)(?![^>]*importmap)[^>]*>/.test(body.replace(/<!--[\s\S]*?-->/g, ""))) fail(`${label}: インラインの script がある`);
}
const ogImage = [...htmlByPath.values()].filter((h) => /og:image/.test(headOf(h))).length;
console.log(`  og:image が付いているページ: ${ogImage} / ${pages.length}`);
if (ogImage !== pages.length) fail(`og:image が全ページに付いていない(${ogImage} / ${pages.length})`);
for (const [label, h] of htmlByPath) {
  const head = headOf(h);
  const at = head.indexOf('<meta property="og:image"');
  for (const [key, want] of [["width", '1200'], ["height", '630']]) {
    const i = head.indexOf(`<meta property="og:image:${key}" content="${want}"`);
    if (at < 0 || i < at) fail(`${label}: og:image:${key}=${want} が og:image の後ろに無い`);
  }
  if (head.indexOf('<meta property="og:image:alt"') < at) fail(`${label}: og:image:alt が og:image の後ろに無い`);
  if (!/<meta name="twitter:card" content="summary_large_image"/.test(head)) fail(`${label}: twitter:card が summary_large_image でない`);
}
for (const [path, type] of [["/favicon.ico", /image\/(x-icon|vnd\.microsoft\.icon)/], ["/favicon.svg", /image\/svg\+xml/], ["/favicon-16x16.png", /image\/png/], ["/favicon-32x32.png", /image\/png/], ["/apple-touch-icon.png", /image\/png/], ["/og.png?v=1", /image\/png/]]) {
  const r = await get(path);
  const bytes = (await r.arrayBuffer()).byteLength;
  if (r.status !== 200 || !type.test(r.headers.get("content-type") || "") || bytes < 100) fail(`${path}: ${r.status} ${r.headers.get("content-type")} ${bytes}B`); else ok(`${path} は 200(${r.headers.get("content-type")}、${bytes}B)`);
}
{
  const { r, body } = await text("/definitely-not-a-page");
  if (r.status !== 404) fail(`知らない URL が ${r.status}`); else ok("知らない URL は 404");
  if (!/<meta name="robots" content="noindex"/.test(body)) fail("404 に noindex が無い"); else ok("404.html は noindex");
  htmlByPath.set("/404.html(知らない URL)", body);
}
if (fails.length === 0) ok("全ページの head と本文");

console.log("\n[2] 内部リンク全件(<base href=\"/\"> に対して解決)");
const base = new URL(NEW + "/");
const targets = new Map();      // URL(クエリ・# を除く) → 参照元の例
const fragments = [];           // [参照元, 行き先の path, id]
for (const [from, html] of htmlByPath) {
  const baseHref = one(html, /<base href="([^"]*)"/) || "/";
  const root = new URL(baseHref, from.startsWith("/") && !from.includes("(") ? NEW + from : NEW + "/");
  for (const m of html.matchAll(/<(a|img|link|script|source|video)\b[^>]*?\b(href|src)="([^"]*)"/g)) {
    const raw = m[3].replace(/&amp;/g, "&");
    if (!raw || /^(mailto:|tel:|javascript:|data:)/i.test(raw)) continue;
    let u;
    try { u = new URL(raw, root); } catch { fail(`${from}: URL が解釈できない ${raw}`); continue; }
    if (u.origin !== base.origin) continue;
    const key = u.pathname;
    if (!targets.has(key)) targets.set(key, from);
    if (u.hash.length > 1 && m[1] === "a") fragments.push([from, key, decodeURIComponent(u.hash.slice(1))]);
  }
}
console.log(`  内部の行き先 ${targets.size} 件を確かめる`);
let broken = 0;
for (const [path, from] of targets) {
  const r = await get(path);
  if (r.status !== 200) { broken++; fail(`リンク切れ ${path}(${r.status})。参照元の例: ${from}`); }
  else await r.arrayBuffer();
}
const idsByPath = new Map();
const idsOf = async (path) => {
  const p = path === "" ? "/" : path.replace(/\/$/, "") || "/";
  if (!idsByPath.has(p)) { const html = htmlByPath.get(p) ?? (await text(p)).body; idsByPath.set(p, new Set([...html.matchAll(/\b(?:id|name)="([^"]+)"/g)].map((m) => m[1]))); }
  return idsByPath.get(p);
};
let fragBroken = 0;
for (const [from, path, id] of fragments) {
  const ids = await idsOf(path);
  // 本文の外(Blazor が付ける id など)は静的 HTML に無いことがある: 絞り込みなどの動的な id は対象外にするため、見出し(h2〜h4)の id だけを必須にする
  if (!ids.has(id)) { fragBroken++; fail(`見出しへのリンク切れ ${path}#${id}(参照元 ${from})`); }
}
if (!broken && !fragBroken) ok(`リンク切れ 0(行き先 ${targets.size} 件・見出しへのリンク ${fragments.length} 件)`);

console.log("\n[3] 旧 URL・404・末尾スラッシュ・検索索引");
for (const [from, to] of [["/operations", "/learn"], ["/avatar-clothing", "/learn/avatar-clothing"], ["/plugins", "/addons"]]) {
  const r = await get(from);
  const loc = r.headers.get("location") || "";
  if (r.status !== 301 || new URL(loc, NEW).pathname !== to) fail(`${from}: ${r.status} ${loc}(期待 301 ${to})`); else ok(`${from} → ${to} 301`);
}
{
  const r = await get("/learn/basics/");
  const loc = r.headers.get("location") || "";
  if (![301, 307, 308].includes(r.status) || new URL(loc, NEW).pathname !== "/learn/basics") fail(`末尾スラッシュ: ${r.status} ${loc}`); else ok(`/learn/basics/ → /learn/basics ${r.status}`);
  const s = await get("/search-index.json");
  const j = await s.json();
  if (s.status !== 200 || !Array.isArray(j) || j.length < 100) fail("/search-index.json"); else ok(`/search-index.json ${j.length} 件`);
}
{
  const r = await get("/sitemap.xml");
  const robots = await (await get("/robots.txt")).text();
  if (EXPECT_NOINDEX_HEADER) {
    if (r.status === 200) fail("確認用に sitemap.xml がある"); else ok(`確認用に sitemap.xml は無い(${r.status})`);
  } else {
    const xml = await r.text();
    const n = count(xml, /<loc>/g);
    if (r.status !== 200 || n !== 74) fail(`sitemap.xml: ${r.status}・${n} URL(期待 74)`); else ok("sitemap.xml 74 URL");
    if (!/Sitemap: /.test(robots)) fail("robots.txt に Sitemap が無い");
  }
  console.log("  robots.txt: " + robots.trim().replace(/\n+/g, " / "));
}

console.log("\n[4] X-Robots-Tag");
for (const path of ["/", "/learn/basics/transform", "/report", "/definitely-not-a-page", "/search-index.json", "/images/site-icon.svg", "/robots.txt"]) {
  const r = await get(path);
  const tag = r.headers.get("x-robots-tag");
  const want = EXPECT_NOINDEX_HEADER ? "noindex" : null;
  if ((tag || null) !== want) fail(`${path}: X-Robots-Tag=${tag}(期待 ${want})`);
  await r.arrayBuffer();
}
ok(EXPECT_NOINDEX_HEADER ? "確認用は全 URL に X-Robots-Tag: noindex" : "本番相当は X-Robots-Tag なし");

if (!NO_BROWSER) {
  console.log("\n[5] ブラウザ(起動の後の head と、アプリ内の移動)");
  const b = await launch();
  const snapshot = (p) => p.evaluate(() => ({
    title: document.querySelectorAll("title").length, titleText: document.title,
    description: [...document.querySelectorAll('meta[name="description"]')].map((m) => m.content),
    canonical: [...document.querySelectorAll('link[rel="canonical"]')].map((l) => l.href),
    ogUrl: [...document.querySelectorAll('meta[property="og:url"]')].map((m) => m.content),
    ogTitle: [...document.querySelectorAll('meta[property="og:title"]')].map((m) => m.content),
    ogDescription: [...document.querySelectorAll('meta[property="og:description"]')].map((m) => m.content),
    ogType: [...document.querySelectorAll('meta[property="og:type"]')].map((m) => m.content),
    robots: [...document.querySelectorAll('meta[name="robots"]')].map((m) => m.content),
  }));
  const dup = (s, label) => {
    if (s.title !== 1) fail(`${label}: title が ${s.title} 個`);
    for (const k of ["description", "canonical", "ogUrl", "ogTitle", "ogDescription", "ogType"]) if (s[k].length !== 1) fail(`${label}: ${k} が ${s[k].length} 個`);
  };
  for (const path of ["/", "/learn", "/learn/basics", "/learn/basics/transform", "/learn/unity/modular-avatar-outfit", "/search", "/report", "/practice", "/privacy"]) {
    const p = await newPage(b, { width: 1280, height: 800 });
    await p.goto(NEW + path, { waitUntil: "load" });
    await p.waitForSelector(".mud-layout", { timeout: 60000 });
    await p.waitForTimeout(500);
    const s = await snapshot(p);
    dup(s, `起動後 ${path}`);
    const want = NOINDEX.has(path) ? 1 : 0;
    if (s.robots.length !== want) fail(`起動後 ${path}: robots meta が ${s.robots.length} 個(期待 ${want})`);
    if (s.canonical[0] !== `${ORIGIN_EXPECT}${path === "/" ? "/" : path}`) fail(`起動後 ${path}: canonical=${s.canonical[0]}`);
    // タブの題名は、静的 HTML の title と同じ(起動の前後で、区切りなどが変わらない)
    const staticTitle = (htmlByPath.get(path) ?? "").match(/<title>(.*?)<\/title>/)?.[1]?.replace(/&amp;/g, "&");
    if (staticTitle && s.titleText !== staticTitle) fail(`起動後 ${path}: document.title「${s.titleText}」が静的 HTML の title「${staticTitle}」と違う`);
    if (p.problems.length) fail(`起動後 ${path}: コンソール ${p.problems.join(" | ")}`);
    await p.context().close();
  }
  // アプリ内の移動: 記事 A → サイドバーの別カテゴリ → 記事
  const p = await newPage(b, { width: 1280, height: 800 });
  await p.goto(NEW + "/learn/basics/transform", { waitUntil: "load" });
  await p.waitForSelector(".mud-layout", { timeout: 60000 });
  const before = await snapshot(p);
  await p.locator('a.mud-nav-link[href="learn/modeling"]').click();
  await p.waitForURL("**/learn/modeling");
  await p.waitForTimeout(400);
  const after = await snapshot(p);
  dup(after, "アプリ内の移動のあと /learn/modeling");
  const wantTitle = (await (await get("/learn/modeling")).text()).match(/<title>(.*?)<\/title>/)[1].replace(/&amp;/g, "&");
  console.log(`  移動前 title: ${before.titleText}`);
  console.log(`  移動後 title: ${after.titleText}`);
  if (after.titleText !== wantTitle) fail(`移動のあとの document.title が違う: ${after.titleText}(期待 ${wantTitle})`); else ok("移動のあとの document.title は新しいページのもの");
  const staleCanonical = after.canonical[0] !== `${ORIGIN_EXPECT}/learn/modeling`;
  const staleDescription = after.description[0] !== (await (await get("/learn/modeling")).text()).match(/<meta name="description" content="([^"]*)"/)[1].replace(/&amp;/g, "&");
  console.log(`  移動のあとの canonical: ${after.canonical[0]}${staleCanonical ? "(古い値のまま)" : ""}`);
  console.log(`  移動のあとの og:type: ${after.ogType[0]}`);
  if (staleCanonical) fail("アプリ内の移動のあと、canonical が新しいページのものに替わっていない");
  if (staleDescription) fail("アプリ内の移動のあと、description が新しいページのものに替わっていない");
  await p.context().close();
  // 404 から他のページへアプリ内で移ったとき: 404.html の head は title と noindex だけなので、canonical・description・OGP が足され、noindex が外れる
  const q = await newPage(b, { width: 1280, height: 800 });
  await q.goto(NEW + "/no-such-page-here", { waitUntil: "load" });
  await q.waitForSelector(".fbc-not-found", { timeout: 60000 });
  const from404 = await snapshot(q);
  if (from404.canonical.length !== 0) fail("404 の head に canonical がある(あってはいけない)");
  await q.locator('a.mud-nav-link[href="learn/modeling"]').click();
  await q.waitForURL("**/learn/modeling");
  await q.waitForTimeout(500);
  const to = await snapshot(q);
  dup(to, "404 から移ったあと /learn/modeling");
  // canonical の出どころは、canonical が無いので、開いているホスト(location.origin)。本番・確認用の公開先では canonical の出どころと同じ
  if (to.canonical[0] !== `${NEW}/learn/modeling`) fail(`404 から移ったあとの canonical=${to.canonical[0]}`);
  if (to.robots.length !== 0) fail("404 から移ったあと、noindex の meta が残っている");
  ok(`404 から移ったあと: canonical・description・OGP が 1 つずつ(${to.ogType[0]})・noindex なし`);
  await q.context().close();
  await b.close();
}

console.log(fails.length ? `\n失敗 ${fails.length} 件` : "\n失敗 0 件");
process.exit(fails.length ? 1 : 0);
