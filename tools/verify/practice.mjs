// 練習 3 ページ・記事内チュートリアル・背景の 3D の確認(Phase 3)。  node practice.mjs
//   NEW=http://127.0.0.1:8787 (publish + Build の出力を ./dist に置いて wrangler dev --env dev)  ROUNDS=20(往復の回数)  CHROME_PATH=…
// ブラウザは WebGL が使える設定(lib.mjs の WEBGL_ARGS)で起動する。確認するのは、次のとおり。
//   1. 操作の結果が基準と同じ: /practice と /practice/unity で、決まったキー・マウスの操作をして、結果(位置・回転・スケール・お題の進み・Unity の階層)が practice-baseline.json と一致する
//      (基準は Phase 5 で SSR 版を消す前に、SSR(develop)と移行版の結果が一致したときの値を固定したもの)
//   2. CSP 違反 0・コンソールのエラーと警告 0(練習 3 ページ・記事内チュートリアル・往復の全部で)
//   3. ページ往復で、WebGL の context・ResizeObserver・IntersectionObserver・setInterval・WebSocket・window/document のイベント・DOM の数が増えない(ROUNDS 往復。ガベージコレクションのあと)
//   4. 背景の 3D(canvas#site-scene)は 1 つだけで、ページを移っても増えず、描画のループが増えない
//   5. スマホ(390): 練習 3 ページは案内を出し、WebGL を使わず、記事内チュートリアルの「画像で見る」が動く
//   6. 共有ルーム: 二人が同じ部屋に入れて、追加・移動が届く(tools/room-check/check.mjs は WebSocket だけを直接確かめる。ここは画面から)
import { readFileSync } from "node:fs";
import { launch, newPage, csp } from "./lib.mjs";

const NEW = process.env.NEW || "http://127.0.0.1:8787";
const BASELINE = JSON.parse(readFileSync(new URL("./practice-baseline.json", import.meta.url), "utf8"));
const ROUNDS = Number(process.env.ROUNDS || 20);
// ENGINE=cs: /practice を C# 版(?engine=cs。Phase 3b S3)で動かす。操作の結果は同じ基準(practice-baseline.json)と一致するはず。既定は Three.js 版
const ENGINE = "cs";   // S7 から JS 版は無い（ENGINE は効かない。既定も C#）
const ENGINE_QUERY = ENGINE === "cs" ? "&engine=cs" : "";
const ARTICLE = "/learn/basics/transform";   // 記事内チュートリアル(::: practice transform)がある記事
const ARTICLE2 = "/learn/basics/select";

let failed = 0;
const ok = (cond, label, detail = "") => { console.log(`${cond ? "ok  " : "NG  "} ${label}${!cond && detail ? "  -> " + detail : ""}`); if (!cond) failed++; };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** 数えるための印を、ページの最初に仕込む(WebGL の context・observer・interval・WebSocket・window/document のイベント・描画ループ) */
function instrument() {
  const live = { gl: [], ro: new Set(), io: new Set(), intervals: new Set(), ws: new Set(), raf: 0, listeners: new Map() };
  window.__live = live;
  const getContext = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (type, ...args) {
    const ctx = getContext.call(this, type, ...args);
    if (ctx && /webgl/.test(String(type))) live.gl.push(new WeakRef(ctx));
    return ctx;
  };
  for (const [name, set] of [["ResizeObserver", live.ro], ["IntersectionObserver", live.io]]) {
    const Base = window[name];
    window[name] = class extends Base {
      constructor(...args) { super(...args); set.add(this); }
      disconnect() { set.delete(this); super.disconnect(); }
    };
  }
  const si = window.setInterval, ci = window.clearInterval;
  window.setInterval = (fn, ms, ...a) => { const id = si(fn, ms, ...a); live.intervals.add(id); return id; };
  window.clearInterval = (id) => { live.intervals.delete(id); ci(id); };
  const WS = window.WebSocket;
  window.WebSocket = class extends WS { constructor(...a) { super(...a); live.ws.add(this); this.addEventListener("close", () => live.ws.delete(this)); } };
  const raf = window.requestAnimationFrame;
  window.requestAnimationFrame = (fn) => raf((t) => { live.raf++; fn(t); });
  // window・document・html・body に付けたイベント(ページの往復で増えると、置き去りになっている)
  const persistent = (t) => t === window || t === document || t === document.documentElement || t === document.body;
  const key = (type, fn, capture) => `${type}|${capture ? 1 : 0}`;
  const records = live.listeners;   // key → Set(listener)
  const add = EventTarget.prototype.addEventListener, remove = EventTarget.prototype.removeEventListener;
  EventTarget.prototype.addEventListener = function (type, fn, opts) {
    if (persistent(this) && fn) {
      const capture = typeof opts === "boolean" ? opts : !!opts?.capture;
      const k = key(type, fn, capture);
      if (!records.has(k)) records.set(k, new Set());
      records.get(k).add(fn);
      if (opts && typeof opts === "object" && opts.signal) opts.signal.addEventListener("abort", () => records.get(k)?.delete(fn), { once: true });
    }
    return add.call(this, type, fn, opts);
  };
  EventTarget.prototype.removeEventListener = function (type, fn, opts) {
    if (persistent(this) && fn) records.get(key(type, fn, typeof opts === "boolean" ? opts : !!opts?.capture))?.delete(fn);
    return remove.call(this, type, fn, opts);
  };
  window.__counts = () => ({
    gl: live.gl.filter((r) => { const c = r.deref(); return c && !c.isContextLost(); }).length,
    ro: live.ro.size, io: live.io.size, intervals: live.intervals.size,
    ws: [...live.ws].filter((w) => w.readyState <= 1).length,
    listeners: [...live.listeners.values()].reduce((n, s) => n + s.size, 0),
    canvases: document.querySelectorAll("canvas").length,
    sceneCanvases: document.querySelectorAll("#site-scene").length,
  });
}

