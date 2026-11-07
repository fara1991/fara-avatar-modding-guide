---
title: 三角ポリゴンを減らす方法
reading: さんかくぽりごんをへらすほうほう
description: VRChat のアバターの三角ポリゴンを減らす方法を、Meshia Mesh Simplification（MMS）と Avatar Optimizer（AAO）の削除の機能を中心に整理します。見た目が変わる条件も扱います。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 法線マップの焼き込みと、decimation（間引き）・retopology（作り直し）の検討がおすすめです
- 服で隠れて見えない部分を消すと、見た目に影響させずに負荷を減らせます（[Remove Mesh By BlendShape](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-blendshape/)）
- 見える部分を消すと見た目が変わるため、減らす前と後を Build & Test で比べる
:::

三角ポリゴンは、アバターのモデルが持つ三角形の数です。英語の項目名は Triangles で、SDK の「パフォーマンスの内訳」では「三角ポリゴンの数」と表示されます。上限は PC の Poor が 70,000、Android の Poor が 20,000 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。

上限を超えると、次のランクになります（[パフォーマンスランク](/learn/vrchat/performance-rank)）。この記事では、三角ポリゴンを減らすツールの機能を、見た目が変わる条件と一緒に整理します。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 三角ポリゴンの数を確かめます。例の画面では、287,318（上限 70,000）と表示されていました。
- **lilAvatarUtils のレンダラーのタブ**: メッシュごとのポリゴン数を見られます。ポリゴン数やマテリアルスロットの多いメッシュを特定して、軽量化に役立てられます。
- **AAO の結果**: AAO を使うと、Control Panel のパフォーマンスランクはあてにならなくなります。Play モードに入ったときのランクで確かめてください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

- 法線マップ（凹凸の陰影を画像にしたもの）の焼き込み、decimation（間引き）や retopology（作り直し）によるメッシュの簡略化を検討するのがおすすめです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) にも同じ内容があります）。
- 服で隠れて見えない部分を消すと、見た目に影響させずに、描画の負荷や BlendShape の処理の負荷を減らせます（[Remove Mesh By BlendShape](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-blendshape/)）。ただし、見える部分を消すと見た目が変わります。
- 減らす前と後で、Build & Test を使って見た目と動きを比べてください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

MA には、三角ポリゴンを減らせる条件つきの機能があります（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview) でもまとめています）。

**何をするか**:

- **Mesh Cutter**: メッシュの一部を、頂点フィルターで指定して、削除または非表示にするコンポーネントです。Mesh Cutter が常に有効（アクティブ）な場合は、選んだポリゴンを完全に削除します。時々非アクティブになる場合は、選んだポリゴンを非表示にします（[公式: Mesh Cutter](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/mesh-cutter)）。どちらの場合も部位が見えなくなるので、見た目が変わります。
- **Shape Changer（Delete モード）**: アニメーションされていない場合は、ポリゴンを削除して数を減らせます。アニメーションされている場合は非表示になりますが、測定されるポリゴン数は減りません（[公式: Shape Changer](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/shape-changer)）。

**注意**:

- Mesh Cutter が時々非アクティブになる場合は、場合によって Constraint を生成する必要があり、パフォーマンスランクの増加に寄与する可能性があるため、注意してください（[公式: Mesh Cutter](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/mesh-cutter)）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**何をするか**: MMS は、Unity、VRChat 向けに、メッシュの三角ポリゴンを減らす（簡略化する）ツールです。

MMS には 2 種類のコンポーネントがあります。

- **Meshia Cascading Avatar Mesh Simplifier**: アバター全体の三角ポリゴンの数を、目標として指定します。アバターの直下に 1 つ置きます。Modular Avatar が必要です。
- **Meshia Mesh Simplifier**: 1 つの Renderer に付けます。結果は、NDMF のプレビュー（EditMode）で確かめられます（README）。

**どう使うか**:

- Cascading の使い方は、次の 3 つです。(1) 対象のアバターの直下に GameObject を作る、(2) 「目標とするパフォーマンスランク」を選ぶ、(3) 見た目を確かめながら、ポリゴン数の配分やオプションを調整する。
- メニューは「GameObject > Meshia Mesh Simplification > Meshia Cascading Avatar Mesh Simplifier」です（パッケージのソースの MenuItem）。
- 目標のプリセットは、次の値です（パッケージのソースの値です）。
  - PC-Poor-Medium-Good: 70,000
  - PC-Excellent: 32,000
  - Mobile-Poor: 20,000
  - Mobile-Medium: 15,000
  - Mobile-Good: 10,000
  - Mobile-Excellent: 7,500
- Cascading の既定の目標は 70,000 の三角形です（パッケージのソース）。PC-Poor-Medium-Good と Mobile-Poor の値は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の Poor の上限と同じ値です。
- 単体の Meshia Mesh Simplifier の既定の目標は、頂点数の比率 0.5 です（パッケージのソース）。三角ポリゴンの数ではないので、混同しないでください。

**注意**:

