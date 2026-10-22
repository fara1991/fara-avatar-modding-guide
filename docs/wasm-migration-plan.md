# avatar の Blazor WebAssembly + MudBlazor 移行計画

- 対象: `fara-avatar-modding-guide`（本番 https://avatar.fara-labs.com / 確認用 https://avatar-dev.fara-labs.com）
- 基準: `origin/develop` = `3340302`（PR #67 のマージ後）。試作は `origin/experiment/wasm-mudblazor` = `414747c`（PR #69、develop 向け、マージしない。D8 で閉じる）
- 参照: `fara-pokemon-battle-assistant`（同じ方式で公開済み。ローカルの `HEAD` は `fix/notice-sticky` のため、`tools/build-entry-pages.mjs` などは `origin/develop` 側（`feature/seo-entry-pages` 由来、コミット `6f6b0a8` / `dca32fe`）を見た）
- 共通部品: `fara1991/fara-site-kit`（作成済み・public。§10）
- この文書は計画と棚卸しだけ。実装はしない。

表記: **[事実]** はリポジトリ・試作・計測ファイルで確認したもの、**[推測]** は見積もりや判断。

---

## 0. 決定事項と、この計画の前提

| 項目 | 決定 | 計画での扱い |
| --- | --- | --- |
| UI の土台 | Blazor WebAssembly + MudBlazor（ポケモンに合わせる） | 採用 |
| 速さ | 問題にしない（回線の遅さは考慮しない） | 計測は参考値として記録。性能は受け入れ条件にしない |
| SEO | ポケモンと同じ方式。ビルド時にページごとの静的 HTML を作り、読み込み後に WebAssembly が引き継ぐ | 採用 |
| 静的 HTML の中身 | 要約ではなく**本文入り**。記事ごとに自動生成する | 採用（現行 SSR 版と同等の本文を出す）。生成は **Phase 1 で前倒し**（§5） |
| お知らせ帯（D4） | **決定**: 注入をやめ、WASM が起動時に Worker の `/api/maintenance` を読んで表示する（Worker の HTML 注入 `withNotice` は削除。Phase 1 で行う） | 採用（§4.8） |
| 共通部品 | 新リポジトリ `fara-site-kit`（submodule で利用） | 採用（§10） |
| ブランチ戦略（D14） | **未決（ユーザーの選択待ち）** | 選択肢と推奨は §6・§9 |

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

**[事実] 静的 HTML の数**（現行の `StaticSiteExporter` の書き出し一覧）: 固定ページ 16（`/` と `/learn` を含む）＋ カテゴリ 10 ＋ 記事 50 ＝ **76 本**。これに `404.html`・`search-index.json`・`_redirects` が加わる。

**[事実] 記事 URL は 50 件**（`Content/learn/**/*.md`）。画像・動画は `Content/learn/<category>/images/` から `/learn/...` として配信（Program.cs の `mediaOnly` 判定。`StaticSiteExporter.IsMedia` と同じ）。Content 配下の画像は **51 枚（すべて PNG）**。

**[事実] 静的ファイル**: `wwwroot/css/site.css`（約 3,260 行、Bootstrap と併用）、`wwwroot/js/*.js` **21 本**、`wwwroot/images/`（SVG 2 本、チュートリアル画像 **WebP 145 枚**（`wwwroot/images/tutorials/`））。

### 1.2 機能の棚卸し（WebAssembly 版での実現・ポケモン版からの流用）

凡例 — 流用: ◎ そのまま近い / ○ 型・パターンを参考 / △ 考え方のみ / ✕ 流用不可。

| # | 機能 | 現行の実現（ファイル） | WebAssembly 版での実現 | ポケモン版から流用 |
| --- | --- | --- | --- | --- |
| 1 | ページ全体のレイアウト | `Components/App.razor`・`Layout/MainLayout.razor`（PC ヘッダー＋サイドバー＋本文＋フッター） | MudLayout（MudAppBar・MudDrawer・MudMainContent）。サイドバーは「PC で隠せる・ホバーで広がる」挙動を MudDrawer の Variant と CSS で再現 | ○ pokemon `Shared/MainLayout.razor`（Mini ドロワー）・`Shared/MudProviders.razor`（確認済み） |
| 2 | テーマ（ダーク／ライト） | App.razor のインライン script（描画前に `data-bs-theme` を設定。キー `avatar-modding-guide.theme`）・`ThemeSwitch.razor`・`sidebar.js` | MudThemeProvider の `IsDarkMode`。描画前の反映は外部ファイル `js/theme-init.js`（§4.9）。キーは同じ `avatar-modding-guide.theme` を使い、既存の保存値を引き継ぐ | ○ AppState のテーマ連携（`AppState.cs` の注記）、MudProviders |
| 3 | サイドバー（目次メニュー）の開閉 | `NavMenu.razor`・`sidebar.js`（キー `avatar-modding-guide.sidebar-hidden`） | MudDrawer＋ローカル保存（同じキー）。スマホは上部メニューから | ○ pokemon `Shared/NavMenu.razor`（MudNavMenu。PC は Mini ドロワーに入れ、隠すとアイコンだけになる。確認済み） |
| 4 | ヘッダーの検索欄・本文上の検索欄 | `QuickSearch.razor`・`quicksearch.js`・`searchcore.js` | Razor の検索欄＋候補。候補の計算は C#（索引は JSON） | △ |
| 5 | サイト内検索（/search） | `Search.razor`・`search.js`・`searchcore.js`（NFKC・小文字・〜・カタカナ→ひらがな・ローマ字の読み替え・強調・抜粋） | 索引の正規化はビルド時（Build が NFKC 済みの文字列を出す）。検索語の正規化は D9 の方式。ローマ字・強調・抜粋は C# へ移植して Node 版と同じ入力で比べる | △ `Services/SearchService`（考え方のみ） |
| 6 | ショートカット一覧 | `Shortcuts.razor`・`shortcuts.js`・`shortcuts.yml` | Razor（絞り込みは MudSelect/MudChip の状態） | ✕ |
| 7 | TIPS・Q&A・用語集・How-to の絞り込み | `TipsFilter.razor`・`tipsfilter.js`（NFKC の正規化を JS で実施） | Razor の状態で絞り込み。正規化は interop（検索と同じ） | ✕ |
| 8 | 記事の目次（PC 右列・スマホは折りたたみ）と現在地の強調 | `ArticlePage.razor`・`toc.js`（スクロールに合わせた強調） | Razor で目次を出す。現在地の強調は IntersectionObserver を JS interop で（小さな JS） | ✕ |
| 9 | パンくず | `ArticlePage.razor`（目次 → 区切り → カテゴリ → 記事） | MudBreadcrumbs または現行の `<ol class="breadcrumb">` 相当の Razor | △ |
| 10 | 前後の記事への送り | `ArticlePage.razor`（`GetNeighbors`） | Razor（ContentService の GetNeighbors を移植） | ✕ |
| 11 | 記事本文（Markdown → HTML） | ContentService（Markdig）→ `Html` | Build が Markdig で HTML 化し、JSON（`content.json`）と静的 HTML に入れる。WASM 側は Markdig を持たない（D2） | ✕（ポケモンは Markdown を使わない） |
| 12 | 記事内の「この記事で使うキー」 | `ArticlePage.razor`（shortcuts 参照） | Razor | ✕ |
| 13 | 進捗の記録（読み終えた記事・件数・「続きを読む」） | `progress.js`・`ProgressSummary.razor`（キー `avatar-modding-guide.progress`）・`tutorial.js` 側の `avatar-modding-guide.tutorials` | Blazor 側の状態＋ localStorage。**キー名と値の形式を変えない**（既存の記録が消えないように）。`ProgressSummary` は Razor に | △（`AppState`/`CloudSync` の localStorage 保存の型） |
| 14 | 記事内チュートリアル（`::: practice <scenario>`） | `Data/ContentService.cs`（`practice-embed` div）・`tutorial.js`（three.js・importmap） | 記事の静的 HTML にはプレースホルダを残し、記事を開いたあとに JS（three.js）を interop で起動。JS 無しでも本文は読める | ✕ |
| 15 | 背景の 3D | `wwwroot/js/bgscene.js`（three.js、全ページ） | JS モジュールを Layout から interop で起動・停止。ページ移動では止めない（破棄しない） | ✕ |
| 16 | スクロール進捗線（画面上部） | `scrollprogress.js`・CSS `.scroll-progress` | 小さな JS interop、または CSS（scroll-driven）。読み上げには出さない（aria-hidden） | ✕ |
| 17 | お知らせ帯（更新予定） | Worker `withNotice`（HTML に注入。`worker/maintenance.js`） | **決定（§0・§4.8）**: WASM が起動時に `GET /api/maintenance` を読み、表示する（Phase 1） | ◎ pokemon `Services/MaintenanceInfo.cs`（起動時に `GET api/maintenance`、2 秒で打ち切り、読めなければ通常）・`Shared/MaintenanceBanner.razor`（Notice のときだけ出す）・Worker の `/api/maintenance`（pokemon `worker/index.js` に同じ形。確認済み） |
| 18 | メンテナンス画面（503） | Worker（`getMaintenance` → `maintenanceResponse`。`MAINTENANCE=on`） | Worker のまま（全リクエストを 503 にする）。WASM 側の画面は不要 | △（`MaintenanceScreen.razor` は WASM 内の表示。avatar は Worker で止めるので原則不要） |
| 19 | 問い合わせ・報告（`/report`） | `Report.razor`・`report.js`（Turnstile）・`worker/report.js`（Discord 転送、`/api/report`、`/api/report/config`） | Blazor のフォームで `/api/report` に POST。Turnstile は JS（`challenges.cloudflare.com`）を interop で描画しトークンを受け取る。Worker は変えない | ◎ pokemon `Services/ReportForm.cs`（入力の検査。サーバーと同じ上限）・`Components/Pages/Report.razor`（HttpClient と JS で Turnstile）・`wwwroot/js/report.js`・`worker/report.js`（同じ API 形）。いずれも確認済み |
| 20 | プライバシーポリシー | `Privacy.razor` | Razor（本文は移す） | ○ |
| 21 | 更新履歴 | `Changelog.razor`・`changelog.yml`・`ChangelogTests` | Razor（ContentService の ParseChangelog を流用） | ○ `Services/Changelog.cs`（考え方） |
| 22 | リンク集 | `Links.razor`・`links.yml` | Razor | ✕ |
| 23 | 用語集・How-to・Q&A | `Tips/Glossary.razor` など・`tips/*.yml` | Razor | ✕ |
| 24 | アドオン | `Addons.razor`・`addons.yml` | Razor | ✕ |
| 25 | 区切り（Blender / Unity / VRChat …）の目次 | `LearnIndex.razor`・`ArticleCard.razor`・`Roadmap.razor` | Razor。アイコンは D6 | ✕ |
| 26 | 難易度・タグ表示 | `DifficultyBadge.razor`・`TipTags.razor` | MudChip | ✕ |
| 27 | 「本文へスキップ」・キーボード操作 | App.razor・MainLayout.razor（`skip-link`） | MudBlazor に合わせた同じリンク。`FocusOnNavigate`（Routes.razor）を継続 | ○ |
| 28 | ページ移動の拡張（nav.js） | `wwwroot/js/nav.js`（再読み込みなしの移動。試作の計測では 80 ms 前後） | Blazor の Router が代わるため**廃止**。three.js を使うページの扱いは Phase 3 で決める | — |
| 29 | 404 | `NotFoundPage.razor`・`NotFoundContent.razor`・`404.html`（`not_found_handling = "404-page"`） | 静的 `404.html`（ステータス 404、noindex）を Build が生成（§3.3）＋ Blazor の NotFound 表示 | △ |
| 30 | 旧 URL の転送 | Program.cs・`_redirects`（`StaticSiteExporter` が書き出し時に作る） | `_redirects` は **Build が生成**する処理に移す（Phase 1） | — |
| 31 | 練習 1: 3D の操作練習 | `Practice.razor`・`practice.js`・`viewport.js`（約 1,940 行の Three.js） | ページの枠は Razor、中身は既存の JS を importmap と script でそのまま使う（Blazor からは起動・破棄だけ interop）。PC 専用（`practice-pc-only`） | ✕ |
| 32 | 練習 2: Unity 風の操作 | `PracticeUnity.razor`・`unity.js`（約 1,440 行）・`unity-practice.js` | 31 と同じ | ✕ |
| 33 | 練習 3: 共有ルーム | `PracticeRoom.razor`・`room.js`（約 350 行、WebSocket `/api/rooms/<ID>/ws`、名前は localStorage `avatar-modding-guide.name`）・`editmesh.js` | JS のまま。Blazor からは入室・退室だけ interop。WebSocket の接続先は変えない | ✕ |
| 34 | タッチ操作の練習ガイド | `TouchPracticeGuide.razor`・`touch-practice.js`（PC 以外の案内） | Razor＋JS（既存）。スマホでは案内だけ出す | ✕ |
| 35 | 「やってみよう」（`scenarios.js`、864 行） | 練習ページと記事内チュートリアルで共有 | JS のまま（練習の 31〜34 と一緒に扱う） | ✕ |
| 36 | 記事のページ内 script（`toc.js`・`tutorial.js`）の読み込み | ArticlePage.razor（`<script>`・importmap） | Blazor の記事ページで必要なときだけ読む（ページ移動で二重に読まない） | ✕ |
| 37 | フッター（SNS アイコン・© 表記・リンク） | `Components/Layout/SiteFooter.razor`・`SiteFooterLinks.cs`（SNS は `appsettings.json` の `Site:Socials`） | MudBlazor の Razor。SNS の並びと文言は `Site` 設定から読む | ○ pokemon の `MainLayout` の下部（確認済み）・kit の Razor（§10） |
| 38 | 報告へのリンクの `?from=`（見ていたページのパスを付ける） | `SiteFooterLinks.ReportHref`・`SiteFooter.razor`（問い合わせ・プライバシーからは付けない） | Razor 側で同じ規則を移す。**受け取り側（`report.js`・`Report.razor`）が `from` を読んでいるかは未確認**（K0 か P1 で確認し、必要なら受け取りを足す） | ✕ |
| 39 | サイト設定（`appsettings.json` の `Site`: Title・BlenderVersion・UnityVersion・RepositoryUrl・PublishedYear・PortfolioUrl・Socials） | `appsettings.json` → `SiteOptions`（DI） | WASM では `appsettings` を使わない。**移行先: `wwwroot/data/site.json`（Build が出す）＝ ビルド時に読み、`SiteOptions` に入れる**（サイト名は kit の `SiteOptions`） | ○ pokemon の `SiteLinks`／設定の型 |

