# Blender Manual

Blender の操作方法を、基本からアバター用衣装の作成まで順番にまとめたマニュアルサイトです。
.NET 9 の Blazor Web App（静的サーバーサイドレンダリング）で実装し、記事は Markdown で管理しています。

## 特徴

- **目次**: 「はじめに → 基本操作 → モデリング → マテリアル → リギング → 実践」の順に読める
- **1記事 = 1 URL**: 記事ごとにブックマーク・共有・検索エンジンからの到達ができる
- **ショートカット連携**: 各記事に「この記事で使うキー」を表示し、一覧ページで逆引きできる
- **Markdown で執筆**: `Content/` 以下にファイルを置くだけで記事を追加できる（コードの変更不要）
- **レスポンシブ**: スマートフォンでも閲覧可能

## 公開 URL

| 環境 | URL | ブランチ |
| --- | --- | --- |
| 本番 | https://blender.fara-labs.com | `master` |
| 開発 | https://blender-dev.fara-labs.com（Cloudflare Access で自分だけが閲覧可能） | `develop` |

## 技術スタック

- .NET 9 / ASP.NET Core Blazor Web App（静的 SSR、SignalR 接続なし）
- 公開時は静的サイトとして書き出し、Cloudflare Workers (Static Assets) でホスティング
- [Markdig](https://github.com/xoofx/markdig)（Markdown → HTML）
- [YamlDotNet](https://github.com/aaubry/YamlDotNet)（フロントマター・データファイル）
- Bootstrap 5.3 / Bootstrap Icons（CDN）

## セットアップ

### 前提条件

- .NET 9.0 SDK

### 実行

```bash
git clone https://github.com/fara1991/fara-blender-manual.git
cd fara-blender-manual
dotnet run
```

起動後、`Properties/launchSettings.json` に書かれた URL（既定では `https://localhost:55505`）を開いてください。
開発環境では `Content/` の変更がブラウザをリロードするだけで反映されます。

## プロジェクト構造

```
fara-blender-manual/
├── Content/                  # 記事・データ（Markdown / YAML）。書き方は Content/README.md 参照
│   ├── learn/
│   │   ├── categories.yml    # ロードマップの段階と並び順
│   │   └── <category>/NN-slug.md
│   ├── shortcuts.yml
│   └── addons.yml
├── Components/
│   ├── App.razor             # HTML ドキュメントのルート
│   ├── Routes.razor          # ルーター・NotFound
│   ├── Layout/               # サイドバー付きレイアウト
│   ├── Pages/                # ページ（/、/learn/...、/shortcuts、/addons、/error）
│   └── Shared/               # ロードマップ、記事カードなどの共通部品
├── Data/
│   ├── Models.cs             # 記事・ショートカット・アドオンのモデル
│   └── ContentService.cs     # Content/ を読み込んで提供するサービス
├── wwwroot/                  # 静的ファイル（CSS、画像、JS）
│   ├── js/
│   │   ├── viewport.js       # Blender と同じキー操作の 3D ビュー（Three.js）。/practice と記事内チュートリアルで共用
│   │   ├── editmesh.js       # 編集モードのメッシュ（頂点・辺・面）と、押し出し・ループカットなどの操作
│   │   ├── practice.js       # /practice のお題
│   │   ├── tutorial.js       # 記事内チュートリアル（::: practice）の案内
│   │   ├── scenarios.js      # 記事内チュートリアルの手順
│   │   ├── room.js           # みんなで練習（部屋）の通信
│   │   └── progress.js       # ページの完了の記録（localStorage）
│   └── images/tutorials/     # 記事内チュートリアルの手順画像（tools/tutorial-images/ で生成）
├── tools/
│   ├── screenshots/          # 記事用スクリーンショットの自動撮影（Blender を自動操作）。tools/screenshots/README.md 参照
│   ├── tutorial-images/      # 手順画像を、サイトの 3D ビューをヘッドレス Chrome で動かして撮る。Content/README.md 参照
│   └── room-check/           # 共同練習の部屋（Durable Object）の動作確認スクリプト
├── worker/index.js           # Cloudflare Worker（/api/* だけ）と、共同練習の部屋の Durable Object
├── Program.cs
└── appsettings.json          # Site セクション（サイト名、対象 Blender バージョン、リポジトリ URL）
```

## URL 一覧

| URL | 内容 |
| --- | --- |
| `/` | ホーム（ロードマップ） |
| `/learn` | 目次 |
| `/practice` | 操作の練習（ブラウザ上の 3D ビューで Blender と同じキー操作を試す） |
| `/practice/room?room=<ID>` | みんなで練習（同じ 3D ビューを複数人で操作する部屋） |
| `/learn/{category}` | カテゴリ内の記事一覧 |
| `/learn/{category}/{slug}` | 記事 |
| `/shortcuts?category=…` | ショートカット一覧（カテゴリで絞り込み） |
| `/addons` | おすすめアドオン |

旧 URL（`/operations`, `/avatar-clothing`, `/plugins`）は新 URL へリダイレクトされます。

各記事の「このページを完了にする」で、読み終えたページを記録できます（ログインなし。ブラウザの localStorage の `blender-manual.progress` に保存）。目次・カテゴリ・サイドバー・ホームに完了の印と件数が出ます。

## みんなで練習（共同練習の部屋）

`/practice/room` で「部屋を作る」と `?room=<ID>` 付きの URL ができ、その URL を開いた人どうしで 1 つの 3D ビューを操作できます。ログインはありません。

```
ブラウザ（wwwroot/js/room.js + viewport.js）
   │  WebSocket  /api/rooms/<ID>/ws
   ▼
Worker（worker/index.js）── /api/* 以外は dist/ の静的ファイル（run_worker_first = ["/api/*"]）
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
- `dotnet run` だけでは `/api` がないので部屋は使えない。確認は書き出してから `npx wrangler dev`（Durable Object もローカルで動く）。`node tools/room-check/check.mjs http://localhost:8787` で同期・上限・入力の検査をまとめて確かめられる

## 記事を追加する

1. `Content/learn/<category>/` に `NN-slug.md` を作る（`NN` は表示順、`slug` が URL になる）
2. 先頭の YAML フロントマターに `title` / `description` / `difficulty` などを書く
3. 本文を Markdown で書く（`::: tip` / `::: warning` ブロック、操作のチュートリアルを埋め込む `::: practice <名前>` が使える）

詳しくは [Content/README.md](Content/README.md) を参照してください。
新しいカテゴリを追加するときは `Content/learn/categories.yml` に追記し、同名のディレクトリを作ります。

## デプロイ（Cloudflare）

`master` / `develop` への push で `.github/workflows/deploy.yml` が動き、静的サイトを書き出して Cloudflare Workers にデプロイします（静的ファイル + `worker/index.js` の Worker と Durable Object。設定は `wrangler.toml`）。Pull Request ではビルドと書き出しだけ行います。

```bash
# ローカルで書き出して確認する
dotnet run -- --export dist
npx wrangler dev            # http://localhost:8787 で dist/ を配信
npx wrangler deploy --env dev   # 手動で develop 環境へ（本番は --env production）
```

### 初回設定

1. GitHub リポジトリの **Settings → Secrets and variables → Actions** に以下を登録（値は fara-portfolio-site と同じものを使えます）
   - Secrets: `CLOUDFLARE_API_TOKEN`（テンプレート Edit Cloudflare Workers、Zone Resources は `fara-labs.com`）
   - Variables: `CLOUDFLARE_ACCOUNT_ID`
2. Cloudflare の `fara-labs.com` の **DNS → Records** に `blender` / `blender-dev` のレコードが無いことを確認
3. 最初のデプロイで `wrangler.toml` の `custom_domain = true` により Cloudflare が DNS レコードと証明書を自動作成します（数分かかります）
4. 開発環境を自分だけに見せるため、Cloudflare **Zero Trust → Access → Applications** の確認用アプリケーションに public hostname `blender-dev.fara-labs.com` を追加します
   - 開発環境には `X-Robots-Tag: noindex` も付けています

### 仕組み

- `dotnet run -- --export dist` が自身を起動して全ページを取得し、`dist/` に `index.html`、`learn/basics/transform.html` などを書き出します（`Publishing/StaticSiteExporter.cs`）
- `wwwroot/` と `Content/learn/**/images/` もコピーされます
- 旧 URL のリダイレクトは `dist/_redirects`、404 は `dist/404.html` で処理します
- 記事を追加したら push するだけで反映されます

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照

## 貢献

Pull Request や記事の修正提案はいつでも歓迎です。Issue テンプレートを使って報告してください。
