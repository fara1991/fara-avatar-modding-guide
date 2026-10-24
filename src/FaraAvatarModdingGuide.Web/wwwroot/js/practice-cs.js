// /practice・記事内チュートリアルの 3D の C# 版(?engine=cs。Services/PracticeCs.cs・TutorialController.cs・Components/TutorialEmbed.razor)の、JavaScript 側の小さな補助。
//   focusRoot(id)       … ビューにフォーカスを戻す(ボタンを押したあと)
//   exposeDebug / setDebugState / clearDebug … ?debug=1 のとき、window.__practice(JS 版の viewport.js のインスタンスと同じ形の、状態の読み取り口)を作る。
//     tools/verify/practice.mjs が、両方のエンジンで同じ読み方(objects[].userData.kind / position / rotation / scale、mode、ortho、view.name、select(o))をする
//   mount(container)    … 記事内チュートリアル: container の中の .practice-embed[data-scenario] のうち、C# 版にあるものに Blazor のルートコンポーネント(tutorial-embed)を置く。返した dispose() で外す
//   registerEmbed など  … 埋め込み 1 つの、画面に入ったか・大きさが変わったか・画像が読めなかったか・画像のカードをめくったかの観察(DOM を測るのは JavaScript の役目)

export function focusRoot(id) {
  document.getElementById(id)?.focus({ preventScroll: true });
}

let selectByIndex = null;

export function exposeDebug(select) {
  selectByIndex = select;
  window.__practice = {
    objects: [], mode: "object", ortho: false, view: { name: "" },
    select(o) { if (o && typeof o.__index === "number") selectByIndex(o.__index); },
  };
}

export function setDebugState(json) {
  const p = window.__practice;
  if (!p) return;
  const s = JSON.parse(json);
  p.objects = s.objects.map((o, i) => ({
    __index: i, userData: { kind: o.kind },
    position: { x: o.p[0], y: o.p[1], z: o.p[2] }, rotation: { x: o.r[0], y: o.r[1], z: o.r[2] }, scale: { x: o.s[0], y: o.s[1], z: o.s[2] },
  }));
  p.mode = s.mode;
  p.ortho = s.ortho;
  p.view = { name: s.view };
}

export function clearDebug() {
  delete window.__practice;
  selectByIndex = null;
}

/** メニューを、ビューからはみ出さない位置に直して見せる(viewport.js の showMenu と同じ式: 実際の大きさ。下はステータスバーの分(30px)を空ける) */
export function positionMenu(rootId, x, y) {
  const root = document.getElementById(rootId);
  const menu = root?.querySelector(".practice-menu");
  if (!menu) return;
  menu.style.left = `${Math.max(0, Math.min(x, root.clientWidth - menu.offsetWidth - 4))}px`;
  menu.style.top = `${Math.max(0, Math.min(y, root.clientHeight - menu.offsetHeight - 30))}px`;
  menu.style.visibility = "";
}

// ---------------------------------------------------------------- 記事内チュートリアル
/** C# 版のチュートリアルの名前(Practice の TutorialScenarios.All のキー。Practice.Tests が同じであることを確かめる)。ここに無いもの(Unity)は、今までの tutorial.js が動かす */
export const CS_SCENARIOS = ["navigation-mouse", "navigation-numpad", "select", "transform", "edit-mode", "extrude-inset-loopcut", "bevel-merge", "mirror", "topology"];

const DONE_KEY = "avatar-modding-guide.tutorials"; // { シナリオ名: 完了日時 }(tutorial.js と同じ)

function loadDone() {
  try {
    const v = JSON.parse(localStorage.getItem(DONE_KEY) || "{}");
    return v && typeof v === "object" && !Array.isArray(v) ? v : {};
  } catch { return {}; }
}

/** 完了を記録し、ページ内のチュートリアルがすべて終わっていればページを完了にして、完了の画面に出す文を返す(tutorial.js の finish と同じ) */
export function finishTutorial(name) {
  const done = loadDone();
  done[name] = new Date().toISOString();
  try { localStorage.setItem(DONE_KEY, JSON.stringify(done)); } catch { /* 保存できなくても続ける */ }
  const page = document.querySelector("[data-progress-page]")?.dataset.progressPage;
  const all = [...document.querySelectorAll(".practice-embed[data-scenario]")].every(el => done[el.dataset.scenario]);
  if (page && all && window.faraProgress) {
    const was = window.faraProgress.isDone(page);
    window.faraProgress.set(page, true);
    return was ? "このページは完了済みです。" : "このページを「完了」にしました。";
  }
  return all ? "" : "このページには、ほかにもチュートリアルがあります。";
}

export function isTouchOnly() {
  return matchMedia("(hover: none) and (pointer: coarse)").matches;
}

export function setHostClass(hostId, cls, on) {
  document.getElementById(hostId)?.classList.toggle(cls, on);
}

