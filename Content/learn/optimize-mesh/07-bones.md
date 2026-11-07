---
title: ボーンの数を減らす
reading: ぼーんのかずをへらす
description: ボーン（骨組み）の数の上限と確かめ方、Modular Avatar・Avatar Optimizer でボーンをまとめる・消す機能と注意点をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- ボーンの上限は、PC が 400、Android が 150
- 使わないボーンは、削除して親のボーンに統合することを検討するのがおすすめです
- この記事の整理として、動くボーンは残し、動かないボーンだけを減らす
- OSC を使うアバターは、自動の最適化のあとに PhysBone と Contact が効くか確かめる（この記事の助言）
:::

ボーンは、アバターのリグ（骨組み）の骨の数を数える項目です。パフォーマンスの内訳では、上限は PC が 400、Android が 150 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。Poor の上限を超えると、その項目がランクを下げる原因になります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「ボーンの数」で、今の値と上限を見ます。
- ボーンを減らすときは、そのボーンが動いているかを確かめます。AAO の基本的な使い方（[チュートリアル](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）では、アニメーションしたり PhysBone で揺らしたりすることのないボーンを統合できます。同じチュートリアルで挙げられている、服のボーンを素体のボーンに入れ子状にして着せる場合は、それ自身を動かすことがないボーンが多く発生します。
- 動いているボーン（アニメーションや PhysBone で動くもの）は、残す判断が必要です。

## 減らし方の考え方

- 使わないボーンは、削除して親のボーンに統合することを検討するのがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表にも同じ内容があり、出典は [Avatar Optimization Tips](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

次の 3 点は、この記事の整理です。

- 減らし方は、ツールによって対象が違います。衣装のボーンを本体のボーンにまとめる（Modular Avatar）、使われていない Object を自動で消す（AAO の Trace And Optimize）、個別の GameObject を親にまとめる（AAO Merge Bone）、の 3 つに分けられます。
- まとめる操作でも、ボーンが増えることがあります。Modular Avatar の Merge Armature は、統合先にないボーンを名前を変えて配置したり、子にボーンを追加したりすることがあります。統合の前に名前を揃えておくと、一致するボーンがまとめられる対象になります。
- 動くボーンは残し、動かないボーンだけを減らします。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

- **何をするか**: Merge Armature は、GameObject のツリー（主に衣装のアーマチュア）を、アバターのアーマチュアに統合するコンポーネントです。衣装アセット専用で、追加されるボーンを最小限にする特殊な処理が入っています。
- **どう使う**: 統合先と名前が一致するボーンは、統合先にまとめられます。統合元にあって統合先にないボーンは、名前を変えて配置されます。連鎖的に統合することも、1.7.0 以降はできます。
- **注意**: 該当するボーンがない場合や、統合元のボーンへの引用を統合先のボーンへ書き換えられない場合は、子にボーンが追加されます。Transform 以外のコンポーネントが入っているボーンは残り、そのほかのボーンは原則として統合後に削除されます。既存のボーンを再利用して、作られるボーンを最小限にします（MA の README）。
- **Bone Proxy**: プレハブ化したオブジェクトを、元のアバターのボーンの中に配置する機能です。衣装向けのものではありません。
- 出典: [Merge Armature（Modular Avatar 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature)、[Bone Proxy（Modular Avatar 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/bone-proxy)

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **Trace And Optimize の「使われていないObjectを自動的に削除する」**: アニメーションなどを走査して、使われていない GameObject・コンポーネント・テクスチャなどを自動で削除します。ボーンを自動で減らす方法の一つが、この機能です。
  - この項目の中の「end ボーンを残す」（Preserve EndBone）: 親が削除されていない end ボーンを削除しないようにします。名前が end（大文字小文字の区別なし）で終わるボーンを end ボーンとして扱います（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **Trace And Optimize の「PhysBone を最適化する」**: Endpoint Position で問題なく表現できる場合に、end ボーン（PhysBone の末端のボーン）を削除して、Endpoint Position に置き換えます。この置き換えで、PhysBone Affected Transforms の数を減らせます。これは SDK の「ボーンの数」とは別の項目です（[PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)）。
- **AAO Merge Bone（手動）**: GameObject を親の GameObject に統合して、元の GameObject を取り除きます。統合対象に付いているほかのコンポーネントも、削除されます。「名前の競合を避ける」を使うと、子の GameObject の名前を変えて、アニメーションの破損を防げます。親にも Merge Bone があれば、さらに親に統合されます。Trace And Optimize が似た処理を自動で行うため、大抵の場合は使う必要はありません。
- **UnusedBonesByReferencesTool**: 非推奨です。代わりに、Trace And Optimize の「使われていないObjectを自動的に削除する」を使うのがおすすめです。
- 出典: [Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)、[Merge Bone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-bone/)

## 手順

1. SDK の「パフォーマンスの内訳」で、「ボーンの数」を確かめます（上限は PC 400、Android 150）。
2. 動くボーン（アニメーションや PhysBone で動くもの）と、動かないボーンを分けます。
3. 衣装を入れるなら、Modular Avatar の Merge Armature で本体に統合します。衣装の組み込みの手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。統合のあと、ボーンが増えていないかを確かめます。
4. アバターのルートに AAO Trace And Optimize を付けます。「使われていないObjectを自動的に削除する」などの項目を、チェックボックスで選びます。OSC を使うギミックがあるときは、先に後ろの「注意・うまくいかないとき」を読んでください。
5. 個別にまとめたい GameObject があれば、AAO Merge Bone を付けます。「名前の競合を避ける」の設定を確かめてから使います。
6. Build & Test で、見た目と動きが変わっていないかを確かめます。AAO を使ったあとは、Control Panel のランクはあてになりません。ランクは、Play モードでの確かめ方（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）で確かめてください。

## 注意・うまくいかないとき

- **OSC で使う PhysBone・Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、他の用途で使われていなければ削除します。これは「使われていないObjectを自動的に削除する」の働きです。OSC で使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定する方法があります（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。OSC を使うアバターでは、自動の最適化のあとに、PhysBone と Contact が効くかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
- **ボーンの統合で動きが変わることがある**: CHANGELOG には、版ごとに次の記録があります。AAO 1.9.20 では、スケールが 100 倍のとき 3mm 以内のボーンが誤って統合されることがある問題がありました（修正の内容は CHANGELOG を見てください）。AAO 1.9.15 では、Merge Bone が z-fighting（面がちらつく現象）を起こすことがある問題を修正しました。AAO 1.8.3 では、C# の名前付きプロパティで Transform をアニメーションすると Merge Bone で壊れる問題を修正しました。
- **統合で、より多くのボーンが統合されるようになった**: AAO 1.7.5 では、Trace And Optimize が以前より多くのボーンを統合するようになりました。
- **Merge PhysBone**: 統合したあとは、掴めるのは 1 か所だけになります（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。また、Merge PhysBone を 1 つ使うごとに、PhysBone Affected Transforms の数が 1 増えます（ルートの GameObject も数えるため）。PhysBone の項目を減らす目的では、この増え方に注意してください。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)
- [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds)
