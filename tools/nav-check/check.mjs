// ページ移動（wwwroot/js/nav.js）を、Edge（headless）で実際に操作して確かめる。外部パッケージ不要（Node.js 22 以上）。
// 使い方:
//   npx wrangler dev    … または、dist/ を配信するサーバー（http://localhost:8787 など）
//   node tools/nav-check/check.mjs http://localhost:8787
// Edge の場所は環境変数 EDGE_PATH で変えられる（既定は Windows の Microsoft Edge）。
// 読み直しの有無（グローバルの印・navigation entry）、要素の作り直し、リスナーの数、戻る・進む、再読み込みの復元を確かめる。
import { spawn } from "node:child_process";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const BASE = process.argv[2] ?? "http://localhost:8787";
const EDGE = process.env.EDGE_PATH ?? "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const PORT = 9333 + Math.floor(Math.random() * 500);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const results = [];
const check = (name, ok, extra = "") => results.push({ name, ok: !!ok, extra: extra ? String(extra) : "" });

const profile = mkdtempSync(join(tmpdir(), "nav-check-"));
spawn(EDGE, ["--headless=new", `--remote-debugging-port=${PORT}`, `--user-data-dir=${profile}`,
  "--no-first-run", "--disable-gpu", "about:blank"], { stdio: "ignore", detached: true }).unref();

let version = null;
for (let i = 0; i < 100 && !version; i++) {
  try { version = await (await fetch(`http://127.0.0.1:${PORT}/json/version`)).json(); } catch { await sleep(200); }
}
if (!version) throw new Error("Edge に接続できません（EDGE_PATH を確かめてください）");

const created = await (await fetch(`http://127.0.0.1:${PORT}/json/new?${BASE}/learn`, { method: "PUT" })).json();
const ws = new WebSocket(created.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));

let seq = 0;
const pending = new Map();
ws.onmessage = (ev) => {
  const m = JSON.parse(ev.data);
  if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
};
const send = (method, params = {}) => new Promise((r) => {
  const id = ++seq;
  pending.set(id, r);
  ws.send(JSON.stringify({ id, method, params }));
});
async function evalJs(expression) {
  const m = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (m.result?.exceptionDetails) throw new Error("評価エラー: " + (m.result.exceptionDetails.exception?.description ?? m.result.exceptionDetails.text));
  return m.result?.result?.value;
}
async function waitFor(expression, timeout = 10000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    try { if (await evalJs(expression)) return true; } catch { /* 読み込み中 */ }
    await sleep(100);
  }
  return false;
}
/** window に付いているイベントの種類ごとの数（二重登録がないかを見る） */
async function listenerCounts() {
  const r = await send("Runtime.evaluate", { expression: "window" });
  const { listeners } = (await send("DOMDebugger.getEventListeners", { objectId: r.result.result.objectId })).result;
  const out = {};
  for (const l of listeners) out[l.type] = (out[l.type] || 0) + 1;
  return JSON.stringify(out);
}
const navEntries = () => evalJs(`performance.getEntriesByType("navigation").length`);

await send("Runtime.enable");
await send("DOMDebugger.enable");
await waitFor(`document.readyState === "complete" && !!window.faraNav`);

// --- 1. 同じページの中での移動（/learn → カテゴリ → 記事） ---
await evalJs(`(() => {
  window.__mark = "A";
  window.__errors = [];
  window.addEventListener("error", (e) => window.__errors.push(String(e.message)));
  window.__scene = document.getElementById("site-scene");
  window.__header = document.querySelector(".app-header");
  window.__title0 = document.title;
  window.__nav0 = performance.getEntriesByType("navigation").length;
  document.documentElement.classList.add("sidebar-hidden");
  return true;
})()`);
const listeners0 = await listenerCounts();

