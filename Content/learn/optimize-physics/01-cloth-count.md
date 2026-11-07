---
title: Cloth の数を減らす
reading: くろーすのかずをへらす
description: Cloth コンポーネントの数の上限と確かめ方、VRChat 公式の勧め（Cloth の使用を強く控える）、AAO での扱いをまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 1 で、モバイル（Android と iOS）では無効
- 公式は、無効にしたものも数えると書いている
:::

Cloth の数は、アバターにある Cloth コンポーネント（布の動きを物理で計算する部品）の数を数える項目です。VRChat 公式の定義は "The total number of Cloth components on the avatar." で、訳は「アバターにある Cloth コンポーネントの総数」です。SDK の「パフォーマンスの内訳」では「CLOTH メッシュの数」と表示されますが、公式の定義は Cloth コンポーネントの数なので、この記事もその定義に合わせて書きます。上限は PC の Poor が 1 で、モバイル（Android と iOS）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

公式の Mobile Removed Components（モバイルで取り除かれるコンポーネントの一覧）には、Cloths（Cloth コンポーネントの数）が入っています。原文は "The following components are disabled on mobile devices since they can never appear on avatars:"（訳: 次のコンポーネントは、アバターに表示されることが決してないため、モバイルでは無効になります）です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表では All Cloth components removed（Cloth コンポーネントがすべて取り除かれる）と書かれています。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります（英語の原文の要約です）。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。ツールの版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「CLOTH メッシュの数」の行で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 1）と表示されていました。
- **表から読めること（この記事の読み方）**: PC の表では、Excellent の上限が 0 です。そのため、Cloth コンポーネントの数が 0 を超えると Excellent にはなりません。Good・Medium・Poor の上限は、いずれも 1 です。2 つ以上あると Poor の上限を超えるため、次のランク（Very Poor）になります。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない Cloth は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめる方法が案内されています（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。

## 減らし方の考え方

VRChat 公式の勧めは、次のとおりです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)の「Limit usage of Cloth」の節）。

- 原文: "Cloth is a default Unity component that has a similar cost to Dynamic Bones and is more difficult to set up. Limit your use of Cloth heavily, and do not apply it to meshes that have greater than 200 or so vertices."
- 訳: Cloth は Unity の標準のコンポーネントで、コストは Dynamic Bones と同程度で、設定も難しい。Cloth の使用は強く控えてください。また、200 頂点程度より多いメッシュには使わないでください。

この記事の整理として、残す Cloth は、公式の勧めに沿って使う場所を見直します。頂点数の勧めは、[Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) で扱います。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO の変更履歴（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）には、1.7.0（2024 年 4 月 30 日、Changed（仕様の変更））の記録があります。Merge Skinned Mesh（複数のメッシュを 1 つにまとめる機能）で、Cloth コンポーネントのある SkinnedMeshRenderer（ボーンで変形するメッシュの部品）を統合元にすると、エラーにする仕様になりました。原文は "Since this version, AAO will make an error if the source Skinned Mesh Renderer is with Cloth component." です（訳: この版から、統合元の SkinnedMeshRenderer に Cloth コンポーネントがあるとエラーにします）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「CLOTH メッシュの数」（公式の定義は Cloth コンポーネントの数）の値と上限を確かめます。PC の上限は Poor が 1 で、モバイル（Android と iOS）では無効です。
2. アバターの中から、Cloth コンポーネントが付いているオブジェクトを探します。そのうえで、布の動きがアバターの見せ方に必要かを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取り、確かめたい布の動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 200 頂点程度より多いメッシュに付く Cloth は、外すかを検討します（[Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) の手順 3 と同じ基準）。
5. 使っていないと確かめたものは、無効にするのではなく取り除きます（無効のままでも数に入るため）。判断できないものは残し、手順 3 で記録した動きと比べます（**この記事の助言**{.lbl .lbl-adv}）。
6. Play モードに入って、残した Cloth の動きが、記録したものと同じかを確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、値を確かめます。AAO を使っているときは、Play モードに入ったときのランクを Actual Performance Window で確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest では Cloth は使えません**: 公式の制限のページは、「Cloth | Completely disabled on Android and Quest.」（訳: Cloth は Android と Quest で完全に無効）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。Mobile Removed Components の一覧の値について、公式は「These values may still appear in VRChat's avatar details screen, but they are always zero.」（訳: これらの値は VRChat のアバターの詳細画面に出ることがあるが、常にゼロです）と書いています。
- **無効にしただけでは数は減りません**: 無効にしたものも数えられるという公式の書き方は、上の「減らす前に確かめること」を参照してください。
- **AAO の Cloth の記録（1.7.0）**: 上の AAO の節のとおり、1.7.0 から、Cloth のある SkinnedMeshRenderer を Merge Skinned Mesh の統合元にするとエラーにする、と CHANGELOG に記録されています。
:::

## 関連ページ

- [Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
