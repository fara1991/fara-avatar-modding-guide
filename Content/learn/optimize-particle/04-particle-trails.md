---
title: パーティクルトレイルを使わないようにする
reading: ぱーてぃくるとれいるをつかわないようにする
description: パーティクルトレイル（Particle Trails）の定義と上限、上限を超えたときの処理を、公式の資料に書かれた範囲でまとめます。公式には減らし方の専用の勧めが見つからなかったため、手順は確かめ方と基本的な操作にとどめています。
difficulty: 中級
updated: 2026-11-06
---

パーティクルトレイルは、Particle System（パーティクルシステム）でトレイル（粒子が通った跡を尾のように残す表示）が有効になっているかを表す項目です。数を数えるのではなく、True（あり）か False（なし）で表されます。公式の定義は、次の原文です。

> "If any Particle Systems on the avatar have Particle Trails enabled, this will be True."

訳: アバターのどれか 1 つの Particle System でトレイルが有効なら、この項目は True になる。

上限は、公式の表では PC・Android ともに Poor の列が True です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。表から読むと、True になると PC では Medium が上限（Good 以上にならない）、Android では Poor が上限になります。これは公式の文での説明ではなく、表の読み方です。

この項目が True のとき、表示の下限（Minimum Displayed Performance Rank）より下のランクになると、公式の表に「All Particle Systems removed」（すべての Particle System が取り除かれる）と書かれています。トレイルのある Particle System だけが外されるのではありません（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main）、TexTransTool・lilAvatarUtils・Meshia Mesh Simplification（版番号は未確認。取得日 2026 年 11 月 6 日の公開サイトと GitHub の main）の資料を、2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルトレイル」の欄は、数字ではなく「FALSE/あり」と表示されます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、左の値（このアバターの値）が FALSE でした。
- **Control Panel の数字**: AAO の公式は、非破壊改変ツールを使った改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- **無効にしたものの扱い**: 公式は「現在無効化されているものを含め、すべての GameObject とコンポーネントが数えられる」（原文: "including those that are currently disabled"）と書いています。ただし、これは GameObject とコンポーネントの数え方の記述です。トレイルの True / False の定義（"have Particle Trails enabled"）に、無効化された Particle System が含まれるかは、この記事では未確認です。

## 減らし方の考え方

- **公式の専用の勧め**: トレイルを減らす専用の勧めは、公式に記載なし（確かめた範囲: Avatar Optimization Tips、Android 向けの最適化のページ）。
- **公式の補足**: Avatar Performance Ranking System の Material Slots の説明には、トレイル付きの Particle System は、マテリアルスロットを 2 つ使うと書かれています（原文: "Particle System with trails use two"）。これは枠の数え方の補足で、トレイルを減らす方法の記述ではありません。
- **ツールごと**: 5 つのツールの資料で確かめた範囲は、下の節のとおりです。

この項目の減らし方は、公式に専用の勧めが見つからなかったため、この記事には書きません。数の確かめ方は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) を見てください。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README）。トレイルを減らす機能の記述は見つかりませんでした。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: ttt.rs64.net の /docs/ 配下 81 ページ）。「トレイル」の語は見つかりませんでした。

### lilAvatarUtils

公式に記載なし（確かめた範囲: ja_JP の公式ドキュメントのトップページと 9 ページ）。トレイルを確かめるタブや項目の記載は見つかりませんでした。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、`.docfx` にあるドキュメント 11 件（目次を含めると 19 件）。公開サイト（ramtype0.github.io）は未確認）。トレイルの語は見つかりませんでした。

### Avatar Optimizer（AAO）

公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページ、CHANGELOG）。"Trail" と「トレイル」を含む記載は見つかりませんでした。

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルトレイル」が FALSE か True かを確かめます。
2. 作業の前に、プロジェクトのバックアップを取り、トレイルのある見た目を記録します（この記事の助言）。
3. トレイルを使っている Particle System を探して、確かめます（探す方法は、下の注意を見てください）。
4. 見つけた Particle System のトレイルを、必要がなければ使わない設定にします（設定の場所は、下の注意を見てください）。
5. Play モードに入って、見た目と動きが手順 2 の記録と同じかを確かめます（この記事の助言）。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「パーティクルトレイル」が FALSE になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **探す方法と設定の場所は未確認です。** トレイルを使っている Particle System を探す画面と、トレイルを使わない設定の場所は、この記事では確かめていません。
- **見た目が変わります**: トレイルを使わなくすると、尾を引く表示がなくなります。見た目が変わるので、記録と比べて確かめてください。
- **トレイルレンダラーは別の項目です**: 「トレイルレンダラーの数」（Trail Renderer の数）は、この項目とは別の項目で、上限も別です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [メッシュパーティクルの三角ポリゴンを減らす](/learn/optimize-particle/mesh-particle-polygons)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
