---
title: パーティクルの衝突を使わないようにする
reading: ぱーてぃくるのしょうとつをつかわないようにする
description: パーティクルの衝突（Particle Collision）が、VRChat のパフォーマンスランクでどう数えられるか（True / False の項目）、上限と確かめ方、おすすめの減らし方、衝突をオフにする手順をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 衝突は数ではなく True か False で、1 つでも Particle Collision が有効なら True
- 上限の表記は True / True（PC の Medium・Android の Poor は表からの読み取り）
- 大量の粒子を性能を保って使う方法の一つとして、衝突を使わないのがおすすめです
:::

パーティクルの衝突は、パーティクルシステムの粒子が、ほかの物とぶつかるかどうかを計算する設定（Particle Collision）です。パフォーマンスランクでは数字ではなく、True（あり）か False（なし）で数えます。定義は、アバターのパーティクルシステムのどれか 1 つでも Particle Collision が有効なら、True になるというものです（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表では「True / True」です。パフォーマンスランクの表（上と同じページ）では、True のとき PC は Medium、Android は Poor の列に当たります（表からの読み取りです）。つまり、PC では Good 以上にならず、Android では Medium 以上にならない、という意味です。

上限を超えると、次のランクになります。表示の制限（Minimum Displayed Performance Rank）の対象になると、All Particle Systems removed（パーティクルシステムがすべて取り除かれる）になります。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルの衝突」を見ます。この項目は「FALSE/あり」と表示されます。この記事の例の画面では、FALSE（緑）でした。読み方は [パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の「FALSE/あり」の節を見てください。
- **AAO を使うとき**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードの Actual Performance Window で確かめます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方

大量のパーティクルを使いながら性能を保つには、スプライトのパーティクルのダイナミックバッチングを調べ、衝突（collision）は使わず、パーティクルの動きをシンプルにするのがおすすめです（[Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

衝突を使わないことは、大量のパーティクルを性能を保って使うための一つの方法です。この記事の整理として、衝突が要らないパーティクルシステムは、衝突の設定をオフにします。すべてのパーティクルシステムでオフになると、この項目は False になります（定義のとおり）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルの衝突」が FALSE か「あり」かを確かめます。
2. 衝突（Particle Collision）が有効なパーティクルシステムを探して、衝突が要るかを確かめます。
3. 変更の前に、プロジェクトのバックアップを取り、今の粒子の見た目と動きを記録します（**この記事の助言**{.lbl .lbl-adv}）。
4. 衝突が要らないパーティクルシステムの、衝突（Particle Collision）の設定をオフにします。
5. 変更のあと、粒子の見た目と動きを、手順 3 の記録と比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「パーティクルの衝突」が FALSE になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- 衝突をオフにすると、粒子の動きや当たり方が変わります。
- 「パーティクルの衝突」は、PhysBone のコライダー（「PHYSBONE コライダーの数」）とは別の項目です。
- Avatar Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があります（[Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。
:::

## 関連ページ

- [トレイルレンダラーの数を減らす](/learn/optimize-particle/trail-renderers)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