**[事実] JS の総量**: `wwwroot/js` は **21 ファイル**。大きいのは `viewport.js`（1,939 行）、`unity.js`（1,437 行）、`scenarios.js`（864 行）、`editmesh.js`（789 行）、`tutorial.js`（499 行）。練習系（31〜35）で約 6,000 行あり、**Blazor に作り直すのではなく JS をそのまま残す**のが前提（Phase 3）。

### 1.3 Worker（`worker/`・`wrangler.toml`）

**[事実]** `wrangler.toml`（name `fara-avatar`、dev は `fara-avatar-dev`。`[assets]` は `./dist`、`html_handling = "auto-trailing-slash"`、`not_found_handling = "404-page"`、`run_worker_first = true`。Durable Object `ROOMS`（SQLite 版、migration v1））。

| ファイル | 役割 | 移行での扱い |
| --- | --- | --- |
| `worker/index.js`（419 行） | 全リクエストの入口。メンテナンス中は 503、`/api/rooms/<ID>/ws` を Durable Object へ、`/api/report*` を報告へ、それ以外は `env.ASSETS.fetch` の結果に `withNotice` を付ける。未知の `/api/*` は 404 | **維持**。`withNotice` の注入は削除し、`/api/maintenance` を足す（Phase 1） |
| `worker/maintenance.js`（104 行） | 503 の HTML と JSON、お知らせ帯の HTML 注入（`withNotice`・`noticeHtml`） | 維持（503 の部分）。注入の部分は削除（Phase 1） |
| `worker/report.js`（150 行） | `/api/report`（Turnstile 検証 → Discord Webhook）と `/api/report/config`（サイトキー） | **維持**（Blazor 版の報告フォームも同じ API を呼ぶ）。kit へ移す（K0） |
| `wrangler.toml` | 本番・確認用の routes、Durable Object、`keep_vars = true`、`workers_dev = false`、冒頭のコメント | 維持。`assets.directory` は `dist` のまま。冒頭のコメントは Phase 5 で更新 |
| `wwwroot/_headers` | **無し**（tracked ファイルにない）。確認用の `X-Robots-Tag` だけ deploy.yml が `dist/_headers` に追記（`cat >>`。前のファイルの末尾に改行が無いと行が壊れる） | CSP を新設（Phase 1 の Build が出す。§4.9） |
| sitemap.xml・robots.txt | **無し**（tracked ファイルに無い） | Build が出す（sitemap は本番のみ。Phase 4 で仕上げ） |

### 1.4 CI・デプロイ（`.github/workflows`）

**[事実] `deploy.yml`**: build ジョブで `dotnet build` → `dotnet test` → `node --test worker/report.test.mjs worker/maintenance.test.mjs` → `node --test .github/scripts/require-maintenance.test.mjs` → `node --test tests/js/nav.test.mjs` → `dotnet run -c Release --no-build --no-launch-profile -- --export dist` → artifact `site`。deploy ジョブは master（本番）と develop（確認用）のみ。`wrangler-action` のバージョンは `4.149.0`。本番は `--var MAINTENANCE:true` 付きで公開（`skip_maintenance_check` で飛ばせる）。`actions/checkout@v4` に `submodules` の指定は無い。

- 移行での変更: build ジョブの `dotnet` が WASM 用の publish と、静的 HTML 生成（Build）に変わる。`nav.test.mjs` は nav.js の廃止で消す。`require-maintenance` と `worker` の node テストは残す。**kit を submodule にするため、build と deploy の両ジョブの checkout に `submodules: true` を足す**（§8）。
- `claude-feature.yml`（Issue から機能を生成する Action）は本体の配信とは独立。移行の影響なし。

### 1.5 テスト

**[事実]**
- `tests/FaraAvatarModdingGuide.Tests/`（xUnit、`[Fact]`/`[Theory]` は計 42 件）: `ChangelogTests`（5）、`FooterAndReportTests`（17）、`MaintenanceConfigTests`（12。CSS とファイルの文字列を読む形が多い）、`NavigationTests`（3）、`ScrollProgressTests`（5）。
- `ScrollProgressTests` は、`.scroll-progress` の CSS 規則・MainLayout の要素・`scrollprogress.js` の中身を**文字列で**検査している（実際の動きは Playwright で確認する、と本文に書いてある）。
- Node: `worker/report.test.mjs`、`worker/maintenance.test.mjs`、`.github/scripts/require-maintenance.test.mjs`、`tests/js/nav.test.mjs`。
- 手動の確認用スクリプト: `tools/nav-check/check.mjs`、`tools/room-check/check.mjs`、`tools/screenshots/`（capture.ps1・capture.py・mkprefs.py・postprocess.py）、`tools/tutorial-images/capture.mjs`。

**移行での扱い**:
- `MaintenanceConfigTests` の一部は「`App.razor` に bootstrap の CDN がある」「CSS のクラス名がある」といった**ファイルの文字列を見るテスト**。WASM 版では対象のファイルが変わるため、振る舞いを見るテストに替える（P1）。
- `ScrollProgressTests`（5 件）: 文字列検査は WASM 版では意味が変わるため、**Blazor 版の検査に置き換える**（P1。CSS の規則の存在と、JS の起動・破棄の確認。実際の動きは Playwright で確認）。削除はしない。
- `NavigationTests`・`tests/js/nav.test.mjs`・`tools/nav-check`: nav.js の廃止で**削除**（Blazor の移動のテストに替える。P2）。
- `tools/room-check`: 共有ルームの確認に使うので**残す**（Phase 3 の完了条件で使う）。
- `tools/screenshots`（PowerShell・Python）と `tools/tutorial-images/capture.mjs`: 画像の撮影用。**残す**（説明画像の再撮影に使う）。ただし URL を変えないかぎり変更はない。確認用に使うのは P4 の差分検証。

### 1.6 ドキュメント

**[事実]** `README.md`（技術スタックは「.NET 9 / ASP.NET Core Blazor Web App（静的 SSR）」「Bootstrap 5.3 / Bootstrap Icons（CDN）」。サイト名は `appsettings.json` の `Site:Title`）、`Content/README.md`。**CLAUDE.md は無い**（origin/develop に無い）。

### 1.7 ドキュメントと実装の差・前提の訂正

