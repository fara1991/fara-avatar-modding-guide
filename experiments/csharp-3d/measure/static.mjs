// 計測用の静的サーバー: node static.mjs <rootDir> <port>。拡張子のないパスは index.html（SPA の /practice-cs）へ。圧縮はしない
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
const [, , root, portStr] = process.argv;
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png' };
http.createServer((req, res) => {
  const p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let file = path.join(root, p);
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root, 'index.html');
  res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-cache' });
  fs.createReadStream(file).pipe(res);
}).listen(Number(portStr), '127.0.0.1', () => console.log('listening', portStr));
