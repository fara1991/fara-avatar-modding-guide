---
title: テクスチャメモリーを減らす方法
reading: てくすちゃめもりーをへらすほうほう
description: VRChat のアバターのテクスチャメモリーを減らす方法を、TexTransTool・lilAvatarUtils・Avatar Optimizer（AAO）の機能ごとに整理します。公式の記載がある点と、確かめていない点を分けて書いています。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 公式の勧めは、Unity のインポート設定で Max Size を下げることと、高解像度のテクスチャを何枚も使わないこと
- クランチ圧縮を使っても VRAM のサイズは変わらないと、lilAvatarUtils の説明に書かれている
- MA でアバター全体のテクスチャを減らす機能は、公式に記載なし
- AAO を使うと Control Panel のランクはあてにならないため、Play モードのランクで確かめる
:::

テクスチャメモリーは、アバターで使うテクスチャが使うメモリーの推定値です。SDK の「パフォーマンスの内訳」に出る項目で、上限は PC の Poor が 150 MB、Android の Poor が 40 MB です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。公式では、どれか 1 項目でも上限を超えると次のランクになると書かれています（[パフォーマンスランク](/learn/vrchat/performance-rank)）。

この記事では、テクスチャメモリーを減らす方法を、ツールの機能ごとに整理します。

::: tip
この記事では、次の言葉を使います。

- **公式に記載なし**{.lbl .lbl-none}: 各ツールの公式の資料（日本語版のページや更新履歴）を検索して、書かれていないことを確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。
:::

## 減らす前に確かめること

パフォーマンスの内訳で、テクスチャメモリーの値を確かめます。ただし、テクスチャメモリーは上限の数字が画面に出ないため、色で段階を読みます（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。テクスチャは、システム RAM と GPU のメモリーの両方を使うと補足されています。GPU 側のメモリーを VRAM と呼びます。

- **SDK の「パフォーマンスの内訳」**: テクスチャメモリーの値と色を見ます。
- **lilAvatarUtils のテクスチャのタブ**: テクスチャごとの VRAM サイズを見られます。ツールの説明では、テクスチャがロードされたときに使われる VRAM のサイズです。
- **AAO の結果**: AAO を使うと、Control Panel のパフォーマンスランクはあてにならなくなると、AAO の公式が書いています。Play モードに入ったときのランクで確かめてください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

