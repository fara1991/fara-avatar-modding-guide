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
