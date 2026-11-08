// CSP の違反ゼロの確認(Phase 4。計画書 4.9・8)。 NEW=http://127.0.0.1:8787 node csp-check.mjs
// _headers の Content-Security-Policy が効く配信(wrangler dev)で、主要ページを PC 1280・スマホ 390 で開き、操作を少しして、
// ブラウザの違反の通知(securitypolicyviolation)・コンソールのエラー/警告・失敗したリクエスト・ページのエラーを集める。1 件でもあれば終了コード 1。
// 含むもの: 記事内チュートリアル・練習 3 ページ(WebGL・共有ルームの WebSocket。three.js は S7 で消した)・検索・報告・404・テーマの切り替え。
import { launch, newPage } from "./lib.mjs";
const NEW = (process.env.NEW || "http://127.0.0.1:8787").replace(/\/$/, "");
const PAGES = [
  "/", "/learn", "/learn/basics", "/learn/getting-started/install", "/learn/basics/transform", "/learn/modeling/edit-mode", "/learn/unity/modular-avatar-outfit",
  "/shortcuts", "/glossary", "/howto", "/tips", "/qa", "/addons", "/links", "/changelog", "/privacy", "/report", "/search?q=blender",
  "/practice", "/practice/unity", "/learn/unity/editor-overview", "/learn/unity/modular-avatar-outfit", "/practice/room", "/practice/room?room=cspcheck01", "/no-such-page-here",
];
const b = await launch();
const rows = [];
let total = 0;
for (const [vpName, vp] of [["pc", { width: 1280, height: 800 }], ["sp", { width: 390, height: 844, mobile: true }]]) {
  for (const path of PAGES) {
    const visit = async () => {
    const p = await newPage(b, { ...vp, theme: "dark" });
    const wsErrors = [];
    p.on("websocket", (ws) => ws.on("socketerror", (e) => wsErrors.push("websocket: " + e)));
    const csp = [];
    await p.exposeFunction("__reportCsp", (m) => csp.push(m));
    await p.addInitScript(() => document.addEventListener("securitypolicyviolation", (e) => window.__reportCsp(`${e.violatedDirective} ${e.blockedURI} @${e.sourceFile || ""}:${e.lineNumber || 0}`)));
    const res = await p.goto(NEW + path, { waitUntil: "load" });
    const status = res.status();
    await p.waitForSelector(path === "/no-such-page-here" ? ".fbc-not-found" : ".fbc-page", { timeout: 60000 });
    await p.waitForTimeout(1500);
    // 少し操作する: スクロール・テーマの切り替え・検索欄・練習の 3D(キー)・チュートリアル
    await p.evaluate(() => { window.scrollTo(0, 300); const c = document.querySelector(".fbc-content"); if (c) c.scrollTop = 300; });
    if (vpName === "pc") {
      await p.locator(".fbc-theme-switch input").first().click({ force: true, timeout: 3000 }).catch(() => {});
      await p.waitForTimeout(300);
      await p.keyboard.press("Control+k").catch(() => {});
      await p.keyboard.type("ウェイト").catch(() => {});
      await p.waitForTimeout(500);
      await p.keyboard.press("Escape").catch(() => {});
      for (const sel of ["#practice-viewport", "#unity-practice", "#room-viewport", ".practice-embed"]) {
        const el = p.locator(sel).first();
        if (await el.count()) { await el.click({ position: { x: 60, y: 60 }, timeout: 3000 }).catch(() => {}); await p.keyboard.press("g").catch(() => {}); await p.keyboard.press("Escape").catch(() => {}); }
      }
      await p.waitForTimeout(800);
    }
    const problems = p.problems.filter((m) => !/favicon/.test(m));
    // 404 ページ(知らない URL)は、ステータス 404 でコンソールにリソースエラーが 1 件出る(ページ自体の取得)。それ以外のエラーは数える
    const filtered = path === "/no-such-page-here" ? problems.filter((m) => !/404 \(Not Found\)|status of 404/.test(m) && !m.startsWith("requestfailed")) : problems;
    await p.context().close();
    return { csp, filtered, wsErrors, status };
    };
    // /api/maintenance の requestfailed だけは、kit の MaintenanceInfo が 2 秒で打ち切る(仕様)ので、配信が重いとき(他の作業と並列に動かしたとき)に出ることがある。その場合だけ 1 回やり直す
    let r = await visit();
    const onlyMaintenance = (x) => x.csp.length === 0 && x.wsErrors.length === 0 && x.filtered.length > 0 && x.filtered.every((m) => m.startsWith("requestfailed:") && m.includes("/api/maintenance"));
    if (onlyMaintenance(r)) { console.log(`     (${vpName} ${path}: /api/maintenance が 2 秒の打ち切りで失敗。やり直す)`); r = await visit(); }
    const { csp, filtered, wsErrors, status } = r;
    const n = csp.length + filtered.length + wsErrors.length;
    total += n;
    rows.push({ vp: vpName, path, status, csp, problems: [...filtered, ...wsErrors] });
    console.log(`${n === 0 ? "ok" : "NG"}  ${vpName} ${path} (${status}) CSP 違反 ${csp.length}・コンソール ${filtered.length + wsErrors.length}`);
    for (const m of [...csp, ...filtered, ...wsErrors]) console.log("     " + m);
  }
}
await b.close();
console.log(total ? `\n違反・問題 ${total} 件` : `\nCSP 違反 0・コンソールのエラー/警告 0(${rows.length} 画面)`);
process.exit(total ? 1 : 0);
