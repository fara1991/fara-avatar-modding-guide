// 使い方: node measure.mjs <port> <url> <coldRuns> <outJson> [sampleMs] [warmRuns]
// 冷たい起動: 毎回、新しいブラウザコンテキスト（HTTP キャッシュ・Cache Storage が空）で読む。
// 温かい起動: 直前の冷たい起動と同じコンテキストで、もう一度読む（2 回目以降）。
// 計測用のタブだけを作り、終わったら閉じる（ほかのタブには触らない）。
import fs from 'node:fs';
import zlib from 'node:zlib';
const [, , port, url, coldStr, outPath, sampleStr, warmStr] = process.argv;
const coldRuns = Number(coldStr || 3);
const warmRuns = Number(warmStr || 0);
const sampleMs = Number(sampleStr || 5000);
const injectSrc = fs.readFileSync(new URL('./inject.js', import.meta.url), 'utf8');
const sleep = ms => new Promise(r => setTimeout(r, ms));

const version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
const browser = new WebSocket(version.webSocketDebuggerUrl);
await new Promise((res, rej) => { browser.onopen = res; browser.onerror = rej; });
let bid = 0;
const bpend = new Map();
browser.onmessage = ev => { const m = JSON.parse(ev.data); if (m.id && bpend.has(m.id)) { bpend.get(m.id)(m); bpend.delete(m.id); } };
const bsend = (method, params = {}) => new Promise(res => { const i = ++bid; bpend.set(i, res); browser.send(JSON.stringify({ id: i, method, params })); });

async function openPage() {
  const ctx = (await bsend('Target.createBrowserContext')).result.browserContextId;
  const tgt = (await bsend('Target.createTarget', { url: 'about:blank', browserContextId: ctx })).result.targetId;
  let info;
  for (let i = 0; i < 100 && !info; i++) {
    await sleep(100);
    const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    info = list.find(t => t.id === tgt);
  }
  return { ctx, tgt, wsUrl: info.webSocketDebuggerUrl };
}
async function closePage(p) {
  await bsend('Target.closeTarget', { targetId: p.tgt });
  await bsend('Target.disposeBrowserContext', { browserContextId: p.ctx });
}

// 1 つのページに接続する
async function connect(wsUrl) {
  const ws = new WebSocket(wsUrl);
  await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  let id = 0;
  const pending = new Map();
  const waiters = [];
  const reqs = new Map();
  ws.onmessage = ev => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); return; }
    if (msg.method === 'Runtime.exceptionThrown') console.error('EXC', JSON.stringify(msg.params.exceptionDetails.exception?.description ?? msg.params.exceptionDetails.text).slice(0, 300));
    if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') console.error('CONSOLE_ERR', msg.params.args.map(a => a.value ?? a.description).join(' ').slice(0, 300));
    if (msg.method === 'Network.requestWillBeSent') reqs.set(msg.params.requestId, { url: msg.params.request.url, type: msg.params.type, status: 0, enc: 0 });
    if (msg.method === 'Network.responseReceived') { const r = reqs.get(msg.params.requestId); if (r) { r.status = msg.params.response.status; r.mime = msg.params.response.mimeType; } }
    if (msg.method === 'Network.loadingFinished') { const r = reqs.get(msg.params.requestId); if (r) r.enc = msg.params.encodedDataLength; }
    for (const w of waiters) w(msg);
  };
  const send = (method, params = {}, timeout = 60000) => new Promise((res, rej) => {
    const i = ++id;
    const t = setTimeout(() => rej(new Error('CDP timeout: ' + method)), timeout);
    pending.set(i, m => { clearTimeout(t); res(m); });
    ws.send(JSON.stringify({ id: i, method, params }));
  });
  const waitEvent = (method, timeout = 60000) => new Promise((res, rej) => {
    const t = setTimeout(() => rej(new Error('timeout ' + method)), timeout);
    const f = m => { if (m.method === method) { clearTimeout(t); waiters.splice(waiters.indexOf(f), 1); res(m); } };
    waiters.push(f);
  });
  const evalJs = async expr => (await send('Runtime.evaluate', { expression: expr, returnByValue: true }, 20000)).result?.result?.value;
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Network.enable');
  await send('Network.setCacheDisabled', { cacheDisabled: true });
  await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false });
  await send('Page.addScriptToEvaluateOnNewDocument', { source: injectSrc });
  return { ws, send, waitEvent, evalJs, reqs, close: () => ws.close() };
}