async function open(browser, opts = {}) {
  const page = await newPage(browser, opts);
  await page.context().addInitScript(instrument);
  return page;
}

/** 起動(MudBlazor のレイアウトが出る)まで待つ */
async function boot(page, path) {
  await page.goto(NEW + path, { waitUntil: "load" });
  await page.waitForSelector(".mud-layout", { timeout: 90000 });
}

async function metrics(page) {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send("HeapProfiler.collectGarbage");
  await cdp.send("Performance.enable");
  const { metrics: list } = await cdp.send("Performance.getMetrics");
  await cdp.detach();
  const m = Object.fromEntries(list.map((x) => [x.name, x.value]));
  return { nodes: m.Nodes, documents: m.Documents, listeners: m.JSEventListeners, heapMB: Math.round(m.JSHeapUsedSize / 1048576) };
}

/** 画面内の移動(再読み込みなし): サイドバーのリンクを押す */
async function go(page, href) {
  await page.locator(`.fsk-nav a[href="${href}"], .fsk-nav a[href="${href.replace(/^\//, "")}"]`).first().click();   // サイドバーの href は <base> に対する相対(learn/basics)
  await page.waitForFunction((h) => location.pathname + location.search === h || location.pathname === h.split("?")[0], href, { timeout: 30000 });
}

async function problems(page, label) {
  const v = await csp(page);
  ok(v.length === 0, `${label}: CSP 違反 0`, JSON.stringify(v));
  ok(page.problems.length === 0, `${label}: コンソールのエラー・警告 0`, page.problems.join(" | "));
}

// ---------------------------------------------------------------- 1. /practice の操作(基準と同じ結果)
async function practiceOps(browser, base, isNew) {
  const page = await open(browser, { width: 1280, height: 800 });
  await page.goto(base + "/practice?debug=1" + ENGINE_QUERY, { waitUntil: "load" });
  if (isNew) await page.waitForSelector(".mud-layout", { timeout: 90000 });
  await page.waitForSelector(".practice-viewport canvas", { timeout: 60000 });
  await page.waitForFunction(() => window.__practice?.objects?.length > 0);
  const vp = page.locator("#practice-viewport");
  const box = await vp.boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;
  await page.mouse.click(cx, cy - 12);   // 立方体をクリック(中央のやや上)
  const type = async (...keys) => { for (const k of keys) { await page.keyboard.press(k); await sleep(30); } };
  await type("g", "z", "2", "Enter");
  await type("r", "z", "4", "5", "Enter");
  await type("s", "2", "Enter");
  await page.keyboard.press("Shift+A");
  await page.locator(".practice-menu button", { hasText: "UV" }).first().click();
  await sleep(100);
  await page.mouse.click(cx, cy);          // (見た目の位置は環境で違うので、球の選択はデバッグの API で行う)
  await page.evaluate(() => { const vp = window.__practice; const s = vp.objects.find((o) => o.userData.kind === "sphere"); vp.select(s); });
  await vp.focus();
  await type("g", "x", "3", "Enter");
  await type("Control+z");
  await type("Numpad1");
  await page.evaluate(() => { document.getElementById("practice-emulate-numpad").click(); });
  await vp.focus();
  await type("5");
  const state = await page.evaluate(() => {
    const vp = window.__practice;
    const r = (n) => Math.round(n * 100) / 100;
    return {
      objects: vp.objects.map((o) => ({ kind: o.userData.kind, p: [o.position.x, o.position.y, o.position.z].map(r), r: [o.rotation.x, o.rotation.y, o.rotation.z].map(r), s: [o.scale.x, o.scale.y, o.scale.z].map(r) })),
      mode: vp.mode, ortho: vp.ortho, view: vp.view.name,
      done: [...document.querySelectorAll("#practice-tasks li.done")].length,
      current: document.querySelector("#practice-tasks li.current")?.textContent,
      tasks: document.querySelectorAll("#practice-tasks li").length,
    };
  });
  if (isNew) await problems(page, "/practice の操作");
  await page.context().close();
  return state;
}

