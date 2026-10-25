// 記事内チュートリアルの「画像で見る」（手順ごとの画像を横にめくるカード）。Markdown の「::: practice <名前>」から生成された .practice-embed に出す。
// 3D ビューは C# 版（practice-cs.js・Services/TutorialController.cs・Components/TutorialEmbed.razor）だけが動かす（Phase 3b S7）。
// この版は、3D のための読み込み（three.js・viewport.js・unity.js）を持たない。タッチだけの端末（スマートフォンなど）の案内も、この画像を見せる。
// 旧版（three.js の 3D ビューと手順の進行）は commit 8262089 の wwwroot/js/tutorial.js にある。
//
// - mount(container) で、その中の .practice-embed に始め、返った dispose() で後始末をする（記事を離れるとき・記事が替わるとき）
// - 画像が無いときの「画像は準備中です」は、HTML の onerror 属性(CSP の script-src で止まる)ではなく、イベントの待ち受けで差し替える
import { SCENARIOS } from "./scenarios.js";

const DONE_KEY = "avatar-modding-guide.tutorials"; // { シナリオ名: 完了日時 }
const touchOnly = matchMedia("(hover: none) and (pointer: coarse)").matches;
const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)");
const tutorials = new Map();

function loadDone() {
  try {
    const v = JSON.parse(localStorage.getItem(DONE_KEY) || "{}");
    return v && typeof v === "object" && !Array.isArray(v) ? v : {};
  } catch { return {}; }
}

function saveDone(name) {
  const done = loadDone();
  done[name] = new Date().toISOString();
  try { localStorage.setItem(DONE_KEY, JSON.stringify(done)); } catch { /* 保存できなくても続ける */ }
}

function escapeHtml(s) {
  return s.replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
}

/** 操作できなかったとき（opfail）の、手順に専用のヒントがない場合の案内 */
function opfailHint(e, vp) {
  if (e.reason === "objectmode") return "<kbd>Tab</kbd> で編集モードに入ってください。";
  if (e.reason === "noselection") return vp?.mode === "edit" ? "先に頂点・辺・面を選択してください。" : "先にオブジェクトをクリックして選択してください。";
  return e.reason; // ベベルなど、理由の文章がそのまま入っている
}

/** HTML の案内文から、画像の代替テキスト用の文字だけを取り出す */
function plainText(html) {
  const d = document.createElement("div");
  d.innerHTML = html;
  return d.textContent;
}

/** 手順 i（0 始まり）の画像の URL */
export function stepImage(name, i) {
  return `/images/tutorials/${name}/${String(i + 1).padStart(2, "0")}.webp`;
}

/** "Shift+A" → <kbd>Shift</kbd>+<kbd>A</kbd> */
function keyHtml(combo) {
  return combo.split("+").map(k => `<kbd data-key="${escapeHtml(k)}">${escapeHtml(k)}</kbd>`).join("+");
}

/** 画面のキー表示 label が、押されたキー e に当たるか */
function keyMatches(label, e) {
  const l = label.toLowerCase();
  if (l === "shift") return e.code.startsWith("Shift");
  if (l === "ctrl") return e.code.startsWith("Control") || e.code.startsWith("Meta");
  if (l === "alt") return e.code.startsWith("Alt");
  if (l === "enter") return e.code === "Enter" || e.code === "NumpadEnter";
  if (l === "esc") return e.code === "Escape";
  if (l === "home") return e.code === "Home";
  if (l === "tab") return e.code === "Tab";
  if (l.startsWith("numpad ")) {
    const k = l.slice(7);
    return k === "." ? e.code === "NumpadDecimal" || e.key === "." : e.code === `Numpad${k}` || e.code === `Digit${k}`;
  }
  if (/^[a-z]$/.test(l)) return e.code === `Key${label.toUpperCase()}`;
  if (/^[0-9]$/.test(l)) return e.code === `Digit${l}` || e.code === `Numpad${l}`;
  if (l === ".") return e.key === "." || e.code === "NumpadDecimal";
  if (l === "-") return e.key === "-" || e.code === "NumpadSubtract";
  return false;
}

