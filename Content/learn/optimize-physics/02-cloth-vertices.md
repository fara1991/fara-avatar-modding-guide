---
title: Cloth の最大頂点の確かめ方
reading: くろーすのさいだいちょうてんのたしかめかた
description: Cloth の最大頂点（Total Cloth Vertices）の上限と確かめ方、VRChat 公式の勧め（200 頂点程度より多いメッシュには Cloth を使わない）、各ツールの公式の資料で確かめた範囲をまとめます。
difficulty: 中級
updated: 2026-11-06
---

Cloth の最大頂点は、SDK の「パフォーマンスの内訳」で「CLOTH の最大頂点の数」と表示される項目です。公式の項目名は Total Cloth Vertices です。ただし、公式の統計表（Avatar Performance Ranking Stats）には、この項目の定義の行を確認できませんでした。そのため、何を数えるか（「最大」の意味を含む）は、この記事では未確認です。上限は PC の Poor が 200 で、Android（モバイル）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。PC の Excellent は 0、Good は 50、Medium は 100 で、これらは [VRChat の公式の表（GitHub の原本の avatar-performance-ranking-system.md）](https://raw.githubusercontent.com/vrchat-community/creator-docs/main/Docs/docs/avatars/avatar-performance-ranking-system.md) の値です。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表では All Cloth components removed（Cloth コンポーネントがすべて取り除かれる）と書かれています。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21（changelog の最新版）、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を、2026 年 11 月 6 日に確かめて書きました。TexTransTool と lilAvatarUtils は、サイトに版の表示は無い。GitHub の最新リリースは TexTransTool v1.0.2（2026-06-26）、lilAvatarUtils 2.1.2（2025-08-11）（2026-11-06 時点）。Meshia Mesh Simplification の公開サイト（ramtype0.github.io）は、トップページを確認。サブページは未確認。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 数の確かめ方

- **SDK の「パフォーマンスの内訳」**: 「CLOTH の最大頂点の数」の行で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 200）と表示されていました。
- **表から読めること（この記事の読み方）**: VRChat の公式の表（PC の値）では、Excellent の上限が 0 です。そのため、値が 0 を超えると Excellent にはなりません。Good の上限は 50、Medium は 100、Poor は 200 です。200 を超えると Poor の上限を超えるため、次のランク（Very Poor）になります。
- **無効にしたものが値に入るかは、未確認です。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "All GameObjects and Components, including those that are currently disabled, count towards the Avatar Performance Rank."）と書いています。ただし、この項目の定義の行を確認できないため、この項目の値に当てはまるかは、この記事では分かりません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめる方法が案内されています（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。

## 公式の勧めの読み方

VRChat 公式の Tips（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）の「Limit usage of Cloth」の節には、次のように書かれています。

- 原文: "Cloth is a default Unity component that has a similar cost to Dynamic Bones and is more difficult to set up. Limit your use of Cloth heavily, and do not apply it to meshes that have greater than 200 or so vertices."
- 訳: Cloth は Unity の標準のコンポーネントで、コストは Dynamic Bones と同程度で、設定も難しい。Cloth の使用は強く控えてください。また、200 頂点程度より多いメッシュには使わないでください。

この記事の読み方を、次のとおり分けて書きます。

- 勧めの 200 は、1 つのメッシュの頂点数についての目安と読めます（原文は "meshes" です）。
- 限度表の項目名に Total とありますが、統計表に定義の行は確認できません。そのため、この 2 つが同じ量を数えるかは、未確認です。

そのため、この記事の整理として、減らす方法を次のように考えます（公式の分類ではありません）。

1. 200 頂点程度より多いメッシュに付いている Cloth を、外すかを検討する。
2. 残す Cloth について、頂点数を減らす方法は、この記事では未確認です。5 つのツールの公式の資料には記載がありません（下の各節）。

この記事の 1 は、公式の勧めの読み方です。Cloth の数そのものを減らす話は、[Cloth の数を減らす](/learn/optimize-physics/cloth-count) で扱います。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: MA の日本語版と英語版のドキュメント 136 ファイル（changelog のページを含む）、CHANGELOG 4 種、README）。Cloth の語は衣装の手順に出てきますが、頂点数や上限の記述はありません。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: 日本語版の公式ドキュメント 81 ページ（sitemap の /docs 配下）。Cloth の語は 1 件もありませんでした）。英語版は未確認です。

### lilAvatarUtils

公式に記載なし（確かめた範囲: 日本語版の公式ドキュメント 11 ページ（トップ・一覧・各タブ・ツール））。Cloth の語は見つかりませんでした。英語版は未確認です。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub の README（英語・日本語）、`.docfx` の日本語・英語のドキュメント。公開サイトは、トップページを確認。サブページは未確認）。Cloth の語は見つかりませんでした。

### Avatar Optimizer（AAO）

公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG）。Cloth の頂点数を減らす機能は書かれていません。CHANGELOG には頂点（vertices）に関する記録は多くありますが、Cloth の頂点数に関する記録は無いことを確かめました。AAO の Cloth の記録（2 件。どちらも 1.7.0 の Changed）は、[Cloth の数を減らす](/learn/optimize-physics/cloth-count) を見てください。

## 手順

1. SDK の「パフォーマンスの内訳」で、「CLOTH の最大頂点の数」の値と上限を確かめます。PC の上限は Poor が 200 で、Android では無効です。
2. アバターの中から、Cloth コンポーネントが付いているメッシュを探します。そのメッシュの頂点数を確かめる方法は、この記事では未確認です。
3. 公式の勧めの読み方（上の「減らし方の考え方」の 1）に沿って、200 頂点程度より多いメッシュに付いている Cloth を、外すかを検討します。
4. 変更の前に、プロジェクトのバックアップを取り、確かめたい布の動きを記録しておきます（この記事の助言）。
5. 外すと決めた Cloth は、無効にするのではなく取り除きます。無効にしただけで値が下がるかは未確認です（この記事の助言）。取り除いたあとに動きが変わったときは、バックアップから元に戻してください。
6. Play モードに入って、残した Cloth の動きが、記録したものと同じかを確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で値を確かめます。AAO を使っているときは、Play モードに入ったときのランクを Actual Performance Window で確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest では Cloth は使えません**: 公式の制限のページは、「Cloth | Completely disabled on Android and Quest.」（訳: Cloth は Android と Quest で完全に無効）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。Mobile Removed Components の一覧には、Total Cloth Vertices も入っています（値は常にゼロ）。
- **定義は未確認です**: Total Cloth Vertices が何を数えるか、「最大」が何を意味するかは、この記事では確認できていません。
- **AAO の Cloth の記録（1.7.0）**: 1.7.0 から、Cloth のある SkinnedMeshRenderer を Merge Skinned Mesh の統合元にするとエラーにする、と CHANGELOG に記録されています。現行の 1.9.21 での挙動は、この記事では未確認です。
:::

## 関連ページ

- [Cloth の数を減らす](/learn/optimize-physics/cloth-count)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
