---
title: レイキャストの数を減らす
reading: れいきゃすとのかずをへらす
description: レイキャスト（VRCRaycast）の数の上限と、1 アバターあたり 80 個の固定の上限、確かめ方をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC が Excellent 1・Good 4・Medium 8・Poor 15、Android が同じ順で 1・2・4・8
- どのプラットフォームでも 1 アバターあたり 80 個の固定の上限があり、FinalIK と共有される
- ほかのプレイヤーに当てるには、VRCRaycast のバージョン 1.1 以降が必要
:::

レイキャスト（VRCRaycast）は、アバターからシーンに光線を飛ばし、ワールドのコライダーやほかのプレイヤーに当たったかを調べる部品です。定義は、アバターにある VRCRaycast コンポーネントの数です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。上限は PC が Excellent 1・Good 4・Medium 8・Poor 15、Android が 1・2・4・8 です。上限の値は、同じページの表と同じです。PC の Poor と Android の Poor は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表とも同じ値です。どのプラットフォームでも 1 アバターあたり 80 個の固定の上限があります。

上限を超えると、次のランクに入ります。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Raycasts がすべて取り除かれます（All Raycasts removed）。

VRChat のドキュメントは英語のみのため、項目名は英語のまま書きます。ツールの版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「レイキャストの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 15）と表示されていました。
- **無効にしたものも数えられます。** 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない VRCRaycast は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

80 個の上限は、FinalIK のコンポーネントと共有されます。FinalIK は、VRCRaycast で足りる場面では、一般に使わないほうがよいです（[VRCRaycast](https://creators.vrchat.com/avatars/avatar-components/raycast/)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「レイキャストの数」を確かめます（上限は PC の Poor が 15、Android の Poor が 8）。
2. 変更の前に、プロジェクトのバックアップを取り、各 VRCRaycast が何を調べているか（何に当てているか）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. アバターにある VRCRaycast を一覧にし、それぞれが何を調べているかを確かめます。
4. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 2 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
5. FinalIK を使っているなら、80 個の上限を FinalIK と分け合っている点を確かめます。VRCRaycast で足りる部分は VRCRaycast に寄せられるかを検討します（**この記事の助言**{.lbl .lbl-adv}）。
6. Play モードに入り、当たり判定の動きが変わっていないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。AAO を使っているときは、ランクを Actual Performance Window で確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、レイキャストの数を確かめます。

## 注意・うまくいかないとき

::: warning
- **80 個の固定の上限**: VRCRaycast のコンポーネントには、全プラットフォームで 1 アバターあたり 80 個の固定の上限があります（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) のランクの表の脚注）。
- **FinalIK と共有**: この上限は FinalIK のコンポーネントと共有されます（上の「減らし方の考え方」を参照）。
- **FinalIK と Android・Quest**: Android と Quest のアバターでは、FinalIK のカスタムのコンポーネントは完全に無効です（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **Player の層**: Player の層（ほかのプレイヤーのコライダー）と、ほかの層を 1 つ以上含むレイヤーのマスクを使うと、光線は 1 本ではなく、合成された 2 本として飛ばされます。ほかのプレイヤーに当てるには、VRCRaycast のバージョン 1.1 以降が必要です。
:::

## 関連ページ

- [オーディオソースの数を減らす](/learn/optimize-physics/audio-sources)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
