---
title: コンストレイントの数を減らす
reading: こんすとれいんとのかずをへらす
description: コンストレイントの数の上限と確かめ方、おすすめの減らし方（Unity のコンストレイントを使わず VRChat のコンストレイントを使う）、Modular Avatar の Convert Constraints などのツールの機能をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- コンストレイントの数の上限は、PC の Poor が 350、Android の Poor が 150
- 数えるのは、VRChat のコンストレイントと Unity のコンストレイントを合わせた数
- Unity のコンストレイントは使わず、VRChat のコンストレイントを使うのがおすすめ
:::

コンストレイントの数は、アバターにあるコンストレイント（ある物の位置・回転・スケールを、別の物に紐づける部品）の総数を数える項目です。VRChat のコンストレイントと Unity のコンストレイントを合わせた数です。上限は PC の Poor が 350、Android の Poor が 150 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、次のランク（Very Poor）になります。Android では、硬い上限（150）を超えると、「Show Avatar」を選んでも回避できず、制限の対象のコンポーネントがすべて取り除かれます。PC の既定では、性能を理由にコンポーネントや表示が影響を受けるアバターはありません。ただし、極端に最適化されていないアバターは、使えなくなる場合があります（詳しくは下の注意）。

上限や挙動は、版が上がると変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンストレイントの数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、199（上限 350）と表示されていました。
- **無効にしたものも数えられます。** 無効化されている GameObject とコンポーネントも、パフォーマンスランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。VRChat のコンストレイントも、無効化されたものを含めて数えます（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。使わないコンストレイントは、無効にするだけでは数は減りません。
- **AAO を使うときの確かめ方**: 非破壊のツールを使うと、SDK の Control Panel のランクは実際の値とずれます。実際の値は、Play モードに入ったときの Actual Performance Window で確かめます（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

減らすには、次の 2 点がおすすめです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)、[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。

- **Unity のコンストレイントを使わず、VRChat のコンストレイントを使う。** Unity のコンストレイントは、毎フレーム互いの依存関係で並べ替えられるため、多く同時にあると大きな性能の問題になることがあります。VRChat のコンストレイントは、VRChat のアバター向けに性能が良くなるように作られているので、こちらを使うのがおすすめです。
- **コンストレイントの総数を減らす。** コンストレイントの総数を減らすと、アバターの性能が上がることがあります。

この記事の整理として、減らす方法を次の 3 つに分けます。

1. Unity のコンストレイントを、VRChat のコンストレイントに変える（おすすめ）。
2. 使っていないコンストレイントを取り除く（無効にするだけでは数に残るため）。
3. ツールの機能を使って変換する（MA の Convert Constraints など。下のツール別の方法を見てください）。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**何をするか**: Convert Constraints は、ビルド時に Unity のコンストレイントを VRChat のコンストレイントに非破壊的に変換するコンポーネントです。アタッチされたオブジェクトとその子オブジェクトにあるコンストレイント、およびそれらを参照しているアニメーションを変換します（[Convert Constraints（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/convert-constraints)）。

**どう使うか**: あらかじめ変換するとパフォーマンスが大幅に向上するため、ほとんどの場合はアバタールートにこれを配置するのがおすすめです。

**注意**:

- 変換でどれだけ減るかは、SDK の「パフォーマンスの内訳」で確かめてください。
- 逆に、コンストレイントを増やす機能もあります。Mesh Cutter は、時々非アクティブになる場合に、Constraint を生成することがあります。その場合、パフォーマンスランクの増加に寄与する可能性があるので、注意してください。
- Merge Armature は、必要に応じて ParentConstraint を生成します。
- World Fixed Object は、複数の World Fixed Object コンポーネントを使っても、Constraint は 1 つだけです。
- **未リリース**: MA Reactive Move は、CHANGELOG の [Unreleased]（正式版には入っていません）にある機能です。VRChat では、いくつかの VRC Constraint を生成して、この動作を実現します。

### Avatar Optimizer（AAO） {.tool .tool-aao}

以下は、AAO を使うときの注意です。

- AAO 1.6.12 では、Constraint コンポーネントの Activeness Optimization を削除しました。コンストレイントは、正しく確実に最適化するには複雑すぎるためです。
- AAO 1.7.12 では、Target を持つ VRCConstraints が意図せず削除される問題を修正しました。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンストレイントの数」を確かめます（上限は PC の Poor が 350、Android の Poor が 150）。
2. 変換の前に、プロジェクトのバックアップを取り、確かめたい動き（対象のアニメーションとコンストレイントの追従）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}です）。
3. Unity のコンストレイントがあれば、VRChat のコンストレイントに変えます。SDK の Control Panel にある該当する Auto Fix の項目を使うと、アバターの Unity のコンストレイントを、同等の VRChat のコンストレイントに変換できます（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)）。
   - Unity のコンストレイントと深さの関係は、[コンストレイントの深さを減らす](/learn/optimize-constraint/constraint-depth) の注意を見てください。
4. 変換したあとは、Play モードに入り、対象のアニメーションとコンストレイントの追従が、変換の前と同じかを確かめます。
5. MA を使っているなら、アバターのルートに Convert Constraints を付けます。付けたあと、SDK の「パフォーマンスの内訳」で、数が変わったかを確かめます。
6. 使っていないと確かめたコンストレイントは、取り除きます。無効にするだけでは数に残るためです。
7. 変更のあと、もう一度 SDK の「パフォーマンスの内訳」で確かめます。

## 注意・うまくいかないとき

::: warning

- **Android の上限**: 「Show Avatar」を使っても、上の上限は回避できません。Android の上限を超えると、制限の対象のコンポーネントが、「Show Avatar」を選んでも取り除かれます。コンストレイントの硬い上限は、150 です。
- **Unity のコンストレイント（Android）**: Android と Quest では、性能の問題が複雑なため、Unity のコンストレイントは完全に無効になります。VRChat のコンストレイントを使うのがおすすめです（Android Content Limitations）。Unity のコンストレイントは、アバターがゲーム内で読み込まれるときや、実行時に、VRChat のコンストレイントに自動で変換されます（[Constraints（VRChat 公式）](https://creators.vrchat.com/common-components/constraints)、[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。
- **PC の既定**: PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。既定では、性能を理由にコンポーネントや表示が影響を受けるアバターはありません。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。表示される最低のランクの設定を変えた場合は、「Constraint Count or Depth」の行（All Constraint components removed）の制限が働きます。
- **数値は変わることがあります**: Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があります。

:::

## 関連ページ

- [コンストレイントの深さを減らす](/learn/optimize-constraint/constraint-depth)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