// ---------------------------------------------------------------- 1b. /practice の HUD・キーの preventDefault・フォーカス(両エンジンで同じ)
// C# 版(?engine=cs。Phase 3b S3)は HUD を Razor で、キーの preventDefault とフォーカスを gl.js・Blazor で作るので、JS 版と同じに振る舞うことを、ここで確かめる
async function practiceHud(browser, base) {
  const page = await open(browser, { width: 1280, height: 800 });
  await page.goto(base + "/practice?debug=1" + ENGINE_QUERY, { waitUntil: "load" });
  await page.waitForSelector(".mud-layout", { timeout: 90000 });
  await page.waitForSelector(".practice-viewport canvas", { timeout: 60000 });
  await page.waitForFunction(() => window.__practice?.objects?.length > 0);
  const label = ENGINE === "cs" ? "C# 版" : "JS 版";
  const text = (sel) => page.locator(sel).first().textContent().then((t) => t?.trim());
  ok((await page.locator(".vp-mode, .vp-view, .vp-status, .vp-numeric, .vp-tools, .practice-npanel").count()) >= 6, `${label}: HUD の主要な要素（.vp-mode・.vp-view・.vp-status・.vp-numeric・.vp-tools・.practice-npanel）がある`);
  ok((await text(".vp-mode")) === "オブジェクトモード", `${label}: 最初の .vp-mode の文言`, await text(".vp-mode"));
  ok((await text(".vp-view")) === "ユーザー・透視投影", `${label}: 最初の .vp-view の文言`, await text(".vp-view"));
  ok((await text(".vp-status")) === "クリックして選択。G / R / S で動かす", `${label}: 最初の .vp-status の文言`, await text(".vp-status"));
  ok((await text(".vp-numeric")) === "", `${label}: 最初の .vp-numeric は空`, await text(".vp-numeric"));
  // キーの defaultPrevented(ページの window で、ビューのハンドラーのあとに見る)
  await page.evaluate(() => { window.__keys = []; window.addEventListener("keydown", (e) => window.__keys.push([e.key, e.defaultPrevented])); });
  const box = await page.locator("#practice-viewport").boundingBox();
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2 - 12);
  ok(await page.evaluate(() => document.activeElement?.id === "practice-viewport"), `${label}: ビューをクリックするとフォーカスがビューに来る`);
  await page.keyboard.press("g"); await sleep(60);
  await page.keyboard.press("z"); await page.keyboard.press("2"); await sleep(120);
  ok((await text(".vp-status")).startsWith("移動"), `${label}: G の .vp-status は「移動」`, await text(".vp-status"));
  ok((await text(".vp-numeric")) === "入力: 2", `${label}: 数値入力の .vp-numeric`, await text(".vp-numeric"));
  await page.keyboard.press("Escape"); await sleep(60);
  await page.keyboard.press("Tab"); await sleep(60);
  ok((await text(".vp-mode")) === "編集モード", `${label}: Tab で .vp-mode が「編集モード」`, await text(".vp-mode"));
  await page.keyboard.press("Control+z"); await sleep(60);
  await page.keyboard.press("a"); await sleep(60);
  const keys = await page.evaluate(() => window.__keys);
  const prevented = Object.fromEntries(["Tab", "z", "g"].map((k) => [k, keys.filter((x) => x[0].toLowerCase() === k.toLowerCase()).map((x) => x[1])]));
  ok(JSON.stringify(prevented.Tab) === "[true]", `${label}: Tab は defaultPrevented（フォーカスがビューから出ない）`, JSON.stringify(prevented.Tab));
  ok(prevented.z.length === 2 && prevented.z[1] === true, `${label}: Ctrl+Z は defaultPrevented（ブラウザの取り消しにしない）`, JSON.stringify(prevented.z));
  ok(await page.evaluate(() => document.activeElement?.id === "practice-viewport"), `${label}: キー操作のあともフォーカスがビューに残る`);
  await page.locator('[data-tool="stats"]').click(); await sleep(150);
  ok(await page.evaluate(() => document.activeElement?.id === "practice-viewport"), `${label}: ボタン（統計）を押したあと、フォーカスがビューに戻る`);
  await page.locator("#practice-reset").click(); await sleep(250);
  ok(await page.evaluate(() => document.activeElement?.id === "practice-viewport"), `${label}: 「最初からやり直す」を押したあと、フォーカスがビューに戻る`);
  ok((await text(".vp-mode")) === "オブジェクトモード", `${label}: やり直しで .vp-mode が戻る`, await text(".vp-mode"));
  await problems(page, `/practice の HUD（${label}）`);
  await page.context().close();
}

