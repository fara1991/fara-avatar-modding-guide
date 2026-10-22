# avatar の Blazor WebAssembly + MudBlazor 移行計画

- 対象: `fara-avatar-modding-guide`（本番 https://avatar.fara-labs.com / 確認用 https://avatar-dev.fara-labs.com）
- 基準: `origin/develop` = `3340302`（PR #67 のマージ後）。試作は `origin/experiment/wasm-mudblazor` = `414747c`（PR #69、develop 向け、OPEN、マージしない）
- 参照: `fara-pokemon-battle-assistant`（同じ方式で公開済み。ローカルの `HEAD` は `fix/notice-sticky` のため、`tools/build-entry-pages.mjs` などは `origin/develop` 側（`feature/seo-entry-pages` 由来、コミット `6f6b0a8` / `dca32fe`）を見た）
- この文書は計画と棚卸しだけ。実装はしない。

表記: **[事実]** はリポジトリ・試作・計測ファイルで確認したもの、**[推測]** は見積もりや判断。

---

## 0. 決定事項（ユーザーの指示）と、この計画の前提

| 項目 | 決定 | 計画での扱い |
| --- | --- | --- |
| UI の土台 | Blazor WebAssembly + MudBlazor（ポケモンに合わせる） | 採用 |
| 速さ | 問題にしない（回線の遅さは考慮しない） | 計測は参考値として記録。性能は受け入れ条件にしない |
| SEO | ポケモンと同じ方式。ビルド時にページごとの静的 HTML を作り、読み込み後に WebAssembly が引き継ぐ | 採用 |
| 静的 HTML の中身 | 要約ではなく**本文入り**。記事ごとに自動生成する | 採用（現行 SSR 版と同等の本文を出す） |

**[事実] 現状は既に「ページごとの静的 HTML」**。`Publishing/StaticSiteExporter.cs` が `dotnet run -- --export dist` で、全ページ（記事 50 件を含む）を HTML として書き出している。つまり SEO の面では、移行後の静的 HTML は「今と同じ本文を、同じ URL で出す」ことが合格ラインになる。

---

## 1. 現状の棚卸し

### 1.1 ルートと配信される URL

**[事実]** `Components/Pages` の `@page` 一覧（20 件）と、Program.cs の追加ルート。

