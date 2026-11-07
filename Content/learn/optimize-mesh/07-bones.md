---
title: ボーンの数を減らす
reading: ぼーんのかずをへらす
description: ボーン（骨組み）の数の上限と確かめ方、Modular Avatar・Avatar Optimizer でボーンをまとめる・消す機能と注意点を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- ボーンの上限は、PC が 400、Android が 150
- 公式の勧めは、使わないボーンを削除して親のボーンに統合することを検討すること
- この記事の整理として、動くボーンは残し、動かないボーンだけを減らす
- OSC を使うアバターは、自動の最適化のあとに PhysBone と Contact が効くかを確かめる（この記事の助言）
:::

ボーンは、アバターのリグ（骨組み）の骨の数を数える項目です。パフォーマンスの内訳では、上限は PC が 400、Android が 150 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。Poor の上限を超えると、その項目がランクを下げる原因になります。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21 と、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「ボーンの数」で、今の値と上限を見ます。
- ボーンを減らすときは、そのボーンが動いているかを確かめます。AAO の公式の基本的な使い方（[チュートリアル](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）は、アニメーションしたり PhysBone で揺らしたりすることのないボーンを統合できる例として挙げています。同じチュートリアルには、服のボーンを素体のボーンに入れ子状にして着せるような場合は、それ自身を動かすことがないボーンが多く発生する、と書かれています。
- 動いているボーン（アニメーションや PhysBone で動くもの）は、残す判断が必要です。

## 減らし方の考え方

- 公式の勧め（Avatar Optimization Tips。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表）は、使わないボーンは、削除して親のボーンに統合することを検討する、というものです。

次の 3 点は、この記事の整理です（公式の分類ではありません）。

- 減らし方は、ツールによって対象が違います。衣装のボーンを本体のボーンにまとめる（Modular Avatar）、使われていない Object を自動で消す（AAO の Trace And Optimize）、個別の GameObject を親にまとめる（AAO Merge Bone）、の 3 つに分けられます。
- まとめる操作でも、ボーンが増えることがあります。Modular Avatar の Merge Armature は、統合先にないボーンを名前を変えて配置したり、子にボーンを追加したりすることがあります。統合の前に名前を揃えておくと、一致するボーンがまとめられる対象になります。
- 動くボーンは残し、動かないボーンだけを減らします。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

- **何をするか**: Merge Armature は、GameObject のツリー（主に衣装のアーマチュア）を、アバターのアーマチュアに統合するコンポーネントです。衣装アセット専用で、追加されるボーンを最小限にする特殊な処理が入っていると書かれています。
- **どう使う**: 統合先と名前が一致するボーンは、統合先にまとめられます。統合元にあって統合先にないボーンは、名前を変えて配置されます。連鎖的に統合することも、1.7.0 以降はできます。
- **注意**: 該当するボーンが見つからない場合や、引用を書き換えられない場合は、子にボーンが追加されると、公式に書かれています。Transform 以外のコンポーネントが入っているボーンは残り、そのほかのボーンは原則として統合後に削除されます。MA の README には、既存のボーンを再利用して、作られるボーンを最小限にする、と書かれています。
- **Bone Proxy**: プレハブ化したオブジェクトを、元のアバターのボーンの中に配置する機能です。衣装向けのものではないと書かれています。ボーンの数との関係は、**公式に記載なし**{.lbl .lbl-none}です（確かめた範囲: Bone Proxy のページ）。
- 出典: [Merge Armature（Modular Avatar 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature)、[Bone Proxy（Modular Avatar 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/bone-proxy)

### TexTransTool（TTT） {.tool .tool-ttt}

- **公式に記載なし**{.lbl .lbl-none}（確かめた範囲: Reference のコンポーネント一覧、AtlasTexture のページ、FAQ）: ボーンの数に関する記載はありません。

### lilAvatarUtils {.tool .tool-lil}

- **Quality はボーンの数ではありません**: レンダラーのタブの Quality は、1 つの頂点を操作できるボーンの数です。数値が大きいほど計算量が増え、パフォーマンスが悪化する可能性があると書かれています。基本的には Auto に設定し、制限したいときは、モデルのインポート設定を変更することがすすめられています。
- **ボーンの数を減らす機能**: **公式に記載なし**{.lbl .lbl-none}です（確かめた範囲: レンダラー・マテリアル・テクスチャ・ツールの各タブ）。
- 出典: [レンダラー（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/RenderersGUI.html)

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

- **公式に記載なし**{.lbl .lbl-none}（確かめた範囲: README（英語・日本語）、公式ドキュメントの日本語版のページ（はじめに・インストール・VRChat 向けのページ））: ボーンの数に関する記載はありません。

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **Trace And Optimize の「使われていないObjectを自動的に削除する」**: アニメーションなどを走査して、使われていない GameObject・コンポーネント・テクスチャなどを自動で削除します。ボーンを自動で減らす方法の一つが、この機能です。対象の種類ごとの細かい条件は、**未確認**{.lbl .lbl-unk}です。
  - この項目の中の「end ボーンを残す」（Preserve EndBone）: 親が削除されていない end ボーンを削除しないようにします。公式では、名前が end（大文字小文字の区別なし）で終わるボーンを end ボーンとして扱います（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **Trace And Optimize の「PhysBone を最適化する」**: Endpoint Position で問題なく表現できる場合に、end ボーン（PhysBone の末端のボーン）を削除して、Endpoint Position に置き換えます。公式の Replace End Bone With Endpoint Position のページは、この置き換えで PhysBone Affected Transforms の数を減らせると書いています。ボーンの数への効果は、**未確認**{.lbl .lbl-unk}です。
- **AAO Merge Bone（手動）**: GameObject を親の GameObject に統合して、元の GameObject を取り除きます。統合対象に付いているほかのコンポーネントも、削除されます。「名前の競合を避ける」を使うと、子の GameObject の名前を変えて、アニメーションの破損を防げます。親にも Merge Bone があれば、さらに親に統合されます。Trace And Optimize が似た処理を自動で行うため、大抵の場合は使う必要はないと書かれています。
- **UnusedBonesByReferencesTool**: 非推奨です。代わりに、Trace And Optimize の「使われていないObjectを自動的に削除する」を使うよう書かれています。
- 出典: [Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)、[Merge Bone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-bone/)

## 手順

1. SDK の「パフォーマンスの内訳」で、「ボーンの数」を確かめます（上限は PC 400、Android 150）。
2. 動くボーン（アニメーションや PhysBone で動くもの）と、動かないボーンを分けます。
3. 衣装を入れるなら、Modular Avatar の Merge Armature で本体に統合します。衣装の組み込みの手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。統合のあと、ボーンが増えていないかを確かめます。
4. アバターのルートに AAO Trace And Optimize を付けます。「使われていないObjectを自動的に削除する」などの項目を、チェックボックスで選びます。OSC を使うギミックがあるときは、先に後ろの「注意・うまくいかないとき」を読んでください。
5. 個別にまとめたい GameObject があれば、AAO Merge Bone を付けます。「名前の競合を避ける」の設定を確かめてから使います。
6. Build & Test で、見た目と動きが変わっていないかを確かめます。AAO を使ったあとは、Control Panel のランクはあてにならないと公式に書かれています。ランクは、Play モードでの確かめ方（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）で確かめてください。

## 注意・うまくいかないとき

- **OSC で使う PhysBone・Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、他の用途で使われていなければ削除するよう設計されています。これは「使われていないObjectを自動的に削除する」の働きです。公式 FAQ（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）では、Asset Description ファイルを作り、Parameters Read By External Tools に設定する方法が書かれています。OSC を使うアバターでは、自動の最適化のあとに、PhysBone と Contact が効くかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
- **ボーンの統合で動きが変わることがある**: CHANGELOG には、版ごとに次の記録があります。1.9.20 には、スケールが 100 倍のとき 3mm 以内のボーンが誤って統合されることがある、と書かれています（修正の内容は CHANGELOG を見てください）。1.9.15 には、Merge Bone が z-fighting（面がちらつく現象）を起こすことがあり、修正したと書かれています。1.8.3 には、C# の名前付きプロパティで Transform をアニメーションすると、Merge Bone で壊れる問題を修正したと書かれています。
- **統合で、より多くのボーンが統合されるようになった**: CHANGELOG の 1.7.5 に、Trace and Optimize は以前より多くのボーンを統合するようになったと書かれています。
- **Merge PhysBone**: 統合したあとは、掴めるのは 1 か所だけになります（公式の原文は「統合後は1つのボーンしか掴むことができません」。[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。また、Merge PhysBone を 1 つ使うごとに、PhysBone Affected Transforms の数が 1 増えます（ルートの GameObject も数えるため）。PhysBone の項目を減らす目的では、この増え方に注意してください。
- **MA と AAO の順序**: 併用したときの順序と衝突は、この記事では**未確認**{.lbl .lbl-unk}です。想定どおりに動くかを、実際に確かめてください。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)
- [範囲（BOUNDS）の確かめ方と注意](/learn/optimize-mesh/bounds)