check("カテゴリへのリンクが押せた", await evalJs(`(() => { const a = document.querySelector('#main-content a[href="/learn/getting-started"]'); if (!a) return false; a.click(); return true; })()`));
check("カテゴリへ移動（URL が変わる）", await waitFor(`location.pathname === "/learn/getting-started" && document.querySelector("#main-content h1")`), await evalJs("location.pathname"));
check("画面は読み直されていない（グローバルの印が残る）", (await evalJs("window.__mark")) === "A");
check("navigation entry が増えていない", (await navEntries()) === (await evalJs("window.__nav0")));
check("背景の canvas が作り直されていない", await evalJs(`window.__scene === document.getElementById("site-scene")`));
check("ヘッダーが残っている", await evalJs(`window.__header === document.querySelector(".app-header")`));
check("サイドバーの開閉状態が残る", await evalJs(`document.documentElement.classList.contains("sidebar-hidden")`));
check("<title> が切り替わった", await evalJs(`document.title !== window.__title0`), await evalJs("document.title"));
check("見出しにフォーカスが移る", await evalJs(`document.activeElement && document.activeElement.tagName === "H1"`));

// --- 2. 記事へ（記事の中の目次などのページ専用 JS が動く） ---
check("記事へのリンクが押せた", await evalJs(`(() => { const a = document.querySelector('#main-content a[href^="/learn/getting-started/"]'); if (!a) return false; a.click(); return true; })()`));
check("記事へ移動", await waitFor(`/^\\/learn\\/getting-started\\/[a-z]/.test(location.pathname) && document.querySelector("#main-content h1")`), await evalJs("location.pathname"));
check("記事でも読み直されていない", (await evalJs("window.__mark")) === "A");
check("記事の本文がある", await evalJs(`document.querySelector("#main-content article, #main-content h1") !== null`));
check("サイドバーのリンクの印が新しいページに合う", await evalJs(`!!document.querySelector(".sidebar-panel a.active, .sidebar-panel .nav-link.active")`));
const listenersArticle = await listenerCounts(); // toc.js は記事で初めて読み込まれる（その分の +1 は想定どおり）

// --- 3. 戻る・進む ---
await evalJs("history.back()");
check("戻る（カテゴリへ戻る）", await waitFor(`location.pathname === "/learn/getting-started"`), await evalJs("location.pathname"));
check("戻っても読み直されていない", (await evalJs("window.__mark")) === "A");
await evalJs("history.back()");
check("戻る（目次へ戻る）", await waitFor(`location.pathname === "/learn"`), await evalJs("location.pathname"));
await evalJs("history.forward()");
check("進む", await waitFor(`location.pathname === "/learn/getting-started"`), await evalJs("location.pathname"));
check("進んでも読み直されていない", (await evalJs("window.__mark")) === "A");
const listenersNow = await listenerCounts();
check("戻る・進むで window のリスナーが増えていない（記事の読み込み後と比べる）", listenersArticle === listenersNow, listenersArticle + " -> " + listenersNow);

// --- 4. 3D・WebSocket のページは、読み直す（/practice） ---
check("練習のリンクが押せた", await evalJs(`(() => { const a = document.querySelector('.sidebar-panel a[href="/practice"]'); if (!a) return false; a.click(); return true; })()`));
check("練習のページは読み直す（従来どおり）", await waitFor(`location.pathname === "/practice" && document.readyState === "complete" && window.__mark === undefined`), await evalJs("location.pathname + ' mark=' + window.__mark"));

// --- 5. 同じページの中の絞り込み（pushState）のあとで、サイドバーから同じページへ（絞り込みが外れる） ---
check("ショートカットへ移動", await evalJs(`(() => { location.assign("/shortcuts"); return true; })()`) && await waitFor(`location.pathname === "/shortcuts" && !!window.faraNav && !!document.querySelector("#shortcut-filters a[data-category]")`));
await evalJs(`(() => { window.__errors = []; window.addEventListener("error", (e) => window.__errors.push(String(e.message))); return true; })()`);
const catLink = await evalJs(`(() => { const a = [...document.querySelectorAll("#shortcut-filters a[data-category]")].find(a => a.dataset.category); if (!a) return ""; a.click(); return a.dataset.category; })()`);
check("絞り込みのボタンで URL に ?category が付く", await waitFor(`location.search.indexOf("category=") >= 0`) && !!catLink, await evalJs("location.search"));
check("絞り込みが効いている（一部の行が隠れる）", await evalJs(`document.querySelectorAll("#shortcut-rows tr[hidden]").length > 0`));
check("サイドバーから同じページを開くと、絞り込みが外れる（ページが替わる）", await evalJs(`(() => { const a = document.querySelector('.sidebar-panel a[href="/shortcuts"]'); if (!a) return false; a.click(); return true; })()`) && await waitFor(`location.search === "" && document.querySelectorAll("#shortcut-rows tr[hidden]").length === 0`), await evalJs("location.href"));

