# Content

サイトの記事・データはすべてこのディレクトリにあります。C# や Razor を触らずに追加・編集できます。

```
Content/
  learn/
    sections.yml          # 目次の大きな区切り（Blender / Unity / VRChat / 通しで作る）
    categories.yml        # 目次の段階（並び順 = 読む順。section でどの区切りに入るかを決める）
    <category-id>/
      01-slug.md          # 記事。ファイル名が「番号-スラッグ.md」で、URL は /learn/<category-id>/<slug>
  tips/
    glossary.yml          # 用語集（/glossary）
    howto.yml             # あの操作、何て名前？（/howto）
    tips.yml              # TIPS（/tips）
    qa.yml                # 改変 Q&A（/qa）
  shortcuts.yml           # ショートカットの一次データ（記事からは frontmatter の shortcuts で参照）
  addons.yml              # アドオン一覧
  links.yml               # リンク集（/links）
```

## 記事の書き方

```markdown
---
title: 移動・回転・拡縮
description: 1行の説明（カード・meta description に使われる）
difficulty: 初級          # 初級 / 中級 / 上級
blender_version: "4.2"    # 省略時は appsettings.json の Site:BlenderVersion
updated: 2026-10-09
reading: うごかいてんすけーる   # 題名の読み（ひらがな）。サイト内検索で、ローマ字（ugoku）・かなで探すため。題名に漢字があるときは必須（テストで確かめる）
shortcuts: [G, R, S]      # shortcuts.yml の key と一致させる
---

本文は Markdown。見出しは `##` から始める（`#` はページタイトルに使われる）。

::: tip
補足やコツはこのブロックに書く。
:::

::: warning
注意事項はこちら。
:::

