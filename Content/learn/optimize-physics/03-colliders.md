---
title: コライダーの数を減らす
reading: こらいだーのかずをへらす
description: パフォーマンスランクの「コライダーの数（Physics Colliders）」の上限と確かめ方、ツールごとに公式に書かれていることを、公式の資料に書かれた範囲でまとめます。PhysBone のコライダーとは別の項目です。
difficulty: 中級
updated: 2026-11-06
---

コライダーの数（Physics Colliders）は、アバターにある Unity の Collider（物理の衝突の形を決める部品。BoxCollider や SphereCollider など）の数を数える項目だと、項目名から読めます。ただし、VRChat 公式の統計表には、この項目の定義の行を確認できませんでした（未確認）。Unity の Collider の数だという読みは、この記事の推定です。PhysBone のコライダー（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)）とは、別の項目です。

上限は、PC では Excellent が 0、Good が 1、Medium と Poor が 8 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。Android と iOS（モバイル）には上限の行がなく、公式の Mobile Removed Components に入っています。

上限を超えると、公式は「どの項目でも上限を超えると、次のランクに入る」と書いています（英語の原文の要約。[パフォーマンスランク](/learn/vrchat/performance-rank)）。表示の最低ランクを設定しているときは、コライダーがすべて取り除かれます（原文: "All Physics Colliders removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、Meshia Mesh Simplification（GitHub の main）の資料を、2026 年 11 月 6 日に確かめて書きました。TexTransTool と lilAvatarUtils は、公式サイトに版の表示が無かったため、版番号は未確認のまま書いています。VRChat の公式は英語の原文だけなので、ここでの引用は原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コライダーの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、1（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないコライダーは、無効にするだけでは数に残ります。
- **PhysBone のコライダーと混同しないこと。** 公式の統計表の「PhysBones Colliders」は、"The number of PhysBone collider scripts on the avatar."（訳: アバターにある PhysBone のコライダーのスクリプトの数）で、別の項目です。
- **AAO を使うとき**: AAO の公式の基本的な使い方のページは、非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。コライダーの数だけを表示する方法は、公式に記載なしです。

## 減らし方の考え方

VRChat 公式の [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips) には、コライダーの専用の項目が無く、減らし方の勧めは確認できませんでした（未確認）。そのため、この記事の考え方は、公式の勧めではなく、この記事の整理です。

- 使っていないと確かめたコライダーは、取り除きます。無効にするだけでは数に残るためです（上の「無効にしたものも数えられます」）。

## ツール別の方法

### Modular Avatar（MA）

**何をするか**: MA の Global Collider は、VRChat の指などの組み込みのグローバルコライダーを差し替えて、ほかのアバターの PhysBone と干渉させる機能だと、公式は説明しています（[Global Collider（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/global-collider)）。Physics Colliders の数を減らす機能としての記載はありません。

