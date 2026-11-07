---
title: スキンドメッシュの数を減らす
reading: すきんどめっしゅのかずをへらす
description: ボーンで動くメッシュ（スキンドメッシュ）の数を減らす方法です。AAO の Merge Skinned Mesh と自動の統合を中心に、ほかのツールの機能、統合したあとの注意を説明します。
difficulty: 中級
updated: 2026-11-07
---

::: point
- スキンドメッシュの上限は、PC が 16、Android が 2
- Skinned Mesh Renderer は 1 つまでにするのがおすすめで、これはランクの上限とは別の推奨値
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなる
- 自動では統合しないメッシュもあるため、必要に応じて手動の Merge Skinned Mesh を使うのがおすすめ
:::

スキンドメッシュ（SkinnedMeshRenderer。ボーンで変形するメッシュのことです）の数は、パフォーマンスランクの「スキンドメッシュの数」で数えられます。上限は PC が 16、Android が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。どれか 1 項目でも上限を超えると、次のランクになります。

## 減らす前に確かめること

- 数は、VRChat SDK の Control Panel の「パフォーマンスの内訳」で確かめます。「スキンドメッシュの数」の左の値が、このアバターの数です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- AAO を使ったあとは、Control Panel のランクはあてになりません。Play モードに入ったときのランクは、anatawa12 さんの Gist Pack の Actual Performance Window で確かめるのがおすすめです。処理後の姿は、アバターの GameObject を右クリックして `NDM Framework` > `Manual bake avatar` を選ぶと、複製で見られます（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）。
- lilAvatarUtils のレンダラーのタブでは、メッシュごとのポリゴン数・マテリアルスロット数・ブレンドシェイプ数を一覧で確かめられます。

## 減らし方の考え方

- メッシュを統合するのがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表にも同じ内容があり、出典は [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。Skinned Mesh Renderer は 1 つまで、静的（Static）メッシュは 3 つまでにするのがおすすめです。この数は、ランクの上限とは別の推奨値です。
- 統合には、AAO の Trace And Optimize による自動の統合と、AAO Merge Skinned Mesh による手動の統合があります。自動では一部のメッシュを統合しないこともあるため、必要に応じて手動の Merge Skinned Mesh を使うのがおすすめです。
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。衣装の切り替えに使っているメッシュは、統合の対象にするかを先に決めてください（**この記事の助言**{.lbl .lbl-adv}）。
- AAO は、アバターの見た目をほとんど変えずに性能を上げることを目的としています（[AAO の基本コンセプト](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/basic-concept/)）。統合のあとは、見た目と動きを確かめてください。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**:
  - **Trace And Optimize の「スキンメッシュレンダラーを統合する」**: 分かれている必要のないスキンメッシュレンダラーを、自動で統合します。
  - **AAO Merge Skinned Mesh**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。統合すると、メッシュ・マテリアル・BlendShape・範囲（BOUNDS）を設定します（ほかの設定は変わりません）。
  - 同じマテリアルを使うマテリアルスロットも統合できます。マテリアルの統合は、DrawCall（描画の呼び出し）を減らします。
- **どう使うか**:
  - Trace And Optimize はアバターのルートに付けます。行う最適化は、チェックボックスで選べます。
  - AAO Merge Skinned Mesh は、新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と一緒に付けます（種別は「Source Edit Skinned Mesh Component」です）。
  - 主な設定は、次のとおりです。
    - スキンメッシュレンダラー（Skinned Renderers）: 統合対象の SkinnedMeshRenderer の一覧です。
    - メッシュレンダラー（Basic Renderers）: 統合対象の MeshRenderer の一覧です（[基本メッシュの数を減らす](/learn/optimize-mesh/basic-mesh)）。
    - 空のレンダラーオブジェクトを削除する（Remove Empty Renderer GameObject）: 統合対象の SkinnedMeshRenderer が付いている GameObject に、ほかのコンポーネントがなければ、その GameObject を取り除きます。既定はオンです（CHANGELOG の 0.4.4）。
    - 有効無効状態が統合先と異なるレンダラーを統合しない（Skip Enablement Mismatched Renderers）: 既定はオフです（CHANGELOG の 1.8.0 で既定がオフに変わりました）。
    - 有効無効状態に関するアニメーションをコピーする（Copy Enablement Animation）: 統合対象の有効無効のアニメーションを、統合先の SkinnedMeshRenderer にコピーします。
    - BlendShape モード（BlendShape Mode）: 既定は、BlendShape 名を自動で変えて重複を避けるモードです。
    - マテリアルの統合（Merge Materials）: 同じマテリアルを使うマテリアルスロットを 1 つにまとめるかを選びます。
  - Trace And Optimize が同様の処理を自動で行うため、大抵の場合は AAO Merge Skinned Mesh を使う必要はありません。自動で統合されなかったメッシュに使うのが目安です（この記事の整理）。
  - 統合の範囲は、メッシュ・マテリアル・BlendShape・範囲（Bounds）を設定するところまでです。Anchor Override などは、統合先の SkinnedMeshRenderer で手動で設定します。
- **注意**:
  - 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。詳しくは、下の「注意・うまくいかないとき」を見てください。
  - Cloth の付いた SkinnedMeshRenderer を統合元にすると、エラーになります（CHANGELOG の 1.7.0）。

#### Merge Material との関係

- AAO Merge Material は、同じシェーダーを使うマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめるコンポーネントです。AAO Merge Skinned Mesh の「マテリアルの統合」は、同じマテリアルを使うマテリアルスロットを 1 つにまとめます。
- 別々のメッシュで使われているマテリアルもまとめたいときは、AAO Merge Skinned Mesh と同じ GameObject に AAO Merge Material を追加します。
- AAO Merge Material で作ったマテリアルを使うマテリアルスロットは、AAO Merge Skinned Mesh では統合されません。
- Merge Material の制限として、マテリアルを置き換えるアニメーションには対応していません。Parallax や UV スクロールのような、使用時にテクスチャの参照される位置が変わるシェーダー機能も、サポートされていません。
- AAO Merge ToonLit Material は非推奨です。代わりに Merge Material を使うのがおすすめです。

## 手順

1. Control Panel の「パフォーマンスの内訳」で、「スキンドメッシュの数」と「基本メッシュの数」を控えます。
2. アバターのルートに AAO Trace And Optimize を付け、「スキンメッシュレンダラーを統合する」を使うかを選びます。OSC（外部ツールと値をやりとりする仕組み）を使うアバターでは、付ける前に、[ボーンの数を減らす](/learn/optimize-mesh/bones) の「注意・うまくいかないとき」を読んでください（自動の最適化で、OSC 用の PhysBone・Contact が消えるおそれがあるためです）。
3. Play モード（Actual Performance Window で確かめる）か、Manual bake avatar で、統合後の見た目と動きを確かめます。
4. 統合されなかったメッシュは、新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と AAO Merge Skinned Mesh を付けて、スキンメッシュレンダラーの一覧に入れます。
5. 同じマテリアルのマテリアルスロットを 1 つにまとめるなら、「マテリアルの統合（Merge Materials）」をオンにします。アニメーションでマテリアルを別のものに置き換えているメッシュは、「マテリアルの統合」の「統合する」をオフにするのがおすすめです（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
6. 統合先の SkinnedMeshRenderer で、Root Bone と Anchor Override を設定します。Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加します。「見えない」の場合は、アバター全体の Root Bone と範囲（BOUNDS）を、「明るさ」の場合は、アバター全体の Anchor Override を、MA Mesh Settings で設定できます（FAQ）。
7. Build & Test で、見た目と動き（衣装のオン・オフ、マテリアルのアニメーション）が変わっていないかを確かめます（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)）。

