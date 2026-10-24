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
};

export async function open(browser, name, cs) {
  const page = await newPage(browser, { width: 1280, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });   // CSS のアニメーション(マーカーの上下など)は、始まった時刻で位相が決まるので止める(同じ CSS なので、止めても比べる意味は変わらない)
  await page.context().addInitScript(() => { performance.now = () => 1000; });   // 点線の点滅を止める
  await page.addInitScript(() => { const st = document.createElement("style"); st.textContent = "#site-scene{display:none !important}"; document.addEventListener("DOMContentLoaded", () => document.head.appendChild(st)); });   // 背景の 3D(動く)が、半透明の部分に透けるので隠す
  await page.goto(`${NEW}${ARTICLES[name]}${cs ? "?engine=cs" : ""}`, { waitUntil: "load" });
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
    };
  });
}

/** 埋め込みの内側(枠から 10 ピクセル内側)のスクリーンショット。外側は、ページの背景(動く)が透けるので含めない */
export async function shot(page, embed) {
  const box = await embed.boundingBox();
  const inset = 10;
  return page.screenshot({ clip: { x: box.x + inset, y: box.y + inset, width: box.width - inset * 2, height: box.height - inset * 2 } });
}

export function pixelDiff(a, b) {
  const diff = new PNG({ width: a.width, height: a.height });
  let over = 0, max = 0;
  for (let i = 0; i < a.data.length; i += 4) {
    let d = 0;
    for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(a.data[i + c] - b.data[i + c]));
    max = Math.max(max, d);
    if (d > 8) over++;
    diff.data[i] = diff.data[i + 1] = diff.data[i + 2] = Math.min(255, d * 8); diff.data[i + 3] = 255;
  }
  return { diff, over: (over / (a.width * a.height)) * 100, max };
}


/** /api/maintenance の requestfailed は、kit の MaintenanceInfo が 2 秒で打ち切る(仕様)ので、配信が重いと出る。csp-check.mjs と同じに、問題として数えない */
export const realProblems = (page) => page.problems.filter((m) => !/Failed to load resource/.test(m) && !(m.startsWith("requestfailed:") && m.includes("/api/maintenance")));