**注意**: 同じ公式は、VRChat ではグローバルコライダーの数が限られていて、6 つ以上 Global Collider を使うと、人差し指のコライダーが上書きされると書いています。これはグローバルコライダーの注意で、Physics Colliders の数の話ではありません。また、MA の Replace Object の公式ドキュメント（英語版）は、Box Collider を Sphere Collider に替えると、古い Box Collider への参照が null になる、と注意しています（英語の原文の要約）。Unity の Collider の数を減らす機能は、公式に記載なし（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README で、Collider とコライダーの語を検索）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: [TexTransTool 公式](https://ttt.rs64.net) の日本語版のドキュメント 11 ページで、Collider とコライダーの語を検索）。

### lilAvatarUtils

**何をするか**: PBコライダーのタブは、アバターに含まれる全ての PhysBone のコライダーの一覧です。公式は、「コライダーの参照元の特定や、どこからも参照されない無駄なコライダーの削減に役立ちます。」と書いています（[PBコライダー（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI.html)。訳は、参照元を調べたり、どこからも参照されないコライダーを減らしたりするのに役立つ、という意味です）。

**注意**: このタブは PhysBone のコライダーの一覧で、Unity の Collider を含むかは、この記事では確認していません（未確認）。一覧から減らす操作の詳細も、未確認です。Unity の Collider の数を減らす機能は、公式に記載なし（確かめた範囲: ja_JP の公式ドキュメントの各ページ）。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: GitHub のリポジトリの README（英語・日本語）、`.docfx` の日本語版と英語版のドキュメント、公開サイト（[MMS 公式](https://ramtype0.github.io/Meshia.MeshSimplification/)）の HTML で、Collider とコライダーの語を検索）。

### Avatar Optimizer（AAO）

**何をするか**: Trace And Optimize の「PhysBone を最適化する」は、全く同じ設定の PhysBone Collider を 1 つに統合します（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。これは PhysBone のコライダーの統合で、Unity の Collider の数を減らす機能ではありません。

Unity の Collider の数を減らす機能は、公式に記載なし（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページで、コライダーと collider を検索）。「使われていないObjectを自動的に削除する」の対象は、「GameObjectやコンポーネント、テクスチャなど」と書かれていて、Collider は名指しされていません。

AAO の CHANGELOG には、Unity の Collider に関係する記録が 3 件あります。いずれも Fixed（不具合の修正）で、減らす機能の記録ではありません。

| 版・日付 | 原文（抜粋） | 記録の中身（訳） |
|---|---|---|
| 1.5.2（2023-10-10） | `Avatar Standard Colliders can be removed` | コライダーが削除されることがあった不具合の修正。Collider の種類は原文に書かれていません |
| 1.6.0（2023-11-27） | `Trigger Colliders can be disapper if you specify Transform instead of Collider instance.` | Collider のインスタンスではなく Transform を指定すると、Trigger Collider が消えることがあった不具合の修正（原文の綴りのまま） |
| 1.8.0（2024-11-30） | `Animation bindings for BoxCollider generated by VRCStation will be removed` | VRCStation が作る BoxCollider のアニメーションのバインドが消されていた不具合の修正。子の項目に、Quest / Android に対応した GogoLoco などの飛行するアバターが壊れる可能性がある、とあります |

3 件とも、今の版で同じことが起きるかは、この記事では未確認です。

## 手順

1. SDK の「パフォーマンスの内訳」で「コライダーの数」を確かめます。PC の上限は、Excellent が 0、Good が 1、Medium と Poor が 8 です。Excellent の上限が 0 なので、1 つでもあると Excellent にはなりません（公式の表からの読み取りです）。
2. 変更の前に、プロジェクトのバックアップを取り、確かめたい動き（コライダーに関係する見た目や動き）を記録しておきます（公式の勧めではなく、この記事の助言です）。
3. アバターの中にある Unity の Collider を確かめ、使っているかを確かめます。使っているかを確かめる方法は、この記事では未確認です。
4. 使っていないと確かめたコライダーは、取り除きます。無効にするだけでは数に残るためです（公式の注記による）。
5. 変更のあと、手順 2 で記録した動きと比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「コライダーの数」を確かめます。

## 注意・うまくいかないとき

::: warning
- **モバイル（Android・iOS）**: 公式の Mobile Removed Components は、"The following components are disabled on mobile devices since they can never appear on avatars:"（訳: 次のコンポーネントは、アバターに表示されることがないため、モバイルの端末では無効になります）と書き、その一覧に Physics Colliders が入っています。公式は、この値は "they are always zero"（いつもゼロ）と書いています。
- **Android と Quest**: 公式の Quest Content Limitations は、"Rigidbodies, colliders, and joints are disabled completely for avatars on Android and Quest."（訳: Android と Quest では、アバターの Rigidbody・コライダー・ジョイントは完全に無効になります）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **アニメーションとの関係**: AAO の 1.8.0 の記録は、BoxCollider のアニメーションのバインドが消されていた不具合です。そのため、アニメーションが Collider を参照していることがあります。参照していないかを確かめる方法は、この記事では未確認です。
- **AAO の自動の削除**: 「使われていないObjectを自動的に削除する」が、Collider を消すかは、公式に名指しがなく、この記事では未確認です。
- **数値は変わることがあります**: 公式は、Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。この記事の数値は、2026 年 11 月 6 日時点のものです。
:::

## 関連ページ

- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [リジッドボディの数を減らす](/learn/optimize-physics/rigidbodies)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
