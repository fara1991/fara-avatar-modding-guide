// 試作の計測（Microsoft Edge を使う）。結果は JSON で標準出力に出す。
//   node measure.mjs size   --wasm <publish wwwroot> --ssr <dist>        … 配布サイズ（生・gzip・Brotli）
//   node measure.mjs timing --base http://127.0.0.1:8799 [--runs 5]      … 初回表示までの時間と転送量（普通／遅い回線）
//   node measure.mjs nav    --base http://127.0.0.1:8799                  … 再読み込みなしの移動
//   node measure.mjs seo    --base http://127.0.0.1:8799                  … 最初の HTML の中身（JS なし）
//   node measure.mjs csp    --wasm <publish wwwroot> --ssr <dist>        … CSP の違反（指令ごと）
// playwright-core は本体のリポジトリに入れない。PW_PATH に file:// の URL を渡す（screenshots.mjs と同じ）。
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const { chromium } = await import(process.env.PW_PATH ?? 'playwright-core');
const brotli = promisify(zlib.brotliCompress);
const gzip = promisify(zlib.gzip);

const [cmd = 'help', ...rest] = process.argv.slice(2);
const opt = (name, def) => {
  const i = rest.indexOf(`--${name}`);
  return i >= 0 ? rest[i + 1] : def;
};
const base = opt('base', 'http://127.0.0.1:8799');
const RUNS = Number(opt('runs', '5'));
const ARTICLE = '/learn/basics/transform';
// 移動の計測は、モジュールの script を持たない記事（nav.js が移動を有効にするページ）で行う
const NAV_ARTICLE = '/learn/getting-started/install';

// 「普通」は絞らない（ローカルの計測なので、実際の回線より速く出る）。「遅い」は指定どおり 150ms・1.6Mbps・CPU 4 倍。
const PROFILES = {
  normal: { label: '普通（絞らない・ローカル）', net: null, cpu: 1 },
  slow: { label: '遅い（150ms・1.6Mbps・CPU 4倍）', net: { latency: 150, downloadThroughput: 1_600_000 / 8, uploadThroughput: 750_000 / 8 }, cpu: 4 },
};

const median = (xs) => {
  const s = [...xs].sort((a, b) => a - b);
  return s.length % 2 ? s[(s.length - 1) / 2] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};

const launch = () => chromium.launch({ channel: 'msedge', headless: true, args: ['--disable-gpu'] });

// ---------------------------------------------------------------------------
async function sizeCmd() {
  const wasm = path.resolve(opt('wasm', ''));
  const ssr = path.resolve(opt('ssr', ''));
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((d) =>
    d.isDirectory() ? walk(path.join(dir, d.name)) : [path.join(dir, d.name)]);
  const files = walk(wasm).filter((f) => !/\.(br|gz)$/.test(f));
  const group = (f) => {
    const rel = path.relative(wasm, f).split(path.sep).join('/');
    if (rel.startsWith('_framework/')) return 'framework';
    if (rel.startsWith('_content/MudBlazor/')) return 'mudblazor';
    if (rel.startsWith('data/')) return 'data-json';
    return 'other';
  };
  const out = {};
  for (const f of files) {
    const g = group(f);
    const raw = fs.readFileSync(f);
    const br = fs.existsSync(f + '.br') ? fs.statSync(f + '.br').size : (await brotli(raw, { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11 } })).length;
    const gz = fs.existsSync(f + '.gz') ? fs.statSync(f + '.gz').size : (await gzip(raw, { level: 9 })).length;
    out[g] ??= { files: 0, raw: 0, gzip: 0, brotli: 0 };
    out[g].files++;
    out[g].raw += raw.length;
    out[g].gzip += gz;
    out[g].brotli += br;
  }
  // 現行の SSR（dist）の、記事 1 枚分の同一オリジンの資産（css・js）。外部 CDN の分は timing で実測する
  const ssrAssets = ['css/site.css', 'js/nav.js', 'js/sidebar.js', 'js/scrollprogress.js', 'js/bgscene.js', 'js/progress.js', 'js/searchcore.js', 'js/quicksearch.js'];
  const ssrOut = {};
  for (const a of ssrAssets) {
    const f = path.join(ssr, a);
    if (!fs.existsSync(f)) continue;
    const raw = fs.readFileSync(f);
    ssrOut[a] = { raw: raw.length, brotli: (await brotli(raw, { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11 } })).length };
  }
  console.log(JSON.stringify({ wasm: out, ssrSameOriginAssets: ssrOut }, null, 2));
}

