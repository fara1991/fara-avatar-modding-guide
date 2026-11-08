// 記事内チュートリアルの比較(tutorial-compare.mjs・tutorial-input-compare.mjs)の共通部品: 記事を開く・文字を集める・埋め込みの内側の画像・画素の差
import { PNG } from "pngjs";
import { newPage } from "./lib.mjs";

export const NEW = process.env.NEW || "http://127.0.0.1:8787";
export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

export const ARTICLES = {
  "navigation-mouse": "/learn/getting-started/navigation",
  "navigation-numpad": "/learn/getting-started/navigation",
  select: "/learn/basics/select",
  transform: "/learn/basics/transform",
  "edit-mode": "/learn/modeling/edit-mode",
  "extrude-inset-loopcut": "/learn/modeling/extrude-inset-loopcut",
  "bevel-merge": "/learn/modeling/bevel-knife-merge",
  mirror: "/learn/modeling/mirror-smooth",
  topology: "/learn/modeling/topology",
  "unity-basics": "/learn/unity/editor-overview",
  "ma-outfit": "/learn/unity/modular-avatar-outfit",
};

export const UNITY = ["unity-basics", "ma-outfit"];

export async function open(browser, name, cs) {
  const page = await newPage(browser, { width: 1280, height: 900 });
  if (process.env.MOTION !== "1") await page.emulateMedia({ reducedMotion: "reduce" });   // MOTION=1: 動きを減らす設定にしない(.ue-hl の枠のアニメーション付きで比べる)。CSS のアニメーション(マーカーの上下など)は、始まった時刻で位相が決まるので止める(同じ CSS なので、止めても比べる意味は変わらない)
  await page.context().addInitScript(() => { performance.now = () => 1000; });   // 点線の点滅を止める
  await page.addInitScript(() => { const st = document.createElement("style"); st.textContent = "#site-scene{display:none !important}"; document.addEventListener("DOMContentLoaded", () => document.head.appendChild(st)); });   // 背景の 3D(動く)が、半透明の部分に透けるので隠す
  // Unity 風の画面は、?debug=1 で状態(JS 版は window.__unity、C# 版は window.__unityCs)を読める(実入力の比較が、ギズモの位置・状態の比べに使う)
  const query = [UNITY.includes(name) ? "debug=1" : null, `engine=${cs ? "cs" : "js"}`].filter(Boolean).join("&");
  await page.goto(`${NEW}${ARTICLES[name]}${query ? "?" + query : ""}`, { waitUntil: "load" });
  await page.waitForSelector(".mud-layout", { timeout: 90000 });
  const embed = page.locator(`.practice-embed[data-scenario="${name}"]`);
  await embed.waitFor({ timeout: 30000 });
  await page.waitForSelector(`.practice-embed[data-scenario="${name}"].is-ready`, { timeout: 30000 });
  await embed.scrollIntoViewIfNeeded();
  await embed.locator("canvas").first().waitFor({ timeout: 60000 });
  await sleep(1200);
  return { page, embed };
}

export async function texts(embed) {
  return embed.evaluate((el) => {
    const t = (sel) => el.querySelector(sel)?.innerText?.trim() ?? null;
    return {
      count: t(".tut-count"), bubble: t(".tut-bubble .tut-text"), keys: el.querySelector(".tut-keys")?.hidden ? null : t(".tut-keys"),
      msg: el.querySelector(".tut-msg")?.hidden ? null : t(".tut-msg"), mode: t(".vp-mode"), view: t(".vp-view"), status: t(".vp-status"), numeric: t(".vp-numeric"),
      stats: el.querySelector(".vp-stats")?.hidden ? null : t(".vp-stats"), complete: el.querySelector(".tut-complete")?.hidden ? null : t(".tut-complete"),
      overlayHidden: el.querySelector(".tut-overlay")?.hidden, marker: el.querySelector(".vp-marker")?.hidden ? null : t(".vp-marker"),
      highlightedTools: [...el.querySelectorAll(".vp-tools .is-highlight")].map((b) => b.dataset.tool), viewHighlight: !!el.querySelector(".vp-view.is-highlight"),
      focused: el.classList.contains("is-focused"), toolsOn: [...el.querySelectorAll(".vp-tools .is-on")].map((b) => b.dataset.tool),
      progress: el.querySelector(".tut-progress span")?.style.width,
      ueStatus: t(".ue-status"), ueInfo: t(".ue-scene-info"), ueLabel: el.querySelector(".ue-hl-label")?.hidden ? null : t(".ue-hl-label"), ueTools: [...el.querySelectorAll(".ue-toolbar .is-on")].map((b) => b.dataset.tool),
    };
  });
}

/** 埋め込みの内側(枠から 10 ピクセル内側)のスクリーンショット。外側は、ページの背景(動く)が透けるので含めない */
export async function shot(page, embed) {
  const box = await embed.boundingBox();
  const inset = 10;
  return page.screenshot({ clip: { x: box.x + inset, y: box.y + inset, width: box.width - inset * 2, height: box.height - inset * 2 } });
}

