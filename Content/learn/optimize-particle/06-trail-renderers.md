---
title: トレイルレンダラーの数を減らす
reading: とれいるれんだらーのかずをへらす
description: トレイルレンダラー（Trail Renderer）の数の上限、超えたときの処理、範囲（BOUNDS）との関係、減らす手順をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- トレイルレンダラーの数の上限は、PC の Poor が 8、Android の Poor が 1
- 範囲（BOUNDS）の計算には、トレイルとラインのレンダラーは入らない
- 無効にしても数は減らないため、必要ないものはコンポーネントを削除する
:::

トレイルレンダラー（Trail Renderer）は、動いた軌跡に尾を引く線を描く部品です。定義は、アバターにあるトレイルレンダラーの数です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。パーティクルシステムのトレイル（パーティクルトレイル）とは別の項目として数えられます。

上限は、PC の Poor が 8、Android の Poor が 1 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。ランクごとの値は次のとおりです。

| ランク | PC | Android |
|---|---|---|
| Excellent | 1 | 0 |
| Good | 2 | 0 |
| Medium | 4 | 0 |
| Poor | 8 | 1 |

上限を超えると、次のランクになります。表示の制限（Minimum Displayed Performance Rank）の対象になると、All Trail Renderers removed（トレイルレンダラーだけが取り除かれる）になります。例えば、最低表示ランクが Poor のとき、トレイルレンダラーが 9 個（Very Poor）のアバターは、トレイルレンダラーをすべて取り除いて表示されます。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「トレイルレンダラーの数」で、今の値と上限を見ます。例の画面では 5/8 で、上限の 8 以下でした（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- **範囲（BOUNDS）の計算には入りません**: トレイルとラインのレンダラーは、範囲（BOUNDS）の計算には入りません（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。範囲の確かめ方は [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds) を見てください。
- **無効にしても数は減りません**: 現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **AAO を使うとき**: 非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクは実際の値とずれます。実際の値は、Play モードの Actual Performance Window で確かめます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「トレイルレンダラーの数」を確かめます（上限は PC の Poor が 8、Android の Poor が 1）。
2. アバターの中で、Trail Renderer のコンポーネントが付いているオブジェクトを探して、数えます。
3. 変更の前に、プロジェクトのバックアップを取り、今のトレイルの見た目を記録します（**この記事の助言**{.lbl .lbl-adv}）。
4. 必要のないトレイルレンダラーは、無効にするのではなく、コンポーネントを削除します。無効のままでも数に入るためです。
5. 変更のあと、トレイルの見た目が想定どおりかを、手順 3 の記録と比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「トレイルレンダラーの数」が減ったかを確かめます。

## 注意・うまくいかないとき

::: warning
- コンポーネントを削除すると、そのトレイルの線は表示されなくなります。見た目が変わるので、必要なものは残してください。
- Android では、トレイルレンダラーは 1 つまでが Poor の上限です。2 つ以上あると、Very Poor の値になります。
- 「パーティクルトレイル」（パーティクルシステムのトレイル）は、別の項目です。パーティクルシステムのトレイルについては、[パーティクルトレイルを使わないようにする](/learn/optimize-particle/particle-trails) を見てください。
:::

## 関連ページ

- [パーティクルの衝突を使わないようにする](/learn/optimize-particle/particle-collision)
- [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
