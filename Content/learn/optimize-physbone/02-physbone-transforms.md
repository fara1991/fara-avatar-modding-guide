---
title: PhysBone トランスフォームの数を減らす
reading: ふぃじっくぼーんとらんすふぉーむのかずをへらす
description: PhysBone トランスフォーム（Affected Transforms）の数の上限と確かめ方、Avatar Optimizer（AAO）の Replace End Bone With Endpoint Position などで減らす方法をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- PhysBone トランスフォームの上限は、PC の Poor が 256、Android の Poor が 64
- 1 つのコンポーネントが 128 を超えるトランスフォームに影響するときは、チェーンを分けることを検討するとよい
- Replace End Bone With Endpoint Position（AAO）は数を減らすが、揺れ方が変わることがある
:::

PhysBone トランスフォームの数は、PhysBone コンポーネントが影響するトランスフォーム（ボーン）の合計数を数える項目です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。上限は PC の Poor が 256、Android の Poor が 64 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) と同じです。表示される最低のランクの設定で PhysBone などが取り除かれること、Android の硬い上限（64）は「Show Avatar」でも回避できないことです。また、1 つの PhysBone コンポーネントが同時に影響できるのは 256 トランスフォームまでで、ルートも子も数えます（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。

版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「PHYSBONE トランスフォームの数」で、今の値と上限を見ます。例の画面では、409（上限 256）と表示されていました。
- **1 つのコンポーネントが影響するトランスフォームの数**: 上限の 256 は、1 つのコンポーネントについての上限でもあります（128 を超える場合の扱いは、下の「減らし方の考え方」を見てください）。
- **lilAvatarUtils の PhysBone タブの「ボーン数」**: 「ボーン数」は、揺れが計算されるボーン数です。この記事では、lilAvatarUtils の「ボーン数」を、SDK の「PHYSBONE トランスフォームの数」とは別の値として扱います。同じタブの「ルートボーン」は、PhysBone の計算を行うボーンまたは Transform の親で、この配下の Transform に揺れが適用されます（[PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。
- **無効にしたものも、ランクの数に入ります。** 無効化されているものも含めて、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[パフォーマンスランク](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

## 減らし方の考え方

トランスフォームの数を減らすには、次の方法がおすすめです（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。

- 本当に必要な場合は、1 つのコンポーネントが 128 を超えるトランスフォームに影響するときに、チェーンを分けることを検討するとよいです。
- 例えば、256 ボーンのドレスが根元で分かれているなら、2〜3 個のコンポーネントに分けると動作します。
- チェーン内のボーンは、直上の親にまとめるのがおすすめです。これは Cat's Blender Plugin などのツールでできます。
- ただし、そもそも動かすトランスフォームを多くしないことを目指すとよいです。

この記事の整理として、減らす方法を次のように分けます。

- **末端のボーンを Endpoint Position に置き換える**: AAO の Replace End Bone With Endpoint Position（手動）と、Trace And Optimize の自動の置き換え。
- **チェーンを分ける**: 上の 128 を超える場合の方法です。
- **動かないボーンを整理する**: AAO Merge Bone。

## ツール別の方法

### Avatar Optimizer（AAO） {.tool .tool-aao}

**AAO Replace End Bone With Endpoint Position（手動）**: PhysBone の end ボーン（末端のボーン）を、Endpoint Position（末端の位置を値で決める設定）に置き換えます。これにより、VRChat のパフォーマンスランクの仕組みにおける PhysBone Affected Transforms の数を減らせます（[Replace End Bone With Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/)）。

- **設定**: Average（end ボーンのローカル座標の平均）か Override（Endpoint Position Override で指定した値）を選びます。
- **対象**: 同じ GameObject にある PhysBone コンポーネントが複数あるときは、その全てに設定が適用されます（[Replace End Bone With Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/)）。

**Trace And Optimize の「PhysBone を最適化する」（自動）**: Endpoint Position で問題なく表現できる場合に、end ボーンを削除して Endpoint Position に置き換えます（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

**Trace And Optimize の「end ボーンを残す」（Preserve EndBone）**: 親が削除されていない end ボーンを、削除しないようにします。置き換えで end ボーンが消えるのを防ぐ設定です。名前が end（大文字小文字の区別なし）で終わるボーンを、end ボーンとして扱います（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

**AAO Clear Endpoint Position**: Endpoint Position の値を 0 に置き換え、`_EndPhysBone`GameObject を追加します（[Clear Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/clear-endpoint-position/)）。ボタンを 1 つ持ち、押すと、この操作を適用してコンポーネントを削除します。

**AAO Merge Bone**: GameObject を親の GameObject に統合して、元の GameObject を取り除きます。Trace And Optimize が似た処理を自動で行うため、大抵の場合は使う必要がありません（[Merge Bone](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-bone/)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE トランスフォームの数」を確かめます（上限は PC の Poor が 256、Android の Poor が 64）。
2. 1 つの PhysBone コンポーネントが、128 を超えるトランスフォームに影響していないかを確かめます。128 を超える場合は、チェーンを分けることを検討するとよいです。
3. 末端のボーンを Endpoint Position で表せる場合は、AAO Replace End Bone With Endpoint Position を、その PhysBone がある GameObject に付けます。Average か Override を選びます。
4. Trace And Optimize の「PhysBone を最適化する」をオンにすると、自動の置き換えが行われることを確かめます。
5. 置き換えたあとは、先端の揺れ方が変わっていないかを、必ず Play モードで確かめます。
6. Play モードに入ったときのランクを確かめます（[AAO の結果の確認](/learn/optimize-aao/avatar-optimizer)）。

## 注意・うまくいかないとき

::: warning
- **先端の揺れ方が変わることがある**: Endpoint Position に置き換えると、先端の揺れ方が変わることがあります。置き換えたあとに、確かめてください。
- **過去の不具合の修正**: AAO の修正として、置き換えに関するものが 3 つあります。AAO 1.9.2 では、アニメーションされている end ボーンが、意図せず Endpoint Position に置き換えられることがある問題が修正されました。AAO 1.9.4 では、場合によって誤って置き換えられることがある問題が修正されました。AAO 1.9.17 では、ボーンの位置が (0, 0, 0) のとき、PhysBone が壊れることがある問題が修正されました。使うときは、AAO のバージョンを確かめてください（[CHANGELOG（AAO）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。
- **Merge PhysBone を使うと数が増える**: Merge PhysBone を 1 つ使うごとに、ルートの GameObject も数えられるため、PhysBone トランスフォームの数が 1 増えます（[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)）。
- **OSC で使う PhysBone や Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、ほかで使われていなければ削除するように設計されています。ただし、この仮定は正しいとは限りません（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。OSC で使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定するとよいです（[Asset Description](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。PhysBone が効かなくなっていないかを、自動の最適化のあとに確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
:::

## 関連ページ

- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [ボーンの数を減らす](/learn/optimize-mesh/bones)
