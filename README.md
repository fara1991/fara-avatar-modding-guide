# Avatar Modding Guide

アバター改変のための Blender・Unity・VRChat の操作ガイドです。Blender でのモデリングから Unity での衣装・マテリアルの設定、VRChat へのアップロードまでをまとめています。
サイト名は `appsettings.json` の `Site:Title` にだけ書いてあり、画面の表示はすべてそこから取ります（名前を変えるときはそこだけ直す）。
.NET 10 の Blazor WebAssembly + MudBlazor で実装し、記事は Markdown で管理しています。ページごとの本文入りの静的 HTML をビルド時に作り（検索エンジン・JavaScript が無い環境向け）、読み込み後に WebAssembly が引き継ぎます。

> 2026-10 に、静的サーバーサイドレンダリング（Blazor Web App）から Blazor WebAssembly + MudBlazor に移行しました（計画と記録: [docs/wasm-migration-plan.md](docs/wasm-migration-plan.md)、切り替えと戻し方: [docs/release-switch-and-rollback.md](docs/release-switch-and-rollback.md)）。URL・本文・localStorage のキーは移行前と同じです。

## 特徴

- **目次**: Blender / Unity / VRChat / 通しで作る の区切りごとに、上から順に読める（区切りは `Content/learn/sections.yml`）
- **1記事 = 1 URL**: 記事ごとにブックマーク・共有・検索エンジンからの到達ができる
- **ショートカット連携**: 各記事に「この記事で使うキー」を表示し、一覧ページで逆引きできる
- **Markdown で執筆**: `Content/` 以下にファイルを置くだけで記事を追加できる（コードの変更不要）
- **操作の練習**: ブラウザ上の 3D ビューで Blender と同じキー操作を試せる（共同練習の部屋つき）
- **レスポンシブ**: スマートフォンでも閲覧可能

## 公開 URL

| 環境 | URL | ブランチ | Worker |
| --- | --- | --- | --- |
| 本番 | https://avatar.fara-labs.com | `master` | `fara-avatar` |
| 確認用 | https://avatar-dev.fara-labs.com（Cloudflare Access で自分だけが閲覧可能） | `develop` | `fara-avatar-dev` |

`wrangler.toml` の `name` はダッシュボードの Worker 名と一致させます（違うと別の Worker が作られる）。

**本番（master）へは、`develop` → `master` の Pull Request のマージ（ユーザーが行う）でしか届きません。** 作業のブランチは `develop` から切り、PR は `develop` 向けに作ります。

## 技術スタック

