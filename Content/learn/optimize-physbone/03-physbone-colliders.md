---
title: PhysBone コライダーの数を減らす
reading: ふぃじっくぼーんこらいだーのかずをへらす
description: PhysBone コライダーの数の上限と確かめ方、Avatar Optimizer（AAO）で同じ設定のコライダーをまとめる方法と注意点をまとめます。VRChat の Global Collision の上限も書きます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- PhysBone コライダーの上限は、PC の Poor が 32、Android の Poor が 16
- AAO の Trace And Optimize は、全く同じ設定の PhysBone コライダーを 1 つに統合する
- アバターでは、Global Collision を有効にしたコライダーは最大 4 つまで
:::

PhysBone コライダーの数は、アバター上の PhysBone コライダー（スクリプト）の数を数える項目です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。上限は PC の Poor が 32、Android の Poor が 16 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) と同じです。表示される最低のランクの設定で PhysBone コライダーなどが取り除かれ、Android の硬い上限（16）は「Show Avatar」でも回避できません（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「PHYSBONE コライダーの数」で、今の値と上限を見ます。例の画面では、7（上限 32）と表示されていました。
- **標準コライダー**: アバター記述子の「Colliders」にある標準コライダー（Standard Colliders）は、性能ランクに影響しません（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **lilAvatarUtils の PBコライダー タブ**: コライダーの参照元を特定したり、どこからも参照されない無駄なコライダーを減らしたりするのに役立ちます。「参照数」は、このコライダーを参照している PhysBone の数です（[PhysBoneCollidersGUI](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。
- **Global Collision**: アバターでは、Global Collision を有効にしたコライダーは最大 4 つです。Global Collision は、Sphere と Capsule のコライダーにだけ対応します。ワールドには制限がありません（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **無効にしたものも、ランクの数に入ります。** 無効化されているものも含めて、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられます（[パフォーマンスランク](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

## 減らし方の考え方

減らす方法は、次のとおりです。

- **同じ設定のコライダーをまとめる**: AAO Trace And Optimize（自動）と、AAO Merge PhysBone の Colliders の Merge（手動）。
- **参照数が 0 のコライダーを外す**: lilAvatarUtils の PBコライダー タブで、どこからも参照されないコライダーを見つけて、外します。

## ツール別の方法

### lilAvatarUtils {.tool .tool-lil}

**何をするか**: PBコライダー タブで、アバターのコライダーを一覧し、どの PhysBone から参照されているかを見られます（[PhysBoneCollidersGUI](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**Trace And Optimize（同じ設定のコライダーの統合）**: 全く同じ設定の PhysBone コライダーを 1 つに統合します（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

- AAO 1.6.0 では、同じ設定の Floor コライダーが 1 つの Floor コライダーにまとめられます（[CHANGELOG（AAO）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

**AAO Merge PhysBone の Colliders（手動）**: コライダーについて「Merge」を選ぶと、統合対象のコライダーの一覧をまとめられます（[Merge PhysBone](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。Merge PhysBone の使い方は、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) を見てください。

**コライダーの削除と統合に関する記録**: AAO 1.9.0 では、一部の PhysBone コライダーが意図せず削除される問題が修正されました。AAO 1.9.12 では、コライダーが外部のボーンを対象にしているときに、Merge PhysBone のコライダーが切り替えの状態が違うものを統合することがある問題が修正されました。AAO 1.9.20 では、同じ Transform を持つ無効な PhysBone コライダーがほかにあると、コライダーが誤って削除されることがある問題が修正されました（いずれも [CHANGELOG（AAO）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE コライダーの数」を確かめます（上限は PC の Poor が 32、Android の Poor が 16）。
2. lilAvatarUtils の PBコライダー タブで、「参照数」が 0 のコライダー（どこからも参照されないもの）を見つけて、外します。
3. OSC で使う PhysBone や Contact Receiver がある場合は、先に「注意・うまくいかないとき」の OSC の項目を読んで、必要な設定を済ませます。そのうえで、アバターのルートに AAO Trace And Optimize を付け、「PhysBone を最適化する」がオンになっているかを確かめます。全く同じ設定のコライダーが、統合の対象になります。
4. まとめきれないものは、AAO Merge PhysBone の Colliders を「Merge」にして、統合対象のコライダーの一覧をまとめます。
5. Global Collision を使うコライダーの数が、アバターの上限（4 つ）を超えていないかを確かめます。
6. Play モードに入って、パフォーマンスランクと、コライダーの当たり方が変わっていないかを確かめます。

## 注意・うまくいかないとき

::: warning
- **統合の条件は「全く同じ設定」です**: Trace And Optimize は、全く同じ設定のコライダーを 1 つに統合します。
- **Global Collision の上限**: アバターでは、Global Collision を有効にしたコライダーは最大 4 つです。対応するのは Sphere と Capsule のコライダーだけです（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **OSC で使う PhysBone や Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、ほかで使われていなければ削除するように設計されています。ただし、この仮定は正しいとは限りません（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。Animator や Expression Menu を使わずに、OSC のギミックで PhysBone や Contact Receiver を使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定するとよいです（[Asset Description](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。OSC を使うアバターでは、自動の最適化のあとに、PhysBone と Contact が動くかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
- **AAO の修正の記録**: 上の 1.9.0・1.9.12・1.9.20 の修正があります。使うときは、AAO のバージョンを確かめてください。
:::

## 関連ページ

- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