// ---------------------------------------------------------------- 2. /practice/unity の操作
async function unityOps(browser, base, isNew) {
  const page = await open(browser, { width: 1280, height: 900 });
  await page.goto(base + "/practice/unity?debug=1" + (isNew ? ENGINE_QUERY : ""), { waitUntil: "load" });   // ENGINE=cs: Unity 風の画面も C# 版(Phase 3b S5)
  if (isNew) await page.waitForSelector(".mud-layout", { timeout: 90000 });
  await page.waitForSelector(".unity-editor canvas", { timeout: 60000 });
  if (isNew && ENGINE === "cs") await page.waitForFunction(() => window.__unityCs?.nodes); else await page.waitForFunction(() => window.__unity?.findByName);
  // Project の Hoodie を、Hierarchy の Avatar へドラッグ
  const asset = page.locator('.ue-asset[data-asset]', { hasText: "Hoodie" }).first();
  const target = page.locator(".ue-scene-row, .ue-hier-list [data-node]", { hasText: "Avatar" }).first();
  const a = await asset.boundingBox(), t = await target.boundingBox();
  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
  await page.mouse.down();
  await page.mouse.move(t.x + 20, t.y + t.height / 2, { steps: 8 });
  await page.mouse.up();
  await sleep(200);
  const hoodie = isNew && ENGINE === "cs" ? await page.evaluate(() => window.__unityCs.nodes.some((n) => n.path === "Avatar/Hoodie")) : await page.evaluate(() => !!window.__unity.findByName("Avatar/Hoodie"));
  await page.keyboard.press("w");
  const state = await page.evaluate(() => ({
    nodes: [...document.querySelectorAll(".ue-hier-list [data-node]")].map((e) => e.textContent.trim().replace(/\s+/g, " ")),
    tasksDone: document.querySelectorAll("#unity-tasks li.done").length,
    tasks: document.querySelectorAll("#unity-tasks li").length,
  }));
  state.hoodie = hoodie;
  if (isNew) await problems(page, "/practice/unity の操作");
  await page.context().close();
  return state;
}