/** 画素の差。ignore は無視する長方形(画像の座標)。GPU_TOLERANT=1 のとき、案内の枠(.ue-hl)の中だけを、DOM の文字が一致していることを条件に無視するのに使う */
export function pixelDiff(a, b, ignore = []) {
  const diff = new PNG({ width: a.width, height: a.height });
  let over = 0, max = 0, ignored = 0;
  const inIgnored = (x, y) => ignore.some((r) => x >= r.x - 1 && x <= r.x + r.w + 1 && y >= r.y - 1 && y <= r.y + r.h + 1);
  for (let i = 0; i < a.data.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(a.data[i + c] - b.data[i + c]));
    if (d > 8 && ignore.length && inIgnored((i / 4) % a.width, Math.floor(i / 4 / a.width))) { ignored++; d = 0; }
    max = Math.max(max, d);
    if (d > 8) over++;
    diff.data[i] = diff.data[i + 1] = diff.data[i + 2] = Math.min(255, d * 8); diff.data[i + 3] = 255;
  }
  return { diff, over: (over / (a.width * a.height)) * 100, max, ignored };
}

/** WebGL の描画が、ソフトウェア(SwiftShader)か、実機の GPU か。画素の厳しい比較(0 % / 8)は SwiftShader が前提(README) */
export const rendererOf = (page) => page.evaluate(() => { const g = document.createElement("canvas").getContext("webgl2"); const e = g?.getExtension("WEBGL_debug_renderer_info"); return e ? g.getParameter(e.UNMASKED_RENDERER_WEBGL) : "unknown"; });

/** GPU_TOLERANT=1: 案内の枠(.ue-hl)と吹き出しの中は、画素の差を無視する(実 GPU では、枠のある行の文字のアンチエイリアスが JS 版と違うことがある)。DOM の文字は別に比べる */
export const GPU_TOLERANT = process.env.GPU_TOLERANT === "1";
export const guideRects = (embed) => embed.evaluate((el) => {
  const box = el.getBoundingClientRect();
  return [...el.querySelectorAll(".ue-hl, .ue-hl-label:not([hidden])")].map((e) => { const r = e.getBoundingClientRect(); return { x: r.left - box.left - 10, y: r.top - box.top - 10, w: r.width, h: r.height }; });
});

/** Unity の画面の、ビルドの表示(ビルドしています… → 完了)が落ち着くまで待つ(表示が変わる 0.9 秒の境目で撮ると、片方だけ変わっている) */
export const settleBuild = (embed) => embed.evaluate((el) => new Promise((res) => { const tick = () => { const pv = el.querySelector(".ue-preview"); if (!pv || pv.hidden || !pv.querySelector(".ue-progress")) res(); else setTimeout(tick, 30); }; tick(); setTimeout(res, 3000); }));

/** /api/maintenance の requestfailed は、kit の MaintenanceInfo が 2 秒で打ち切る(仕様)ので、配信が重いと出る。csp-check.mjs と同じに、問題として数えない */
export const realProblems = (page) => page.problems.filter((m) => !/Failed to load resource/.test(m) && !(m.startsWith("requestfailed:") && m.includes("/api/maintenance")));

/** 要素の DOM の骨格(タグ・クラス・data-*・aria-*・hidden・disabled・type・href)。page.evaluate に渡す(tutorial-dom.mjs・unity-compare.mjs) */
export const skeleton = (el) => {
  const out = [];
  const walk = (e, d) => {
    // C# 版は、canvas を Blazor の外の空の div(.practice-gl)に入れる(Blazor の子の数え方が狂わないように)。canvas だけを見て、空の div は無いものとする
    if (e.classList?.contains("practice-gl")) { for (const c of e.children) walk(c, d); return; }
    // Unity 風の画面(C# 版): ドラッグの影を入れる空の div と、Scene の上の操作を無視する印(Razor の作り)は、JS 版に無いものとする。閉じた SDK の画面の古い中身も見ない
    if (e.classList?.contains("ue-ghost-host")) return;
    if (e.tagName === "CANVAS") { out.push(`${" ".repeat(d)}[gl]`); return; }
    // 入力欄の値・チェック・選択は、属性ではなく現在の値(JS 版は innerHTML の属性、Razor は DOM のプロパティで持つ。見えるのは現在の値)
    const live = e.tagName === "INPUT" && e.type === "checkbox" ? [`checked=${e.checked}`] : e.tagName === "INPUT" ? [`value=${e.value}`] : e.tagName === "OPTION" ? [`selected=${e.selected}`] : [];
    const attrs = [...e.attributes].filter((a) => !(a.name === "class" && !e.classList.length)).filter((a) => !["style", "id", "data-cs", "data-i", "data-gl-ignore", "checked", "value", "selected"].includes(a.name) && !(a.name.startsWith("_bl_") || a.name.startsWith("blazor"))).map((a) => `${a.name}=${a.name === "class" ? [...e.classList].sort().join(" ") : a.value}`).concat(live).sort();
    out.push(`${" ".repeat(d)}<${e.tagName.toLowerCase()} ${attrs.join(" ")}>${[...e.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent).join("").replace(/\s+/g, " ").trim().slice(0, 40)}`);   // 子の要素のあいだの文字も見る
    if ((e.classList?.contains("ue-sdk") || e.classList?.contains("ue-preview")) && e.hidden) return;
    for (const c of e.children) walk(c, d + 1);
  };
  walk(el, 0);
  return out;
};
