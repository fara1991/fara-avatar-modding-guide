# 動作確認(Playwright)

確認用のスクリプト。CI では動かさない(ブラウザが要る)。`npm i playwright-core@1.64.1 pngjs@7.0.0 pixelmatch@5.3.0` を、このフォルダの外(作業用のフォルダ)で入れて、そこから実行する。
ブラウザは `CHROME_PATH`(Chromium の実行ファイル)で指定できる。WebGL(練習の 3D)は、GPU が無くても動くソフトウェア描画(SwiftShader)の設定で起動する(`lib.mjs` の `WEBGL_ARGS`)。

- 配信: `dotnet publish src/FaraAvatarModdingGuide.Web -c Release -o publish` → `publish/wwwroot` を `dist` にコピー → `dotnet run --project src/FaraAvatarModdingGuide.Build -c Release -- --template publish/wwwroot/index.html --out dist --env develop` → `npx wrangler dev --env dev --local`(`_headers` の CSP と `/api/maintenance` が効く。手順は README の「ビルドと公開」)で出し、`NEW=http://127.0.0.1:<port>` を指定する(本番用の確認は `--env production`)
- `article.mjs` … 記事: サイドバーの強調が 1 つ・完了の記録の読み書き(既存の値を壊さない)・目次の強調・記事から記事への移動(再読み込みなし・スクロールが戻る)
- `search-filters-progress.mjs` … ヘッダーの検索(Ctrl+K・/・Enter)・検索ページ・TIPS/用語集の絞り込みと #id・ショートカットのカテゴリ・目次/カテゴリ/ホームの進捗
- `back-nav.mjs` … 戻る/進むでスクロール位置が復元される(PC・スマホ)
- `fixes.mjs` … content.json が {} のとき・filter.json が無いとき・未知の記事・#id と絞り込み
- `hash-links.mjs` … 見出し付き URL(直接・アプリ内・同じページの目次)で見出しに止まる(PC・スマホ)
- `practice.mjs` … 練習(Phase 3): `/practice`・`/practice/unity` の操作の結果が、固定した基準(`practice-baseline.json`。Phase 5 で SSR 版を消す前に、SSR(develop)と移行版の結果が一致したときの値)と同じになる・記事内チュートリアル(画像で見る・別の記事へ移ると前の 3D が解放される)・スマホ(390)は案内だけで WebGL を使わない・共有ルームに二人が入って追加が届く(画面から)・ページ往復(`ROUNDS=` 回。既定 20)で WebGL の context・ResizeObserver・IntersectionObserver・setInterval・WebSocket・window/document のイベント・DOM・背景の 3D(`#site-scene` が 1 つ・描画のループ)が増えない。CSP 違反 0・コンソールのエラーと警告 0
- `seo.mjs` … SEO の検査(Phase 4): 全 76 ページ + 404 の head(title・description・canonical・OGP・noindex が 1 つずつ)と本文・内部リンク全件のリンク切れ・旧 URL の 301・404・X-Robots-Tag・起動後と、アプリ内の移動のあとの head。`ORIGIN_EXPECT=` で canonical の出どころ(確認用は https://avatar-dev.fara-labs.com)、`EXPECT_NOINDEX_HEADER=0` で本番相当(X-Robots-Tag なし・sitemap.xml が 74 URL)を確かめる
- `boot-diff.mjs` … 起動の前後の見た目の差(Phase 4。計画書 4.10): 前 = WebAssembly を止めた静的 HTML、後 = 起動後。7 ページ(記事 5・ホーム・検索)× PC 1280 / スマホ 390 × ダーク / ライトで、ピクセル差の割合・本文(見出し・パンくず・段落)の位置の差・起動の瞬間の見出しの移動・CLS・読み込み表示が覆う割合を出し、閾値(既定: ピクセル差 0.5%・位置 1px・覆う 2%)を超えたら終了コード 1。`SIDEBAR_HIDDEN=1` で PC のサイドバーを隠した設定。3 枚組(前 | 後 | 差)の画像を出力フォルダに書く。サイドバーの「いまのページ」の強調の計算済みスタイル(color・background-color)が静的と起動後で同じかも確かめる(小さな領域で、ピクセル差の閾値では見つからないため)
- `boot-scroll.mjs` … 起動の前の静的な本文でスクロールしてから起動しても、読んでいる位置が飛ばない(PC・スマホ)
- `dom-diff.mjs` … 静的 HTML(WebAssembly を止めた状態)と、起動後の本文(`#fbc-main` の中)の DOM を比べる(練習 3 ページが既定。`ALL=1` で全 76 ページ)。Blazor のコメント・空白・JavaScript が中身を入れる枠を除く。`/practice/unity` の静的 HTML と Blazor の差の確認(計画書 13.5)
- `csp-check.mjs` … CSP の違反ゼロの確認(Phase 4。計画書 13.4): `_headers` の CSP が効く配信で、主要 23 ページ × PC・スマホを開き、スクロール・テーマ・検索・練習の 3D・共有ルームを操作して、`securitypolicyviolation`・コンソールのエラー/警告・失敗したリクエスト・ページのエラーを集める。1 件でもあれば終了コード 1
  - `csp-check.mjs` の注意(Web Analytics): ビーコン(`static.cloudflareinsights.com/beacon.min.js`)は、Cloudflare がゾーンの設定で本番・確認用の配信にだけ差し込む。`wrangler dev --local` では差し込まれないので、この検査では見えない。CSP の許可(script-src `static.cloudflareinsights.com`・connect-src `cloudflareinsights.com`)は `tools/site.json` と `SiteBuilderTests` で確かめ、実際の配信(avatar-dev)のコンソールで違反が無いことを目で確かめる
  - `csp-check.mjs` の注意: `requestfailed: …/api/maintenance` は、kit の `MaintenanceInfo` が 2 秒で打ち切る(仕様。読めなければ通常表示)ので、配信(wrangler dev)が重いとき(別の作業・別のスクリプトと**並列**に動かしたとき)に、ページを開いた直後に出ることがある。CSP の違反ではなく、実際の不具合でもない。このスクリプトは、その失敗だけのときに 1 回やり直す。それでも出るときは、配信を単独にして直列で再実行する
## Phase 3b（練習・記事内チュートリアル・Unity 風の画面・共有ルーム）

three.js の版は S7 で消した。今の既定は C# 版（`?engine=` の指定は無い。`?engine=js` も無視される）。

### 凍結した基準（`golden.mjs`）

- **既定（`GOLDEN_MODE` なし）**: 保存した基準（`tests/visual/golden/`・`tests/js/golden/`）と、いま配っている C# 版を比べる。three.js は要らない。
- **`GOLDEN_MODE=capture`**: 基準を作る（上書きする）。three.js の版が残っている commit でだけ動く。作る前に `assertCaptureTarget` が、配信先の `/js/viewport.js` と `/lib/three/three.module.js` が 200 かを確かめ、無ければ止める（C# の出力で基準を上書きしないため）。作り方は docs/phase3b-design.md の §17。
- `GOLDEN_DIR=<フォルダ>`: 既定は `tests/visual/golden`。別の場所から（`pw/verify` などに写して）実行するときは絶対パス。
- 基準を作ったときの元の commit は `tests/visual/golden/room-compare/meta.json` の `source` に書いてある（共有ルームは commit 32b19c8）。

### 画像・DOM・文字の比較（基準と C# 版）

- `practice-compare.mjs` … `/practice` のビューポート（3D + HUD）の画素。9 場面。閾値 0.001 % / 8
- `practice-details.mjs` … `/practice` の細部（枠・カーソル・フォーカス・メニューの位置・HUD の文字）を、25 の場面で値として比べる
- `tutorial-compare.mjs` … 記事内チュートリアル 11 本を、手順ごとに比べる（閾値 0 % / 8。156 件）
- `tutorial-input-compare.mjs` … `transform`・`unity-basics`・`ma-outfit` を、本物のマウス・キーで操作して比べる（66 件）
- `tutorial-dom.mjs` … 記事内チュートリアルの DOM の骨格（5 つの状態）
- `unity-compare.mjs` … `/practice/unity` を、本物のマウス・キーの 44 操作で比べる（画素・DOM・お題・文字・状態）
- `room-compare.mjs` … `/practice/room` を、台本（もう一人の参加者を生の WebSocket で動かす）で比べる（9 場面・送った文字の件数）。部屋の WebSocket は Worker が要るので、`npx wrangler@4.149.0 dev --env dev --local` の配信に対して実行する（`NEW=`）
- 画素の 0.000 % は SwiftShader（既定。DPR 1）が前提。実 GPU・小数の DPR の差は `GPU=1`・`DPR=…`・`GPU_TOLERANT=1`（tutorial-compare）で扱う（docs/phase3b-design.md の §15.5）

### 基準を使わない確認（C# 版だけで動く）

- `practice.mjs` … 練習の操作（`practice-baseline.json` と比べる）・記事内チュートリアル・Unity 風の画面・スマホの案内・共有ルームの往復（部屋の Worker が要る）
- `room-race.mjs` … 共有ルームで、読み込み中に部屋を替える（A の開始が遅れても、残るのは B だけ・WebSocket は 1 本・canvas は 1 つ）
- `practice-race.mjs` … 練習の wasm を遅らせ、読み込み中に離れて戻る（wasm を二重に読まず、エラーが出ない）
- `practice-mixed.mjs` … 記事内の Blender と Unity のチュートリアルが同じページにあるとき、3D の canvas が 1 つだけ
- `dom-diff.mjs` … `/practice`・`/practice/unity`・`/practice/room` の静的 HTML と、起動後の本文の DOM が同じか（PC の 3 ページ）
- `csp-check.mjs` … 主要ページの CSP 違反 0（Worker の API が無いと `/report` と部屋の WebSocket が NG になる）
- `gl-measure.mjs`・`gl-csp.mjs` … レンダーラボ（S2）の計測・CSP（RenderLab を `gl-serve.mjs` で配る）

### JS 版が無くなって代わりに使うもの（三次元の JS を外した後の対応表）

| 以前の確認 | 今の代わり |
| --- | --- |
| 練習・記事内チュートリアル・共有ルームの JS 起動と解放（`practice-entry` の id） | `dotnet test`（Web.Tests `PracticeTests`: 案内の start・dispose の id、部屋切替で前の部屋を id で解放） |
| 部屋切替の順序（JS 版の `room.js`） | `room-race.mjs`（ブラウザ）＋ `PracticeTests`（id） |
| `/practice`・`/practice/unity` の静的 HTML との DOM | `dotnet test`（PageDomParityTests。タッチ端末の模擬で比べる）＋ `dom-diff.mjs`（ブラウザ。PC は C# 版の起動後の DOM） |
| Unity ページの起動・破棄 | `dotnet test`（PracticeTests）＋ `practice.mjs` の Unity の操作 |
| 記事内チュートリアルの画像の作り直し（`tools/tutorial-images`） | なし（S7 で消した。commit 8262089 にある。画像は残る） |

### 注意

- `tutorial-compare` などの基準の比較で、基準の作り方（three.js の版）と今の C# 版は、同じ配信を使わない。基準を作るのは、3D の JS が残っている commit だけ。
- `practice-race`・`room-race`・`dom-diff` は、C# 版の確認としての代替（JS 版の比較ではない）。
