// Phase 3b S2 の確認用の静的サーバー(CI では動かさない)。  node gl-serve.mjs [port]
//   WEB=<src/FaraAvatarModdingGuide.Web/wwwroot>   現行の three.js 版(js/・lib/three/・css/)。既定はこのリポジトリの値
//   LAB=<dotnet publish したレンダーラボの wwwroot>  /lab/ に出す(既定 ./publish-lab/wwwroot)
//   HEADERS=<Build が書いた dist/_headers>            あれば、その `/*` の Content-Security-Policy を全部の応答に付ける(本番と同じ CSP で動くことの確認)
//   /ref.html … 現行の three.js 版のビューポートだけを出す参照ページ(画素の比較用)。/probe.html … 「新しいコンテキストで描画が始まらない」の切り分け用
// 配信するのは確認用のファイルだけ。.br / .gz はあれば使わず、Content-Type は拡張子で決める。
import { createServer } from "node:http";
import { readFileSync, existsSync, statSync } from "node:fs";
import { extname, join, normalize, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));
const WEB = resolve(process.env.WEB || join(here, "../../src/FaraAvatarModdingGuide.Web/wwwroot"));
const LAB = resolve(process.env.LAB || "publish-lab/wwwroot");
const PORT = Number(process.argv[2] || process.env.PORT || 8790);
const TYPES = {
  ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
  ".json": "application/json", ".wasm": "application/wasm", ".dll": "application/octet-stream", ".dat": "application/octet-stream", ".png": "image/png", ".svg": "image/svg+xml",
  ".map": "application/json", ".blat": "application/octet-stream",
};

/** _headers の最初の `/*` ブロックから、Content-Security-Policy の値を取り出す */
function cspFromHeaders(file) {
  if (!file) return null;
  const text = readFileSync(file, "utf8");
  const m = text.match(/^\s*Content-Security-Policy:\s*(.+)$/im);
  return m ? m[1].trim() : null;
}
const CSP = cspFromHeaders(process.env.HEADERS);

const files = {
  "/ref.html": join(here, "gl-ref.html"),
  "/ref.js": join(here, "gl-ref.js"),
  "/probe.html": join(here, "gl-probe.html"),
  "/probe.js": join(here, "gl-probe.js"),
};

function resolveUnder(root, rel) {
  const p = normalize(join(root, rel));
  if (p !== root && !p.startsWith(root + sep)) return null;
  return p;
}

createServer((req, res) => {
  const url = new URL(req.url, "http://localhost");
  let path = decodeURIComponent(url.pathname);
  let file = files[path] ?? null;
  if (!file && path.startsWith("/lab")) {
    let rel = path.slice("/lab".length) || "/";
    if (rel === "/") rel = "/index.html";
    file = resolveUnder(LAB, rel);
    if (file && !existsSync(file)) file = join(LAB, "index.html"); // クライアント側のルーティング用
  } else if (!file) {
    file = resolveUnder(WEB, path === "/" ? "/index.html" : path);
  }
  if (!file || !existsSync(file) || !statSync(file).isFile()) {
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("not found");
    return;
  }
  const headers = { "Content-Type": TYPES[extname(file)] || "application/octet-stream", "Cache-Control": "no-store" };
  if (CSP) headers["Content-Security-Policy"] = CSP;
  res.writeHead(200, headers);
  res.end(readFileSync(file));
}).listen(PORT, "127.0.0.1", () => console.log(`gl-serve: http://127.0.0.1:${PORT}  web=${WEB}  lab=${LAB}  csp=${CSP ? "on" : "off"}`));
