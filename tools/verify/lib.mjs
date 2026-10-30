import { chromium } from "playwright-core";
export const EXE = "C:/Users/Fara/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe";
/** ヘッドレスで WebGL を使う(練習の 3D。GPU が無くてもソフトウェア描画 SwiftShader で動く) */
export const WEBGL_ARGS = process.env.GPU === "1"
  ? ["--use-angle=d3d11", "--ignore-gpu-blocklist", "--enable-webgl", "--enable-gpu-rasterization"]   // GPU=1: 実機の GPU で描く(SwiftShader との文字の描き方の差を調べる。画像比較の既定は SwiftShader)
  : ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--enable-webgl"];
export async function launch() {
  // 稀にブラウザの起動が落ちる(共有の ms-playwright を他の作業が触っているとき等)ので、数回試す
  for (let i = 1; ; i++) {
    try { return await chromium.launch({ headless: process.env.HEADED !== "1", ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}), args: WEBGL_ARGS }); } catch (e) { if (i >= 6) throw e; await new Promise((r) => setTimeout(r, 2000)); }
  }
}
/** ページを作り、コンソールエラー・CSP 違反・ページエラーを集める */
export async function newPage(browser, { width = 1280, height = 800, theme = "dark", seed = {}, mobile = false } = {}) {
  const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: Number(process.env.DPR || 1), colorScheme: theme, hasTouch: mobile, isMobile: mobile });
  await ctx.addInitScript(({ seed, theme }) => {
    try {
      if (!sessionStorage.getItem("__seeded")) {
        sessionStorage.setItem("__seeded", "1");
        if (!window.__noThemeSeed) localStorage.setItem("avatar-modding-guide.theme", theme);
        for (const [k, v] of Object.entries(seed)) localStorage.setItem(k, v);
      }
    } catch {
      // 保存領域が使えない（プライベートブラウズ・保存の禁止など）ときは、初期値が入らないだけ。確認は続ける
    }
    window.__csp = [];
    document.addEventListener("securitypolicyviolation", (e) => window.__csp.push(e.violatedDirective + " " + e.blockedURI));
  }, { seed, theme });
  const page = await ctx.newPage();
  page.problems = [];
  page.on("console", (m) => { if (m.type() === "error" || m.type() === "warning") page.problems.push(`${m.type()}: ${m.text()}`); });
  page.on("pageerror", (e) => page.problems.push("pageerror: " + e.message));
  page.on("requestfailed", (r) => page.problems.push("requestfailed: " + r.url()));
  return page;
}
export async function csp(page) { return page.evaluate(() => window.__csp || []); }