- **[事実]** ポケモン版の `tools/build-entry-pages.mjs` は、ローカルの `HEAD`（`fix/notice-sticky`）には無い。`origin/develop` と `feature/seo-entry-pages` にある。**計画はこの版を前提にする**（取り込み前に、ポケモン側の develop の最新を確認すること）。
- **[事実]** avatar には sitemap.xml・robots.txt・`_headers`（CSP）が無い。「sitemap/robots を流用する」は、新設になる。
- **[事実]** avatar の Bootstrap は CDN（`App.razor` で bootstrap 5.3.3 と bootstrap-icons 1.11.3、SRI 付き）。Bootstrap の CSS クラスは Razor の全ページで使われている（`row`、`col-lg-8`、`btn`、`card`、`breadcrumb`、`d-none d-lg-block` など）。**Bootstrap 廃止は、ページの見た目の作り直しを伴う**（§5 の見積もりに入れる）。
- **[事実]** Bootstrap Icons のクラス（`bi-*`）は、`Content/` 配下にコード上は 12 種類、C# の `Icon`/`Section`/`Category` に文字列で持つ（Models.cs）。`bi-` を含む行は Content・Components・Data で計 70 箇所前後（`grep -c` の合計。概数）。
- **[事実]** avatar には `Directory.Build.props` が無い（origin/develop の tracked ファイルに無い）。
- **[推測]** 「ILLink の断続的な失敗」は、portfolio 側の CLAUDE.md に記録がある事象。avatar では観測されていない（CI が Linux のため）。Phase 1 の publish で同じ現象が出るかは未確認。

---

## 2. 試作（PR #69、`experiments/wasm/`）の評価

### 2.1 試作の中身（ブランチ `origin/experiment/wasm-mudblazor`、コミット `414747c`）

**[事実]**

| 部品 | 中身 | 本番へ流用できるか |
| --- | --- | --- |
| `ContentExport/` | 本体の `ContentService.cs`・`Models.cs`・`SearchIndex.cs` を **`<Compile Include="..\..\..\Data\...">`（リンク）** で取り込み、`content.json`・`search.json` を書く。画像の相対 `src` を `/learn/<cat>/...` の絶対パスに直す。索引の検索用正規化（NFKC・小文字・〜・カタカナ→ひらがな）をビルド時に済ませる | **流用の中心**。ただし `ContentService` が ASP.NET の `IWebHostEnvironment` に依存しているため、試作は `FakeEnvironment` で回避している。本番では依存を外して「ディレクトリを渡すだけの読み込み」にする（§4.1） |
| `FaraAvatarWasm/` | `App.razor`・`Layout/MainLayout.razor`・`Pages/Home\|Article\|Search\|Practice.razor`・`Services/ContentStore.cs`（JSON を最初の一回だけ取得）・`Services/SearchMatcher.cs`（簡易検索）・`Services/ThemeState.cs`・`wwwroot/js/theme.js`・`wwwroot/css/app.css` | レイアウト・ContentStore・ThemeState は**型と考え方を流用**。Practice は three.js を入れていないため、本番では流用せず JS を残す |
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
| 移動（再読み込みなし、各回の値） | nav.js で 62〜86 ms、状態維持 | Blazor の移動で 79〜80 ms。ただし、three.js の練習ページを静的に開いた状態からの移動は状態が失われる（`wasmPracticeStaticKept: lost`） |

**[事実] 圧縮後の WebAssembly 本体**: framework 3.23 MB（Brotli）、data-json 0.27 MB（Brotli）、MudBlazor 0.10 MB（Brotli）。合計 約 3.6 MB。

**読み取り**
- 速さは問題にしない、という決定があるので、表示時間の差は受け入れない理由にはしない。
- **SEO の差が決定的**。試作の HTML は本文が無い（SEO の要件そのもの）。これが §4 の静的 HTML の必要性を裏づける。
- 練習ページで状態が失われる件は、Blazor の移動で「three.js を使うページは従来どおり読み直す」と決めれば解決する（nav.js と同じ考え方）。

### 2.3 CSP（試作の計測）

**[事実]** `results.json` の `csp`（※ 計測は nav 修正の前）:
- `A-no-wasm-eval/wasm`: 描画されない（`'wasm-unsafe-eval'` が無いと WebAssembly がコンパイルできない）
- `B-wasm-eval/wasm`: 描画されるが `style-src` の違反 6 件（原因は計測の記録からは特定できない。MudBlazor の動的な style が候補【推測】）
- `C-wasm-eval-inline-style/wasm`: 違反 0 件

**[推測] 結論**: `script-src 'self' 'wasm-unsafe-eval'` は必須。`style-src` には `'unsafe-inline'` が要る（MudBlazor の動的な style）。ポケモンの `_headers` は `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` と既に同じ形（pokemon の `wwwroot/_headers`）。

### 2.4 試作のコードで確かめるべき点

- **`string.Normalize(FormKC)` が WebAssembly で動かない問題**: ユーザーの指示（試作で確認済み）と、試作 `ContentExport/Program.cs` の注釈（ブラウザの WebAssembly では `string.Normalize` が使えないため、ビルド時に正規化する）に基づく事実として扱う。試作の `SearchMatcher` も C# の `Normalize` を使わず、全角→半角・カタカナ→ひらがなを手で行っている。よって**索引はビルド時に正規化**し、検索語の扱いだけを D9 で決める。
- 本体の `searchcore.js`・`tipsfilter.js` は `String.prototype.normalize("NFKC")` を使う（`searchcore.js:31`、`tipsfilter.js:17`）。ブラウザでは JS の normalize が常に使える。

### 2.5 PR #69 の扱い（D8）

**[事実]** PR #69 は OPEN（base `develop`、head `experiment/wasm-mudblazor`、タイトル「[試作・マージしない]」）。
**決定（D8）**: 試作は**マージせず閉じる**（ブランチ `experiment/wasm-mudblazor` は参照用に残す）。計画の決定事項とこの評価を本文に残す。

---

## 3. 目標の構成

### 3.1 プロジェクト構成（D1）

**案 A（決定）: 並走させてから置き換える**

```
src/
  FaraAvatarModdingGuide.Content/      … 新: 記事・データの読み込み（ContentService・Models・SearchIndex を移す）。ASP.NET 非依存（ディレクトリを渡す）。Markdig・YamlDotNet を使う。ビルド時専用
  FaraAvatarModdingGuide.Web/          … 新: Blazor WebAssembly + MudBlazor（本番の画面）。Content は参照しない（JSON と静的 HTML を読む）
  FaraAvatarModdingGuide.Build/        … 新: 静的 HTML・JSON・404.html・_redirects・_headers・画像コピーを書き出す（C#。§3.3）
tests/
  FaraAvatarModdingGuide.Tests/        … 既存（Phase 5 で置き換え）
  FaraAvatarModdingGuide.Web.Tests/    … 新
FaraAvatarModdingGuide.csproj（既存 SSR）… Phase 5 で削除
```

- Phase 1〜4 の間は既存の SSR（master と develop の現行）を壊さない。新しいプロジェクトは `src/` の下に作る。
- Phase 5 で既存の csproj と `Components/`・`Data/`・`Publishing/`・`wwwroot/`（JS は練習用に移す）を削除し、`Content/` だけを残す。

**案 B: 既存の csproj を WebAssembly に置き換える**（非採用）: 変更の差分が大きく、master（本番）を壊すリスクが高い。

**`Content/` の扱い**: ルートの `Content/` を単一の情報源のまま残す（記事は Markdown。移動しない）。

**ContentService の扱い**: ASP.NET（`IWebHostEnvironment`・`ILogger`）を外し、「ディレクトリのパス」と「ログの出し先（任意）」だけを受ける。試作の `FakeEnvironment` は不要になる。既存のテストは、このライブラリを直接呼ぶ形に移す。

### 3.1.1 新プロジェクトと既存 csproj の衝突（追加）

**[事実]** 既存の `FaraAvatarModdingGuide.csproj` は `Microsoft.NET.Sdk.Web` の既定の glob で **`**/*.cs` を取り込む**。現在の除外は `tests\**` だけ（`Compile Remove`・`EmbeddedResource Remove`・`None Remove`）。このままだと、`src/` の新プロジェクトの `.cs` と、その `obj/`（自動生成の `AssemblyInfo` 等）がルートのプロジェクトに混ざり、ビルドが壊れる。avatar に `Directory.Build.props` は無い（§1.7）。

**対策（Phase 1 の最初の作業）**:
- ルートの csproj に `src\**` を足す（`tests\**` と同じ 3 つの項目）:
  ```xml
  <ItemGroup>
    <Compile Remove="tests\**;src\**" />
    <EmbeddedResource Remove="tests\**;src\**" />
    <None Remove="tests\**;src\**" />
  </ItemGroup>
  ```
- 新プロジェクトの `obj/`・`bin/` は `.gitignore` で無視されていることを確認（既存の `.gitignore` は `bin/`・`obj/` を含む想定。実装時に確認）。
- `FaraAvatarModdingGuide.sln` に `src/` の新プロジェクトを追加するのは **P1 の完了時**（それまでは sln に入れない。`dotnet build` の対象を変えないため）。
- 将来 `Directory.Build.props` を置くときは、ルートのプロジェクトにも効くことを前提に、`TargetFramework` などを上書きしない形にする（置かない方が安全。現状は置かない）。
- 検証: `dotnet build` と `dotnet test` がルートで通ること、`dotnet run -- --export dist` の出力が変わらないこと（Phase 1 の最初に確認）。

### 3.2 ルーティングと配信

