---
title: 最大パーティクルの数を減らす
reading: さいだいぱーてぃくるのかずをへらす
description: 最大パーティクルの数（ランキングの表での行名は Total Particles Active）の上限と確かめ方、おすすめの減らし方（同時に放出される粒子の数を制限する）、使わないパーティクルシステムを削除する手順をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 最大パーティクルの数は、各 Particle System の maxParticles の合計で、放出されている数や画面に出ている数とは別の値
- 上限は、PC の Poor が 2,500、Android の Poor が 200
- 同時に放出される粒子の最大数を制限するのがおすすめです
:::

最大パーティクルの数は、アバターのすべての Particle System の maxParticles（最大粒子数）の合計を数える項目です。VRChat のランキングの表では、この項目の行名は Total Particles Active です。定義は、アバターのすべてのパーティクルシステムの maxParticles の合計です。上限は PC の Poor が 2,500、Android の Poor が 200 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、ランクが次のランクに下がります。表示の下限（Minimum Displayed Performance Rank）に届かないと、All Particle Systems removed（パーティクルシステムがすべて取り除かれる）になります。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは使えなくなることがあります。Android の既定の表示の下限は Medium で、Poor と Very Poor のアバターは既定では見えません。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「最大パーティクルの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、622,022（上限 2,500）と表示されていました。
- **合計の出し方**: 定義どおり、アバターの各 Particle System の maxParticles を足し合わせると、どれが大きいかを見つけられます（**この記事の助言**{.lbl .lbl-adv}）。
- **行名の Active について**: 行名は Active を含みますが、定義はアバターのすべての Particle System を対象にしています。無効のものも含めて数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **AAO を使うときのランク**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなります。Play モードに入ったときのランクは、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」の方法で確かめます。

## 減らし方の考え方

減らすには、使っているパーティクルシステムの数を制限し、同時に放出される粒子の最大数も制限するのがおすすめです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。粒子の多いパーティクルシステムを使いつつ性能を保つには、スプライトのパーティクルのダイナミックバッチングを調べ、衝突（collision）を使わず、粒子の動きを単純にするとよいです。

一般には、大きな半透明の粒子は、小さく不透明な粒子を多く使うより負荷が大きくなります。Unity のパーティクルシステムは、うまく使えば十分に最適化されていて、速く動きます（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

「同時に放出される粒子の最大数」は、この記事では maxParticles の合計を下げる手がかりとして扱います。

この記事の整理として、減らす方法を次の 2 つに分けます。

1. 同時に放出される粒子の最大数を制限する。
2. 使っていないパーティクルシステムを削除する。合計から、その分の maxParticles が減ります（[パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems) の手順）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「最大パーティクルの数」を確かめます（上限は PC の Poor が 2,500、Android の Poor が 200）。
2. アバターの各 Particle System の maxParticles を見て、合計を出します。大きいものから順に見ると、見直す場所を決めやすくなります（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取ります。見た目と動きは、変更の前に記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 使っていないと確かめたパーティクルシステムは、無効にするのではなく削除します（[パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems) の手順 4）。
5. 残したパーティクルシステムは、同時に放出される粒子の最大数を見直します。数を下げると、粒子の見え方が変わることがあるので、記録したものと比べます（**この記事の助言**{.lbl .lbl-adv}）。
6. Build & Test や Play モードで、見た目と動きが変わっていないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
7. SDK の「パフォーマンスの内訳」で、「最大パーティクルの数」が上限以下になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **定義は maxParticles の合計です。** 画面に出ている粒子の数や放出数とは別の値です。
- **Android の上限は 200 です。** Android と Quest のアバターでは、パーティクルは大きく制限されています。その設定は PC の Avatar Particle System Limits に合わせてあります（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。ただし、パフォーマンスランクの表では、モバイルの値（200）は PC の値（2,500）と違います。
- **表示制限では、パーティクルシステムの全部が取り除かれます。** 表示の下限に届かないと、All Particle Systems removed になります。
- **メッシュパーティクルの三角ポリゴンは別の項目です。** ランキングの表では Mesh Particle Active Polys という別の行で、この記事では扱いません。
:::

## 関連ページ

- [パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