| URL | 定義 | 現行の配信 | 備考 |
| --- | --- | --- | --- |
| `/` | Home.razor | `index.html`（SSR 書き出し） | 目次の入口、Roadmap 等 |
| `/learn` | LearnIndex.razor | `learn.html` | 区切り（sections.yml）ごとの目次 |
| `/learn/{CategoryId}` | CategoryPage.razor | `learn/<cat>.html` | 10 カテゴリ（categories.yml） |
| `/learn/{CategoryId}/{Slug}` | ArticlePage.razor | `learn/<cat>/<slug>.html` | 記事 50 件（`Content/learn/**/*.md`） |
| `/tips` `/howto` `/qa` `/glossary` | Tips/*.razor | `*.html` | tipsfilter.js で絞り込み |
| `/shortcuts` | Shortcuts.razor | `shortcuts.html` | shortcuts.js |
| `/addons` | Addons.razor | `addons.html` | |
| `/links` | Links.razor | `links.html` | links.yml |
| `/changelog` | Changelog.razor | `changelog.html` | changelog.yml |
| `/report` | Report.razor | `report.html`（noindex） | report.js + Turnstile |
| `/privacy` | Privacy.razor | `privacy.html` | |
| `/search` | Search.razor | `search.html` | search.js |
| `/practice` | Practice.razor | `practice.html` | three.js（PC のみ） |
| `/practice/room` | PracticeRoom.razor | `practice/room.html` | room.js（WebSocket） |
| `/practice/unity` | PracticeUnity.razor | `practice/unity.html` | unity.js（three.js） |
| `/error` `/not-found` | Error.razor / NotFoundPage.razor | 404 は `404.html` | `UseStatusCodePagesWithReExecute("/not-found")` |
| `/search-index.json` | Program.cs（Minimal API） | `search-index.json` | 検索索引（BuildSearchIndex） |
| `/operations` `/avatar-clothing` `/plugins` | Program.cs | 301（`_redirects`） | 旧 URL |

**[事実] 記事 URL は 50 件**（`Content/learn/**/*.md`）。画像・動画は `Content/learn/<category>/images/` から `/learn/...` として配信（Program.cs の `mediaOnly` 判定。`StaticSiteExporter.IsMedia` と同じ）。

**[事実] 静的ファイル**: `wwwroot/css/site.css`（約 3,260 行、Bootstrap と併用）、`wwwroot/js/*.js` 22 本、`wwwroot/images/`（SVG 2 本、チュートリアル画像 WebP 約 120 枚）。

### 1.2 機能の棚卸し（WebAssembly 版での実現・ポケモン版からの流用）

凡例 — 流用: ◎ そのまま近い / ○ 型・パターンを参考 / △ 考え方のみ / ✕ 流用不可。

| # | 機能 | 現行の実現（ファイル） | WebAssembly 版での実現 | ポケモン版から流用 |
| --- | --- | --- | --- | --- |
| 1 | ページ全体のレイアウト | `Components/App.razor`・`Layout/MainLayout.razor`（PC ヘッダー＋サイドバー＋本文＋フッター） | MudLayout（MudAppBar・MudDrawer・MudMainContent）。サイドバーは「PC で隠せる・ホバーで広がる」挙動を MudDrawer の Variant と CSS で再現 | ○ `Shared/MainLayout.razor`・`Shared/MudProviders.razor` |
| 2 | テーマ（ダーク／ライト） | App.razor のインライン script（描画前に `data-bs-theme` を設定。キー `avatar-modding-guide.theme`）・`ThemeSwitch.razor`・`sidebar.js` | MudThemeProvider の `IsDarkMode`。描画前の反映は head の小さな script で続ける（キーは同じ `avatar-modding-guide.theme` を使い、既存の保存値を引き継ぐ） | ○ AppState のテーマ連携（`AppState.cs` の注記）、MudProviders |
| 3 | サイドバー（目次メニュー）の開閉 | `NavMenu.razor`・`sidebar.js`（キー `avatar-modding-guide.sidebar-hidden`） | MudDrawer＋ローカル保存（同じキー）。スマホは上部メニューから | ○ |
| 4 | ヘッダーの検索欄・本文上の検索欄 | `QuickSearch.razor`・`quicksearch.js`・`searchcore.js` | Razor の検索欄＋候補。候補の計算は C#（索引は JSON） | △ |
| 5 | サイト内検索（/search） | `Search.razor`・`search.js`・`searchcore.js`（NFKC・小文字・〜・カタカナ→ひらがな・ローマ字の読み替え・強調・抜粋） | 索引の正規化はビルド時（ContentExport が NFKC 済みの文字列を出す）。検索語の正規化はブラウザで `String.prototype.normalize` を JS interop で呼ぶ（D9 参照）。ローマ字・強調・抜粋は C# へ移植して Node 版と同じ入力で比べる | △ `Services/SearchService`（考え方のみ） |
| 6 | ショートカット一覧 | `Shortcuts.razor`・`shortcuts.js`・`shortcuts.yml` | Razor（絞り込みは MudSelect/MudChip の状態） | ✕ |
| 7 | TIPS・Q&A・用語集・How-to の絞り込み | `TipsFilter.razor`・`tipsfilter.js`（NFKC の正規化を JS で実施） | Razor の状態で絞り込み。正規化は interop（検索と同じ） | ✕ |
| 8 | 記事の目次（PC 右列・スマホは折りたたみ）と現在地の強調 | `ArticlePage.razor`・`toc.js`（スクロールに合わせた強調） | Razor で目次を出す。現在地の強調は IntersectionObserver を JS interop で（小さな JS） | ✕ |
| 9 | パンくず | `ArticlePage.razor`（目次 → 区切り → カテゴリ → 記事） | MudBreadcrumbs または現行の `<ol class="breadcrumb">` 相当の Razor | △ |
| 10 | 前後の記事への送り | `ArticlePage.razor`（`GetNeighbors`） | Razor（ContentService の GetNeighbors を移植） | ✕ |
| 11 | 記事本文（Markdown → HTML） | ContentService（Markdig）→ `Html` | ビルド時に Markdig で HTML 化して JSON（または記事ごとの HTML）に入れる。WASM 側は Markdig を持たない（D2） | ✕（ポケモンは Markdown を使わない） |
| 12 | 記事内の「この記事で使うキー」 | `ArticlePage.razor`（shortcuts 参照） | Razor | ✕ |
| 13 | 進捗の記録（読み終えた記事・件数・「続きを読む」） | `progress.js`・`ProgressSummary.razor`（キー `avatar-modding-guide.progress`）・`tutorial.js` 側の `avatar-modding-guide.tutorials` | Blazor 側の状態＋ localStorage。**キー名と値の形式を変えない**（既存の記録が消えないように）。`ProgressSummary` は Razor に | △（`AppState`/`CloudSync` の localStorage 保存の型） |
| 14 | 記事内チュートリアル（`::: practice <scenario>`） | `Data/ContentService.cs`（`practice-embed` div）・`tutorial.js`（three.js・importmap） | 記事の静的 HTML にはプレースホルダを残し、記事を開いたあとに JS（three.js）を interop で起動。JS 無しでも本文は読める | ✕ |
| 15 | 背景の 3D | `wwwroot/js/bgscene.js`（three.js、全ページ） | JS モジュールを Layout から interop で起動・停止。ページ移動では止めない（破棄しない） | ✕ |
| 16 | スクロール進捗線（画面上部） | `scrollprogress.js`・CSS `.scroll-progress` | 小さな JS interop、または CSS（scroll-driven）。読み上げには出さない（aria-hidden） | ✕ |
| 17 | お知らせ帯（更新予定） | Worker `withNotice`（HTML に注入。`worker/maintenance.js`） | D4 参照（Worker で注入を残すか、WASM が `/api/maintenance` を読むか） | ○ `MaintenanceBanner.razor`・`MaintenanceInfo.cs`（クライアント読み込みの型） |
| 18 | メンテナンス画面（503） | Worker（`getMaintenance` → `maintenanceResponse`。`MAINTENANCE=on`） | Worker のまま（全リクエストを 503 にする）。WASM 側の画面は不要 | △（`MaintenanceScreen.razor` は WASM 内の表示。avatar は Worker で止めるので原則不要） |
| 19 | 問い合わせ・報告（`/report`） | `Report.razor`・`report.js`（Turnstile）・`worker/report.js`（Discord 転送、`/api/report`、`/api/report/config`） | Blazor のフォームで `/api/report` に POST。Turnstile は JS（`challenges.cloudflare.com`）を interop で描画しトークンを受け取る。Worker は変えない | ◎ `Services/ReportForm.cs`・`Report.razor`・`wwwroot/js/report.js`・`worker/report.js`（同じ API 形） |
| 20 | プライバシーポリシー | `Privacy.razor` | Razor（本文は移す） | ○ |
| 21 | 更新履歴 | `Changelog.razor`・`changelog.yml`・`ChangelogTests` | Razor（ContentService の ParseChangelog を流用） | ○ `Services/Changelog.cs`（考え方） |
| 22 | リンク集 | `Links.razor`・`links.yml` | Razor | ✕ |
| 23 | 用語集・How-to・Q&A | `Tips/Glossary.razor` など・`tips/*.yml` | Razor | ✕ |
| 24 | アドオン | `Addons.razor`・`addons.yml` | Razor | ✕ |
| 25 | 区切り（Blender / Unity / VRChat …）の目次 | `LearnIndex.razor`・`ArticleCard.razor`・`Roadmap.razor` | Razor。アイコンは D6 | ✕ |
| 26 | 難易度・タグ表示 | `DifficultyBadge.razor`・`TipTags.razor` | MudChip | ✕ |
| 27 | 「本文へスキップ」・キーボード操作 | App.razor・MainLayout.razor（`skip-link`） | MudBlazor に合わせた同じリンク。`FocusOnNavigate`（Routes.razor）を継続 | ○ |
| 28 | ページ移動の拡張（nav.js） | `wwwroot/js/nav.js`（再読み込みなしの移動。試作の計測では 80 ms 前後） | Blazor の Router が代わるため**廃止**。three.js を使うページの扱いは Phase 3 で決める | — |
| 29 | 404 | `NotFoundPage.razor`・`NotFoundContent.razor`・`404.html`（`not_found_handling = "404-page"`） | 静的 `404.html`（ステータス 404、noindex）＋ Blazor の NotFound 表示 | △ |
| 30 | 旧 URL の転送 | Program.cs・`_redirects`（書き出し時） | `_redirects` をそのまま使う | — |
| 31 | 練習 1: 3D の操作練習 | `Practice.razor`・`practice.js`・`viewport.js`（約 1,940 行の Three.js） | ページの枠は Razor、中身は既存の JS を importmap と script でそのまま使う（Blazor からは起動・破棄だけ interop）。PC 専用（`practice-pc-only`） | ✕ |
| 32 | 練習 2: Unity 風の操作 | `PracticeUnity.razor`・`unity.js`（約 1,440 行）・`unity-practice.js` | 31 と同じ | ✕ |
| 33 | 練習 3: 共有ルーム | `PracticeRoom.razor`・`room.js`（約 350 行、WebSocket `/api/rooms/<ID>/ws`、名前は localStorage `avatar-modding-guide.name`）・`editmesh.js` | JS のまま。Blazor からは入室・退室だけ interop。WebSocket の接続先は変えない | ✕ |
| 34 | タッチ操作の練習ガイド | `TouchPracticeGuide.razor`・`touch-practice.js`（PC 以外の案内） | Razor＋JS（既存）。スマホでは案内だけ出す | ✕ |
| 35 | 「やってみよう」（`scenarios.js`、864 行） | 練習ページと記事内チュートリアルで共有 | JS のまま（練習の 31〜34 と一緒に扱う） | ✕ |
| 36 | 記事のページ内 script（`toc.js`・`tutorial.js`）の読み込み | ArticlePage.razor（`<script>`・importmap） | Blazor の記事ページで必要なときだけ読む（ページ移動で二重に読まない） | ✕ |

**[事実] JS の総量**: `wwwroot/js` は 22 ファイル。大きいのは `viewport.js`（1,939 行）、`unity.js`（1,437 行）、`scenarios.js`（864 行）、`editmesh.js`（789 行）、`tutorial.js`（499 行）。練習系（31〜35）で約 6,000 行あり、**Blazor に作り直すのではなく JS をそのまま残す**のが前提（Phase 3）。

### 1.3 Worker（`worker/`・`wrangler.toml`）

**[事実]** `wrangler.toml`（name `fara-avatar`、dev は `fara-avatar-dev`。`[assets]` は `./dist`、`html_handling = "auto-trailing-slash"`、`not_found_handling = "404-page"`、`run_worker_first = true`。Durable Object `ROOMS`（SQLite 版、migration v1））。

| ファイル | 役割 | 移行での扱い |
| --- | --- | --- |
| `worker/index.js`（419 行） | 全リクエストの入口。メンテナンス中は 503、`/api/rooms/<ID>/ws` を Durable Object へ、`/api/report*` を報告へ、それ以外は `env.ASSETS.fetch` の結果に `withNotice` を付ける。`/api/` 以外の未知の API は 404 | **維持**。お知らせの扱いだけ D4 で変える |
| `worker/maintenance.js`（104 行） | 503 の HTML と JSON、お知らせ帯の HTML 注入（`withNotice`） | 維持。`/api/maintenance`（JSON）を D4 の場合に足す |
| `worker/report.js`（150 行） | `/api/report`（Turnstile 検証 → Discord Webhook）と `/api/report/config`（サイトキー） | **維持**（Blazor 版の報告フォームも同じ API を呼ぶ） |
| `wrangler.toml` | 本番・確認用の routes、Durable Object、`keep_vars = true`、`workers_dev = false` | 維持。`assets.directory` は `dist` のまま |
| `wwwroot/_headers` | **無し**（tracked ファイルにない）。確認用の `X-Robots-Tag` だけ deploy.yml が `dist/_headers` に追記 | CSP を新設（Phase 4、§4.9） |
| sitemap.xml・robots.txt | **無し**（tracked ファイルに無い。ソース全体を検索して無いことを確認） | Phase 4 で新設（本番のみ sitemap） |

### 1.4 CI・デプロイ（`.github/workflows`）

**[事実] `deploy.yml`**: build ジョブで `dotnet build` → `dotnet test` → `node --test worker/report.test.mjs worker/maintenance.test.mjs` → `node --test .github/scripts/require-maintenance.test.mjs` → `node --test tests/js/nav.test.mjs` → `dotnet run -c Release --no-build --no-launch-profile -- --export dist` → artifact `site`。deploy ジョブは master（本番）と develop（確認用）のみ。`wrangler-action` のバージョンは `4.149.0`。本番は `--var MAINTENANCE:true` 付きで公開（`skip_maintenance_check` で飛ばせる）。

- 移行での変更: build ジョブの `dotnet` が WASM 用の publish と、静的 HTML 生成（ContentExport → 生成ツール）に変わる。`nav.test.mjs` は nav.js の廃止で消す。`require-maintenance` と `worker` の node テストは残す。
- `claude-feature.yml`（Issue から機能を生成する Action）は本体の配信とは独立。移行の影響なし。

### 1.5 テスト

**[事実]**
- `tests/FaraAvatarModdingGuide.Tests/`（xUnit、`[Fact]`/`[Theory]` は計 42 件）: `ChangelogTests`（5）、`FooterAndReportTests`（17）、`MaintenanceConfigTests`（12。CSS とファイルの文字列を読む形が多い）、`NavigationTests`（3）、`ScrollProgressTests`（5）。
- Node: `worker/report.test.mjs`、`worker/maintenance.test.mjs`、`.github/scripts/require-maintenance.test.mjs`、`tests/js/nav.test.mjs`。
- 手動の確認用スクリプト: `tools/nav-check/check.mjs`、`tools/room-check/check.mjs`、`tools/screenshots/`（capture.ps1・capture.py・mkprefs.py・postprocess.py）、`tools/tutorial-images/capture.mjs`。

**移行での扱い**: `MaintenanceConfigTests` の一部は「`App.razor` に bootstrap の CDN がある」「CSS のクラス名がある」といった**ファイルの文字列を見るテスト**。WASM 版では対象のファイルが変わるため、テストを書き換える（振る舞いを見るテストに替える）。`NavigationTests`・`tests/js/nav.test.mjs` は nav.js の廃止で削除する。

### 1.6 ドキュメント

**[事実]** `README.md`（技術スタックは「.NET 9 / ASP.NET Core Blazor Web App（静的 SSR）」「Bootstrap 5.3 / Bootstrap Icons（CDN）」。サイト名は `appsettings.json` の `Site:Title`）、`Content/README.md`。**CLAUDE.md は無い**（origin/develop に無い）。

### 1.7 ドキュメントと実装の差・前提の訂正

- **[事実]** ポケモン版の `tools/build-entry-pages.mjs` は、ローカルの `HEAD`（`fix/notice-sticky`）には無い。`origin/develop` と `feature/seo-entry-pages` にある。**計画はこの版を前提にする**（取り込み前に、ポケモン側の develop の最新を確認すること）。
- **[事実]** avatar には sitemap.xml・robots.txt・`_headers`（CSP）が無い。「sitemap/robots を流用する」は、新設になる。
- **[事実]** avatar の Bootstrap は CDN（`App.razor` で bootstrap 5.3.3 と bootstrap-icons 1.11.3、SRI 付き）。Bootstrap の CSS クラスは Razor の全ページで使われている（`row`、`col-lg-8`、`btn`、`card`、`breadcrumb`、`d-none d-lg-block` など）。**Bootstrap 廃止は、ページの見た目の作り直しを伴う**（§5 の見積もりに入れる）。
- **[事実]** Bootstrap Icons のクラス（`bi-*`）は、`Content/` 配下にコード上は 12 種類、C# の `Icon`/`Section`/`Category` に文字列で持つ（Models.cs）。アイコン 70 箇所前後（`grep -c "bi-"` の合計。概数）。
- **[推測]** 「ILLink の断続的な失敗」は、portfolio 側の CLAUDE.md に記録がある事象。avatar では観測されていない（CI が Linux のため）。Phase 1 の publish で同じ現象が出るかは未確認。

---

## 2. 試作（PR #69、`experiments/wasm/`）の評価

### 2.1 試作の中身（ブランチ `origin/experiment/wasm-mudblazor`、コミット `414747c`）

**[事実]**

| 部品 | 中身 | 本番へ流用できるか |
| --- | --- | --- |
| `ContentExport/` | 本体の `ContentService.cs`・`Models.cs`・`SearchIndex.cs` を **`<Compile Include="..\..\..\Data\...">`（リンク）** で取り込み、`content.json`・`search.json` を書く。画像の相対 `src` を `/learn/<cat>/...` の絶対パスに直す。索引の検索用正規化（NFKC・小文字・〜・カタカナ→ひらがな）をビルド時に済ませる | **流用の中心**。ただし `ContentService` が ASP.NET の `IWebHostEnvironment` に依存しているため、試作は `FakeEnvironment` で回避している。本番では依存を外して「ディレクトリを渡すだけの読み込み」にする（§4.1） |
| `FaraAvatarWasm/` | `App.razor`・`Layout/MainLayout.razor`・`Pages/Home|Article|Search|Practice.razor`・`Services/ContentStore.cs`（JSON を最初の一回だけ取得）・`Services/SearchMatcher.cs`（簡易検索）・`Services/ThemeState.cs`・`wwwroot/js/theme.js`・`wwwroot/css/app.css` | レイアウト・ContentStore・ThemeState は**型と考え方を流用**。Practice は three.js を入れていないため、本番では流用せず JS を残す |
| `FaraAvatarWasm/Program.cs` | `AddMudServices()`、`ContentStore`、`ThemeState` | 流用（MudBlazor の設定は、ポケモンの `MudProviders` と揃える） |
| `FaraAvatarWasm/wwwroot/index.html` | `<base href="/wasm/">`、`_content/MudBlazor/MudBlazor.min.css`、`blazor-error-ui` | 本番では `base href="/"` にする（`/wasm/` の前提を外す） |
| `measure/`（計測スクリプト）・`docs/`（スクリーンショット・`results.json`） | Edge＋playwright-core で、サイズ・表示時間・移動・SEO・CSP を計る | 計測の方法として Phase 0/4 で再利用可能 |
| `build.sh` | ContentExport → `dotnet publish` | 本番では CI に組み込む（§4.3） |

**[事実] 試作の対象範囲**: ホーム・記事・検索・練習（枠だけ）の 4 画面。`SearchMatcher` は「ローマ字の読み替え・半角カナ・強調・抜き出しは省いている」と自分で書いている。TIPS・用語集・ショートカット・リンク・更新履歴・報告・プライバシー・お知らせ・メンテナンス・サイドバー・背景 3D・スクロール線・記事内チュートリアルは**含まれない**。

### 2.2 計測結果（`docs/results.json`、Edge、各 5 回の中央値）

**[事実]** 通常（絞らない・ローカル）と遅い回線（150 ms・1.6 Mbps・CPU 4 倍）。

| 指標 | SSR（現行） | WebAssembly（試作） |
| --- | --- | --- |
| 記事の本文が見える（通常） | 37 ms | 778 ms |
| 記事の本文が見える（遅い） | 235 ms | 18,687 ms |
| load イベント（通常） | 337 ms | 161 ms |
| load イベント（遅い） | 10,381 ms | 1,273 ms |
| 配布量（通常、合計） | 1.91 MB（外部 1.11 MB） | 4.30 MB（WebAssembly 本体 2.94 MB、data 0.09 MB、MudBlazor 0.06 MB） |
| 最初の HTML の本文 | 31 KB（本文あり） | 1.4 KB（**本文なし**） |
| 移動（再読み込みなし、3 回の中央値ではなく各回の値） | nav.js で 62〜86 ms、状態維持 | Blazor の移動で 79〜80 ms。ただし、three.js の練習ページを静的に開いた状態からの移動は状態が失われる（`wasmPracticeStaticKept: lost`） |

**[事実] 圧縮後の WebAssembly 本体**: framework 3.23 MB（Brotli）、data-json 0.27 MB（Brotli）、MudBlazor 0.10 MB（Brotli）。

**読み取り**
- 速さは問題にしない、という決定があるので、表示時間の差は受け入れない理由にはしない。
- **SEO の差が決定的**。試作の HTML は本文が無い（SEO の要件そのもの）。これが §4 の静的 HTML の必要性を裏づける。
- 練習ページで状態が失われる件は、Blazor の移動で「three.js を使うページは従来どおり読み直す」と決めれば解決する（nav.js と同じ考え方）。

### 2.3 CSP（試作の計測）

**[事実]** `results.json` の `csp`（※ 計測は nav 修正の前）:
- `A-no-wasm-eval/wasm`: 描画されない（`'wasm-unsafe-eval'` が無いと WebAssembly がコンパイルできない）
- `B-wasm-eval/wasm`: 描画されるが `style-src` の違反 6 件（原因は計測の記録からは特定できない。MudBlazor の動的な style が候補【推測】）
- `C-wasm-eval-inline-style/wasm`: 違反 0 件

**[推測] 結論**: `script-src 'self' 'wasm-unsafe-eval'` は必須。`style-src` には `'unsafe-inline'` が要る（MudBlazor の動的な style）。ポケモンの `_headers` は `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` と既に同じ形（pokemon の `wwwroot/_headers`）なので、そのまま流用できる。

### 2.4 試作のコードで確かめるべき点（Phase 1 の最初に確認）

- **`string.Normalize(FormKC)`**: 試作の `ContentExport` の注釈は「ブラウザの WebAssembly では `string.Normalize` が使えない（`PlatformNotSupportedException`）」と書いている。**これは試作の作者の記述で、計画の作成時点では再現していない**。Phase 1 の最初に、WebAssembly で `Normalize` を呼ぶ最小のページで確かめる（D9）。
- 本体の `searchcore.js`・`tipsfilter.js` は `String.prototype.normalize("NFKC")` を使う（`searchcore.js:31`、`tipsfilter.js:17`）。ブラウザでは JS の normalize が常に使える。

### 2.5 PR #69 の扱い（D8）

**[事実]** PR #69 は OPEN（base `develop`、head `experiment/wasm-mudblazor`、タイトル「[試作・マージしない]」）。
**推奨**: 試作は**マージせず閉じる**（ブランチは参照用に残す）。計画の決定事項とこの評価を本文に残す。

---

## 3. 目標の構成（案）

### 3.1 プロジェクト構成（D1）

**案 A（推奨）: 並走させてから置き換える**

```
src/
  FaraAvatarModdingGuide.Content/      … 新: 記事・データの読み込み（ContentService・Models・SearchIndex を移す）。ASP.NET 非依存（ディレクトリを渡す）。Markdig・YamlDotNet を使う。ビルド時専用
  FaraAvatarModdingGuide.Web/          … 新: Blazor WebAssembly + MudBlazor（本番の画面）。Content は参照しない（JSON と静的 HTML を読む）
  FaraAvatarModdingGuide.Build/        … 新: 静的 HTML と JSON を書き出す（ContentExport と生成ツールを統合。C#）
tests/
  FaraAvatarModdingGuide.Tests/        … 既存（Phase 5 で置き換え）
  FaraAvatarModdingGuide.Web.Tests/    … 新
FaraAvatarModdingGuide.csproj（既存 SSR）… Phase 5 で削除
```

- Phase 1〜4 の間は既存の SSR（master と develop の現行）を壊さない。新しいプロジェクトは `src/` の下に作る。
- Phase 5 で既存の csproj と `Components/`・`Data/`・`Publishing/`・`wwwroot/`（JS は練習用に移す）を削除し、`Content/` だけを残す。

**案 B: 既存の csproj を WebAssembly に置き換える**
- 変更の差分が大きく、master（本番）を壊すリスクが高い。Phase 1 から master に入れられなくなるので、非推奨。

**`Content/` の扱い**: ルートの `Content/` を単一の情報源のまま残す（ポケモンと違い、記事は Markdown。移動しない）。

**ContentService の扱い**: ASP.NET（`IWebHostEnvironment`・`ILogger`）を外し、「ディレクトリのパス」と「ログの出し先（任意）」だけを受ける。試作の `FakeEnvironment` は不要になる。既存のテスト（ChangelogTests・NavigationTests 等）は、このライブラリを直接呼ぶ形に移す。

### 3.2 ルーティングと配信

- **全ページを静的 HTML として書き出す**（現行の `StaticSiteExporter` と同じ URL 一覧）。WASM は各 HTML の `#app` を引き継ぐ。
- **SPA フォールバック（index.html を全部に返す）は使わない**。理由: 存在しない URL が本文入りの 200 になり、soft 404 になる（ポケモンは `index.html` を汎用の title と読み込み表示だけにしている。`tools/build-entry-pages.mjs` の冒頭コメント）。avatar も同じ方式にする。
- **404**: 静的 `404.html`（ステータス 404、noindex）。Worker の `not_found_handling = "404-page"` は維持。
- **旧 URL**: `_redirects`（現行の `/operations`・`/avatar-clothing`・`/plugins`）をそのまま使う。
- **末尾スラッシュ**: `html_handling = "auto-trailing-slash"` を維持（`/learn/basics/` → `/learn/basics` へ）。
- **検索索引**: 現在は `/search-index.json`（Minimal API）。WASM 版では静的ファイルとして同じ URL に置く（キャッシュ・外部の参照を壊さない）。

### 3.3 静的 HTML の生成（D3）

**方式**（ポケモンの `tools/build-entry-pages.mjs` を一般化）:
1. `dotnet publish`（WebAssembly）で得た `index.html` を**テンプレート**にする（`<!-- SEO-HEAD:START/END -->`・`<!-- SEO-BODY:START/END -->` の区切り）。
2. 生成ツール（C#、`FaraAvatarModdingGuide.Build`）が、`Content` から全ページの本文を作り、テンプレートの区切りの間を書き換える。
   - **head**: `<title>`、`<meta name="description">`、`<link rel="canonical">`、OGP（`og:title`・`og:description`・`og:url`・`og:type`・`og:image`・`og:site_name`）。
   - **body**: 本文（記事の `Html`、目次、パンくず、前後の送り、区切りや一覧の要素）。WebAssembly の画面と**同じ DOM 構造と同じ CSS クラス**で作る（起動時のちらつき対策。§4.10）。
3. `robots.txt`（本番は `Sitemap:` の行。確認用は Disallow を付けず、`X-Robots-Tag: noindex` で止める。ポケモンと同じ）と `sitemap.xml`（本番のみ、全ページ）を書く。
4. `index.html`（SPA の汎用フォールバック）は title と読み込み表示だけ（ホームの本文は入れない）。

**記事 50 件・ページ数**: 現行と同じ数（約 80 ページ）。`dist` の内容の差は、差分比較で確かめる（§5 の受け入れ条件）。

**Node か C# か（D3）**: ポケモンは Node（`build-entry-pages.mjs`）。avatar は本文の HTML を Markdig（C#）で作るため、**C# で生成する**のが素直。Node の方式（テンプレートの区切り・canonical・sitemap・noindex の決まり）は、そのまま流用する。

### 3.4 WebAssembly の起動と「ちらつき」対策（§4.10 で詳述）

- 静的 HTML の本文と、WASM 起動後の画面の DOM 構造を揃える。
- head の script で、テーマ（`data-theme` 相当）とサイドバーの状態を描画前に反映する（現行の App.razor と同じ）。
- MudBlazor の CSS は head で読み込み、フォントは Google Fonts（現行と同じ）。

### 3.5 Worker の変更（§4.8）

| 項目 | 現行 | 移行後 |
| --- | --- | --- |
| `/` と記事 URL | 静的アセット（SSR の HTML） | 静的アセット（本文入りの静的 HTML）。Worker は変えない |
| メンテナンス 503 | Worker（全リクエスト） | 同じ（維持） |
| お知らせ帯 | Worker が HTML に注入（`withNotice`） | D4 で決める。推奨は「WASM が `/api/maintenance` を読む」 |
| `/api/report`・`/api/report/config` | Worker | 維持（Blazor の報告フォームが呼ぶ） |
| `/api/rooms/<ID>/ws` | Durable Object | 維持 |
| `/search-index.json` | Minimal API（Program.cs） | 静的ファイル（同じ URL） |
| `_headers` | deploy.yml が確認用の noindex を追記 | 本体の `_headers` を新設（CSP・X-Content-Type-Options 等。ポケモンと同じ形） |

---

## 4. 各項目の詳細

### 4.1 コンテンツのエクスポートと既存の Models/ContentService（§3.1 の続き）
- `ContentService` は、ビルド時の処理（Markdown → HTML、YAML の読み込み、索引の作成）を担う。WebAssembly の実行時には使わない。
- WebAssembly に送るのは JSON（`content.json`：区切り・カテゴリ・記事（HTML 含む）・ショートカット・addons・用語・Q&A・How-to・TIPS・リンク・更新履歴）と `search.json`（索引。Norm は生成時に正規化済み）。
- テスト: 既存の `ChangelogTests` 等の一部はライブラリ側に移す。新規: 「50 記事すべてが JSON に入る」「記事 URL が現行と一致する」「相対画像の URL が `/learn/...` になる」。

### 4.2 ページの置き方（MudLayout）
- MudLayout の `MudAppBar`（ハンバーガー・サイト名・検索欄・テーマ切り替え）、`MudDrawer`（目次。PC は隠せる、スマホは一時表示）、`MudMainContent`（本文・フッター）。
- ポケモンの `MainLayout.razor`・`MudProviders.razor`（`MudThemeProvider`・`MudPopoverProvider`・`MudDialogProvider`・`MudSnackbarProvider`）をほぼそのまま使う。
- 色は現行の CSS 変数（`:root`）をテーマの色に写す（D6 と合わせて決める）。

### 4.3 CI/デプロイ
1. `dotnet build`・`dotnet test`（新旧の両方。Phase 5 で旧を削除）。
2. `dotnet publish src/FaraAvatarModdingGuide.Web -c Release`（WebAssembly）。
3. `dotnet run --project src/FaraAvatarModdingGuide.Build -- --content Content --template <publish の index.html> --out dist`（静的 HTML・JSON・sitemap・robots）。
4. 確認用は `_headers` に `X-Robots-Tag: noindex, nofollow` を追記（現行と同じ）。
5. 本番（master）の MAINTENANCE=notice の確認（`require-maintenance.mjs`）は現行のまま。
6. Node テスト（report・maintenance・require-maintenance）は残す。nav.test.mjs は削除。

**ILLink（D・§5 リスク）**: publish の trim 設定を明示（`PublishTrimmed` の既定のまま、必要な型は `TrimmerRootAssembly` で守る）。CI で失敗したら再実行で済むか記録し、続くなら `TrimMode`・`IlcTrimMetadata` を見直す（推測。avatar では未観測）。

### 4.4 検索（D9）
- 索引（`search.json`）: 生成時に `Norm` を作る（NFKC・小文字・〜・カタカナ→ひらがな）。現行の `searchcore.js` の規則と同じにする。
- 検索語: ブラウザで `String.prototype.normalize("NFKC")` を JS interop で呼ぶ（推奨）。C# の `Normalize` が WebAssembly で動くなら、C# でもよい（Phase 1 で確かめる）。
- ローマ字の読み替え・半角カナ・強調・抜き出し: 現行の `searchcore.js` の仕様を Node の既存の振る舞いと比べられる形（同じ入力・同じ件数）で C# に移す。**テストは現行の searchcore の結果を正解データにする**。

### 4.5 練習ページ（Three.js の組み込み・JS interop・解放・PC 専用）
- 既存の JS（`viewport.js`・`unity.js`・`scenarios.js`・`editmesh.js`・`room.js`・`tutorial.js`・`practice.js`・`unity-practice.js`・`touch-practice.js`）は**書き換えない**。
- Blazor 側は、ページの枠（Razor）と、JS の起動・破棄だけ（`IJSRuntime` で `init(element)` / `dispose()`）。`IAsyncDisposable` で破棄を呼ぶ。
- importmap（`three` は CDN の `three@0.170.0`。現行どおり）は、練習ページを開いたときに入れる。ページ移動で三つ目の import が二重にならないよう、一度だけ登録する（nav.js の「二重登録しない」の教訓）。
- PC 専用（`practice-pc-only`）: 現行どおり、スマホではガイドだけを出す（`TouchPracticeGuide`）。
- 共有ルーム: 接続先 `/api/rooms/<ID>/ws` は変えない。入室・退室の状態だけ Blazor に持つ。名前の保存キー `avatar-modding-guide.name` は同じ。

### 4.6 報告フォーム（Blazor 版・Turnstile）
- `Report.razor` を Blazor のフォームにする。送信は `/api/report`（JSON）へ POST（ポケモンの `ReportForm.cs` と同じ形）。
- Turnstile は `wwwroot/js/report.js` と同じ方式（`challenges.cloudflare.com/turnstile/v0/api.js` を読み、トークンを受け取る）。サイトキーは `/api/report/config`。
- 送信の対策（同一ページからか、本文の長さ・種類、honeypot の `website`）は Worker の `worker/report.js` がそのまま行う。
- テスト: 既存の `FooterAndReportTests` の一部（フォームの項目・API の形）を、Blazor 版の検査に替える。

### 4.7 背景 3D・スクロール線
- `bgscene.js`（three.js）: Layout から一度だけ起動。ページ移動では止めない（Blazor の移動は画面を差し替えるだけなので、背景は残る）。`prefers-reduced-motion` の扱いは現行（bgscene.js）どおり。
- `scrollprogress.js`: 小さな JS interop か CSS（`animation-timeline: scroll()`）。読み上げには出さない。
- いずれも Phase 1 で移し、Phase 3 の練習ページと干渉しないことを確かめる（three.js の renderer が 2 つ動く状態の確認）。

### 4.8 Worker（お知らせ帯・メンテナンス・API）（D4）
- **維持するもの**: 503（`getMaintenance` → `maintenanceResponse`）、`/api/report*`、`/api/rooms/*`、`run_worker_first = true`。
- **お知らせ帯（D4）**:
  - A. 現状どおり Worker が HTML に注入（本文入りの静的 HTML にも注入される。ちらつきなし。JS 無しでも出る）
  - B. WASM が `/api/maintenance`（新設・JSON: `state`、`message`、`notice`）を読んで MudBlazor の Alert で出す（起動後に出るので、数秒は出ない）
  - **推奨: B**（ユーザーの指示どおり）。ただし、静的 HTML の注入は消さない（B の前に A を残すと二重になるので、どちらか一方にする）。B にすると `worker/maintenance.js` の HTML 注入（`withNotice`）と、そのテスト（`maintenance.test.mjs` の一部）を削除する。
- `_headers` を新設（CSP は §4.9）。

### 4.9 CSP（ポケモンの `_headers` を基準）
**[事実] ポケモンの `_headers`**: `default-src 'self'`、`script-src 'self' 'wasm-unsafe-eval' https://static.cloudflareinsights.com https://challenges.cloudflare.com`、`style-src 'self' 'unsafe-inline' https://fonts.googleapis.com`、`font-src 'self' https://fonts.gstatic.com`、`img-src 'self' data:`、`connect-src 'self' https://cloudflareinsights.com https://challenges.cloudflare.com`、`frame-src https://challenges.cloudflare.com`、`object-src 'none'`、`base-uri 'self'`、`form-action 'self'`、`frame-ancestors 'none'`。

**avatar の追加分（推測。要確認）**:
- `script-src`: three.js の CDN（`https://cdn.jsdelivr.net`）。importmap の module は CSP の対象になる。
- `style-src`: Bootstrap（削除する）・Google Fonts（現行どおり）。`'unsafe-inline'` は MudBlazor のため。
- `connect-src`: `'self'`（`/api/rooms/*/ws` は同じオリジン。WebSocket は `wss:` の扱いを確かめる）。
- `img-src`: `'self' data:`（記事の画像は同じオリジン）。
- `_headers` は全ページに同じ CSP が付く（pokemon の注記どおり）。`/api/*` には付かない（Worker 側の応答）。

### 4.10 起動時のちらつき対策
- 静的 HTML の本文と WASM の描画は、**同じ DOM 構造・同じクラス**にする（MudBlazor のクラスを静的 HTML にも付ける）。
- WASM 起動前は静的 HTML の本文が見えている（SEO と JS 無しの読みやすさ）。起動後に Blazor が `#app` の中身を置き換えるので、見た目が変わらないことを検証する。
- 検証: 起動の前後で、スクリーンショットの差（ピクセル差）を記事 5 ページ・ホーム・検索で取る。差が閾値を超えるページは静的 HTML の CSS を直す。
- 起動時に `#app` を空にしない（静的 HTML を残す）。ポケモンの `MaintenanceScreen` 方式（WASM だけで画面を作る）は avatar では採らない。

### 4.11 その他の決まり（Bootstrap 廃止、nav.js 廃止、Icons）
- **Bootstrap 廃止**（MudBlazor に統一）: 現行 `site.css`（約 3,260 行）のうち、MudBlazor の既定と重なる部分を捨て、記事本文（`.article-body` 相当）・目次・パンくず・テーブル・コードのスタイルだけ残す（必要な共通 CSS のみ）。
- **nav.js 廃止**: Blazor の Router が移動を担う（§1.2 の 28）。テスト `tests/js/nav.test.mjs` と `NavigationTests` は削除。
- **アイコン（D6）**: Bootstrap Icons（`bi-*`）は、MudBlazor の Material アイコンへ写す（推奨）。写せないものは SVG を `wwwroot/images/` に置く。

---

## 5. 段階（フェーズ）と PR の分割

**見積もりは推測（1 人＋ AI 支援、1 人日 = 実作業 6 時間の想定）。** 実際は記事 50 件の目視確認とネイティブの検証で増える。

### Phase 0: 計画（この PR）— 約 1 日（推測）
- PR: `docs/wasm-migration-plan` → `develop`（マージしない）。成果: 本文書。

### Phase 1: 土台 — 約 10〜14 人日（推測）
- 内容: `src/FaraAvatarModdingGuide.Content`（ContentService 移設、ASP.NET 非依存）、`src/FaraAvatarModdingGuide.Web`（WASM＋MudBlazor、MudLayout、テーマ、サイドバー、ヘッダー、フッター、404、お知らせ（D4 の方式）、報告フォーム、プライバシー、更新履歴、リンク集、ホーム）、Normalize の検証（§2.4）。
- 含まないもの: 記事（Phase 2）、練習（Phase 3）、静的 HTML（Phase 4）。
- 完了条件: 上の各ページが `dotnet run` で表示され、`dotnet test`（新）が通る。報告フォームが確認用の API で送信できる（Turnstile は確認用キー）。
- 並行: Content ライブラリの移設と、MudLayout の作業は並行できる。

### Phase 2: 記事 — 約 12〜18 人日（推測）
- 内容: 記事ページ（本文・目次・パンくず・前後の送り・ショートカット表示・進捗の記録）、区切り・カテゴリの一覧、検索（/search・ヘッダー・本文上の候補）、TIPS・用語集・Q&A・How-to（絞り込み）、ショートカット一覧、addons、ProgressSummary。
- 完了条件（受け入れテスト）:
  - 記事 50 件すべてで、本文のテキスト（改行・空白を除いて）が現行 SSR 版と一致する。
  - 検索: `searchcore.js` と同じ入力（検索語 100 件程度）で、件数と上位 10 件の順が一致する。
  - 進捗: 既存の localStorage（`avatar-modding-guide.progress`）の値が、そのまま読める。
- 並行: 検索（§4.4）と TIPS 系は、記事ページと別に進められる。

### Phase 3: 練習 — 約 8〜12 人日（推測）
- 内容: 練習 1・2・3（`/practice`・`/practice/unity`・`/practice/room`）、記事内チュートリアル（`::: practice`）、タッチ案内、背景 3D（bgscene.js）・スクロール線の組み込みと破棄の確認。
- 完了条件: 3 つの練習ページで、現行版と同じ操作（キー・マウス）ができる。ページ移動のあとも three.js の renderer が増えない（メモリ・リスナーの確認）。共有ルームで二人が接続できる（`tools/room-check` の再利用）。
- 並行: Phase 2 の途中から JS 部分（interop の作成）を始められる。

### Phase 4: SEO — 約 5〜8 人日（推測）
- 内容: `Build` プロジェクトの静的 HTML 生成（本文・head・OGP・canonical）、`sitemap.xml`・`robots.txt`、`_headers`（CSP）、Worker の `/api/maintenance`（D4 B）、起動時のちらつき検証（§4.10）、確認用の noindex。
- 完了条件:
  - 約 80 ページすべてに、本文が入った HTML が出る（JS 無しで読める）。
  - ページごとの title・description・canonical・OGP がある。`sitemap.xml` は本番だけ。確認用は `X-Robots-Tag: noindex`。
  - 起動の前後のスクリーンショット差が、閾値以下（記事 5・ホーム・検索）。
  - CSP で違反が 0（ブラウザの違反ログ）。
- 並行: 生成ツールは Phase 2 の途中から作れる（記事の HTML が出来た時点で）。

### Phase 5: 切り替え — 約 4〜6 人日（推測）
- 内容: CI/デプロイを WASM 版に変える、旧 SSR のコード（`Components/`・`Data/`・`Publishing/`・`wwwroot/js/` の不要分・`FaraAvatarModdingGuide.csproj`）を削除、テストの置き換え、README・Content/README の更新、Obsidian（ユーザーの運用ルールに従う）。
- 完了条件: develop の確認用（avatar-dev）で、§6 の受け入れ確認が全部通る。master への PR はユーザーがマージする（§6）。

**合計: 約 40〜60 人日（推測）**。Phase 3 と Phase 4 は、Phase 2 の後半から並行できる。

### 依存関係

```
P0 → P1 → P2 ─┬→ P4 → P5
        └→ P3 ┘
```

- P1 の Normalize 検証（§2.4）が通らないと、P2 の検索の方式（D9）が決まらない。**P1 の最初に確かめる**。
- P3 は P2 の記事ページの骨格が要る（記事内チュートリアルの埋め込み）。

---

## 6. リリース戦略（D7）

1. **develop に段階的に入れる**: Phase ごとに PR を develop へ。dev サイト（avatar-dev）で確認する。確認は Cloudflare Access の内側（自分だけ）。
2. **master は触らない（移行の完了まで）**: 本番（avatar.fara-labs.com）は現行の SSR のまま。途中の master を壊さないため、**移行が完了（Phase 5 の確認が通る）するまで master へは入れない**。
3. **master への合流**: develop → master の PR はユーザーがマージする（Claude は PR の作成まで）。deploy.yml の本番の確認（MAINTENANCE=notice）は、そのまま動く。
4. **旧版との並行期間**: 本番は SSR のまま。dev で WASM 版を確認する期間を置く。切り替えの日は、お知らせ（notice）を出してから行う（現行の運用どおり）。
5. **移行後の URL**: 変更しない（同じ URL、同じ本文）。旧 URL の転送（`_redirects`）も残す。
6. **ロールバック**: master は SSR のまま、切り替えの PR を revert すれば戻せる。

---

## 7. リスクと対策

| リスク | 影響 | 対策 |
| --- | --- | --- |
| 静的 HTML と WASM の画面の差（ちらつき・レイアウトずれ） | 見た目・CLS | 同じ DOM とクラス、起動前後のピクセル差の検証（§4.10） |
| SEO（移行で検索順位が落ちる） | 流入 | URL を変えない、本文を同じ量で出す（テキスト一致の検証）、sitemap の新設 |
| WebAssembly の配布量（約 4.3 MB。Brotli で約 3.5 MB） | 初回表示の遅さ | 速さは決定事項として問題にしない。記録だけ残す |
| 検索の正規化（`Normalize`） | 検索結果のずれ | JS の `normalize` を使う（D9）、テストは現行 searchcore の結果を正解にする |
| Three.js と Blazor の共存（破棄漏れ・二重起動） | メモリ・不具合 | `IAsyncDisposable` で破棄、ページ移動のテスト、renderer の数の確認 |
| CSP（style の `unsafe-inline`） | セキュリティの弱まり | 必要な範囲だけ（script は self と wasm-unsafe-eval のみ）。試作の計測で確認済み |
| Bootstrap 廃止による見た目の差 | 既存の読者の印象 | スクリーンショットの比較（PC・スマホ、ライト・ダーク）を Phase 2・4 で行う |
| ILLink の失敗（断続的） | CI の失敗 | 再実行で済むか記録、設定の明示（§4.3）。avatar では未観測（推測） |
| localStorage のキー変更 | 既存の進捗が消える | キー名を変えない（§1.2 の 13・2・3） |
| お知らせの二重表示（注入とクライアント表示） | 見た目 | D4 で一方に決める |
| 移行中の master の不整合 | 本番障害 | master は移行完了まで触らない（§6） |
| ポケモン側の build-entry-pages の版違い | 方式の食い違い | 取り込み時に、ポケモンの develop の最新を確認 |

---

## 8. 完了条件（全体の受け入れ確認）

- [ ] 現行の全 URL（§1.1 の一覧 + 記事 50 件）が、WASM 版で同じ URL に表示される（404 は 404 のまま）
- [ ] 旧 URL 3 件が 301 で転送される
- [ ] 記事 50 件の本文テキストが、現行 SSR 版と一致する（改行・空白を除く）
- [ ] 静的 HTML は約 80 ページ全部が、JS 無しで本文を読める
- [ ] title・description・canonical・OGP が全ページにある。sitemap.xml（本番）・robots.txt がある。確認用は noindex
- [ ] 検索の件数・順序が現行と一致する（テストデータ 100 件程度）
- [ ] 進捗の記録（既存の localStorage）が読める
- [ ] 練習 3 ページ・記事内チュートリアル・共有ルームが、現行と同じ操作で動く（PC）。スマホは案内が出る
- [ ] 報告フォームが Turnstile 付きで送信できる（確認用）
- [ ] お知らせ（notice）と 503（メンテナンス）が、現行と同じ条件で出る
- [ ] CSP の違反が 0（主要ページ）
- [ ] ライト・ダーク、PC・スマホで、主要ページのスクリーンショットに大きな差がない（ユーザーの目視確認）
- [ ] CI（build・test・node・deploy の確認）が通る。旧テスト（nav.js 等）は削除済み
- [ ] README・Content/README が WASM 版の内容になっている

---

## 9. ユーザーに決めてほしい点（D1〜D10）

| # | 決めること | 選択肢 | 推奨と理由 |
| --- | --- | --- | --- |
| D1 | プロジェクト配置 | A: `src/` に新規を並走させ、Phase 5 で置き換え／B: 既存の csproj を置き換え | **A**（master を壊さない） |
| D2 | 記事本文のデータ | (a) 全記事の HTML を 1 つの JSON に入れる（約 1.7 MB・Brotli 約 0.27 MB 〈試作の実測〉）／(b) 記事ごとに HTML 断片を別ファイルにして開いたときだけ取る | **(a)**（速さは問題でないので、構造が単純な (a)） |
| D3 | 静的 HTML の生成 | (a) C#（`FaraAvatarModdingGuide.Build`、Markdig をそのまま使う）／(b) Node（ポケモン流 `build-entry-pages.mjs`、本文は C# の JSON を入力） | **(a)**（本文の HTML は Markdig で作るため、同じ C# で作るのが単純） |
| D4 | お知らせ帯 | (a) Worker が注入（現状）／(b) WASM が `/api/maintenance` を読む | **(b)**（指示どおり。注入は削除。起動後まで出ない点は許容） |
| D5 | OGP 画像 | (a) 新しく 1200×630 の PNG を作る／(b) `site-icon.svg` を流用（SVG は多くの SNS で表示されない） | **(a)**（PNG を 1 枚作る。デザインは要確認） |
| D6 | アイコンと Bootstrap | (a) Bootstrap Icons を捨てて Material（MudBlazor）に写す／(b) bootstrap-icons の必要な分だけ SVG で同梱 | **(a)**（Bootstrap を完全に外す方針と合う） |
| D7 | 旧版との並行 | (a) 移行完了まで master は SSR のまま（dev だけ WASM）／(b) 段階ごとに master へも入れる | **(a)**（§6） |
| D8 | 試作 PR #69 | (a) 閉じる（参照用ブランチは残す）／(b) 残す | **(a)**（本文書に評価を残す） |
| D9 | 検索の正規化 | (a) JS interop の `normalize("NFKC")`／(b) C# の `Normalize`（Phase 1 で WebAssembly 上の動作を確認してから） | **(a)**（確実に動く。C# 側は索引だけ生成時に正規化） |
| D10 | 進め方の人数・順序 | (a) 1 人で P1→P5 を順に／(b) P3 と P4 を並行（人数が増えたとき） | **(a)** を基本に、P3 と P4 は後半で並行可 |

**追加で確認したいこと（D に入れない）**
- CSP の `style-src 'unsafe-inline'` を許すか（§2.3）。**推奨: 許す**（MudBlazor の要件。script は self と wasm-unsafe-eval のみ）。
- Phase 5 の後の CLAUDE.md の扱い（avatar には無い。作るかどうか）。

---

## 付録 A: 確認に使ったファイル（抜粋）

- avatar（`origin/develop` 3340302）: `Program.cs`、`Components/App.razor`・`Routes.razor`・`Layout/MainLayout.razor`、`Components/Pages/**`、`Components/Shared/**`、`Data/ContentService.cs`・`Models.cs`・`SearchIndex.cs`、`Publishing/StaticSiteExporter.cs`、`worker/index.js`・`maintenance.js`・`report.js`、`wrangler.toml`、`.github/workflows/deploy.yml`、`README.md`、`wwwroot/js/*.js`（先頭と規模）
- avatar 試作（`origin/experiment/wasm-mudblazor` 414747c）: `experiments/wasm/ContentExport/*`、`FaraAvatarWasm/*`、`build.sh`、`measure/measure.mjs`、`docs/results.json`
- ポケモン（`origin/develop` の `feature/seo-entry-pages` 由来）: `tools/build-entry-pages.mjs`、`worker/home.js`、`src/FaraPokemonBattleAssistant.Web/` の `Program.cs`・`Shared/MudProviders.razor`・`Shared/MainLayout.razor`・`Services/ReportForm.cs`・`MaintenanceScreen.razor`・`wwwroot/_headers`
- avatar PR #69: OPEN、base `develop`

## 付録 B: 用語

- **静的 HTML（事前生成）**: ビルド時に作る、ページごとの HTML。本文が入っている。
- **#app の引き継ぎ**: 静的 HTML の `#app` の中身を、WebAssembly の起動で置き換えること。
