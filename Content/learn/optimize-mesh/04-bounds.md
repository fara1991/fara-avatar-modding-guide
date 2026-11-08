---
title: 範囲（BOUNDS）の確かめ方と注意
reading: はんいばうんずのたしかめかたとちゅうい
description: 範囲（BOUNDS）が何を表すか、上限と確かめ方、Modular Avatar・lilAvatarUtils・Avatar Optimizer の範囲に関わる機能と注意点をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 範囲（BOUNDS）の上限は、PC・Android とも 5 × 6 × 5 m
- 範囲が大きいときは、常時は見えない大きなアニメーションがある可能性があります
- AAO Merge Skinned Mesh は統合のときに範囲（BOUNDS）を設定する
:::

範囲（BOUNDS）は、アバター全体の大きさを表す箱（3 方向の寸法）です。パフォーマンスの内訳では、この大きさが Poor の上限を超えると、ランクを下げる項目になります。上限は PC・Android とも 5 × 6 × 5 m です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

範囲が大きいと、常時は見えない大きなアニメーションがある可能性があります。一方で、範囲（BOUNDS）は、アバターの表示が制限される理由にはなりません。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「範囲（BOUNDS）」の行で、値を見ます。上限の単位は m です。
- 上限を超えている値は、赤で表示されます。
- lilAvatarUtils のレンダラーのタブにも、Bounds の項目があります。

## 範囲が大きいときの考え方

- まず範囲が大きくなっている原因を探します。常時は見えない大きなアニメーションがないかを確かめます。
- ツールは、範囲の値を決める機能や、統合のときに範囲を設定する機能です。設定したあとは、SDK の内訳で値を確かめてください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

- **何をするか**: MA Mesh Settings は、指定した GameObject の配下にあるメッシュに、アンカーオーバーライドと範囲（BOUNDS）を設定します。範囲は、Root Bone として指定したトランスフォームを基準に決まります。
- **どう使う**: アバターの最上位（ルート）に置くと、アバター内のすべてのメッシュに、範囲とライトプローブアンカーが一貫して設定されます。設定モードは 4 つあります（「継承」「設定」「設定しない」「親で指定されているときは継承、それ以外では設定」）。
- **注意**: 範囲は SkinnedMeshRenderer だけに影響します。配布用のアセットに範囲を設定するときは、注意が必要です。
- 出典: [Modular Avatar の Mesh Settings](https://modular-avatar.nadena.dev/ja/docs/reference/mesh-settings)

### lilAvatarUtils {.tool .tool-lil}

- **何をするか**: レンダラーのタブで、Bounds と ルートボーン（Root Bone）を確認できます。Bounds は、メッシュが画面やライトの範囲外にあるかどうかを判定するときの境界です。ルートボーンは、メッシュの原点となるボーンまたは Transform で、Bounds はこの Transform に追従して動きます。
- **どう使う**: Update When Offscreen をオンにすると、画面の外にあるメッシュでも Bounds を計算します。既定ではオフです。
- **注意**: メッシュがこの範囲に収まっていないと、画面内にあるはずのメッシュが突然消えたり、ライトが突然暗くなったりします。そのため、範囲は適切に設定してください。
- 出典: [lilAvatarUtils のレンダラー](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/RenderersGUI.html)

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**: AAO Merge Skinned Mesh は、統合のときに、メッシュ・マテリアル・BlendShape と範囲（Bounds）を設定します。そのほかの設定は変わりません。
- **Trace And Optimize の範囲**: AAO 1.7.1 では、自動の統合の判定に使う範囲の条件を緩和しました。範囲が小数点以下 6 桁まで同じなら、統合します。
- 統合先の SkinnedMeshRenderer の Root Bone と Anchor Override は、自動では設定されません。手動で設定します（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- 出典: [Merge Skinned Mesh](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-skinned-mesh/)

## 手順

1. SDK の「パフォーマンスの内訳」で、範囲（BOUNDS）の値を見ます。上限は 5 × 6 × 5 m です。
2. 範囲が大きい原因を探します。常時は見えない大きなアニメーションがないかを確かめます。
3. lilAvatarUtils のレンダラーのタブで、Bounds と ルートボーンを確認します。
4. Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加して、Root Bone を指定します。
5. AAO の Merge Skinned Mesh で統合したなら、統合先の SkinnedMeshRenderer で Root Bone と Anchor Override を手動で設定します。
6. Build & Test で、メッシュが消えないか、暗くならないかを確かめます。AAO を使ったあとは、Control Panel の値はあてになりません。範囲の値は、Play モードでの確かめ方（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）で見てください。

## 注意・うまくいかないとき

- **統合後にメッシュが見えない**: 多くの場合は、統合先の Root Bone を指定し忘れていることが原因です（[AAO の FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- **統合後に明るさが違う**: 多くの場合は、統合先の Anchor Override を指定し忘れていることが原因です。詳しくは [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」を見てください。
- **メッシュ未設定の SkinnedMeshRenderer**: AAO 1.6.7 では、メッシュが設定されていない SkinnedMeshRenderer は、ポリゴンの無いメッシュになります。これは、VRChat のパフォーマンスランクの範囲（Bounds）に影響するかもしれません。
- **頂点の削除**: AAO 1.9.0 では、範囲を決めるために使われている可能性がある頂点（ポリゴンに属さない頂点）を、残すようになりました。
- **Update When Offscreen**: CHANGELOG の 1.6.11 では、Update When Offscreen をオンにすると範囲が壊れることがある問題を修正しました。
- **無限大の BlendShape**: CHANGELOG の 1.9.0 では、無限大の delta を持つ BlendShape を固定すると範囲が NaN や Infinity になり、メッシュが消えることがある問題を修正しました。
- **MA Mesh Settings**: 配布用のアセットに範囲を設定するときは、注意が必要です（「ツール別の方法」の Modular Avatar の項を見てください）。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [ボーンの数を減らす](/learn/optimize-mesh/bones)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