class Tutorial {
  constructor(el) {
    this.el = el;
    this.name = el.dataset.scenario;
    this.scenario = SCENARIOS[this.name];
    this.vp = null;
    this.visible = false;
    this.disposed = false;
    this.abort = new AbortController();
    this.index = 0;
    this.ctx = {};
    this.finished = false;
    if (!this.scenario) {
      el.classList.add("is-ready");
      el.innerHTML = `<div class="tut-error">チュートリアル「${escapeHtml(this.name)}」が見つかりません（wwwroot/js/scenarios.js）。</div>`;
      return;
    }
    this.unity = this.scenario.engine === "unity";
    el.dataset.engine = this.unity ? "unity" : "blender";
    this.build();
    this.renderStep();
    this.setImageMode();
  }

  build() {
    const s = this.scenario;
    const intro = this.el.innerHTML.trim(); // ::: practice の中に書いた文章
    this.el.classList.add("is-ready");
    this.el.innerHTML = `
      <div class="tut-head">
        <span class="tut-label"><i class="bi bi-controller" aria-hidden="true"></i> やってみよう</span>
        <span class="tut-title">${escapeHtml(s.title)}</span>
        <span class="tut-count"></span>
      </div>
      <div class="tut-progress" aria-hidden="true"><span></span></div>
      ${intro ? `<div class="tut-intro">${intro}</div>` : ""}
      ${this.stepsHtml()}
      <div class="tut-bubble" aria-live="polite">
        <div class="tut-text"></div>
      </div>
      <div class="tut-stage">
        <div class="tut-msg" hidden role="status"></div>
        <div class="tut-vp ${this.unity ? "" : "practice-viewport"}"></div>
        <div class="tut-keys" aria-hidden="true"></div>
        <div class="tut-ok" aria-hidden="true"><i class="bi bi-check-lg"></i></div>
        <button type="button" class="tut-overlay"><span><i class="bi bi-play-fill" aria-hidden="true"></i> <span class="tut-overlay-text">クリックして始める</span></span></button>
        <div class="tut-complete" hidden>
          <div class="tut-complete-title"><i class="bi bi-trophy" aria-hidden="true"></i> すべてできました</div>
          <div class="tut-complete-text"></div>
          <div class="d-flex flex-wrap justify-content-center gap-2 mt-3">
            <button type="button" class="btn btn-sm btn-light" data-act="restart"><i class="bi bi-arrow-repeat" aria-hidden="true"></i> もう一度</button>
            <a class="btn btn-sm btn-outline-light" href="/practice"><i class="bi bi-controller" aria-hidden="true"></i> 自由に試す</a>
          </div>
        </div>
      </div>
      <div class="tut-toolbar">
        <button type="button" class="btn btn-sm btn-outline-secondary tut-mode-btn" data-act="images" aria-pressed="false"></button>
        <button type="button" class="btn btn-sm btn-outline-primary" data-act="retry"><i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i> やり直す</button>
        <button type="button" class="btn btn-sm btn-outline-secondary" data-act="skip">次へ（スキップ）<i class="bi bi-skip-forward-fill ms-1" aria-hidden="true"></i></button>
        <button type="button" class="btn btn-sm btn-link" data-act="restart">最初から</button>
        ${s.numpad ? `<div class="form-check form-check-inline mb-0 ms-1">
          <input class="form-check-input" type="checkbox" id="tut-numpad-${this.name}" data-act="numpad" />
          <label class="form-check-label small" for="tut-numpad-${this.name}">テンキーを模倣（数字キーで視点を切り替え）</label>
        </div>` : ""}
        <span class="tut-tip small text-body-secondary ms-auto">ビューをクリックしてからキーを押します</span>
      </div>`;
    const q = sel => this.el.querySelector(sel);
    this.ui = {
      count: q(".tut-count"), bar: q(".tut-progress span"), text: q(".tut-bubble .tut-text"), msg: q(".tut-msg"), bubble: q(".tut-bubble"),
      stage: q(".tut-stage"), vp: q(".tut-vp"), keys: q(".tut-keys"), ok: q(".tut-ok"),
      overlay: q(".tut-overlay"), overlayText: q(".tut-overlay-text"), complete: q(".tut-complete"), completeText: q(".tut-complete-text"),
      numpad: q('[data-act="numpad"]'), modeBtn: q('[data-act="images"]'),
      track: q(".tut-steps-track"), stepCount: q(".tut-steps-count"),
    };

    const signal = this.abort.signal;
    // 画像が無いときは「準備中」の表示に差し替える(error は伝わらないので、捕まえる側で待つ)
    this.el.addEventListener("error", e => {
      if (e.target instanceof HTMLImageElement && e.target.closest(".tut-step-card")) {
        e.target.replaceWith(Object.assign(document.createElement("div"), { className: "tut-step-noimg", textContent: "画像は準備中です" }));
      }
    }, { capture: true, signal });
    this.el.addEventListener("click", e => {
      const act = e.target.closest("[data-act]")?.dataset.act;
      if (act === "prev" || act === "next") { this.goCard(this.card + (act === "next" ? 1 : -1)); return; }
      if (act === "retry") this.retry();
      else if (act === "skip") this.skip();
      else if (act === "restart") this.restart();
      else return;
      this.focusViewport();
    }, { signal });
    this.ui.numpad?.addEventListener("change", () => this.focusViewport(), { signal });
    // ビュー以外にフォーカスがあるとキーが届かないので、そのときは「クリックして操作」を出す
    this.ui.overlay.addEventListener("pointerdown", e => { e.preventDefault(); this.onOverlay(); }, { signal });
    this.ui.overlay.addEventListener("click", () => this.onOverlay(), { signal });
    this.ui.stage.addEventListener("focusin", () => this.updateOverlay(), { signal });
    this.ui.stage.addEventListener("focusout", () => setTimeout(() => { if (!this.disposed) this.updateOverlay(); }, 0), { signal });
    // 手順で吹き出しの行数が変わっても、下のビューが動かないように高さをそろえる（幅が変わったら測り直す）
    this.fitBubble();
    let lastW = this.el.clientWidth;
    this.resizeObserver = new ResizeObserver(() => { if (this.el.clientWidth !== lastW) { lastW = this.el.clientWidth; this.fitBubble(); } });
    this.resizeObserver.observe(this.el);
    document.fonts?.ready.then(() => { if (!this.disposed) this.fitBubble(); });
    this.card = 0;
    this.ui.track.addEventListener("scroll", () => {
      const w = this.ui.track.clientWidth || 1;
      const i = Math.round(this.ui.track.scrollLeft / w);
      if (i !== this.card) { this.card = i; this.updateCards(); }
    }, { passive: true, signal });
  }

