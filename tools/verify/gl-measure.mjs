// Phase 3b S2 の計測(AOT なしの WebAssembly): 起動・フレーム・重い操作・新しいコンテキストの繰り返し・解放。  node gl-measure.mjs
//   ORIGIN=http://127.0.0.1:8790(gl-serve.mjs。LAB=レンダーラボの publish)  CHROME_PATH=…  COLD=5  CONTEXTS=20  CYCLES=20
// 結果は JSON で標準出力の最後に出す。比べる相手(ネイティブの .NET)の値は Render.Tests の NativeBenchmarks が出す。
import { launch } from "./lib.mjs";

const ORIGIN = process.env.ORIGIN || "http://127.0.0.1:8790";
const COLD = Number(process.env.COLD || 5);
const CONTEXTS = Number(process.env.CONTEXTS || 20);
const CYCLES = Number(process.env.CYCLES || 20);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const out = {};
const browser = await launch();

async function page(url, { width = 1000, height = 600 } = {}) {
  const ctx = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1 });
  const p = await ctx.newPage();
  p.problems = [];
  p.on("console", (m) => { if (["error", "warning"].includes(m.type())) p.problems.push(m.text().slice(0, 200)); });
  p.on("pageerror", (e) => p.problems.push("pageerror " + e.message.slice(0, 200)));
  await p.addInitScript(() => {
    window.__t = {};
    const poll = setInterval(() => {
      let d; try { d = window.__glDiag && JSON.parse(window.__glDiag()); } catch { return; }
      if (d?.started && window.__t.started == null) window.__t.started = performance.now();
      if (d?.drawn > 0 && window.__t.firstDraw == null) { window.__t.firstDraw = performance.now(); clearInterval(poll); }
    }, 5);
  });
  await p.goto(url);
  return { ctx, p };
}
const report = (p, name) => p.evaluate((n) => window.__labReport?.[n], name);

// 1. 起動(冷: 毎回新しいコンテキスト。キャッシュなし) と 温(同じコンテキストで再読み込み)
const cold = [];
for (let i = 0; i < COLD; i++) {
  const { ctx, p } = await page(`${ORIGIN}/lab/`);
  await p.waitForFunction(() => window.__t.firstDraw != null, null, { timeout: 90000 });
  const t = await p.evaluate(() => ({ ...window.__t, nav: performance.getEntriesByType("navigation")[0].domContentLoadedEventEnd }));
  const bytes = await p.evaluate(() => performance.getEntriesByType("resource").reduce((s, r) => s + (r.decodedBodySize || 0), 0));
  const marks = await p.evaluate(() => Object.fromEntries(performance.getEntriesByType("mark").map((m) => [m.name, m.startTime])));
  const g = (a, b) => (marks[a] != null && marks[b] != null ? +(marks[b] - marks[a]).toFixed(1) : null);
  cold.push({ started: Math.round(t.started), firstDraw: Math.round(t.firstDraw), decodedBytes: bytes,
    abs: Object.fromEntries(Object.entries(marks).map(([k, v]) => [k, Math.round(v)])),
    split: { navToMain: Math.round(marks["lab:main"]), importModule: g("lab:main", "lab:imported"), importToGlStart: g("lab:imported", "gl:start"), initContextAndShader: g("gl:start", "gl:inited"), firstFrameCsAndPaint: g("gl:inited", "gl:firstPaint") } });
  if (i === 0) {
    await p.reload();
    await p.waitForFunction(() => window.__t.firstDraw != null, null, { timeout: 90000 });
    const w = await p.evaluate(() => window.__t);
    out.warmReload = { started: Math.round(w.started), firstDraw: Math.round(w.firstDraw) };
  }
  await ctx.close();
}
out.cold = cold;

