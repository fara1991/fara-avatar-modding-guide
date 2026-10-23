import { chromium } from "playwright-core";
export const EXE = "C:/Users/Fara/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe";
export async function launch() {
  // 稀にブラウザの起動が落ちる(共有の ms-playwright を他の作業が触っているとき等)ので、数回試す
  for (let i = 1; ; i++) {
    try { return await chromium.launch({ ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}) }); } catch (e) { if (i >= 6) throw e; await new Promise((r) => setTimeout(r, 2000)); }
  }
}
/** ページを作り、コンソールエラー・CSP 違反・ページエラーを集める */
export async function newPage(browser, { width = 1280, height = 800, theme = "dark", seed = {}, mobile = false } = {}) {
  const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1, colorScheme: theme, hasTouch: mobile, isMobile: mobile });
  await ctx.addInitScript(({ seed, theme }) => {
    try {
      if (!sessionStorage.getItem("__seeded")) {
        sessionStorage.setItem("__seeded", "1");
        if (!window.__noThemeSeed) localStorage.setItem("avatar-modding-guide.theme", theme);
        for (const [k, v] of Object.entries(seed)) localStorage.setItem(k, v);
      }
    } catch {}
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
