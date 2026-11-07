---
title: パーティクルトレイルを使わないようにする
reading: ぱーてぃくるとれいるをつかわないようにする
description: パーティクルトレイル（Particle Trails）の定義と上限、上限を超えたときの処理、トレイルを使わない設定にする手順をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- パーティクルトレイルは数ではなく、True（あり）か False（なし）で表される
- アバターのどれか 1 つでもトレイルが有効なら、True になる
- True のとき届く最高のランクは、PC が Medium、Android が Poor
- トレイルを使わなくすると尾を引く表示がなくなるため、見た目を記録と比べて確かめる
:::

パーティクルトレイルは、Particle System（パーティクルシステム）でトレイル（粒子が通った跡を尾のように残す表示）が有効になっているかを表す項目です。数を数えるのではなく、True（あり）か False（なし）で表されます。公式の定義は、次の原文です。

> "If any Particle Systems on the avatar have Particle Trails enabled, this will be True."

訳: アバターのどれか 1 つの Particle System でトレイルが有効なら、この項目は True になる。

公式の表では、True が許される列は、PC が Medium と Poor、Android が Poor です。つまり、True のとき、PC は Medium、Android は Poor が届く最高のランクです。

この項目が True のとき、表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表に「All Particle Systems removed」（すべての Particle System が取り除かれる）と書かれています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は原文に訳を添えています。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルトレイル」の欄は、数字ではなく「FALSE/あり」と表示されます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、内訳の「パーティクルトレイル」の欄が FALSE でした。
- **Control Panel の数字**: AAO の公式は、非破壊改変ツールを使った改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- **無効にしたものの扱い**: 公式は「現在無効化されているものを含め、すべての GameObject とコンポーネントが数えられる」（原文: "including those that are currently disabled"）と書いています。

画面の表示（FALSE/あり）の読み方は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の「FALSE/あり」の節を見てください。

## マテリアルスロットへの影響

- **公式の補足**: Avatar Performance Ranking System の Material Slots の説明には、トレイル付きの Particle System は、マテリアルスロットを 2 つ使うと書かれています（原文: "Particle System with trails use two"）。トレイル付きの Particle System は 2 枠、トレイルのないものは 1 枠を使うので、トレイルを外すと、その枠は 2 から 1 に減ります（公式の Material Slots の補足による）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルトレイル」が FALSE か True かを確かめます。
2. 作業の前に、プロジェクトのバックアップを取り、トレイルのある見た目を記録します（**この記事の助言**{.lbl .lbl-adv}）。
3. トレイルを使っている Particle System を探して、確かめます。
4. 必要がないと確かめたトレイルは、Particle System の Trails モジュールのチェックを外します。
5. Play モードに入って、見た目と動きが手順 2 の記録と同じかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「パーティクルトレイル」が FALSE になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **見た目が変わります**: トレイルを使わなくすると、尾を引く表示がなくなります。見た目が変わるので、記録と比べて確かめてください。
- **トレイルレンダラーは別の項目です**: 「トレイルレンダラーの数」（Trail Renderer の数）は、この項目とは別の項目で、上限も別です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [メッシュパーティクルの三角ポリゴン数の確かめ方と、減らす手がかり](/learn/optimize-particle/mesh-particle-polygons)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
