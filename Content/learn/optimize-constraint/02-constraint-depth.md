---
title: コンストレイントの深さを減らす
reading: こんすとれいんとのふかさをへらす
description: コンストレイントの深さ（依存関係のいちばん長いつながり）の上限と確かめ方、VRChat 公式の考え方（最も長いつながりを短くする、Unity のコンストレイントを VRChat のものに変える）を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- コンストレイントの深さの上限は、PC の Poor が 100、Android の Poor が 50
- 深さは合計ではなく、アバター全体でいちばん長い依存のつながりで決まる
- 公式は、いちばん長いつながりを短くすると深さが下がると書いている
:::

コンストレイントの深さは、アバターにあるコンストレイントの依存関係（あるコンストレイントが、別のコンストレイントの結果に依存しているつながり）のうち、いちばん長いつながりの段数です。VRChat 公式の定義は "The avatar's overall constraint depth rating is the longest chain of dependencies across the entire avatar." で、アバター全体で最も長い依存のつながりです。公式は、"If the chain has 3 constraints along it, then that means the chain has a constraint depth of 3."（訳: つながりに 3 つのコンストレイントがあれば、そのつながりの深さは 3 です）と例を挙げています。上限は PC の Poor が 100、Android の Poor が 50 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、コンストレイントの数と同じです。Android では、硬い上限（50）を超えると、「Show Avatar」を選んでも回避できず、制限の対象のコンポーネントが取り除かれます。詳しくは [コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count) を見てください。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

「依存のつながり」は、公式の dependencies のことです。この記事では、「あるコンストレイントが、別のコンストレイントの結果に依存しているつながり」と言い換えています。

版は、AAO 1.9.21、Modular Avatar（GitHub の main）、TexTransTool v1.0.2、lilAvatarUtils 2.1.2、Meshia Mesh Simplification（main）で、2026 年 11 月 6 日に確かめました。VRChat の公式ドキュメントは英語の原文だけなので、引用は英語の原文に日本語の訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンストレイントの深さ」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、34（上限 100）と表示されていました。
- **深さは合計ではなく、いちばん長いつながりです。** 公式は、いくつかの腕があって、それぞれの深さが 3 でも、ほかに長いつながりがなければ、アバター全体の深さは 3 のままだと例を挙げています（原文: "if your avatar has several arms that all have a depth of 3, the overall score for the avatar would still be 3"）。
- **いちばん長いつながりを見つける方法**: この記事で確かめた範囲の資料には、どのコンストレイントがいちばん長いつながりかを見つける方法は書かれていません（**公式に記載なし**{.lbl .lbl-none}）。

## 減らし方の考え方

公式は、次の 2 つの考え方を書いています（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)。訳は、この記事で添えたものです）。

- **いちばん長いつながりを短くする。** 原文は "Constraint depth can be lowered by reducing the length of the longest chain of constraints." です。訳は、コンストレイントの深さは、いちばん長いつながりの長さを減らすと下がる、という意味です。
- **依存を減らして、同時に動けるようにする。** 原文は "By organizing constraints to minimize the dependencies between them, many of them will be able to run at the same time, which results in better performance compared to situations where the constraints must run one after another." です。訳は、コンストレイントどうしの依存を減らすように並べると、多くを同時に動かせるため、順番に動かさなければならない場合より性能が良くなる、という意味です。

公式はさらに、深さを正しく計算できるのは、VRChat のコンストレイントだけを使っているときだけだと書いています（原文: "The constraint depth of an avatar can only be accurately calculated when it uses VRChat constraints exclusively."）。

この記事の整理として、次の 3 段階で考えます（公式の分類ではありません）。

1. Unity のコンストレイントを、VRChat のコンストレイントに変える。
2. いちばん長いつながりを見つけて、途中の依存を減らし、同時に動ける形に並べ替える。
3. SDK の「パフォーマンスの内訳」で、数字がどう変わったかを確かめる。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版のページ一式と CHANGELOG-jp.md で、「深さ」「ネスト」「階層」「depth」「nest」の語を検索。ヒットは、メニューの階層などコンストレイントと関係のない用法だけでした）。

Convert Constraints は、Unity のコンストレイントを VRChat のコンストレイントに変換するコンポーネントです（[コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count) の MA の節を参照）。ただし、深さへの効果は、MA の公式には書かれていません。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語の公式ドキュメントの Reference・FAQ・チュートリアルなどで、「constraint」「コンストレイント」の語を検索）。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ドキュメントの本文 9 ページと CHANGELOG で、「constraint」「コンストレイント」の語を検索）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、日本語・英語のドキュメントで、「constraint」「コンストレイント」の語を検索）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG で、「nest」「depth」「入れ子」「ネスト」「深さ」の語を検索）。

