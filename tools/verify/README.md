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
- `dom-diff.mjs` … 静的 HTML(WebAssembly を止めた状態)と、起動後の本文(`#fsk-main` の中)の DOM を比べる(練習 3 ページが既定。`ALL=1` で全 76 ページ)。Blazor のコメント・空白・JavaScript が中身を入れる枠を除く。`/practice/unity` の静的 HTML と Blazor の差の確認(計画書 13.5)
- `csp-check.mjs` … CSP の違反ゼロの確認(Phase 4。計画書 13.4): `_headers` の CSP が効く配信で、主要 23 ページ × PC・スマホを開き、スクロール・テーマ・検索・練習の 3D・共有ルームを操作して、`securitypolicyviolation`・コンソールのエラー/警告・失敗したリクエスト・ページのエラーを集める。1 件でもあれば終了コード 1
  - `csp-check.mjs` の注意(Web Analytics): ビーコン(`static.cloudflareinsights.com/beacon.min.js`)は、Cloudflare がゾーンの設定で本番・確認用の配信にだけ差し込む。`wrangler dev --local` では差し込まれないので、この検査では見えない。CSP の許可(script-src `static.cloudflareinsights.com`・connect-src `cloudflareinsights.com`)は `tools/site.json` と `SiteBuilderTests` で確かめ、実際の配信(avatar-dev)のコンソールで違反が無いことを目で確かめる
  - `csp-check.mjs` の注意: `requestfailed: …/api/maintenance` は、kit の `MaintenanceInfo` が 2 秒で打ち切る(仕様。読めなければ通常表示)ので、配信(wrangler dev)が重いとき(別の作業・別のスクリプトと**並列**に動かしたとき)に、ページを開いた直後に出ることがある。CSP の違反ではなく、実際の不具合でもない。このスクリプトは、その失敗だけのときに 1 回やり直す。それでも出るときは、配信を単独にして直列で再実行する
- Phase 3b S2（レンダーラボ。docs/phase3b-design.md の §11）: `dotnet publish src/FaraAvatarModdingGuide.RenderLab -c Release -o publish-lab` のあと、`LAB=<publish-lab/wwwroot> [HEADERS=<dist/_headers>] node gl-serve.mjs 8790` で `/lab/`（C# + 薄い WebGL 層）・`/ref.html`（現行の three.js 版だけ）・`/probe.html` を出す。`CHROME_PATH` は Playwright 同梱の Chromium（システムの Edge/Chrome に SwiftShader のフラグを付けると WebGL を作れない）
  - `gl-compare.mjs` … 同じシーン・操作で、three.js 版とレンダーラボの画素を比べる（面の明るさ ±3・画素の差。`WIDTH=390` でスマホ幅）。3 枚組を `OUT` に書く
  - `gl-measure.mjs` … AOT なしの WebAssembly の計測（起動・フレーム・重い操作・新しいコンテキスト 20 回・開始と停止 20 回・コンテキスト喪失からの復帰）
  - `gl-csp.mjs` … 本番と同じ CSP（`HEADERS=`）で、PC・スマホ幅の描画と CSP 違反 0
  - `gl-context-matrix.mjs`（`gl-probe.*`）… 「新しいコンテキストで描画が始まらない」の切り分け。ブラウザを CDP で直接操作し、隠れた（最小化・覆われた）ウィンドウで rAF が止まることを確かめる
- Phase 3b S3（`/practice` の C# 版 `?engine=cs`。docs/phase3b-design.md の §13）: `ENGINE=cs node practice.mjs`（`/practice` の操作・往復を C# 版で。基準は JS 版と同じ `practice-baseline.json`）、`practice-compare.mjs`（同じ操作で、Three.js 版と C# 版のビューポート（3D + HUD）の画素を比べる。`NEW=` はサイトの配信（wrangler dev など）、閾値 0.001 % / 8。S4 で厳しくした）
- Phase 3b S4（記事内チュートリアルの C# 版 `?engine=cs`。docs/phase3b-design.md の §14）: Three.js 版と C# 版の差を 0 にするための比較。`NEW=` はサイトの配信（wrangler dev など）。`CHROME_PATH` は Playwright 同梱の Chromium
  - `tutorial-compare.mjs` … 9 本を「次へ（スキップ）」で 1 手順ずつ進めながら、毎回、埋め込み全体（題・進み・吹き出し・3D + HUD・キー表示・ボタン）の画素と文字を比べる（閾値は 0 % / 8。`ONLY=`・`STEPS=`・`OUT=`）。背景の 3D を隠し、CSS アニメーションを止める（始まった時刻で位相が決まるため）
  - `tutorial-input-compare.mjs` … `transform` を、Three.js 版と C# 版で同じ本物のマウス・キーで操作して、毎回、画素と文字を比べる（スキップを通らない本物のイベントの経路。23 スナップショット）
  - `tutorial-dom.mjs` … 埋め込みの DOM の骨格（タグ・クラス・data-*・aria-*）を、5 つの状態（最初・フォーカス後・スキップ後・画像で見る・完了）で比べる
  - `practice-details.mjs` … `/practice` の細部（カーソル・フォーカス・枠・ボックス選択の枠・メニューの位置・HUD の文字・ツールの点灯・N パネル）を、25 の場面（ドラッグ中・数値入力中・メニューを開いたまま）で値で比べる
  - `practice-race.mjs` … 低速な読み込み中に「離れて戻る」（`Practice`・`Render` の wasm を 3.5 秒遅らせ、`/practice?engine=cs` → `/learn` → 戻る）。wasm を二重に読まず、エラーが出ないこと