/** CSS のアニメーションを最初からやり直す(クラスを外して、レイアウトを確定させて、付け直す) */
export function restartClass(selector, hostId, cls) {
  const el = document.getElementById(hostId)?.querySelector(selector);
  if (!el) return;
  el.classList.remove(cls);
  void el.offsetWidth;
  el.classList.add(cls);
}

/** 吹き出しの高さを、このチュートリアルで一番長い手順(と完了の文)に合わせて固定する(手順で行数が変わっても、下のビューが動かないように) */
export function fitBubble(hostId, variants) {
  const host = document.getElementById(hostId);
  const bubble = host?.querySelector(".tut-bubble");
  if (!bubble || !bubble.offsetParent) return; // 画像で見ているあいだは非表示(測れない)
  const clone = bubble.cloneNode(true);
  clone.removeAttribute("id");
  clone.style.cssText = `position:absolute;visibility:hidden;pointer-events:none;left:0;top:0;min-height:0;width:${bubble.getBoundingClientRect().width}px`;
  bubble.parentNode.insertBefore(clone, bubble);
  const text = clone.querySelector(".tut-text");
  let max = 0;
  for (const v of variants) { text.innerHTML = v; max = Math.max(max, clone.getBoundingClientRect().height); }
  clone.remove();
  bubble.style.minHeight = `${Math.ceil(max)}px`;
}

export function scrollToCard(hostId, i, smooth) {
  const t = document.getElementById(hostId)?.querySelector(".tut-steps-track");
  if (t) t.scrollTo({ left: i * t.clientWidth, behavior: smooth ? "smooth" : "auto" });
}

const observers = new Map();

/** 埋め込み 1 つの観察を始める。画面に入ったら ref.OnVisible(bool)、画像が読めなかったら ref.OnImageError(i)、カードをめくったら ref.OnCardScrolled(i) を呼ぶ */
export function registerEmbed(hostId, ref, variants) {
  unregisterEmbed(hostId);
  const host = document.getElementById(hostId);
  if (!host) return;
  const abort = new AbortController();
  const { signal } = abort;
  const io = new IntersectionObserver(entries => {
    const e = entries[entries.length - 1];
    ref.invokeMethodAsync("OnVisible", e.isIntersecting);
  }, { rootMargin: "200px 0px" });
  io.observe(host);
  let lastW = host.clientWidth;
  const ro = new ResizeObserver(() => { if (host.clientWidth !== lastW) { lastW = host.clientWidth; fitBubble(hostId, variants); } });
  ro.observe(host);
  document.fonts?.ready.then(() => { if (!abort.signal.aborted) fitBubble(hostId, variants); });
  // 画像が無いときは「準備中」の表示にする(error は伝わらないので、捕まえる側で待つ)
  host.addEventListener("error", e => {
    const card = e.target instanceof HTMLImageElement ? e.target.closest(".tut-step-card") : null;
    if (card) ref.invokeMethodAsync("OnImageError", Number(card.dataset.i));
  }, { capture: true, signal });
  const track = host.querySelector(".tut-steps-track");
  track?.addEventListener("scroll", () => {
    const w = track.clientWidth || 1;
    ref.invokeMethodAsync("OnCardScrolled", Math.round(track.scrollLeft / w));
  }, { passive: true, signal });
  observers.set(hostId, { io, ro, abort });
  fitBubble(hostId, variants);
}

export function unregisterEmbed(hostId) {
  const o = observers.get(hostId);
  if (!o) return;
  o.io.disconnect();
  o.ro.disconnect();
  o.abort.abort();
  observers.delete(hostId);
}

let nextHost = 1;

/**
 * container の中の .practice-embed[data-scenario] のうち、C# 版にあるものに、Blazor のルートコンポーネント(tutorial-embed)を置く。
 * Blazor は要素の中身(::: practice の中に書いた文章)を置き換えるので、文章は先に取り出して、パラメーターで渡す。返した dispose() を、記事を離れるとき・記事が替わるときに呼ぶ。
 */
export async function mount(container = document) {
  const mounted = [];
  for (const el of container.querySelectorAll(".practice-embed[data-scenario]:not([data-cs])")) {
    if (!CS_SCENARIOS.includes(el.dataset.scenario)) continue;
    el.dataset.cs = "1";
    el.dataset.engine = "blender";   // tutorial.js と同じ印(CSS は unity だけ見る)
    if (!el.id) el.id = `tut-host-${nextHost++}`;
    const intro = el.innerHTML.trim();
    const component = await window.Blazor.rootComponents.add(el, "tutorial-embed", { scenario: el.dataset.scenario, intro, hostId: el.id, debug: new URLSearchParams(location.search).get("debug") === "1" });
    mounted.push({ el, component });
  }
  return {
    async dispose() {
      for (const m of mounted) {
        try { await m.component.dispose(); } catch { /* すでに外れている */ }
        unregisterEmbed(m.el.id);
        delete m.el.dataset.cs;
      }
      mounted.length = 0;
    },
  };
}
