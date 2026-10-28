#!/usr/bin/env node
// レイアウトの自動計測（npm run layout-check）。公開用の静的出力（site-production）を配り、kit の CLI で計測する。
// 先に publish と Build で site-production を作る（README の「レイアウトの計測」）。終わったら、このプロセスが起動したサーバーだけを止める。
//
// 配信は kit の serve-static（createStaticServer の応答）を使う。ただし、kit の応答は「フォルダがあればその index.html」だけを見る。
// Build は /learn に learn.html（一覧）と learn/（記事の置き場）の両方を作るので、kit のままだと /learn が 404 になる。
// Cloudflare（html_handling の既定）は learn.html を返すので、ここでは「index.html の無いフォルダ」の要求だけ <パス>.html に書き換える。
// （kit の serve-static を直す案は、kit の docs/layout-check.md・次のタグで検討する）
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { existsSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createStaticServer } from "../../external/fara-site-kit/tools/layout-check/serve-static.mjs";

const root = fileURLToPath(new URL("../../", import.meta.url));
const siteDir = resolve(root, "site-production");
const configPath = resolve(root, "layout-check.config.mjs");
const cliPath = resolve(root, "external/fara-site-kit/tools/layout-check/cli.mjs");
const START_PORT = 8870;
const TRIES = 10;

/** 「フォルダ（index.html なし）」を、同じ名前の .html に読み替える。kit の応答には触れず、req.url だけを書き換える。 */
function mapFolderToHtml(req) {
  const url = new URL(req.url ?? "/", "http://localhost");
  const rel = url.pathname.replace(/^\/+|\/+$/g, "");
  if (!rel) return;
  const dir = join(siteDir, decodeURIComponent(rel));
  const isFolder = statSync(dir, { throwIfNoEntry: false })?.isDirectory() === true;
  if (isFolder && !existsSync(join(dir, "index.html")) && existsSync(`${dir}.html`)) {
    req.url = `/${rel}.html${url.search}`;
  }
}

/** kit の配信の応答を、同じポート探しの手順で配る。止めるのはこの関数が返した close だけ。 */
async function serveSite(startPort) {
  const kitHandler = createStaticServer(siteDir).listeners("request")[0];
  for (let port = startPort; port < startPort + TRIES; port += 1) {
    const server = createServer((req, res) => {
      mapFolderToHtml(req);
      kitHandler(req, res);
    });
    const listened = await new Promise((resolveListen) => {
      server.once("error", () => resolveListen(false));
      server.listen(port, "127.0.0.1", () => resolveListen(true));
    });
    if (listened) {
      return {
        origin: `http://127.0.0.1:${port}`,
        close: () => new Promise((resolveClose) => server.close(() => resolveClose())),
      };
    }
  }
  throw new Error(`${startPort} から ${TRIES} 個のポートがすべて使われている`);
}

if (!existsSync(join(siteDir, "index.html"))) {
  console.error(`エラー: ${siteDir} が無い。先に publish と Build で作る（README の「レイアウトの計測」）`);
  process.exitCode = 2;
} else {
  const server = await serveSite(START_PORT);
  console.log(`配信中: ${server.origin}（元: ${siteDir}）`);
  try {
    const code = await new Promise((resolveRun, rejectRun) => {
      const child = spawn(process.execPath, [cliPath, "--config", configPath], {
        cwd: root,
        stdio: "inherit",
        env: { ...process.env, LAYOUT_CHECK_BASE_URL: server.origin },
      });
      child.once("error", rejectRun);
      child.once("exit", (exitCode) => resolveRun(exitCode ?? 2));
    });
    process.exitCode = code;
  } finally {
    await server.close();
  }
}
