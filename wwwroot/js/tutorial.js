// 記事内チュートリアル。Markdown の「::: practice <名前>」から生成された .practice-embed に 3D ビューを埋め込み、
// scenarios.js の手順を 1 つずつ案内する（ゲームのチュートリアルのように、達成すると自動で次へ進む）。
//
// - three.js と viewport.js は、埋め込みが画面に入ったときに初めて読み込む
// - WebGL を使うビューはページ内で 1 つだけ。別のものを始めると、前のものは一時停止して「クリックして再開」になる
// - 最後まで進むと、ページ内のチュートリアルがすべて終わっていればページを完了にする（progress.js）
import { SCENARIOS } from "./scenarios.js";

const DONE_KEY = "blender-manual.tutorials"; // { シナリオ名: 完了日時 }
const touchOnly = matchMedia("(hover: none) and (pointer: coarse)").matches;
const tutorials = new Map();
let active = null; // WebGL を使っている Tutorial

function loadDone() {
  try { return JSON.parse(localStorage.getItem(DONE_KEY) || "{}") || {}; } catch { return {}; }
}

function saveDone(name) {
  const done = loadDone();
  done[name] = new Date().toISOString();
  try { localStorage.setItem(DONE_KEY, JSON.stringify(done)); } catch { /* 保存できなくても続ける */ }
}

function escapeHtml(s) {
  return s.replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
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
    this.index = 0;
    this.ctx = {};
    this.finished = false;
    if (!this.scenario) {
      el.classList.add("is-ready");
      el.innerHTML = `<div class="tut-error">チュートリアル「${escapeHtml(this.name)}」が見つかりません（wwwroot/js/scenarios.js）。</div>`;
      return;
    }
    this.build();
    this.renderStep();
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
      ${touchOnly ? `<div class="tut-touch-note"><i class="bi bi-keyboard" aria-hidden="true"></i> このチュートリアルはキーボードとマウスで操作します。スマートフォン・タブレットでは表示の確認だけになります。</div>` : ""}
      <div class="tut-bubble" aria-live="polite">
        <div class="tut-text"></div>
        <div class="tut-msg" hidden></div>
      </div>
      <div class="tut-stage">
        <div class="tut-vp practice-viewport"></div>
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
      count: q(".tut-count"), bar: q(".tut-progress span"), text: q(".tut-text"), msg: q(".tut-msg"), bubble: q(".tut-bubble"),
      stage: q(".tut-stage"), vp: q(".tut-vp"), keys: q(".tut-keys"), ok: q(".tut-ok"),
      overlay: q(".tut-overlay"), overlayText: q(".tut-overlay-text"), complete: q(".tut-complete"), completeText: q(".tut-complete-text"),
      numpad: q('[data-act="numpad"]'),
    };

    this.el.addEventListener("click", e => {
      const act = e.target.closest("[data-act]")?.dataset.act;
      if (act === "retry") this.retry();
      else if (act === "skip") this.skip();
      else if (act === "restart") this.restart();
      else return;
      this.focusViewport();
    });
    this.ui.numpad?.addEventListener("change", () => this.focusViewport());
    // ビュー以外にフォーカスがあるとキーが届かないので、そのときは「クリックして操作」を出す
    this.ui.overlay.addEventListener("pointerdown", e => { e.preventDefault(); this.onOverlay(); });
    this.ui.overlay.addEventListener("click", () => this.onOverlay());
    this.ui.stage.addEventListener("focusin", () => this.updateOverlay());
    this.ui.stage.addEventListener("focusout", () => setTimeout(() => this.updateOverlay(), 0));
  }

  // ---------------------------------------------------------------- ビューの用意
  async activate() {
    if (this.vp || this.loading || !this.scenario) return;
    if (active && active !== this) active.suspend();
    active = this;
    this.loading = true;
    this.ui.overlayText.textContent = "読み込み中…";
    try {
      const { Viewport } = await import("./viewport.js");
      if (active !== this) return; // 読み込み中に別のものが始まった
      this.vp = new Viewport(this.ui.vp, {
        scene: this.scenario.scene,
        emulateNumpad: () => this.ui.numpad?.checked ?? false,
        wheelNeedsFocus: true,
      });
      this.vp.on("*", e => this.onEvent(e));
      this.replayTo(this.finished ? this.scenario.steps.length : this.groupStart(this.index));
    } catch (err) {
      console.error(err);
      this.ui.vp.innerHTML = `<div class="tut-error">3D ビューを表示できません。WebGL が有効なブラウザで開いてください。</div>`;
    } finally {
      this.loading = false;
      this.updateOverlay();
    }
  }

  /** 別のチュートリアルにビューを譲る */
  suspend() {
    if (!this.vp) return;
    this.vp.dispose();
    this.vp = null;
    if (active === this) active = null;
    this.updateOverlay();
  }

  focusViewport() {
    if (this.vp) this.ui.vp.focus({ preventScroll: true });
    else this.activate().then(() => this.vp && this.ui.vp.focus({ preventScroll: true }));
  }

  onOverlay() {
    this.focusViewport();
  }

  updateOverlay() {
    const focused = this.ui.stage.contains(document.activeElement) && document.activeElement !== this.ui.overlay;
    const show = !this.finished && (!this.vp || !focused);
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

  /** 最初の状態から、i 番目の手順の直前まで進めた状態にする */
  replayTo(i) {
    const vp = this.vp;
    if (vp) {
      vp.silent = true;
      vp.resetScene(this.scenario.scene);
      for (let k = 0; k < i && k < this.scenario.steps.length; k++) this.scenario.steps[k].skip?.(vp);
      vp.silent = false;
      vp.setStatus("案内にしたがって操作してください");
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
      setTimeout(() => { if (this.vp) { this.replayTo(target); this.showMsg(`${fail} この操作の最初からやり直しましょう。`, "warn"); } }, 400);
      return;
    }
    const hint = step.hint?.(e, this.vp, this.ctx)
      ?? (e.type === "opfail" ? "先にオブジェクトをクリックして選択してください。" : undefined)
      ?? (e.type === "opstart" && !step.allowOp ? "移動・回転・スケールが始まりました。<kbd>Esc</kbd> か右クリックでキャンセルしてください。" : undefined);
    if (hint) this.showMsg(hint, "hint");
  }

  renderStep(okText = null) {
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
const embeds = document.querySelectorAll(".practice-embed[data-scenario]");
const observer = new IntersectionObserver(entries => {
  for (const entry of entries) {
    const t = tutorials.get(entry.target);
    if (!t) continue;
    t.visible = entry.isIntersecting;
    // 画面に入ったら読み込む。ほかのビューが見えているあいだは奪わない（クリックで切り替え）
    if (entry.isIntersecting && !t.vp && (!active || !active.visible)) t.activate();
  }
}, { rootMargin: "200px 0px" });

for (const el of embeds) {
  const t = new Tutorial(el);
  tutorials.set(el, t);
  if (t.scenario) observer.observe(el);
}

// 確認用（?debug=1）: コンソールや自動テストから状態を見られるようにする
if (new URLSearchParams(location.search).get("debug") === "1") window.__tutorials = [...tutorials.values()];