const out = { url, runs: [], requests: [] };
let page = await openPage();
let conn = await connect(page.wsUrl);
const total = coldRuns + warmRuns;
for (let run = 1; run <= total; run++) {
  const cold = run <= coldRuns;
  if (cold && run > 1) {
    // 冷たい起動: 前のタブとコンテキストを閉じて、新しいコンテキストで開く
    conn.close();
    await closePage(page);
    page = await openPage();
    conn = await connect(page.wsUrl);
  }
  conn.reqs.clear();
  const loaded = conn.waitEvent('Page.loadEventFired');
  await conn.send('Page.navigate', { url });
  await loaded;
  console.error('step: loaded run', run, JSON.stringify(await conn.evalJs("({m: typeof window.__m, hud: (document.querySelector('.cs-hud')||{}).textContent, url: location.href, t: document.title})")));
  await sleep(Number(process.env.WAITMS || 8000)); // アプリの起動（Blazor / Three.js の読み込みと初期化）を待つ
  const firstDraw = await conn.evalJs('window.__m && window.__m.firstDraw');
  const loadEnd = await conn.evalJs("(() => { const n = performance.getEntriesByType('navigation')[0]; return n ? n.loadEventEnd : null; })()");
  const a = await conn.evalJs('({ f: window.__m.drawFrames, c: window.__m.cbMs, cs: window.__csStats ?? null })');
  await sleep(sampleMs);
  const b = await conn.evalJs('({ f: window.__m.drawFrames, c: window.__m.cbMs, cs: window.__csStats ?? null })');
  const frames = b.f - a.f;
  const secs = sampleMs / 1000;
  const csN = b.cs && a.cs ? b.cs.n - a.cs.n : 0;
  const csDraw = csN > 0 ? +((b.cs.ms - a.cs.ms) / csN).toFixed(3) : null;
  const rec = { run, cold, firstDrawMs: firstDraw, loadEventEndMs: loadEnd, fps: +(frames / secs).toFixed(1), cpuMsPerFrame: frames ? +((b.c - a.c) / frames).toFixed(3) : null, csDrawMsPerFrame: csDraw, csFrames: csN };
  out.runs.push(rec);
  console.log(JSON.stringify(rec));
  if (run === 1) {
    // 配信された中身（デコード後）の大きさを、raw / gzip / brotli で測る（最初の 1 回だけ）
    for (const [rid, r] of conn.reqs) {
      if (r.status !== 200) continue;
      try {
        const body = await conn.send('Network.getResponseBody', { requestId: rid });
        const buf = body.result.base64Encoded ? Buffer.from(body.result.body, 'base64') : Buffer.from(body.result.body, 'utf8');
        const gz = zlib.gzipSync(buf, { level: 9 }).length;
        const br = zlib.brotliCompressSync(buf, { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11, [zlib.constants.BROTLI_PARAM_SIZE_HINT]: buf.length } }).length;
        out.requests.push({ url: r.url, type: r.type, mime: r.mime, raw: buf.length, gz, br, wire: r.enc });
      } catch (e) {
        out.requests.push({ url: r.url, error: String(e) });
      }
    }
  }
}
fs.writeFileSync(outPath, JSON.stringify(out, null, 1));
console.log('saved', outPath, 'requests', out.requests.length);
conn.close();
await closePage(page).catch(() => {});
browser.close();
process.exit(0);
