---
title: コンストレイントの深さを減らす
reading: こんすとれいんとのふかさをへらす
description: コンストレイントの深さ（依存関係のいちばん長いつながり）の上限と確かめ方、減らし方の考え方（いちばん長いつながりを短くする、Unity のコンストレイントを VRChat のものに変える）をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- コンストレイントの深さの上限は、PC の Poor が 100、Android の Poor が 50
- 深さは合計ではなく、アバター全体でいちばん長い依存のつながりで決まる
- いちばん長いつながりを短くすると、深さが下がる
:::

コンストレイントの深さは、アバターにあるコンストレイントの依存関係（あるコンストレイントが、別のコンストレイントの結果に依存しているつながり）のうち、いちばん長いつながりの段数です。例えば、つながりに 3 つのコンストレイントがあれば、そのつながりの深さは 3 です。上限は PC の Poor が 100、Android の Poor が 50 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、コンストレイントの数と同じです。Android では、硬い上限（50）を超えると、「Show Avatar」を選んでも回避できず、制限の対象のコンポーネントが取り除かれます。詳しくは [コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count) を見てください。

「依存のつながり」は、英語の dependencies にあたる言葉です。この記事では、「あるコンストレイントが、別のコンストレイントの結果に依存しているつながり」と言い換えています。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンストレイントの深さ」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、34（上限 100）と表示されていました。
- **深さは合計ではなく、いちばん長いつながりです。** いくつかの腕があって、それぞれの深さが 3 でも、ほかに長いつながりがなければ、アバター全体の深さは 3 のままです。

## 減らし方の考え方

減らすには、次の 2 つの考え方がおすすめです（[Constraints](https://creators.vrchat.com/common-components/constraints)）。

- **いちばん長いつながりを短くする。** コンストレイントの深さは、いちばん長いつながりの長さを減らすと下がります。
- **依存を減らして、同時に動けるようにする。** コンストレイントどうしの依存を減らすように並べると、多くのコンストレイントを同時に動かせます。順番に動かさなければならない場合より、性能が良くなります。

深さを正しく計算できるのは、VRChat のコンストレイントだけを使っているときです。

この記事の整理として、次の 3 段階で考えます。

1. Unity のコンストレイントを、VRChat のコンストレイントに変える。
2. いちばん長いつながりを見つけて、途中の依存を減らし、同時に動ける形に並べ替える。
3. SDK の「パフォーマンスの内訳」で、数字がどう変わったかを確かめる。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**何をするか**: Convert Constraints は、ビルド時に Unity のコンストレイントを VRChat のコンストレイントに非破壊的に変換するコンポーネントです。アタッチされているオブジェクトとその子オブジェクトにあるコンストレイントを対象にします。

ツールの詳しい説明と注意は、[コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count) の MA の節を見てください。

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO 1.6.5 以降は、ネストしたコンストレイントが Trace And Optimize で壊れる不具合が修正されています。1.7.0 以降は、その修正がやり直されています。AAO を使うときは、深さが変わっていないかを確かめてください。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンストレイントの深さ」を確かめます（上限は PC の Poor が 100、Android の Poor が 50）。
2. 変換の前に、プロジェクトのバックアップを取り、確かめたい動き（対象のアニメーションとコンストレイントの追従）を記録しておきます（**この記事の助言**{.lbl .lbl-adv}です）。
3. Unity のコンストレイントが含まれているなら、VRChat のコンストレイントに変えます。SDK の Control Panel にある該当する Auto Fix の項目を使うと、アバターの Unity のコンストレイントを、同等の VRChat のコンストレイントに変換できます（[Constraints](https://creators.vrchat.com/common-components/constraints)）。
   - Unity のコンストレイントがあると、深さが過大に見積もられることがあります（下の注意を見てください）。
4. 変換したあとは、Play モードに入り、対象のアニメーションとコンストレイントの追従が、変換の前と同じかを確かめます。
5. いちばん長いつながりを探します。コンストレイントの設定（どの物をどの物に合わせているか）を確かめて、つながりを追います（この記事の整理）。
6. 途中の依存を減らし、多くのコンストレイントが同時に動けるように並べ替えます（上の「減らし方の考え方」）。
7. 変更のあと、SDK の「パフォーマンスの内訳」で、深さの数字が変わったかを確かめます。

## 注意・うまくいかないとき

::: warning

- **Android の硬い上限**: Android の硬い上限は、依存の深さ 50 です（Mobile Avatar Component Limits）。「Show Avatar」を使っても、上の上限は回避できません。
- **並べ替え**: 依存を減らすように並べると、多くのコンストレイントが同時に動けます。並べ替えは、コンストレイントの設定を確かめて、つながりを追ってから決めてください。
- **Unity のコンストレイントと深さ**:
  - Unity のコンストレイントがあると、この項目は、Unity のコンストレイントは一度に 1 つしか動かないと仮定するため、深さが過大に見積もられることがあります（[Constraints](https://creators.vrchat.com/common-components/constraints)）。
  - Android・Quest 以外では、VRChat は、実行時に Unity のコンストレイントを VRChat のコンストレイントに自動で変換します（[Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。
  - Android と Quest では、Unity のコンストレイントは完全に無効になります。VRChat のコンストレイントを使うのがおすすめです（[Quest Content Limitations](https://docs.vrchat.com/docs/quest-content-limitations)）。
- **数値は変わることがあります**: Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があります。
- **AAO の不具合の修正の記録**: AAO 1.5.5 以降は、自動の MergeBone で、コンストレイントとアニメーションが壊れる不具合が修正されています。1.8.13 以降は、ローカル空間で解く VRCConstraints が、自動の merge bone で壊れる不具合が修正されています。AAO を使ったあとは、コンストレイントの動きを確かめてください。

:::

## 関連ページ

- [コンストレイントの数を減らす](/learn/optimize-constraint/constraint-count)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