- **全ページを静的 HTML として書き出す**（現行の `StaticSiteExporter` と同じ URL 一覧。76 本）。WASM は各 HTML の `#app` を引き継ぐ。ただし **P1 のあいだ、記事ページは静的のまま（WASM を読み込まない）**。WASM の記事ルートは P2 で足し、そのとき記事ページだけ WASM 読み込みをオンにする（Build のページ設定 `wasm: true/false`）。これで P1〜P3 の dev で記事の深いリンクが NotFound に置き換わらない。
- **SPA フォールバック（存在しない URL に `index.html` を返す方式）は使わない**。理由: 存在しない URL が本文入りの 200 になり、soft 404 になる。ポケモンは `index.html` を SPA フォールバックにするため、汎用の title と読み込み表示だけにしている（`tools/build-entry-pages.mjs` の冒頭コメント）。avatar は `404-page` を使うので、`index.html` はホームのまま（§3.3 の 4）。
- **404**: 静的 `404.html`（ステータス 404、noindex）。Build が生成する（現行は `StaticSiteExporter` が書く）。Worker の `not_found_handling = "404-page"` は維持。
- **旧 URL**: `_redirects`（`/operations`・`/avatar-clothing`・`/plugins` の 301）は **Build が生成**する（現行は `StaticSiteExporter` が書いている処理の移し替え）。
- **末尾スラッシュ**: `html_handling = "auto-trailing-slash"` を維持（`/learn/basics/` → `/learn/basics` へ）。
- **検索索引**: 現在は `/search-index.json`（Minimal API）。WASM 版では静的ファイルとして同じ URL に置く（キャッシュ・外部の参照を壊さない）。
- **`/report` の noindex**: 静的 HTML の `<head>` に `<meta name="robots" content="noindex">` を Build が入れる（ページ設定 `noindex: true`）。WASM 版の Blazor 側（`HeadOutlet`）でも同じ meta を出す。sitemap からは外す。確認用は `X-Robots-Tag` も付く（§4.3）。

### 3.3 静的 HTML の生成（D3）と責務の分担

**決定（本文と全体の生成は C#、テンプレート差し込みの共通処理だけ kit の Node ツール）**:

| 工程 | 担当 | 備考 |
| --- | --- | --- |
| 記事・ページの本文 HTML の作成（Markdig）、ページ一覧（path・title・description・canonical・noindex・bodyHtml・wasm の有無）の JSON 化 | **avatar の C#（`FaraAvatarModdingGuide.Build`）** | D3 の決定どおり |
| `search.json`・`content.json`・`site.json`（サイト設定）・`404.html` の本文 | C# | |
| 画像・動画のコピー（`wwwroot/**`、`Content/learn/**/images/**` の画像と動画を `/learn/...` へ） | C# | `IsMedia` と同じ判定 |
| テンプレート（`index.html`）の `SEO-HEAD`・`SEO-BODY` の区切りへの差し込み、head（title・description・canonical・OGP・robots）の組み立て、`robots.txt`・`sitemap.xml`（本番のみ）の書き出し | **kit の Node ツール（`build/`）**。C# の Build がページ一覧 JSON を渡して呼ぶ（CI では Node が使える） | ポケモンの `tools/build-entry-pages.mjs` を一般化したもの。pokemon と共有する |
| `_headers`（CSP・確認用の noindex）、`_redirects` | C# の Build が書く（importmap のハッシュを含むため。§4.9）。kit の `csp/` テンプレートを読む | 末尾の改行を必ず付ける（§1.4） |

- `index.html`（テンプレート）: `dotnet publish` の出力を使う。区切りは `<!-- SEO-HEAD:START/END -->`・`<!-- SEO-BODY:START/END -->`（ポケモンと同じ）。
- 方式（ポケモンの `tools/build-entry-pages.mjs` を一般化）:
  1. C# の Build がページ一覧 JSON を作り、kit の Node ツールを呼ぶ。
  2. Node ツールが、テンプレートの区切りの間を head と body で書き換える。head は `<title>`、`<meta name="description">`、`<link rel="canonical">`、OGP（`og:title`・`og:description`・`og:url`・`og:type`・`og:image`・`og:site_name`）、`robots`（noindex のページのみ）。body は C# が作った本文。
  3. 同じツールが `robots.txt`（本番は `Sitemap:` の行。確認用は Disallow を付けず `X-Robots-Tag: noindex` で止める。ポケモンと同じ）と `sitemap.xml`（本番のみ）を書く。
  4. `index.html` は**ホームそのもの**（本文入りの `/` の HTML）。SPA フォールバックは作らない。存在しない URL は `404.html` を返す。ポケモンの `worker/home.js` は、`index.html` が SPA フォールバックを兼ねるために必要なもので、avatar では**不要**（`not_found_handling = "404-page"` を使うため）。
- WASM 用の JS（importmap を含む）・CSS は C# の Build がコピーする。

**記事 50 件・ページ数**: 76 本（§1.1）。`dist` の内容の差は、差分比較で確かめる（§8）。

**Node か C# か（D3）**: 本文の HTML は Markdig（C#）で作るため C#。テンプレートの差し込みと sitemap・robots は kit（Node）に任せ、pokemon と共有する。kit のツールは「ページ一覧 JSON を入力にする」ことだけを前提にする（avatar の本文の作り方には依存しない）。

### 3.4 WebAssembly の起動と「ちらつき」対策（§4.10 で詳述）

- 静的 HTML の本文と、WASM 起動後の画面の DOM 構造を揃える。
- テーマとサイドバーの状態を描画前に反映する。インラインの script は CSP で止まるため（§4.9）、外部ファイル（`js/theme-init.js`、head で同期的に読む）にする。
- MudBlazor の CSS は head で読み込み、フォントは Google Fonts（現行と同じ）。

### 3.5 Worker の変更（§4.8）

| 項目 | 現行 | 移行後 |
| --- | --- | --- |
| `/` と記事 URL | 静的アセット（SSR の HTML） | 静的アセット（本文入りの静的 HTML）。Worker は変えない |
| メンテナンス 503 | Worker（全リクエスト） | 同じ（維持） |
| お知らせ帯 | Worker が HTML に注入（`withNotice`） | 決定（§4.8）: WASM が `/api/maintenance` を読む（Phase 1） |
| `/api/maintenance` | 無し | 新設（Phase 1）。pokemon の `worker/index.js` と同じ形 |
| `/api/report`・`/api/report/config` | Worker | 維持（Blazor の報告フォームが呼ぶ） |
| `/api/rooms/<ID>/ws` | Durable Object | 維持 |
| `/search-index.json` | Minimal API（Program.cs） | 静的ファイル（同じ URL） |
| `_headers` | deploy.yml が確認用の noindex を追記 | Build が本体の `_headers` を出す（CSP・X-Content-Type-Options 等。ポケモンと同じ形）。確認用の追記は残す |

---

## 4. 各項目の詳細

### 4.1 コンテンツのエクスポートと既存の Models/ContentService（§3.1 の続き）
- `ContentService` は、ビルド時の処理（Markdown → HTML、YAML の読み込み、索引の作成）を担う。WebAssembly の実行時には使わない。
- WebAssembly に送るのは JSON（`content.json`：区切り・カテゴリ・記事（HTML 含む）・ショートカット・addons・用語・Q&A・How-to・TIPS・リンク・更新履歴）と `search.json`（索引。Norm は生成時に正規化済み）と `site.json`（サイト設定）。
- テスト: 既存のテストの一部はライブラリ側に移す。新規: 「50 記事すべてが JSON に入る」「記事 URL が現行と一致する」「相対画像の URL が `/learn/...` になる」。

### 4.2 ページの置き方（MudLayout）
- MudLayout の `MudAppBar`（ハンバーガー・サイト名・検索欄・テーマ切り替え）、`MudDrawer`（目次。PC は隠せる、スマホは一時表示）、`MudMainContent`（本文・フッター）。
- ポケモンの `MainLayout.razor`・`MudProviders.razor`（`MudThemeProvider`・`MudPopoverProvider`・`MudDialogProvider`・`MudSnackbarProvider`）をほぼそのまま使う（kit の Razor 経由）。
- 色は現行の CSS 変数（`:root`）をテーマの色に写す（D6 と合わせて決める）。

### 4.3 CI/デプロイ
1. `dotnet build`・`dotnet test`（新旧の両方。Phase 5 で旧を削除）。
2. `dotnet publish src/FaraAvatarModdingGuide.Web -c Release`（WebAssembly）。
3. `dotnet run --project src/FaraAvatarModdingGuide.Build -- --content Content --template <publish の index.html> --out dist`（静的 HTML・JSON・404.html・_redirects・_headers・画像・sitemap・robots。sitemap と robots は kit の Node ツールを呼ぶ）。
4. 確認用は `_headers` に `X-Robots-Tag: noindex, nofollow` を追記（現行と同じ。deploy.yml の `cat >>`。Build の出力は末尾改行を必ず付ける）。
5. 本番（master）の MAINTENANCE=notice の確認（`require-maintenance.mjs`）は現行のまま。
6. Node テスト（report・maintenance・require-maintenance）は残す。nav.test.mjs は削除。
7. `actions/checkout@v4` の build・deploy 両ジョブに `submodules: true`（kit を使うため。§8）。

**ILLink（§5 リスク）**: publish の trim 設定を明示（`PublishTrimmed` の既定のまま、必要な型は `TrimmerRootAssembly` で守る）。CI で失敗したら再実行で済むか記録し、続くなら `TrimMode`・`IlcTrimMetadata` を見直す（推測。avatar では未観測）。

### 4.4 検索（D9）
- 索引（`search.json`）: 生成時に `Norm` を作る（NFKC・小文字・〜・カタカナ→ひらがな）。現行の `searchcore.js` の規則と同じにする。
- 検索語: D9 の決定どおり、ブラウザで `String.prototype.normalize("NFKC")` を JS interop で呼ぶ。C# の `Normalize` は試作の注釈どおり使わない。
- ローマ字の読み替え・半角カナ・強調・抜き出し: 現行の `searchcore.js` の仕様を Node の既存の振る舞いと比べられる形（同じ入力・同じ件数）で C# に移す。**テストは現行の searchcore の結果を正解データにする**。

### 4.5 練習ページ（Three.js の組み込み・JS interop・解放・PC 専用・importmap）

- 既存の JS（`viewport.js`・`unity.js`・`scenarios.js`・`editmesh.js`・`room.js`・`tutorial.js`・`practice.js`・`unity-practice.js`・`touch-practice.js`）は**書き換えない**。
- Blazor 側は、ページの枠（Razor）と、JS の起動・破棄だけ（`IJSRuntime` で `init(element)` / `dispose()`）。`IAsyncDisposable` で破棄を呼ぶ。

**importmap の扱い（既定を決める）**:
- **既定: importmap は Build が出す静的 HTML のテンプレート（`index.html` の head）に 1 つだけ置く**（全ページ共通。`three` の CDN のみ。`three@0.170.0` のバージョン固定）。CSP は importmap の sha256 を `script-src` に入れる（§4.9）。
- 理由: importmap は、その文書で最初の module script が読まれる前に存在している必要がある。Blazor が描画後に挿入した importmap は、後から読む module の解決に効かない可能性が高い【推測。仕様上の性質。試作は練習ページを含まず未検証】。
- 代案: JS の `import "three"` を CDN の絶対 URL（バージョン固定）に書き換え、importmap を無くす。既存の JS を編集するため、既定にはしない。
- **検証タスク（Phase 3 の最初）**: 練習ページ 1 つ（`/practice`）で、テンプレートの importmap と CSP のハッシュの組み合わせで three.js が読み込まれ、Blazor の画面遷移のあとも動くことを確認する。動かなければ代案に切り替える。
- ページ移動のあとに module を二重に読まない（nav.js の「二重登録しない」の教訓）。

