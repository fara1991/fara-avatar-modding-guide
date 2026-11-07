---
title: パーティクルトレイルを使わないようにする
reading: ぱーてぃくるとれいるをつかわないようにする
description: パーティクルトレイル（Particle Trails）の定義と上限、上限を超えたときの処理を、公式の資料に書かれた範囲でまとめます。公式には減らし方の専用の勧めが見つからなかったため、手順は確かめ方と基本的な操作にとどめています。
difficulty: 中級
updated: 2026-11-07
---

::: point
- パーティクルトレイルは数ではなく、True（あり）か False（なし）で表される
- アバターのどれか 1 つでもトレイルが有効なら、True になる
- 表から読むと、上限は PC が Medium、Android が Poor（公式の文ではなく表の読み方）
- トレイルを減らす専用の勧めは、確かめた範囲（Avatar Optimization Tips など）で公式に記載なし
:::

パーティクルトレイルは、Particle System（パーティクルシステム）でトレイル（粒子が通った跡を尾のように残す表示）が有効になっているかを表す項目です。数を数えるのではなく、True（あり）か False（なし）で表されます。公式の定義は、次の原文です。

> "If any Particle Systems on the avatar have Particle Trails enabled, this will be True."

訳: アバターのどれか 1 つの Particle System でトレイルが有効なら、この項目は True になる。

上限は、公式の表では PC は Medium と Poor、Android は Poor の列が True です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。表から読むと、True になると PC では Medium が上限（Good 以上にならない）、Android では Poor が上限になります。これは公式の文での説明ではなく、表の読み方です。

この項目が True のとき、表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表に「All Particle Systems removed」（すべての Particle System が取り除かれる）と書かれています。トレイルのある Particle System だけが外されるのではありません（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **この記事の助言**{.lbl .lbl-adv}: 公式の説明ではない、この記事の助言です。

この記事は、AAO・Modular Avatar（GitHub の main）・TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を、2026 年 11 月 6 日に確かめて書きました。版は、GitHub の最新リリースで 2026 年 11 月 7 日に確かめました。AAO は 1.9.21（changelog の最新版）、TexTransTool は v1.0.2（2026-06-26）、lilAvatarUtils は 2.1.2（2025-08-11）、Meshia Mesh Simplification は 3.3.1（2026-11-04）です。TexTransTool と lilAvatarUtils は、サイトに版の表示がありません。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルトレイル」の欄は、数字ではなく「FALSE/あり」と表示されます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、左の値（このアバターの値）が FALSE でした。
- **Control Panel の数字**: AAO の公式は、非破壊改変ツールを使った改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- **無効にしたものの扱い**: 公式は「現在無効化されているものを含め、すべての GameObject とコンポーネントが数えられる」（原文: "including those that are currently disabled"）と書いています。ただし、これは GameObject とコンポーネントの数え方の記述です。

## 減らし方の考え方

- **公式の専用の勧め**: トレイルを減らす専用の勧めは、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: Avatar Optimization Tips、Android 向けの最適化のページ）。
- **公式の補足**: Avatar Performance Ranking System の Material Slots の説明には、トレイル付きの Particle System は、マテリアルスロットを 2 つ使うと書かれています（原文: "Particle System with trails use two"）。これは枠の数え方の補足で、トレイルを減らす方法の記述ではありません。
- **ツールごと**: 5 つのツールの資料で確かめた範囲は、下の節のとおりです。

画面の表示（FALSE/あり）の読み方は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の「FALSE/あり」の節を見てください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README）。トレイルを減らす機能の記述は見つかりませんでした。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ttt.rs64.net の /docs/ 配下 81 ページ）。「トレイル」の語は見つかりませんでした。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ドキュメントのトップページと 9 ページ）。トレイルを確かめるタブや項目の記載は見つかりませんでした。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、`.docfx` にあるドキュメント 11 件（目次を含めると 19 件））。トレイルの語は見つかりませんでした。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページ、CHANGELOG）。"Trail" と「トレイル」を含む記載は見つかりませんでした。

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルトレイル」が FALSE か True かを確かめます。
2. 作業の前に、プロジェクトのバックアップを取り、トレイルのある見た目を記録します（**この記事の助言**{.lbl .lbl-adv}）。
3. トレイルを使っている Particle System を探して、確かめます。
4. 見つけた Particle System のトレイルを、必要がなければ使わない設定にします。
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
- [メッシュパーティクルの三角ポリゴンを減らす](/learn/optimize-particle/mesh-particle-polygons)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
