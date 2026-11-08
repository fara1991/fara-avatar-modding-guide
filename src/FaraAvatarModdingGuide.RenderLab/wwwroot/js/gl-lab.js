// レンダーラボ(src/FaraAvatarModdingGuide.RenderLab。サイトには載せない)専用の補助。gl.js(薄い WebGL 層。サイトの練習・記事内チュートリアルが使う)から分けたもの(Phase 3b S5)。
// 状態の要約・コンテキストを人為的に失わせる・計測の結果を置く・制御用の関数・クエリ文字列。確認用の window.__glDiag / __glLose は、レンダーラボだけが exposeDebugGlobals() で作る。
import { inspectState } from "./gl.js";

/** 調べる用: いまの状態の要約 */
export function diag() {
  const s = inspectState();
  const base = { visibilityState: document.visibilityState, hasFocus: document.hasFocus(), raf: s ? s.stats.rafCalls || 0 : 0 };
  if (!s) return JSON.stringify({ ...base, started: false });
  return JSON.stringify({
    ...base, started: true, lost: s.lost, onScreen: s.onScreen, renderer: s.renderer, pixelRatio: s.pixelRatio,
    canvas: { w: s.canvas.width, h: s.canvas.height, cw: s.root.clientWidth, ch: s.root.clientHeight },
    buffers: s.buffers.size, ...s.stats,
  });
}

/** 取り出した計測の結果を置く場所(C# の Report が書き、Playwright が読む) */
export function report(name, json) {
  (globalThis.__labReport ??= {})[name] = JSON.parse(json);
}

/** C# の制御用の関数(ベンチ・停止・再開)を、ページから呼べるようにする */
export function exposeControl(control) {
  globalThis.__labControl = control;
}

export function getQuery() { return location.search; }

/** 呼び出し 1 回の時間を測るための、いまの時刻(ms) */
export function now() { return performance.now(); }

/** 調べる用: コンテキストを人為的に失わせる / 戻す(WEBGL_lose_context) */
export function loseContext(restoreAfterMs) {
  const s = inspectState();
  const ext = s?.gl?.getExtension("WEBGL_lose_context");
  if (!ext) return false;
  ext.loseContext();
  if (restoreAfterMs >= 0) setTimeout(() => ext.restoreContext(), restoreAfterMs);
  return true;
}

/** 確認用: window.__glDiag / __glLose を作る。レンダーラボ（RenderLab）だけが呼ぶ（サイトの /practice では公開しない） */
export function exposeDebugGlobals() {
  globalThis.__glDiag = diag;
  globalThis.__glLose = loseContext;
}