- PC 専用（`practice-pc-only`）: 現行どおり、スマホではガイドだけを出す（`TouchPracticeGuide`）。
- 共有ルーム: 接続先 `/api/rooms/<ID>/ws` は変えない。入室・退室の状態だけ Blazor に持つ。名前の保存キー `avatar-modding-guide.name` は同じ。

### 4.6 報告フォーム（Blazor 版・Turnstile・noindex）
- `Report.razor` を Blazor のフォームにする。送信は `/api/report`（JSON）へ POST（ポケモンの `ReportForm.cs` と同じ形）。
- Turnstile は `wwwroot/js/report.js` と同じ方式（`challenges.cloudflare.com/turnstile/v0/api.js` を読み、トークンを受け取る）。サイトキーは `/api/report/config`。
- 送信の対策（同一ページからか、本文の長さ・種類、honeypot の `website`）は Worker の `worker/report.js` がそのまま行う。
- `?from=` は `SiteFooterLinks.ReportHref` が付ける（§1.2 の 38）。受け取り側の確認は K0 か P1 で行う。
- 静的 HTML の noindex: `/report` の静的 HTML に `<meta name="robots" content="noindex">` を Build が入れる（§3.2）。
- テスト: 既存の `FooterAndReportTests` の一部（フォームの項目・API の形）を、Blazor 版の検査に替える。

### 4.7 背景 3D・スクロール線
- `bgscene.js`（three.js）: Layout から一度だけ起動。ページ移動では止めない（Blazor の移動は画面を差し替えるだけなので、背景は残る）。`prefers-reduced-motion` の扱いは現行（bgscene.js）どおり。
- `scrollprogress.js`: 小さな JS interop か CSS（`animation-timeline: scroll()`）。読み上げには出さない。
- いずれも Phase 1 で移し、Phase 3 の練習ページと干渉しないことを確かめる（three.js の renderer が 2 つ動く状態の確認）。
- `ScrollProgressTests`（5 件）は Blazor 版の検査に替える（§1.5）。

### 4.8 Worker（お知らせ帯・メンテナンス・API）（D4）
- **維持するもの**: 503（`getMaintenance` → `maintenanceResponse`）、`/api/report*`、`/api/rooms/*`、`run_worker_first = true`。
- **お知らせ帯（決定・§0）**: WASM が起動時に `GET /api/maintenance` を読み、`state` が `notice` のときだけ帯を出す（ポケモンの `Services/MaintenanceInfo.cs` と同じ作り。2 秒で打ち切り、読めなければ通常）。Worker の `/api/maintenance` は、ポケモンの `worker/index.js`（origin/develop）と同じ形（`{state, message}`）を返す。avatar の `worker/maintenance.js` の `getMaintenance` は既に `state` と `message` を返すので、ルートを一つ足すだけで済む。**実施は Phase 1**。
  - 代償: 起動の前（数秒）は帯が出ない。JS が無い環境では出ない。
  - 削除するもの（Phase 1）: `worker/index.js` の `withNotice` による HTML 注入、`worker/maintenance.js` の `noticeHtml`・`rewriteWithHtmlRewriter`、それらのテスト（`worker/maintenance.test.mjs` の該当部分）。
  - 残すもの: 503（`MAINTENANCE=on`）の処理と画面（`getMaintenance`・`maintenanceResponse`）。
- `_headers` は Build が出す（CSP は §4.9）。

### 4.9 CSP（ポケモンの `_headers` を基準。ユーザー決定）

**[事実] ポケモンの `_headers`**: `default-src 'self'`、`script-src 'self' 'wasm-unsafe-eval' https://static.cloudflareinsights.com https://challenges.cloudflare.com`、`style-src 'self' 'unsafe-inline' https://fonts.googleapis.com`、`font-src 'self' https://fonts.gstatic.com`、`img-src 'self' data: https://raw.githubusercontent.com`、`connect-src 'self' https://cloudflareinsights.com https://challenges.cloudflare.com`、`frame-src https://challenges.cloudflare.com`、`object-src 'none'`、`base-uri 'self'`、`form-action 'self'`、`frame-ancestors 'none'`。

**決定（ユーザー）**: `'wasm-unsafe-eval'` と `style-src 'unsafe-inline'` は許可。`script-src` の `'unsafe-inline'` と `'unsafe-eval'` は**許可しない**。ホストは使うものだけに限定する。

**avatar の CSP（Phase 1 の Build が出す初版）**:
- `script-src 'self' 'wasm-unsafe-eval' 'sha256-…'（importmap のハッシュ。Build が計算）https://cdn.jsdelivr.net https://challenges.cloudflare.com`（報告の Turnstile）。Cloudflare Web Analytics を使う場合は `https://static.cloudflareinsights.com` を足す（現状の avatar は未確認）。
- `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com`（Bootstrap は廃止するので CDN は外す）。
- `font-src 'self' https://fonts.gstatic.com`、`img-src 'self' data:`（avatar は外部の画像を使わない）。
- `connect-src 'self'`（`/api/*`・`/api/rooms/*/ws` は同じオリジン。WebSocket は `wss:` の扱いを確かめる）、`frame-src https://challenges.cloudflare.com`。
- `object-src 'none'`、`base-uri 'self'`、`form-action 'self'`、`frame-ancestors 'none'`。
- `_headers` は全ページに同じ CSP が付く。`/api/*` には付かない（Worker 側の応答）。

**インラインのスクリプトの扱い**（試作の CSP 計測で、インラインの script は `script-src 'self'` で止まることを確認している）:
- テーマの描画前 script（現行 `App.razor` の head のインライン script）→ 外部ファイル `js/theme-init.js` にし、head で同期的に読む。
- importmap（§4.5 の既定）→ 外部ファイルにできないため、**sha256 のハッシュを `script-src` に入れる**。ハッシュは Build が importmap の文字列から計算し、`_headers` を書くときに入れる（three.js の版を上げるとハッシュも変わるので、手で直さない）。
- Blazor の `blazor.webassembly.js` と MudBlazor の JS は同じオリジンなので `'self'` で足りる。

### 4.10 起動時のちらつき対策
- 静的 HTML の本文と WASM の描画は、**同じ DOM 構造・同じクラス**にする（MudBlazor のクラスを静的 HTML にも付ける）。
- 静的 HTML の本文は、WASM の起動まで見えたまま残す。起動後に Blazor が `#app` の中身（静的 HTML）を置き換える。ここで見た目が変わらないことを検証する。
- 検証: 起動の前後で、スクリーンショットの差（ピクセル差）を記事 5 ページ・ホーム・検索で取る。差が閾値を超えるページは静的 HTML の CSS を直す。
- ポケモンの `MaintenanceScreen` 方式（WASM だけで画面を作る）は avatar では採らない。

### 4.11 その他の決まり（Bootstrap 廃止、nav.js 廃止、Icons）
- **Bootstrap 廃止**（MudBlazor に統一）: 現行 `site.css`（約 3,260 行）のうち、MudBlazor の既定と重なる部分を捨て、記事本文（`.article-body` 相当）・目次・パンくず・テーブル・コードのスタイルだけ残す（必要な共通 CSS のみ）。
- **nav.js 廃止**: Blazor の Router が移動を担う（§1.2 の 28）。テスト `tests/js/nav.test.mjs` と `NavigationTests` は削除。
- **アイコン（D6）**: Bootstrap Icons（`bi-*`）は、MudBlazor の Material アイコンへ写す。写せないものは SVG を `wwwroot/images/` に置く。

### 4.12 画像・OGP 画像（D5）
- **Build の画像工程**: `wwwroot/**`（SVG・WebP 145 枚を含む）を `dist/` へそのままコピー。`Content/learn/**/images/**` の画像（PNG 51 枚）と動画を `dist/learn/...` へコピー（`IsMedia` と同じ判定）。
- **OGP 画像（D5 の決定 (a)）**: 1200×630 の PNG を `wwwroot/images/og-image.png` に置く。**作成は Phase 1**（デザインは avatar のサイトの色とフォントに合わせ、**ユーザーが承認**）。Phase 4 で og:image を全ページに入れ、SNS の表示を確認する。サイズの目安は 300 KB 以下（推測）。

---

## 5. 段階（フェーズ）と PR の分割

**見積もりは推測（1 人＋ AI 支援、1 人日 = 実作業 6 時間の想定）。** 実際は記事 50 件の目視確認とネイティブの検証で増える。

### Phase 0: 計画（PR #71）— 約 1 日（推測）
- PR: `docs/wasm-migration-plan` → `develop`（マージしない）。成果: 本文書。

### Phase K0: fara-site-kit の作成と pokemon からの切り出し — 約 4〜6 人日（推測）
- 内容（ステップ順。各ステップは pokemon の PR 1 つ。pokemon を壊さない順番は §10.5）:
  - **K0-1 Worker 共有 JS**（`maintenance.js`・`report.js`）→ kit へ。pokemon は submodule 参照に切り替え（§8 の検証を先に行う）。
  - **K0-2 C# Core**（`MaintenanceInfo`・`ReportForm` の検証・`SiteLinks`・`SiteOptions`）→ kit へ。名前空間は変えない。
  - **K0-3 build ツール**（pokemon の `tools/build-entry-pages.mjs` を一般化して kit の `build/` へ。§3.3 の役割）。
  - **K0-4 Razor 部品**（MudProviders・MainLayout の部品・フッター・MaintenanceBanner・報告フォームの Razor）→ kit へ。
  - **K0-5 NavMenu の基底・ThemeSwitch** → kit へ（P2 で使う）。
- 完了条件: pokemon の `dotnet test`・Node テスト・`wrangler deploy --dry-run` が通り、dev サイトの表示が変わらない。重複したファイルは別 PR で削除する。
- **P1 との依存**（§5 の依存関係を参照）: K0-1・K0-2・K0-3・K0-4 が P1 の前提。K0-5 は P2 の前提。

