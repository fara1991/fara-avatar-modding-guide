---
title: 範囲（BOUNDS）を上限に収める
reading: はんいばうんずをじょうげんにおさめる
description: 範囲（BOUNDS）が何を表すか、上限と確かめ方、Modular Avatar・lilAvatarUtils・Avatar Optimizer の範囲に関わる機能と注意点を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-06
---

範囲（BOUNDS）は、アバター全体の大きさを表す箱（3 方向の寸法）です。パフォーマンスの内訳では、この大きさが Poor の上限を超えると、ランクを下げる項目になります。上限は PC・Android とも 5 × 6 × 5 m です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

公式の補足には、範囲が大きいと、常時は見えない大きなアニメーションがある可能性がある、と書かれています。一方で、公式の注記には、範囲（BOUNDS）は、アバターの表示が制限される理由にはなりません、とも書かれています。

::: tip
この記事で「公式に記載なし」は、そのツールの公式の資料を検索して、書かれていないことを確かめた、という意味です。「未確認」は、この記事では確認していない、という意味です。無い・できないという意味ではありません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「範囲（BOUNDS）」の行で、値を見ます。画面の値には単位が出ていませんが、上限の単位は m です。
- 上限を超えている値は、赤で表示されます。
- lilAvatarUtils のレンダラーのタブにも、Bounds の項目があります。値を編集できるかは、未確認です。

## 減らし方の考え方

- 範囲（BOUNDS）を小さくする公式の勧めは、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表では未確認です。
- この記事の整理として、まず範囲が大きくなっている原因を探します。公式の補足にあるとおり、常時は見えない大きなアニメーションがないかを確かめます。
- ツールは、範囲の値を決める機能や、統合のときに範囲を設定する機能です。どの設定で上限内に収まるかは、この記事では確かめていません。設定したあとは、SDK の内訳で値を確かめてください。

## ツール別の方法

### 1. Modular Avatar（MA）

- **何をするか**: MA Mesh Settings は、指定した GameObject の配下にあるメッシュに、アンカーオーバーライドと範囲（BOUNDS）を設定します。範囲は、Root Bone として指定したトランスフォームを基準に決まります。
- **どう使う**: アバターの最上位（ルート）に置くと、アバター内のすべてのメッシュに、範囲とライトプローブアンカーが一貫して設定されると書かれています。設定モード（継承・設定・設定しない）があります。
- **注意**: 範囲は SkinnedMeshRenderer だけに影響します。配布用のアセットに範囲を設定するときは、注意が必要だと書かれています。
- 小さすぎたとき・大きすぎたときの挙動や、数値の基準は公式に記載なしです（確かめた範囲: Mesh Settings のページ）。
- 出典: [Mesh Settings（Modular Avatar 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/mesh-settings)

### 2. TexTransTool（TTT）

未確認です。TTT の範囲（BOUNDS）に関する記載は、この記事では確かめていません。TTT はテクスチャを扱うツールです。

### 3. lilAvatarUtils

- **何をするか**: レンダラーのタブで、Bounds と ルートボーン（Root Bone）を確認できます。Bounds は、メッシュが画面やライトの範囲外にあるかどうかを判定するときの境界です。ルートボーンは、メッシュの原点となるボーンまたは Transform で、Bounds はこの Transform に追従して動きます。
- **どう使う**: Update When Offscreen をオンにすると、画面の外にあるメッシュでも Bounds を計算します。負荷を抑えるため、既定ではオフです。
- **注意**: 公式には、メッシュがこの範囲に収まっていないと、画面内にあるはずのメッシュが突然消えたり、ライトが突然暗くなったりするため、適切に設定する必要があると書かれています。Bounds の数値の上限は、公式に記載なしです。値を編集できるかは、未確認です。
- 出典: [レンダラー（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/RenderersGUI.html)

### 4. Meshia Mesh Simplification（MMS）

未確認です。MMS の範囲（BOUNDS）に関する記載は、この記事では確かめていません。

### 5. Avatar Optimizer（AAO）

- **何をするか**: AAO Merge Skinned Mesh は、統合のときに、メッシュ・マテリアル・BlendShape と範囲（Bounds）を設定すると、公式に書かれています。そのほかの設定は変わりません。公式の設定の一覧に、範囲を手動で設定する項目は書かれていません。
- **Trace And Optimize の範囲**: リファレンス本文（Trace And Optimize のページ）には、範囲（Bounds）の記載がありません（公式に記載なし。確かめた範囲: 日本語版・英語版の同じページで「Bounds」「範囲」「バウンド」を検索）。CHANGELOG の 1.7.1 には、自動統合の判定に範囲を使う、という記載があります（下の注意を見てください）。
- 統合先の SkinnedMeshRenderer の Root Bone と Anchor Override は、自動では設定されません。手動で設定します（公式 FAQ）。
- 範囲を減らす効果は、公式の資料では確かめていません（未確認）。
- 出典: [Merge Skinned Mesh（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-skinned-mesh/)

## 手順

1. SDK の「パフォーマンスの内訳」で、範囲（BOUNDS）の値を見ます。上限は 5 × 6 × 5 m です。
2. 範囲が大きい原因を探します。常時は見えない大きなアニメーションがないかを確かめます。
3. lilAvatarUtils のレンダラーのタブで、Bounds と ルートボーンを確認します。
4. Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加して、Root Bone を指定します。
5. AAO の Merge Skinned Mesh で統合したなら、統合先の SkinnedMeshRenderer で Root Bone と Anchor Override を手動で設定します。
6. Build & Test で、メッシュが消えないか、暗くならないかを確かめます。AAO を使ったあとは、Control Panel の値はあてにならないと公式に書かれています。範囲の値は、Play モードでの確かめ方（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）で見てください。

## 注意・うまくいかないとき

- **統合後にメッシュが見えない**: AAO の FAQ によると、多くの場合は、統合先の Root Bone を指定し忘れていることが原因です。
- **統合後に明るさが違う**: FAQ によると、多くの場合は、統合先の Anchor Override を指定し忘れていることが原因です。詳しくは [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」を見てください。
- **メッシュ未設定の SkinnedMeshRenderer**: CHANGELOG の 1.6.7 に、メッシュが設定されていない SkinnedMeshRenderer は、ポリゴンの無いメッシュになり、VRChat のパフォーマンスランクの範囲（Bounds）に影響するかもしれない、と書かれています。
- **自動で統合するかの判定**: CHANGELOG の 1.7.1 から、範囲が小数点以下 6 桁まで同じであれば、Trace And Optimize は Skinned Mesh を統合するようになりました（それ以前は、範囲が正確に同じでないと統合しませんでした）。
- **頂点の削除**: CHANGELOG の 1.9.0 に、範囲を決めるために使われている可能性がある頂点（ポリゴンに属さない頂点）は、残すようになったと書かれています。
- **Update When Offscreen**: CHANGELOG（AAO）の 1.6.11 には、Update When Offscreen をオンにすると範囲が壊れることがあった、という修正の記録があります。
- **無限大の BlendShape**: CHANGELOG の 1.9.0 には、無限大（infinity）の delta を持つ BlendShape を AAO で固定すると、範囲が NaN や Infinity になり、Unity がメッシュを消すことがあった、という修正の記録があります。
- **MA Mesh Settings**: 配布用のアセットに範囲を設定するときは、注意が必要です（前の節を見てください）。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [ボーンの数を減らす](/learn/optimize-mesh/bones)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
