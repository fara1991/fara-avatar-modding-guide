---
title: 三角ポリゴンを減らす方法
reading: さんかくぽりごんをへらすほうほう
description: VRChat のアバターの三角ポリゴンを減らす方法を、Meshia Mesh Simplification（MMS）と Avatar Optimizer（AAO）の削除の機能を中心に整理します。見た目が変わる条件と、公式に記載がない点も書いています。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 公式の勧めは、法線マップの焼き込みと、decimation（間引き）・retopology（作り直し）の検討
- 服で隠れて見えない部分を消すと、見た目に影響させずに負荷を減らせると AAO の公式が書いている
- 見える部分を消すと見た目が変わるため、減らす前と後を Build & Test で比べる
:::

三角ポリゴンは、アバターのモデルが持つ三角形の数です。公式の項目名は Triangles で、SDK の「パフォーマンスの内訳」では「三角ポリゴンの数」と表示されます。上限は PC の Poor が 70,000、Android の Poor が 20,000 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。

上限を超えると、公式では次のランクになると書かれています（[パフォーマンスランク](/learn/vrchat/performance-rank)）。この記事では、三角ポリゴンを減らすツールの機能を、見た目が変わる条件と一緒に整理します。

::: tip
この記事では、次の言葉を使います。

- **公式に記載なし**{.lbl .lbl-none}: 各ツールの公式の資料（日本語版のページや更新履歴）を検索して、書かれていないことを確かめた、という意味です。できない・減らせないという意味ではありません。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 三角ポリゴンの数を確かめます。例の画面では、287,318（上限 70,000）と表示されていました。
- **lilAvatarUtils のレンダラーのタブ**: メッシュごとのポリゴン数を見られます。ツールの説明では、ポリゴン数やマテリアルスロットの多いメッシュを特定して、軽量化に役立てられると書かれています。
- **AAO の結果**: AAO を使うと、Control Panel のパフォーマンスランクはあてにならなくなると、AAO の公式が書いています。Play モードに入ったときのランクで確かめてください（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。

## 減らし方の考え方

- 公式の勧めは、法線マップ（凹凸の陰影を画像にしたもの）の焼き込み、decimation（間引き）や retopology（作り直し）によるメッシュの簡略化を検討することです（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表に、公式の勧めとして載せています）。
- 服で隠れて見えない部分を消すと、AAO の公式は、見た目に影響させずに描画の負荷や BlendShape の処理の負荷を減らせると書いています。ただし、見える部分を消すと見た目が変わります。
- 減らす前と後で、Build & Test を使って見た目と動きを比べてください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

MA には、三角ポリゴンを減らせる条件つきの機能があります（公式の説明をもとに、[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview) でまとめています）。

**何をするか**:

- **Mesh Cutter**: メッシュの一部を、頂点フィルターで指定して、削除または非表示にするコンポーネントです。公式は、Mesh Cutter が常に有効（アクティブ）な場合は選んだポリゴンを完全に削除し、時々非アクティブになる場合は選んだポリゴンを非表示にすると書いています（[公式: Mesh Cutter](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/mesh-cutter)）。どちらの場合も部位が見えなくなるので、見た目が変わります。
- **Shape Changer（Delete モード）**: アニメーションされていない場合は、ポリゴンを削除して数を減らせます。アニメーションされている場合は非表示になりますが、測定されるポリゴン数は減りません（[公式: Shape Changer](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/shape-changer)）。

**注意**:

- 公式ページには、「場合によっては、Constraint を生成する必要があり、パフォーマンスランクの増加に寄与する可能性がありますのでご注意ください。」と書かれています。Mesh Cutter が時々非アクティブになる場合に当たる注意です。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: TTT の公式サイトのリファレンス（コンポーネント一覧）、チュートリアル、FAQ、TextureFineTuning の説明）。TTT は、テクスチャとマテリアルを扱うツールとして説明されていて、三角ポリゴンを減らす機能の説明は見つかりませんでした。

