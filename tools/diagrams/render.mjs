// 記事の図（HTML）を PNG にする。使い方: node tools/diagrams/render.mjs
// 図の HTML は、このフォルダーにある。出力先は、各記事のカテゴリの images/（下の JOBS）。
// 幅 480px の 1 列にしてあるのは、スマホ幅（約 340px に縮む）でも文字が読めるようにするため。
import { chromium } from 'playwright-core';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const JOBS = [
  ['apl-flow.html', 'Content/learn/vrchat-features/images/avatar-pose-library-flow.png'],
  ['apl-structure.html', 'Content/learn/vrchat-features/images/avatar-pose-library-structure.png'],
  ['aao-two-ways.html', 'Content/learn/optimize-aao/images/avatar-optimizer-two-ways.png'],
  ['aao-where.html', 'Content/learn/optimize-aao/images/avatar-optimizer-where-to-attach.png'],
];

const browser = await chromium.launch({ channel: 'msedge' });
const page = await (await browser.newContext({ viewport: { width: 600, height: 800 }, deviceScaleFactor: 2 })).newPage();
for (const [html, out] of JOBS) {
  await page.goto(pathToFileURL(path.join(here, html)).href);
  await page.waitForTimeout(300);
  await (await page.$('.card')).screenshot({ path: path.join(root, out) });
  console.log(html, '->', out);
}
await browser.close();
