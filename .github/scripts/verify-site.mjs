// 公開する静的ファイル一式（Build の出力）が、公開先の環境（production / develop）のものか、wrangler deploy の前に確かめる。
// 「develop 用のビルドを本番に出す」「本番用のビルドを確認用に出す」取り違えを、公開の直前で止めるための関門（fail-closed）。
//
//   node .github/scripts/verify-site.mjs <静的ファイルのフォルダ> <production|develop>
//
// 本番:   X-Robots-Tag なし（検索に載せる）・sitemap.xml あり（Sitemap: の行が robots.txt にある）・canonical は https://avatar.fara-labs.com/
// 確認用: X-Robots-Tag: noindex あり・sitemap.xml なし・canonical は https://avatar-dev.fara-labs.com/
// どちらも: 起動に要る WebAssembly の一式（_framework）と、本文入りの静的 HTML・404.html・_headers（CSP）・_redirects がある。
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

export const ORIGINS = {
  production: "https://avatar.fara-labs.com",
  develop: "https://avatar-dev.fara-labs.com",
};

const read = (dir, name) => readFileSync(join(dir, name), "utf8");

/**
 * @param {string} dir 静的ファイルのフォルダ
 * @param {string} env "production" か "develop"
 * @returns {string[]} 問題の一覧（空なら公開してよい）
 */
export function verifySite(dir, env) {
  const origin = ORIGINS[env];
  if (!origin) return [`環境の指定が不正です: ${JSON.stringify(env)}（production か develop）`];
  const problems = [];
  const need = (name) => {
    const ok = existsSync(join(dir, name));
    if (!ok) problems.push(`${name} がありません`);
    return ok;
  };

  for (const name of ["index.html", "404.html", "_headers", "_redirects", "robots.txt", "learn/basics/transform.html", "data/content.json", "data/search.json"]) need(name);
  if (!existsSync(join(dir, "_framework")) || !readdirSync(join(dir, "_framework")).some((f) => f.startsWith("blazor.webassembly"))) {
    problems.push("_framework/blazor.webassembly* がありません（dotnet publish の出力ではありません）");
  }
  if (problems.length > 0) return problems;

  const headers = read(dir, "_headers");
  const robots = read(dir, "robots.txt");
  const index = read(dir, "index.html");
  const canonical = /<link rel="canonical" href="([^"]*)"/.exec(index)?.[1];

  if (!/^\s*Content-Security-Policy: /m.test(headers)) problems.push("_headers に Content-Security-Policy がありません");
  const noindex = /^\s*X-Robots-Tag:/mi.test(headers);

  if (env === "production") {
    if (noindex) problems.push("本番用のはずが、_headers に X-Robots-Tag があります（確認用のビルドです）");
    if (!existsSync(join(dir, "sitemap.xml"))) problems.push("本番用のはずが、sitemap.xml がありません");
    if (!robots.includes(`Sitemap: ${origin}/sitemap.xml`)) problems.push(`robots.txt に Sitemap: ${origin}/sitemap.xml がありません`);
    if (/^\s*Disallow:\s*\/\s*$/m.test(robots)) problems.push("robots.txt が全体を禁止しています");
  } else {
    if (!noindex) problems.push("確認用のはずが、_headers に X-Robots-Tag: noindex がありません（検索に載ってしまいます）");
    if (existsSync(join(dir, "sitemap.xml"))) problems.push("確認用のはずが、sitemap.xml があります（本番用のビルドです）");
  }
  if (canonical !== `${origin}/`) problems.push(`index.html の canonical が ${origin}/ ではありません（${canonical ?? "なし"}）`);

  // 別の環境のホスト名が、ほかの環境の出力に混ざっていない（本番の出力に avatar-dev、確認用の出力に avatar.fara-labs.com の URL がない）
  const other = env === "production" ? ORIGINS.develop : ORIGINS.production;
  const stray = [];
  for (const name of ["index.html", "404.html", "robots.txt", "learn/basics/transform.html", "data/site.json"]) {
    if (existsSync(join(dir, name)) && read(dir, name).includes(other)) stray.push(name);
  }
  if (stray.length > 0) problems.push(`別の環境のホスト名（${other}）が含まれています: ${stray.join(", ")}`);
  return problems;
}

if (process.argv[1]?.endsWith("verify-site.mjs")) {
  const [dir, env] = process.argv.slice(2);
  if (!dir || !env) {
    console.error("使い方: node .github/scripts/verify-site.mjs <フォルダ> <production|develop>");
    process.exit(2);
  }
  const problems = verifySite(dir, env);
  if (problems.length > 0) {
    for (const p of problems) console.error(`::error::${p}`);
    console.error(`${dir} は ${env} 用の公開物として確認できませんでした。公開を止めます`);
    process.exit(1);
  }
  console.log(`${dir} は ${env} 用の公開物です（canonical ${ORIGINS[env]}/、${env === "production" ? "noindex なし・sitemap あり" : "noindex あり・sitemap なし"}）`);
}
