---
title: ライトの数を減らす
reading: らいとのかずをへらす
description: アバターにある Light コンポーネントの数の上限と、Android で無効になる点、おすすめの減らし方をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- ライトの数の上限は、PC の Poor が 1（Excellent・Good・Medium は 0）
- Android と Quest では、ライトは完全に無効になる
- アバターでどの種類のライトも使わないのがおすすめ
:::

ライトの数は、アバターにある Light コンポーネントの数を数える項目です。上限は PC の Poor が 1 です（Excellent・Good・Medium は 0）（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。モバイル（Android・iOS。Quest を含む）では、ライトは無効になります。アバターに表示されることがないためです（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。Android と Quest では、ライトは完全に無効になります（Android Content Limitations）。

上限を超えると、表示される最低のランクを設定しているときに、すべてのライトが取り除かれます（ランキングの表の「All Lights removed」）。ランキングの例では、上限を超えたためにライトとパーティクルシステムが無効になります。

上限や挙動は、版が上がると変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「ライトの数」で、今の値と上限を見ます。例の画面では、1（上限 1）と表示されていました。上限ちょうどのため、Poor の範囲になります。
- **無効にしたものも数えられます。** 無効化されている GameObject とコンポーネントも、パフォーマンスランクに数えられます（[パフォーマンスランク（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。ライトを消すだけでは、数は減りません。
- **PC の既定の設定**: PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなる場合があります。上限を超えたライトが取り除かれるのは、表示される最低のランクを設定しているときです。
- **AAO を使うときの数**: 非破壊のツールを使うと、Control Panel のパフォーマンスランクは実際の値とずれます（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。Play モードに入ったときのランクは、anatawa12's Gist Pack の Actual Performance Window で確かめます（[AAO の結果の確認](/learn/optimize-aao/avatar-optimizer) を見てください）。

## 減らし方の考え方

ライトについて、次の点を押さえてください（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips/)）。

- アバターのライトはリアルタイムで計算されるため、非常に負荷が大きいです。
- ライトを追加すると、そのライトが当たるものは、描画の呼び出し（ドローコール）が 2 倍になります。
- 常に点いているライトは使わないのがおすすめです。懐中電灯のように点けたり消したりするなら、Animation Override を使うのがおすすめです。ライトをまったく使わないのも、おすすめです。
- ライトを使うなら、そのライトの Shadows はオフにするのがおすすめです。
- パーティクルシステムで一部のパーティクルにライトを付ける設定は、絶対に使わないのがおすすめです。
- 全体として、アバターにどの種類のライトも使わないのがおすすめです。

無効化されたものも数えます（上の「減らす前に確かめること」）。そのため、消灯だけでは数は減りません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「ライトの数」を確かめます（上限は PC の Poor が 1。Android では無効）。
2. アバターで使っているライトを確かめます。見た目に必要ないライトは、Light コンポーネントを外します。ライトをまったく使わないのがおすすめです。
3. 残すライトは、常に点いたままにしないようにします。懐中電灯のように必要なときだけ点けたい場合は、Animation Override で点灯と消灯を切り替えます。ただし、無効化されたものも数えるため、消灯だけでは Light コンポーネントの数は減りません（上の「減らす前に確かめること」を見てください）。
4. 残すライトで、影（Shadows）が必要ないものは、Shadows をオフにします（おすすめです）。
5. パーティクルシステムのライトの設定は使わないようにします（絶対に使わないのがおすすめです）。
6. 処理のあと、SDK の「ライトの数」と、Play モードでの Actual Performance Window の値を比べます。AAO を使うときは、処理の前後で Light の数を確かめてください。

## 注意・うまくいかないとき

::: warning
- **Android と Quest ではライトは無効**: Android と Quest のアバターでは、ライトは完全に無効になります（Android Content Limitations）。Android 向けのアバターでは、ライトを当てにしないでください。
- **無効にしても数は減りません**: 無効化されたものも数えます。
- **AAO を使うときは**: 先に OSC の注意（[よくある質問（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）を読んでください。AAO は、OSC（外部ツールと値をやりとりする仕組み）で使われている PhysBone や Contact Receiver を判別できないため、Asset Description の設定が必要になることがあります。
- **パーティクルのライト**: ライトの付いたパーティクルは、それぞれがリアルタイムライトとして扱われ、非常に負荷が大きくなります（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips/)）。
:::

## 関連ページ

- [アニメーターの数を減らす](/learn/optimize-constraint/animators)
- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
