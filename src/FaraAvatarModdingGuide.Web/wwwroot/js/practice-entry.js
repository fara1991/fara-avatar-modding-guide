// 練習ページ(/practice, /practice/unity, /practice/room)の入口。Blazor のページ(PracticeHost.cs)が、IJSRuntime で import して呼ぶ。SSR 版の touch-practice.js に当たる。
//   start(id, pcScript, options) … 部屋（みんなで練習）を始めるとき（pcScript = room.js）。タッチだけの端末では、案内だけ
//   startArticle(id, selector)   … 記事を開いたとき(記事内チュートリアル。3D は C# 版、タッチの端末は「画像で見る」)
//   dispose(id)                  … ページを離れるとき(Blazor の DisposeAsync から)。id は、呼び出し側(PracticeHost)ごとの印
// - 1 人用の練習（/practice・/practice/unity）の 3D は、C# 版（practice-cs.js・Blazor）が動かす（Phase 3b S7。Three.js の版は消した）。
//   このファイルは、みんなで練習（room.js）の入口と、タッチだけの端末の案内と、記事内チュートリアルの入口を持つ
// - PC（room.js）: 練習のスクリプトを読み込んで、その start(options) を呼ぶ（options が null のときは始めない。部屋に入っていないとき）
// - タッチだけの端末（スマートフォンなど。記事内チュートリアルと同じ判定）: キーボードとマウスが必要な練習は出さず(読み込みもしない)、案内(#touch-practice)を出して、
//   記事内チュートリアルの「画像で見る」(tutorial.js の mount)を、案内の中の .practice-embed に始める
// 三次元の描画（room.js の viewport.js・three.js）は、S6（共有ルームの C# 化）まで残る。importmap は index.html の head にある
// 同時に 1 つだけ動く。始めるときに前のものが残っていれば、先に解放する(ページの往復で WebGL のコンテキストを増やさない)。
// dispose(id) は、いま動いているものが同じ id のときだけ解放する(前のページの解放が、次のページの開始より遅れて届いても、次のページを止めない)
const touchOnly = () => matchMedia("(hover: none) and (pointer: coarse)").matches;

/** タッチだけの端末か(C# 版は PC 用。タッチの端末では、これまでの案内の画面にする) */
export function isTouchOnly() { return touchOnly(); }

let current = null;

export async function start(id, pcScript, options) {
  disposeCurrent();
  const mine = { id, dispose() { } };
  current = mine;
  const host = document.getElementById("touch-practice");
  let handle;
  if (touchOnly()) {
    if (host) host.hidden = false;
    // 埋め込みが無いページ(みんなで練習)は、案内の文章とリンクだけ
    if (!host || !host.querySelector(".practice-embed[data-scenario]")) return;
    handle = (await import("./tutorial.js")).mount(host);
  } else {
    if (options === null) return;
    try {
      handle = (await import(`./${pcScript}`)).start(options);
    } catch (err) {
      // 練習のスクリプトや three.js を読み込めなかった(通信の失敗など)。枠が空のままにならないように、理由を書く
      console.error(err);
      const frame = document.querySelector("#room-viewport");
      if (frame && frame.childElementCount === 0) frame.textContent = "3D ビューを読み込めませんでした。通信を確かめて、ページを再読み込みしてください。";
      return;
    }
  }
  if (current !== mine) { handle?.dispose(); return; }   // 読み込んでいるあいだに、ページを離れた・次が始まった
  mine.dispose = () => handle?.dispose();
}

/**
 * 記事の本文(selector)の中の記事内チュートリアル(::: practice が出す空の .practice-embed[data-scenario])を始める(ArticlePage から)。
 * 3D は C# 版（practice-cs.js の mount。Blazor のコンポーネント）。C# 版にないもの・タッチの「画像で見る」は tutorial.js（画像だけ）。
 */
export async function startArticle(id, selector) {
  disposeCurrent();
  const mine = { id, dispose() { } };
  current = mine;
  const container = document.querySelector(selector);
  if (!container) return;
  const csHandle = await (await import("./practice-cs.js")).mount(container);
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
