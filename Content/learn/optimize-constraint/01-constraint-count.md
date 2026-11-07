---
title: コンストレイントの数を減らす
reading: こんすとれいんとのかずをへらす
description: コンストレイントの数の上限と確かめ方、VRChat 公式の勧め（Unity のコンストレイントを使わず VRChat のコンストレイントを使う）、Modular Avatar の Convert Constraints などのツールの機能を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- コンストレイントの数の上限は、PC の Poor が 350、Android の Poor が 150
- 数えるのは、VRChat のコンストレイントと Unity のコンストレイントを合わせた数
- 公式は、Unity のコンストレイントを使わず、VRChat のコンストレイントを使うよう勧めている
:::

コンストレイントの数は、アバターにあるコンストレイント（ある物の位置・回転・スケールを、別の物に紐づける部品）の総数を数える項目です。VRChat 公式の定義は "The total number of VRChat constraints and Unity constraints on the avatar." で、VRChat のコンストレイントと Unity のコンストレイントを合わせた数です。上限は PC の Poor が 350、Android の Poor が 150 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、公式では次のランク（Very Poor）になると書かれています。Android では、ハード上限（150）を超えると、「Show Avatar」を選んでも回避できず、制限の対象のコンポーネントがすべて取り除かれると公式は書いています。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は**未確認**{.lbl .lbl-unk}）、TexTransTool v1.0.2、lilAvatarUtils 2.1.2、Meshia Mesh Simplification（main）の公式の資料を、2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は英語の原文に、日本語の訳を添えています。VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンストレイントの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、199（上限 350）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。VRChat のコンストレイントの文書も、無効化されたものを含めて数えると書いています（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。使わないコンストレイントは、無効にするだけでは数は減りません。
- **AAO を使うときの確かめ方**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられます（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。ただし、コンストレイントの数だけを表示する方法は、公式の資料に書かれていません（**公式に記載なし**{.lbl .lbl-none}）。

## 減らし方の考え方

