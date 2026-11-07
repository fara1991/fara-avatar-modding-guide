---
title: Cloth の数を減らす
reading: くろーすのかずをへらす
description: Cloth コンポーネントの数の上限と確かめ方、VRChat 公式の勧め（Cloth の使用を強く控える）、各ツールで確かめた範囲を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 1 で、Android（モバイル）では無効
- 公式は、無効にしたものも数えると書いている
- 使っているかを確かめる方法は未確認のため、取り除く操作は勧めない
:::

Cloth の数は、アバターにある Cloth コンポーネント（布の動きを物理で計算する部品）の数を数える項目です。VRChat 公式の定義は "The total number of Cloth components on the avatar." で、訳は「アバターにある Cloth コンポーネントの総数」です。SDK の「パフォーマンスの内訳」では「CLOTH メッシュの数」と表示されますが、公式の定義は Cloth コンポーネントの数なので、この記事もその定義に合わせて書きます。上限は PC の Poor が 1 で、Android（モバイル）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

公式の Mobile Removed Components（モバイルで取り除かれるコンポーネントの一覧）には、Cloths（Cloth コンポーネントの数）が入っています。原文は "The following components are disabled on mobile devices since they can never appear on avatars:"（訳: 次のコンポーネントは、アバターに表示されることが決してないため、モバイルでは無効になります）です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表では All Cloth components removed（Cloth コンポーネントがすべて取り除かれる）と書かれています。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21（changelog の最新版）、Modular Avatar（GitHub の main。正式版 1.18.7 との差は**未確認**{.lbl .lbl-unk}）、TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を、2026 年 11 月 6 日に確かめて書きました。TexTransTool と lilAvatarUtils は、サイトに版の表示は無い。GitHub の最新リリースは TexTransTool v1.0.2（2026-06-26）、lilAvatarUtils 2.1.2（2025-08-11）（2026-11-06 時点）。Meshia Mesh Simplification の公開サイト（ramtype0.github.io）は、トップページを確認。サブページは**未確認**{.lbl .lbl-unk}。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「CLOTH メッシュの数」の行で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 1）と表示されていました。
- **表から読めること（この記事の読み方）**: PC の表では、Excellent の上限が 0 です。そのため、Cloth コンポーネントの数が 0 を超えると Excellent にはなりません。Good・Medium・Poor の上限は、いずれも 1 です。2 つ以上あると Poor の上限を超えるため、次のランク（Very Poor）になります。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない Cloth は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめる方法が案内されています（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。

## 減らし方の考え方

VRChat 公式の勧めは、次のとおりです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)の「Limit usage of Cloth」の節）。

- 原文: "Cloth is a default Unity component that has a similar cost to Dynamic Bones and is more difficult to set up. Limit your use of Cloth heavily, and do not apply it to meshes that have greater than 200 or so vertices."
- 訳: Cloth は Unity の標準のコンポーネントで、コストは Dynamic Bones と同程度で、設定も難しい。Cloth の使用は強く控えてください。また、200 頂点程度より多いメッシュには使わないでください。

この記事の整理として、減らす方法を次の 2 つに分けます（公式の分類ではありません）。

1. 使っているかを確かめる方法は、この記事では**未確認**{.lbl .lbl-unk}です。そのため、取り除く操作は勧めません。使っているか判断できないものは残します。
2. 残す Cloth は、公式の勧めに沿って、使う場所を見直す。頂点数の勧めは、[Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) で扱います。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント 136 ファイル（changelog のページを含む）、CHANGELOG 4 種、README）。Cloth の語は、衣装の手順に出てきます。手順には「Clothをオフにすればこの衣装の設定を完全に自動化することもできます。」（原文（ja））とありますが、数を減らす機能の記述ではありません。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版の公式ドキュメント 81 ページ（sitemap の /docs 配下）。Cloth の語は 1 件もありませんでした）。英語版は**未確認**{.lbl .lbl-unk}です。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版の公式ドキュメント 11 ページ（トップ・一覧・各タブ・ツール））。Cloth の語は見つかりませんでした。英語版は**未確認**{.lbl .lbl-unk}です。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub の README（英語・日本語）、`.docfx` の日本語・英語のドキュメント。公開サイトは、トップページを確認。サブページは**未確認**{.lbl .lbl-unk}）。Cloth の語は見つかりませんでした。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG）。Cloth の数を減らす機能は書かれていません。CHANGELOG の Cloth の記録は 2 件あり、どちらも 1.7.0（2024 年 4 月 30 日）の Changed（仕様の変更）です。減らす効果の記録ではありません。

