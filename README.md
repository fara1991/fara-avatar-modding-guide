# FaraBlenderManual

Blenderの操作方法をまとめたレスポンシブWebサイトです。Blazor Server Appとして実装されており、操作方法、ショートカット一覧、プラグインおすすめ、アバター服作成方法などの情報を提供します。

## 機能

- **操作方法表示**: Blenderの基本操作から応用まで
- **ショートカット一覧**: よく使用されるキーボードショートカット
- **プラグインおすすめ一覧**: 便利なアドオンの紹介
- **アバター服作成方法**: VRChat等で使用するアバター用衣装の作成手順
- **レスポンシブデザイン**: スマートフォンでも快適に閲覧可能

## 技術スタック

- **.NET 8.0**
- **Blazor Server**
- **Bootstrap 5**: レスポンシブデザイン
- **C#**: サーバーサイドロジック

## セットアップ手順

### 前提条件

- .NET 8.0 SDK
- Visual Studio 2022 または Visual Studio Code

### 1. リポジトリのクローン

```bash
git clone https://github.com/yourusername/FaraBlenderManual.git
cd FaraBlenderManual
```

### 2. 依存関係の復元

```bash
dotnet restore
```

### 3. アプリケーションの実行

```bash
dotnet run
```

またはVisual Studioで `FaraBlenderManual.sln` を開いてF5キーで実行

### 4. ブラウザでアクセス

`https://localhost:7000` または `http://localhost:5000` でアクセス可能

## プロジェクト構造

```
FaraBlenderManual/
├── Components/           # Blazorコンポーネント
├── Data/                # データモデル
├── Pages/               # ページコンポーネント
├── wwwroot/             # 静的ファイル
├── Program.cs           # アプリケーションエントリーポイント
└── appsettings.json     # 設定ファイル
```

## 開発ガイドライン

### 新しいページの追加

1. `Pages/` ディレクトリに新しい `.razor` ファイルを作成
2. `@page "/route"` ディレクティブでルートを設定
3. `Components/Layout/NavMenu.razor` にナビゲーションリンクを追加

### データモデルの追加

1. `Data/` ディレクトリに新しいクラスを作成
2. 必���に応じて `Program.cs` でサービスを登録

## 自動機能開発について

このリポジトリはClaude AIによる自動コード生成機能を搭載しています。

### 使用方法

1. GitHub Issueを作成
2. 以下のラベルのいずれかを付与：
   - `feature`: 新機能
   - `bug`: バグ修正
   - `enhancement`: 改善
   - `refactor`: リファクタリング
3. 詳細な要件をIssueに記述
4. 自動的にPull Requestが作成されます

### 注意事項

- `ANTHROPIC_API_KEY` シークレットの設定が必要
- `.github/workflows/` 配下のファイルは自動変更されません

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照

## 貢献

Pull Requestや機能提案はいつでも歓迎です。Issue テンプレートを使用して報告してください。

## サポート

問題が発生した場合は、GitHub Issuesで報告してください。