VRChat 公式の勧めは、次の 2 点です（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)、[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。

- **Unity のコンストレイントを使わず、VRChat のコンストレイントを使う。** 公式の見出しは「Do not use Unity Constraints!」です。原文は "The engine's constraints are sorted based on the dependencies between them every frame, which means they can cause significant performance problems when enough of them exist at once. Use VRChat Constraints instead, because they're designed to provide better performance in the context of VRChat avatars." です。訳は、Unity のコンストレイントは、毎フレーム互いの依存関係で並べ替えられるため、多く同時にあると大きな性能の問題になることがある。VRChat のコンストレイントは、VRChat のアバター向けに性能が良くなるように作られているので、そちらを使う、という意味です。
- **コンストレイントの総数を減らす。** 公式は "Decreasing the total number of constraints can improve your avatar's performance." と書いています（訳: コンストレイントの総数を減らすと、アバターの性能が上がることがあります）。

この記事の整理として、減らす方法を次の 3 つに分けます（公式の分類ではありません）。

1. Unity のコンストレイントを、VRChat のコンストレイントに変える（公式の勧め）。
2. 使っていないコンストレイントを取り除く（無効にするだけでは数に残るため）。
3. ツールの機能を使って変換する（MA の Convert Constraints など。下のツール別の方法を見てください）。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**何をするか**: Convert Constraints は、ビルド時に Unity のコンストレイントを VRChat のコンストレイントに変換するコンポーネントです。公式の日本語版のページには、「Convert Constraintsコンポーネントは、ビルド時にUnityのConstraintsをVRChatのConstraintに非破壊的に変換するようにModular Avatarに指示します。」「アタッチされているオブジェクトとその子オブジェクトにあるConstraint、およびそれらを参照しているアニメーションを変換します。」と書かれています（[Convert Constraints（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/convert-constraints)）。

**どう使うか**: 公式は「あらかじめ変換するとパフォーマンスが大幅に向上するため、ほとんどの場合はアバタールートにこれを配置するのが良いでしょう。」と書いています。

**注意**:

- 「大幅に向上」は定性的な書き方で、変換前後のコンストレイントの数や、性能の数値は書かれていません。この記事でも数値は書きません。変換で数がどれだけ減るかは、SDK の「パフォーマンスの内訳」で確かめてください。
- 逆に、コンストレイントを増やす機能もあります。Mesh Cutter は、時々非アクティブになる場合に、「場合によっては、Constraint を生成する必要があり、パフォーマンスランクの増加に寄与する可能性がありますのでご注意ください。」と書かれています。Merge Armature は、必要に応じて ParentConstraint が生成されると書かれています。
- World Fixed Object は、「複数のWorld Fixed Objectコンポーネントを使っても、Constraintは一つだけです。」と書かれています。ただし、これは 1 つのコンポーネントについての記述で、アバター全体の数の上限ではありません。
- **未リリース**: MA Reactive Move は、CHANGELOG の [Unreleased]（正式版には入っていません）にある機能です。公式は「VRChat では、いくつかの VRC Constraint を生成して、この動作を実現します。」と書いています。生成される数は「いくつか」とだけ書かれています。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語の公式ドキュメントの Reference・FAQ・チュートリアルなど、「constraint」「コンストレイント」の語を検索）。英語版のサイトは**未確認**{.lbl .lbl-unk}です。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ドキュメントの本文 9 ページと CHANGELOG で、「constraint」「コンストレイント」の語を検索。コンストレイントの数を確かめるタブの記載も見つかりませんでした）。英語版は**未確認**{.lbl .lbl-unk}です。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README（英語・日本語）と、日本語・英語のドキュメントで、「constraint」「コンストレイント」の語を検索）。公式サイトの表示は**未確認**{.lbl .lbl-unk}です。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG で、「constraint」「コンストレイント」の語を検索）。

**注意**:

- 「使われていないObjectを自動的に削除する」の原文は「アニメーションなどを走査して、使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します。」です。コンストレイントがこの対象に入るかは、**公式に記載なし**{.lbl .lbl-none}です。
- CHANGELOG の 1.6.12 では、`Removed` に「Activeness Optimization for Constraint component」があります。理由は「The constraint component is too complex to optimize correctly and reliably」（訳: コンストレイントは、正しく確実に最適化するには複雑すぎる）と書かれています。
- CHANGELOG の 1.7.12 には、「VRConstraints with Target might be removed unexpectedly」（意図せず削除される）の修正があります。これは不具合の修正の記録で、削除の対象だと示すものではありません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンストレイントの数」を確かめます（上限は PC の Poor が 350、Android の Poor が 150）。
2. 変換の前に、プロジェクトのバックアップを取り、確かめたい動き（対象のアニメーションとコンストレイントの追従）を記録しておきます（公式の勧めではなく、**この記事の助言**{.lbl .lbl-adv}です）。変換を元に戻せるかは、この記事では**未確認**{.lbl .lbl-unk}です。
3. Unity のコンストレイントがあれば、VRChat のコンストレイントに変えます。公式は、次のように書いています（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。
   - 原文: "You can convert all the Unity constraints on your avatar into equivalent VRChat constraints by using the relevant Auto Fix option in the control panel in the SDK."（訳: アバターの Unity のコンストレイントは、SDK の Control Panel にある該当する Auto Fix の項目を使うと、同等の VRChat のコンストレイントに変換できます。）
   - 変換後の動きへの影響は、この記事では**未確認**{.lbl .lbl-unk}です。詳しくは [コンストレイントの深さを減らす](/learn/optimize-constraint/constraint-depth) の注意も見てください。
4. 変換したあとは、Play モードに入り、対象のアニメーションとコンストレイントの追従が、変換の前と同じかを確かめます。
5. MA を使っているなら、アバターのルートに Convert Constraints を付けます。付けたあと、SDK の「パフォーマンスの内訳」で、数が変わったかを確かめます。
6. 使っていないと確かめたコンストレイントは、取り除きます。無効にするだけでは数に残るためです（公式の注記による）。
7. 変更のあと、もう一度 SDK の「パフォーマンスの内訳」で確かめます。

## 注意・うまくいかないとき

::: warning

- **Android の上限**: 公式は、「You cannot bypass the limits above by using "Show Avatar".」（訳: 上の上限は、「Show Avatar」を使っても回避できません）と書いています。Android の上限を超えると、制限の対象のコンポーネントが、「Show Avatar」を選んでも取り除かれます。コンストレイントの硬い上限は、150 です。
- **Unity のコンストレイント（Android）**: 公式の 3 つの文書は、次のように書いています。Android で Unity のコンストレイントが無効になるとき、それが上限の数えられ方にどう影響するかは、**公式に記載なし**{.lbl .lbl-none}です（確かめた範囲: この 3 文書）。原文を並べます。
  - VRChat Constraints の文書: "Unity constraints are automatically converted into VRChat constraints when your avatar is loaded in-game."（訳: Unity のコンストレイントは、アバターがゲーム内で読み込まれるときに、VRChat のコンストレイントに自動で変換されます。）
  - Optimization Tips: "VRChat will automatically convert Unity constraints to VRChat constraints at runtime."（訳: VRChat は、実行時に Unity のコンストレイントを VRChat のコンストレイントに自動で変換します。）
  - Android Content Limitations: "Unity constraints are disabled completely for avatars on Android and Quest due to complex performance issues. Use VRChat Constraints instead."（訳: Android と Quest では、性能の問題が複雑なため、Unity のコンストレイントは完全に無効になります。VRChat のコンストレイントを使ってください。）
- **PC の既定**: 公式の「PC Default Performance Rank Blocking」は、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書いています。また、「Minimum Displayed Performance Rank on PC」の節には、既定では性能を理由にコンポーネントや表示が影響を受けないと書かれています（原文: "no avatars will have their components or display affected for performance reasons on PC"）。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています。この設定を変えた場合は、「Constraint Count or Depth」の行（All Constraint components removed）の制限が働きます。
- **AAO**: AAO の Trace And Optimize で、コンストレイントの数を減らす機能は、公式に書かれていません（**公式に記載なし**{.lbl .lbl-none}）。そのため、AAO は、この記事の手順には入れていません。
- **数値は変わることがあります**: 公式は、Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。この記事の数値は、2026 年 11 月 6 日時点のものです。

:::

## 関連ページ

- [コンストレイントの深さを減らす](/learn/optimize-constraint/constraint-depth)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
