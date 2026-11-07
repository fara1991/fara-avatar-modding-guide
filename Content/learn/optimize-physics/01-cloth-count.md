---
title: Cloth の数を減らす
reading: くろーすのかずをへらす
description: Cloth コンポーネントの数の上限と確かめ方、Cloth の使用を控えるおすすめ、AAO での扱いをまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 1 で、モバイル（Android と iOS）では無効
- 無効にしたものも、ランクの数に入ります
:::

Cloth の数は、アバターにある Cloth コンポーネント（布の動きを物理で計算する部品）の数を数える項目です。SDK の「パフォーマンスの内訳」では「CLOTH メッシュの数」と表示されますが、定義は Cloth コンポーネントの総数です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。この記事も、その定義に合わせて書きます。上限は PC の Poor が 1 で、モバイル（Android と iOS）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

モバイルでは、Cloth は表示されることがないため無効になります。Mobile Removed Components（モバイルで取り除かれるコンポーネントの一覧）には、Cloths（Cloth コンポーネントの数）が入っています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

上限を超えると、次のランクになります。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Cloth コンポーネントがすべて取り除かれます（All Cloth components removed）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります。

VRChat のドキュメントは英語のみのため、項目名は英語のまま書きます。ツールの版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「CLOTH メッシュの数」の行で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 1）と表示されていました。
- **表から読めること（この記事の読み方）**: PC の表では、Excellent の上限が 0 です。そのため、Cloth コンポーネントの数が 0 を超えると Excellent にはなりません。Good・Medium・Poor の上限は、いずれも 1 です。2 つ以上あると Poor の上限を超えるため、次のランク（Very Poor）になります。
- **無効にしたものも数えられます。** 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない Cloth は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめます（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。

## 減らし方の考え方

Cloth の使用は、強く控えるのがおすすめです。Cloth は Unity の標準のコンポーネントで、コストは Dynamic Bones と同程度で、設定も難しいです。また、200 頂点程度より多いメッシュには使わないのがおすすめです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips) の「Limit usage of Cloth」の節）。

この記事では、残す Cloth は、上の勧めに沿って使う場所を見直します。頂点数の勧めは、[Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) で扱います。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO 1.7.0（2024 年 4 月 30 日、Changed（仕様の変更））以降、Cloth のある SkinnedMeshRenderer（ボーンで変形するメッシュの部品）は、Merge Skinned Mesh（複数のメッシュを 1 つにまとめる機能）の統合元にできません（エラーになります。[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「CLOTH メッシュの数」（定義は Cloth コンポーネントの数）の値と上限を確かめます。PC の上限は Poor が 1 で、モバイル（Android と iOS）では無効です。
2. アバターの中から、Cloth コンポーネントが付いているオブジェクトを探します。そのうえで、布の動きがアバターの見せ方に必要かを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取り、確かめたい布の動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 200 頂点程度より多いメッシュに付く Cloth は、外すかを検討します（[Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) の手順 3 と同じ基準）。
5. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 3 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
6. Play モードに入って、残した Cloth の動きが、記録したものと同じかを確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、値を確かめます。AAO を使っているときは、Play モードに入ったときのランクを Actual Performance Window で確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest**: Android と Quest では、Cloth は完全に無効です（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。Mobile Removed Components の一覧の値は、アバターの詳細画面に出ることがありますが、常にゼロです。
- **無効にしただけでは数は減りません**: 無効にしたものも数えられます。上の「減らす前に確かめること」を参照してください。
- **AAO の Cloth の記録（1.7.0）**: 1.7.0 から、Cloth のある SkinnedMeshRenderer を Merge Skinned Mesh の統合元にするとエラーになります（上の AAO の節）。
:::

## 関連ページ

- [Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
