// 記事内チュートリアル（::: practice）の手順ごとの画像を作る。
// スマートフォンや「画像で見る」で表示する /images/tutorials/<シナリオ名>/NN.webp を、
// サイトの 3D ビューをヘッドレス Chrome で動かして撮る（各手順を「次へ（スキップ）」と同じ処理で再現した直後の画面）。
//
// 使い方（Node.js 22 以上。外部パッケージは不要）:
//   1. サイトを起動する: dotnet run（http://localhost:5xxx）か、書き出して npx wrangler dev（http://localhost:8787）
//   2. node tools/tutorial-images/capture.mjs http://localhost:8787            … すべてのシナリオ
//      node tools/tutorial-images/capture.mjs http://localhost:8787 transform  … 指定したシナリオだけ
//   Chrome の場所は環境変数 CHROME で変えられる。シナリオ（wwwroot/js/scenarios.js）を変えたら撮り直す。
import { spawn } from "node:child_process";
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, readdirSync, rmSync, existsSync, statSync } from "node:fs";
import { tmpdir, platform } from "node:os";
import { join, dirname, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const outRoot = join(root, "wwwroot", "images", "tutorials");
const [base, ...only] = process.argv.slice(2);
if (!base) {
  console.error("使い方: node tools/tutorial-images/capture.mjs <サイトの URL> [シナリオ名...]");
  process.exit(1);
}

// Content/learn/<category>/NN-slug.md の「::: practice 名前」から、シナリオ → 記事の URL を集める
function findScenarios() {
  const map = new Map();
  const learn = join(root, "Content", "learn");
  for (const cat of readdirSync(learn)) {
    const dir = join(learn, cat);
    if (!statSync(dir).isDirectory()) continue;
    for (const file of readdirSync(dir).filter(f => /^\d+-.+\.md$/.test(f))) {
      const slug = file.replace(/^\d+-/, "").replace(/\.md$/, "");
      for (const m of readFileSync(join(dir, file), "utf8").matchAll(/^:::\s*practice\s+([a-z0-9-]+)\s*$/gm)) map.set(m[1], `/learn/${cat}/${slug}`);
    }
  }
  return map;
}

const chromePath = process.env.CHROME ?? {
  win32: "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  darwin: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
}[platform()] ?? "google-chrome";

const sleep = ms => new Promise(r => setTimeout(r, ms));
const profile = mkdtempSync(join(tmpdir(), "tutorial-images-"));
// WebGL をヘッドレスで動かすには SwiftShader（ソフトウェア描画）を使う
const chrome = spawn(chromePath, [
  "--headless=new", "--disable-gpu", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--hide-scrollbars",
  "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank",
], { stdio: "ignore" });

try {
  // 自分で起動した Chrome のポートだけを使う（DevToolsActivePort はこのプロファイルにだけ書かれる）
  let target;
  for (let i = 0; i < 150 && !target; i++) {
    await sleep(200);
    try {
      const port = readFileSync(join(profile, "DevToolsActivePort"), "utf8").split("\n")[0].trim();
      target = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find(t => t.type === "page");
    } catch { /* 起動待ち */ }
  }
  if (!target) throw new Error(`Chrome を起動できませんでした（${chromePath}）。環境変数 CHROME で場所を指定してください`);

  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.addEventListener("open", resolve); ws.addEventListener("error", reject); });
  let id = 0;
  const pending = new Map();
  ws.addEventListener("message", m => {
    const msg = JSON.parse(m.data);
    if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
    if (msg.method === "Runtime.exceptionThrown") console.error("ページのエラー:", msg.params.exceptionDetails.exception?.description);
  });
  const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  const evaluate = async expr => {
    const r = await send("Runtime.evaluate", { expression: expr, returnByValue: true, awaitPromise: true });
    if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? "evaluate failed");
    return r.result?.result?.value;
  };
  await send("Runtime.enable");
  await send("Page.enable");
  // 記事の幅（約 734px）× 1.5 倍 ≒ 1100 × 619 の画像になる
  await send("Emulation.setDeviceMetricsOverride", { width: 1300, height: 1000, deviceScaleFactor: 1.5, mobile: false });

  const scenarios = findScenarios();
  const names = only.length ? only : [...scenarios.keys()];
  const pages = new Map();
  for (const name of names) {
    const url = scenarios.get(name);
    if (!url) { console.error(`「${name}」を埋め込んでいる記事が見つかりません`); process.exitCode = 1; continue; }
    if (!pages.has(url)) pages.set(url, []);
    pages.get(url).push(name);
  }

  for (const [url, list] of pages) {
    await send("Page.navigate", { url: new URL(`${url}?debug=1`, base).href });
    let ready = false;
    for (let i = 0; i < 60 && !ready; i++) { await sleep(250); ready = await evaluate("!!(window.__tutorials && window.__tutorials.length)").catch(() => false); }
    if (!ready) throw new Error(`${url} でチュートリアルが見つかりません（?debug=1 で __tutorials が必要）`);
    const all = await evaluate("__tutorials.map(t => t.name)");
    for (const name of list) {
      const ti = all.indexOf(name);
      const count = await evaluate(`__tutorials[${ti}].scenario.steps.length`);
      const dir = join(outRoot, name);
      mkdirSync(dir, { recursive: true });
      await evaluate(`__tutorials[${ti}].el.scrollIntoView({ block: "center" })`);
      await sleep(300);
      for (let k = 0; k < count; k++) {
        await evaluate(`__tutorials[${ti}].captureStep(${k})`);
        // 描画（と吹き出しの位置の更新）を 2 フレーム待つ
        await evaluate("new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        await sleep(150);
        const rect = await evaluate(`(() => { const r = __tutorials[${ti}].ui.vp.getBoundingClientRect(); return { x: r.left + scrollX, y: r.top + scrollY, width: r.width, height: r.height, scale: 1 }; })()`);
        const shot = await send("Page.captureScreenshot", { format: "webp", quality: 82, clip: rect, captureBeyondViewport: true });
        const file = join(dir, `${String(k + 1).padStart(2, "0")}.webp`);
        writeFileSync(file, Buffer.from(shot.result.data, "base64"));
      }
      // 手順が減ったときの古い画像を消す
      for (const f of readdirSync(dir)) {
        const n = parseInt(f, 10);
        if (!Number.isFinite(n) || n > count) rmSync(join(dir, f));
      }
      console.log(`${name}: ${count} 枚 → ${relative(root, dir)}`);
    }
  }
  ws.close();
} finally {
  chrome.kill();
  await sleep(500);
  if (existsSync(profile)) rmSync(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 });
}