// --- 6. 再読み込みで、同じ位置に戻る ---
await evalJs(`(() => { window.scrollTo(0, 600); return true; })()`);
await sleep(1000); // スクロールが止まり、保存されるまで待つ（滑らかなスクロールの途中で再読み込みしない）
const beforeReload = await evalJs(`scrollY + " " + JSON.stringify(history.state) + " " + document.documentElement.scrollHeight`);
await evalJs(`location.reload()`);
check("再読み込みで、同じ位置に戻る", await waitFor(`document.readyState === "complete" && Math.abs(window.scrollY - 600) < 40`, 8000), await evalJs("window.scrollY + \" \" + JSON.stringify(history.state)") + " before:" + beforeReload);

// --- 7. TIPS（絞り込み・ページ専用 JS）を行き来しても、読み直さず、二重に登録されない ---
await evalJs(`(() => { location.assign("/tips"); return true; })()`);
await waitFor(`location.pathname === "/tips" && document.querySelectorAll("[data-tip-item]").length > 0 && !!window.faraNav`);
await evalJs(`(() => { window.__mark = "B"; window.__errors = []; window.addEventListener("error", (e) => window.__errors.push(String(e.message))); window.__nav1 = performance.getEntriesByType("navigation").length; return true; })()`);
const tipsListeners0 = await listenerCounts();
for (const target of ["/glossary", "/tips", "/howto", "/tips"]) {
  await evalJs(`(() => { const a = document.querySelector('.sidebar-panel a[href="${target}"]'); if (a) a.click(); return !!a; })()`);
  await waitFor(`location.pathname === "${target}" && document.querySelector("#main-content h1")`);
}
check("TIPS・用語集・何て名前？ を行き来しても読み直さない", (await evalJs("window.__mark")) === "B");
check("行き来のあとも navigation entry が増えていない", (await navEntries()) === (await evalJs("window.__nav1")));
const tipsListeners1 = await listenerCounts();
check("行き来しても window のリスナーが増えない", tipsListeners0 === tipsListeners1, tipsListeners0 + " -> " + tipsListeners1);
const filterOk = await evalJs(`(() => {
  const input = document.querySelector("[data-tips-q]");
  if (!input) return "no-input";
  input.value = "zzzz-no-match";
  input.dispatchEvent(new Event("input"));
  const c = document.querySelector("[data-tips-count]");
  return c ? c.textContent : "no-count";
})()`);
check("TIPS の絞り込みが新しいページで効く", /件中 0 件/.test(String(filterOk)), String(filterOk));
await evalJs(`(() => { const input = document.querySelector("[data-tips-q]"); if (input) { input.value = ""; input.dispatchEvent(new Event("input")); } return true; })()`);

// --- 8. ヘッダーの検索（移動のあとも動く）と、検索ページ ---
await evalJs(`(() => { const input = document.querySelector("[data-quick-search] input[type=search]"); if (input) { input.value = "ブラ"; input.dispatchEvent(new Event("input")); } return !!input; })()`);
check("ヘッダーの検索が移動のあとも候補を出す", await waitFor(`(() => { const l = document.querySelector("[data-quick-search] .quick-search-list"); return !!l && !l.hidden && l.querySelectorAll(".quick-search-item").length > 0; })()`, 5000));
await evalJs(`(() => { const a = document.querySelector('[data-quick-search] .quick-search-all'); if (a) a.click(); else location.assign("/search?q=" + encodeURIComponent("ブラ")); return true; })()`);
check("検索ページへ移動して結果が出る", await waitFor(`location.pathname === "/search" && document.querySelector("#search-results") && document.querySelector("#search-results").textContent.length > 0`, 8000), await evalJs("location.search"));

// --- 9. 途中の JS エラーがない ---
check("ページ移動の途中で JS エラーが出ていない", (await evalJs("window.__errors.length")) === 0, await evalJs("JSON.stringify(window.__errors)"));

for (const r of results) console.log(`${r.ok ? "ok  " : "NG  "} ${r.name}${r.extra ? "  [" + r.extra + "]" : ""}`);
const failed = results.filter((r) => !r.ok).length;
console.log(`\n${results.length - failed} passed, ${failed} failed`);
ws.close();
process.exit(failed ? 1 : 0);
