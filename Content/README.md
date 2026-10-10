# Content

サイトの記事・データはすべてこのディレクトリにあります。C# や Razor を触らずに追加・編集できます。

```
Content/
  learn/
    categories.yml        # ロードマップの段階（並び順 = 学習順）
    <category-id>/
      01-slug.md          # 記事。ファイル名が「番号-スラッグ.md」で、URL は /learn/<category-id>/<slug>
  shortcuts.yml           # ショートカットの一次データ（記事からは frontmatter の shortcuts で参照）
  addons.yml              # アドオン一覧
```

## 記事の書き方

```markdown
---
title: 移動・回転・拡縮
description: 1行の説明（カード・meta description に使われる）
difficulty: 初級          # 初級 / 中級 / 上級
blender_version: "4.2"    # 省略時は appsettings.json の Site:BlenderVersion
updated: 2026-10-09
shortcuts: [G, R, S]      # shortcuts.yml の key と一致させる
---

本文は Markdown。見出しは `##` から始める（`#` はページタイトルに使われる）。

::: tip
補足やコツはこのブロックに書く。
:::

::: warning
注意事項はこちら。
:::
```

- 開発中（`dotnet run`）はファイルを保存するだけでブラウザをリロードすれば反映されます。
- ファイル名の番号は表示順にだけ使われ、URL には含まれません。

## 画像・GIF を載せる

Blender の操作は文章だけでは伝わりにくいので、手順ごとにスクリーンショットや GIF を添えてください。

今ある `images/*.png` は `tools/screenshots/` のスクリプトで Blender を自動操作して撮ったものです。撮り直し方と新しいショットの足し方は [tools/screenshots/README.md](../tools/screenshots/README.md) を見てください。手で撮った画像を置いても構いません。

1. 画像をカテゴリの `images/` に置く: `Content/learn/<category-id>/images/<slug>-<内容>.png`
2. Markdown からは `images/` からの相対パスで参照する（`/learn/<category-id>/images/...` に自動で解決されます）

```markdown
1. <kbd>G</kbd> キーで移動モードに入る

   ![G キーを押すとオブジェクトがマウスに追従する](images/transform-grab.gif)
   *移動中は画面左下に移動量が表示される*
```

- 画像の直後の行に `*斜体*` で1行書くとキャプションになります
- 対応形式: png / jpg / gif / webp / svg / mp4 / webm（`Content/learn/` 以下の画像・動画だけが配信され、.md や .yml は配信されません）
- ファイル名は `スラッグ-内容.png` のように記事が分かる名前にしてください
- GIF は 10 秒以内・幅 800px 程度に。長いものは mp4/webm にして `<video src="images/x.mp4" autoplay loop muted></video>` で埋め込めます