- .NET 10 / Blazor WebAssembly（ホスティングは静的ファイルのみ。サーバーで .NET は動かさない）
- [MudBlazor](https://mudblazor.com/)（画面部品）
- 共通部品: [fara-site-kit](https://github.com/fara1991/fara-site-kit)（`external/fara-site-kit`。git submodule。レイアウト・フッター・報告フォーム・メンテナンス表示・静的 HTML の生成・CSP・Worker の共有 JS）
- [Markdig](https://github.com/xoofx/markdig)（Markdown → HTML）と [YamlDotNet](https://github.com/aaubry/YamlDotNet)（フロントマター・データ）は、ビルド時だけ使う（ブラウザには載らない）
- 練習の 3D: C#（Blazor WebAssembly。Phase 3b S7 で three.js を外した。共有ルームも C#）
- Cloudflare Workers（Static Assets + Durable Objects）でホスティング

## セットアップ

### 前提条件

- .NET 10.0 SDK
- Node.js 22 以上（静的 HTML の生成に kit の Node ツールを使う。テストにも使う）
- `external/fara-site-kit` の submodule（無いとビルド・テストが失敗する）

```bash
git clone --recurse-submodules https://github.com/fara1991/fara-avatar-modding-guide.git
cd fara-avatar-modding-guide
# すでに clone してあるとき: git submodule update --init
```

### ビルドと確認

公開物は 3 段で作ります（CI の `.github/workflows/build.yml`・`deploy-develop.yml`・`deploy.yml` も同じ手順）。

```bash
# 1. WebAssembly のアプリ（publish/wwwroot）
dotnet publish src/FaraAvatarModdingGuide.Web -c Release -o publish

# 2. 公開物のコピーに、Build が本文入りの静的 HTML・データの JSON・robots・sitemap・_headers などを書き足す
#    --env develop    = 確認用（X-Robots-Tag: noindex・sitemap なし・canonical は avatar-dev）
#    --env production = 本番用（noindex なし・sitemap あり・canonical は avatar.fara-labs.com）
rm -rf dist && cp -r publish/wwwroot dist        # PowerShell: Remove-Item dist -Recurse -Force; Copy-Item publish/wwwroot dist -Recurse
dotnet run --project src/FaraAvatarModdingGuide.Build -c Release -- --template publish/wwwroot/index.html --out dist --env develop

# 3. Cloudflare と同じ動き（Worker・_headers の CSP・/api/maintenance）で配信する → http://localhost:8787
npx wrangler@4.149.0 dev --env dev
```

- `dist/` が wrangler の `assets`（`wrangler.toml`）です。本番用と確認用で内容が違うので、公開するときは **公開先の環境のビルドだけ** を置きます（CI は `site-production/` と `site-develop/` に作り、`deploy` ジョブが公開先の方だけを `dist` に取り出し、`.github/scripts/verify-site.mjs` で環境を確かめます）。
- 記事（`Content/`）を直したら、2 と 3 をやり直します（Build が記事を静的 HTML と `data/*.json` にするため、ブラウザのリロードだけでは変わりません）。アプリ（`src/FaraAvatarModdingGuide.Web`）を直したら、1 からやり直します。
- 検索の索引・正規化の確認: `SEARCH_INDEX_FILE=dist/search-index.json node --test tests/js/search.test.mjs`、`node tests/js/search-norm.mjs dist/data/search.json`。
- ローカル（WSL など）で `dotnet publish` の ILLink が断続的に落ちることがあります。コードの問題ではなく、再実行で通ります。

### テスト

```bash
dotnet test -c Release                       # Content.Tests・Build.Tests・Web.Tests・Practice.Tests（Build.Tests は Node を使う。Build.Tests は kit も）
node --test external/fara-site-kit/worker/report.test.mjs external/fara-site-kit/worker/maintenance.test.mjs worker/index.test.mjs
node --test .github/scripts/require-maintenance.test.mjs .github/scripts/verify-site.test.mjs
node --test tests/js/storage-migrate.test.mjs tests/js/theme-default.test.mjs tests/js/search-norm-fn.test.mjs tests/js/notice.test.mjs tests/js/practice-entry.test.mjs tests/js/head-sync.test.mjs
```

ブラウザでの動作確認（Playwright。CI では動かさない）は [tools/verify/README.md](tools/verify/README.md)。

## プロジェクト構造

```
fara-avatar-modding-guide/
├── Content/                  # 記事・データ（Markdown / YAML）。書き方は Content/README.md 参照
│   ├── learn/
│   │   ├── sections.yml      # 目次の大きな区切り（Blender / Unity / VRChat / 通しで作る）
│   │   ├── categories.yml    # ロードマップの段階と並び順（section で区切りを指定）
│   │   └── <category>/NN-slug.md
│   ├── tips/                 # 用語集・何て名前？・TIPS・改変 Q&A
│   ├── shortcuts.yml
│   ├── addons.yml
│   └── links.yml             # リンク集
├── src/
│   ├── FaraAvatarModdingGuide.Web/      # Blazor WebAssembly + MudBlazor のアプリ（ページ・検索・進捗・練習の組み込み）
│   │   └── wwwroot/          # index.html（Build のテンプレート）・css・js（入口・画像で見るの JavaScript）
│   ├── FaraAvatarModdingGuide.Build/    # 公開物の生成: 本文入りの静的 HTML・data/*.json・robots・sitemap・_headers（kit の Node ツールも使う）
│   ├── FaraAvatarModdingGuide.Content/  # Content/ を読む（Markdown → HTML）。Build が使う
│   ├── FaraAvatarModdingGuide.Shared/   # Build と Web が共有する型・サイドバーの項目
│   └── FaraAvatarModdingGuide.Practice/ # 練習の 3D の純粋なロジック（G/R/S・お題の判定・視点・レイ判定・編集モード・共有ルームの規則。Phase 3b。練習・記事内チュートリアル・共有ルームから呼ぶ。docs/phase3b-design.md）
├── wwwroot/images/           # 画像（記事内チュートリアルの手順画像。S7 以降は作り直せない。作り方は docs/phase3b-design.md の §17.7）。Build が公開物にコピーする
├── tests/
│   ├── FaraAvatarModdingGuide.Content.Tests / Build.Tests / Web.Tests   # xUnit（Web.Tests は bUnit。公開の設定の検査 = DeployWorkflowTests を含む）
│   ├── FaraAvatarModdingGuide.Practice.Tests   # xUnit。Practice を、凍結した基準（tests/js/golden/*.json.gz。three.js の版から作った）と突き合わせる（Node は使わない）
│   └── js/                   # Node のテスト。fixtures/searchcore.reference.js は検索の正解（旧 SSR 版の写しを固定したもの）
├── tools/
│   ├── site.json             # 公開先の設定（origin・devOrigin・CSP・robots）。Build が kit に渡す
│   ├── verify/               # Playwright での確認スクリプト（練習・SEO・CSP・起動の前後の差など）
│   ├── screenshots/          # 記事用スクリーンショットの自動撮影（Blender を自動操作）。tools/screenshots/README.md 参照
│   ├── icons/                # 練習の JS が使う Bootstrap Icons を、CSS だけで描く css/bi-icons.css に書き出す
│   └── room-check/           # 共同練習の部屋（Durable Object）の動作確認スクリプト
├── worker/index.js           # Cloudflare Worker（/api/* と共同練習の部屋の Durable Object）。報告・メンテナンスの判定は kit の共有 JS
├── external/fara-site-kit/   # 共通部品（git submodule）
├── .github/
│   ├── workflows/            # build.yml（ビルド・テスト）・deploy-develop.yml（develop → 確認用）・deploy.yml（master → 本番）
│   └── scripts/              # require-maintenance.mjs（本番の公開のゲート）・verify-site.mjs（環境の取り違えを止める）
├── docs/                     # wasm-migration-plan.md（移行の計画と記録）・release-switch-and-rollback.md（切り替えと戻し方）
├── wrangler.toml
└── appsettings.json          # Site セクション（サイト名、対象 Blender バージョン、リポジトリ URL、フッターの SNS）。Build とテストが読む
```

## URL 一覧

| URL | 内容 |
| --- | --- |
| `/` | ホーム（ロードマップ） |
| `/learn` | 目次 |
| `/practice` | 操作の練習（ブラウザ上の 3D ビューで Blender と同じキー操作を試す） |
| `/practice/room?room=<ID>` | みんなで練習（同じ 3D ビューを複数人で操作する部屋） |
| `/practice/unity` | Unity の練習（Unity エディターに似た画面で、Modular Avatar で衣装を着せる流れを試す） |
| `/glossary` `/howto` `/tips` `/qa` | 用語集・あの操作、何て名前？・TIPS・改変 Q&A |
| `/links` | リンク集（Unity のツール・参考記事・BOOTH の商品など） |
| `/learn/{category}` | カテゴリ内の記事一覧 |
| `/learn/{category}/{slug}` | 記事 |
| `/shortcuts?category=…` | ショートカット一覧（カテゴリで絞り込み） |
| `/addons` | おすすめアドオン |
| `/changelog` `/search` `/report` `/privacy` | 更新履歴・検索・作者への報告（Turnstile 付き）・プライバシーポリシー |

旧 URL（`/operations`, `/avatar-clothing`, `/plugins`）は新 URL へ 301 で転送されます（`_redirects`）。静的 HTML は 76 本、存在しないパスは `404.html`（404 のステータス）です。

各記事の「このページを完了にする」で、読み終えたページを記録できます（ログインなし。ブラウザの localStorage の `avatar-modding-guide.progress` に保存。移行前と同じキー）。目次・カテゴリ・サイドバー・ホームに完了の印と件数が出ます。

## みんなで練習（共同練習の部屋）

`/practice/room` で「部屋を作る」と `?room=<ID>` 付きの URL ができ、その URL を開いた人どうしで 1 つの 3D ビューを操作できます。ログインはありません。

```
ブラウザ（C#。src/FaraAvatarModdingGuide.Web の PracticeRoom.razor・room-cs.js。3D は Practice・Render）
   │  WebSocket  /api/rooms/<ID>/ws
   ▼
Worker（worker/index.js）── 静的ファイルは env.ASSETS（run_worker_first = true。メンテナンス・報告・/api/maintenance のため全リクエストが通る）
   │  部屋の ID ごとに 1 つ
   ▼
Durable Object「Room」（SQLite 版。WebSocket Hibernation）
   - シーン（オブジェクトの種類・名前・位置・回転・スケール）を保存し、入った人に丸ごと送る
   - 確定した変更（追加・変形・削除）を全員に配る。同じオブジェクトへの変更はサーバーに届いた順（後勝ち）
   - 操作中のオブジェクトのロック、操作中の変形・カーソル・選択・視点の中継（保存しない）
```

- 共有するのはオブジェクトモードの追加・削除・複製・移動・回転・スケール。**編集モードの変更は自分の画面だけ**
- 上限: 1 部屋 8 人、オブジェクト 200 個、1 メッセージ 16 KB、1 人あたり平均 40 通 / 秒（瞬間 80 通）。表示名は 20 文字（制御文字は除去、画面には文字として表示）
- サーバーは受け取ったメッセージの形式と値の範囲（座標 ±1000 m、スケール 0.001〜1000、種類は 6 種類など）をすべて確かめ、不正なものは捨てる。不正なものが続くと切断
- 最後の変更から 24 時間で部屋の内容を消す（Durable Object のアラーム）。60 秒何も届かない接続は外す（クライアントは 20 秒ごとに ping。ping の応答は Durable Object を起こさない）
- ほかの人の変更を受け取ると、取り消し（<kbd>Ctrl</kbd>+<kbd>Z</kbd>）の記録は消える（食い違いを防ぐため）
- 部屋は Worker（Durable Object）が要るので、上の「ビルドと確認」の `wrangler dev` で確かめる。`node tools/room-check/check.mjs http://localhost:8787` で同期・上限・入力の検査をまとめて確かめられる

## 記事を追加する

1. `Content/learn/<category>/` に `NN-slug.md` を作る（`NN` は表示順、`slug` が URL になる）
2. 先頭の YAML フロントマターに `title` / `description` / `difficulty` などを書く
3. 本文を Markdown で書く（`::: tip` / `::: warning` ブロック、操作のチュートリアルを埋め込む `::: practice <名前>` が使える）

詳しくは [Content/README.md](Content/README.md) を参照してください。
新しいカテゴリを追加するときは `Content/learn/categories.yml` に追記し（`section` で Blender / Unity / VRChat / 通しで作る のどこに入るかを指定）、同名のディレクトリを作ります。区切り自体は `Content/learn/sections.yml` です。

## デプロイ（Cloudflare）

> **本番（avatar.fara-labs.com）に届く経路は 1 つだけ: `master` への push（`develop` → `master` の PR のマージ）と、`master` を選んだ手動実行（Actions の Run workflow）。**
> develop への push は確認用（avatar-dev）にしか届きません。ほかのブランチ・Pull Request は、ビルドとテスト、`wrangler deploy --dry-run` だけで、公開しません。
> `develop` → `master` の PR のマージは、中身（特に `deploy.yml`・`deploy-develop.yml`・`wrangler.toml`・Worker）を確かめてからユーザーが行います。Claude は PR の作成までで、master にはマージしません。

ワークフローは 3 つです（fara-pokemon-battle-assistant・fara-portfolio-site と同じ形）。公開の 2 つは自分でビルドして公開し、成果物（artifact）は使いません。

| ファイル（Actions での名前） | 動くとき | 内容 |
| --- | --- | --- |
| `build.yml`（Build and test） | Pull Request と、develop・master への push | ビルド・テスト・本番用と確認用の両方の出力・環境の確認・dry-run。公開しない |
| `deploy-develop.yml`（Deploy (develop)） | `develop` への push と、develop を選んだ手動実行 | 確認用の出力を作り、`fara-avatar-dev`（`--env dev`）に公開 |
| `deploy.yml`（Deploy (production)） | `master` への push と、master を選んだ手動実行 | 本番用の出力を作り、`fara-avatar`（`--env production`）に公開（下の「本番の公開とメンテナンス」） |

`build.yml` は、本番用（`--env production`）と確認用（`--env develop`）の出力を別のフォルダに作り、`verify-site.mjs` で「本番用は noindex なし・`sitemap.xml` あり・canonical が `https://avatar.fara-labs.com/`、確認用はその逆」を確かめ、`wrangler deploy --dry-run` を両方の環境で実行します。公開の 2 つは、その環境の出力だけを作り、公開の直前に `verify-site.mjs` で確かめます（取り違えていれば止まります）。さらに、`production` へは master 以外から、`dev` へは develop 以外からは、公開のステップが止まります。

### 初回設定

1. GitHub リポジトリの **Settings → Secrets and variables → Actions** に以下を登録（値は fara-portfolio-site と同じものを使えます）
   - Secrets: `CLOUDFLARE_API_TOKEN`（テンプレート Edit Cloudflare Workers、Zone Resources は `fara-labs.com`）
   - Variables: `CLOUDFLARE_ACCOUNT_ID`
2. Cloudflare の `fara-labs.com` の **DNS → Records** に `avatar` / `avatar-dev` のレコードが無いことを確認（`wrangler.toml` の `custom_domain = true` で自動作成される）
3. 開発環境を自分だけに見せるため、Cloudflare **Zero Trust → Access → Applications** の確認用アプリケーションに public hostname `avatar-dev.fara-labs.com` を追加します
4. 報告フォーム（`/report`）の変数（`TURNSTILE_SITE_KEY`・`TURNSTILE_SECRET`・`DISCORD_WEBHOOK_URL`）を、各 Worker（`fara-avatar`・`fara-avatar-dev`）にダッシュボードで設定します。`wrangler.toml` の `keep_vars = true` で、公開のたびに消えません

### 本番の公開とメンテナンス（MAINTENANCE_MODE）

- Worker の変数 `MAINTENANCE_MODE`（`true` = 全体メンテナンス・全ページ 503 / `notice` = お知らせ帯 / `false` または未設定 = 通常）と `MAINTENANCE_MESSAGE`（文面）で切り替えます。旧名 `MAINTENANCE` は読みません（残っていても無視。ダッシュボードで削除してかまいません）。
- 切り替えは **Actions → Deploy (production) → Run workflow**（Use workflow from = `master`）、確認用は **Actions → Deploy (develop) → Run workflow**（`develop`）で、`maintenance_mode`（true / notice / false / 空）と `maintenance_message`（空 = 変更しない）を入れて実行します。4 サイトまとめては `external/fara-site-kit/tools/maintenance.mjs`（`external/fara-site-kit/docs/maintenance-cli.md`）。実行ページ下部のジョブサマリーに、設定した値と反映後の現在値が出ます。
- **本番の push（master）と、何も指定しない手動実行**は、公開の前に `MAINTENANCE_MODE` が `notice` か `true` かをゲート（`.github/scripts/require-maintenance.mjs`）で確かめ、公開と同時に `--var MAINTENANCE_MODE:true`（メンテナンス画面）にします。作業が終わったら `maintenance_mode` に `false` を入れて Run workflow（master）で通常に戻します（お知らせも一緒に消える。このとき、その時点の master の内容がすべて公開されます）。
  - `maintenance_mode` を指定した手動実行はゲートを飛ばし、その値を渡します。`maintenance_message` だけの手動実行は `MAINTENANCE_MODE` を変えません。
  - `skip_maintenance_check` は確認を飛ばし、`MAINTENANCE_MODE` を作りも変えもしません（**お知らせなし・メンテナンス画面なしで、すぐ公開されます**。緊急の修正用）。
- **本番の初回の公開**（Worker `fara-avatar` がまだ無い）: ゲートは Worker の不在（Cloudflare API が 404）を「初回の公開」として通します。master への push（または `maintenance_mode` を空にした手動実行）で `--var MAINTENANCE_MODE:true` が付くので、**Worker・カスタムドメイン・Durable Object が作られ、全ページがメンテナンス画面（503）のまま公開されます**。
  1. 公開前の確認は avatar-dev で行う（同じコードの確認用ビルド。本番は 503 で、中身は見えない）
  2. master への初回の push のあと、ダッシュボードで `fara-avatar` に報告の変数（上の初回設定の 4）を設定する
  3. Actions → Deploy (production) → Run workflow（Use workflow from: `master`）で `maintenance_mode` に `false`（先にお知らせを見せるなら `notice`）を入れて実行 → 通常公開になる
  - `skip_maintenance_check` は初回には使わない（メンテナンス画面を経由せず、すぐ公開されてしまう）。
- 切り替えの手順・戻し方・Cloudflare 側の後始末は [docs/release-switch-and-rollback.md](docs/release-switch-and-rollback.md)。

### 仕組み

- `dotnet publish` が WebAssembly のアプリ一式（`_framework/`・`index.html`・css・js）を作り、Build が `Content/` の記事から本文入りの静的 HTML（76 本）・`404.html`・`data/*.json`・`search-index.json`・`robots.txt`・`sitemap.xml`（本番のみ）・`_headers`（CSP。確認用は `X-Robots-Tag: noindex`）・`_redirects`（旧 URL の 301）を書き足します。ルートの `wwwroot/images/` と `Content/learn/**/images/` もコピーされます。
- 静的 HTML は読み込み前でも本文が読める（JavaScript が無くても読める）ようにしたもので、WebAssembly が起動すると同じ DOM・同じ class のまま引き継ぎます（起動の前後の差は 0px。`tools/verify/boot-diff.mjs`）。
- すべてのリクエストが Worker を通るので（`run_worker_first = true`）、静的ファイルの配信も Workers のリクエスト数に数えられます（無料プランは 1 日 10 万リクエスト）。
- 記事を追加したら、develop に push するだけで確認用に反映されます。

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照

## 貢献

Pull Request や記事の修正提案はいつでも歓迎です。Issue テンプレートを使って報告してください。
