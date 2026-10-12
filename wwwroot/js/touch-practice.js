// 練習ページ（/practice, /practice/unity, /practice/room）の入口。
// - PC: 本来の練習のスクリプト（#touch-practice の data-pc-script）を読み込む。見た目も動作も従来どおり
// - タッチだけの端末（スマートフォンなど。記事内チュートリアルと同じ判定）: キーボードとマウスが必要な練習は出さず（読み込みもしない）、
//   記事内チュートリアルの「画像で見る」（tutorial.js）を差し込む
const touchOnly = matchMedia("(hover: none) and (pointer: coarse)").matches;
const host = document.getElementById("touch-practice");

if (!host) {
  // 案内の枠がないページでは何もしない
} else if (touchOnly) {
  host.hidden = false;
  const tpl = document.getElementById("touch-practice-tpl");
  if (tpl) {
    host.append(tpl.content.cloneNode(true));
    // 埋め込みを差し込んだあとで読み込む（tutorial.js は読み込み時に .practice-embed を探す）
    import("./tutorial.js");
  }
} else {
  import(host.dataset.pcScript);
}
