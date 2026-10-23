# 移行版の動作確認(Playwright)

確認用のスクリプト。CI では動かさない(ブラウザが要る)。`npm i playwright-core@1.64.1 pngjs@7.0.0 pixelmatch@5.3.0` を、このフォルダの外(作業用のフォルダ)で入れて、そこから実行する。
ブラウザは `CHROME_PATH`(Chromium の実行ファイル)で指定できる。

- 配信: `dotnet publish` + Build の出力(publish/wwwroot)を `npx wrangler dev --env wasm-dev`(`_headers` の CSP と `/api/maintenance` が効く)で出し、`NEW=http://127.0.0.1:<port>` を指定する
- `article.mjs` … 記事: サイドバーの強調が 1 つ・完了の記録の読み書き(既存の値を壊さない)・目次の強調・記事から記事への移動(再読み込みなし・スクロールが戻る)
- `search-filters-progress.mjs` … ヘッダーの検索(Ctrl+K・/・Enter)・検索ページ・TIPS/用語集の絞り込みと #id・ショートカットのカテゴリ・目次/カテゴリ/ホームの進捗
- `back-nav.mjs` … 戻る/進むでスクロール位置が復元される(PC・スマホ)
- `fixes.mjs` … content.json が {} のとき・filter.json が無いとき・未知の記事・#id と絞り込み
- `hash-links.mjs` … 見出し付き URL(直接・アプリ内・同じページの目次)で見出しに止まる(PC・スマホ)
- `shots.mjs` … SSR(`SSR=` 現行の develop をローカルで動かしたもの)との見た目の比較(PC 1280・スマホ 390・ダーク/ライトの 3 枚組と差の割合)。コンソールエラー・CSP 違反も出す
