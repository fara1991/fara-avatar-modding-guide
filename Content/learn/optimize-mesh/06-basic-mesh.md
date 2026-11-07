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
- AAO Merge Skinned Mesh の「メッシュレンダラー」に MeshRenderer を入れて統合できる（AAO 1.9.21 の公式の資料）
- Trace And Optimize の自動の統合について、公式の説明は、自動の統合の対象としてスキンメッシュレンダラーを挙げている
:::

基本メッシュ（MeshRenderer。ボーンで動かないメッシュのことです）の数は、パフォーマンスランクの「基本メッシュの数」で数えられます。上限は PC が 24、Android が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。上限を超えると次のランクになります。描画の呼び出し（ドローコール）は、メッシュごとに 1 回ずつ必要になるのが基本で、減らすと描画の時間が短くなると、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) に、Unity マニュアルの説明として書かれています。

## 減らす前に確かめること

- 数は、VRChat SDK の Control Panel の「パフォーマンスの内訳」で確かめます。「基本メッシュの数」の左の値が、このアバターの数です。数えるのは、ボーンで動かない Mesh Renderer のコンポーネントの数です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- AAO を使ったあとは、Control Panel のランクはあてになりません。Play モードに入ったときのランクや、Manual bake avatar の複製で確かめてください（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）。
- lilAvatarUtils のレンダラーのタブでは、メッシュごとのポリゴン数・マテリアルスロット数などを一覧で確かめられます。

## 減らし方の考え方

- VRChat 公式の勧め（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表。出典は Avatar Optimization Tips）は、メッシュを統合することです。静的（Static）メッシュは 3 つまでを推奨しています。この数は、ランクの上限とは別の推奨値です。
- AAO の Merge Skinned Mesh は、統合対象に MeshRenderer を入れられます。公式は、統合する静的メッシュの移動・回転・変形を、ボーンで再現すると説明しています。
- AAO の Remove Mesh 系の説明では、服で隠れていて見えない部分のメッシュを削除すると、見た目に影響させずに描画の負荷を減らせると書かれています。MeshRenderer に使えるのは、1.9.0 以降の Remove Mesh By Box・Remove Mesh By Mask・Remove Mesh By UV Tile です（公式の一覧）。
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります（[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の注意）。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**:
  - **AAO Merge Skinned Mesh の「メッシュレンダラー（Basic Renderers）」**: 統合対象の MeshRenderer を入れる一覧です。公式は、静的メッシュの移動・回転・変形をボーンで再現すると書いています。
  - **どう使うか**: 付け方と設定は、[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の AAO の節と同じです。統合する MeshRenderer は、「メッシュレンダラー（Basic Renderers）」の一覧に入れます。
- **注意**:
  - 「空のレンダラーオブジェクトを削除する」の既定の記載は、SkinnedMeshRenderer が付いている GameObject についてのものです。
  - 別々のメッシュのマテリアルもまとめたいときは、AAO Merge Material を同じ GameObject に追加します。公式のコンポーネントの一覧では、1.9.0 以降、Merge Material は MeshRenderer にも対応すると書かれています（[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の「Merge Material との関係」）。
  - changelog の 1.7.5 に、MeshRenderer を形（Shape）に使うパーティクルシステムが壊れる問題の修正が記録されています（[changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

## 手順

1. Control Panel の「パフォーマンスの内訳」で、「基本メッシュの数」を控えます。
2. 服などで隠れていて見えない部分が多いメッシュは、AAO の Remove Mesh By Box・Remove Mesh By Mask・Remove Mesh By UV Tile で、削除する範囲を先に決めるかを検討します（1.9.0 以降の MeshRenderer 対応）。
3. 新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と AAO Merge Skinned Mesh を付けます。
4. 「メッシュレンダラー（Basic Renderers）」の一覧に、統合する MeshRenderer を入れます。
5. 同じマテリアルのマテリアルスロットを 1 つにまとめるなら、「マテリアルの統合（Merge Materials）」をオンにします。統合したいマテリアルが別のメッシュにあるなら、同じ GameObject に AAO Merge Material を追加します。
6. 統合先の SkinnedMeshRenderer で、Root Bone と Anchor Override を設定します。Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加します。FAQ の「見えない」の項では、アバター全体の Root Bone と範囲（BOUNDS）を、「明るさ」の項では、アバター全体の Anchor Override を、MA Mesh Settings で設定できると案内されています。
7. Play モード（Actual Performance Window で確かめる）か Manual bake avatar で確かめたうえで、Build & Test で見た目と動きが変わっていないかを確かめます（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)）。

## 注意・うまくいかないとき

::: warning
統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。切り替えに使っているメッシュを統合するときは、切り替えが統合のあとも動くかを確かめてください。
:::

- **統合後にメッシュが見えない**: FAQ によると、多くの場合は、統合先の SkinnedMeshRenderer の Root Bone を指定し忘れていることが原因です。AAO Merge Skinned Mesh は Root Bone を自動で設定しません。
- **統合後に明るさが違う**: FAQ によると、多くの場合は、統合先の SkinnedMeshRenderer の Anchor Override を指定し忘れていることが原因です。
- **マテリアルのアニメーションが競合する**: 公式の書き方は食い違っています。詳しくは、[スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh) の「注意・うまくいかないとき」を見てください。
- **Trace And Optimize が自動で統合しないメッシュ**: 公式の案内は、自動では統合しないメッシュがあるときは、手動の AAO Merge Skinned Mesh を使う、というものです。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [スキンドメッシュの数を減らす](/learn/optimize-mesh/skinned-mesh)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