- **Preserve Border Edges（メッシュの端の辺を保持）**: 指先に穴が空いたりする場合は、有効化してみるのがおすすめです。他の面に接していない辺（穴や切れ目の縁など）を削除しないようにする設定です（パッケージのソース。[Meshia Cascading Avatar Mesh Simplifier の使い方](https://github.com/RamType0/Meshia.MeshSimplification/blob/main/.docfx/docs/ja/vrchat/meshia-cascading-avatar-mesh-simplifier.md)）。
- Cascading では、既定で左右の手と指のボーンが保持の対象になります（パッケージのソースの既定値）。
- MMS を使うときは、バックアップを取ってから使うのが安全です。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO の削除の機能は、Edit Skinned Mesh の種類のコンポーネントです。1.9.0 以降は、Box・Mask・UV Tile の 3 つが MeshRenderer にも使えます。

- **AAO Remove Mesh By Mask**: マスクテクスチャで指定した範囲のポリゴンを削除します。マスクは、削除する範囲を色で指定する画像です。削除モードは「Remove Black」か「Remove White」を選びます。
- **AAO Remove Mesh By Box**: 1 つ以上の箱で指定した範囲のポリゴンを削除します。箱の内側と外側のどちらを削るかを選べます（[公式: Remove Mesh By Box](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-box/)）。
- **AAO Remove Mesh By UV Tile**: UV タイルで指定した範囲のポリゴンを削除します。Poiyomi や lilToon の UV Tile Discard の Vertex モードに近い動きです（[公式: Remove Mesh By UV Tile](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-uv-tile/)）。
- **AAO Remove Mesh By BlendShape**: 選んだ BlendShape で動く頂点と、その頂点を含むポリゴンを削除します。多くの素体に含まれている貫通防止用の BlendShape を使うと、簡単にメッシュを削除できます。
- **AAO Remove Zero Sized Polygon**: 面積がゼロのポリゴンを削除します。これは高度な最適化の一つで、既定ではオフです（下の注意を見てください）。

**どう使うか**:

- Remove Mesh By BlendShape は、次の順で使います（[AAO の記事](/learn/optimize-aao/avatar-optimizer) の手順）。
  1. 削除したい部分の BlendShape を選びます。
  2. 「プレビューのために、切り替えたBlendShapeの値を自動的に変更する」をオンにして、削除される範囲を確かめます。
  3. 範囲が合わないときだけ、許容差を調整して、もう一度プレビューで確かめます。

**注意**:

- **形で削る Remove Mesh 系（Mask・Box・UV Tile・BlendShape）は、手動のコンポーネントとして追加して使います。**
- **面積がゼロのポリゴンの削除は、Trace And Optimize の高度な最適化「面積がゼロのポリゴンを自動的に削除する」で、自動で行えます**（[公式: Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。AAO 1.7.1 では、この機能は既定ではオフです。一部のシェーダーやアニメーションを壊すおそれがあります。
- 面積がゼロのポリゴンを削除する Remove Zero Sized Polygon を手動で追加するより、Trace And Optimize を使うのがおすすめです。統合対象の SkinnedMeshRenderer に追加しても効果はありません（[Remove Zero Sized Polygon のページ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-zero-sized-polygon/)）。
- Remove Mesh By BlendShape の許容差の向きは、定義（選んだ BlendShape が頂点を許容差より大きく動かしていたら、その頂点を削除する。通常モード）と、例文および Unity 内の説明で逆になっています。プレビューで確かめながら調整してください。

## 手順

1. SDK の「パフォーマンスの内訳」で、三角ポリゴンの数を確かめます。
2. lilAvatarUtils のレンダラーのタブで、ポリゴン数の多いメッシュを見つけます。
3. 見えない部分（服の内側など）が分かるメッシュには、AAO の Remove Mesh 系を使います。マスク（Mask）・箱（Box）・UV タイル（UV Tile）・BlendShape のどれを使うかは、部位に合わせて選びます。
4. Remove Mesh By BlendShape を使うときは、上の順でプレビューを使い、範囲を確かめます。
5. 全体の数をさらに減らすときは、MMS の Cascading を使い、目標のパフォーマンスランクを選びます。見た目を確かめながら、配分を調整します。
6. 処理の後、Play モードに入ったときのランクか、NDM Framework の「Manual bake avatar」で作った複製で、見た目と数を確かめます。

## 注意・うまくいかないとき

::: warning
- 削除の範囲を間違えると、見える部分が消えたり、隠れる部分が残ったりします。
- Mesh Read/Write を、いずれかの mesh（パーティクルを含む）で無効にすると、ランクは即座に Very Poor になります（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- Android では、ランクが Very Poor のアバターは表示されないことがあります（三角ポリゴンの Android の Poor の上限は 20,000 です）。
- MMS の「マージ候補に含める頂点同士の距離」（Vertex Link Distance）は、値を大きくすると処理時間が伸びる場合があります。また、4 万頂点以上のメッシュで大きな値を指定すると、クラッシュする場合があります（パッケージのソースの ja.po の記載です）。
- MA の Shape Changer は、アニメーションされているポリゴンを減らさないため、数が減らないことがあります。
- MMS と AAO を一緒に使うときは、MMS が先に処理されます（パッケージのソースからの読み取り）。
:::

## 関連ページ

- [テクスチャメモリーを減らす方法](/learn/optimize-mesh/texture-memory)
- [マテリアルスロットを減らす方法](/learn/optimize-mesh/material-slots)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [アバターを軽くする](/learn/vrchat/optimization)