- Phase 3b S6（共有ルームの C# 版 `?engine=cs`。docs/phase3b-design.md の §16）:
  - `room-compare.mjs` … `/practice/room` を、room.js（JS 版）と C# 版で、同じ台本（もう一人の参加者を生の WebSocket で動かす）に通す。A の画素（9 場面。背景の 3D は隠す。閾値 `MAX_OVER_PCT=0` `MAX_DIFF=8`）・状態と参加者の文字・A が送った文字（hello・name・select・lock・unlock・ops を完全に比べる。cursor・preview・view は数だけ）を比べる。`NEW=` は wrangler dev など
  - `room-mixed.mjs` … JS 版の A と C# 版の B を同じ部屋に入れ、互いの変更が届くことを、3D ビューの画素（ステータスの行とほかの人のカーソルを除く）で比べる
  - `practice.mjs` の room（`ENGINE=cs` と `ENGINE=js`）と `ONLY=trips`（`ENGINE=cs` は部屋も往復する）・`csp-check.mjs`（`?engine=cs` の部屋 2 画面を含む）
- Phase 3b S5（Unity 風の画面。docs/phase3b-design.md の §15）:
  - `unity-compare.mjs` … `/practice/unity` を、Three.js 版と C# 版（`?engine=cs`）で、同じ本物のマウス・キーの 38 操作（Hierarchy・Scene・ギズモ・Project のドラッグ・右クリック・Inspector・SDK・Build & Test…）で操作して、毎回、画素・DOM の骨格・お題・ステータス・状態（`window.__unity` と `window.__unityCs`）を比べる。閾値は `MAX_OVER_PCT=0` `MAX_DIFF=8`
  - `unity-lib.mjs` … ドラッグ・ギズモの位置・状態の読み取りと比べ（`unity-compare.mjs`・`tutorial-input-compare.mjs` の共通部品）
  - `tutorial-compare.mjs`・`tutorial-dom.mjs`・`tutorial-input-compare.mjs` は Unity の 2 本（`unity-basics`・`ma-outfit`）も比べる（`tutorial-input-compare` は `transform`・`unity-basics`・`ma-outfit` の実入力の列。`ONLY=<名前>`）
  - `practice.mjs` の `ENGINE=cs` は、`/practice/unity` の操作も C# 版で動かす。`csp-check.mjs` は `?engine=cs` の Unity 3 画面も見る
  - **画像比較の 0.000 % は SwiftShader（既定。DPR 1）が前提**。実 GPU で描くときは `GPU=1`、画面の拡大率は `DPR=1.25` など、ウィンドウ付きは `HEADED=1`（`lib.mjs`）。実 GPU・小数の DPR では、案内の枠のある Unity の手順で、小さな差（0.003〜0.012 %。実行によっては Scene の上端・下端の 1 行 0.1 %）が出ることがある既知の差。`tutorial-compare.mjs` の `GPU_TOLERANT=1` が、案内の枠（`.ue-hl`）の中の画素の差を無視し、閾値を 0.2 % / 40 に緩める（DOM の文字は比べる。通常の厳しさは変わらない）。`MOTION=1` は動きを減らす設定を外す（枠の脈動の位相が違うので、比べる用ではない）
  - `practice-mixed.mjs` … Unity と Blender の記事内チュートリアルが同じページにあるとき（`ma-outfit` の記事に `transform` を足して作る）、WebGL の切り替えで canvas が 1 つだけ・もう一方に 3D ビューが残らない