CHANGELOG には、ネストしたコンストレイントが Trace And Optimize で壊れる不具合の修正があります（1.6.5 の「Nested Constraint can be broken with Trace and Optimize」、1.7.0 の「Re-fix Nested Constraint can be broken with Trace and Optimize」）。これは不具合の修正の記録で、深さを減らす機能の記載ではありません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンストレイントの深さ」を確かめます（上限は PC の Poor が 100、Android の Poor が 50）。
2. 変換の前に、プロジェクトのバックアップを取り、確かめたい動き（対象のアニメーションとコンストレイントの追従）を記録しておきます（公式の勧めではなく、**この記事の助言**{.lbl .lbl-adv}です）。変換を元に戻せるかは、この記事では**未確認**{.lbl .lbl-unk}です。
3. Unity のコンストレイントが含まれているなら、VRChat のコンストレイントに変えます。公式は、次のように書いています（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。
   - 原文: "You can convert all the Unity constraints on your avatar into equivalent VRChat constraints by using the relevant Auto Fix option in the control panel in the SDK."（訳: アバターの Unity のコンストレイントは、SDK の Control Panel にある該当する Auto Fix の項目を使うと、同等の VRChat のコンストレイントに変換できます。）
   - 公式は、Unity のコンストレイントがあると、深さが過大に見積もられることがあると書いています。変換後の動きへの影響は、この記事では**未確認**{.lbl .lbl-unk}です。
4. 変換したあとは、Play モードに入り、対象のアニメーションとコンストレイントの追従が、変換の前と同じかを確かめます。
5. いちばん長いつながりを探します。公式の資料には、その見つけ方が書かれていないため、コンストレイントの設定（どの物をどの物に合わせているか）を確かめて、つながりを追います（この記事の整理）。
6. 途中の依存を減らし、多くのコンストレイントが同時に動けるように並べ替えます（公式の考え方）。
7. 変更のあと、SDK の「パフォーマンスの内訳」で、深さの数字が変わったかを確かめます。

AAO を使う場合も、深さを減らす機能は公式に書かれていないため、手順には入れていません（**公式に記載なし**{.lbl .lbl-none}）。

## 注意・うまくいかないとき

::: warning

- **Android の硬い上限**: Android の硬い上限は、依存の深さ 50 です（公式の Mobile Avatar Component Limits）。公式は、「You cannot bypass the limits above by using "Show Avatar".」（訳: 上の上限は、「Show Avatar」を使っても回避できません）と書いています。
- **並べ替えの具体的な操作**: 公式は、依存を減らすように並べると多くが同時に動けると書いていますが、具体的な操作の手順は書かれていません。この記事でも、具体的な操作は書いていません。コンストレイントの設定を確かめて、つながりを追ってください。
- **Unity のコンストレイントと深さ**: 公式は、次のように書いています。
  - VRChat Constraints の文書: "If your avatar contains any Unity constraints, the constraint depth is likely to be over-estimated, as the category assumes that Unity constraints can only run one at a time."（訳: アバターに Unity のコンストレイントがあると、この項目は、Unity のコンストレイントは一度に 1 つしか動かないと仮定するため、深さが過大に見積もられることがあります。）
  - Optimization Tips: "VRChat will automatically convert Unity constraints to VRChat constraints at runtime."（訳: VRChat は、実行時に Unity のコンストレイントを VRChat のコンストレイントに自動で変換します）。
  - Android Content Limitations: "Unity constraints are disabled completely for avatars on Android and Quest due to complex performance issues. Use VRChat Constraints instead."（訳: Android と Quest では、Unity のコンストレイントは完全に無効になります。VRChat のコンストレイントを使ってください。）
  - これらの関係、特に Android で Unity のコンストレイントが無効になるときに深さがどう数えられるかは、**公式に記載なし**{.lbl .lbl-none}です（確かめた範囲: 上の 3 つの文書）。
- **数値は変わることがあります**: 公式は、推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。
- **AAO の不具合の修正の記録**: CHANGELOG には、自動の MergeBone で、コンストレイントが壊れる不具合の修正もあります（1.5.5 の「Constraints and Animations can be broken with Automatic MergeBone」、1.8.13 の「VRCConstraints solve in local space can be broken with automatic merge bone」）。AAO を使ったあとは、コンストレイントの動きを確かめてください。

:::

## 関連ページ

- [コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
