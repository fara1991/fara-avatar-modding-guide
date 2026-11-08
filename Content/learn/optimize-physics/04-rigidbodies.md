---
title: リジッドボディの数を減らす
reading: りじっどぼでぃのかずをへらす
description: パフォーマンスランクの「リジッドボディの数（Physics Rigidbodies）」の上限と確かめ方、Android と Quest での扱い、AAO の修正の記録をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Excellent が 0、Good が 1、Medium と Poor が 8
- Android と Quest では、Rigidbody は完全に無効です
:::

リジッドボディ（Rigidbody）は、Unity の物理演算で動く部品です。リジッドボディの数（Physics Rigidbodies）は、パフォーマンスランクの項目で、アバターにある Rigidbody の数を数えます。

上限は、PC では Excellent が 0、Good が 1、Medium と Poor が 8 です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。モバイル（Android と iOS）では、Mobile Removed Components の一覧に入っていて、値は常にゼロです。

上限を超えると、次のランクに入ります（[パフォーマンスランク](/learn/vrchat/performance-rank)）。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、リジッドボディがすべて取り除かれます（All Physics Rigidbodies removed）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。

VRChat のドキュメントは英語のみのため、項目名は英語のまま書きます。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「リジッドボディの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、2（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないリジッドボディは、無効にするだけでは数に残ります。
- **AAO を使うとき**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/) では、Rigidbody に関係する修正が 1 件あります。Joint（関節。Rigidbody どうしをつなぐ Unity の部品）が壊れる不具合の修正です。

| 版・日付 | 節 | 修正の内容 |
|---|---|---|
| 1.5.10（2023-11-04） | Fixed（不具合の修正） | Rigidbody の Joint が壊れることがあった不具合を修正 |

## 手順

1. SDK の「パフォーマンスの内訳」で「リジッドボディの数」を確かめます。PC の上限は、Excellent が 0、Good が 1、Medium と Poor が 8 です。Excellent の上限が 0 なので、1 つでもあると Excellent にはなりません。
2. 変更の前に、プロジェクトのバックアップを取り、確かめたい動き（リジッドボディに関係する見た目や動き。特に Joint が関係する動き）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. Rigidbody が付いているオブジェクトを探し、Joint でつながっているかを見ます。
4. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 2 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
5. 変更のあと、手順 2 で記録した動きと比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「リジッドボディの数」を確かめます。

## 注意・うまくいかないとき

::: warning
- **モバイル（Android と iOS）**: Mobile Removed Components の一覧には Physics Rigidbodies が入っていて、値は常にゼロです（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **Android と Quest**: Android と Quest では、アバターの Rigidbody・コライダー・ジョイントは完全に無効になります（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **Joint の不具合の記録**: AAO の 1.5.10 では、Rigidbody の Joint が壊れることがあった不具合を修正しました。Joint が関係するものは、変更の前後で動きを比べてください（**この記事の助言**{.lbl .lbl-adv}）。
- **数値は変わることがあります**: Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があります（[Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。
:::

## 関連ページ

- [コライダーの数を減らす](/learn/optimize-physics/colliders)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
