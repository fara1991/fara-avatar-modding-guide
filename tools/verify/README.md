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
- `seo.mjs` … SEO の検査(Phase 4): 全 76 ページ + 404 の head(title・description・canonical・OGP・noindex が 1 つずつ)と本文・内部リンク全件のリンク切れ・旧 URL の 301・404・X-Robots-Tag・起動後と、アプリ内の移動のあとの head。`ORIGIN_EXPECT=` で canonical の出どころ(確認用は https://avatar-wasm-dev.fara-labs.com)、`EXPECT_NOINDEX_HEADER=0` で本番相当(X-Robots-Tag なし・sitemap.xml が 74 URL)を確かめる
- `boot-diff.mjs` … 起動の前後の見た目の差(Phase 4。計画書 4.10): 前 = WebAssembly を止めた静的 HTML、後 = 起動後。7 ページ(記事 5・ホーム・検索)× PC 1280 / スマホ 390 × ダーク / ライトで、ピクセル差の割合・本文(見出し・パンくず・段落)の位置の差・起動の瞬間の見出しの移動・CLS・読み込み表示が覆う割合を出し、閾値(既定: ピクセル差 0.5%・位置 1px・覆う 2%)を超えたら終了コード 1。`SIDEBAR_HIDDEN=1` で PC のサイドバーを隠した設定。3 枚組(前 | 後 | 差)の画像を出力フォルダに書く。サイドバーの「いまのページ」の強調の計算済みスタイル(color・background-color)が静的と起動後で同じかも確かめる(小さな領域で、ピクセル差の閾値では見つからないため)
- `boot-scroll.mjs` … 起動の前の静的な本文でスクロールしてから起動しても、読んでいる位置が飛ばない(PC・スマホ)
- `dom-diff.mjs` … 静的 HTML(WebAssembly を止めた状態)と、起動後の本文(`#fsk-main` の中)の DOM を比べる(練習 3 ページが既定。`ALL=1` で全 76 ページ)。Blazor のコメント・空白・JavaScript が中身を入れる枠を除く。`/practice/unity` の静的 HTML と Blazor の差の確認(計画書 13.5)
- `csp-check.mjs` … CSP の違反ゼロの確認(Phase 4。計画書 13.4): `_headers` の CSP が効く配信で、主要 23 ページ × PC・スマホを開き、スクロール・テーマ・検索・練習の 3D・共有ルームを操作して、`securitypolicyviolation`・コンソールのエラー/警告・失敗したリクエスト・ページのエラーを集める。1 件でもあれば終了コード 1
  - `csp-check.mjs` の注意: `requestfailed: …/api/maintenance` は、kit の `MaintenanceInfo` が 2 秒で打ち切る(仕様。読めなければ通常表示)ので、配信(wrangler dev)が重いとき(別の作業・別のスクリプトと**並列**に動かしたとき)に、ページを開いた直後に出ることがある。CSP の違反ではなく、実際の不具合でもない。このスクリプトは、その失敗だけのときに 1 回やり直す。それでも出るときは、配信を単独にして直列で再実行する
