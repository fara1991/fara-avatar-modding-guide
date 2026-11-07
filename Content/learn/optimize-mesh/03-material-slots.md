---
title: マテリアルスロットを減らす方法
reading: まてりあるすろっとをへらすほうほう
description: VRChat のアバターのマテリアルスロットを減らす方法を、TexTransTool・Avatar Optimizer（AAO）の統合の機能を中心に整理します。TexTransTool の AtlasTexture では、マテリアルスロットは結合されないと公式が明記しているため、その注意も書いています。
difficulty: 中級
updated: 2026-11-07
---

::: point
- マテリアルスロットの上限は、PC の Poor が 32、Android の Poor が 4
- TTT の AtlasTexture では、マテリアルスロットは結合できないと公式が明言している
- AAO Merge Skinned Mesh の「マテリアルの統合」（既定でオン）で、同じマテリアルのスロットを 1 つにまとめる
- 統合先の Root Bone と Anchor Override は自動では設定されないため、手動で設定する
:::

マテリアルスロットは、メッシュがマテリアルを割り当てる枠の数です。SDK の「パフォーマンスの内訳」では「マテリアルスロットの数」と表示され、上限は PC の Poor が 32、Android の Poor が 4 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。上限を超えると、公式では次のランクになると書かれています（[パフォーマンスランク](/learn/vrchat/performance-rank)）。

この記事では、マテリアルスロットを減らす方法を、ツールの機能ごとに整理します。

::: tip
この記事では、次の言葉を使います。

- **公式に記載なし**{.lbl .lbl-none}: 各ツールの公式の資料（日本語版のページや更新履歴）を検索して、書かれていないことを確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: マテリアルスロットの数を確かめます。例の画面では、88（上限 32）と表示されていました。
- **枠の数え方**: パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠、Line Renderer は 1 枠を使うと、公式の補足にあります。
- **lilAvatarUtils のレンダラーのタブ**: メッシュごとのマテリアルスロット数を見られます。公式は、この数が大きいほど描画の負荷が大きくなると説明しています。

## 減らし方の考え方

- 公式の勧めは、マテリアルが 10 を超えるなら、テクスチャアトラス（複数のテクスチャを 1 枚にまとめたもの）を検討することです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表に、公式の勧めとして載せています）。
- 同じマテリアルを使う枠を 1 つにまとめる機能は、AAO の Merge Skinned Mesh と Trace And Optimize にあります。描画の呼び出し（ドローコール）を減らせると、AAO の公式は書いています。
- マテリアルを一つにまとめる作業と、枠（スロット）を一つにまとめる作業は、別のものです。下の TTT の注意を見てください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のリファレンス一覧と、各ページ）。更新履歴には「MA Material Setter」の項目がありますが、説明のページは確かめていないため、スロット数への効果は**未確認**{.lbl .lbl-unk}です。

### TexTransTool（TTT） {.tool .tool-ttt}

**何をするか**: AtlasTexture は、マテリアルを 1 つにまとめて、テクスチャを 1 枚にまとめる機能です。公式は、AtlasTexture のマテリアルの結合は「マテリアルをまとめるだけ」で、マテリアルスロットの結合はできないと明言しています。

> AtlasTexture のマテリアルの結合は **マテリアルをまとめるだけ** で、**マテリアルスロットの結合はできません。**
> （TexTransTool の[公式チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)の末尾の注意）

**どう使うか**:

