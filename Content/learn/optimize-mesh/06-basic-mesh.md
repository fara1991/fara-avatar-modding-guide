---
title: 基本メッシュの数を減らす
reading: きほんめっしゅのかずをへらす
description: ボーンで動かないメッシュ（基本メッシュ）の数を減らす方法です。AAO の Merge Skinned Mesh に MeshRenderer を入れて統合する方法を中心に、ほかのツールの機能、注意を説明します。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 基本メッシュの上限は、PC が 24、Android が 2
- 描画の呼び出し（ドローコール）は、メッシュごとに 1 回ずつ必要になるのが基本（Unity マニュアルの説明）
- AAO 1.9.21 では、AAO Merge Skinned Mesh の「メッシュレンダラー」に MeshRenderer を入れて統合できます
- Trace And Optimize の自動の統合では、スキンメッシュレンダラーが対象になります
:::

基本メッシュ（MeshRenderer。ボーンで動かないメッシュのことです）の数は、パフォーマンスランクの「基本メッシュの数」で数えられます。上限は PC が 24、Android が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。上限を超えると次のランクになります。描画の呼び出し（ドローコール）は、メッシュごとに 1 回ずつ必要になるのが基本で、減らすと描画の時間が短くなります（Unity マニュアルの説明。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) にも補足があります）。

## 減らす前に確かめること

- 数は、VRChat SDK の Control Panel の「パフォーマンスの内訳」で確かめます。「基本メッシュの数」の左の値が、このアバターの数です。数えるのは、ボーンで動かない Mesh Renderer のコンポーネントの数です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- AAO を使ったあとは、Control Panel のランクはあてになりません。Play モードに入ったときのランクや、Manual bake avatar の複製で確かめてください（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）。
- lilAvatarUtils のレンダラーのタブでは、メッシュごとのポリゴン数・マテリアルスロット数などを一覧で確かめられます。

## 減らし方の考え方

- メッシュを統合するのがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表にも同じ内容があり、出典は [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。静的（Static）メッシュは 3 つまでにするのがおすすめです。この数は、ランクの上限とは別の推奨値です。
- AAO の Merge Skinned Mesh は、統合対象に MeshRenderer を入れられます。統合する静的メッシュの移動・回転・変形は、ボーンで再現されます。
- AAO の Remove Mesh 系では、服で隠れていて見えない部分のメッシュを削除すると、見た目に影響させずに描画の負荷を減らせます。MeshRenderer に使えるのは、1.9.0 以降の Remove Mesh By Box・Remove Mesh By Mask・Remove Mesh By UV Tile です。
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります（[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の注意）。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**:
  - **AAO Merge Skinned Mesh の「メッシュレンダラー（Basic Renderers）」**: 統合対象の MeshRenderer を入れる一覧です。静的メッシュの移動・回転・変形は、ボーンで再現されます。
  - **どう使うか**: 付け方と設定は、[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の AAO の節と同じです。統合する MeshRenderer は、「メッシュレンダラー（Basic Renderers）」の一覧に入れます。
- **注意**:
  - 「空のレンダラーオブジェクトを削除する」の既定の記載は、SkinnedMeshRenderer が付いている GameObject についてのものです。
  - 別々のメッシュのマテリアルもまとめたいときは、AAO Merge Material を同じ GameObject に追加します。AAO 1.9.0 以降は、Merge Material も MeshRenderer に対応します（[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の「Merge Material との関係」）。
  - AAO 1.7.5 では、MeshRenderer を形（Shape）に使うパーティクルシステムが壊れる問題を修正しました（[changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

## 手順

1. Control Panel の「パフォーマンスの内訳」で、「基本メッシュの数」を控えます。
2. 服などで隠れていて見えない部分が多いメッシュは、AAO の Remove Mesh By Box・Remove Mesh By Mask・Remove Mesh By UV Tile で、削除する範囲を先に決めるかを検討します（1.9.0 以降の MeshRenderer 対応）。
3. 新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と AAO Merge Skinned Mesh を付けます。
4. 「メッシュレンダラー（Basic Renderers）」の一覧に、統合する MeshRenderer を入れます。
5. 同じマテリアルのマテリアルスロットを 1 つにまとめるなら、「マテリアルの統合（Merge Materials）」をオンにします。統合したいマテリアルが別のメッシュにあるなら、同じ GameObject に AAO Merge Material を追加します。
6. 統合先の SkinnedMeshRenderer で、Root Bone と Anchor Override を設定します。Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加します。「見えない」の場合は、アバター全体の Root Bone と範囲（BOUNDS）を、「明るさ」の場合は、アバター全体の Anchor Override を、MA Mesh Settings で設定できます（FAQ）。
7. Play モード（Actual Performance Window で確かめる）か Manual bake avatar で確かめたうえで、Build & Test で見た目と動きが変わっていないかを確かめます（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)）。

## 注意・うまくいかないとき

::: warning
統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。切り替えに使っているメッシュを統合するときは、切り替えが統合のあとも動くかを確かめてください。
:::

- **統合後にメッシュが見えない**: 多くの場合は、統合先の SkinnedMeshRenderer の Root Bone を指定し忘れていることが原因です（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。AAO Merge Skinned Mesh は Root Bone を自動で設定しません。
- **統合後に明るさが違う**: 多くの場合は、統合先の SkinnedMeshRenderer の Anchor Override を指定し忘れていることが原因です。
- **マテリアルのアニメーションが競合する**: 統合前のアニメーションがそのまま機能する場合と、競合する場合があります。詳しくは、[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の「注意・うまくいかないとき」を見てください。
- **Trace And Optimize が自動で統合しないメッシュ**: 自動では統合しないメッシュがあるときは、手動の AAO Merge Skinned Mesh を使うのがおすすめです。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