// 2. フレーム(C# の 1 フレームの CPU = Build + 詰める + JS 呼び出し(描画の発行まで)。rAF の間隔)
{
  const { ctx, p } = await page(`${ORIGIN}/lab/?scene=${encodeURIComponent(JSON.stringify([{ kind: "sphere" }]))}`);
  await p.waitForFunction(() => window.__t.firstDraw != null, null, { timeout: 90000 });
  const frames = async (label) => {
    await p.evaluate(() => window.__labControl("start", 0)); // 計測を 0 から(再開)
    await p.waitForFunction(() => window.__t && window.__glDiag && JSON.parse(window.__glDiag()).drawn > 0);
    await sleep(6000);
    await p.evaluate(() => window.__labControl("frames", 0));
    const f = await report(p, "frames");
    const d = JSON.parse(await p.evaluate(() => window.__glDiag()));
    out[label] = { csFrames: f.frames, csMsPerFrame: f.msPerFrame, jsCallbackMsPerFrame: d.callbackMs / d.frames, rafPerSec: d.rafCalls / 6 };
  };
  await frames("frameObjectMode");
  await p.click("#viewport", { position: { x: 480, y: 270 } });
  await p.keyboard.press("Tab");
  await p.keyboard.press("KeyA");
  await sleep(300);
  const d0 = JSON.parse(await p.evaluate(() => window.__glDiag()));
  await p.evaluate(() => window.__labControl("frames", 0));
  const f0 = await report(p, "frames");
  await sleep(6000);
  await p.evaluate(() => window.__labControl("frames", 0));
  const f1 = await report(p, "frames");
  const d1 = JSON.parse(await p.evaluate(() => window.__glDiag()));
  out.frameEditModeAllSelected = { csFrames: f1.frames - f0.frames, csMsPerFrame: (f1.msPerFrame * f1.frames - f0.msPerFrame * f0.frames) / (f1.frames - f0.frames), rafPerSec: (d1.rafCalls - d0.rafCalls) / 6 };

  // 2b. 編集モードで全頂点を G ドラッグ中のページ内のフレーム(C# の Build + 送信 + draw の呼び出しまで = GL の命令の発行を含む。GPU の実行時間は含まない)
  {
    await p.evaluate(() => window.__labControl("start", 0));
    await p.waitForFunction(() => window.__glDiag && JSON.parse(window.__glDiag()).drawn > 0);
    await p.click("#viewport", { position: { x: 480, y: 270 } });
    await p.keyboard.press("Tab");
    await p.keyboard.press("KeyA");
    await p.keyboard.press("KeyG");
    await sleep(300);
    await p.evaluate(() => window.__labControl("frames", 0));
    const a = await report(p, "frames");
    const box = await p.locator("#viewport").boundingBox();
    const N = 240;
    for (let i = 0; i < N; i++) { await p.mouse.move(box.x + 300 + ((i * 7) % 300), box.y + 200 + ((i * 5) % 100)); await sleep(12); }
    await p.evaluate(() => window.__labControl("frames", 0));
    const b = await report(p, "frames");
    const frameMs = b.msPerFrame * b.frames - a.msPerFrame * a.frames, moves = b.pointerEvents - a.pointerEvents, moveMs = b.pointerMs - a.pointerMs;
    // 1 回のマウス移動あたり = ポインターのハンドラー(操作・形の作り直し)+ そのあとのフレーム(Build・送信・draw の呼び出し)。フレームの平均は移動のないフレームも含む
    out.dragFramePage = { frames: b.frames - a.frames, moves, pointerMsPerMove: moveMs / moves, frameMsPerFrame: frameMs / (b.frames - a.frames), perMoveTotalMs: moveMs / moves + frameMs / moves };
    await p.keyboard.press("Escape");
  }

  // 3. 重い操作(WASM)
  out.bench = {};
  for (const [name, n] of [["frame-object", 200], ["frame-edit", 200], ["drag-edit-frame", 100], ["click-object", 100], ["click-vert", 50], ["box-select-all", 10], ["move-all-vertices", 100], ["extrude-start", 5], ["undo", 10]]) {
    await p.evaluate(([nm, k]) => window.__labControl("bench:" + nm, k), [name, n]);
    out.bench[name] = await report(p, "bench:" + name);
  }
  out.problems = p.problems;
  await ctx.close();
}

// 4. 新しいコンテキストを CONTEXTS 回(毎回キャッシュなし)。全部で描画が始まるか
{
  let ok = 0;
  const problems = [];
  for (let i = 0; i < CONTEXTS; i++) {
    const { ctx, p } = await page(`${ORIGIN}/lab/`);
    try { await p.waitForFunction(() => window.__t.firstDraw != null, null, { timeout: 60000 }); ok++; } catch { problems.push(i); }
    problems.push(...p.problems.filter((m) => !/404/.test(m)));
    await ctx.close();
  }
  out.newContexts = { total: CONTEXTS, drew: ok, problems };
}

// 5. 開始と停止を CYCLES 回(WebGL の context・リスナー・rAF が増えないか)
{
  const { ctx, p } = await page(`${ORIGIN}/lab/`);
  await p.waitForFunction(() => window.__t.firstDraw != null, null, { timeout: 90000 });
  await p.evaluate(() => {
    window.__ctxCount = 0;
    const g = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (...a) { const c = g.apply(this, a); if (c && /webgl/.test(a[0])) window.__ctxCount++; return c; };
    window.__live = { raf: 0 };
    const raf = window.requestAnimationFrame;
    window.__rafActive = new Set();
    window.requestAnimationFrame = (fn) => { const id = raf((t) => { window.__rafActive.delete(id); fn(t); }); window.__rafActive.add(id); return id; };
    const caf = window.cancelAnimationFrame;
    window.cancelAnimationFrame = (id) => { window.__rafActive.delete(id); caf(id); };
  });
  for (let i = 0; i < CYCLES; i++) {
    await p.evaluate(() => window.__labControl("stop", 0));
    await p.evaluate(() => window.__labControl("start", 0));
    await sleep(100);
  }
  await sleep(500);
  out.cycles = await p.evaluate(() => ({ cycles: window.__ctxCount, canvases: document.querySelectorAll("#viewport canvas").length, activeRaf: window.__rafActive.size, diag: JSON.parse(window.__glDiag()) }));
  // コンテキスト喪失 → 復帰で描画が続く
  await p.evaluate(() => window.__glLose(300));
  await sleep(1500);
  out.contextRestore = await p.evaluate(() => { const d = JSON.parse(window.__glDiag()); return { lost: d.lost, contextLost: d.contextLost, contextRestored: d.contextRestored, drawn: d.drawn }; });
  await sleep(500);
  out.contextRestore.drawnLater = await p.evaluate(() => JSON.parse(window.__glDiag()).drawn);
  await ctx.close();
}

await browser.close();
console.log(JSON.stringify(out, null, 1));