### Phase 1: 土台（ほぼ全ページの静的生成を含む） — 約 12〜17 人日（推測）
- 内容:
  - `src/FaraAvatarModdingGuide.Content`（ContentService 移設）。ルートの csproj の `src\**` 除外（§3.1.1）。
  - `src/FaraAvatarModdingGuide.Build`: **76 本の静的 HTML（本文入り）**・`404.html`・`search.json`・`content.json`・`site.json`・`_redirects`・`_headers`（CSP 初版）・画像コピー。ページ一覧 JSON を kit の Node ツールに渡す（§3.3）。P1 の時点で記事 50 件は本文入りの静的 HTML として出る（WASM は読み込まない。§3.2）。
  - `src/FaraAvatarModdingGuide.Web`（WASM＋MudBlazor）: MudLayout、テーマ（`js/theme-init.js`）、サイドバー、ヘッダー、フッター、404、報告フォーム、プライバシー、更新履歴、リンク集、ホーム、お知らせ（§4.8）。
  - Worker: `/api/maintenance` を追加、`withNotice` の注入を削除（§4.8）。
  - OGP 画像の作成（§4.12。ユーザー承認）。
  - テストの置き換え（`MaintenanceConfigTests`・`ScrollProgressTests` の一部。§1.5）。
  - sln への追加（P1 の完了時）。
- 含まないもの: 記事の WASM 化（P2）、練習（P3）、sitemap の仕上げと検証（P4）。
- 完了条件（dev で確認）:
  - 76 本の静的 HTML と 404.html が、dev の URL で表示され、深いリンク（記事 URL を直接開く）が 404 にならない。記事は静的のまま表示される。
  - 旧 URL 3 件が 301。
  - 報告フォームが確認用の API で送信できる（Turnstile は確認用キー）。
  - お知らせ（notice）の表示と、503 の表示が現行どおり。
  - `dotnet test`（新）と Node テストが通る。
- 並行: ContentService の移設・Build の骨格は、kit の完成を待たずに進められる。Razor 部品は K0-4 の後。

### Phase 2: 記事 — 約 12〜18 人日（推測）
- 内容: 記事の WASM 化（本文・目次・パンくず・前後の送り・ショートカット表示・進捗の記録）。ページ設定 `wasm: true` を記事に付ける。区切り・カテゴリの一覧、検索（/search・ヘッダー・本文上の候補）、TIPS・用語集・Q&A・How-to（絞り込み）、ショートカット一覧、addons、ProgressSummary。nav.js と NavigationTests の削除（§1.5）。
- 完了条件（受け入れテスト）:
  - 記事 50 件すべてで、本文のテキスト（改行・空白を除いて）が現行 SSR 版と一致する。
  - 検索: `searchcore.js` と同じ入力（検索語 100 件程度）で、件数と上位 10 件の順が一致する。
  - 進捗: 既存の localStorage（`avatar-modding-guide.progress`）の値が、そのまま読める。
- 並行: 検索（§4.4）と TIPS 系は、記事ページと別に進められる。

### Phase 3: 練習 — 約 8〜12 人日（推測）
- 内容: 練習 1・2・3（`/practice`・`/practice/unity`・`/practice/room`）、記事内チュートリアル（`::: practice`）、タッチ案内、背景 3D（bgscene.js）・スクロール線の組み込みと破棄の確認。
- **最初の作業（検証）**: 練習ページ 1 つ（`/practice`）で importmap（テンプレート head）と CSP のハッシュの組み合わせを確かめる。動かなければ §4.5 の代案に切り替える。
- 完了条件: 3 つの練習ページで、現行版と同じ操作（キー・マウス）ができる。ページ移動のあとも three.js の renderer が増えない（メモリ・リスナーの確認）。共有ルームで二人が接続できる（`tools/room-check` を使う）。
- 並行: Phase 2 の途中から JS 部分（interop の作成）を始められる。

### Phase 4: SEO の仕上げ — 約 3〜5 人日（推測）
- 内容（P1 で作った生成の上に、仕上げだけを行う）:
  - `sitemap.xml`（本番のみ）と `robots.txt` の最終形。**sitemap の対象（推奨）**: 固定ページ（16）・カテゴリ（10）・記事（50）。除外: `/report`（noindex）・`/search`（検索結果のための画面で中身が薄い）・旧 URL・404・`/search-index.json`。`/privacy` は含める。
  - OGP の全ページ適用（og:image を §4.12 の PNG に）、canonical の検査。
  - 起動の前後のスクリーンショット差の検証（§4.10）。
  - CSP の違反ゼロの確認（主要ページ。ブラウザの違反ログ）。
  - 本文の一致の最終検証（§8）。
- 完了条件:
  - 76 本すべてに本文が入った HTML が出る（JS 無しで読める）。
  - ページごとの title・description・canonical・OGP がある。`sitemap.xml` は本番だけ。確認用は `X-Robots-Tag: noindex`。
  - 起動の前後のスクリーンショット差が、閾値以下（記事 5・ホーム・検索）。
  - CSP で違反が 0。
- 並行: P3 と並行できる。

### Phase 5: 切り替え — 約 4〜6 人日（推測）
- 内容: CI/デプロイを WASM 版に変える、旧 SSR のコード（`Components/`・`Data/`・`Publishing/`・`wwwroot/js/` の不要分・`FaraAvatarModdingGuide.csproj`）を削除、テストの置き換え、README・Content/README の更新、`wrangler.toml` 冒頭のコメント（SSR 前提の説明・`dist/` の生成元）の更新、Obsidian（ユーザーの運用ルールに従う）。
- **切り替え PR に含まれるもの（ロールバックの範囲）**: (1) `deploy.yml`（WASM build・submodules・nav.test の削除）、(2) `wrangler.toml` のコメント、(3) 旧 SSR のファイル削除、(4) テストの置き換え、(5) README・Content/README、(6) 残った旧 JS の削除。ロールバックは、この切り替えのマージを revert すれば旧 SSR に戻る（§6）。
- 完了条件: develop の確認用（avatar-dev）で、§8 の受け入れ確認が全部通る。master への PR はユーザーがマージする（§6）。

**見積もりの合計（推測）**:

| フェーズ | 見積もり（人日） |
| --- | --- |
| P0 計画 | 1 |
| K0 kit の作成と切り出し | 4〜6 |
| P1 土台（静的生成を含む） | 12〜17 |
| P2 記事 | 12〜18 |
| P3 練習 | 8〜12 |
| P4 SEO の仕上げ | 3〜5 |
| P5 切り替え | 4〜6 |
| **合計（P0・K0 を含む）** | **44〜65** |

### 依存関係

```
P0 → K0-1〜K0-4 → P1 → P2 ─┬→ P4 → P5
                       └→ P3 ┘
K0-5（NavMenu 基底・ThemeSwitch）→ P2 の前
```

- P1 の前提: K0-1（Worker 共有 JS）・K0-2（C# Core）・K0-3（build ツール）・K0-4（Razor 部品）。K0-4 は P1 の後半から使うので、P1 の前半（ContentService 移設・Build の骨格）は K0-4 と並行できる。
- P3 は P2 の記事ページの骨格が要る（記事内チュートリアルの埋め込み）。
- P4 は P1 の生成の上に乗る（P2・P3 と並行できる）。

---

## 6. リリース戦略（D7・D14）

1. **develop に段階的に入れる**: Phase ごとに PR を develop へ（D14 の (2) を選んだ場合）。dev サイト（avatar-dev）で確認する。確認は Cloudflare Access の内側（自分だけ）。
2. **master は触らない（移行の完了まで）**（D7）: 本番（avatar.fara-labs.com）は現行の SSR のまま。
3. **master への合流**: develop → master の PR はユーザーがマージする（Claude は PR の作成まで）。deploy.yml の本番の確認（MAINTENANCE=notice）は、そのまま動く。
4. **旧版との並行期間**: 本番は SSR のまま。dev で WASM 版を確認する期間を置く。切り替えの日は、お知らせ（notice）を出してから行う（現行の運用どおり）。
5. **移行後の URL**: 変更しない（同じ URL、同じ本文）。旧 URL の転送（`_redirects`）も残す。
6. **ロールバック**: 切り替えの PR（§5 の P5 の一覧）を revert すれば SSR に戻る。master は、切り替えの後に develop→master の PR をマージするまで旧版のまま。

### D14 ブランチ戦略（決定待ち）

「master を壊さない仕組み」は運用の約束だけでは不十分（develop→master の PR を出した瞬間に、未完成の WASM が本番に出る）。案は次の 3 つ。

**(1) 長期ブランチ `migration/wasm`（推奨）**
- 移行の変更はすべて `migration/wasm` に積む。`develop` は現行 SSR のまま、SSR の修正は `develop` に入れる。
- `deploy.yml` に `migration/wasm` を足し、**別の確認用 Worker** に出す（名前・ドメインは要確認。例: Worker `fara-avatar-wasm-dev`、ドメイン `avatar-wasm-dev.fara-labs.com`。wrangler.toml に `env.wasm-dev` を追加。Cloudflare Access の設定が要る）。
- `develop` の修正は定期的に `migration/wasm` へマージする（目安: 週 1 回。推測）。
- 完成したら `migration/wasm` を `develop` にマージ（切り替え。P5 の PR）→ 以後は従来どおり develop→master。
- 利点: master も develop も移行中ずっと安全。dev の確認が WASM 専用の URL で行える。切り替えは 1 回のマージ。
- 欠点: 長期ブランチのマージ衝突（SSR 側の修正が多いと増える）。確認用 Worker が 1 つ増える（推測。Cloudflare の費用は小さいと見込む）。

**(2) develop に直接入れ、develop→master の PR を移行中は止める（運用またはブランチ保護）**
- 利点: 単純で、ブランチが増えない。
- 欠点: develop が途中の WASM になるので、SSR の修正の確認用 dev が壊れる。「master への PR を出さない」は忘れやすい（GitHub のブランチ保護で防ぐと設定の手間が増える）。

**(3) feature flag で両方を並走（同じビルドで両方を出す）**
- 利点: ブランチは 1 つ。
- 欠点: 2 つのフレームワーク（SSR と WASM）を同じリポジトリ・同じビルドで維持する。同じ URL を二重に出せない（path を分けると URL が変わる）。移行後の掃除が大きい。

**推奨: (1)**。理由: master の安全（D7）を、ブランチで物理的に守れる。develop は現行 SSR を安全に保てる。dev で WASM を別 URL で確認できる。切り替えは最後の 1 回のマージで済む。コストは長期ブランチの定期マージと、確認用 Worker の追加。

---

## 7. リスクと対策

