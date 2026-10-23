# 移行版の動作確認(Playwright)

確認用のスクリプト。CI では動かさない(ブラウザが要る)。`npm i playwright-core@1.64.1 pngjs@7.0.0 pixelmatch@5.3.0` を、このフォルダの外(作業用のフォルダ)で入れて、そこから実行する。
ブラウザは `CHROME_PATH`(Chromium の実行ファイル)で指定できる。WebGL(練習の 3D)は、GPU が無くても動くソフトウェア描画(SwiftShader)の設定で起動する(`lib.mjs` の `WEBGL_ARGS`)。

- 配信: `dotnet publish` + Build の出力(publish/wwwroot)を `npx wrangler dev --env wasm-dev`(`_headers` の CSP と `/api/maintenance` が効く)で出し、`NEW=http://127.0.0.1:<port>` を指定する
- `article.mjs` … 記事: サイドバーの強調が 1 つ・完了の記録の読み書き(既存の値を壊さない)・目次の強調・記事から記事への移動(再読み込みなし・スクロールが戻る)
- `search-filters-progress.mjs` … ヘッダーの検索(Ctrl+K・/・Enter)・検索ページ・TIPS/用語集の絞り込みと #id・ショートカットのカテゴリ・目次/カテゴリ/ホームの進捗
- `back-nav.mjs` … 戻る/進むでスクロール位置が復元される(PC・スマホ)
- `fixes.mjs` … content.json が {} のとき・filter.json が無いとき・未知の記事・#id と絞り込み
- `hash-links.mjs` … 見出し付き URL(直接・アプリ内・同じページの目次)で見出しに止まる(PC・スマホ)
- `shots.mjs` … SSR(`SSR=` 現行の develop をローカルで動かしたもの)との見た目の比較(PC 1280・スマホ 390・ダーク/ライトの 3 枚組と差の割合)。コンソールエラー・CSP 違反も出す
- `practice.mjs` … 練習(Phase 3): `/practice`・`/practice/unity` の操作が SSR(`SSR=`)と同じ結果になる・記事内チュートリアル(画像で見る・別の記事へ移ると前の 3D が解放される)・スマホ(390)は案内だけで WebGL を使わない・共有ルームに二人が入って追加が届く(画面から)・ページ往復(`ROUNDS=` 回。既定 20)で WebGL の context・ResizeObserver・IntersectionObserver・setInterval・WebSocket・window/document のイベント・DOM・背景の 3D(`#site-scene` が 1 つ・描画のループ)が増えない。CSP 違反 0・コンソールのエラーと警告 0
