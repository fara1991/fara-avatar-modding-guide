---
title: コライダーの数を減らす
reading: こらいだーのかずをへらす
description: パフォーマンスランクの「コライダーの数（Physics Colliders）」の上限と確かめ方、PhysBone のコライダーとの違い、減らし方と AAO での扱いをまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC では Excellent が 0、Good が 1、Medium と Poor が 8
- PhysBone のコライダーとは別の項目
- Android と Quest では、コライダーは完全に無効です
:::

コライダーの数（Physics Colliders）は、アバターにある Collider（Unity の物理の衝突の形を決める部品）の数です。PhysBone のコライダー（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)）とは、別の項目です。

上限は、PC では Excellent が 0、Good が 1、Medium と Poor が 8 です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。Android と iOS では、Mobile Removed Components の一覧に入っていて、値は常にゼロです。

上限を超えると、次のランクに入ります（[パフォーマンスランク](/learn/vrchat/performance-rank)）。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、コライダーがすべて取り除かれます（All Physics Colliders removed）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。

VRChat のドキュメントは英語のみのため、項目名は英語のまま書きます。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コライダーの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、1（上限 8）と表示されていました。
- **無効にしたものも数えます。** 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **PhysBone のコライダーと混同しないこと。** 統計表の「PhysBones Colliders」は、アバターにある PhysBone のコライダーのスクリプトの数で、別の項目です。
- **AAO を使うとき**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

- 使っていないと確かめた Collider は、無効ではなく取り除きます（無効のものも数に入るためです）。
- PhysBone のコライダーは、AAO の Trace And Optimize で同じ設定のものを統合できます（PhysBone のコライダーの記事を参照: [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)）。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: Trace And Optimize の「PhysBone を最適化する」は、全く同じ設定の PhysBone Collider を 1 つに統合します（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。これは PhysBone のコライダーの統合です（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders) を参照）。

[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/) では、Unity の Collider に関係する修正が 3 件あります。いずれも Fixed（不具合の修正）です。

| 版・日付 | 修正の内容 |
|---|---|
| 1.5.2（2023-10-10） | Avatar Standard Colliders が削除されることがあった不具合を修正 |
| 1.6.0（2023-11-27） | Collider のインスタンスではなく Transform を指定したときに、Trigger Collider が消えることがあった不具合を修正 |
| 1.8.0（2024-11-30） | VRCStation が作る BoxCollider のアニメーションのバインドが消されていた不具合を修正 |

1.8.0 の不具合では、Quest / Android に対応した GogoLoco などの飛行するアバターが壊れる可能性がありました。

## 手順

1. SDK の「パフォーマンスの内訳」で「コライダーの数」を確かめます。PC の上限は、Excellent が 0、Good が 1、Medium と Poor が 8 です。Excellent の上限が 0 なので、1 つでもあると Excellent にはなりません。
2. 変更の前に、プロジェクトのバックアップを取り、確かめたい動き（コライダーに関係する見た目や動き）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. 不要と確かめた Collider を取り除き、記録と比べます。
4. もう一度 SDK の「パフォーマンスの内訳」を開き、「コライダーの数」を確かめます。

## 注意・うまくいかないとき

::: warning
- **モバイル（Android と iOS）**: Mobile Removed Components の一覧には Physics Colliders が入っていて、値は常にゼロです（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **Android と Quest**: Android と Quest では、アバターの Rigidbody・コライダー・ジョイントは完全に無効になります（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **数値は変わることがあります**: Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があります（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。
:::

## 関連ページ

- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [リジッドボディの数を減らす](/learn/optimize-physics/rigidbodies)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
