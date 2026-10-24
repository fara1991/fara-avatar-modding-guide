// 記事内チュートリアルの埋め込みの DOM の骨格(タグ・クラス・data-*・aria-*・hidden・disabled・type・href・id)を、Three.js 版と C# 版で並べて比べる(S4)
import { launch, newPage } from "./lib.mjs";
import { skeleton } from "./tutorial-lib.mjs";
const NEW = process.env.NEW || "http://127.0.0.1:8787";
const cases = [["navigation-numpad", "/learn/getting-started/navigation"], ["transform", "/learn/basics/transform"], ["extrude-inset-loopcut", "/learn/modeling/extrude-inset-loopcut"], ["unity-basics", "/learn/unity/editor-overview"], ["ma-outfit", "/learn/unity/modular-avatar-outfit"]];
let failed = 0;
const browser = await launch();
for (const [name, url] of cases) {
  const res = {};
  for (const cs of [false, true]) {
    const page = await newPage(browser, { width: 1280, height: 900 });
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto(`${NEW}${url}${cs ? "?engine=cs" : ""}`, { waitUntil: "load" });
    const sel = `.practice-embed[data-scenario="${name}"]`;
    await page.waitForSelector(sel + ".is-ready", { timeout: 60000 });
    const embed = page.locator(sel);
    await embed.scrollIntoViewIfNeeded();
    await embed.locator("canvas").first().waitFor({ timeout: 60000 });
    await page.waitForTimeout(1200);
    const states = {};
    states.initial = await embed.evaluate(skeleton);
    await embed.locator(".tut-overlay").click();
    await page.waitForTimeout(300);
    states.focused = await embed.evaluate(skeleton);
    await embed.locator('[data-act="skip"]').click();
    await page.waitForTimeout(300);
    states.skipped = await embed.evaluate(skeleton);
    await embed.locator('[data-act="images"]').click();
    await page.waitForTimeout(500);
    states.images = await embed.evaluate(skeleton);
    await embed.locator('[data-act="images"]').click();
    await page.waitForTimeout(500);
    for (let i = 0; i < 40; i++) { const t = await embed.locator(".tut-count").textContent(); const [a, b] = t.split("/").map((x) => Number(x.trim())); if (a >= b) break; await embed.locator('[data-act="skip"]').click(); await page.waitForTimeout(80); }
    await page.waitForTimeout(400);
    states.complete = await embed.evaluate(skeleton);
    res[cs ? "cs" : "js"] = states;
    await page.context().close();
  }
  for (const st of Object.keys(res.js)) {
    const a = res.js[st], b = res.cs[st];
    const diffs = [];
    for (let i = 0; i < Math.max(a.length, b.length); i++) if (a[i] !== b[i]) diffs.push(`  JS: ${a[i]}\n  C#: ${b[i]}`);
    if (diffs.length) { failed++; console.log(`NG  ${name} / ${st}: ${diffs.length} 行違う\n${diffs.slice(0, 6).join("\n")}`); } else console.log(`ok  ${name} / ${st} (${a.length} 行)`);
  }
}
await browser.close();
process.exit(failed ? 1 : 0);
