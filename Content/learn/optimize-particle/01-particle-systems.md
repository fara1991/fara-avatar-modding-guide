---
title: パーティクルシステムの数を減らす
reading: ぱーてぃくるしすてむのかずをへらす
description: パーティクルシステムの数の上限と確かめ方、おすすめの減らし方（使うパーティクルシステムの数を制限する）、使わないものを削除する手順をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- アバターにある Particle System の数の上限は、PC の Poor が 16、Android の Poor が 2
- 無効にしたものも数えられるため、使わないものは削除します
- 使っているパーティクルシステムの数を制限するのがおすすめです
:::

パーティクルシステムの数は、アバターにある Particle System コンポーネント（パーティクルを出す部品）の数を数える項目です。上限は PC の Poor が 16、Android の Poor が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、ランクが次のランクに下がります。表示の下限（Minimum Displayed Performance Rank）に届かないと、All Particle Systems removed（パーティクルシステムがすべて取り除かれる）になります。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは使えなくなることがあります。Android の既定の表示の下限は Medium で、Poor と Very Poor のアバターは既定では見えません。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルシステムの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、20（上限 16）と表示されていました。
- **無効にしたものも数えられます。** 現在無効化されているものを含め、すべての GameObject とコンポーネントがパフォーマンスランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないパーティクルシステムは、無効にするだけでは数が減りません。

## 減らし方の考え方

使っているパーティクルシステムの数を制限し、同時に放出される粒子の最大数も制限するのがおすすめです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。後半の「同時に放出される粒子の最大数」は、[最大パーティクルの数を減らす](/learn/optimize-particle/max-particles) で扱います。

この記事の整理として、減らす方法を次の 2 つに分けます。

1. 使っていないパーティクルシステムを削除する。
2. 残すものは、同時に放出される粒子の最大数を見直します（次の記事）。AAO を使うときの注意は、下の節にあります。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO の「使われていないObjectを自動的に削除する」は、アニメーションなどを走査して、使われていない Object（GameObject やコンポーネント、テクスチャなど）を自動的に削除します。削除の処理は、パーティクルシステムに及ぶことがあります。

::: warning
[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/) では、パーティクルシステムの削除に関わる修正が、次の 2 つの版にあります。

- AAO 1.8.10（2025 年 5 月 4 日）では、パーティクルシステムが正しく削除されない不具合が修正されました。
- AAO 1.6.0（2023 年 11 月 27 日）では、未使用の Object の削除が ParticleSystem を壊すことがある不具合が修正されました。
:::

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルシステムの数」を確かめます（上限は PC の Poor が 16、Android の Poor が 2）。
2. アバターの中の Particle System コンポーネントを探し、使っていないものを選びます。使っているかどうかは、そのパーティクルを出す仕組み（アニメーションや Expression Menu など）を見て確かめます（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取り、確かめたい動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 使っていないと確かめたパーティクルシステムは、無効にするのではなく削除します。無効にするだけでは数に残るためです（上の「減らす前に確かめること」を参照）。
5. 残したものは、同時に放出される粒子の最大数を見直します。詳しくは [最大パーティクルの数を減らす](/learn/optimize-particle/max-particles) を見てください。
6. 削除のあと、Build & Test や Play モードで、見た目と動きが変わっていないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
7. SDK の「パフォーマンスの内訳」で、「パーティクルシステムの数」が上限以下になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **無効にしただけでは数は減りません。** 使わないものは削除してください（上の手順 4）。
- **Android の上限は 2 です。** Android と Quest のアバターでは、パーティクルは大きく制限されています。その設定は PC の Avatar Particle System Limits に合わせてあります（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。ただし、パフォーマンスランクの表では、モバイルの値（2）は PC の値（16）と違います。
- **表示制限では、パーティクルシステムの全部が取り除かれます。** 表示の下限に届かないと、All Particle Systems removed になります。
- **マテリアルスロット。** パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠のマテリアルスロットを使います（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。数を減らすときは、マテリアルスロットの数も SDK の「パフォーマンスの内訳」で確かめてください（**この記事の助言**{.lbl .lbl-adv}。マテリアルスロットの減らし方は [マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots) を見てください）。
- **Mesh Read/Write に注意してください。** パーティクルシステムのメッシュを含め、アバターのいずれかの mesh で Mesh Read/Write を無効にすると、パフォーマンスランクは即座に Very Poor になります。
:::

## 関連ページ

- [最大パーティクルの数を減らす](/learn/optimize-particle/max-particles)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