- マテリアルを結合するには、「結合時参照」を指定します。公式は、結合時参照が None のままだとマテリアルは結合されず、何らかのマテリアルが割り当てられている場合に、そのマテリアルに結合されると書いています（[公式: AtlasTexture](https://ttt.rs64.net/docs/Reference/AtlasTexture)）。
- 部分的にマテリアルを選び、それぞれの結合時参照を指定できる「マテリアルの結合グループ」があります。

**注意**:

- 公式は、マテリアルスロットを減らすには、AAO の Trace And Optimize や Merge Skinned Mesh との併用を「強く推奨」すると書いています（同じチュートリアル）。
- TTT の公式のチュートリアル（[Quest対応のためのアトラス化とマテリアルの結合のチュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）には、Quest 向けにマテリアルを結合する例があります。公式は、これを「Quest対応のための、見た目を維持できない軽量化になります」と明記しています。
- マテリアルの結合の条件として「同じシェーダーであること」は、**公式に記載なし**{.lbl .lbl-none}です。

### lilAvatarUtils {.tool .tool-lil}

レンダラーのタブで、メッシュごとのマテリアルスロット数を見られます。マテリアルのタブの「置き換え」は、全マテリアルを一括で別のマテリアルに置き換える機能ですが、スロットの数が減るとは書かれていません。マテリアルのクリーンアップは、割り当て中のシェーダーで使用されない不要なプロパティを削除する機能で（[公式: ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）、スロットの数を減らす機能の説明はありません。**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: レンダラー・マテリアル・ツールの各タブの説明）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**未確認**{.lbl .lbl-unk}。MMS は三角ポリゴンを減らすツールとして説明されていて、マテリアルスロットについては、この記事では確認していません。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO には、スロットを減らす機能が 3 つあります。

- **Trace And Optimize（自動）**: 「スキンメッシュレンダラーを統合する」は、統合しても問題のないスキンドメッシュを 1 つにまとめます。自動では一部のメッシュを統合しないこともあるので、必要に応じて手動の Merge Skinned Mesh を使うよう、公式は書いています。同じマテリアルが複数の枠にあるときは、自動で 1 つにまとめられます（1.8.0 の更新履歴）。
- **AAO Merge Skinned Mesh（手動）**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。「マテリアルの統合」にチェックを入れると、同じマテリアルのスロットを 1 つにまとめます。公式は、これは DrawCall（描画の呼び出し）を減らすと書いています。
- **AAO Merge Material（手動）**: 同じシェーダーを使うマテリアルを、テクスチャを並べて 1 つのマテリアルにまとめます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の記事も見てください）。

**どう使うか**:

- Merge Skinned Mesh は、新しい空の GameObject を作り、メッシュを指定していない SkinnedMeshRenderer と、AAO Merge Skinned Mesh を付けます。統合したいレンダラーを一覧に入れ、「マテリアルの統合」をオンにします。
- 別々のメッシュで使われているマテリアルも一緒にまとめたいときは、AAO Merge Material を、AAO Merge Skinned Mesh と同じ GameObject に追加します（公式の注意）。

**注意**:

- AAO Merge Material で作ったマテリアルを使うマテリアルスロットは、AAO Merge Skinned Mesh では統合されません。
- 統合すると、個々のメッシュをアニメーションでオンとオフにできなくなります。統合先の Root Bone と Anchor Override は、自動では設定されないため、手動で設定します。
- 統合対象で同じマテリアルのスロットは、既定で統合されます。アニメーションで別のマテリアルに置き換えている場合は、Merge Skinned Mesh の「マテリアルの統合」の「統合する」をオフにするよう、公式 FAQ が案内しています。
- マテリアルプロパティのアニメーションの競合について、公式の記述は食い違っています。備考には「マテリアルに関するアニメーションは統合前のものがそのまま機能します」とあり、FAQ には「現時点では競合してしまう仕様です」とあります。FAQ の書き方を前提にして、統合したあとにマテリアルのアニメーションが動くかを、実際に確かめてください。
- マテリアルスロットの前後関係を変える設定（Allow Shuffling Material Slots）は、1.9.0 から既定でオンです。公式は「Avatar Optimizerの作者が知る限りにおいて、マテリアルスロットの前後関係は特に意味を持ちません。」と書いています。画面で、オンとオフの状態を確かめてください。

## 手順

1. SDK の「パフォーマンスの内訳」で、マテリアルスロットの数を確かめます。
2. lilAvatarUtils のレンダラーのタブで、マテリアルスロット数の多いメッシュを見つけます。
3. アバターのルートに AAO Trace And Optimize を付け、「スキンメッシュレンダラーを統合する」と「マテリアルスロットの前後関係を変えることを許可する」が、どちらもオンになっているかを確かめます。
4. 自動で統合されないメッシュは、AAO Merge Skinned Mesh で手動で統合します。統合先の Root Bone と Anchor Override を設定します。
5. 同じシェーダーのマテリアルは、AAO Merge Material を、Merge Skinned Mesh と同じ GameObject に追加します。
6. TTT の AtlasTexture を使うときは、結合時参照を指定して、マテリアルをまとめます。マテリアルスロットが減るとは限らないため、手順 3 から 5 と組み合わせて確かめてください（この記事の整理）。
7. 処理の後、Play モードに入ったときのランクか、NDM Framework の「Manual bake avatar」で作った複製で、アニメーションが動くかと、マテリアルスロットの数を確かめます。

## 注意・うまくいかないとき

::: warning
- TTT の AtlasTexture では、マテリアルスロットの結合はできないと、公式が明言しています。マテリアルスロットの数を減らすには、AAO の統合の機能と併用してください。
- 統合後に、マテリアルのアニメーションが動かないときは、AAO の公式 FAQ の案内を見てください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- AAO Merge Skinned Mesh で統合したメッシュは、見えない・明るさが違うなどの症状が出ることがあります。FAQ では、Root Bone や Anchor Override の指定忘れが原因として挙がっています（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」）。
- Modular Avatar を使っている場合は、アバターのルートに MA Mesh Settings を追加します。AAO の FAQ の「見えない」の項では、アバター全体の Root Bone と Bounds を設定できると案内されています。「明るさ」の項では、アバター全体の Anchor Override を設定できると案内されています（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- MMS のマテリアルスロットへの効果は、この記事では**未確認**{.lbl .lbl-unk}です。
:::

## 関連ページ

- [テクスチャメモリーを減らす方法](/learn/optimize-mesh/texture-memory)
- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)
