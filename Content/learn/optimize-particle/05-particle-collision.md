---
title: パーティクルの衝突を使わないようにする
reading: ぱーてぃくるのしょうとつをつかわないようにする
description: パーティクルの衝突（Particle Collision）が、VRChat のパフォーマンスランクでどう数えられるか（True / False の項目）、上限と確かめ方、VRChat 公式の勧め、各ツールで公式に記載があるかを、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 衝突は数ではなく True か False で、1 つでも Particle Collision が有効なら True
- 上限の表記は True / True（PC の Medium・Android の Poor は表からの読み取り）
- 公式の勧めは、大量の粒子を性能を保って使う方法の一つとして、衝突を使わないこと
- 衝突をオフにしたときの見た目への影響は、公式の資料に記載がなく未確認
:::

パーティクルの衝突は、パーティクルシステムの粒子が、ほかの物とぶつかるかどうかを計算する設定（Particle Collision）です。パフォーマンスランクでは数字ではなく、True（あり）か False（なし）で数えます。VRChat 公式の定義は "If any Particle Systems on the avatar have Particle Collision enabled, this will be True."（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）で、訳は「アバターのパーティクルシステムのどれか 1 つでも Particle Collision が有効なら、True になる」です。

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表では「True / True」と書かれています。公式の表（上と同じページ）を読むと、True のとき PC は Medium、Android は Poor の列に当たります。これは表からの読み取りで、公式が文でそう説明しているものではありません。つまり、PC では Good 以上にならず、Android では Medium 以上にならない、という意味です。

上限を超えると、公式では次のランクになると書かれています。表示の制限（Minimum Displayed Performance Rank）の対象になると、パーティクルシステムがすべて取り除かれます（原文: "All Particle Systems removed"）。PC では、既定の表示の下限は Very Poor で、ほとんどのアバターは見えると書かれています。ただし、極端に最適化されていないアバターは、使えなくなる場合があると書かれています（英語の原文の要約です）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21 と Modular Avatar（GitHub の main。正式版 1.18.7 との差は**未確認**{.lbl .lbl-unk}）を含む 5 つのツールの公式の資料を、2026 年 11 月 6 日に確かめて書きました。ほかのツールは版番号を**未確認**{.lbl .lbl-unk}のまま、取得日の時点の公式の資料を確かめました。MMS の公開ドキュメント（ramtype0.github.io）は、**未確認**{.lbl .lbl-unk}です。

VRChat の文章は、公式ドキュメントのソース（GitHub の vrchat-community/creator-docs）で確かめました。公開サイトの表示は、この記事では確かめていません。VRChat の公式は英語の原文だけなので、ここでの引用は原文に訳を添えています。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「パーティクルの衝突」を見ます。この項目は「FALSE/あり」と表示されます。この記事の例の画面では、FALSE（緑）でした。読み方は [パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の「FALSE/あり」の節を見てください。
- **衝突を使っているパーティクルシステム**: どのパーティクルシステムで衝突が有効になっているかを画面で探す方法は、この記事では**未確認**{.lbl .lbl-unk}です。
- **AAO を使うとき**: 公式の基本的な使い方のページは、非破壊改変ツールを使う改変では、Control Panel のパフォーマンスランクはあてにならないと書いています。Play モードに入ったときのランクは、Actual Performance Window で確かめられると案内されています（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

VRChat 公式の勧めは、Avatar Optimization Tips（[公式](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）の、粒子の放出量とパーティクルシステムの数を減らす節にあります。原文は次のとおりです。

> There are ways to have particle systems with large numbers of particles and retain performance. If you are interested in this, look into dynamic batching for sprite particles, don't use collision, and ensure the movement of your particles is simplistic.

訳: 大量のパーティクルを使いながら性能を保つ方法があります。それに興味があるなら、スプライトのパーティクルのダイナミックバッチングを調べ、衝突（collision）は使わず、パーティクルの動きをシンプルにしてください。

この勧めは、大量のパーティクルを性能を保って使う話の中に書かれています。衝突を使わないことは、そのための一つの方法として挙げられています。この記事の整理として、衝突が要らないパーティクルシステムは、衝突の設定をオフにします。すべてのパーティクルシステムでオフになると、この項目は False になります（公式の定義のとおり）。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のドキュメント、CHANGELOG、README で、パーティクル（Particle・パーティクル）の語を検索）。ヒットしたのは、サンプルの説明（拍手の効果音とパーティクルが出る、という文）だけでした。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 公式サイトの `/docs/` 配下 81 ページで、パーティクルの語を検索）。

### lilAvatarUtils {.tool .tool-lil}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: ja_JP の公式ドキュメントの本文 9 ページで、パーティクルの語を検索）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: GitHub のリポジトリの README と、`.docfx` にあるドキュメント 11 件（目次を含めると 19 件）で、パーティクルの語を検索。公開サイト（ramtype0.github.io）は**未確認**{.lbl .lbl-unk}）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版と英語版の公式ドキュメントの全ページと CHANGELOG で、「collision」「衝突」を検索）。PhysBone の Collider の記載はありますが、パーティクルの衝突とは別の機能です。

## 手順

1. SDK の「パフォーマンスの内訳」で、「パーティクルの衝突」が FALSE か「あり」かを確かめます。
2. 衝突（Particle Collision）が有効なパーティクルシステムを探して、衝突が要るかを確かめます（探す方法は、下の注意を見てください）。
3. 変更の前に、プロジェクトのバックアップを取り、今の粒子の見た目と動きを記録します（公式の勧めではなく、**この記事の助言**{.lbl .lbl-adv}です）。
4. 衝突が要らないパーティクルシステムの、衝突（Particle Collision）の設定をオフにします（設定の場所は、下の注意を見てください）。
5. 変更のあと、粒子の見た目と動きを、手順 3 の記録と比べて確かめます。
6. もう一度 SDK の「パフォーマンスの内訳」を開き、「パーティクルの衝突」が FALSE になったかを確かめます。

## 注意・うまくいかないとき

::: warning
- **探す方法と設定の場所は未確認です。** 衝突が有効なパーティクルシステムを探す画面と、衝突の設定の場所は、この記事では確かめていません。
- 衝突をオフにすると、粒子の動きや当たり方が変わります。衝突をオフにしたときの見た目への影響は、公式の資料に記載がなく、この記事では**未確認**{.lbl .lbl-unk}です。
- 「パーティクルの衝突」は、PhysBone のコライダー（「PHYSBONE コライダーの数」）とは別の項目です。
- 「**公式に記載なし**{.lbl .lbl-none}」の各ツールは、衝突を自動で減らす機能が書かれていないという意味です。ツールに任せず、必要かどうかを自分で判断してください。
- 公式は、Avatar Optimization Tips の推奨の数値や上限は、いつでも変わる可能性があると書いています（原文: "Any recommended numbers or limits in this document are subject to change at any time."）。
:::

## 関連ページ

- [トレイルレンダラーの数を減らす](/learn/optimize-particle/trail-renderers)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
