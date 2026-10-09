# Fara Blender Manual

Blender の操作方法を、基本からアバター用衣装の作成まで順番に学べるマニュアルサイトです。
.NET 9 の Blazor Web App（静的サーバーサイドレンダリング）で実装し、記事は Markdown で管理しています。

## 特徴

- **学習ロードマップ**: 「はじめに → 基本操作 → モデリング → マテリアル → リギング → 実践」の順に進められる
- **1記事 = 1 URL**: 記事ごとにブックマーク・共有・検索エンジンからの到達ができる
- **ショートカット連携**: 各記事に「この記事で使うキー」を表示し、一覧ページで逆引きできる
- **Markdown で執筆**: `Content/` 以下にファイルを置くだけで記事を追加できる（コードの変更不要）
- **レスポンシブ**: スマートフォンでも閲覧可能

## 技術スタック

- .NET 9 / ASP.NET Core Blazor Web App（静的 SSR、SignalR 接続なし）
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
├── wwwroot/                  # 静的ファイル（CSS、画像）
├── Program.cs
└── appsettings.json          # Site セクション（サイト名、対象 Blender バージョン、リポジトリ URL）
```

## URL 一覧

| URL | 内容 |
| --- | --- |
| `/` | ホーム（ロードマップ） |
| `/learn` | 学習ロードマップ |
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

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照

## 貢献

Pull Request や記事の修正提案はいつでも歓迎です。Issue テンプレートを使って報告してください。
