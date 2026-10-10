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
├── tools/screenshots/        # 記事用スクリーンショットの自動撮影（Blender を自動操作）。tools/screenshots/README.md 参照
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
├── wwwroot/                  # 静的ファイル（CSS、画像）
├── Program.cs
└── appsettings.json          # Site セクション（サイト名、対象 Blender バージョン、リポジトリ URL）
```

## URL 一覧

| URL | 内容 |
| --- | --- |
| `/` | ホーム（ロードマップ） |
| `/learn` | 目次 |
| `/practice` | 操作の練習（ブラウザ上の 3D ビューで Blender と同じキー操作を試す） |
| `/learn/{category}` | カテゴリ内の記事一覧 |
| `/learn/{category}/{slug}` | 記事 |
| `/shortcuts?category=…` | ショートカット一覧（カテゴリで絞り込み） |
| `/addons` | おすすめアドオン |

旧 URL（`/operations`, `/avatar-clothing`, `/plugins`）は新 URL へリダイレクトされます。

## 記事を追加する

1. `Content/learn/<category>/` に `NN-slug.md` を作る（`NN` は表示順、`slug` が URL になる）
2. 先頭の YAML フロントマターに `title` / `description` / `difficulty` などを書く
3. 本文を Markdown で書く（`::: tip` / `::: warning` ブロックが使える）

詳しくは [Content/README.md](Content/README.md) を参照してください。
新しいカテゴリを追加するときは `Content/learn/categories.yml` に追記し、同名のディレクトリを作ります。

## デプロイ（Cloudflare）

`master` / `develop` への push で `.github/workflows/deploy.yml` が動き、静的サイトを書き出して Cloudflare Workers (Static Assets) にデプロイします。Pull Request ではビルドと書き出しだけ行います。

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
