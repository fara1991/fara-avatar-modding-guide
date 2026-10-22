// 現行の静的 SSR 版と WebAssembly 版を、PC・スマホ、ライト・ダークで撮る（Microsoft Edge を使う）。
// 使い方: node screenshots.mjs --base http://127.0.0.1:8799 --out <フォルダ>
// PW_PATH=file:///<scratchpad>/node_modules/playwright-core/index.mjs node screenshots.mjs ...
// 前提: serve.mjs が起動していること（/ が SSR 版、/wasm/ が WebAssembly 版）。
// playwright-core は本体のリポジトリに入れない。PW_PATH に、そのモジュールの file:// URL を渡す。
const { chromium } = await import(process.env.PW_PATH ?? 'playwright-core');
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : def;
};
const base = opt('base', 'http://127.0.0.1:8799');
const out = path.resolve(opt('out', 'docs'));
fs.mkdirSync(out, { recursive: true });

const query = encodeURIComponent('アバター');
const apps = {
  ssr: {
    prefix: '',
    pages: {
      home: { url: '/', ready: 'h1' },
      article: { url: '/learn/basics/transform', ready: '.article-body' },
      search: { url: `/search?q=${query}`, ready: 'main' },
    },
  },
  wasm: {
    prefix: '/wasm',
    pages: {
      home: { url: '/', ready: 'text=最初から読む' },
      article: { url: '/learn/basics/transform', ready: '.article-body' },
      search: { url: `/search?q=${query}`, ready: 'text=件（最大' },
    },
  },
};
const devices = {
  pc: { viewport: { width: 1280, height: 800 } },
  sp: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 },
};
const themes = ['light', 'dark'];

// GPU を使うと、背景の 3D（bgscene.js）で描画プロセスが落ちることがあったので、GPU は使わない
for (const [appName, app] of Object.entries(apps)) {
  for (const [pageName, p] of Object.entries(app.pages)) {
    for (const [devName, dev] of Object.entries(devices)) {
      for (const theme of themes) {
        // 1 枚ごとに Edge を起動し直す（同じブラウザで続けて撮ると、描画プロセスが落ちることがあったため）
        const browser = await chromium.launch({ channel: 'msedge', headless: true, args: ['--disable-gpu'] });
        const context = await browser.newContext({ ...dev, locale: 'ja-JP' });
        await context.addInitScript((t) => {
          try { localStorage.setItem('avatar-modding-guide.theme', t); } catch (e) { }
        }, theme);
        const page = await context.newPage();
        await page.goto(base + app.prefix + p.url, { waitUntil: 'load' });
        await page.waitForSelector(p.ready, { timeout: 120000 });
        await page.waitForTimeout(800); // フェードインなどが終わるまで
        const file = path.join(out, `${appName}-${pageName}-${devName}-${theme}.png`);
        await page.screenshot({ path: file });
        console.log('saved', file);
        await context.close();
        await browser.close();
      }
    }
  }
}
