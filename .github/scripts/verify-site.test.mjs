// 公開物の取り違えを止める関門（verify-site.mjs）のテスト。  node --test .github/scripts/verify-site.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { verifySite, ORIGINS } from "./verify-site.mjs";

const CSP = "  Content-Security-Policy: default-src 'self'\n";

/** 環境ごとの、Build の出力に近い最小の公開物を作る */
function site(env, { noindex = env === "develop", sitemap = env === "production", canonical = ORIGINS[env] + "/", extra = {} } = {}) {
  const dir = mkdtempSync(join(tmpdir(), "verify-site-"));
  const put = (name, text) => { mkdirSync(dirname(join(dir, name)), { recursive: true }); writeFileSync(join(dir, name), text); };
  put("index.html", `<html><head><link rel="canonical" href="${canonical}" /></head></html>`);
  put("404.html", "<html></html>");
  put("learn/basics/transform.html", "<html></html>");
  put("_redirects", "/operations /learn 301\n");
  put("_headers", `/*\n${CSP}${noindex ? "  X-Robots-Tag: noindex\n" : ""}`);
  put("robots.txt", `User-agent: *\n${env === "production" ? `Disallow: /api/\n\nSitemap: ${ORIGINS.production}/sitemap.xml\n` : "Allow: /\n"}`);
  put("data/content.json", "{}");
  put("data/search.json", "[]");
  put("data/site.json", "{}");
  put("_framework/blazor.webassembly.js", "");
  if (sitemap) put("sitemap.xml", "<urlset/>");
  for (const [name, text] of Object.entries(extra)) put(name, text);
  return dir;
}
const check = (env, claimed, options) => {
  const dir = site(env, options);
  try { return verifySite(dir, claimed); } finally { rmSync(dir, { recursive: true, force: true }); }
};

test("本番用の公開物は本番として通り、確認用として止まる", () => {
  assert.deepEqual(check("production", "production"), []);
  const asDevelop = check("production", "develop");
  assert.ok(asDevelop.some((p) => p.includes("X-Robots-Tag: noindex がありません")));
  assert.ok(asDevelop.some((p) => p.includes("sitemap.xml があります")));
  assert.ok(asDevelop.some((p) => p.includes("canonical")));
});

test("確認用の公開物は確認用として通り、本番として止まる", () => {
  assert.deepEqual(check("develop", "develop"), []);
  const asProduction = check("develop", "production");
  assert.ok(asProduction.some((p) => p.includes("X-Robots-Tag がありません") || p.includes("X-Robots-Tag があります")));
  assert.ok(asProduction.some((p) => p.includes("sitemap.xml がありません")));
  assert.ok(asProduction.some((p) => p.includes("canonical")));
});

test("本番に noindex が混ざる・canonical が違う・別の環境のホスト名が混ざると止まる", () => {
  assert.ok(check("production", "production", { noindex: true }).some((p) => p.includes("X-Robots-Tag")));
  assert.ok(check("production", "production", { canonical: "https://avatar-dev.fara-labs.com/" }).some((p) => p.includes("canonical")));
  assert.ok(check("production", "production", { extra: { "404.html": "<a href='https://avatar-dev.fara-labs.com/'>" } }).some((p) => p.includes("別の環境のホスト名")));
  assert.ok(check("production", "production", { extra: { "robots.txt": "User-agent: *\nDisallow: /\n" } }).length > 0);
});

test("必要なファイルが欠けている・CSP がないと止まる", () => {
  const dir = site("production");
  try {
    rmSync(join(dir, "_framework"), { recursive: true });
    assert.ok(verifySite(dir, "production").some((p) => p.includes("_framework")));
  } finally { rmSync(dir, { recursive: true, force: true }); }
  assert.ok(check("production", "production", { extra: { "_headers": "/*\n  X-Frame-Options: DENY\n" } }).some((p) => p.includes("Content-Security-Policy")));
});

test("環境の指定が不正なら止まる", () => {
  const dir = site("production");
  try {
    assert.ok(verifySite(dir, "staging").length > 0);
    assert.ok(verifySite(dir, "").length > 0);
    assert.ok(verifySite(dir, undefined).length > 0);
  } finally { rmSync(dir, { recursive: true, force: true }); }
});