  /** WebGL のビュー・タイマー・観察・イベントを全部解放する(記事を離れるとき) */
  dispose() {
    this.disposed = true;
    this.abort.abort();
    this.resizeObserver?.disconnect();
    clearTimeout(this.failTimer);
    clearTimeout(this.msgTimer);
    this.suspend();
  }

  // ---------------------------------------------------------------- 画像で見る（スマートフォン・「画像で見る」）
  stepsHtml() {
    const steps = this.scenario.steps;
    const cards = steps.map((st, i) => {
      const keys = (st.keys ?? []).map(keyHtml).join(" ");
      const mouse = st.mouse ? `<span class="tut-mouse-inline"><i class="bi bi-mouse" aria-hidden="true"></i> ${escapeHtml(st.mouse)}</span>` : "";
      return `<figure class="tut-step-card" aria-roledescription="手順" aria-label="${i + 1} / ${steps.length}">
          <img src="${stepImage(this.name, i)}" alt="${escapeHtml(plainText(st.text))}" loading="lazy" width="1100" height="${this.unity ? 840 : 619}" />
          <figcaption>
            <div class="tut-text"><span class="tut-step-no">${i + 1}</span><span>${st.text}</span></div>
            ${keys || mouse ? `<div class="tut-step-keys">${keys}${mouse}</div>` : ""}
          </figcaption>
        </figure>`;
    }).join("");
    return `<div class="tut-steps" hidden>
        <div class="tut-steps-track" tabindex="0" aria-label="手順の画像（横にスワイプ）">${cards}</div>
        <div class="tut-steps-nav">
          <button type="button" class="btn btn-sm btn-outline-secondary" data-act="prev" aria-label="前の手順"><i class="bi bi-chevron-left" aria-hidden="true"></i> 前へ</button>
          <span class="tut-steps-count small"></span>
          <button type="button" class="btn btn-sm btn-outline-secondary" data-act="next" aria-label="次の手順">次へ <i class="bi bi-chevron-right" aria-hidden="true"></i></button>
        </div>
        <p class="tut-steps-note small text-body-secondary mb-0">${touchOnly
          ? "スマートフォン・タブレットでは、手順を画像で表示しています（3D ビューの操作にはキーボードとマウスが必要です）。"
          : "各画像は、その手順を終えたあとの画面です。"}</p>
      </div>`;
  }