### lilAvatarUtils {.tool .tool-lil}

レンダラーのタブで、メッシュごとのポリゴン数を表示します。ポリゴン数を減らす機能の説明は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: レンダラーのタブとツールのタブの説明）。確認に使うツールとして読んでください。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**何をするか**: MMS は、メッシュの三角ポリゴンを減らす（簡略化する）ツールです。公式は「Unity、VRChat 向けのメッシュ軽量化ツール」と説明しています。確認した版は 3.3.1（2026 年 11 月 4 日の公開）です。

MMS には 2 種類のコンポーネントがあります。

- **Meshia Cascading Avatar Mesh Simplifier**: アバター全体の三角ポリゴンの数を、目標として指定します。アバターの直下に 1 つ置きます。Modular Avatar が必要です（公式のインストールの説明）。
- **Meshia Mesh Simplifier**: 1 つの Renderer に付けます。結果は、NDMF のプレビュー（EditMode）で確かめられます（README）。

**どう使うか**:

- Cascading の公式の手順は、(1) 対象のアバターの直下に GameObject を作る、(2) 「目標とするパフォーマンスランク」を選ぶ、(3) 見た目を確かめながら、ポリゴン数の配分やオプションを調整する、の 3 つです。
- メニューは「GameObject > Meshia Mesh Simplification > Meshia Cascading Avatar Mesh Simplifier」です（パッケージのソースの MenuItem。公式ドキュメントは、アバターの直下にこの GameObject を作る手順を案内しています）。
- 目標のプリセットは、パッケージのソースに次の値で入っています。公式ドキュメントの本文の値ではありません。
  - PC-Poor-Medium-Good: 70,000
  - PC-Excellent: 32,000
  - Mobile-Poor: 20,000
  - Mobile-Medium: 15,000
  - Mobile-Good: 10,000
  - Mobile-Excellent: 7,500
- Cascading の既定の目標は 70,000 の三角形です（パッケージのソース）。PC-Poor-Medium-Good と Mobile-Poor の値は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の Poor の上限と同じ値です。
- 単体の Meshia Mesh Simplifier の既定の目標は、頂点数の比率 0.5 です（パッケージのソース）。三角ポリゴンの数ではないので、混同しないでください。

**注意**:

- **Preserve Border Edges（メッシュの端の辺を保持）**: 公式ドキュメントには、「指先に穴が空いたりする場合、有効化してみてください。」と書かれています。ツールチップでは、穴や切れ目の縁など、他の面に接していない辺を削除しないようにする設定と説明されています（パッケージのソース）。
- Cascading では、既定で左右の手と指のボーンが保持の対象になっています（パッケージのソースの既定値。公式ドキュメントの本文には記載なし）。
- 処理の順序は、パッケージのソースでは、AAO より前に行われます（ソースからの読み取りで、公式の記述ではありません）。
- MMS が「非破壊」かどうかは、**公式に記載なし**{.lbl .lbl-none}です。バックアップを取ってから使うのが安全です（この記事の整理）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO の削除の機能は、Edit Skinned Mesh の種類のコンポーネントです。1.9.0 以降は、Box・Mask・UV Tile の 3 つが MeshRenderer にも使えます。

- **AAO Remove Mesh By Mask**: マスクテクスチャで指定した範囲のポリゴンを削除します。マスクは、削除する範囲を色で指定する画像です。削除モードは「Remove Black」か「Remove White」を選びます。
- **AAO Remove Mesh By Box**: 1 つ以上の箱で指定した範囲のポリゴンを削除します。箱の内側と外側のどちらを削るかを選べます（[公式: Remove Mesh By Box](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-box/)）。
- **AAO Remove Mesh By UV Tile**: UV タイルで指定した範囲のポリゴンを削除します。Poiyomi や lilToon の UV Tile Discard の Vertex モードに近い動きです（[公式: Remove Mesh By UV Tile](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-uv-tile/)）。
- **AAO Remove Mesh By BlendShape**: 選んだ BlendShape で動く頂点と、その頂点を含むポリゴンを削除します。公式は、多くの素体に含まれている貫通防止用の BlendShape を使うと「簡単にメッシュを削除することができます」と書いています。
- **AAO Remove Zero Sized Polygon**: 面積がゼロのポリゴンを削除します。これは高度な最適化の一つで、既定ではオフです（下の注意を見てください）。

