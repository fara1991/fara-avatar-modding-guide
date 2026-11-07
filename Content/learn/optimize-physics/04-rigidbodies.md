---
title: リジッドボディの数を減らす
reading: りじっどぼでぃのかずをへらす
description: パフォーマンスランクの「リジッドボディの数（Physics Rigidbodies）」の上限と確かめ方、Android と Quest での扱い、AAO の記録をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Excellent が 0、Good が 1、Medium と Poor が 8
- Android と Quest では、Rigidbody は完全に無効と公式に書かれている
:::

リジッドボディ（Rigidbody）は、Unity の物理演算で動く部品です。リジッドボディの数（Physics Rigidbodies）は、VRChat 公式のパフォーマンスランクの項目で、アバターにある Rigidbody の数を数えます。

上限は、PC では Excellent が 0、Good が 1、Medium と Poor が 8 です（[VRChat の公式の表](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。モバイル（Android と iOS）では、公式の Mobile Removed Components に入っていて、値は常にゼロと書かれています。

上限を超えると、次のランクに入ると書かれています（英語の原文の要約。[パフォーマンスランク](/learn/vrchat/performance-rank)）。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、リジッドボディがすべて取り除かれます（原文: "All Physics Rigidbodies removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

VRChat の公式は英語の原文だけなので、ここでの引用は原文に訳を添えています。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「リジッドボディの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、2（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないリジッドボディは、無効にするだけでは数に残ります。
- **AAO を使うとき**: AAO の公式の基本的な使い方のページは、非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO の CHANGELOG で、Rigidbody に関係する記録は 1 件です。Joint（関節。Rigidbody どうしをつなぐ Unity の部品）が壊れる、という記録です。

| 版・日付 | 節 | 原文 | 記録の中身（訳） |
|---|---|---|---|
| 1.5.10（2023-11-04） | Fixed | `RigidBody Joint can be broken` | Rigidbody の Joint が壊れることがある、という不具合を直した記録 |

## 手順

1. SDK の「パフォーマンスの内訳」で「リジッドボディの数」を確かめます。PC の上限は、Excellent が 0、Good が 1、Medium と Poor が 8 です。Excellent の上限が 0 なので、1 つでもあると Excellent にはなりません（公式の表からの読み取りです）。
2. 変更の前に、プロジェクトのバックアップを取り、確かめたい動き（リジッドボディに関係する見た目や動き。特に Joint が関係する動き）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. Rigidbody が付いているオブジェクトを探し、Joint でつながっているかを見ます。
4. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 2 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
5. 変更のあと、手順 2 で記録した動きと比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「リジッドボディの数」を確かめます。

## 注意・うまくいかないとき

::: warning
- **モバイル（Android と iOS）**: 公式の Mobile Removed Components は、"The following components are disabled on mobile devices since they can never appear on avatars:"（訳: 次のコンポーネントは、アバターに表示されることがないため、モバイルの端末では無効になります）と書き、その一覧に Physics Rigidbodies が入っています。公式は、この値は "they are always zero"（いつもゼロ）と書いています。
- **Android と Quest**: 公式の Quest Content Limitations は、"Rigidbodies, colliders, and joints are disabled completely for avatars on Android and Quest."（訳: Android と Quest では、アバターの Rigidbody・コライダー・ジョイントは完全に無効になります）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **Joint の不具合の記録**: AAO の 1.5.10 の記録には、Rigidbody の Joint が壊れることがあったという不具合があります。Joint が関係するものは、変更の前後で動きを比べてください（**この記事の助言**{.lbl .lbl-adv}）。
- **数値は変わることがあります**: 公式は、Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。
:::

## 関連ページ

- [コライダーの数を減らす](/learn/optimize-physics/colliders)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