// ---------------------------------------------------------------------------
async function loadOnce(browser, url, profile, ready) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: 'ja-JP' });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  await cdp.send('Network.enable');
  await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });
  if (profile.net) await cdp.send('Network.emulateNetworkConditions', { offline: false, ...profile.net });
  if (profile.cpu > 1) await cdp.send('Emulation.setCPUThrottlingRate', { rate: profile.cpu });

  const urls = new Map();
  const transfers = [];
  cdp.on('Network.responseReceived', (e) => urls.set(e.requestId, e.response.url));
  cdp.on('Network.loadingFinished', (e) => transfers.push({ url: urls.get(e.requestId) ?? '?', bytes: e.encodedDataLength }));

  await page.goto(url, { waitUntil: 'commit', timeout: 300_000 });
  const handle = await page.waitForFunction(ready, null, { polling: 'raf', timeout: 300_000 });
  const ms = Math.round(await handle.jsonValue());
  // 本文が出た後も、CSS・フォント・画像・外部 CDN の読み込みは続く。load イベント（全部読み終わった時点）も測る
  await page.waitForLoadState('load', { timeout: 300_000 }).catch(() => { });
  const loadMs = Math.round(await page.evaluate(() => performance.getEntriesByType('navigation')[0].loadEventEnd));
  // 画像・フォント・後から読むものも含めて、ネットワークが静かになるまで待ってから転送量を数える
  await page.waitForLoadState('networkidle', { timeout: 300_000 }).catch(() => { });
  await page.waitForTimeout(500);
  await context.close();

  const total = transfers.reduce((s, t) => s + t.bytes, 0);
  const local = (u) => u.startsWith(base);
  const isWasmAsset = (u) => /\/wasm\/(_framework|_content)\//.test(u);
  const isWasmData = (u) => /\/wasm\/data\//.test(u);
  const sum = (f) => transfers.filter(f).reduce((s, t) => s + t.bytes, 0);
  return {
    ms,
    loadMs,
    totalBytes: total,
    sameOriginBytes: sum((t) => local(t.url)),
    externalBytes: sum((t) => !local(t.url) && t.url.startsWith('http')),
    wasmFrameworkBytes: sum((t) => isWasmAsset(t.url)),
    wasmDataBytes: sum((t) => isWasmData(t.url)),
    mudblazorBytes: sum((t) => /\/_content\/MudBlazor\//.test(t.url)),
    requests: transfers.length,
    externalHosts: [...new Set(transfers.filter((t) => t.url.startsWith('http') && !local(t.url)).map((t) => new URL(t.url).host))],
  };
}

const articleReady = () => {
  const el = document.querySelector('.article-body');
  return el && el.textContent.trim().length > 100 ? Math.round(performance.now()) : null;
};