::: practice transform
:::
```

`::: practice <名前>` は、操作のチュートリアル（下の「操作のチュートリアルを埋め込む」）を埋め込みます。

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

## 操作のチュートリアルを埋め込む

記事の中に、Blender と同じキー操作で動かせる 3D ビューを置き、1 キーずつ案内できます（ゲームのチュートリアルのように、できたら自動で次の手順に進みます）。今あるものは次のとおりです。

| 名前 | 内容 | 使っている記事 |
|---|---|---|
| `navigation-mouse` | ホイールボタンで回す・Shift で平行移動・ホイールでズーム・Home | getting-started/navigation |
| `navigation-numpad` | テンキーで前 / 右 / 上 / 後ろ、Numpad 5、Numpad . | getting-started/navigation |
| `select` | クリック・Shift+クリック・A / Alt+A・ボックス選択・Shift+A・X・Shift+D・Ctrl+Z | basics/select |
| `transform` | G / R / S、軸の固定、数値入力、キャンセル、N、Ctrl+Z | basics/transform |
| `edit-mode` | Tab、選択モード（1 / 2 / 3）、Alt+クリックのループ選択、ループの拡大、A / Alt+A、Alt+Z とボックス選択 | modeling/edit-mode |
| `extrude-inset-loopcut` | 面を選んで E → I → E（へこませる）→ Ctrl+R（ホイールで 2 本） | modeling/extrude-inset-loopcut |
| `bevel-merge` | Ctrl+B、E → Esc で重複を作って M > 距離で、面を削除して Alt+クリック → F | modeling/bevel-knife-merge |
| `mirror` | 中央にループカット → 左半分の面を削除 → ミラー | modeling/mirror-smooth |
| `topology` | 統計を表示 → ループカット → 辺を溶解 | modeling/topology |
| `unity-basics` | Unity: Hierarchy の開閉と選択、F、Alt+ドラッグ・ホイール・中ボタンの視点操作、W の矢印での移動、Inspector の数値、E / R / Q | unity/editor-overview |
| `ma-outfit` | Unity: 衣装をアバターに入れる → Setup Outfit → 元の服を無効・EditorOnly → マテリアル → MA Blendshape Sync → VRChat SDK → Build & Test | unity/modular-avatar-outfit |

### 記事に置く

置きたい場所（その操作を説明する見出しのすぐ下など）に書きます。

```markdown
::: practice transform
:::
```

- 中に文章を書くと、チュートリアルの上に説明として表示されます
- 1 ページに複数置けます。3D ビュー（WebGL）が動くのは常に 1 つだけで、別のものを始めると前のものは一時停止します
- 3D ビューは画面に入ったときに読み込まれるので、置いてもページは重くなりません
- ページ内のチュートリアルを最後まで進めると、そのページが「完了」になります
- スクリーンショットはそのまま残してください（Blender の実際の画面はチュートリアルでは再現しきれません）

### 新しいチュートリアルを作る

手順は `wwwroot/js/scenarios.js` の `SCENARIOS` に、名前をキーにして書きます（ここだけは JavaScript です）。

```js
"my-scenario": {
  title: "立方体を上に動かす",
  scene: [{ kind: "cube" }],          // 最初に置くもの。kind は plane / cube / sphere / cylinder / cone / torus
  steps: [
    clickStep("Cube", "立方体をクリックして選択してください。"),
    ...opSteps({ key: "G", axis: "z", value: "2", group: "move" }), // G → Z → 2 → Enter を 4 手順に分けて案内
    {
      text: "<kbd>N</kbd> でサイドバーを開いてください。", // 案内文（HTML 可）
      keys: ["N"],                                      // 画面下に出すキー。押すと光る（"Shift+A" で同時押し）
      done: e => e.type === "npanel" && e.open,         // できたかどうか（e は 3D ビューのイベント）
      skip: vp => vp.toggleNPanel(true),                // 「次へ（スキップ）」で、できた状態にする処理
    },
  ],
},
```

- `done(e)` に届くイベントの種類（`select` / `opstart` / `axis` / `numeric` / `confirm` / `cancel` / `view` / `add` / `delete` / `undo` など）と中身は `wwwroot/js/viewport.js` の先頭に書いてあります
- `hint(e)` で文字列を返すとヒントを表示し、`fail(e)` で文字列を返すと `group` の最初の手順からやり直しになります
- `skip` は必ず書いてください。「やり直す」や一時停止からの再開は、最初の状態から前の手順の `skip` を順に実行して再現しています
- `highlight: { object: "Cube", axis: "z", hud: "view" }` で、対象のオブジェクト・軸・左上の視点表示を目立たせます。編集モードでは `at: [x, y, z]`（ワールド座標の点に吹き出し）と `tool: "face"`（上の「頂点 / 辺 / 面・透過・統計・ミラー」ボタンを光らせる）が便利です
- 編集モード用の部品が `scenarios.js` にあります: `tabStep`（Tab）、`selectModeStep`（1 / 2 / 3）、`editOpSteps`（E / I / Ctrl+B → 数値 → Enter）、`editClickStep`（面や辺をクリック）、`loopCutSteps`（Ctrl+R → ホイール → クリック → 位置）
- 編集モードの選択を判定するときは、`select` イベントの `e.edit` と `e.counts`（選択した頂点・辺・面の数）を見ます。どの面を選んだかは `vp.editInfo().faces`（ワールド座標の中心と法線）で確かめられます
- `skip` でマウス位置に頼る操作（ループカットの辺など）は、`vp.findEdge(条件)` のように条件で対象を決めてください（再現のときはマウスの位置が決まっていないため）
- 編集モードの操作はおおむね Blender と同じ結果になりますが、ベベルは分割数 1 だけ、差し込みは複数の面でも面ごと（Blender で I を 2 回押したとき）、ナイフ（K）・隠す（H）・編集モードの複製（Shift+D）は未対応です。ミラーはモディファイアーの代わりに上の「ミラー」ボタン（X 方向）で表示します
- 動作確認は `dotnet run` で記事を開いて最後まで操作し、「次へ（スキップ）」だけでも最後まで進めることを確かめてください。`?debug=1` を付けると、ブラウザのコンソールから `__tutorials` で状態を見られます

### Unity のチュートリアル

`engine: "unity"` を付けたシナリオは、Blender の 3D ビューの代わりに Unity エディターに似た練習用の画面（`wwwroot/js/unity.js`）を使います。今あるのは `unity-basics`（unity/editor-overview）と `ma-outfit`（unity/modular-avatar-outfit）です。

```js
"my-unity": {
  title: "…",
  engine: "unity",
  scene: { instances: ["Avatar"] },   // 最初に置く Prefab（Avatar / Hoodie）
  steps: [
    {
      text: "Project の Hoodie を Avatar の上にドラッグしてください。",
      mouse: "ドラッグ",
      highlight: { project: "Hoodie", label: "Avatar へドラッグ" },  // hierarchy / project / inspector / menu / popup / sdk / tool / scene
      done: e => e.type === "instantiate" && e.parentName === "Avatar",
      skip: ed => ed.instantiate("Hoodie", ed.findByName("Avatar").id),
    },
  ],
},
```

- イベントの一覧（`select` `instantiate` `reparent` `setupOutfit` `active` `tag` `material` `componentAdded` `sdk` `build` など）と、スキップで使えるメソッドは `unity.js` の先頭と「操作」の節にあります
- 人形・衣装・VRChat SDK の画面は練習用のシミュレーションです。実在のアバターのデータは使っていません

### スマートフォン向けの手順画像

スマートフォン・タブレット（タッチだけの端末）では 3D ビューを出さず、手順ごとの画像を横にめくるカードで表示します。PC でも「画像で見る」で切り替えられます。

画像は `wwwroot/images/tutorials/<シナリオ名>/01.webp` 〜 で、サイトの 3D ビューをヘッドレス Chrome で動かして撮ります。**シナリオを追加・変更したら撮り直してください。**

```bash
# サイトを起動しておく（dotnet run、または書き出して npx wrangler dev）
node tools/tutorial-images/capture.mjs http://localhost:8787             # すべて
node tools/tutorial-images/capture.mjs http://localhost:8787 transform   # 指定したシナリオだけ
```

- 各画像は「その手順を終えたあと」の画面です（`skip` で再現するので、`skip` が正しく書かれている必要があります）
- Chrome の場所は環境変数 `CHROME` で指定できます（Node.js 22 以上、追加のパッケージは不要）
- 画像がないシナリオは「画像は準備中です」と表示されます

### 完了の記録

- 各記事の最後にある「このページを完了にする」か、ページ内のチュートリアルを最後まで進めると記録されます
- 記録はブラウザの localStorage（`avatar-modding-guide.progress`）にだけ保存されます。目次ページの「記録を消す」で消せます
- 確認用に、`/learn?debug=1&seed=basics/select,basics/transform` のように開くと、指定したページを完了にできます

## 用語集・何て名前？・TIPS・改変 Q&A

`Content/tips/` の YAML に 1 項目ずつ書きます。どれもサイト内検索の対象になり、ページではタグで絞り込めます。項目の形は各ファイルの先頭のコメントを見てください。

題名（記事の `title`、TIPS の `title`、How-to・Q&A の `question`、カテゴリの `title`）に漢字があるときは、`reading:` に**ひらがな**の読みを書きます（英語はカタカナ）。読みは、ローマ字（`yougo` → 用語集）・かなで探すときだけに使い、題名そのものの一致は今まで通りです。`dotnet test` が、読みの無い題名と、読みに漢字・英字が混じるものを見つけます。カテゴリには、英語の名前を `en:` に書くと、英語で探せます（`modeling` → モデリング）。

```yaml
# glossary.yml
- id: merge                 # /glossary#merge。英小文字・数字・ハイフン
  term: マージ
  reading: まーじ           # 読み（並び順に使う）
  en: Merge                 # 英語 UI での表記
  description: 選んだ頂点を 1 つにまとめる（編集モードで <kbd>M</kbd>）。   # Markdown
  tags: [Blender, モデリング]
  shortcuts: [M]            # shortcuts.yml の key と同じ表記ならリンクになる
  articles: [modeling/bevel-knife-merge]   # カテゴリ/スラッグ
  related: [join]           # 関連する用語の id
  source: discord           # Discord の会話から起こした項目なら（「Discord より」と表示）
