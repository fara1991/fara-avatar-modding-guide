---
title: Cloth の最大頂点の確かめ方
reading: くろーすのさいだいちょうてんのたしかめかた
description: Cloth の最大頂点（Total Cloth Vertices）の上限と確かめ方、200 頂点程度より多いメッシュには Cloth を使わないおすすめをまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 200 で、モバイル（Android と iOS）では無効
- PC の Excellent は 0、Good は 50、Medium は 100
- 200 頂点程度より多いメッシュには、Cloth を使わないのがおすすめです
:::

Cloth の最大頂点は、SDK の「パフォーマンスの内訳」で「CLOTH の最大頂点の数」と表示される項目です。項目名は Total Cloth Vertices です。上限は PC の Poor が 200 で、モバイル（Android と iOS）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。PC の Excellent は 0、Good は 50、Medium は 100 です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

上限を超えると、次のランクになります。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Cloth コンポーネントがすべて取り除かれます（All Cloth components removed）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります。

VRChat のドキュメントは英語のみのため、項目名は英語のまま書きます。

## 数の確かめ方

- **SDK の「パフォーマンスの内訳」**: 「CLOTH の最大頂点の数」の行で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 200）と表示されていました。
- **表から読めること（この記事の読み方）**: VRChat のランクの表（PC の値）では、Excellent の上限が 0 です。そのため、値が 0 を超えると Excellent にはなりません。Good の上限は 50、Medium は 100、Poor は 200 です。200 を超えると Poor の上限を超えるため、次のランク（Very Poor）になります。
- **AAO を使うときの確かめ方**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめます（[基本の使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。

## 勧めの読み方

Cloth は Unity の標準のコンポーネントで、コストは Dynamic Bones と同程度で、設定も難しいです。そのため、Cloth の使用は強く控えるのがおすすめです。また、200 頂点程度より多いメッシュには使わないのがおすすめです（[Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips) の「Limit usage of Cloth」の節）。

この記事では、200 頂点程度より多いメッシュに付いている Cloth は、外すかを検討します。Cloth の数そのものを減らす話は、[Cloth の数を減らす](/learn/optimize-physics/cloth-count) で扱います。

## 手順

1. SDK の「パフォーマンスの内訳」で、「CLOTH の最大頂点の数」の値と上限を確かめます。PC の上限は Poor が 200 で、モバイル（Android と iOS）では無効です。
2. アバターの中から、Cloth コンポーネントが付いているメッシュを探します。
3. 上の「勧めの読み方」に沿って、200 頂点程度より多いメッシュに付いている Cloth を、外すかを検討します。
4. 変更の前に、プロジェクトのバックアップを取り、確かめたい布の動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
5. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 4 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
6. Play モードに入って、残した Cloth の動きが、記録したものと同じかを確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で値を確かめます。AAO を使っているときは、Play モードに入ったときのランクを Actual Performance Window で確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest**: Android と Quest では、Cloth は完全に無効です（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。Mobile Removed Components の一覧には、Total Cloth Vertices も入っています（値は常にゼロ）。
- **AAO の Cloth の記録（1.7.0）**: 1.7.0 から、Cloth のある SkinnedMeshRenderer を Merge Skinned Mesh の統合元にするとエラーになります。
:::

## 関連ページ

- [Cloth の数を減らす](/learn/optimize-physics/cloth-count)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