## 注意・うまくいかないとき

::: warning
統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。衣装の切り替えに使っているメッシュを統合するときは、切り替えが統合のあとも動くかを、必ず確かめてください。
:::

- **統合後にメッシュが見えない**: 多くの場合は、統合先の SkinnedMeshRenderer の Root Bone を指定し忘れていることが原因です（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。AAO Merge Skinned Mesh は Root Bone を自動で設定しません。Root Bone か Anchor Override が設定されていないときは、警告が出ます（CHANGELOG の 1.7.0）。
- **統合後に明るさが違う**: 多くの場合は、統合先の SkinnedMeshRenderer の Anchor Override を指定し忘れていることが原因です。
- **統合後に個別のオン・オフができない**: 「有効無効状態に関するアニメーションをコピーする」は、コピーできるのは 1 種類だけです。複数の GameObject がアニメーションされていると、エラーになり、ビルドが止まります（CHANGELOG の 1.8.0）。統合先の SkinnedMeshRenderer の enabled は、この機能を使うときにアニメーションしてはいけません。
- **マテリアルのプロパティのアニメーションが競合する**: 統合前のアニメーションがそのまま機能する場合と、競合する場合があります。競合は既知のバグで、現時点では競合してしまう仕様です（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)。issue は [#340](https://github.com/anatawa12/AvatarOptimizer/issues/340)）。競合するときは警告が表示されます。この記事では、FAQ の書き方を前提にして、統合のあとにマテリアルのアニメーションが動くかを実際に確かめてください。
- **マテリアルスロットのアニメーションが競合する**: 「マテリアルの統合」の「統合する」をオフにするのがおすすめです（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- **PhysBone・Contact が効かない（OSC ギミック）**: 注意と対応（FAQ の Asset Description の方法）は、[ボーンの数を減らす](/learn/optimize-mesh/bones) の「注意・うまくいかないとき」にまとめています。
- **自動で統合されないメッシュ**: 統合されなかったメッシュは、手動の AAO Merge Skinned Mesh を使います。
- **ほかのツールとの併用**: TTT の AtlasTexture と AAO を併用するときは、AAO の Trace And Optimize や Merge Skinned Mesh との併用を強くおすすめします（[Quest対応のためのアトラス化とマテリアルの結合のチュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [基本メッシュの数を減らす](/learn/optimize-mesh/basic-mesh)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
