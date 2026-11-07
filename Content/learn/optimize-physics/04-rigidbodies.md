---
title: リジッドボディの数を減らす
reading: りじっどぼでぃのかずをへらす
description: パフォーマンスランクの「リジッドボディの数（Physics Rigidbodies）」の上限と確かめ方、ツールごとに公式に書かれていることを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Excellent が 0、Good が 1、Medium と Poor が 8
- 項目の定義の行は未確認で、Unity の Rigidbody の数という読みは推定
- Rigidbody を減らす機能は、5 ツールとも公式に記載なし
- Android と Quest では、Rigidbody は完全に無効と公式に書かれている
:::

リジッドボディの数（Physics Rigidbodies）は、アバターにある Unity の Rigidbody（物理演算で動く部品）の数を数える項目だと、項目名から読めます。ただし、VRChat 公式の統計表には、この項目の定義の行を確認できませんでした（**未確認**{.lbl .lbl-unk}）。Unity の Rigidbody の数だという読みは、この記事の推定です。

上限は、PC では Excellent が 0、Good が 1、Medium と Poor が 8 です（[VRChat の公式の表（GitHub の原本の avatar-performance-ranking-system.md）](https://raw.githubusercontent.com/vrchat-community/creator-docs/main/Docs/docs/avatars/avatar-performance-ranking-system.md)）。Android には上限の行がなく、公式の Mobile Removed Components に入っています（iOS については**未確認**{.lbl .lbl-unk}）。

上限を超えると、公式は「どの項目でも上限を超えると、次のランクに入る」と書いています（英語の原文の要約。[パフォーマンスランク](/learn/vrchat/performance-rank)）。表示の最低ランクを設定しているときは、リジッドボディがすべて取り除かれます（原文: "All Physics Rigidbodies removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は**未確認**{.lbl .lbl-unk}）、Meshia Mesh Simplification（GitHub の main）の資料を、2026 年 11 月 6 日に確かめて書きました。TexTransTool と lilAvatarUtils は、サイトに版の表示は無い。GitHub の最新リリースは TexTransTool v1.0.2（2026-06-26）、lilAvatarUtils 2.1.2（2025-08-11）（2026-11-06 時点）。Meshia Mesh Simplification の公開サイト（ramtype0.github.io）は、トップページを確認。サブページは**未確認**{.lbl .lbl-unk}。VRChat の公式は英語の原文だけなので、ここでの引用は原文に訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「リジッドボディの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、2（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わないリジッドボディは、無効にするだけでは数に残ります。これは、すべての GameObject とコンポーネントを数える、という一般の文から読んだもので、この項目の値に当てはまるかは**未確認**{.lbl .lbl-unk}です。
- **AAO を使うとき**: AAO の公式の基本的な使い方のページは、非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。リジッドボディの数だけを表示する方法は、**公式に記載なし**{.lbl .lbl-none}です。

## 減らし方の考え方

VRChat 公式の [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips) には、リジッドボディの専用の項目が無く、減らし方の勧めは確認できませんでした（**未確認**{.lbl .lbl-unk}）。そのため、この記事の考え方は、公式の勧めではなく、この記事の整理です。

- 使っているかを確かめる方法は、この記事では**未確認**{.lbl .lbl-unk}です。そのため、取り除く操作は勧めません。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README で、Rigidbody・rigid body・リジッドボディ・剛体の語を検索）。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: [TexTransTool 公式](https://ttt.rs64.net) の日本語版のドキュメント 81 ページ（sitemap の /docs 配下）で、Rigidbody・リジッドボディ・剛体の語を検索）。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ドキュメントの各ページで、Rigidbody・リジッドボディ・剛体の語を検索）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）、`.docfx` の日本語版と英語版のドキュメントで、Rigidbody・rigid body・リジッドボディの語を検索。公開サイト（[MMS 公式](https://ramtype0.github.io/Meshia.MeshSimplification/)）は、トップページを確認。サブページは**未確認**{.lbl .lbl-unk}）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページで、Rigidbody・リジッドボディ・剛体を検索）。「使われていないObjectを自動的に削除する」の対象は、「GameObjectやコンポーネント、テクスチャなど」と書かれていて、Rigidbody は名指しされていません。

AAO の CHANGELOG で、Rigidbody に関係する記録は 1 件です。Joint（関節。Rigidbody どうしをつなぐ Unity の部品）が壊れる、という記録です。

| 版・日付 | 節 | 原文 | 記録の中身（訳） |
|---|---|---|---|
| 1.5.10（2023-11-04） | Fixed | `RigidBody Joint can be broken` | Rigidbody の Joint が壊れることがある、という不具合を直した記録。原因や影響の範囲は、この 1 行からは分かりません |

Fixed は、不具合を直した記録です。減らす機能の記録ではありません。この版は古いため、今の版で同じ問題が起きるかは、この記事では**未確認**{.lbl .lbl-unk}です。

## 手順

1. SDK の「パフォーマンスの内訳」で「リジッドボディの数」を確かめます。PC の上限は、Excellent が 0、Good が 1、Medium と Poor が 8 です。Excellent の上限が 0 なので、1 つでもあると Excellent にはなりません（公式の表からの読み取りです）。
2. 変更の前に、プロジェクトのバックアップを取り、確かめたい動き（リジッドボディに関係する見た目や動き。特に Joint が関係する動き）を記録しておきます（公式の勧めではなく、**この記事の助言**{.lbl .lbl-adv}です）。
3. アバターの中にある Unity の Rigidbody を確かめます。使っているかを確かめる方法は、この記事では**未確認**{.lbl .lbl-unk}です。
4. そのため、取り除く操作は勧めません。使っているか判断できないリジッドボディは残し、手順 2 で記録した動きと比べてください（**この記事の助言**{.lbl .lbl-adv}）。
5. 変更のあと、手順 2 で記録した動きと比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「リジッドボディの数」を確かめます。

## 注意・うまくいかないとき

::: warning
- **モバイル（Android。iOS については未確認）**: 公式の Mobile Removed Components は、"The following components are disabled on mobile devices since they can never appear on avatars:"（訳: 次のコンポーネントは、アバターに表示されることがないため、モバイルの端末では無効になります）と書き、その一覧に Physics Rigidbodies が入っています。公式は、この値は "they are always zero"（いつもゼロ）と書いています。
- **Android と Quest**: 公式の Quest Content Limitations は、"Rigidbodies, colliders, and joints are disabled completely for avatars on Android and Quest."（訳: Android と Quest では、アバターの Rigidbody・コライダー・ジョイントは完全に無効になります）と書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **Joint の不具合の記録**: AAO の 1.5.10 の記録には、Rigidbody の Joint が壊れることがあったという不具合があります。Joint が関係するものは、変更の前後で動きを比べてください（**この記事の助言**{.lbl .lbl-adv}）。
- **AAO の自動の削除**: 「使われていないObjectを自動的に削除する」が、Rigidbody を消すかは、公式に名指しがなく、この記事では**未確認**{.lbl .lbl-unk}です。
- **数値は変わることがあります**: 公式は、Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。この記事の数値は、2026 年 11 月 6 日時点のものです。
:::

## 関連ページ

- [コライダーの数を減らす](/learn/optimize-physics/colliders)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
