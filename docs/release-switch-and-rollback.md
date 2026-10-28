# 切り替え（develop への統合）と、戻し方

Blazor WebAssembly + MudBlazor への移行（[wasm-migration-plan.md](wasm-migration-plan.md)）の Phase 5。この文書は、(1) 本番に届く経路、(2) 統合から本番の初公開までの手順（ユーザーの作業）、(3) 戻し方、をまとめる。公開の仕組み（build.yml・deploy-develop.yml・deploy.yml）は README の「デプロイ（Cloudflare）」。

## 1. 本番に届く経路

2026-10-23 に、別のリポジトリで `develop` → `master` の PR を取り違えてマージし、本番がメンテナンス画面（503）で公開された事故があった。このリポジトリでは、本番に届く経路を 1 つに絞り、そのことを設定とテストで固定している。

| 操作 | 届く先 | 備考 |
| --- | --- | --- |
| `master` への push（`develop` → `master` の PR のマージを含む） | **本番** `fara-avatar`（avatar.fara-labs.com） | 唯一の経路。公開の前にゲート（`MAINTENANCE_MODE` が notice か true、または Worker がまだ無い）を通り、公開と同時に `MAINTENANCE_MODE:true` が付く |
| Actions の手動実行で `master` を選ぶ | **本番** | 入力（`maintenance_mode` など）で表示を切り替える。選べるのは master / develop だけ |
| `develop` への push、手動実行で `develop` を選ぶ | 確認用 `fara-avatar-dev`（avatar-dev.fara-labs.com） | |
| それ以外のブランチ（`migration/wasm`・作業ブランチ）・Pull Request | **どこにも公開しない** | ビルド・テスト・`wrangler deploy --dry-run` だけ |

この Phase 5 の作業（`migration/wasm` 向けの 3 つの PR と、`migration/wasm` → `develop` の統合 PR）は、どれも `master` に触れない。`develop` に入ると確認用（avatar-dev）に出るところまで。`develop` → `master` の PR を作る・マージするのは、統合のあと、ユーザーが判断する。

守りは 4 重:
1. 公開のワークフローの `on` と `if`（`deploy.yml` は master、`deploy-develop.yml` は develop の push と手動実行だけ）
2. 公開のステップの先頭で、`production` は master から、`dev` は develop からしか公開しない（それ以外は wrangler を呼ぶ前に止まる）
3. `verify-site.mjs`: 公開先の環境の出力か（本番用: noindex なし・`sitemap.xml` あり・canonical が avatar.fara-labs.com。確認用: その逆）を、build.yml と公開のワークフロー（公開の直前）で確かめる
4. `Web.Tests` の `DeployWorkflowTests`: 上の 1〜3 と、`wrangler deploy` が必ず `--env` を付けること、本番のファイルに確認用の名前が、確認用のファイルに本番の名前が出てこないこと、公開のステップを実際に bash で動かした引数（`MAINTENANCE_MODE:true` が付く場面・付かない場面）を確かめる

## 2. 統合から本番の初公開まで（ユーザーの作業）

### 2.1 統合（avatar-dev が WebAssembly 版になる）

1. Phase 5 の PR を、番号の順に `migration/wasm` にマージする（(1/3) CI → (2/3) 旧 SSR の削除 → (3/3) ドキュメント。後の PR は前の PR の変更を含む）。マージのたびに CI（build・テスト・dry-run）が動く。`migration/wasm` への push は、どこにも公開しない（`deploy-wasm-dev.yml` は削除済み）。
2. 統合 PR（`migration/wasm` → `develop`）の本文の確認事項を見て、マージする。マージコミットで統合する（`git revert -m 1` で戻せる）。
3. `develop` への push で `Deploy` が動き、avatar-dev が WebAssembly 版になる。Actions の `Deploy` が success になり、`Verify the output matches the target` と `Deploy to Cloudflare Workers` のログで `--env dev` になっていることを確かめる。
4. avatar-dev で動作を確認する（確認項目は統合 PR の本文）。
5. **avatar-dev の変数の付け替え**: 旧 SSR 版の Worker は `MAINTENANCE`、新しい Worker（と kit）は `MAINTENANCE_MODE` を読む。付け替えるまで、avatar-dev の `MAINTENANCE` は無視される（メンテナンス・お知らせは出ない）。統合のあと、kit の Actions（Set maintenance (all sites)）か、次のどちらかで `MAINTENANCE_MODE` を作る:
   - avatar のリポジトリの Actions → `Deploy` → Run workflow、Use workflow from: `develop`、`maintenance_mode`: `false`（通常のまま変数だけを作る）
   - 手元: `node external/fara-site-kit/tools/maintenance.mjs set --env dev --site avatar --mode false`（ドライラン）→ `--apply`
   実行ページ下部のジョブサマリーで、`MAINTENANCE_MODE` の現在値が出ることを確かめる。そのあと、ダッシュボードの旧 `MAINTENANCE` は削除してよい（読まれない）。