  /** 吹き出しの高さを、このチュートリアルで一番長い手順（と完了の文）に合わせて固定する */
  fitBubble() {
    const b = this.ui.bubble;
    if (!b.offsetParent) return; // 画像で見ているあいだは非表示（測れない）
    const text = this.ui.text;
    const saved = text.innerHTML;
    const stepHtml = (n, html) => `<span class="tut-step-no">${n}</span><span>${html}</span>`;
    const variants = this.scenario.steps.map((st, i) => stepHtml(i + 1, st.text));
    variants.push(stepHtml("✓", `「${escapeHtml(this.scenario.title)}」の手順はすべて終わりです。`));
    b.style.minHeight = "";
    let max = 0;
    for (const v of variants) { text.innerHTML = v; max = Math.max(max, b.getBoundingClientRect().height); }
    text.innerHTML = saved;
    b.style.minHeight = `${Math.ceil(max)}px`;
  }

  /** 画像で見る（この版は画像だけ。3D は C# 版（practice-cs.js）が動かす） */
  setImageMode() {
    this.imageMode = true;
    this.el.classList.add("is-images");
    this.el.querySelector(".tut-steps").hidden = false;
    this.ui.modeBtn.hidden = true;
    this.suspend();
    this.updateCards();
    this.renderStep();
  }

  goCard(i) {
    const n = this.scenario.steps.length;
    this.card = Math.max(0, Math.min(n - 1, i));
    const t = this.ui.track;
    t.scrollTo({ left: this.card * t.clientWidth, behavior: reduceMotion.matches ? "auto" : "smooth" });
    this.updateCards();
  }

  updateCards() {
    const n = this.scenario.steps.length;
    this.ui.stepCount.textContent = `${this.card + 1} / ${n}`;
    this.el.querySelector('[data-act="prev"]').disabled = this.card === 0;
    this.el.querySelector('[data-act="next"]').disabled = this.card === n - 1;
    if (this.imageMode) {
      this.ui.count.textContent = `${this.card + 1} / ${n}`;
      this.ui.bar.style.width = `${((this.card + 1) / n) * 100}%`;
    }
  }

  /**
   * 画像を作るとき用（tools/tutorial-images/）: k 番目の手順を終えた状態にして、その手順の案内（矢印など）を出す。
   * ?debug=1 のときだけ window.__tutorials から呼べる
   */
  /** 画像を作るとき用（tools/tutorial-images/）は、3D を動かす版でだけ作れた。この版では作れない（旧版は commit 8262089） */
  async captureStep() {
    throw new Error("画像は 3D の版（commit 8262089）で作る。この版は画像で見る専用");
  }

  /** 別のチュートリアルにビューを譲る */
  suspend() {
    if (!this.vp) return;
    this.vp.dispose();
    this.vp = null;
    this.updateOverlay();
  }

  focusViewport() {
    if (this.vp) this.ui.vp.focus({ preventScroll: true });
  }