// ---------------------------------------------------------------- 3. 往復で増えない
async function roundTrips(browser) {
  const page = await open(browser, { width: 1280, height: 800 });
  await boot(page, "/learn");
  await page.evaluate(() => { window.__marker = "same-document"; });
  const series = [];
  const snapshot = async (label) => {
    await sleep(600);
    const c = await page.evaluate(() => window.__counts());
    const m = await metrics(page);
    series.push({ label, ...c, ...m });
    return { ...c, ...m };
  };
  const base = await snapshot("start");
  const rafRate = async () => { const a = await page.evaluate(() => window.__live.raf); await sleep(2000); return (await page.evaluate(() => window.__live.raf)) - a; };
  const rafStart = await rafRate();   // 往復の前の、/learn での描画のループの回数(2 秒)。背景の 3D だけが回っている
  // waitForSelector は要素の参照(ElementHandle)を返し、解放しないと外した DOM を保持して「節が増える」ように見える
  const visit = async (href, waitFor) => { if (ENGINE === "cs" && href === "/practice") await page.evaluate(() => window.Blazor.navigateTo("/practice?engine=cs")); else await go(page, href); await page.locator(waitFor).first().waitFor({ timeout: 60000 }); await sleep(300); };
  for (let i = 1; i <= ROUNDS; i++) {
    await visit("/practice", ".practice-viewport canvas");
    await visit("/learn", "h1");
    await visit("/practice/unity", ".unity-editor canvas");
    await visit("/learn", "h1");
    // ENGINE=cs: 部屋も C# 版で往復する（Phase 3b S6）。ナビのリンクに ?engine が無いので、Blazor の移動で開く
    if (ENGINE === "cs") { await page.evaluate(() => window.Blazor.navigateTo("/practice/room?engine=cs")); await page.locator("#room-create").waitFor({ timeout: 30000 }); }   // 画面内の移動（再読み込みしない）
    else await visit("/practice/room", "#room-create");
    await page.locator("#room-create").click();
    await page.locator("#room-viewport canvas").first().waitFor({ timeout: 30000 });
    await page.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 30000 });
    await visit("/learn", "h1");
    // 記事内チュートリアル(::: practice)。記事を開いて、埋め込みを画面に入れて 3D を始める
    if (ENGINE === "cs") await page.evaluate((url) => window.Blazor.navigateTo(url), ARTICLE + "?engine=cs");   // 記事のリンクは ?engine を引き継がない
    else {
      await go(page, "/learn/basics");
      await page.locator(`.fsk-content a[href="${ARTICLE}"]`).first().click();
    }
    await page.locator(".practice-embed.is-ready").first().waitFor({ timeout: 30000 });
    await page.locator(".practice-embed").first().scrollIntoViewIfNeeded();
    await page.locator(".practice-embed canvas").first().waitFor({ timeout: 60000 });
    await visit("/learn", "h1");
    if (i === 1 || i % 5 === 0) await snapshot(`round ${i}`);
  }
  const end = await snapshot("end");
  const marker = await page.evaluate(() => window.__marker);
  ok(marker === "same-document", "往復のあいだ、ページの再読み込みは起きていない(画面内の移動だけ)");
  console.log("     counts:");
  for (const s of series) console.log("       " + JSON.stringify(s));
  // 練習のページにいないとき(/learn)の数が、始めに対して増えていない
  for (const k of ["gl", "ro", "io", "intervals", "ws", "canvases", "sceneCanvases"]) ok(end[k] <= base[k], `往復 ${ROUNDS} 回のあと、${k} が増えていない`, `start=${base[k]} end=${end[k]}`);
  // イベントリスナーは、最初の 1 往復で、遅れて読み込むもの(C# 版の Blazor の入口など)が数個足されて、そのあとは増えない(往復ごとに増えていたら漏れ)
  const first = series.find((x) => x.label === "round 1") ?? series[1];
  ok(end.listeners <= first.listeners, `往復 ${ROUNDS} 回のあと、listeners が最初の 1 往復のあとから増えていない`, `round1=${first.listeners} end=${end.listeners}`);
  ok(end.listeners <= base.listeners + 4, `往復 ${ROUNDS} 回のあと、listeners が始めから数個より増えていない`, `start=${base.listeners} end=${end.listeners}`);
  ok(end.sceneCanvases === 1, "背景の 3D(#site-scene)は 1 つだけ");
  ok(end.gl === 0, "練習のページを離れたあと、動いている WebGL の context は 0", String(end.gl));
  ok(end.nodes <= base.nodes * 1.25, "DOM の節(Nodes)が増え続けていない", `start=${base.nodes} end=${end.nodes}`);
  const mid = series.find((x) => x.label === `round ${Math.floor(ROUNDS / 2 / 5) * 5 || 5}`) ?? series[1];
  ok(end.documents <= mid.documents, "Documents が増え続けていない(往復の半ばから終わりまで同じ)", `mid=${mid.documents} end=${end.documents}`);
  ok(end.listeners <= base.listeners * 1.15 + 20, "JS のイベントリスナーが増え続けていない", `start=${base.listeners} end=${end.listeners}`);
  // 描画のループ: /learn にいる間の requestAnimationFrame の回数(背景の 3D だけ。往復の前後で同じ程度)
  const after = await rafRate();
  ok(after <= rafStart * 1.6 + 5, "往復のあと、/learn での描画のループ(requestAnimationFrame)が増えていない", `往復の前 ${rafStart} 回 / 往復のあと ${after} 回(2 秒)`);
  await problems(page, `往復 ${ROUNDS} 回`);
  await page.context().close();
}

