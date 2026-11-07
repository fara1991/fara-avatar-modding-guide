---
title: コンタクトの数を減らす
reading: こんたくとのかずをへらす
description: VRChat のパフォーマンス表の「コンタクトの数」が何を数えるか、上限、Local Only の扱い、減らし方と注意、ツールごとの挙動をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- コンタクトの上限は、PC の Poor が 32、Android の Poor が 16
- Filtering が Local Only に設定された receiver は、数えない
- Local Only に設定した receiver は、アバターで最大 256 個まで使える
:::

コンタクトは、アバターに付けた Contact（触れたときに反応する仕組み）の数を数える項目です。アバター上の Contact の数を数えますが、Filtering が Local Only に設定された receiver は数えません（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。上限は、PC の Poor が 32、Android の Poor が 16 です。

| 項目 | PC（Excellent / Good / Medium / Poor） | Android（Excellent / Good / Medium / Poor） |
|---|---|---|
| コンタクトの数 | 8 / 16 / 24 / 32 | 2 / 4 / 8 / 16 |

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値です。超えると、表示ランクの下限（Minimum Displayed Performance Rank）を下回るアバターでは、PhysBone・PhysBone コライダー・コンタクトのコンポーネントがすべて取り除かれます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。Android では、硬い上限（16）を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれます。

版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンタクトの数」で、今の値と上限を見ます。
- **数え方**: 数えないのは Local Only の receiver です。sender は、Contacts のページの Filtering の記述（性能ランクを下げない）に従います（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。
- **Play モードのランク**: 非破壊改変ツールを使うと、VRCSDK の Control Panel のパフォーマンスランクは実際の値とずれます（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。Play モードのランクの確かめ方は、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」を見てください。

## 減らし方の考え方

- アバターでコンタクトを使うと、使う数が性能ランクに影響します（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。
- Advanced Parameters は計算のコストが高いため、必要なものだけを有効にするのがおすすめです（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。

手順全体は、[アバターを軽くする](/learn/vrchat/optimization) を見てください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

- **何をするか**: MA Rename Collision Tags は、同じ GameObject の下にある Contacts のタグを、それに応じて名前を変えます（[Rename VRChat Collision Tags（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/rename-vrchat-collision-tags)）。
- **どう使うか**: 未リリースの変更では、`VRC Contact Receiver` の parameter は接頭辞設定の有無にかかわらずリネームされるように変わります（[CHANGELOG-jp.md](https://github.com/bdunderscore/modular-avatar/blob/main/CHANGELOG-jp.md)・[bdunderscore/modular-avatar の #2189](https://github.com/bdunderscore/modular-avatar/pull/2189)）。まだ正式版には入っていない変更です。
- **注意**: MA Rename Collision Tags を Contacts の `Root Transform` の親 GameObject に置いても、その Contacts のタグは変更されません（[Rename VRChat Collision Tags（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/rename-vrchat-collision-tags)）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**: Trace And Optimize の「使われていないObjectを自動的に削除する」（Remove Unused Objects）は、AAO 1.7.0 以降、Animator Controller で使われていないパラメーターが定義されている Contact Receiver を削除します（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。
- **どう使うか**: Trace And Optimize の「使われていないObjectを自動的に削除する」を選ぶと、この自動の処理が行われます（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **注意**: 上の AAO 1.7.0 の動きは、条件つきで Contact Receiver を削除するものです。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンタクトの数」の値と上限（PC の Poor が 32、Android の Poor が 16）を確かめます。
2. Contact の Filtering を見て、Local Only が付いているものと付いていないものを分けます。Local Only は自分の中だけで検知する設定なので、付ける前に、ほかの人との触れ合いで反応させたいものではないかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。Local Only の receiver は、アバターで最大 256 個です。
3. Advanced Parameters が有効になっているものを確かめ、必要なものだけを有効にします。
4. Contact Receiver や PhysBone が OSC（外部ツールと値をやりとりする仕組み）で使われているときは、先に「注意・うまくいかないとき」の OSC の項目（Asset Description の設定）を行います。OSC を使うなら、Trace And Optimize を動かす前に設定します（**この記事の助言**{.lbl .lbl-adv}）。
5. AAO の Trace And Optimize を使うときは、「使われていないObjectを自動的に削除する」をオンにして、Animator Controller で使われていない Contact Receiver が消えていないかを確かめます。OSC を使うアバターでは、4 を先に行ってください。
6. Play モードのランク、または Build & Test で、SDK の値と Contact の動きを確かめます。

## 注意・うまくいかないとき

::: warning
- **Local Only は、自分の中だけで検知します**: Local Only は、自分のクライアントの中だけで働くよう制限する設定です（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)。既存の [PhysBone と Contact](/learn/vrchat-features/physbone-contact) の説明のとおり、自分の中だけで検知するものです）。ほかの人との触れ合いで反応させたいものには付けない、というのは**この記事の助言**{.lbl .lbl-adv}です。
- **Local Only の receiver は 256 個まで**: Local Only を有効にした receiver は、アバターで最大 256 個まで使えます。超えると、アバターをアップロードできません（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。
- **Android の上限**: モバイルの硬い上限は、コンタクトが 16 です。硬い上限を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれます。Android の表示の既定値は Medium で、Poor と Very Poor のアバターは表示されません（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の「Mobile Default Performance Rank Blocking」の節）。
- **タグは 16 個まで**: Contact は、最大 16 個のタグを持てます（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。
- **標準コライダーは性能ランクに影響しない**: 標準コライダー（Standard Colliders）は、性能ランクに影響しません（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts) の Standard Colliders の節）。
- **無効にしたものも、ランクの数に入ります**: 無効化されているものも、性能ランクに数えられます（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
:::

::: warning
**OSC で使う Contact Receiver・PhysBone**: AAO の Trace And Optimize は、Contact Receiver や PhysBone が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、使われていないものと仮定して、ほかの用途で使われていなければ削除するように設計されています。この仮定は正しいとは限らないため、Animator や Expression Menu を使わずに OSC ギミックで使う場合は、Asset Description ファイルを作成し、「Parameters Read By External Tools」を設定するとよいです（[よくある質問（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)、[Asset Description（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。OSC で使う Contact Receiver が効かないときは、この設定を確かめてください。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [PhysBone 衝突チェックの数を減らす](/learn/optimize-physbone/collision-checks)