  onOverlay() {
    this.focusViewport();
  }

  updateOverlay() {
    const focused = this.ui.stage.contains(document.activeElement) && document.activeElement !== this.ui.overlay;
    const show = !this.imageMode && !this.finished && (!this.vp || !focused);
    this.ui.overlay.hidden = !show;
    this.ui.overlayText.textContent = this.loading ? "読み込み中…" : !this.vp ? (this.started ? "クリックして再開" : "クリックして始める") : "クリックして操作する";
    this.ui.overlay.classList.toggle("is-light", !!this.vp);
    this.el.classList.toggle("is-focused", !!this.vp && focused);
    if (this.vp) this.started = true;
  }

  // ---------------------------------------------------------------- 手順
  groupStart(i) {
    const steps = this.scenario.steps;
    const g = steps[i]?.group;
    if (!g) return i;
    while (i > 0 && steps[i - 1].group === g) i--;
    return i;
  }

  /** 最初の状態から、i 番目の手順の直前まで進めた状態にする。keepStatus なら最後の操作のステータス表示を残す */
  replayTo(i, keepStatus = false) {
    clearTimeout(this.failTimer);
    const vp = this.vp;
    if (vp) {
      vp.silent = true;
      vp.resetScene(this.scenario.scene);
      for (let k = 0; k < i && k < this.scenario.steps.length; k++) this.scenario.steps[k].skip?.(vp);
      vp.silent = false;
      if (!keepStatus) vp.setStatus("案内にしたがって操作してください");
    }
    this.index = i;
    this.ctx = {};
    this.renderStep();
  }

  retry() {
    if (this.finished) return this.restart();
    this.replayTo(this.groupStart(this.index));
    this.showMsg("この操作の最初からやり直します。", "info");
  }

  restart() {
    this.finished = false;
    this.ui.complete.hidden = true;
    this.replayTo(0);
  }

  skip() {
    if (this.finished) return;
    const step = this.scenario.steps[this.index];
    if (this.vp) {
      this.vp.silent = true;
      step.skip?.(this.vp);
      this.vp.silent = false;
    }
    this.advance(false);
  }

  advance(success = true) {
    clearTimeout(this.failTimer); // 失敗後のやり直し待ちのあいだに進んだら、やり直さない
    this.index++;
    this.ctx = {};
    if (success) this.flashOk();
    if (this.index >= this.scenario.steps.length) this.finish();
    else this.renderStep(success ? "できました！" : null);
  }

  onEvent(e) {
    if (this.finished) return;
    if (e.type === "key") { this.flashKey(e); }
    const step = this.scenario.steps[this.index];
    if (!step) return;
    if (step.done(e, this.vp, this.ctx)) { this.advance(); return; }
    const fail = step.fail?.(e, this.vp, this.ctx);
    if (fail) {
      // 少し待ってから戻す（確定やキャンセルの結果が一瞬見えるように）
      const target = this.groupStart(this.index);
      this.showMsg(`${fail} この操作の最初からやり直します。`, "warn");
      clearTimeout(this.failTimer);
      this.failTimer = setTimeout(() => { if (this.vp) { this.replayTo(target); this.showMsg(`${fail} この操作の最初からやり直しましょう。`, "warn"); } }, 400);
      return;
    }
    const hint = step.hint?.(e, this.vp, this.ctx)
      ?? (e.type === "opfail" ? opfailHint(e, this.vp) : undefined)
      ?? (e.type === "opstart" && ["G", "R", "S"].includes(e.kind) && !e.extrude && !step.allowOp ? "移動・回転・スケールが始まりました。<kbd>Esc</kbd> か右クリックでキャンセルしてください。" : undefined);
    if (hint) this.showMsg(hint, "hint");
  }