async function timingCmd() {
  const browser = await launch();
  const result = {};
  const targets = {
    ssr: `${base}${ARTICLE}`,
    wasm: `${base}/wasm${ARTICLE}`,
  };
  try {
    for (const [profileName, profile] of Object.entries(PROFILES)) {
      for (const [app, url] of Object.entries(targets)) {
        const runs = [];
        for (let i = 0; i < RUNS; i++) {
          runs.push(await loadOnce(browser, url, profile, articleReady));
          process.stderr.write(`${profileName} ${app} run ${i + 1}: ${runs.at(-1).ms} ms\n`);
        }
        result[`${profileName}/${app}`] = {
          medianMs: median(runs.map((r) => r.ms)),
          runsMs: runs.map((r) => r.ms),
          medianLoadEventMs: median(runs.map((r) => r.loadMs)),
          runsLoadEventMs: runs.map((r) => r.loadMs),
          medianTotalBytes: median(runs.map((r) => r.totalBytes)),
          medianSameOriginBytes: median(runs.map((r) => r.sameOriginBytes)),
          medianExternalBytes: median(runs.map((r) => r.externalBytes)),
          medianWasmFrameworkBytes: median(runs.map((r) => r.wasmFrameworkBytes)),
          medianWasmDataBytes: median(runs.map((r) => r.wasmDataBytes)),
          medianMudBlazorBytes: median(runs.map((r) => r.mudblazorBytes)),
          requests: runs[0].requests,
          externalHosts: runs[0].externalHosts,
        };
      }
    }
  } finally {
    await browser.close();
  }
  console.log(JSON.stringify(result, null, 2));
}

// ---------------------------------------------------------------------------
async function navCmd() {
  const browser = await launch();
  const result = {};
  const next = async (page, sel) => {
    const before = await page.textContent('.article-body');
    const t0 = Date.now();
    await page.locator(sel).last().click();
    await page.waitForFunction((b) => {
      const el = document.querySelector('.article-body');
      return el && el.textContent !== b && el.textContent.trim().length > 100;
    }, before, { polling: 'raf', timeout: 120_000 });
    return Date.now() - t0;
  };
  try {
    for (const run of ['1', '2', '3']) {
      // WebAssembly 版: 記事の「次へ」（Blazor のルーティング）→ 検索（同じ WebAssembly の中）→ 練習（別のページへ）
      const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: 'ja-JP' });
      const page = await ctx.newPage();
      await page.goto(`${base}/wasm${NAV_ARTICLE}`, { waitUntil: 'load' });
      await page.waitForFunction(articleReady, null, { polling: 'raf', timeout: 300_000 });
      await page.evaluate(() => { window.__marker = 'kept'; });
      const wasmNextMs = await next(page, 'a:has-text("→")');
      const wasmKept = await page.evaluate(() => window.__marker ?? 'lost');
      await page.locator('a', { hasText: '検索' }).first().click();
      await page.waitForURL(/\/wasm\/search/);
      const wasmSearchKept = await page.evaluate(() => window.__marker ?? 'lost');
      // 練習: WebAssembly の中の入口ページ（ドロワーから）→ 本体の静的ページ（入口ページのリンク）。後者は別のページ（読み直し）になるはず
      await page.locator('a', { hasText: '練習（3D）の入口' }).first().click();
      await page.waitForURL(/\/wasm\/practice/);
      const wasmPracticeInAppKept = await page.evaluate(() => window.__marker ?? 'lost');
      await page.locator('a', { hasText: '練習ページを開く' }).first().click();
      await page.waitForURL((u) => u.pathname === '/practice', { timeout: 60000 });
      await page.waitForTimeout(1500);
      const wasmPracticeStaticKept = await page.evaluate(() => window.__marker ?? 'lost').catch(() => 'lost (page unloaded)');
      await ctx.close();

      // 現行の SSR 版: nav.js による「次へ」
      const ctx2 = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: 'ja-JP' });
      const page2 = await ctx2.newPage();
      await page2.goto(`${base}${NAV_ARTICLE}`, { waitUntil: 'load' });
      await page2.waitForSelector('.article-body');
      // nav.js は defer で読まれる。読み終わる前にクリックすると通常の移動になるので、初期化の完了を待つ
      await page2.waitForFunction(() => !!window.faraNav, null, { timeout: 60000 });
      await page2.evaluate(() => { window.__marker = 'kept'; });
      const ssrHasModuleScript = await page2.evaluate(() => !!document.querySelector('script[type="module"]'));
      const ssrNextMs = await next(page2, '.article-nav a');
      const ssrKept = await page2.evaluate(() => window.__marker ?? 'lost');
      await ctx2.close();

      result[`run${run}`] = { wasmNextMs, wasmKept, wasmSearchKept, wasmPracticeInAppKept, wasmPracticeStaticKept, ssrNextMs, ssrKept, ssrHasModuleScript };
      process.stderr.write(`run ${run} done\n`);
    }
  } finally {
    await browser.close();
  }
  console.log(JSON.stringify(result, null, 2));
}