// ---------------------------------------------------------------- 4. 記事内チュートリアル
async function tutorial(browser) {
  const page = await open(browser, { width: 1280, height: 800 });
  await boot(page, ARTICLE + "?debug=1" + ENGINE_QUERY);   // ENGINE=cs: 記事内チュートリアルも C# 版(Components/TutorialEmbed.razor)
  await page.waitForSelector(".practice-embed.is-ready", { timeout: 30000 });
  ok(await page.locator("[data-progress-page]").getAttribute("data-progress-page") === "basics/transform", "記事の見出しに data-progress-page(完了の記録の印)がある");
  await page.locator(".practice-embed").first().scrollIntoViewIfNeeded();
  await page.waitForSelector(".practice-embed canvas", { timeout: 60000 });
  ok(await page.locator(".tut-step-no").count() > 0, "チュートリアルの手順の表示が出ている");
  // 画像で見る(PC でも切り替えられる)。画像は /images/tutorials/...
  await page.locator('[data-act="images"]').first().click();
  await page.waitForSelector(".tut-step-card img", { timeout: 15000 });
  const broken = await page.evaluate(async () => {
    const imgs = [...document.querySelectorAll(".tut-step-card img")];
    for (const i of imgs) i.loading = "eager";   // loading="lazy" は画面の外だと読まないので、確かめるために即時にする
    await Promise.all(imgs.map((i) => (i.complete ? 0 : new Promise((r) => { i.onload = i.onerror = r; setTimeout(r, 10000); }))));
    return imgs.filter((i) => i.naturalWidth === 0).length;
  });
  ok(broken === 0, "「画像で見る」の画像がすべて読める", String(broken));
  await page.locator('[data-act="images"]').first().click();   // 3D に戻す
  await sleep(500);
  ok((await page.evaluate(() => window.__counts().gl)) === 1, "3D ビューに戻すと、WebGL の context は 1 つ");
  // 別の記事(チュートリアルあり)へ: 前の記事の 3D は解放される
  await go(page, "/learn/basics");
  if (ENGINE === "cs") await page.evaluate((url) => window.Blazor.navigateTo(url), ARTICLE2 + "?engine=cs");   // サイドバー・記事内のリンクは ?engine を引き継がない
  else await page.locator(`.fsk-content a[href="${ARTICLE2}"]`).first().click();
  await page.waitForSelector(".practice-embed.is-ready", { timeout: 30000 });
  await page.locator(".practice-embed").first().scrollIntoViewIfNeeded();
  await page.waitForSelector(".practice-embed canvas", { timeout: 60000 });
  await sleep(500);
  ok((await page.evaluate(() => window.__counts().gl)) === 1, "別の記事へ移ったあと、WebGL の context は 1 つだけ(前の記事の分は解放された)");
  await problems(page, "記事内チュートリアル");
  await page.context().close();
}

// ---------------------------------------------------------------- 5. スマホ
async function phone(browser) {
  for (const path of ["/practice", "/practice/unity", "/practice/room"]) {
    const page = await open(browser, { width: 390, height: 844, mobile: true });
    await boot(page, path);
    await sleep(800);
    ok(await page.locator("#touch-practice").isVisible(), `${path}: スマホは「パソコンで開いてください」の案内を出す`);
    ok(await page.locator(".practice-pc-only").first().isHidden(), `${path}: PC 用の練習の部分は隠れている`);
    const gl = await page.evaluate(() => window.__counts().gl);
    ok(gl === 0, `${path}: スマホでは WebGL を使わない`, String(gl));
    if (path !== "/practice/room") {
      await page.locator(".touch-practice-item > summary").first().click();
      await page.waitForSelector(".touch-practice-item[open] .tut-step-card img", { timeout: 15000 });
      ok(await page.locator(".touch-practice-item[open] canvas").count() === 0, `${path}: 「画像で見る」で、3D ビューは作らない`);
    } else {
      ok(await page.locator(".touch-practice-links a").count() === 2, `${path}: 記事へのリンクが 2 つ`);
    }
    await problems(page, `${path} (スマホ)`);
    await page.context().close();
  }
}

