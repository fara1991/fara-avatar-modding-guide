---
title: オーディオソースの数を減らす
reading: おーでぃおそーすのかずをへらす
description: オーディオソース（Audio Sources）の数の上限と確かめ方、公式の定義が見つからなかった点、Android・Quest で無効になること、各ツールで公式に記載があるかを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 上限は PC の Poor が 8（Excellent 1、Good 4、Medium 8）で、モバイル（Android と iOS）では無効
- 公式の項目の説明の表に、この項目の行がありません
- Avatar Optimization Tips には、オーディオソースの数を減らす勧めがありません
:::

オーディオソース（Audio Sources）は、VRChat 公式のパフォーマンスランクの項目の一つです。公式の項目の説明の表には、この項目の行がありません。上限は PC の Poor が 8 で（段階ごとに、Excellent 1、Good 4、Medium 8。これらは [VRChat の公式の表（GitHub の原本の avatar-performance-ranking-system.md）](https://raw.githubusercontent.com/vrchat-community/creator-docs/main/Docs/docs/avatars/avatar-performance-ranking-system.md) の値です）、モバイル（Android と iOS）では無効です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、公式では次のランクになると書かれています。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、Audio Sources がすべて取り除かれます（原文: "All Audio Sources removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **この記事の助言**{.lbl .lbl-adv}: 公式の説明ではない、この記事の助言です。

この記事は、AAO・Modular Avatar（GitHub の main）・TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を、2026 年 11 月 6 日に確かめて書きました。版は、GitHub の最新リリースで 2026 年 11 月 7 日に確かめました。AAO は 1.9.21（changelog の最新版）、TexTransTool は v1.0.2（2026-06-26）、lilAvatarUtils は 2.1.2（2025-08-11）、Meshia Mesh Simplification は 3.3.1（2026-11-04）です。TexTransTool と lilAvatarUtils は、サイトに版の表示がありません。Meshia Mesh Simplification の公開サイト（ramtype0.github.io）は、トップページを確かめました。版が上がると挙動が変わることがあります。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「オーディオソースの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、2（上限 8）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない Audio Source は、無効にするだけでは数が減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

VRChat 公式の [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips) には、オーディオソースの項目がありません。

公式の [Avatar Size Limits](https://creators.vrchat.com/avatars/avatar-size-limits) には、音声クリップを短くする、クリップの品質を下げる、または Audio Clip のインポート設定で "Force to Mono" を有効にする、という勧めがあります（英語の原文の要約）。ただし、これはダウンロードのサイズを減らす話で、オーディオソースの数についての勧めではありません。

[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview) では、5 ツールとも、オーディオソースの数は「**公式に記載なし**{.lbl .lbl-none}」です。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版の docs 全件、CHANGELOG（安定版とプレリリース版）、README）。CHANGELOG には Audio Source の語が出ますが、数を減らす記述ではなく、挙動の修正です。例えば 1.12.0（2025-04-01）は、Object Toggle で制御される Audio Source が、アニメーションでブロックされたときの挙動を直したものです（[MA の CHANGELOG](https://github.com/bdunderscore/modular-avatar/blob/main/CHANGELOG.md)）。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版の公式ドキュメント 81 ページ（sitemap の /docs 配下））。Audio Source の語は、確かめた範囲に出ませんでした。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ページ 11 件）。Audio Source の語は、確かめた範囲に出ませんでした。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、ja/en のドキュメント。公開サイトは、トップページを確かめました）。Audio Source の語は、確かめた範囲に出ませんでした。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG で、「audio」「オーディオ」「AudioSource」を検索）。CHANGELOG に AudioSource の語が 1 件だけあります。1.6.0（2023-11-27、Fixed）の項目の原文は「warning for `ONSPAudioSource`, `VRCImpostorSettings`, and `RectTransform` are fixed」（訳: ONSPAudioSource、VRCImpostorSettings、RectTransform の警告が直った）で、警告表示の修正です（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。オーディオソースの数を減らす記録ではありません。

「使われていないObjectを自動的に削除する」の原文は「アニメーションなどを走査して、使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します。」です（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。Audio Source がこの対象に入るかは、**公式に記載なし**{.lbl .lbl-none}です。

## 手順

1. SDK の「パフォーマンスの内訳」で、「オーディオソースの数」を確かめます（上限は PC の Poor が 8。モバイル（Android と iOS）では無効です）。
2. 変更の前に、プロジェクトのバックアップを取り、いまの音が鳴るタイミングを記録しておきます（公式の勧めではなく、**この記事の助言**{.lbl .lbl-adv}です）。
3. アバターにある Audio Source を確かめます。
4. 取り除く操作は勧めません。判断できないものは残し、手順 5 で必要な音が記録どおり鳴るかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
5. Play モードに入り、必要な音が記録どおりに鳴るかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。AAO を使っているときは、ランクを Actual Performance Window で確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」で、オーディオソースの数を確かめます。

## 注意・うまくいかないとき

::: warning
- **Android と Quest**: 公式は、Android と Quest のアバターでは Audio Sources が完全に無効になると書いています（[Quest Content Limitations（VRChat 公式）](https://docs.vrchat.com/docs/quest-content-limitations)）。原文は "Audio sources are disabled completely for avatars on Android and Quest. Audio sources consume a significant amount of CPU resources and voices have priority." で、訳は「Android と Quest のアバターでは、Audio sources は完全に無効になります。Audio sources は多くの CPU の資源を使い、voices が優先されます」です。公式の [Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の Mobile Removed Components の一覧にも Audio Sources があり、値は常にゼロと書かれています。
- **AAO の自動の削除**: 「使われていないObjectを自動的に削除する」の対象に Audio Source が入るかは、**公式に記載なし**{.lbl .lbl-none}です。この記事では、使っているかを確かめる方法を示していません。判断できないものは残してください（**この記事の助言**{.lbl .lbl-adv}）。
- **音声クリップの大きさは別の項目です**: 上の「減らし方の考え方」のとおり、クリップの長さや品質の勧めは、ダウンロードのサイズの話です。オーディオソースの数とは別です。
- **定義**: 公式の項目の説明の表に、この項目の行がありません。どの Audio Source が数えられるかの細かい条件は、公式の表からは読み取れません。
:::

## 関連ページ

- [レイキャストの数を減らす](/learn/optimize-physics/raycasts)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
