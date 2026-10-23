# CLAUDE.md

Avatar Modding Guide（https://avatar.fara-labs.com/）。.NET 10 / Blazor WebAssembly + MudBlazor の静的サイトを Cloudflare Workers（静的アセット + Durable Object）に公開する。詳細は README.md、移行の計画と記録は docs/wasm-migration-plan.md、切り替えと戻し方は docs/release-switch-and-rollback.md。

## ブランチ運用（必ず守る）

- 作業ブランチは `develop` から作成する（`master` から切らない）。作業ブランチの PR は `develop` 向けに作る
- `master` へは `develop` からのみマージする。**master へのマージはユーザーが行う。Claude は develop → master の PR の作成までで、master にはマージしない。master 向けの PR は、頼まれたときだけ作る**
- **本番（avatar.fara-labs.com）に届く経路は、master への push（と、master を選んだ手動実行）だけ。** develop への push は確認用（avatar-dev.fara-labs.com）に自動公開される。ほかのブランチ・PR は公開されない（ビルドとテストだけ）
- 2026-10-23 に、別リポジトリで develop → master の PR を取り違えてマージし、本番がメンテナンス画面（503）で公開された事故があった。master 向け・本番向けの変更（`deploy.yml`・`wrangler.toml`・`worker/`・`.github/scripts/`）の PR 本文には、大きく警告を書く
- 既存の `develop` → `master` の PR（#4）は古い題名のまま。統合のあとは WebAssembly 版への切り替えそのものになるので、そのままマージしない（docs/release-switch-and-rollback.md）
- 履歴の書き換え（force push・rebase の push）はしない。コミットの末尾に `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`

## 構成メモ

- 記事・データは `Content/`（Markdown / YAML。書き方は Content/README.md）。サイト名・Blender バージョン・SNS は `appsettings.json` の `Site`（Build とテストが読む。ASP.NET の設定ではない）
- `src/FaraAvatarModdingGuide.Web`: Blazor WebAssembly のアプリ。`wwwroot/js/` の viewport.js・editmesh.js・unity.js・scenarios.js・bgscene.js は旧 SSR 版から書き換えずに持ってきた練習の 3D の本体
- `src/FaraAvatarModdingGuide.Build`: 本文入りの静的 HTML・`data/*.json`・robots・sitemap・`_headers`・`_redirects` を、`dotnet publish` の出力に書き足す。本番用（`--env production`）と確認用（`--env develop`）で内容が違う（sitemap・noindex・canonical）
- `src/FaraAvatarModdingGuide.Content`・`Shared`: 記事を読む部分（ビルド時のみ）と、Build と Web が共有する型
- ルートの `wwwroot/images/` は画像だけ（Build がコピーする。`tools/tutorial-images` が書く）
- 共通部品は `external/fara-site-kit`（git submodule。直接は直さず、kit のリポジトリで直す）。`worker/index.js` は kit の共有 JS（報告・メンテナンス）を読む
- 公開の設定は `wrangler.toml`（`name` はダッシュボードの Worker 名と一致させる。assets は `./dist`）と `.github/workflows/deploy.yml`。wrangler のバージョンは `4.149.0` で固定
- メンテナンス表示は Worker の変数 `MAINTENANCE_MODE`（`true` / `notice` / `false`）と `MAINTENANCE_MESSAGE`。旧名 `MAINTENANCE` は読まない。本番の初回の公開は `MAINTENANCE_MODE:true` が付き、メンテナンス画面のまま公開される（README の「本番の公開とメンテナンス」）
- 検索の「正解」は `tests/js/fixtures/searchcore.reference.js`（旧 SSR 版の写しを固定したもの）。仕様を変えるとき以外は書き換えない
- スクロール演出・テーマ・検索などの UI の実装は kit と MudBlazor。`app.css` は Web の `wwwroot/css/` にある（色は MudBlazor のテーマ変数）

## 確認

```bash
dotnet test -c Release                     # Content.Tests・Build.Tests・Web.Tests（Node と submodule が要る）
node --test external/fara-site-kit/worker/*.test.mjs worker/index.test.mjs .github/scripts/*.test.mjs tests/js/*.test.mjs   # search.test.mjs は SEARCH_INDEX_FILE=dist/search-index.json が要る
# ビルドと配信（README の「ビルドと確認」）: publish → Build → wrangler dev
dotnet publish src/FaraAvatarModdingGuide.Web -c Release -o publish
rm -rf dist && cp -r publish/wwwroot dist
dotnet run --project src/FaraAvatarModdingGuide.Build -c Release -- --template publish/wwwroot/index.html --out dist --env develop
npx wrangler@4.149.0 dev --env dev         # http://localhost:8787
npx wrangler@4.149.0 deploy --dry-run --env production   # dist に --env production のビルドを置いて。公開はしない
```

- `tests/js/search.test.mjs` と `search-norm.mjs` は、Build の出力（`SEARCH_INDEX_FILE` / `data/search.json`）を読む
- ブラウザでの確認（Playwright）は tools/verify/README.md。`playwright-core` などは作業用のフォルダに入れて実行する（リポジトリには入れない）
- ローカルで `dotnet publish` の ILLink が断続的に落ちることがある。コードの問題ではないので、まず再実行する
- 作業用のサーバー（wrangler dev など）は、終わったら自分の PID だけを止める（`pkill -f dotnet` や `taskkill /IM` のような広い停止はしない）
