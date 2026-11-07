---
title: マテリアルスロットを減らす方法
reading: まてりあるすろっとをへらすほうほう
description: VRChat のアバターのマテリアルスロットを減らす方法を、TexTransTool・Avatar Optimizer（AAO）の統合の機能を中心に整理します。TexTransTool の AtlasTexture では、マテリアルスロットは結合できないため、その注意も扱います。
difficulty: 中級
updated: 2026-11-07
---

::: point
- マテリアルスロットの上限は、PC の Poor が 32、Android の Poor が 4
- TTT の AtlasTexture では、マテリアルスロットは結合できません
- 「マテリアルの統合」で、同じマテリアルのスロットを 1 つにまとめる
- 統合先の Root Bone と Anchor Override は自動では設定されないため、手動で設定する
:::

マテリアルスロットは、メッシュがマテリアルを割り当てる枠の数です。SDK の「パフォーマンスの内訳」では「マテリアルスロットの数」と表示され、上限は PC の Poor が 32、Android の Poor が 4 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。上限を超えると、次のランクになります（[パフォーマンスランク](/learn/vrchat/performance-rank)）。

この記事では、マテリアルスロットを減らす方法を、ツールの機能ごとに整理します。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: マテリアルスロットの数を確かめます。例の画面では、88（上限 32）と表示されていました。
- **枠の数え方**: パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠、Line Renderer は 1 枠を使います。
- **lilAvatarUtils のレンダラーのタブ**: メッシュごとのマテリアルスロット数を見られます。この数が大きいほど、描画の負荷が大きくなります。

## 減らし方の考え方

- マテリアルが 10 を超えるなら、テクスチャアトラス（複数のテクスチャを 1 枚にまとめたもの）を検討するのがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) にも同じ内容があります）。
- 同じマテリアルを使う枠を 1 つにまとめる機能は、AAO の Merge Skinned Mesh と Trace And Optimize にあります。描画の呼び出し（ドローコール）を減らせます。
- マテリアルを一つにまとめる作業と、枠（スロット）を一つにまとめる作業は、別のものです。下の TTT の注意を見てください。

## ツール別の方法

### TexTransTool（TTT） {.tool .tool-ttt}

**何をするか**: AtlasTexture は、マテリアルを 1 つにまとめて、テクスチャを 1 枚にまとめる機能です。AtlasTexture のマテリアルの結合は、マテリアルをまとめるだけで、マテリアルスロットの結合はできません（[TexTransTool の公式チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing) の末尾の注意）。

**どう使うか**:

- マテリアルを結合するには、「結合時参照」を指定します。
- 結合時参照が None のままだと、マテリアルは結合されません。
- 何らかのマテリアルが割り当てられている場合は、そのマテリアルに結合されます（[公式: AtlasTexture](https://ttt.rs64.net/docs/Reference/AtlasTexture)）。
- 部分的にマテリアルを選び、それぞれの結合時参照を指定できる「マテリアルの結合グループ」があります。

**注意**:

- マテリアルスロットを減らすには、AAO の Trace And Optimize や Merge Skinned Mesh との併用を強くおすすめします（[TexTransTool の公式チュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。
- [Quest対応のためのアトラス化とマテリアルの結合のチュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing) には、Quest 向けにマテリアルを結合する例があります。Quest 向けのこの方法は、見た目を維持できない軽量化になります。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO には、スロットを減らす機能が 3 つあります。

- **Trace And Optimize（自動）**: 「スキンメッシュレンダラーを統合する」は、統合しても問題のないスキンドメッシュを 1 つにまとめます。自動では一部のメッシュを統合しないこともあるため、必要に応じて手動の Merge Skinned Mesh を使うのがおすすめです。同じマテリアルが複数の枠にあるときは、自動で 1 つにまとめられます。AAO 1.8.0 では、この動きが加わりました。
- **AAO Merge Skinned Mesh（手動）**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。「マテリアルの統合」にチェックを入れると、同じマテリアルのスロットを 1 つにまとめます。マテリアルの統合は、DrawCall（描画の呼び出し）を減らします。
- **AAO Merge Material（手動）**: 同じシェーダーを使うマテリアルを、テクスチャを並べて 1 つのマテリアルにまとめます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の記事も見てください）。

**どう使うか**:

- Merge Skinned Mesh は、新しい空の GameObject を作り、メッシュを指定していない SkinnedMeshRenderer と、AAO Merge Skinned Mesh を付けます。統合したいレンダラーを一覧に入れ、「マテリアルの統合」をオンにします。
- 別々のメッシュで使われているマテリアルも一緒にまとめたいときは、AAO Merge Material を、AAO Merge Skinned Mesh と同じ GameObject に追加します。

**注意**:

- AAO Merge Material で作ったマテリアルを使うマテリアルスロットは、AAO Merge Skinned Mesh では統合されません。
- 統合すると、個々のメッシュをアニメーションでオンとオフにできなくなります。統合先の Root Bone と Anchor Override は、自動では設定されないため、手動で設定します。
- 統合対象で同じマテリアルのスロットは、既定で統合されます。アニメーションで別のマテリアルに置き換えている場合は、Merge Skinned Mesh の「マテリアルの統合」の「統合する」をオフにするのがおすすめです（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- マテリアルプロパティのアニメーションについて、[Merge Skinned Mesh](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-skinned-mesh/) のリファレンスの備考は『マテリアルに関するアニメーションは統合前のものがそのまま機能します』と説明していますが、[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/) は『現時点では競合してしまう仕様です』と説明していて、食い違っています。統合したあとで、実際に動くかを確かめてください。
- マテリアルスロットの前後関係を変える設定（Allow Shuffling Material Slots）は、1.9.0 から既定でオンです。Avatar Optimizer の作者が知る限り、マテリアルスロットの前後関係は特に意味を持ちません。画面で、オンとオフの状態を確かめてください。

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
- TTT の AtlasTexture では、マテリアルスロットの結合はできません。マテリアルスロットの数を減らすには、AAO の統合の機能と併用してください。
- 統合後に、マテリアルのアニメーションが動かないときは、AAO の FAQ を見てください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- AAO Merge Skinned Mesh で統合したメッシュは、見えない・明るさが違うなどの症状が出ることがあります。Root Bone や Anchor Override の指定忘れが原因になることがあります（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」）。
- Modular Avatar を使っている場合は、アバターのルートに MA Mesh Settings を追加します。「見えない」の場合は、アバター全体の Root Bone と Bounds を設定できます。「明るさ」の場合は、アバター全体の Anchor Override を設定できます（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
:::

## 関連ページ

- [テクスチャメモリーを減らす方法](/learn/optimize-mesh/texture-memory)
- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)