- 1 件目（[AAO の CHANGELOG](https://github.com/anatawa12/AvatarOptimizer/blob/master/CHANGELOG.md) の 1.7.0。PR は 949）: Merge Skinned Mesh（複数のメッシュを 1 つにまとめる機能）で、Cloth コンポーネントのある SkinnedMeshRenderer（ボーンで変形するメッシュの部品）を統合元にすると、エラーにする仕様になりました。原文は "Since this version, AAO will make an error if the source Skinned Mesh Renderer is with Cloth component." です（訳: この版から、統合元の SkinnedMeshRenderer に Cloth コンポーネントがあるとエラーにします）。以前の版では、バグによって元の SkinnedMeshRenderer を残していた、と書かれています。
- 2 件目（同じ 1.7.0。PR は 1029）: Cloth コンポーネントのある SkinnedMeshRenderer では、BlendShape（表情などでメッシュの形を変える仕組み）の自動の固定（AutoFreezeBlendShape）を行わないことにしました。報告によると、自動の固定で一部のポリゴンをサイズゼロにすると、アバターの初期化が非常に重くなったためです（英語の原文の要約です）。

現行の 1.9.21 で、これらの挙動が同じかは、この記事では**未確認**{.lbl .lbl-unk}です。

## 手順

1. SDK の「パフォーマンスの内訳」で、「CLOTH メッシュの数」（公式の定義は Cloth コンポーネントの数）の値と上限を確かめます。PC の上限は Poor が 1 で、Android では無効です。
2. アバターの中から、Cloth コンポーネントが付いているオブジェクトを探します。そのうえで、布の動きがアバターの見せ方に必要かを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
3. 変更の前に、プロジェクトのバックアップを取り、確かめたい布の動きを記録しておきます（**この記事の助言**{.lbl .lbl-adv}）。
4. 使っているかを確かめる方法は、この記事では**未確認**{.lbl .lbl-unk}です。そのため、取り除く操作は勧めません。使っているか判断できないものは残し、手順 3 で記録した布の動きと比べてください（**この記事の助言**{.lbl .lbl-adv}）。
5. 残す Cloth は、公式の勧めに沿って、使う場所を見直します（頂点数の勧めは [Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices) を参照）。
6. Play モードに入って、残した Cloth の動きが、記録したものと同じかを確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、値を確かめます。AAO を使っているときは、Play モードに入ったときのランクを Actual Performance Window で確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest では Cloth は使えません**: 公式の制限のページは、「Cloth | Completely disabled on Android and Quest.」（訳: Cloth は Android と Quest で完全に無効）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。Mobile Removed Components の一覧の値について、公式は「These values may still appear in VRChat's avatar details screen, but they are always zero.」（訳: これらの値は VRChat のアバターの詳細画面に出ることがあるが、常にゼロです）と書いています。
- **無効にしただけでは数は減りません**: 無効にしたものも数えられるという公式の書き方は、上の「減らす前に確かめること」を参照してください。
- **AAO の Cloth の記録（1.7.0）**: 上の AAO の節のとおり、1.7.0 から、Cloth のある SkinnedMeshRenderer を Merge Skinned Mesh の統合元にするとエラーにすると、CHANGELOG に記録されています。現行の 1.9.21 での挙動は、この記事では**未確認**{.lbl .lbl-unk}です。
:::

## 関連ページ

- [Cloth の最大頂点の確かめ方](/learn/optimize-physics/cloth-vertices)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
