---
title: スキンドメッシュの数を減らす
reading: すきんどめっしゅのかずをへらす
description: ボーンで動くメッシュ（スキンドメッシュ）の数を減らす方法です。AAO の Merge Skinned Mesh と自動の統合を中心に、ほかのツールで資料に書かれていること、統合したあとの注意を説明します。
difficulty: 中級
updated: 2026-11-07
---

::: point
- スキンドメッシュの上限は、PC が 16、Android が 2
- VRChat 公式は Skinned Mesh Renderer を 1 つまでと推奨し、これはランクの上限とは別の値
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなる
- 自動では統合しないメッシュもあり、公式は必要に応じて手動の Merge Skinned Mesh を使うよう書いている
:::

スキンドメッシュ（SkinnedMeshRenderer。ボーンで変形するメッシュのことです）の数は、パフォーマンスランクの「スキンドメッシュの数」で数えられます。上限は PC が 16、Android が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。公式では、どれか 1 項目でも上限を超えると次のランクになると書かれています。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog）を検索して、書かれていないと確かめた、という意味です。
- **未確認**{.lbl .lbl-unk}: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21 と、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- 数は、VRChat SDK の Control Panel の「パフォーマンスの内訳」で確かめます。「スキンドメッシュの数」の左の値が、このアバターの数です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown)）。
- AAO を使ったあとは、Control Panel のランクはあてになりません。公式は、Play モードに入ったときのランクを、anatawa12 さんの Gist Pack の Actual Performance Window で確かめる方法を案内しています。処理後の姿は、アバターの GameObject を右クリックして `NDM Framework` > `Manual bake avatar` を選ぶと、複製で見られます（[Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」）。
- lilAvatarUtils のレンダラーのタブでは、メッシュごとのポリゴン数・マテリアルスロット数・ブレンドシェイプ数を一覧で確かめられます（確認のみで、減らす操作ではありません）。

## 減らし方の考え方

- VRChat 公式の勧め（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表。出典は Avatar Optimization Tips）は、メッシュを統合することです。Skinned Mesh Renderer は 1 つまで、静的（Static）メッシュは 3 つまでを推奨しています。この数は、ランクの上限とは別の推奨値です。
- 統合には、AAO の Trace And Optimize による自動の統合と、AAO Merge Skinned Mesh による手動の統合があります。公式は、自動では一部のメッシュを統合しないこともあるため、必要に応じて手動の Merge Skinned Mesh を使うよう書いています。
- 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。衣装の切り替えに使っているメッシュは、統合の対象にするかを先に決めてください（**この記事の助言**{.lbl .lbl-adv}）。
- AAO は、アバターの見た目をほとんど変えずに性能を上げることを目的にしています（[AAO の基本コンセプト](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/basic-concept/)）。統合のあとは、見た目と動きを確かめてください。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

- **何をするか**: Merge Armature は、衣装のアーマチュア（ボーンの階層）をアバターに統合するコンポーネントです。スキンドメッシュを 1 つにまとめる機能ではありません（[Merge Armature](https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature)）。
- **どう使うか**: 衣装を組み込む手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。
- **公式に記載なし**{.lbl .lbl-none}: スキンドメッシュの数を減らす機能は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版と英語版のリファレンス一覧、Merge Armature・Bone Proxy・Mesh Settings のページ）。

### TexTransTool（TTT） {.tool .tool-ttt}

- **何をするか**: AtlasTexture は、マテリアルを結合してテクスチャを 1 枚にまとめます。公式は、AtlasTexture のマテリアルの結合では、マテリアルスロットは結合できないと明記しています（[公式: Quest 対応のアトラス化とマテリアルの結合のチュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。
- **どう使うか**: そのチュートリアルの末尾には、マテリアルスロットも減らすには、AAO の Trace And Optimize や Merge Skinned Mesh との併用を強く推奨する、と書かれています。
- **公式に記載なし**{.lbl .lbl-none}: スキンドメッシュの数を減らす機能は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: Reference のコンポーネント一覧、AtlasTexture と TextureFineTuning のページ、チュートリアル、FAQ）。

### lilAvatarUtils {.tool .tool-lil}

- **何をするか**: レンダラーのタブで、メッシュごとのポリゴン数・マテリアルスロット数などを一覧で表示します。ポリゴン数やマテリアルスロットの多いメッシュを特定するための機能です（[公式: レンダラー](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/RenderersGUI.html)）。
- **公式に記載なし**{.lbl .lbl-none}: メッシュを結合する機能は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: レンダラー・マテリアル・テクスチャ・ツールの各タブの説明。ツールのタブは、マテリアルのクリーンアップ、AnimatorController のクリーンアップ、Missing なコンポーネントの削除です）。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

- **何をするか**: メッシュを簡略化するツールです。コンポーネントは、1 つの Renderer に付ける Meshia Mesh Simplifier と、アバター直下に 1 つ置く Meshia Cascading Avatar Mesh Simplifier の 2 種類です（[公式ドキュメント](https://ramtype0.github.io/Meshia.MeshSimplification/)）。
- **公式に記載なし**{.lbl .lbl-none}: スキンドメッシュの数を減らす機能は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: README、公式ドキュメント、この 2 種類のコンポーネントの説明。どちらも、メッシュを簡略化するものとして書かれています）。

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**:
  - **Trace And Optimize の「スキンメッシュレンダラーを統合する」**: 分かれている必要のないスキンメッシュレンダラーを、自動で統合します。
  - **AAO Merge Skinned Mesh**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。統合すると、メッシュ・マテリアル・BlendShape・範囲（BOUNDS）を設定します（ほかの設定は変わりません）。
  - 同じマテリアルを使うマテリアルスロットも統合できます。公式は、マテリアルの統合は DrawCall（描画の呼び出し）を減らすと書いています。
- **どう使うか**:
  - Trace And Optimize はアバターのルートに付けます。行う最適化は、チェックボックスで選べます。
  - AAO Merge Skinned Mesh は、新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と一緒に付けます（種別は「Source Edit Skinned Mesh Component」です。公式ドキュメントに書かれている分類名です）。
  - 主な設定は、次のとおりです。
    - スキンメッシュレンダラー（Skinned Renderers）: 統合対象の SkinnedMeshRenderer の一覧です。
    - メッシュレンダラー（Basic Renderers）: 統合対象の MeshRenderer の一覧です（[基本メッシュの数を減らす](/learn/optimize-mesh/basic-mesh)）。
    - 空のレンダラーオブジェクトを削除する（Remove Empty Renderer GameObject）: 統合対象の SkinnedMeshRenderer が付いている GameObject に、ほかのコンポーネントがなければ、その GameObject を取り除きます。既定はオンです（changelog の 0.4.4）。
    - 有効無効状態が統合先と異なるレンダラーを統合しない（Skip Enablement Mismatched Renderers）: 既定はオフです（changelog の 1.8.0 で既定がオフに変わりました）。
    - 有効無効状態に関するアニメーションをコピーする（Copy Enablement Animation）: 統合対象の有効無効のアニメーションを、統合先の SkinnedMeshRenderer にコピーします。
    - BlendShape モード（BlendShape Mode）: 既定は、BlendShape 名を自動で変えて重複を避けるモードです。
    - マテリアルの統合（Merge Materials）: 同じマテリアルを使うマテリアルスロットを 1 つにまとめるかを選びます。
  - 公式は、Trace And Optimize が同様の処理を自動で行うため、大抵の場合は AAO Merge Skinned Mesh を使う必要はないと書いています。自動で統合されなかったメッシュに使うのが目安です（この記事の整理）。
  - 統合の範囲は、公式の「このコンポーネントはメッシュ・マテリアル・BlendShape・Boundsを設定しますが、その他の設定については変更しません」という記述のとおりです。Anchor Override などは、統合先の SkinnedMeshRenderer で手動で設定します。
- **注意**:
  - 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。詳しくは、下の「注意・うまくいかないとき」を見てください。
  - Cloth の付いた SkinnedMeshRenderer を統合元にすると、エラーになります（changelog の 1.7.0）。

#### Merge Material との関係

- AAO Merge Material は、同じシェーダーを使うマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめるコンポーネントです。AAO Merge Skinned Mesh の「マテリアルの統合」は、同じマテリアルを使うマテリアルスロットを 1 つにまとめます。
- 別々のメッシュで使われているマテリアルもまとめたいときは、AAO Merge Skinned Mesh と同じ GameObject に AAO Merge Material を追加します（公式）。
- AAO Merge Material で作ったマテリアルを使うマテリアルスロットは、AAO Merge Skinned Mesh では統合されません（公式）。
- Merge Material の制限として、マテリアルを置き換えるアニメーションには対応していません。Parallax や UV スクロールのような、使用時にテクスチャの参照される位置が変わるシェーダー機能も、サポートされていません（公式）。
- AAO Merge ToonLit Material は非推奨で、公式は代わりに Merge Material の使用を推奨しています。

## 手順

1. Control Panel の「パフォーマンスの内訳」で、「スキンドメッシュの数」と「基本メッシュの数」を控えます。
2. アバターのルートに AAO Trace And Optimize を付け、「スキンメッシュレンダラーを統合する」を使うかを選びます。OSC（外部ツールと値をやりとりする仕組み）を使うアバターでは、付ける前に、[ボーンの数を減らす](/learn/optimize-mesh/bones) の「注意・うまくいかないとき」を読んでください（自動の最適化で、OSC 用の PhysBone・Contact が消えるおそれがあるためです）。
3. Play モード（Actual Performance Window で確かめる）か、Manual bake avatar で、統合後の見た目と動きを確かめます。
4. 統合されなかったメッシュは、新しい空の GameObject に、メッシュを指定していない SkinnedMeshRenderer と AAO Merge Skinned Mesh を付けて、スキンメッシュレンダラーの一覧に入れます。
5. 同じマテリアルのマテリアルスロットを 1 つにまとめるなら、「マテリアルの統合（Merge Materials）」をオンにします。アニメーションでマテリアルを別のものに置き換えているメッシュは、「マテリアルの統合」の「統合する」をオフにします（FAQ の案内）。
6. 統合先の SkinnedMeshRenderer で、Root Bone と Anchor Override を設定します。Modular Avatar を使うなら、アバターのルートに MA Mesh Settings を追加します。FAQ の「見えない」の項では、アバター全体の Root Bone と範囲（BOUNDS）を、「明るさ」の項では、アバター全体の Anchor Override を、MA Mesh Settings で設定できると案内されています。
7. Build & Test で、見た目と動き（衣装のオン・オフ、マテリアルのアニメーション）が変わっていないかを確かめます（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)）。

## 注意・うまくいかないとき

::: warning
統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。衣装の切り替えに使っているメッシュを統合するときは、切り替えが統合のあとも動くかを、必ず確かめてください。
:::

- **統合後にメッシュが見えない**: FAQ によると、多くの場合は、統合先の SkinnedMeshRenderer の Root Bone を指定し忘れていることが原因です。AAO Merge Skinned Mesh は Root Bone を自動で設定しません。Root Bone か Anchor Override が設定されていないときは、警告が出ます（changelog の 1.7.0）。
- **統合後に明るさが違う**: FAQ によると、多くの場合は、統合先の SkinnedMeshRenderer の Anchor Override を指定し忘れていることが原因です。
- **統合後に個別のオン・オフができない**: 公式の注意のとおりです。「有効無効状態に関するアニメーションをコピーする」は、コピーできるのは 1 種類だけです。複数の GameObject がアニメーションされていると、エラーになり、ビルドが止まります（changelog の 1.8.0）。統合先の SkinnedMeshRenderer の enabled を、この機能を使うときにアニメーションしてはいけないとも書かれています。
- **マテリアルのプロパティのアニメーションが競合する**: 公式の書き方は食い違っています。備考には「マテリアルに関するアニメーションは統合前のものがそのまま機能します」とあります。一方、FAQ には「マテリアルプロパティに対するアニメーションが競合する」という項目があり、「既知のバグであり、現時点では競合してしまう仕様です」と書かれています（issue は [#340](https://github.com/anatawa12/AvatarOptimizer/issues/340)）。競合するときは警告が表示されます。この記事では、FAQ の書き方を前提にして、統合のあとにマテリアルのアニメーションが動くかを実際に確かめてください。
- **マテリアルスロットのアニメーションが競合する**: FAQ の方法は、「マテリアルの統合」の「統合する」をオフにすることです。
- **PhysBone・Contact が効かない（OSC ギミック）**: 注意と対応（FAQ の Asset Description の方法）は、[ボーンの数を減らす](/learn/optimize-mesh/bones) の「注意・うまくいかないとき」にまとめています。
- **自動で統合されないメッシュ**: **公式に記載なし**{.lbl .lbl-none}（確かめた範囲: Trace And Optimize と Merge Skinned Mesh の日本語版のページ。自動で統合されない条件の一覧は書かれていません）。統合されなかったメッシュは、手動の AAO Merge Skinned Mesh を使います。
- **ほかのツールとの併用**: MA と AAO を併用するときの順序や衝突は、**未確認**{.lbl .lbl-unk}です（[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)）。TTT の AtlasTexture と AAO を併用するときは、TTT の公式の推奨があります。

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [基本メッシュの数を減らす](/learn/optimize-mesh/basic-mesh)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
