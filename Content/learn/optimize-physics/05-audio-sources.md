---
title: オーディオソースの数を減らす
reading: おーでぃおそーすのかずをへらす
description: オーディオソース（Audio Sources）の数の上限と確かめ方、Android・Quest で無効になること、減らすときの注意をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 8（Excellent 1、Good 4、Medium 8）で、モバイル（Android と iOS）では無効
- Android と Quest では、Audio sources は完全に無効と公式に書かれている
:::

Audio Source は、Unity の音を鳴らす部品です。オーディオソース（Audio Sources）は、VRChat 公式のパフォーマンスランクの項目の一つです。上限は PC の Poor が 8 です。Excellent は 1、Good は 4、Medium は 8 です（[VRChat 公式の表](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。モバイル（Android と iOS）では無効です。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Audio Sources がすべて取り除かれます（原文: "All Audio Sources removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

ツールの版が上がると挙動が変わることがあります。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「オーディオソースの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、2（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない Audio Source は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「オーディオソースの数」を確かめます（上限は PC の Poor が 8。モバイル（Android と iOS）では無効です）。
2. 変更の前に、プロジェクトのバックアップを取り、いまの音が鳴るタイミングを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. アバターにある Audio Source を一覧にし、それぞれが何の音かを確かめます。
4. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 2 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
5. Play モードに入り、必要な音が記録どおりに鳴るかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。AAO を使っているときは、ランクを Actual Performance Window で確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」で、オーディオソースの数を確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest**: 公式は、Android と Quest のアバターでは Audio Sources が完全に無効になると書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。原文は "Audio sources are disabled completely for avatars on Android and Quest. Audio sources consume a significant amount of CPU resources and voices have priority." で、訳は「Android と Quest のアバターでは、Audio sources は完全に無効になります。Audio sources は多くの CPU の資源を使い、voices が優先されます」です。公式の [Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の Mobile Removed Components の一覧にも Audio Sources があり、値は常にゼロと書かれています。
:::

## 関連ページ

- [レイキャストの数を減らす](/learn/optimize-physics/raycasts)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
