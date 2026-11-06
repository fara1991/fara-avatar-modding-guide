---
title: レイキャストの数を減らす
reading: れいきゃすとのかずをへらす
description: レイキャスト（VRCRaycast）の数の上限と、1 アバターあたり 80 個の固定の上限、確かめ方、各ツールで公式に記載があるかを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-06
---

レイキャスト（VRCRaycast）は、アバターからシーンに光線を飛ばし、ワールドのコライダーやほかのプレイヤーに当たったかを調べる部品です。VRChat 公式の定義は "The number of VRCRaycast components on the avatar." で、訳は「アバターにある VRCRaycast コンポーネントの数」です。上限は PC が Excellent 1・Good 4・Medium 8・Poor 15、Android が 1・2・4・8 で（PC の Poor と Android の Poor の値は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）、どのプラットフォームでも 1 アバターあたり 80 個の固定の上限があります。

上限を超えると、公式では次のランクになると書かれています。表示される最低のランクの設定によっては、Raycasts がすべて取り除かれます（原文: "All Raycasts removed"）。80 個を超えたときに何が起きるかは、公式に書かれていません（未確認）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、TexTransTool v1.0.2、lilAvatarUtils 2.1.2、Meshia Mesh Simplification（main）の公式の資料を、2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「レイキャストの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、0（上限 15）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない VRCRaycast は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

VRChat 公式の [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips) には、レイキャストの数を減らす勧めは見つかりませんでした（未確認）。

一方、[VRCRaycast のページ（VRChat 公式のドキュメントの原本）](https://raw.githubusercontent.com/vrchat-community/creator-docs/main/Docs/docs/avatars/avatar-components/raycast.md) には、80 個の上限は FinalIK のコンポーネントと共有されていると書かれています。そして、FinalIK は「VRCRaycast で足りる場面では、一般に使わないほうがよい」と書かれています（原文: "which should generally not be used in situations where VRCRaycast is enough to meet your needs."。英語の原文の要約）。

全体の考え方は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) と [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview) を見てください。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: MA の日本語版と英語版の docs 全件、CHANGELOG、README）。CHANGELOG には、MA Parameters が VRCRaycast に対応したと書かれています（1.17.0、2026-05-11。原文: "Added support for `VRCRaycast` components and parameters in `MA Parameters`"）。これは対応の記録で、VRCRaycast の数に関する記載はありません。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: 日本語版の公式ドキュメント 11 ページと CHANGELOG。英語版は未確認）。TTT の CHANGELOG にある「レイキャスト」は、TTT 自身の処理の話で、VRChat の VRCRaycast とは別のものです（原文: 「IslandCulling のレイキャスト処理の高速化 (#172)」）。

### lilAvatarUtils

公式に記載なし（確かめた範囲: ja_JP の公式ページ 11 件）。Raycast の語は、確かめた範囲に出ませんでした。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、ja/en のドキュメント。公開サイトは未確認）。Raycast の語は、確かめた範囲に出ませんでした。

### Avatar Optimizer（AAO）

公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページで、「raycast」「レイキャスト」「RaycastCount」を検索）。CHANGELOG には VRCRaycast に関する記録が 4 行あります。いずれも対応の追加か不具合の修正で、数を減らす記録ではありません（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

- 1.9.9（2026-04-18、Added）: VRCSDK 3.10.3 で追加された `VRCRaycast` コンポーネントへの対応を追加した。子の項目では、`RaycastCount` のパフォーマンスカテゴリへの対応も追加したと書かれています。
- 1.9.10（2026-04-18、Fixed）: VRCSDK 3.10.3 の安定化で `VRCRaycast` のプロパティが 1 つ削除され、AAO でエラーが出ていた不具合の修正です。
- 1.9.21（2026-10-30、Added）: VRCSDK 3.10.6 の機能への対応の一つとして、VRC Raycast v1.1 への対応を追加した、と書かれています。

AAO が VRCRaycast の数を減らすという記載は、公式のドキュメントにも CHANGELOG にもありません（公式に記載なし）。RaycastCount への対応も、数を減らす機能の記載ではありません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「レイキャストの数」を確かめます（上限は PC の Poor が 15、Android の Poor が 8）。
2. 変更の前に、プロジェクトのバックアップを取り、各 VRCRaycast が何を調べているか（何に当てているか）を記録しておきます（公式の勧めではなく、この記事の助言です）。
3. 使っていない VRCRaycast を探します。探す方法は、この記事では未確認です。
4. 使っていないと確かめた VRCRaycast は、無効にするだけでなく取り除きます。無効にしたものも数えられるためです（公式の注記による）。
5. FinalIK を使っているなら、80 個の上限を FinalIK と分け合っている点を確かめます。VRCRaycast で足りる部分は VRCRaycast に寄せられるかを検討します（公式の注記に沿った、この記事の助言です）。
6. Play モードに入り、当たり判定の動きが変わっていないかを確かめます（この記事の助言）。AAO を使っているときは、ランクを Actual Performance Window で確かめます。
7. もう一度 SDK の「パフォーマンスの内訳」で、レイキャストの数を確かめます。

## 注意・うまくいかないとき

::: warning

- **80 個の固定の上限**: 公式は、VRCRaycast のコンポーネントには、全プラットフォームで 1 アバターあたり 80 個の固定の上限があると書いています（原文: "VRCRaycast components have a hard upper limit of 80 components per avatar on all platforms."。[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) のランクの表の脚注）。80 個を超えたときの挙動は、公式に書かれておらず、未確認です。
- **FinalIK と共有**: この上限は FinalIK のコンポーネントと共有されます（上の VRCRaycast のページの記載）。
- **Android と Quest**: [Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations) には、Raycast の記述がありません。Android で上限（8）を超えたときの扱いは、未確認です。
- **Player の層**: VRCRaycast のページ（VRChat 公式）は、Player の層（ほかのプレイヤーのコライダー）を含み、ほかの層も 1 つ以上含むレイヤーのマスクを使うと、光線が 1 つではなく 2 本の合成された光線として飛ばされると書いています（原文: "two combined rays are fired instead of one."）。ほかのプレイヤーに当てるには、バージョン 1.1 以降が必要です（原文: "you must use version 1.1 or later."）。

:::

## 関連ページ

- [オーディオソースの数を減らす](/learn/optimize-physics/audio-sources)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
