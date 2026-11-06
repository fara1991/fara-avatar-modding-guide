# 記事の図（ダイアグラム）

記事に載せる説明用の図の元（HTML）と、PNG にする手順です。スクリーンショットではなく、記事の内容を図にしたものです（他人のアセットや、個人のプロジェクトの画面は入れない）。

- `common.css`: 図の共通の見た目（白地のカード。ダーク・ライトのどちらの記事でも読める）
- `*.html`: 図の元。幅 480px の 1 列（スマホで縮んでも文字が読める大きさ）
- `render.mjs`: 全部の図を PNG にして、各記事の `images/` に書き出す

```bash
node tools/diagrams/render.mjs   # Edge と playwright-core が要る（npm ci 済み）
```

図を足すときは、HTML を足して `render.mjs` の `JOBS` に 1 行足し、記事には `[![代替テキスト](images/x.png)](/learn/<カテゴリ>/images/x.png)` と、次の行に `*キャプション*` を書く（Content/README.md の「画像・GIF を載せる」）。図の内容は、その記事の本文にあることだけにする。