// ---------------------------------------------------------------------------
async function seoCmd() {
  const pick = async (url) => {
    const html = await (await fetch(url)).text();
    const text = html.replace(/<script[\s\S]*?<\/script>|<style[\s\S]*?<\/style>/g, '').replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
    return {
      htmlBytes: Buffer.byteLength(html),
      title: (html.match(/<title>([^<]*)<\/title>/) ?? [])[1],
      hasH1: /<h1[\s>]/.test(html),
      visibleTextChars: text.length,
      visibleTextSample: text.slice(0, 120),
      hasArticleBody: html.includes('article-body'),
    };
  };
  const result = {
    ssrArticle: await pick(`${base}${ARTICLE}`),
    wasmArticle: await pick(`${base}/wasm${ARTICLE}`),
    wasmHome: await pick(`${base}/wasm/`),
    ssrHome: await pick(`${base}/`),
  };
  console.log(JSON.stringify(result, null, 2));
}

// ---------------------------------------------------------------------------
const CSP_VARIANTS = {
  // 1. WebAssembly に必要な wasm-unsafe-eval を入れず、スタイルは 'self' だけ
  'A-no-wasm-eval': "default-src 'self'; script-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'",
  // 2. wasm-unsafe-eval を足す（スクリプトは 'self' のまま）
  'B-wasm-eval': "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'",
  // 3. 2 に、インラインのスタイル（style 属性・<style>）を足す
  'C-wasm-eval-inline-style': "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'",
};

async function startServer(port, cspValue) {
  const serve = fileURLToPath(new URL('./serve.mjs', import.meta.url));
  const child = spawn(process.execPath, [serve, '--port', String(port), '--ssr', opt('ssr', ''), '--wasm', opt('wasm', ''), '--csp', cspValue], { stdio: 'ignore' });
  await new Promise((r) => setTimeout(r, 1500));
  return child;
}

async function cspCmd() {
  const result = {};
  let port = 8801;
  for (const [name, value] of Object.entries(CSP_VARIANTS)) {
    const child = await startServer(port, value);
    const browser = await launch();
    try {
      for (const app of ['ssr', 'wasm']) {
        const url = `http://127.0.0.1:${port}${app === 'wasm' ? '/wasm' : ''}${ARTICLE}`;
        const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: 'ja-JP' });
        const page = await context.newPage();
        const violations = [];
        page.on('console', (m) => { if (/Content Security Policy|Refused to|violates/i.test(m.text())) violations.push(m.text().slice(0, 240)); });
        await page.goto(url, { waitUntil: 'load', timeout: 120_000 }).catch(() => { });
        let rendered = true;
        await page.waitForFunction(articleReady, null, { polling: 'raf', timeout: 60_000 }).catch(() => { rendered = false; });
        await page.waitForTimeout(1000);
        const directives = [...new Set(violations.map((v) => (v.match(/(script-src-elem|script-src|style-src-attr|style-src-elem|style-src|wasm-unsafe-eval|connect-src|font-src|img-src)/) ?? ['?'])[0]))];
        result[`${name}/${app}`] = { rendered, violationCount: violations.length, directives, sample: violations.slice(0, 3) };
        await context.close();
      }
    } finally {
      await browser.close();
      child.kill();
    }
    port++;
  }
  console.log(JSON.stringify(result, null, 2));
}

// ---------------------------------------------------------------------------
switch (cmd) {
  case 'size': await sizeCmd(); break;
  case 'timing': await timingCmd(); break;
  case 'nav': await navCmd(); break;
  case 'seo': await seoCmd(); break;
  case 'csp': await cspCmd(); break;
  default:
    console.error('usage: node measure.mjs size|timing|nav|seo|csp [options]');
    process.exit(2);
}