- 公式の勧めは、Unity のインポート設定で Max Size を下げることと、高解像度のテクスチャを何枚も使わないことです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表に、公式の勧めとして載せています）。
- lilAvatarUtils の説明では、解像度が小さいほど VRAM のサイズを抑えられるので、見た目の劣化が目立たない範囲で小さくすることが勧められています。
- 同じ説明では、クランチ圧縮を使っても VRAM のサイズは変わらないと書かれています。
- 解像度を下げても、元の画像ファイルは変わりません。lilAvatarUtils の説明では、元の画像ファイルの解像度は変更されないため、小さな値を設定しても元に戻せると書かれています（[lilAvatarUtils のテクスチャのタブ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

アバター全体のテクスチャを減らす機能は、**公式に記載なし**{.lbl .lbl-none}です（確かめた範囲: MA の日本語版と英語版のリファレンス一覧と、各ページ）。ただし MA の [CHANGELOG](https://modular-avatar.nadena.dev/ja/docs/changelog) には、「テクスチャのサイズが4の倍数でない場合に、エクスプレッションメニューアイコンの自動圧縮が失敗する問題を修正」という記載があります。この記載から、表情メニューのアイコンに自動圧縮があることは読み取れますが、テクスチャメモリーへの効果は**未確認**{.lbl .lbl-unk}です。衣装の組み込みは [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。

### TexTransTool（TTT） {.tool .tool-ttt}

**何をするか**: AtlasTexture は、公式によると「テクスチャの必要な部分のみを集めたテクスチャを作ることで VRAM の削減を実現する」コンポーネントです。必要な部分だけを集めた 1 枚のテクスチャを作ります。

**どう使うか**:

- アバターのルートを右クリックして、「TexTransTool/TTT AtlasTexture」を選びます。アバターの中に `AtlasTexture` という GameObject ができ、マテリアルが一覧で出ます。
- 対象のマテリアルにチェックを入れ、Manual Bake Avatar か、Unity エディターの再生で確かめます（[公式チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。
- TextureFineTuning（テクスチャー詳細設定）で、解像度や圧縮を設定します。公式は、AtlasTexture が作ったテクスチャをそのまま使うと、かえって VRAM が増える可能性があると書いています。
- TextureFineTuning の「Remove」は、指定したテクスチャを消去し、マテリアルに割り当てられないようにする設定です。公式は「テクスチャを消せばVRAM削減になります」と書いています（[公式: TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。この記事の補足ですが、消したテクスチャを使っている部分は表示が変わるため、対象を確かめてから設定してください。

**TextureConfigurator（実験的）**: 解像度と MipMap を、非破壊的に制御するコンポーネントです。メニューは「TexTransTool/Other/TTT TextureConfigurator」です。一括で作る場合は「TexTransTool/Generate/TTT TextureConfigurator」を使います（無効化された TextureConfigurator を一括生成すると、公式チュートリアルに書かれています）。

公式のチュートリアル（[テクスチャ設定でテクスチャーメモリを削減する](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByTextureSetting)）には、解像度を 2048 から 1024 にし、圧縮を Normal から High にする例があります。公式は、解像度を 1 段階下げると容量が 1/4 になると書いています。PC 向けの場合は、圧縮設定を Normal から High に上げると容量が 2 倍になると書いています。PC 向けの場合のこの例では、公式は「VRAM の使用量は元の半分になります」と書いています（1/4 × 2 = 1/2）。これは公式の例であり、どのテクスチャでも同じ結果になるとは限りません。

**注意**:

- TextureConfigurator は、公式が実験的なコンポーネントと警告しています。予告なく機能が変わったり、削除されたりすることがあります。

### lilAvatarUtils {.tool .tool-lil}

**何をするか**: テクスチャのタブで、テクスチャごとの VRAM サイズと最大解像度を見られます。最大解像度は、縦または横の最大サイズで、Unity のインポート時に縮小される設定として扱われます。

**どう使うか**:

- VRAM サイズの大きいテクスチャを見つけ、Unity のインポート設定で最大解像度を下げる作業の補助として使います。公式の説明では、最大解像度は画像を読み込むとき（インポート時）に縮小される値です。ツールの画面でこの値を直接編集できるかは、この記事では**未確認**{.lbl .lbl-unk}です。
- 「置き換え」は、アバターにあるすべてのマテリアルの該当テクスチャを一括で置き換えます。

**注意**:

- 「マテリアルのクリーンアップ」は、割り当て中のシェーダーで使用されない不要なプロパティを削除する機能です（[公式: ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。公式は、不要なテクスチャの割り当てが残っていると、アバターに含まれて容量が大きくなることがあると説明しています。これは容量（ファイルの大きさ）の話で、VRAM とは別の話として読んでください。
- マテリアルのクリーンアップは、アセットを変更する操作です。作業の前に、プロジェクトのバックアップを取るのが安全です。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**未確認**{.lbl .lbl-unk}。MMS は三角ポリゴンを減らすツールとして説明されていて、テクスチャを減らす機能については、この記事では確認していません（[三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles) の MMS の項を見てください）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO には、テクスチャを減らす機能が 3 つあります。

- **Optimize Texture（自動）**: Trace And Optimize の「テクスチャを最適化する」です。公式は、見た目に影響を与えずに UV パッキング（UV の配置を詰めること）とテクスチャサイズの縮小を行うと説明しています。対象は、AAO が対応しているシェーダーのマテリアルだけです。
- **AAO Max Texture Size（手動）**: 任意の GameObject に付けると、その子孫すべてに効きます。特定の Mipmap レベルを減らして縮小し、元のテクスチャフォーマットと設定は残ります。
- **AAO Merge Material（手動）**: 同じシェーダーを使うマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめます。生成されるテクスチャのサイズとフォーマットを指定できます。

**注意**:

- 公式は、テクスチャの枚数や MB が何割減るかの数値を書いていません。
- Trace And Optimize の「使われていないObjectを自動的に削除する」は、使われていないテクスチャも削除します（1.9.0 の更新履歴）。

## 手順

1. SDK の「パフォーマンスの内訳」で、テクスチャメモリーの値と色を確かめます。
2. lilAvatarUtils のテクスチャのタブで、VRAM サイズの大きいテクスチャを確かめます。
3. 大きすぎるテクスチャは、Unity のインポート設定で最大解像度を下げます（公式の勧め）。
4. アバターのルートに AAO Trace And Optimize を付け、「テクスチャを最適化する」がオンになっているか確かめます。
5. 同じシェーダーのマテリアルが多いときは、AAO Merge Material を検討します（マテリアルスロットへの影響は、[マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots) を見てください）。
6. TTT の AtlasTexture を使うときは、「TexTransTool/TTT AtlasTexture」で作り、Manual Bake Avatar か再生で確かめます。
7. 最後に、NDM Framework の「Manual bake avatar」で、処理後の複製を作って見た目を比べ、パフォーマンスの内訳をもう一度見ます。

## 注意・うまくいかないとき

::: warning
- TTT のテクスチャの圧縮は、NDMF のプレビューには反映されません。公式は、NDMF の Apply On Play か Manual Bake で確かめるよう書いています（[公式: TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
- TTT のコンポーネントは、ビルドのときに有効（Active）でないと動きません。公式 FAQ の原文は「TexTransTool のコンポーネントは基本的に、ビルド時に Active でないと動作しません。」です。アバター改変では NDMF が必要です（TTT の公式の説明）。
- TextureSize を元の設定より大きくすると、無意味に拡大されると公式が書いています。
- MipMap の設定は、よく分からないときは触らないよう、公式が警告しています。
- Format Quality の「None」について、公式は「シェーダーについて完全に理解している人ではない、技術者ではないのであるならば、絶対に「None」を選ばないでください」と書いています。シェーダーに詳しくない人は、None を選ばないよう公式が勧めています。
- TTT の FAQ には、一部の GPU と DirectX 11 で動かない既知の不具合が書かれています。ただし、この記述は古い版のものです。v1.0.2 で解消したかは**未確認**{.lbl .lbl-unk}です。古い TTT で作られたアセットが動かないときは、バックアップを取ったうえで Migrator を実行するよう、公式 FAQ が案内しています。
- AAO の Max Texture Size は、テクスチャに Mipmap が有効になっている必要があります（[公式: Max Texture Size](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/max-texture-size/)）。Mipmap のないテクスチャは、スキップされて警告が出ます。公式は「Crunch圧縮を使用しているテクスチャはリサイズすることができません。」と書いています。
- AAO の Merge Material は、同じシェーダーのマテリアルが対象です。公式は「マテリアルを置き換えるアニメーションには対応していません。」と書いています。また「ParallaxやUVスクロールのような、テクスチャの参照される位置が使用時に変化するようなシェーダ機能はサポートされていません」とも書いています。
- AAO の処理の結果は、Control Panel の数字ではなく、Play モードに入ったときのランクか、NDM Framework の「Manual bake avatar」で作った複製で確かめてください。
:::

## 関連ページ

- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [アバターを軽くする](/learn/vrchat/optimization)