6. Cloudflare・GitHub の後始末（`wasm-dev` は使わなくなった）: Worker `fara-avatar-wasm-dev`・カスタムドメイン `avatar-wasm-dev.fara-labs.com`・その Cloudflare Access のアプリケーションを削除する。GitHub の Environment `wasm-dev` も削除してよい。kit の `tools/maintenance-sites.json` に `wasm-dev` を足していなければ、変更は要らない。

### 2.2 本番の初公開（`develop` → `master`）

**この手順は、統合のあと、ユーザーが判断して行う。Claude は PR の作成までで、マージしない。**

- 本番 Worker `fara-avatar`（avatar.fara-labs.com）は、おそらくまだ Cloudflare にない（2026-10-23 時点で avatar.fara-labs.com の名前が引けない。GitHub Actions に master の実行が 1 件もない）。`master` の中身は、Cloudflare に出す前の最初のコミット（Initial Page）のまま。Worker が既にあれば、ゲートは `MAINTENANCE_MODE` を見て、`notice`（または `true`）でなければ止まる（そのときは、先に Run workflow で `maintenance_mode: notice` を入れる）。最初の push で、Worker・カスタムドメイン・Durable Object（Room）が作られる。
- **既存の `develop` → `master` の PR #4（「.NET 9 への移行、検索・画像・内容修正、Cloudflare デプロイ」）は、古い内容の題名のまま、統合のあと WebAssembly 版への切り替えそのものになる。** そのまま「いつもの更新」のつもりでマージしないこと。統合の前に閉じ、本番の初公開の準備ができたときに、題名・本文に「本番の初公開（WebAssembly 版）」と書いた新しい PR を作ることを勧める。
- マージの前に確かめること: avatar-dev で WebAssembly 版を一通り確認した / Cloudflare に本番の報告用の変数（Turnstile・Discord Webhook）を設定する準備がある / PR の差分に `deploy.yml`・`wrangler.toml`・Worker の変更が含まれていることを理解している。
- マージのあと（README の「本番の初回の公開」と同じ）:
  1. 初回は、master を選んで Run workflow（Deploy (production)）で `first_publish` を `true` にして実行する（Worker が無い 404 は、既定では止まる。初回の手動実行だけ通す。README の「本番の初回の公開」）。`--var MAINTENANCE_MODE:true` 付きで公開され、**全ページがメンテナンス画面（503）のまま**になる。
  2. ダッシュボードで `fara-avatar` に報告の変数（`TURNSTILE_SITE_KEY`・`TURNSTILE_SECRET`・`DISCORD_WEBHOOK_URL`）を設定する。
  3. Actions → `Deploy` → Run workflow（Use workflow from: `master`）、`maintenance_mode`: `false`（先にお知らせを見せるなら `notice`）→ 通常公開。このとき、その時点の master の内容がすべて公開される。
- 2 回目以降の master への公開は、先に `MAINTENANCE_MODE` を `notice` にしておく（ゲート）。`notice` にするには、master を選んで Run workflow で `maintenance_mode`: `notice`（公開も一緒に行われる）、または kit の CLI。

## 3. 戻し方