```

- タグは `Blender` / `Unity` / `VRChat` / `Modular Avatar` と、作業の種類（`モデリング` `ウェイト` `マテリアル` `UV` `リギング` `書き出し`）を組み合わせます
- `source: discord` の項目は「Discord より」の印が付き、絞り込みのボタンも出ます。会話の内容は、誰が言ったかがわからない形に書き直してください
- どの種類にも、長い説明や手順を `body`（Markdown。「詳しく」を開くと表示）、用語集に無い関連語を `keywords`（文字のまま表示・検索の対象）として書けます。TIPS は症状・原因・直し方の形にならないコツを `summary` に書けます
- 本文から別の項目へは `[文字](/tips#id)` `[文字](/qa#id)` `[文字](/glossary#id)` `[文字](/links#id)` のようにリンクします
- 存在しない記事・用語・項目を参照すると、起動時に警告が出ます

## リンク集

`Content/links.yml` に 1 件ずつ書きます（形は先頭のコメント）。区切り（Unity のツール / VRChat / 参考になる記事・資料 / BOOTH のアバター / BOOTH の衣装・小物）は `Data/ContentService.cs` の `LinkCategories` です。`status: old` を付けると「古い情報」と表示されます。Blender のアドオンは `addons.yml`（/addons）に書きます。

