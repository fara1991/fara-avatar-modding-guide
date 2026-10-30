// 練習ページ(/practice, /practice/unity, /practice/room)と記事内チュートリアルの入口。Blazor のページ(PracticeHost.cs)が、IJSRuntime で import して呼ぶ。SSR 版の touch-practice.js に当たる。
//   start(id)                   … 練習のページを開いたとき。タッチだけの端末では、案内（#touch-practice）と「画像で見る」を出す。PC では何もしない（3D は C# 版）
//   startArticle(id, selector)  … 記事を開いたとき（記事内チュートリアル）。PC は C# 版（practice-cs.js）、タッチの端末は「画像で見る」（tutorial.js）
//   dispose(id)                 … ページを離れるとき（Blazor の DisposeAsync から）。id は、呼び出し側（PracticeHost）ごとの印
// three.js は使わない（Phase 3b S7。みんなで練習も C# 版（room-cs.js と Practice・Render））。
// 同時に 1 つだけ動く。始めるときに前のものが残っていれば、先に解放する。dispose(id) は、いま動いているものが同じ id のときだけ解放する
// （前のページの解放が、次のページの開始より遅れて届いても、次のページを止めない）。
const touchOnly = () => matchMedia("(hover: none) and (pointer: coarse)").matches;

/** タッチだけの端末か（C# 版は PC 用。タッチの端末では、これまでの案内の画面にする） */
export function isTouchOnly() { return touchOnly(); }

let current = null;

export async function start(id) {
  disposeCurrent();
  const mine = { id, dispose() { } };
  current = mine;
  if (!touchOnly()) return;   // PC の 3D は C# 版（Blazor が始める）
  const host = document.getElementById("touch-practice");
  if (host) host.hidden = false;
  // 埋め込みが無いページ（みんなで練習）は、案内の文章とリンクだけ
  if (!host || !host.querySelector(".practice-embed[data-scenario]")) return;
  const handle = (await import("./tutorial.js")).mount(host);
  if (current !== mine) { handle.dispose(); return; }   // 読み込んでいるあいだに、ページを離れた・次が始まった
  mine.dispose = () => handle.dispose();
}

/**
 * 記事の本文（selector）の中の記事内チュートリアル（::: practice が出す空の .practice-embed[data-scenario]）を始める（ArticlePage から）。
 * PC の 3D は C# 版（practice-cs.js の mount）。タッチだけの端末は 3D を読まない（画像で見るだけの tutorial.js）。
 */
export async function startArticle(id, selector) {
  disposeCurrent();
  const mine = { id, dispose() { } };
  current = mine;
  const container = document.querySelector(selector);
  if (!container) return;
  // C# 版の記事内チュートリアルが失敗しても（読み込みの失敗など）、「画像で見る」（tutorial.js）は始める。失敗は console.error に出す
  let csHandle = null;
  if (!touchOnly()) {
    try { csHandle = await (await import("./practice-cs.js")).mount(container); }
    catch (err) { console.error(err); }
  }
  const handle = (await import("./tutorial.js")).mount(container);
  if (current !== mine) { handle.dispose(); csHandle?.dispose(); return; }
  mine.dispose = () => { handle.dispose(); csHandle?.dispose(); };
}

export function dispose(id) {
  if (current?.id === id) disposeCurrent();
}

function disposeCurrent() {
  const was = current;
  current = null;
  was?.dispose();
}
