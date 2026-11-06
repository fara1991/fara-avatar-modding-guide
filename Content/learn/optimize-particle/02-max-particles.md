---
title: 最大パーティクルの数を減らす
reading: さいだいぱーてぃくるのかずをへらす
description: 最大パーティクルの数（公式の行名は Total Particles Active）の上限と確かめ方、VRChat 公式の勧め（同時に放出される粒子の数を制限する）、各ツールで公式に書かれていることを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-06
---

最大パーティクルの数は、アバターのすべての Particle System の maxParticles（最大粒子数）の合計を数える項目です。VRChat 公式のランキングの表では、この項目の行名は Total Particles Active です。定義は "The sum of maxParticles across all particle systems on the avatar." で、訳は「アバターのすべてのパーティクルシステムの maxParticles の合計」です。上限は PC の Poor が 2,500、Android の Poor が 200 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、ランクが次のランクに下がります。表示の下限（Minimum Displayed Performance Rank）に届かないと、公式の表示制限では All Particle Systems removed（パーティクルシステムがすべて取り除かれる）と書かれています（英語の原文の要約）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは使えなくなることがあります（英語の原文の要約。原文: "the default Minimum Displayed Performance Rank level is "Very Poor". This means that users can see most avatars by default. … However, if your avatar is extremely unoptimized, VRChat may prevent you from using it."）。Android の既定の表示の下限は Medium で、Poor と Very Poor のアバターは既定では見えない、と書かれています（英語の原文の要約）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21（changelog の最新版）、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、lilAvatarUtils・TexTransTool・Meshia Mesh Simplification（版番号は未確認。取得時点の公開サイトまたは GitHub の main）の公式の資料を、2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「最大パーティクルの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、622,022（上限 2,500）と表示されていました。
- **合計の出し方**: 定義どおり、アバターの各 Particle System の maxParticles を足し合わせると、どれが大きいかを見つけられます（この記事の助言）。足した値が SDK の表示と一致するかは、この記事では未確認です。
- **行名の Active について**: 行名には Active とありますが、定義は「アバターのすべての Particle System」を対象にしていて、有効・無効の区別は書かれていません。また、公式は、現在無効化されているものを含めて、すべての GameObject とコンポーネントがパフォーマンスランクに数えられると書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **AAO を使うときのランク**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」の方法で確かめます。

## 減らし方の考え方

VRChat 公式の勧めは、次のとおりです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

- 原文: "Limit the number of particle systems that you're using, and limit the maximum amount of particles emitting at any one given moment."
- 訳: 使っているパーティクルシステムの数を制限し、同時に放出される粒子の最大数も制限する。
- 原文: "There are ways to have particle systems with large numbers of particles and retain performance. If you are interested in this, look into dynamic batching for sprite particles, don't use collision, and ensure the movement of your particles is simplistic."
- 訳: 粒子の多いパーティクルシステムを使いつつ性能を保つ方法はあります。興味があれば、スプライトのパーティクルのダイナミックバッチングを調べ、衝突（collision）を使わず、粒子の動きを単純にしてください。
- 原文: "Generally speaking, large transparent particles are worse than a lot of smaller, opaque ones. Unity's Particle System is actually quite optimized and runs quickly *if used well.*"
- 訳: 一般には、大きな半透明の粒子は、小さく不透明な粒子を多く使うより負荷が大きくなります。Unity のパーティクルシステムは、うまく使えば十分に最適化されていて、速く動きます。

公式の原文は maxParticles という語を使っていません。「同時に放出される粒子の最大数」は、この項目（maxParticles の合計）と近い考え方なので、この記事では減らす対象として扱います（この記事の整理）。

この記事の整理として、減らす方法を次の 2 つに分けます（公式の分類ではありません）。

1. 同時に放出される粒子の最大数を制限する（公式の勧め）。
2. 使っていないパーティクルシステムを削除する。合計から、その分の maxParticles が減ります（[パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems) の手順）。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: MA の日本語版と英語版のドキュメント全件と CHANGELOG。「パーティクル」の語は、サンプル（Clap プレハブ）の説明に 1 件だけあり、仕様の記述ではありません）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: sitemap に載る公式サイトのドキュメント 81 ページ。目次のページは取得できなかったため、この 81 ページで確かめました）。

### lilAvatarUtils

公式に記載なし（確かめた範囲: 日本語版の公式ドキュメント 9 ページと、英語版のレンダラーのタブで、「パーティクル」の語を検索。見つかりませんでした）。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、`.docfx` にある日本語・英語のドキュメント 11 件（目次を含めると 19 件）。公開サイト（ramtype0.github.io）は未確認）。MMS は、ポリゴン数を減らすツールとして説明されています。

### Avatar Optimizer（AAO）

公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページ 72 件と CHANGELOG）。

CHANGELOG のパーティクル関連の項目は 7 件あり、そのうち 6 件は、パーティクルが壊れることの修正です（1.6.0 の "Remove Unused Object may break ParticleSystem" など）。残る 1 件（1.8.10）は、パーティクルシステムが正しく削除されない不具合の修正で、削除の処理がパーティクルシステムに及ぶことを示します。どちらも、パーティクルを減らす効果の記載ではありません。詳しくは [パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems) の AAO の項を見てください。

## 手順

1. SDK の「パフォーマンスの内訳」で、「最大パーティクルの数」を確かめます（上限は PC の Poor が 2,500、Android の Poor が 200）。
2. アバターの各 Particle System の maxParticles を見て、合計を出します。大きいものから順に見ると、見直す場所を決めやすくなります（この記事の助言）。
3. 変更の前に、プロジェクトのバックアップを取ります。見た目と動きは、変更の前に記録しておきます（この記事の助言）。
4. 使っていないと確かめたパーティクルシステムは、無効にするのではなく削除します（[パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems) の手順 4）。
5. 残したパーティクルシステムは、この記事の整理として、同時に放出される粒子の最大数を見直します。数を下げると、粒子の見え方が変わることがあるので、記録したものと比べます（この記事の助言）。
6. Build & Test や Play モードで、見た目と動きが変わっていないかを確かめます（この記事の助言）。
7. SDK の「パフォーマンスの内訳」で、「最大パーティクルの数」が上限以下になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **定義は、放出されている数ではありません。** 公式の定義は maxParticles の合計です。実際に画面に出ている粒子の数とは別の値です。
- **Android の上限は 200 です。** 公式の Android の制限のページ（Quest Content Limitations）は、"Particles are limited heavily on avatars for Android and Quest, with settings mirroring the Avatar Particle System Limits on PC." と書いています（訳: Android と Quest のアバターでは、パーティクルは大きく制限されています。PC の Avatar Particle System Limits に合わせた設定です）。ただし、パフォーマンスランクの表では、モバイルの値（200）は PC の値（2,500）と違います。そのため、「Android の上限が PC と同じ」とは読めません。
- **表示制限では、パーティクルシステムの全部が取り除かれます。** 表示の下限に届かないときは、All Particle Systems removed と書かれています（英語の原文の要約）。
- **メッシュパーティクルの三角ポリゴンは別の項目です。** 公式のランキングの表では Mesh Particle Active Polys という別の行で、この記事では扱いません。
:::

## 関連ページ

- [パーティクルシステムの数を減らす](/learn/optimize-particle/particle-systems)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
