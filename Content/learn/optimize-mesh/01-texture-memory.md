---
title: テクスチャメモリーを減らす方法
reading: てくすちゃめもりーをへらすほうほう
description: VRChat のアバターのテクスチャメモリーを減らす方法を、TexTransTool・lilAvatarUtils・Avatar Optimizer（AAO）の機能ごとに整理します。
difficulty: 中級
updated: 2026-11-07
---

::: point
- Unity のインポート設定で Max Size を下げることと、高解像度のテクスチャを何枚も使わないことがおすすめです
- DXT・ETC 形式のクランチ圧縮では、VRAM のサイズは変わりません（[lilAvatarUtils のテクスチャのタブ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）
- AAO を使うと Control Panel のランクはあてにならないため、Play モードで確かめます
:::

テクスチャメモリーは、アバターで使うテクスチャが使うメモリーの量を表す項目です。SDK の「パフォーマンスの内訳」に出る項目で、上限は PC の Poor が 150 MB、Android の Poor が 40 MB です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。どれか 1 項目でも上限を超えると、次のランクになります（[パフォーマンスランク](/learn/vrchat/performance-rank)）。

この記事では、テクスチャメモリーを減らす方法を、ツールの機能ごとに整理します。

## 減らす前に確かめること

パフォーマンスの内訳で、テクスチャメモリーの値を確かめます。ただし、テクスチャメモリーは上限の数字が画面に出ないため、色で段階を読みます（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。テクスチャは、システム RAM と GPU のメモリーの両方を使います。GPU 側のメモリーを VRAM と呼びます。

- **SDK の「パフォーマンスの内訳」**: テクスチャメモリーの値と色を見ます。
- **lilAvatarUtils のテクスチャのタブ**: テクスチャごとの VRAM サイズを見られます。ツールの説明では、テクスチャがロードされたときに使われる VRAM のサイズです。
- **AAO の結果**: AAO を使うと、Control Panel のパフォーマンスランクはあてにならなくなります。Play モードに入ったときのランクで確かめてください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

- Unity のインポート設定で Max Size を下げることと、高解像度のテクスチャを何枚も使わないことがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) にも同じ内容があります）。
- 解像度が小さいほど VRAM のサイズを抑えられます。見た目の劣化が目立たない範囲で小さくするのがおすすめです（[lilAvatarUtils のテクスチャのタブ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。
- DXT・ETC 形式のクランチ圧縮では、VRAM のサイズは変わりません（[lilAvatarUtils のテクスチャのタブ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。
- 解像度を下げても、元の画像ファイルの解像度は変わらないため、小さな値を設定しても元に戻せます（[lilAvatarUtils のテクスチャのタブ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。

## ツール別の方法

### TexTransTool（TTT） {.tool .tool-ttt}

**何をするか**: AtlasTexture は、テクスチャの必要な部分だけを集めた 1 枚のテクスチャを作って、VRAM を減らすコンポーネントです。

**どう使うか**:

- アバターのルートを右クリックして、「TexTransTool/TTT AtlasTexture」を選びます。アバターの中に `AtlasTexture` という GameObject ができ、マテリアルが一覧で出ます。
- 対象のマテリアルにチェックを入れ、Manual Bake Avatar か、Unity エディターの再生で確かめます（[チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。
- TextureFineTuning（テクスチャー詳細設定）で、解像度や圧縮を設定します。AtlasTexture が作ったテクスチャをそのまま使うと、かえって VRAM が増える可能性があります。
- TextureFineTuning の「Remove」は、指定したテクスチャを消去し、マテリアルに割り当てられないようにする設定です。テクスチャを消すと VRAM を減らせます（[TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。ただし、消したテクスチャを使っている部分は表示が変わるため、対象を確かめてから設定してください。

**TextureConfigurator（実験的）**: 解像度と MipMap を、非破壊的に制御するコンポーネントです。メニューは「TexTransTool/Other/TTT TextureConfigurator」です。一括で作る場合は「TexTransTool/Generate/TTT TextureConfigurator」を使います。このメニューでは、無効化された TextureConfigurator を一括で生成できます（[チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByTextureSetting)）。

チュートリアル（[テクスチャ設定でテクスチャーメモリを削減する](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByTextureSetting)）の例では、解像度を 2048 から 1024 にし、圧縮を Normal から High にします。解像度を 1 段階下げると、容量は 1/4 になります。PC 向けの場合は、圧縮設定を Normal から High に上げると、容量は 2 倍になります。この例では、VRAM の使用量は元の半分になります（1/4 × 2 = 1/2）。ただし、これは例であり、どのテクスチャでも同じ結果になるとは限りません。

**注意**:

- TextureConfigurator は実験的なコンポーネントです。予告なく機能が変わったり、削除されたりすることがあります。

### lilAvatarUtils {.tool .tool-lil}

**何をするか**: テクスチャのタブで、テクスチャごとの VRAM サイズと最大解像度を見られます。最大解像度は、縦または横の最大サイズで、Unity のインポート時に縮小される設定として扱われます。

**どう使うか**:

- VRAM サイズの大きいテクスチャを見つけ、Unity のインポート設定で最大解像度を下げる作業の補助として使います。最大解像度は、画像を読み込むとき（インポート時）に縮小される値です。
- 「置き換え」は、アバターにあるすべてのマテリアルの該当テクスチャを一括で置き換えます。

**注意**:

- 「マテリアルのクリーンアップ」は、割り当て中のシェーダーで使用されない不要なプロパティを削除する機能です（[ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。不要なテクスチャの割り当てが残っていると、アバターに含まれて容量が大きくなることがあります。これは容量（ファイルの大きさ）の話で、VRAM とは別の話として読んでください。
- マテリアルのクリーンアップは、アセットを変更する操作です。作業の前に、プロジェクトのバックアップを取るのが安全です。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO には、テクスチャを減らす機能が 3 つあります。

- **Optimize Texture（自動）**: Trace And Optimize の「テクスチャを最適化する」です。見た目に影響を与えずに UV パッキング（UV の配置を詰めること）とテクスチャサイズの縮小を行います。対象は、AAO が対応しているシェーダーのマテリアルだけです。
- **AAO Max Texture Size（手動）**: 任意の GameObject に付けると、その子孫すべてに効きます。特定の Mipmap レベルを減らして縮小し、元のテクスチャフォーマットと設定は残ります。
- **AAO Merge Material（手動）**: 同じシェーダーを使うマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめます。生成されるテクスチャのサイズとフォーマットを指定できます。

**注意**:

- AAO 1.9.0 では、Trace And Optimize の「使われていないObjectを自動的に削除する」が、使われていないテクスチャも削除します。

## 手順

1. SDK の「パフォーマンスの内訳」で、テクスチャメモリーの値と色を確かめます。
2. lilAvatarUtils のテクスチャのタブで、VRAM サイズの大きいテクスチャを確かめます。
3. 大きすぎるテクスチャは、Unity のインポート設定で最大解像度を下げます。
4. アバターのルートに AAO Trace And Optimize を付け、「テクスチャを最適化する」がオンになっているか確かめます。
5. 同じシェーダーのマテリアルが多いときは、AAO Merge Material を検討します（マテリアルスロットへの影響は、[マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots) を見てください）。
6. TTT の AtlasTexture を使うときは、「TexTransTool/TTT AtlasTexture」で作り、Manual Bake Avatar か再生で確かめます。
7. 最後に、NDM Framework の「Manual bake avatar」で、処理後の複製を作って見た目を比べ、パフォーマンスの内訳をもう一度見ます。

## 注意・うまくいかないとき

::: warning
- TTT のテクスチャの圧縮は、NDMF のプレビューには反映されません。NDMF の Apply On Play か Manual Bake で確かめてください（[TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
- TTT のコンポーネントは、基本的に、ビルドのときに有効（Active）でないと動きません（[TTT の FAQ](https://github.com/ReinaS-64892/TTT-Docs/blob/main/docs/FAQ/index.md)）。アバター改変には NDMF が必要です。
- TextureSize を元の設定より大きくすると、無意味に拡大されます。
- MipMap の設定は、よく分からないときは触らないでください。
- Format Quality の「None」は、シェーダーについて完全に理解している人でなければ選ばないでください。
- TTT v0.9.x〜v0.10.x では、DirectX 11 と AMD Radeon の組み合わせで、TTT PSD Importer などが正常に動かないことがあります（[TTT の FAQ](https://github.com/ReinaS-64892/TTT-Docs/blob/main/docs/FAQ/index.md)）。古い TTT で作られたアセットが動かないときは、バックアップを取ったうえで Migrator を実行するのがおすすめです（[TTT の FAQ](https://github.com/ReinaS-64892/TTT-Docs/blob/main/docs/FAQ/index.md)）。
- AAO の Max Texture Size は、テクスチャに Mipmap が有効になっている必要があります（[Max Texture Size](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/max-texture-size/)）。Mipmap のないテクスチャは、スキップされて警告が出ます。Crunch 圧縮を使用しているテクスチャは、リサイズできません。
- AAO の Merge Material は、同じシェーダーのマテリアルが対象です。マテリアルを置き換えるアニメーションには対応していません。Parallax や UV スクロールのような、使用時にテクスチャの参照される位置が変化するシェーダー機能も、サポートされていません。
- AAO の処理の結果は、Control Panel の数字ではなく、Play モードに入ったときのランクか、NDM Framework の「Manual bake avatar」で作った複製で確かめてください。
:::

## 関連ページ

- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [アバターを軽くする](/learn/vrchat/optimization)
