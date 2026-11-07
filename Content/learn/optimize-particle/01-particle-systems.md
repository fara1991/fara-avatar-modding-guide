---
title: パーティクルシステムの数を減らす
reading: ぱーてぃくるしすてむのかずをへらす
description: パーティクルシステムの数の上限と確かめ方、VRChat 公式の勧め（使うパーティクルシステムの数を制限する）、各ツールで公式に書かれていることを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- アバターにある Particle System の数の上限は、PC の Poor が 16、Android の Poor が 2
- 公式は無効にしたものも数えると書いており、使わないものは削除するのがこの記事の助言
- 公式の勧めは、使っているパーティクルシステムの数を制限すること
:::

パーティクルシステムの数は、アバターにある Particle System コンポーネント（パーティクルを出す部品）の数を数える項目です。VRChat 公式の定義は "The number of Particle System components on the avatar." です（訳: アバターにある Particle System コンポーネントの数）。上限は PC の Poor が 16、Android の Poor が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、ランクが次のランクに下がります。表示の下限（Minimum Displayed Performance Rank）に届かないと、公式の表示制限では All Particle Systems removed（パーティクルシステムがすべて取り除かれる）と書かれています。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは使えなくなることがあります。Android の既定の表示の下限は Medium で、Poor と Very Poor のアバターは既定では見えない、と書かれています（どちらも英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **この記事の助言**{.lbl .lbl-adv}: 公式の説明ではない、この記事の助言です。

この記事は、AAO・Modular Avatar（GitHub の main）・TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を、2026 年 11 月 6 日に確かめて書きました。版は、GitHub の最新リリースで 2026 年 11 月 7 日に確かめました。AAO は 1.9.21（changelog の最新版）、TexTransTool は v1.0.2（2026-06-26）、lilAvatarUtils は 2.1.2（2025-08-11）、Meshia Mesh Simplification は 3.3.1（2026-11-04）です。TexTransTool と lilAvatarUtils は、サイトに版の表示がありません。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルシステムの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、20（上限 16）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないパーティクルシステムは、無効にするだけでは数が減りません。

## 減らし方の考え方

VRChat 公式の勧めは、次のとおりです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

- 原文: "Limit the number of particle systems that you're using, and limit the maximum amount of particles emitting at any one given moment."
- 訳: 使っているパーティクルシステムの数を制限し、同時に放出される粒子の最大数も制限する。

後半の「同時に放出される粒子の最大数」は、[最大パーティクルの数を減らす](/learn/optimize-particle/max-particles) で扱います。

この記事の整理として、減らす方法を次の 2 つに分けます（公式の分類ではありません）。

1. 使っていないパーティクルシステムを削除する。
2. 残すものは、ツールの機能で数を減らせるかを確かめる。ただし、下のとおり、公式にパーティクルシステムの数を減らす機能の記載は見つかりませんでした。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント全件と CHANGELOG。「パーティクル」の語は、サンプル（Clap プレハブ）の説明に 1 件だけあり、仕様の記述ではありません）。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ttt.rs64.net の /docs/ 配下 81 ページ）。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版の公式ドキュメント 9 ページと、英語版のレンダラーのタブで、「パーティクル」の語を検索。見つかりませんでした）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、`.docfx` にある日本語・英語のドキュメント 11 件（目次を含めると 19 件））。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページ 72 件と CHANGELOG。docs に、パーティクルシステムの数を減らす機能の記載は無いことを確かめました）。CHANGELOG の記録は、減らす機能の記載ではありません（下の注意）。

「使われていないObjectを自動的に削除する」の原文は「アニメーションなどを走査して、使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します。」です。この原文では、パーティクルシステムは名指しされていません。CHANGELOG からは、削除の処理がパーティクルシステムに及ぶことがあると分かりますが、どの機能がどこまで削除するかは、**公式に記載なし**{.lbl .lbl-none}です。

::: warning
AAO の CHANGELOG（1.9.21 時点）には、パーティクル関連の主項目（箇条の先頭の項目）が 7 件あります。そのうち 6 件（1.9.19・1.8.8・1.7.13・1.7.5・1.7.1・1.6.0）は、パーティクルが壊れることの修正です。残る 1 件（1.8.10）は、パーティクルシステムが正しく削除されない不具合の修正で、削除の処理がパーティクルシステムに及ぶことを示します。どちらも、パーティクルを減らす効果の記載ではありません。

- 1.8.10（2025 年 5 月 4 日）: "Fixed Particle Systems are not correctly removed"（訳: パーティクルシステムが正しく削除されない、の修正）
- 1.6.0（2023 年 11 月 27 日）: "Remove Unused Object may break ParticleSystem"（訳: 未使用の Object の削除が ParticleSystem を壊すことがある、の修正）
:::

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルシステムの数」を確かめます（上限は PC の Poor が 16、Android の Poor が 2）。
2. アバターの中の Particle System コンポーネントを探し、使っていないものを選びます。使っているかどうかは、そのパーティクルを出す仕組み（アニメーションや Expression Menu など）を見て確かめます（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取り、確かめたい動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 使っていないと確かめたパーティクルシステムは、無効にするのではなく削除します。無効にするだけでは数に残るためです（上の「減らす前に確かめること」を参照）。
5. 残したものは、公式の勧めに沿って、同時に放出される粒子の最大数を見直します。詳しくは [最大パーティクルの数を減らす](/learn/optimize-particle/max-particles) を見てください。
6. 削除のあと、Build & Test や Play モードで、見た目と動きが変わっていないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
7. SDK の「パフォーマンスの内訳」で、「パーティクルシステムの数」が上限以下になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **無効にしただけでは数は減りません。** 使わないものは削除してください（上の手順 4）。
- **Android の上限は 2 です。** 公式の Android の制限のページ（Quest Content Limitations）は、"Particles are limited heavily on avatars for Android and Quest, with settings mirroring the Avatar Particle System Limits on PC." と書いています（訳: Android と Quest のアバターでは、パーティクルは大きく制限されています。PC の Avatar Particle System Limits に合わせた設定です）。ただし、パフォーマンスランクの表では、モバイルの値（2）は PC の値（16）と違います。そのため、「Android の上限が PC と同じ」とは読めません。
- **表示制限では、パーティクルシステムの全部が取り除かれます。** 表示の下限に届かないときは、All Particle Systems removed と書かれていて、取り除かれるのは一部ではなく、パーティクルシステムの全部です（英語の原文の要約）。
- **マテリアルスロットの数の内訳。** 公式の補足では、パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠のマテリアルスロットを使うと書かれています。数を減らすときは、マテリアルスロットの数も SDK の「パフォーマンスの内訳」で確かめてください（**この記事の助言**{.lbl .lbl-adv}。マテリアルスロットの減らし方は [マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots) を見てください）。
- **Mesh Read/Write に注意してください。** 公式は、パーティクルシステムのメッシュを含め、アバターのいずれかの mesh で Mesh Read/Write を無効にすると、パフォーマンスランクが即座に Very Poor になると書いています（英語の原文の要約。原文: "including particle systems"）。
:::

## 関連ページ

- [最大パーティクルの数を減らす](/learn/optimize-particle/max-particles)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
