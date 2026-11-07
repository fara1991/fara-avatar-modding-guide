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

レイキャスト（VRCRaycast）は、アバターからシーンに光線を飛ばし、ワールドのコライダーやほかのプレイヤーに当たったかを調べる部品です。VRChat 公式の定義は "The number of VRCRaycast components on the avatar." で、訳は「アバターにある VRCRaycast コンポーネントの数」です。上限は PC が Excellent 1・Good 4・Medium 8・Poor 15、Android が 1・2・4・8 です。上限の値は、[VRChat の公式の表](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) と同じです。PC の Poor と Android の Poor は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表とも同じ値です。どのプラットフォームでも 1 アバターあたり 80 個の固定の上限があります。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Raycasts がすべて取り除かれます（原文: "All Raycasts removed"）。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。ツールの版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「レイキャストの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 15）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない VRCRaycast は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

VRCRaycast のページ（VRChat の公式ドキュメント）には、80 個の上限は FinalIK のコンポーネントと共有されていると書かれています。そして、FinalIK は、VRCRaycast で足りる場面では、一般に使わないほうがよいと書かれています（原文: "which should generally not be used in situations where VRCRaycast is enough to meet your needs."。英語の原文の要約）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「レイキャストの数」を確かめます（上限は PC の Poor が 15、Android の Poor が 8）。
2. 変更の前に、プロジェクトのバックアップを取り、各 VRCRaycast が何を調べているか（何に当てているか）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
3. アバターにある VRCRaycast を一覧にし、それぞれが何を調べているかを確かめます。
4. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 2 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
5. FinalIK を使っているなら、80 個の上限を FinalIK と分け合っている点を確かめます。VRCRaycast で足りる部分は VRCRaycast に寄せられるかを検討します（公式の注記に沿った、**この記事の助言**{.lbl .lbl-adv}です）。
6. Play モードに入り、当たり判定の動きが変わっていないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。AAO を使っているときは、ランクを Actual Performance Window で確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、レイキャストの数を確かめます。

## 注意・うまくいかないとき

::: warning
- **80 個の固定の上限**: 公式は、VRCRaycast のコンポーネントには、全プラットフォームで 1 アバターあたり 80 個の固定の上限があると書いています（原文: "VRCRaycast components have a hard upper limit of 80 components per avatar on all platforms."。訳: 全プラットフォームで 1 アバターあたり 80 個の固定の上限。[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) のランクの表の脚注）。
- **FinalIK と共有**: この上限は FinalIK のコンポーネントと共有されます（上の「減らし方の考え方」の記載）。
- **FinalIK と Android・Quest**: 同じ公式の Quest Content Limitations は、FinalIK も "Custom FinalIK components are completely disabled for avatars on Android and Quest."（訳: FinalIK のカスタムのコンポーネントは、Android と Quest のアバターで完全に無効）と書いています。
- **Player の層**: Player の層（ほかのプレイヤーのコライダー）と、ほかの層を 1 つ以上含むレイヤーのマスクを使うと、光線は 1 本ではなく、合成された 2 本として飛ばされます。ほかのプレイヤーに当てるには、VRCRaycast のバージョン 1.1 以降が必要です。
:::

## 関連ページ

- [オーディオソースの数を減らす](/learn/optimize-physics/audio-sources)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
