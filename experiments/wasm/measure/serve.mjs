// 試作の計測用サーバー。外部依存なし（Node の標準モジュールだけ）。
//   /        → 現行の静的 SSR 版（dist/。--ssr で指定）
//   /wasm/   → WebAssembly 版（dotnet publish の wwwroot。--wasm で指定）
// 圧縮: 本番（Cloudflare）と同じく br / gzip を返す。.br / .gz が既にあればそれを使い、無ければ初回に圧縮して記憶する。
// --csp "<値>" を付けると、HTML に Content-Security-Policy を付ける（CSP の確認用）。
//
// 使い方: node serve.mjs --port 8799 --ssr <dist> --wasm <publish wwwroot> [--csp "<値>"]
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { promisify } from 'node:util';

const brotli = promisify(zlib.brotliCompress);
const gzip = promisify(zlib.gzip);

const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : def;
};
const port = Number(opt('port', '8799'));
const ssrRoot = path.resolve(opt('ssr', ''));
const wasmRoot = path.resolve(opt('wasm', ''));
const csp = opt('csp', '');

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8', '.wasm': 'application/wasm',
  '.dll': 'application/octet-stream', '.dat': 'application/octet-stream', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml', '.webp': 'image/webp', '.woff2': 'font/woff2', '.woff': 'font/woff', '.ttf': 'font/ttf',
  '.txt': 'text/plain; charset=utf-8', '.xml': 'application/xml', '.mp4': 'video/mp4', '.webm': 'video/webm',
  '.gif': 'image/gif', '.ico': 'image/x-icon',
};
const COMPRESSIBLE = /^(text\/|application\/(json|javascript|wasm|xml|octet-stream))|svg/;

const cache = new Map(); // 圧縮の結果（パス+エンコーディング → Buffer）

function resolveFile(root, rel) {
  const clean = path.normalize(path.join(root, rel));
  if (!clean.startsWith(root)) return null;
  const candidates = [clean, `${clean}.html`, path.join(clean, 'index.html')];
  for (const c of candidates) {
    if (fs.existsSync(c) && fs.statSync(c).isFile()) return c;
  }
  return null;
}

async function encoded(file, enc) {
  const sidecar = file + (enc === 'br' ? '.br' : '.gz');
  if (fs.existsSync(sidecar)) return fs.readFileSync(sidecar);
  const key = `${file}|${enc}`;
  if (!cache.has(key)) {
    const raw = fs.readFileSync(file);
    cache.set(key, enc === 'br'
      ? await brotli(raw, { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11 } })
      : await gzip(raw, { level: 9 }));
  }
  return cache.get(key);
}

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    let pathname = decodeURIComponent(url.pathname);
    let root;
    let rel;
    if (pathname === '/wasm' || pathname.startsWith('/wasm/')) {
      root = wasmRoot;
      rel = pathname.slice('/wasm'.length).replace(/^\/+/, '');
      if (rel === '' || !resolveFile(root, rel)) {
        // WebAssembly 版は、どの URL でも index.html（クライアント側のルーティング）
        if (!/\.[a-z0-9]+$/i.test(rel) || rel === '') rel = 'index.html';
      }
    } else {
      root = ssrRoot;
      rel = pathname.replace(/^\/+/, '');
      if (rel === '') rel = 'index.html';
    }
    const file = resolveFile(root, rel);
    if (!file) {
      const notFound = resolveFile(ssrRoot, '404.html');
      res.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(notFound ? fs.readFileSync(notFound) : 'not found');
      return;
    }
    const ext = path.extname(file).toLowerCase();
    const type = TYPES[ext] ?? 'application/octet-stream';
    const headers = { 'Content-Type': type, 'Cache-Control': 'no-cache', Vary: 'Accept-Encoding' };
    if (ext === '.html' && csp) headers['Content-Security-Policy'] = csp;
    const accept = String(req.headers['accept-encoding'] ?? '');
    const compressible = COMPRESSIBLE.test(type);
    let body;
    if (compressible && /\bbr\b/.test(accept)) {
      body = await encoded(file, 'br');
      headers['Content-Encoding'] = 'br';
    } else if (compressible && /\bgzip\b/.test(accept)) {
      body = await encoded(file, 'gz');
      headers['Content-Encoding'] = 'gzip';
    } else {
      body = fs.readFileSync(file);
    }
    headers['Content-Length'] = body.length;
    res.writeHead(200, headers);
    res.end(req.method === 'HEAD' ? undefined : body);
  } catch (e) {
    res.writeHead(500);
    res.end(String(e));
  }
});

server.listen(port, '127.0.0.1', () => {
  console.log(`serving ssr=${ssrRoot} wasm=${wasmRoot} on http://127.0.0.1:${port}`);
});