**どう使うか**:

- Remove Mesh By BlendShape は、次の順で使います（[AAO の記事](/learn/optimize-aao/avatar-optimizer) の手順）。
  1. 削除したい部分の BlendShape を選びます。
  2. 「プレビューのために、切り替えたBlendShapeの値を自動的に変更する」をオンにして、削除される範囲を確かめます。
  3. 範囲が合わないときだけ、許容差を調整して、もう一度プレビューで確かめます。

**注意**:

- **形で削る Remove Mesh 系（Mask・Box・UV Tile・BlendShape）は、Trace And Optimize の自動の対象に入りません。** 公式の [Trace And Optimize のページ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/) の自動の一覧に、これらの項目は入っていません。使うときは、手動のコンポーネントとして追加します。
- **面積がゼロのポリゴンの削除は、Trace And Optimize の高度な最適化「面積がゼロのポリゴンを自動的に削除する」で、自動で行えます**（[公式: Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。ただし既定ではオフです。公式の changelog（1.7.1）には「not enabled by default」と書かれています。公式は、一部のシェーダーやアニメーションを壊すおそれがあると書いています。
- 面積がゼロのポリゴンを削除する Remove Zero Sized Polygon を手動で追加することは、公式は勧めていません。公式は、手動で追加するより Trace And Optimize を使うことを勧めています。統合対象の SkinnedMeshRenderer に追加しても、効果がないと書かれています。
- Remove Mesh By BlendShape の許容差の向きは、公式の定義（選んだ BlendShape が頂点を許容差より大きく動かしていたら、その頂点を削除する。通常モード）と比べて、公式の例文と Unity 内の説明では逆に書かれています。プレビューで確かめながら調整してください。

## 手順

1. SDK の「パフォーマンスの内訳」で、三角ポリゴンの数を確かめます。
2. lilAvatarUtils のレンダラーのタブで、ポリゴン数の多いメッシュを見つけます。
3. 見えない部分（服の内側など）が分かるメッシュには、AAO の Remove Mesh 系を使います。マスク（Mask）・箱（Box）・UV タイル（UV Tile）・BlendShape のどれを使うかは、部位に合わせて選びます。
4. Remove Mesh By BlendShape を使うときは、上の順でプレビューを使い、範囲を確かめます。
5. 全体の数をさらに減らすときは、MMS の Cascading を使い、目標のパフォーマンスランクを選びます。見た目を確かめながら、配分を調整します。
6. 処理の後、Play モードに入ったときのランクか、NDM Framework の「Manual bake avatar」で作った複製で、見た目と数を確かめます。

## 注意・うまくいかないとき

::: warning
- 削除の範囲を間違えると、見える部分が消えたり、隠れる部分が残ったりします（AAO の公式の注意）。
- Mesh Read/Write を、いずれかの mesh（パーティクルを含む）で無効にすると、ランクは即座に Very Poor になると、公式に書かれています（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- Android では、ランクが Very Poor のアバターは表示されないことがあると、公式に書かれています（三角ポリゴンの Android の Poor の上限は 20,000 です）。
- MMS の「マージ候補に含める頂点同士の距離」（Vertex Link Distance）は、値を大きくすると処理時間が伸びる場合があります。また、4 万頂点以上のメッシュで大きな値を指定するとクラッシュする場合があると、パッケージのツールチップに書かれています（ソースの ja.po。公式ドキュメントの本文ではありません）。
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