  renderStep(okText = null) {
    if (this.imageMode) { this.updateCards?.(); return; }
    const steps = this.scenario.steps;
    const n = steps.length;
    const i = Math.min(this.index, n);
    this.ui.count.textContent = `${Math.min(i + 1, n)} / ${n}`;
    this.ui.bar.style.width = `${(i / n) * 100}%`;
    const step = steps[i];
    if (!step) return;
    this.ui.text.innerHTML = `<span class="tut-step-no">${i + 1}</span><span>${step.text}</span>`;
    if (okText) this.showMsg(okText, "ok"); else this.hideMsg();
    const keys = (step.keys ?? []).map(keyHtml).join(" ");
    const mouse = step.mouse ? `<span class="tut-mouse"><i class="bi bi-mouse" aria-hidden="true"></i> ${escapeHtml(step.mouse)}</span>` : "";
    this.ui.keys.innerHTML = keys + mouse;
    this.ui.keys.hidden = !keys && !mouse;
    this.vp?.setHighlight(step.highlight ?? {});
  }

  showMsg(html, kind) {
    const m = this.ui.msg;
    m.innerHTML = html;
    m.className = `tut-msg is-${kind}`;
    m.hidden = false;
    clearTimeout(this.msgTimer);
    if (kind === "ok") this.msgTimer = setTimeout(() => this.hideMsg(), 1500);
  }

  hideMsg() { this.ui.msg.hidden = true; }

  flashOk() {
    const ok = this.ui.ok;
    ok.classList.remove("is-on");
    void ok.offsetWidth; // アニメーションを最初から
    ok.classList.add("is-on");
    this.ui.bubble.classList.remove("is-ok");
    void this.ui.bubble.offsetWidth;
    this.ui.bubble.classList.add("is-ok");
  }

  flashKey(e) {
    for (const kbd of this.ui.keys.querySelectorAll("kbd")) {
      if (!keyMatches(kbd.dataset.key, e)) continue;
      kbd.classList.add("is-pressed");
      setTimeout(() => kbd.classList.remove("is-pressed"), 250);
    }
  }

  finish() {
    this.finished = true;
    this.renderStep();
    this.ui.bar.style.width = "100%";
    this.ui.count.textContent = "完了";
    this.ui.text.innerHTML = `<span class="tut-step-no"><i class="bi bi-check-lg"></i></span><span>「${escapeHtml(this.scenario.title)}」の手順はすべて終わりです。</span>`;
    this.ui.keys.hidden = true;
    this.vp?.setHighlight({});
    saveDone(this.name);

    // ページ内のチュートリアルがすべて終わったら、ページを完了にする
    const page = document.querySelector("[data-progress-page]")?.dataset.progressPage;
    const done = loadDone();
    const all = [...tutorials.values()].every(t => !t.scenario || done[t.name]);
    let text = "";
    if (page && all && window.faraProgress) {
      const was = window.faraProgress.isDone(page);
      window.faraProgress.set(page, true);
      text = was ? "このページは完了済みです。" : "このページを「完了」にしました。";
    } else if (!all) {
      text = "このページには、ほかにもチュートリアルがあります。";
    }
    this.ui.completeText.textContent = text;
    this.ui.complete.hidden = false;
    this.updateOverlay();
  }
}

// ---------------------------------------------------------------- 起動
/**
 * container の中の .practice-embed[data-scenario] にチュートリアルを始める。返した dispose() を、ページを離れるとき・記事が替わるときに呼ぶ。
 * container の中身は Blazor が差し替える(記事の本文は MarkupString)ので、始めるのは、その本文が画面に出たあと。
 */
export function mount(container = document) {
  const mine = [];
  for (const el of container.querySelectorAll(".practice-embed[data-scenario]:not([data-cs])")) {
    if (tutorials.has(el)) continue;
    const t = new Tutorial(el);
    tutorials.set(el, t);
    mine.push(t);
  }

  // 確認用（?debug=1）: コンソールや自動テストから状態を見られるようにする
  if (new URLSearchParams(location.search).get("debug") === "1") window.__tutorials = [...tutorials.values()];

  return {
    dispose() {
      for (const t of mine) {
        t.dispose();
        tutorials.delete(t.el);
      }
      mine.length = 0;
      if (window.__tutorials) delete window.__tutorials;
    },
  };
}