| リスク | 影響 | 対策 |
| --- | --- | --- |
| 静的 HTML と WASM の画面の差（ちらつき・レイアウトずれ） | 見た目・CLS | 同じ DOM とクラス、起動前後のピクセル差の検証（§4.10） |
| SEO（移行で検索順位が落ちる） | 流入 | URL を変えない、本文を同じ量で出す（テキスト一致の検証）、sitemap の新設 |
| WebAssembly の配布量（約 4.3 MB。Brotli で約 3.6 MB） | 初回表示の遅さ | 速さは決定事項として問題にしない。記録だけ残す |
| 検索の正規化（`Normalize`） | 検索結果のずれ | JS の `normalize` を使う（D9）、テストは現行 searchcore の結果を正解にする |
| Three.js と Blazor の共存（破棄漏れ・二重起動） | メモリ・不具合 | `IAsyncDisposable` で破棄、ページ移動のテスト、renderer の数の確認 |
| importmap が Blazor の描画後に効かない | 練習ページが動かない | テンプレートの head に置く（§4.5）。Phase 3 の最初に検証、ダメなら代案 |
| CSP（style の `unsafe-inline`） | セキュリティの弱まり | 決定済み（§4.9）。script の `unsafe-inline`・`unsafe-eval` は不許可 |
| csproj の衝突（新プロジェクトの .cs を旧 csproj が拾う） | ビルドが壊れる | `src\**` の除外（§3.1.1）、sln への追加は P1 の完了時 |
| submodule（kit）を wrangler・CI が読めない | デプロイが止まる | §8 の検証（試験ブランチ・dry-run・checkout の submodules） |
| Bootstrap 廃止による見た目の差 | 既存の読者の印象 | スクリーンショットの比較（PC・スマホ、ライト・ダーク）を Phase 2・4 で行う |
| ILLink の失敗（断続的） | CI の失敗 | 再実行で済むか記録、設定の明示（§4.3）。avatar では未観測（推測） |
| localStorage のキー変更 | 既存の進捗が消える | キー名を変えない（§1.2 の 2・3・13） |
| お知らせの二重表示（注入とクライアント表示） | 見た目 | 決定済み（§4.8）。Worker の注入は Phase 1 で削除 |
| `_headers` の末尾改行の欠落（deploy.yml の `cat >>` で行が壊れる） | CSP・noindex が効かない | Build の出力は末尾改行を必ず付ける（§1.4） |
| 長期ブランチのマージ衝突（D14 (1) の場合） | 作業の手戻り | 定期的に develop を取り込む。SSR 側の修正は小さく保つ |
| ポケモン側の build-entry-pages の版違い | 方式の食い違い | 取り込み時に、ポケモンの develop の最新を確認 |

---

## 8. 完了条件（全体の受け入れ確認）

- [ ] 現行の全 URL（§1.1 の一覧。静的 HTML 76 本）が、WASM 版で同じ URL に表示される（404 は 404 のまま）
- [ ] 旧 URL 3 件が 301 で転送される
- [ ] 記事 50 件の本文テキストが、現行 SSR 版と一致する（改行・空白を除く）
- [ ] 静的 HTML の 76 本すべてが、JS 無しで本文を読める
- [ ] title・description・canonical・OGP が全ページにある。sitemap.xml（本番）・robots.txt がある。確認用は noindex。`/report` は noindex
- [ ] 検索の件数・順序が現行と一致する（テストデータ 100 件程度）
- [ ] 進捗の記録（既存の localStorage）が読める
- [ ] 練習 3 ページ・記事内チュートリアル・共有ルームが、現行と同じ操作で動く（PC）。スマホは案内が出る
- [ ] 報告フォームが Turnstile 付きで送信できる（確認用）。`?from=` が受け取れる
- [ ] お知らせ（notice）と 503（メンテナンス）が、現行と同じ条件で出る
- [ ] CSP の違反が 0（主要ページ）。確認用の `_headers`（X-Robots-Tag・CSP）が ASSETS の応答に付く
- [ ] ライト・ダーク、PC・スマホで、主要ページのスクリーンショットに大きな差がない（ユーザーの目視確認）
- [ ] CI（build・test・node・deploy の確認）が通る。旧テスト（nav.js 等）は削除済み
- [ ] README・Content/README が WASM 版の内容になっている

### 8.1 submodule（kit）を wrangler と CI が読めるかの検証手順（K0-1 の最初に行う）

1. 試験ブランチ（例: `spike/kit-submodule`）で `external/fara-site-kit` を submodule として追加し、`worker/index.js` から `external/fara-site-kit/worker/maintenance.js` を import する。
2. wrangler を **4.149.0 に固定**して `npx wrangler deploy --dry-run --outdir <dir>` を実行し、出力された bundle に `getMaintenance` が入っていることを確認する。
3. `.github/workflows/deploy.yml` の **build と deploy の両ジョブ**の `actions/checkout@v4` に `submodules: true` を付ける（現在は無い）。
4. `npx wrangler dev` で `/api/maintenance` と `/api/report/config` の応答を確認する。
5. ASSETS の応答に `_headers` の `X-Robots-Tag` と CSP が付くことを、`curl -I` で dev の URL に対して確認する（確認用の追記も含む）。

---

## 9. 決定事項（D1〜D14）

| # | 決めること | 選択肢 | 決定 |
| --- | --- | --- | --- |
| D1 | プロジェクト配置 | A: `src/` に新規を並走させ、Phase 5 で置き換え／B: 既存の csproj を置き換え | **決定（2026-10-22）**: A（`src/` に新規を並走させ、Phase 5 で置き換え） |
| D2 | 記事本文のデータ | (a) 全記事の HTML を 1 つの JSON に入れる（試作の実測。`content.json` と `search.json` の合計で約 1.7 MB、Brotli 約 0.27 MB）／(b) 記事ごとに HTML 断片を別ファイルにして開いたときだけ取る | **決定（2026-10-22）**: (a) 全記事の HTML を 1 つの JSON に入れる |
| D3 | 静的 HTML の生成 | (a) C#（Markdig）で本文を作る（Build の本体）／(b) Node（ポケモン流）で本文まで作る | **決定（2026-10-22）**: (a) C#（Markdig）で本文の HTML を作る。テンプレートの差し込み・sitemap・robots は kit の Node ツールを呼ぶ（§3.3） |
| D4 | お知らせ帯 | — | **決定（2026-10-22）**: 推奨どおり（§0。WASM が `/api/maintenance` を読む。Phase 1 で実施） |
| D5 | OGP 画像 | (a) 新しく 1200×630 の PNG を作る／(b) `site-icon.svg` を流用（SVG は多くの SNS で表示されない） | **決定（2026-10-22）**: (a) 新しく 1200×630 の PNG を作る（作成は Phase 1。デザインはユーザーが承認。§4.12） |
| D6 | アイコンと Bootstrap | (a) Bootstrap Icons を捨てて Material（MudBlazor）に写す／(b) bootstrap-icons の必要な分だけ SVG で同梱 | **決定（2026-10-22）**: (a) Bootstrap Icons を Material（MudBlazor）に写す |
| D7 | 旧版との並行 | (a) 移行完了まで master は SSR のまま（dev だけ WASM）／(b) 段階ごとに master へも入れる | **決定（2026-10-22）**: (a) 移行完了まで master は SSR のまま（dev だけ WASM） |
| D8 | 試作 PR #69 | (a) 閉じる（参照用ブランチは残す）／(b) 残す | **決定（2026-10-22）**: (a) 閉じる（ブランチ `experiment/wasm-mudblazor` は残す） |
| D9 | 検索語の正規化（索引は両案ともビルド時に正規化） | (a) JS interop で `String.prototype.normalize("NFKC")` を呼ぶ／(b) C# で手書きの正規化（NFKC の一部だけ） | **決定（2026-10-22）**: (a) 検索語は JS interop の `String.prototype.normalize("NFKC")`（索引はビルド時に正規化） |
| D10 | 進め方の人数・順序 | (a) 1 人で P1→P5 を順に／(b) P3 と P4 を並行（人数が増えたとき） | **決定（2026-10-22）**: (a) 1 人で P1→P5 を順に（P3 と P4 の並行は後半で可） |
| D11 | kit のリポジトリ名 | (a) `fara-site-kit`／(b) 別名 | **決定（2026-10-22）**: `fara-site-kit` |
| D12 | kit の公開・非公開 | (a) 公開（秘密情報は置かない）／(b) 非公開 | **決定（2026-10-22）**: GitHub のリポジトリを公開（秘密情報は入れない。NuGet は使わない） |
| D13 | 利用方法 | A: submodule ＋ ProjectReference／B: NuGet（GitHub Packages）／C: monorepo | **決定（2026-10-22）**: A（git submodule ＋ ProjectReference） |
| D14 | ブランチ戦略（master を守る仕組み） | (1) 長期ブランチ `migration/wasm`（develop は SSR のまま。別の確認用 Worker に出す。完成後に migration/wasm を develop にマージ）／(2) develop に直接入れ、移行中は develop→master の PR を止める（運用またはブランチ保護）／(3) feature flag で両方を並走 | **未決（ユーザーの選択待ち）**。推奨: **(1)**（§6） |

**決定の補足（2026-10-22）**:
- リポジトリ `fara1991/fara-site-kit` は作成済み（public。ブランチは `master` と `develop`。README のみ）。
- フェーズ K0 の最初の作業（kit の骨組み、Worker 共有 JS の取り込み、pokemon を submodule 参照に切り替え）は、別担当が開始する。
- CSP は、`'wasm-unsafe-eval'` と `style-src 'unsafe-inline'` を許可することを**決定**。`script-src` の `'unsafe-inline'`・`'unsafe-eval'` は不許可（方針）。

**追加で確認したいこと（D に入れない）**
- Phase 5 の後の CLAUDE.md の扱い（avatar には無い。作るかどうか）。
- 確認用 Worker の名前・ドメイン（D14 (1) を選んだ場合。例 `fara-avatar-wasm-dev`・`avatar-wasm-dev.fara-labs.com` は仮）。

---

## 10. 共通部品リポジトリ `fara-site-kit`

ユーザーの決定（追加）: avatar の移行を先に完了する。bot・ポートフォリオの採用は avatar 完了後に再評価（今回の対象外）。ポケモンと avatar の共通部品は、再利用できる形（kit）にまとめる。**作成済み（D11・D12）**。

### 10.1 構成

