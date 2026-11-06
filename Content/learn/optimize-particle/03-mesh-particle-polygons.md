---
title: メッシュパーティクルの三角ポリゴンを減らす
reading: めっしゅぱーてぃくるのさんかくぽりごんをへらす
description: メッシュパーティクルの三角ポリゴン数の上限と確かめ方、公式の定義の食い違い、ツールごとに資料で確かめた範囲をまとめます。この項目を減らす具体的な方法は資料で確かめられなかったため、手順には入れていません。
difficulty: 中級
updated: 2026-11-06
---

メッシュパーティクルの三角ポリゴン数は、有効（active）な Particle System（パーティクルシステム）が出すメッシュ状の粒子（メッシュパーティクル）の三角ポリゴンの数を数える項目です。SDK の「パフォーマンスの内訳」では「メッシュパーティクルの三角ポリゴン数」と表示され、公式の表の行名は Mesh Particle Active Polys です。上限は、PC の Poor が 5,000、Android の Poor が 400 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになったときは、公式の表に「All Particle Systems removed」（すべての Particle System が取り除かれる）と書かれています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、英語の原文には、極端に最適化されていないアバターは使えなくなることがある、とも書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は**未確認**{.lbl .lbl-unk}）、TexTransTool・lilAvatarUtils・Meshia Mesh Simplification（版番号は**未確認**{.lbl .lbl-unk}。取得日 2026 年 11 月 6 日の公開サイトと GitHub の main）の資料を、2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に訳を添えています。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「メッシュパーティクルの三角ポリゴン数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、4,000,000（上限 5,000）と表示されていました。
- **Control Panel の数字**: AAO の公式は、非破壊改変ツールを使った改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- **数え方の式**: 公式の定義は、次の原文です。
  > "The total number of triangles of Mesh Particles emitted by Particle Systems that are active. In other words, maxEmission * meshParticleVerts."

  訳: 有効な Particle System が出すメッシュパーティクルの三角形（triangles）の合計数。言い換えると、maxEmission × meshParticleVerts。
- **定義の食い違い**: 定義の文は「三角形（triangles）の数」と書いていますが、式の meshParticleVerts は、名前のとおり頂点（verts）の数と読めます。この 2 つの関係は、公式の資料からは判断できず、**未確認**{.lbl .lbl-unk}です。この記事では、表示名と式の両方を、原文のまま並べています。

## 減らし方の考え方

VRChat 公式の勧めは、パーティクル全般のものです。Avatar Optimization Tips には、次の原文があります（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

> "Limit the number of particle systems that you're using, and limit the maximum amount of particles emitting at any one given moment."

訳: 使う Particle System の数を制限し、同時に放出される粒子の最大数を制限する。

メッシュパーティクルの三角ポリゴンだけに当てはめた減らし方は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: VRChat 公式の Avatar Performance Ranking System、Avatar Optimization Tips、Android 向けの 2 ページ）。PC の Particle System の上限の専用ページ（Avatar Particle System Limits）は、この記事では取得できていないため、**未確認**{.lbl .lbl-unk}です。上の勧めが、この項目にどれだけ効くかも、この記事では確かめていません。

::: tip
この記事の判断: 減らす具体的な操作は、効果を確かめられなかったため、手順には入れていません。ツールの節は、そのツールの資料で確かめた範囲だけを書いています。
:::

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README）。ドキュメントに「パーティクル」が出るのは、サンプル（Clap プレハブ）の説明の 1 箇所だけで、仕様の記述ではありません。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ttt.rs64.net の /docs/ 配下 81 ページ。「パーティクル」「トレイル」「LineRenderer」の語は見つかりませんでした）。

### lilAvatarUtils {.tool .tool-lil}

**未確認**{.lbl .lbl-unk}。レンダラーのタブの「ポリゴン数」は、「メッシュのポリゴン数です。」と説明されていますが、パーティクルのメッシュを数えるかは書かれていません（確かめた範囲: ja_JP の公式ドキュメント 9 ページ）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: README（英語・日本語）と、`.docfx` にあるドキュメント 11 件（目次を含めると 19 件）。公開サイト（ramtype0.github.io）は**未確認**{.lbl .lbl-unk}）。パーティクルの語は見つかりませんでした。簡略化の入力は Mesh と書かれているだけで、対象のレンダラーの種類の記載もありません。パーティクルのメッシュに使えるかは、**未確認**{.lbl .lbl-unk}です。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG）。CHANGELOG のパーティクル関連の項目は 7 件あり、そのうち 6 件は「壊れる」ことの修正、1 件（1.8.10）は、パーティクルシステムが正しく削除されない不具合の修正です。いずれも、三角ポリゴンを減らす効果の記載ではありません。例えば 1.9.19 には、次の原文があります（原文のまま。誤字を含みます。[公式の changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

> "Particle Systems with the Shape of vertices of Skinnd Mesh Renderers only with no-triangle submeshesas would be borken"

## 手順

1. SDK の「パフォーマンスの内訳」で、「メッシュパーティクルの三角ポリゴン数」の今の値と上限（PC の Poor が 5,000、Android の Poor が 400）を確かめます。
2. 作業の前に、プロジェクトのバックアップを取り、対象のパーティクルの見た目と動きを記録します（**この記事の助言**{.lbl .lbl-adv}）。
3. 作業するときは、Mesh Read/Write を無効にしないでください（下の注意）。
4. 対象のパーティクルシステムの設定を変えたあとは、Play モードに入って見た目と動きが変わっていないかを確かめ、パフォーマンスの内訳をもう一度見て、数が変わったかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。

## 注意・うまくいかないとき

::: warning
- **Mesh Read/Write**: 公式の記述によると、アバターのいずれかの mesh（パーティクルシステムのメッシュを含む）で Mesh Read/Write を無効にすると、アバターの Triangles（三角ポリゴン数）の行が「Mesh Read/Write Disabled」と表示され、ランクは即座に Very Poor になります。原文は "regardless of the actual triangle count on the avatar"（アバターの実際の三角ポリゴン数にかかわらず）です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **MMS と AAO**: 上のツールの節のとおり、この項目を減らす効果が資料で確かめられたツールはありません。MMS をパーティクルのメッシュに使えるかは**未確認**{.lbl .lbl-unk}です。使う前に、確かめてください。
:::

## 関連ページ

- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [パーティクルトレイルを使わないようにする](/learn/optimize-particle/particle-trails)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