// ---------------------------------------------------------------- 6. 共有ルーム(画面から二人)
async function room(browser) {
  const alice = await open(browser, { width: 1280, height: 800 });
  const bob = await open(browser, { width: 1280, height: 800 });
  await boot(alice, "/practice/room");
  await alice.locator("#room-create").click();
  await alice.waitForSelector("#room-viewport canvas", { timeout: 30000 });
  await alice.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 30000 });
  const url = alice.url();
  ok(/\/practice\/room\?room=[a-z0-9]{10}$/.test(url), "部屋を作ると ?room=<10 字> の URL になる(再読み込みなし)", url);
  await alice.locator("#room-name").fill("Alice");
  await sleep(900);
  await bob.goto(url + "&debug=1", { waitUntil: "load" });   // debug=1 で、部屋の状態(window.__room)を確かめられる
  await bob.waitForSelector(".mud-layout", { timeout: 90000 });
  await bob.waitForSelector("#room-viewport canvas", { timeout: 30000 });
  await bob.waitForFunction(() => document.querySelector("#room-status")?.textContent.includes("接続しました"), null, { timeout: 30000 });
  await bob.locator("#room-name").fill("Bob");
  await sleep(900);
  await alice.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 15000 });
  await bob.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 2, null, { timeout: 15000 });
  ok(true, "二人が同じ部屋に入り、どちらの画面にも「参加している人」が 2 人出る");
  const names = await alice.locator("#room-peers .peer-name").allTextContents();
  ok(names.some((n) => n.includes("Bob")) && names.some((n) => n.includes("Alice")), "表示名(Alice・Bob)が相手に見える", names.join(","));
  // Alice が Shift+A で球を追加 → Bob に届く
  const vp = alice.locator("#room-viewport");
  await vp.click({ position: { x: 300, y: 200 } });
  await alice.keyboard.press("Shift+A");
  await alice.locator(".practice-menu button", { hasText: "UV" }).first().click();
  await bob.waitForFunction(() => window.__room?.vp?.objects?.some((o) => o.userData.kind === "sphere"), null, { timeout: 10000 }).catch(() => { });
  const bobSees = await bob.evaluate(() => window.__room?.vp?.objects?.map((o) => o.userData.kind));
  ok(bobSees?.includes("sphere"), "Alice の追加(球)が Bob の画面に届く", JSON.stringify(bobSees));
  // Bob が退室(別のページへ)すると、Alice の一覧から消える
  await go(bob, "/learn");
  await alice.waitForFunction(() => document.querySelectorAll("#room-peers li").length === 1, null, { timeout: 15000 });
  ok(true, "Bob がページを離れると、Alice の一覧から消える(退室を知らせた)");
  await problems(alice, "共有ルーム(Alice)");
  await problems(bob, "共有ルーム(Bob)");
  await alice.context().close();
  await bob.context().close();
}

// ONLY=ops,tutorial,phone,room,trips のように、一部だけ動かせる(既定は全部)
const only = new Set((process.env.ONLY || "ops,tutorial,phone,room,trips").split(","));
const browser = await launch();
try {
  if (only.has("ops")) {
    const n = await practiceOps(browser, NEW, true);
    console.log("     NEW:", JSON.stringify(n));
    ok(JSON.stringify(n) === JSON.stringify(BASELINE.practice), "/practice: 同じ操作の結果(オブジェクトの位置・回転・スケール・視点・お題の進み)が基準(practice-baseline.json)と一致する", JSON.stringify(BASELINE.practice));
    await practiceHud(browser, NEW);
    const nu = await unityOps(browser, NEW, true);
    console.log("     NEW:", JSON.stringify(nu));
    ok(nu.hoodie, "/practice/unity: Hoodie を Avatar へドラッグして入れられる");
    ok(JSON.stringify(nu) === JSON.stringify(BASELINE.unity), "/practice/unity: 同じ操作の結果(階層・お題)が基準と一致する", JSON.stringify(BASELINE.unity));
  }
  if (only.has("tutorial")) await tutorial(browser);
  if (only.has("phone")) await phone(browser);
  if (only.has("room")) await room(browser);
  if (only.has("trips")) await roundTrips(browser);
} finally {
  await browser.close();
}
console.log(failed ? `\n${failed} 件 NG` : "\nすべて ok");
process.exit(failed ? 1 : 0);