| 置き場所 | 中身 | 出どころ（確認済み） |
| --- | --- | --- |
| `src/FaraSiteKit.Razor`（Razor クラスライブラリ） | MudProviders（テーマ・ポップアップ・ダイアログ・通知）、MainLayout の部品（Mini ドロワー・ヘッダー・スキップリンク）、SiteFooter と SNS アイコン、NavMenu の基底（項目の型と描画。サイト固有の項目はサイト側）、ThemeSwitch、MaintenanceBanner、MaintenanceScreen、報告フォームの Razor（Report.razor のフォーム部分）、スクロール進捗（ラッパー＋JS） | pokemon: `src/FaraPokemonBattleAssistant.Web/Shared/MudProviders.razor`・`Shared/MainLayout.razor`・`Shared/NavMenu.razor`・`Shared/MaintenanceBanner.razor`・`MaintenanceScreen.razor`（Web 直下）・`Components/Pages/Report.razor`。avatar: `Components/Layout/SiteFooter.razor`・`SiteFooterLinks.cs`・scrollprogress.js |
| `src/FaraSiteKit.Core`（C#、UI と通信なし） | `SiteLinks`（確認用ホストへの切り替え）、`MaintenanceInfo`（Parse・既定値・2 秒で打ち切る読み込み）、`ReportForm`（入力の検査・上限。サーバーと同じ値）、`SiteOptions`（サイト名など） | pokemon: `Services/MaintenanceInfo.cs`・`Services/ReportForm.cs`・SiteLinks（pokemon の Core 配下。ファイル名は実装時に確認）。avatar: `appsettings.json` の `Site`（§1.2 の 39） |
| `worker/`（共有 JS） | `maintenance.js`、`report.js`、`home.js`（ホームの HTML を返す。pokemon のみ必要。avatar は不要） | **pokemon と avatar の `worker/maintenance.js`・`worker/report.js` は内容が同一**（行数と diff で確認） |
| `build/`（Node ツール） | テンプレート（index.html）の `SEO-HEAD`・`SEO-BODY` の区切りへの差し込み、head（title・description・canonical・OGP・robots）の組み立て、`robots.txt`・`sitemap.xml`（本番のみ）の書き出し。入力は**ページ一覧の JSON**（path・title・description・canonical・noindex・bodyHtml）。本文は作らない（§3.3） | pokemon: `tools/build-entry-pages.mjs`・`tools/entry-pages.json`（origin/develop）を一般化 |
| `csp/` | `_headers` のテンプレート（ホストは各サイトが足す。avatar の初版は §4.9） | pokemon: `wwwroot/_headers` |
| `tests/` | 共通の検査（SiteLinks・MaintenanceInfo.Parse・ReportForm.Validate。xUnit）、Worker の Node テスト、`_headers` と build の検査 | 両サイトのテストから移す |
| `docs/` | 利用方法・変更の流れ・バージョン・各サイトの組み込み手順 | 新規 |

**avatar の本文入り HTML（D3）との整合**: 本文の HTML は avatar の C#（Markdig）が作り、kit の Node ツールにはページ一覧（JSON）を渡す。テンプレートの差し込み・head・sitemap・robots の決まりは kit が持つ。

### 10.2 利用方法（D13 = A）

| 方法 | 利点 | 欠点 |
| --- | --- | --- |
| **A. git submodule ＋ ProjectReference（決定）** | bot の `external/fara-pokemon-battle-assistant` と同じ流儀（コーディネーターの指示による。このリポジトリでは未確認）。Razor の変更をそのまま参照できる。CI は `submodules: true` | サブモジュールの更新を忘れる。Worker の JS を wrangler が submodule 内から読めるかは §8.1 で確かめる |
| B. NuGet（GitHub Packages）（非採用） | バージョンが明確 | 公開の手順が要る。静的アセットの配り方を作る必要がある。消費者が 2 つでは手間が上回る（推測） |
| C. monorepo（非採用） | 変更が一度で済む | 既存 2 リポの統合が大工事（推測） |

### 10.3 バージョン管理
- kit は SemVer のタグ（`v0.1.0` など）で切る。各サイトはタグのコミットに固定し、追従は手動の PR で行う。
- 破壊的変更（API・名前空間・ファイル名）はメジャーを上げる。サイトは古いタグのまま動かせる。
- 変更履歴は `docs/` の一覧に残す。

### 10.4 変更の流れ
1. kit に PR（テストを必ず付ける）→ マージ → タグ。
2. 各サイトで、サブモジュールをそのタグに上げる PR（develop 向け）→ 確認用サイトで確認 → master への反映はユーザーが判断（既存の運用どおり）。
3. サイト固有の変更は、サイトのリポジトリに置く（kit に入れない）。

### 10.5 pokemon から kit への切り出し（pokemon を壊さない順番）
1. §8.1 の検証を先に行う（試験ブランチ）。
2. **最初は Worker の JS**（K0-1。`maintenance.js`・`report.js`。同一内容なので両サイトで同じ変更にできる）。pokemon は `external/fara-site-kit` を submodule で追加し、既存のファイルは残したまま、`worker/index.js` の import 先だけを切り替える PR。dev で確認。
3. 次に C# の小さなクラス（K0-2。`MaintenanceInfo`・`ReportForm` の検証・SiteLinks）。**名前空間は変えない**（using を書き換えない）。ProjectReference を足し、既存ファイルを消すのは別 PR。
4. build ツール（K0-3）と Razor 部品（K0-4。MudProviders・フッター・MaintenanceBanner）。
5. 最後に NavMenu・MainLayout の基底（K0-5。手間が一番多い。サイト固有の項目を含むので、基底だけ kit に出す）。
6. 各段階の PR で、pokemon の `dotnet test`・Node テスト・`wrangler deploy --dry-run` が通ること、dev サイトで表示が変わらないことを確認してから、重複したファイルを消す。

### 10.6 4 リポジトリで同じ内容を保つファイルの扱い
本調査で中身を確認したのは **pokemon と avatar の 2 リポジトリ**。他のリポジトリ（bot・ポートフォリオ）の同一性は**未確認**（今回の対象外）。

| ファイル | 現状（2 リポの比較） | kit での扱い |
| --- | --- | --- |
| `worker/maintenance.js` | 内容が同一（104 行） | **kit へ（最優先・K0-1）** |
| `worker/report.js` | 内容が同一（150 行） | **kit へ（最優先・K0-1）** |
| `wwwroot/js/report.js` | 差あり（pokemon は JS の報告フォーム） | avatar は Blazor 化で不要になる。Turnstile の描画だけ kit の JS に置き、フォームは kit の Razor へ |
| `nav.js`（avatar のみ） | pokemon には無い | **kit に入れない**（avatar では廃止。Blazor の Router で代替） |
| `_headers` | 形が同じ（pokemon の内容を基準） | kit の `csp/` テンプレート（ホストは各サイト） |
| `build-entry-pages.mjs` | pokemon のみ（avatar は C# 側で本文を作る） | kit の `build/` へ（K0-3。一般化） |

### 10.7 フェーズへの組み込み（kit に入れるもの）

| フェーズ | kit に入れるもの | avatar 側の作業 |
| --- | --- | --- |
| **K0（新設）: kit の作成と pokemon からの切り出し** | Worker JS（K0-1）、C# Core（K0-2）、build ツール（K0-3）、Razor 部品（K0-4）、NavMenu 基底・ThemeSwitch（K0-5） | pokemon の submodule 化（§10.5） |
| P1 土台 | K0-1〜K0-4 を使う。SiteOptions | 静的生成（Build）・Worker の `/api/maintenance` ほか（§5） |
| P2 記事 | K0-5 を使う（NavMenu・ThemeSwitch）。記事・検索・練習は avatar 固有なので kit に入れない | 記事ページ一式は avatar に残す |
| P3 練習 | なし（練習の JS は avatar 固有） | — |
| P4 SEO | build ツールの sitemap・robots・OGP の仕上げ、csp テンプレート | avatar の C# がページ一覧 JSON を渡す |
| P5 切り替え | 使い方の確定（ドキュメント・テスト） | 旧 SSR の削除。pokemon 側の残りを kit へ寄せるかは完了後に再評価 |

### 10.8 CSP の方針（決定）
- **許す**: `script-src` に `'wasm-unsafe-eval'`、`style-src` に `'unsafe-inline'`（MudBlazor のため）。
- **許さない**: `script-src` の `'unsafe-inline'` と `'unsafe-eval'`。インラインの script は外部ファイルにし、importmap はハッシュで許可する（§4.9）。
- ホストは**限定する**（pokemon の `_headers` と同じ形。使うものだけ）。
- `csp/` テンプレートは kit に置き、各サイトはホストの欄だけを足す。

---

## 付録 A: 確認に使ったファイル（抜粋）

- avatar（`origin/develop` 3340302）: `Program.cs`、`Components/App.razor`・`Routes.razor`・`Layout/MainLayout.razor`・`Layout/SiteFooter.razor`・`Layout/SiteFooterLinks.cs`、`Components/Pages/**`、`Components/Shared/**`、`Data/ContentService.cs`・`Models.cs`・`SearchIndex.cs`、`Publishing/StaticSiteExporter.cs`、`worker/index.js`・`maintenance.js`・`report.js`、`wrangler.toml`、`.github/workflows/deploy.yml`、`README.md`、`appsettings.json`、`tests/FaraAvatarModdingGuide.Tests/ScrollProgressTests.cs`、`wwwroot/js/*.js`（先頭と規模）
- avatar 試作（`origin/experiment/wasm-mudblazor` 414747c）: `experiments/wasm/ContentExport/*`、`FaraAvatarWasm/*`、`build.sh`、`measure/measure.mjs`、`docs/results.json`
- ポケモン（`origin/develop` の `feature/seo-entry-pages` 由来）: `tools/build-entry-pages.mjs`、`worker/home.js`、`src/FaraPokemonBattleAssistant.Web/` の `Program.cs`・`Shared/MudProviders.razor`・`Shared/MainLayout.razor`・`Shared/NavMenu.razor`・`Services/MaintenanceInfo.cs`・`Services/ReportForm.cs`・`MaintenanceScreen.razor`（Web 直下）・`wwwroot/_headers`、`worker/index.js`（`/api/maintenance`）
- avatar PR #69: OPEN、base `develop`

## 付録 B: 用語

- **静的 HTML（事前生成）**: ビルド時に作る、ページごとの HTML。本文が入っている。
- **#app の引き継ぎ**: 静的 HTML の `#app` の中身を、WebAssembly の起動で置き換えること。
