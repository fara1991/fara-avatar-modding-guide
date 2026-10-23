// 「新しいブラウザコンテキストで描画が始まらない」の切り分け(docs/phase3b-design.md §10.1)。
//   node gl-context-matrix.mjs   (先に  node gl-serve.mjs 8790  を別に動かす)
//   ORIGIN=http://127.0.0.1:8790  OUT=<結果の JSON>  CELLS=<絞り込み: 名前の一部>  USERDATA=<作業用のプロファイルの置き場所。既定は OS の一時フォルダ>
// ブラウザを CDP(--remote-debugging-port)で直接操作する(Playwright を通さない。Playwright はページを「常に表示・フォーカスあり」にするため、非表示の問題を隠す)。
// 各セルで同じ値を 3 秒間記録する: visibilityState・hasFocus・rAF の回数・getContext の戻り・context の喪失・canvas の大きさ。
// ブラウザは毎回、新しい --user-data-dir で起動する(使用中のブラウザに合流して --remote-debugging-port が無視されるのを避ける)。終わったら PID で止める。
import { spawn, execFileSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const ORIGIN = process.env.ORIGIN || "http://127.0.0.1:8790";
const SW = ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"];
const BROWSERS = {
  chromium: "C:/Users/Fara/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe",
  edge: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  chrome: "C:/Program Files/Google/Chrome/Application/chrome.exe",
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

class Cdp {
  constructor(url) { this.url = url; this.id = 0; this.pending = new Map(); this.handlers = []; }
  async open() {
    this.ws = new WebSocket(this.url);
    await new Promise((res, rej) => { this.ws.onopen = res; this.ws.onerror = rej; });
    this.ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && this.pending.has(m.id)) { this.pending.get(m.id)(m); this.pending.delete(m.id); } else this.handlers.forEach((h) => h(m));
    };
  }
  send(method, params = {}, sessionId) {
    return new Promise((res, rej) => {
      const id = ++this.id;
      const t = setTimeout(() => rej(new Error("CDP timeout " + method)), 30000);
      this.pending.set(id, (m) => { clearTimeout(t); m.error ? rej(new Error(method + ": " + m.error.message)) : res(m.result); });
      this.ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
  }
  close() { try { this.ws.close(); } catch {} }
}

async function launch(name, { headless, extra = [], sw = true }) {
  const dir = mkdtempSync(join(process.env.USERDATA || tmpdir(), "glm-"));
  const port = 9300 + Math.floor(Math.random() * 500);
  const args = [`--remote-debugging-port=${port}`, `--user-data-dir=${dir}`, "--no-first-run", "--no-default-browser-check", "--window-size=1100,800", ...(sw ? SW : []), ...(headless ? ["--headless=new"] : []), ...extra, "about:blank"];
  const child = spawn(BROWSERS[name], args, { stdio: "ignore", detached: false });
  let version;
  for (let i = 0; i < 100 && !version; i++) {
    await sleep(200);
    try { version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); } catch {}
  }
  if (!version) { child.kill(); throw new Error("browser did not start: " + name); }
  const cdp = new Cdp(version.webSocketDebuggerUrl);
  await cdp.open();
  return {
    port, cdp, child, dir,
    async stop() {
      cdp.close();
      try { execFileSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" }); } catch {}
      await sleep(500);
      try { rmSync(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 }); } catch {}
    },
  };
}

/** target に接続して、probe を実行し、観測値を返す */
async function measure(b, { newContext, engine, bringToFront, emulateFocus, hide, reveal, query = "" }) {
  let targetId, ctxId;
  if (newContext) {
    ctxId = (await b.cdp.send("Target.createBrowserContext")).browserContextId;
    targetId = (await b.cdp.send("Target.createTarget", { url: "about:blank", browserContextId: ctxId })).targetId;
  } else {
    const list = await (await fetch(`http://127.0.0.1:${b.port}/json/list`)).json();
    targetId = list.find((t) => t.type === "page").id;
  }
  const { sessionId } = await b.cdp.send("Target.attachToTarget", { targetId, flatten: true });
  const S = (m, p) => b.cdp.send(m, p, sessionId);
  const consoleMsgs = [];
  b.cdp.handlers.push((m) => {
    if (m.sessionId !== sessionId) return;
    if (m.method === "Runtime.consoleAPICalled" && ["error", "warning"].includes(m.params.type)) consoleMsgs.push(m.params.args.map((a) => a.value ?? a.description).join(" ").slice(0, 200));
    if (m.method === "Runtime.exceptionThrown") consoleMsgs.push("EXC " + (m.params.exceptionDetails.exception?.description ?? m.params.exceptionDetails.text).slice(0, 200));
  });
  await S("Page.enable"); await S("Runtime.enable");
  if (emulateFocus) await S("Emulation.setFocusEmulationEnabled", { enabled: true });
  await S("Page.navigate", { url: `${ORIGIN}/probe.html?engine=${engine}${query}` });
  await sleep(1500);
  if (bringToFront) await S("Page.bringToFront");
  const ev = async (expr) => (await S("Runtime.evaluate", { expression: expr, awaitPromise: true, returnByValue: true })).result.value;
  const windowOf = async (tid) => (await b.cdp.send("Browser.getWindowForTarget", { targetId: tid })).windowId;
  let cover;
  if (hide === "minimize") {
    await b.cdp.send("Browser.setWindowBounds", { windowId: await windowOf(targetId), bounds: { windowState: "minimized" } });
  } else if (hide === "occlude") {
    // 別のウィンドウ(既定のコンテキスト)を最大化して、計測するウィンドウを覆う
    cover = (await b.cdp.send("Target.createTarget", { url: "about:blank", newWindow: true })).targetId;
    await b.cdp.send("Browser.setWindowBounds", { windowId: await windowOf(cover), bounds: { windowState: "maximized" } });
  }
  if (hide) await sleep(1500);
  const before = hide ? await ev("document.visibilityState") : null;
  await ev("window.__probeStart()");
  await sleep(3000);
  const r = await ev("window.__probeRead()");
  r.visibilityBeforeStart = before;
  if (hide && reveal) {
    if (hide === "minimize") await b.cdp.send("Browser.setWindowBounds", { windowId: await windowOf(targetId), bounds: { windowState: "normal" } });
    if (reveal === "bringToFront" || hide === "occlude") await S("Page.bringToFront");
    if (reveal === "activate") await b.cdp.send("Target.activateTarget", { targetId });
    await sleep(2500);
    r.afterReveal = await ev("window.__probeRead()");
    r.afterReveal.events = r.afterReveal.events.slice(-4);
  }
  if (cover) { try { await b.cdp.send("Target.closeTarget", { targetId: cover }); } catch {} }
  r.console = consoleMsgs;
  try { await b.cdp.send("Target.closeTarget", { targetId }); if (ctxId) await b.cdp.send("Target.disposeBrowserContext", { browserContextId: ctxId }); } catch {}
  return r;
}

const CELLS = [];
// [名前, ブラウザ, headless, 起動の追加引数, 計測の設定]。noSw = SwiftShader の指定を付けない(実 GPU のまま。システムの Edge/Chrome はこちらで動く)
for (const [b, headless, noSw] of [["chromium", true, false], ["edge", false, true], ["chrome", false, true], ["edge", true, true], ["edge", false, false]]) {
  for (const engine of ["raw", "three"]) {
    for (const newContext of [false, true]) {
      CELLS.push([`${b}-${headless ? "headless" : "headed"}${noSw ? "-gpu" : "-sw"}/${newContext ? "new" : "default"}/${engine}`, b, headless, [], { newContext, engine, noSw }]);
    }
  }
}
// 非表示(最小化・別のウィンドウに覆われる)で始めたとき
for (const hide of ["minimize", "occlude"]) {
  for (const [label, extra, reveal] of [["plain", [], "bringToFront"], ["no-occlusion-calc", ["--disable-features=CalculateNativeWinOcclusion"], "bringToFront"], ["no-backgrounding", ["--disable-renderer-backgrounding", "--disable-background-timer-throttling", "--disable-backgrounding-occluded-windows", "--disable-features=CalculateNativeWinOcclusion"], "bringToFront"]]) {
    CELLS.push([`edge-headed-gpu/new/raw/hide-${hide}/${label}`, "edge", false, extra, { newContext: true, engine: "raw", noSw: true, hide, reveal }]);
  }
}
const only = process.env.CELLS;
const results = [];
for (const [name, browser, headless, extra, opts] of CELLS) {
  if (only && !name.includes(only)) continue;
  let b;
  try {
    b = await launch(browser, { headless, extra, sw: !opts.noSw });
    const r = await measure(b, opts);
    results.push({ name, ...r });
    console.log(`${name}: ${r.visibilityBeforeStart !== undefined && r.visibilityBeforeStart !== null ? "visBefore=" + r.visibilityBeforeStart + " " : ""}vis=${r.visibilityState} focus=${r.hasFocus} raf=${r.rafCalls} ctx=${r.ctx} lost=${r.lost} canvas=${JSON.stringify(r.canvas)} renderer=${r.renderer}${r.console.length ? " console=" + JSON.stringify(r.console) : ""}${r.afterReveal ? " AFTER-REVEAL vis=" + r.afterReveal.visibilityState + " raf=" + r.afterReveal.rafCalls + " ctx=" + r.afterReveal.ctx : ""}${r.events.length ? " events=" + JSON.stringify(r.events.map((e) => e.type + ":" + e.detail)) : ""}`);
  } catch (e) {
    console.log(`${name}: ERROR ${e.message}`);
    results.push({ name, error: e.message });
  } finally {
    await b?.stop();
  }
}
if (process.env.OUT) writeFileSync(process.env.OUT, JSON.stringify(results, null, 1));