### 3.1 develop（avatar-dev）を旧 SSR 版に戻す

統合 PR のマージコミットを `develop` で revert する（履歴は書き換えない）。

```bash
git switch develop && git pull
git revert -m 1 <統合 PR のマージコミット>
git push           # develop の保護があるときは、revert の PR を develop 向けに作る
```

- `develop` への push で、戻った `deploy.yml`（旧 SSR 版: `dotnet run -- --export dist`）が動き、avatar-dev が旧 SSR 版に戻る。revert は、統合で入った変更（WebAssembly 版の実装・Phase 5 の削除・CI・wrangler.toml・Worker・テスト・ドキュメント）をまとめて取り消す。統合のあとに `develop` に入った変更は残る。
- 旧 SSR 版の Worker は変数 `MAINTENANCE`（`_MODE` なし）を読む。メンテナンス表示が要るときは、ダッシュボードで `MAINTENANCE` を作り直す（付け替えで消していた場合）。
- 同じ内容をもう一度統合したいときは、その revert コミットを revert する（`git revert <revert のコミット>`）。
- 共同練習の部屋（Durable Object `Room`）は、旧版と新版で同じクラス・同じマイグレーション（`v1`）で、24 時間で内容が消える。戻しても、部屋の保存形式の食い違いは起きない。localStorage のキーも移行前から変えていないので、読者の進捗・テーマは両方で共通。

### 3.2 本番に WebAssembly 版が出たあとに、旧 SSR 版に戻す

1. 先に、公開を止める（表示だけ止めたいとき）: Cloudflare のダッシュボードで `fara-avatar` の変数 `MAINTENANCE_MODE` を `true` にする（即時。`keep_vars` で次の公開でも残る）。または Actions → `Deploy` → Run workflow（master）で `maintenance_mode`: `true`（公開も一緒に行われる）。
2. Cloudflare の Workers → `fara-avatar` → Deployments から、1 つ前のバージョンにロールバックする（コードと静的ファイルが戻る。本番の初公開のように前のバージョンがないときは、1 のメンテナンス画面のままにする）。
3. コードを戻すとき: 3.1 の revert を `develop` に入れ、`develop` → `master` の PR（ユーザーがマージ）で本番に届ける。旧 SSR 版の `deploy.yml` は `MAINTENANCE`（`_MODE` なし）を読む・渡すので、その公開の前に、本番の Worker に `MAINTENANCE=notice` を作っておく（ゲート）。

### 3.3 切り替えの途中（統合の前）に止める

統合 PR をマージしない限り、`develop`・`master`・avatar-dev・本番には何も起きない。Phase 5 の PR は `migration/wasm` 向けなので、閉じる・revert するだけでよい（`migration/wasm` の `git revert -m 1`）。

## 4. この Phase 5 で消したもの・残したもの

消した（理由は [wasm-migration-plan.md](wasm-migration-plan.md) §14）: `.github/workflows/deploy-wasm-dev.yml`（`migration/wasm` の確認用。統合のあとは avatar-dev が WebAssembly 版なので不要）/ `wrangler.toml` の `env.wasm-dev` / ルートの `FaraAvatarModdingGuide.csproj`・`Components/`・`Publishing/`・`Program.cs`・`Properties/`・`appsettings.Development.json`・`wwwroot/css`・`wwwroot/js` / 旧テストプロジェクト `tests/FaraAvatarModdingGuide.Tests` / `tools/compare`（SSR との本文比較）・`tools/verify/shots.mjs`（SSR との見た目の比較）/ `deploy.yml` の SSR の書き出し・develop の SSR との比較・`cat >> dist/_headers`（noindex の追記）

残した: `wwwroot/images/`（Build が公開物にコピーする画像）/ `appsettings.json`（`Site` のみ。Build とテストが読む）/ `tests/js/fixtures/searchcore.reference.js`（検索の正解の固定の写し）/ `tools/verify/practice-baseline.json`（練習の操作の結果の基準）/ `deploy.yml` の `workflow_dispatch` の入力（kit の一括変更が使う）
