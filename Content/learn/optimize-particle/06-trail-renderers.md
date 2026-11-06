---
title: トレイルレンダラーの数を減らす
reading: とれいるれんだらーのかずをへらす
description: トレイルレンダラー（Trail Renderer）の数の上限、超えたときの処理、範囲（BOUNDS）との関係、ツールごとに公式に記載があるかを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-06
---

トレイルレンダラー（Trail Renderer）は、動いた軌跡に尾を引く線を描く部品です。VRChat 公式の定義は "The number of Trail Renderers on the avatar."（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）で、訳は「アバターにあるトレイルレンダラーの数」です。パーティクルシステムのトレイル（パーティクルトレイル）とは別の項目として数えられます。

上限は、PC の Poor が 8、Android の Poor が 1 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。ランクごとの値は次のとおりです。

| ランク | PC | Android |
|---|---|---|
| Excellent | 1 | 0 |
| Good | 2 | 0 |
| Medium | 4 | 0 |
| Poor | 8 | 1 |

上限を超えると、公式では次のランクになると書かれています。表示の制限（Minimum Displayed Performance Rank）の対象になると、トレイルレンダラーだけが取り除かれます（原文: "All Trail Renderers removed"）。公式の例は、最低表示ランクが Poor のとき、トレイルレンダラーが 9 個（Very Poor）のアバターは、トレイルレンダラーをすべて取り除いて表示される、というものです（英語の原文の要約）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21 と Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）を含む 5 つのツールの公式の資料を、2026 年 11 月 6 日に確かめて書きました。ほかのツールは版番号を未確認のまま、取得日の時点の公式の資料を確かめました。MMS の公開ドキュメント（ramtype0.github.io）は、未確認です。

VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。VRChat の公式は英語の原文だけなので、ここでの引用は原文に訳を添えています。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「トレイルレンダラーの数」で、今の値と上限を見ます。例の画面では 5/8 で、上限の 8 以下でした（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- **範囲（BOUNDS）の計算には入りません**: 公式の脚注は "Trail and Line Renderers do not count for this calculation."（訳: トレイルとラインのレンダラーは、この計算に入らない）と書いています。範囲の確かめ方は [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds) を見てください。
- **無効にしても数は減りません**: 公式は "All GameObjects and Components, including those that are currently disabled, count towards the Avatar Performance Rank."（訳: 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる）と書いています。
- **AAO を使うとき**: 公式の基本的な使い方のページは、非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならないと書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられると案内されています（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

VRChat 公式の資料に、トレイルレンダラーを減らす専用の勧めは見当たりませんでした（Avatar Optimization Tips と Android の制限の文書を確かめた範囲）。そのため、この記事では、減らす具体的な方法は書きません。パーティクルシステムの勧めは、パーティクルの項目で扱います。

減らし方は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) と [アバターを軽くする](/learn/vrchat/optimization) を見てください。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README で、Trail・トレイル・LineRenderer の語を検索）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: 公式サイトの `/docs/` 配下 81 ページで、Trail・トレイル・LineRenderer の語を検索）。

### lilAvatarUtils

未確認。レンダラーのタブは「アバターに含まれる全てのレンダラーの一覧です。」と説明していますが、トレイルレンダラーを一覧に含むかは書かれていません。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub のリポジトリの README と、`.docfx` にあるドキュメント 11 件（目次を含めると 19 件）で、Trail・トレイル・LineRenderer の語を検索。公開サイト（ramtype0.github.io）は未確認）。

### Avatar Optimizer（AAO）

公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG で、TrailRenderer・LineRenderer・Trail を検索）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「トレイルレンダラーの数」を確かめます（上限は PC の Poor が 8、Android の Poor が 1）。
2. アバターの中で、Trail Renderer のコンポーネントが付いているオブジェクトを探して、数えます（探す方法は、下の注意を見てください）。
3. 変更の前に、プロジェクトのバックアップを取り、今のトレイルの見た目を記録します（公式の勧めではなく、この記事の助言です）。
4. 必要のないトレイルレンダラーは、無効にするのではなく、コンポーネントを削除します。無効のままでも数に入るためです（公式の注記による）。
5. 変更のあと、トレイルの見た目が想定どおりかを、手順 3 の記録と比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「トレイルレンダラーの数」が減ったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **探す方法は未確認です。** Trail Renderer のコンポーネントが付いたオブジェクトを探す画面と方法は、この記事では確かめていません。
- コンポーネントを削除すると、そのトレイルの線は表示されなくなります。見た目が変わるので、必要なものは残してください。
- 無効にするだけでは、数は減りません（公式の注記による）。
- Android では、トレイルレンダラーは 1 つまでが Poor の上限です。2 つ以上あると、Very Poor の値になります（公式の表による）。
- 「パーティクルトレイル」（パーティクルシステムのトレイル）は、別の項目です。パーティクルシステムのトレイルについては、[パーティクルトレイルを使わないようにする](/learn/optimize-particle/particle-trails) を見てください。
:::

## 関連ページ

- [パーティクルの衝突を使わないようにする](/learn/optimize-particle/particle-collision)
- [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
