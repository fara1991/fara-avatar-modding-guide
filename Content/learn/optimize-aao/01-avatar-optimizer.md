---
title: Avatar Optimizer（AAO）でできること・使い方
reading: あばたーおぷてぃまいざーでできることつかいかた
description: Avatar Optimizer（AAO）が自動で行うことと、手動で使うコンポーネント、入れ方、結果の確かめ方、うまくいかないときの対応です。
difficulty: 中級
updated: 2026-11-05
---

## Avatar Optimizer（AAO）とは

**Avatar Optimizer**（AAO）は、VRChat のアバターを軽くするための機能をまとめたツールです。作者は anatawa12 さんで、ライセンスは MIT です。公式の説明では「アバター軽量化用のちょっとした非破壊ユーティリティ群」とされています。

AAO の目標は、**見た目と動きを変えずに性能を上げること**です。公式ドキュメントには、見た目や動きが変わったら AAO の不具合として扱うと書かれています。

::: tip
この記事は、AAO **1.9.21**（2026-10-30 リリース）を確認して書きました。取得日は 2026-11-05 です。版が上がると挙動が変わることがあるので、使うときは [リリース一覧](https://github.com/anatawa12/AvatarOptimizer/releases) も確かめてください。
:::

### 非破壊とは

AAO は **非破壊** です。プレハブを展開せず、コンポーネントを追加するだけで使えます。プロジェクトの中のアバターは変わりません。AAO は、Play モードに入るときやアバターをビルドするときに作られる、一時的なコピーにだけ作用します。AAO のコンポーネントを外せば、元どおりに戻ります。

「非破壊」の考え方は、[Avatar Optimizer のドキュメント（基本概念）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/basic-concept/) に書かれています。

### 対応環境

AAO の依存関係（VPM リポジトリのメタデータ）は、次のとおりです。

| 項目 | 内容 |
|---|---|
| Unity | 2022.3 |
| VRChat SDK（Avatars） | 3.7.0 以上 3.11.0 未満 |
| NDMF（NDM Framework） | 1.8.0 以上 2.0.0 未満 |

- Unity 2019 は、AAO 1.8.0 でサポートが終わりました。2019 で使うときは 1.7.x を使います（changelog に記載）。
- Unity 6000.x は、1.9.18 から「初期的・非公式な」対応です。changelog には「not yet officially supported」と書かれています。

::: warning
上の表は、VPM リポジトリのメタデータが根拠です。公式ドキュメントの本文には、Unity や VRChat SDK の版の記載はほとんどありません。
:::

## 入れ方

### ALCOM で入れる（推奨）

1. ALCOM で、VPM リポジトリ `https://vpm.anatawa12.com/vpm.json` を追加します。
2. プロジェクトに、パッケージ `com.anatawa12.avatar-optimizer` を追加します。表示名は `AAO: Avatar Optimizer` です。

公式ページでは、VCC（VRChat Creator Companion）は動作にバグがあり、正しく動かない可能性があるため、ALCOM が推奨されています。

### そのほかの方法

- **UnityPackage**: インストーラーの unitypackage を Unity に import します（VPAI を使う方法）。
- **vrc-get**: 次のコマンドで入れられます。

```
vrc-get repo add https://vpm.anatawa12.com/vpm.json
vrc-get install com.anatawa12.avatar-optimizer
```

- **VPM CLI**: 次のコマンドで入れられます。

```
vpm add repo https://vpm.anatawa12.com/vpm.json
vpm add package com.anatawa12.avatar-optimizer
```

VCC の画面での手順は、確認した公式ページには書かれていませんでした。

## 何が減るか（パフォーマンスランクの項目との対応）

AAO は、パフォーマンスランクの項目のうち、一部を減らせます。自動で行われるものと、コンポーネントを付けて手動で行うものがあります。項目名は、[パフォーマンスランク](/learn/vrchat/performance-rank) の表に合わせています。

| 項目 | AAO での扱い | 主な機能 |
|---|---|---|
| テクスチャメモリ | 自動と手動 | 自動: Optimize Texture。手動: AAO Max Texture Size、AAO Merge Material |
| 三角形（ポリゴン）の数 | 自動と手動 | 自動: Automatically Remove Zero Sized Polygons。手動: AAO Remove Mesh By Mask・Box・UV Tile・BlendShape |
| Skinned Mesh の数 | 自動と手動 | 自動: Merge Skinned Meshes。手動: AAO Merge Skinned Mesh |
| Mesh（MeshRenderer）の数 | 手動 | AAO Merge Skinned Mesh の Mesh renderers（自動の対象になるかは未確認） |
| マテリアルスロット | 自動と手動 | 自動: Merge Skinned Meshes、Allow Shuffling Material Slots。手動: AAO Merge Skinned Mesh の Merge materials、AAO Merge Material |
| ボーン | 自動と手動 | 自動: Remove unused Objects。手動: AAO Merge Bone |
| PhysBone のコンポーネント | 自動と手動 | 自動: Optimize PhysBone Settings、Remove unused Objects。手動: AAO Merge PhysBone |
| PhysBone の Transform | 自動と手動 | 自動: Optimize PhysBone Settings。手動: AAO Replace End Bone With Endpoint Position、AAO Merge Bone |
| PhysBone のコライダー | 自動と手動 | 自動: Optimize PhysBone Settings。手動: AAO Merge PhysBone の Colliders（Merge） |
| Animator | 未確認 | Optimize Animator はレイヤーを整理しますが、Animator の数への効果は未確認です |
| Bounds（範囲） | 未確認 | 公式ページは、バウンズを減らすとは書いていません |
| PhysBone の衝突判定の数、Contact の数 | 未確認 | 公式ページに、効果の記載がありません |
| そのほかの項目 | 減らせない | 後述の「AAO では減らせない項目」を見てください |

## Trace And Optimize でできること

AAO を使うときは、まず **AAO Trace And Optimize** を付けます。アバター全体をスキャンして、安全にできる範囲で最適化を行うコンポーネントです。どの項目を行うかは、チェックボックスで選べます。

### 付け方

- アバターのルート（VRC Avatar Descriptor がある GameObject）に追加します。
- 種別は Avatar Global Component です。
- Unity の検索語やメニューの階層は、確認した公式ドキュメントには書かれていませんでした。コンポーネント名は `AAO Trace And Optimize` です。

::: warning
ルート以外に付けると、Inspector にエラーが出て、コンポーネントは **一切動きません**。付けたのに効果がないときは、置いた場所を確かめてください。
:::

### 自動で行われること

| オプション（Unity の英語表記。公式の日本語表記） | 自動で行うこと | 主に効く項目 |
|---|---|---|
| Optimize BlendShape（BlendShapeを最適化する） | アニメーションを調べて、BlendShape を固定・除去・統合し、数を減らします。BlendShape の負荷を下げるための処理です。 | パフォーマンスランクの表の項目には、該当するものがありません |
| Remove unused Objects（使われていないObjectを自動的に削除する） | 使われていない GameObject・Component・Texture を削除します。ほかのオブジェクトだけに使われる PhysBone は、オン・オフを連動させます。 | ボーン、テクスチャメモリ、PhysBone のコンポーネント、PhysBone の Transform（Contact への効果は未確認） |
| Optimize PhysBone Settings（PhysBoneを最適化する） | （1）同じ設定のコライダーを統合します。（2）必要のない `Is Animated` をオフにします。（3）統合後に同じ動きになる PhysBone をまとめます（掴めないものなど）。（4）再現できる場合は、end ボーンを Endpoint Position に置き換えます。 | PhysBone のコンポーネント・Transform・コライダーの数（衝突判定の数への効果は未確認） |
| Optimize Animator（アニメーターの最適化） | Animator Controller のレイヤーを整理します。AnyState レイヤーと完全グラフのレイヤーを Entry-Exit に、Diamond／Linear の Entry-Exit を BlendTree に変えます。複数の BlendTree レイヤーを 1 つの Direct BlendTree にまとめ、State も Transition もないレイヤーを削除します。 | 効くのはレイヤーの構成です。Animator の数への効果は未確認です |
| Merge Skinned Meshes（スキンメッシュレンダラーを統合する） | 不要に分かれたレンダラーをまとめます。すべてはまとまらないことがあり、その場合は手動の AAO Merge Skinned Mesh を使います。 | スキンドメッシュの数、マテリアルスロット |
| Optimize Texture（テクスチャを最適化する） | 見た目を変えずに、UV パッキングとテクスチャサイズの縮小を行います。対応シェーダーのマテリアルだけが対象です。 | テクスチャメモリ |
| Automatically Remove Zero Sized Polygons（面積がゼロのポリゴンを自動的に削除する） | 面積がゼロのポリゴンを削除します。上級の設定です。 | 三角形（ポリゴン）の数 |

いくつかの項目には、下位の設定があります。

- **Preserve EndBone（endボーンを残す）**: Remove unused Objects の下位の設定です。親が削除されない end ボーンを残します。名前が end で終わるボーンを end ボーンとみなします（大文字と小文字は問いません）。
- **Allow Shuffling Material Slots（マテリアルスロットの前後関係を変えることを許可する）**: Merge Skinned Meshes の下位の設定です。マテリアルスロットの順番を変えることを許可し、描画のコストを下げる場合があります。公式ページには、作者は順番による意味のある影響を知らないと書かれています。
- **MMDワールドとの互換性**: MMD ワールドの BlendShape を固定しないようにします。Unity 上の英語表記は確認できませんでした。
- **Debug Options**: 回避策のための設定です。公式ページには、不安定で、十分なテストがされていないと書かれています。

::: tip
どの項目が既定でオンになっているかは、確認した公式ページには書かれていません。「面積がゼロのポリゴンを自動的に削除する」は、既定でオンかどうかが未確認です。画面で、オンとオフの状態を確かめてください。

古い記事や資料では、Trace And Optimize が「Automatic Configuration」と呼ばれていることがあります。
:::

::: warning
「面積がゼロのポリゴンを自動的に削除する」は、シェーダーやスケールのアニメーションで見た目が変わることがあります。
:::

### 除外できるか

確認した公式ページには、Trace And Optimize の対象から、オブジェクト単位やアバター単位で外す設定の記載はありませんでした。代わりに、次のものがあります。

- 「endボーンを残す」（Preserve EndBone）で、end ボーンを残せます。
- 項目はチェックボックスで個別にオフにできます。どの項目が原因かを探すときに使えます。（公式の記載ではなく、この記事の方法です。）

## 手動で使うコンポーネント

Trace And Optimize の自動の処理では足りないときや、消す範囲を自分で決めたいときに使います。公式ドキュメントでは、多くのコンポーネントについて「Trace And Optimize が同じことを自動で行うため、通常は不要」と書かれています。

コンポーネント名は、公式ドキュメントの表記（`AAO` で始まる名前）で書いています。Unity 上の表示と検索語は、確認した公式ページには書かれていないため、この記事には書いていません。

### まとめる（統合する）

- **AAO Merge Skinned Mesh**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。描画の処理と、同じマテリアルのスロットをまとめて、描画の呼び出し（draw call）を減らします。
  - 追加する場所: マッシュが未設定の SkinnedMeshRenderer を持つ、新しい GameObject です。種別は Source Edit Skinned Mesh Component です。
  - 主な設定: Skinned renderers、Mesh renderers、Remove empty renderer GameObjects（SkinnedMeshRenderer 以外のコンポーネントがない GameObject を削除）、Skip mismatched enablement（有効無効の状態が統合先と違うものを除外）、Copy enablement animation、BlendShape mode、Merge materials です。
  - 注意: 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。コンポーネントが設定するのは、メッシュ・マテリアル・BlendShape・バウンズです。Root Bone や Anchor Override は変わりません。
  - Copy enablement animation は、一度にコピーできるのは 1 種類です。両方がアニメーションされていると、警告かエラーになります。
- **AAO Merge Bone**: GameObject を親の GameObject に統合し、元の GameObject を削除します。同じ GameObject にある、ほかのコンポーネントも削除されます。
  - 設定の「名前の競合を避ける」を使うと、子の GameObject の名前を変えて、重複した名前によるアニメーションの破損を防げます。
  - 親にも Merge Bone があると、両方とも祖父の GameObject に統合されます。
  - Trace And Optimize が似た処理を自動で行うため、通常は不要です。
- **AAO Merge PhysBone**: 複数の PhysBone を 1 つにまとめます。統合後の MultiChildType は Ignore になります。
  - 新しい GameObject に追加します。統合する PhysBone は、同じ GameObject の子である必要があります。または、「このGameObjectの子にする」を使います。
  - 設定の「オーバーライド」は、Copy か Override を選べます。Colliders は Merge に、Endpoint Position は Clear に、Angle Limits は Fix になります。
  - ルートの GameObject も PhysBone Affected Transforms に数えられるため、Merge PhysBone を 1 つ使うごとに 1 増えます。公式ページは、VRChat のパフォーマンスランクのバグの可能性にも触れています。
  - 統合すると、掴めるボーンは 1 つだけになります。

### 消す（見えない部分を削除する）

::: warning
Remove Mesh 系のコンポーネントは、削除する範囲を間違えると、見える部分が消えたり、隠れる部分が残ったりします。
:::

- **AAO Remove Mesh By Mask**: マスクテクスチャで指定した範囲のポリゴンを削除します。服で隠れる部分などを消すと、描画と BlendShape の負荷を下げられます。公式ページには、外見は変わらないと書かれています。
  - 追加する場所: SkinnedMeshRenderer か MeshRenderer のある GameObject です。
  - 設定: マテリアルスロットのリスト、Mask Texture、Remove Mode（`Remove Black` か `Remove White`）です。
  - 注意: マスクの色は作品ごとに違います。Remove Mode を間違えると、別の部分が消えます。アルファマスクや、gatosyocora の MeshDeleterWithTexture 用のマスクも使えます。
- **AAO Remove Mesh By BlendShape**: 選んだ BlendShape で動く頂点と、その頂点を含むポリゴンを削除します。体のめり込みを防ぐ BlendShape で使われることが多いと説明されています。
  - 追加する場所: SkinnedMeshRenderer のある GameObject です。種別は Modifying Edit Skinned Mesh Component です。
  - 設定: BlendShape のリスト、許容差（tolerance）、切り替えた BlendShape の値を自動で変えるプレビューの設定です。許容差は、この値を超えて頂点が動くと削除されます。残りすぎるときは上げ、消えすぎるときは下げます。
- **AAO Remove Mesh By Box**: 1 つ以上の箱の範囲にあるポリゴンを削除します。隠れた部分を消すと、描画と BlendShape の負荷を下げられます。
  - 設定: 削除するポリゴン（箱の内側・外側）、箱のリスト（数を増やせます。中心・サイズ・角度はローカル座標で設定します）、`Edit This Box`（Scene ビューのギズモで編集）です。
- **AAO Remove Mesh By UV Tile**: 指定した UV タイルの範囲にあるポリゴンを削除します。Poiyomi や lilToon の UV Tile Discard に似ていますが、UV チャンネルを任意に選べます。
  - 設定: マテリアルスロットの一覧、削除する UV タイル、UV チャンネルです。

### BlendShape

- **AAO Freeze BlendShape**: BlendShape をメッシュに固定して、除去します。設定は、固定して除去する BlendShape のリストだけです。
  - 0 でない値の BlendShape は、処理の負荷になります。アニメーションで変わらないものを固定すると、負荷が下がります。
  - 常に 0 の BlendShape を固定しても、ファイルサイズは下がります。
  - 固定すると、アニメーションで変えられなくなります。
  - Trace And Optimize の Optimize BlendShape が同様の処理を自動で行うため、通常は不要です。
- **AAO Rename BlendShape**: メッシュの BlendShape の名前を変えます。変更後の名前が重複するとエラーになります。

### PhysBone・テクスチャ・マテリアル

- **AAO Replace End Bone With Endpoint Position**: PhysBone の End Bone を、Endpoint Position の設定に置き換えます。PhysBone Affected Transforms の数を減らせます。
  - 設定は、Average（End Bone のローカル座標の平均）か Override（Endpoint Position Override の値）です。
  - 同じ GameObject にあるすべての PhysBone に適用されます。
  - 先端の揺れ方が変わることがあるため、確かめてください。
- **AAO Clear Endpoint Position**: PhysBone の Endpoint Position を 0 にし、`_EndPhysBone` という GameObject を追加します。「適用して今すぐcomponentを削除する」ボタンを使うと、適用したあとにコンポーネントを削除します。Affected Transforms にどう効くかは、確認した公式ページには書かれていません。
- **AAO Max Texture Size**: テクスチャの上限サイズを設定します。特定のミップマップのレベルを減らしますが、元のフォーマットは保ちます。任意の GameObject に付けられ、子孫に適用されます。
  - 近い親の設定が優先されます。複数の設定が当たるときは、最大のサイズが使われます。
  - ミップマップがないテクスチャと、Crunch 圧縮のテクスチャはスキップされ、警告が出ます。
- **AAO Merge Material**: 同じシェーダーのマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめます。
  - 追加する場所: SkinnedMeshRenderer か MeshRenderer のある GameObject です。
  - 注意: Merge Material で作ったマテリアルは、Merge Skinned Mesh でさらに統合されません。別のメッシュのマテリアルも統合したいときは、Merge Skinned Mesh と同じ GameObject に追加します。
  - マテリアルを入れ替えるアニメーションには対応していません。テクスチャのサンプリング位置が変わる効果（パララックスや UV スクロールなど）にも対応していません。

### 非推奨のコンポーネント

::: warning
次のコンポーネントは、公式ドキュメントで非推奨とされています。新しく使うのは避け、代わりのものを使ってください。

- **AAO Merge ToonLit Material**: 対応シェーダーは `VRChat/Mobile/Toon Lit` だけです。今後の動作の改善はないと書かれています。Merge Material の使用が推奨されています。
- **AAO Remove Zero Sized Polygon**: Trace And Optimize が自動で追加するため、手動での使用は推奨されていません。
- **AAO Make Children**: アバターの最適化とは関係がないと書かれています。将来は、別の非破壊のアバター改変ユーティリティに置き換わる予定です。
- **AAO UnusedBonesByReferencesTool**: 代わりに、Trace And Optimize の「使われていないObjectを自動的に削除する」を使うよう書かれています。
:::

## 結果の確認

AAO の処理は、Control Panel では確かめにくいものです。次の方法で確かめます。

1. **Performance Rank の数字は、そのままでは信頼しない**: 公式ドキュメントは、非破壊ツールを使うアバターでは、Control Panel の Performance Rank は信頼できないと書いています。
2. **Play モードで確かめる**: 公式ドキュメントは、Play モードで **Actual Performance Window**（anatawa12 さんの Gist Pack）を使うよう勧めています。
3. **複製で処理後の姿を見る**: アバターを右クリックして、`NDM Framework` > `Manual bake avatar` を選びます。AAO などを適用した複製ができ、元のアバターは変わりません。見た目と動きを比べ、不要になったら複製は削除できます。公式ドキュメントは、VRM 形式で書き出すときにもこの方法を勧めています。

ランクの見方は、[パフォーマンスランク](/learn/vrchat/performance-rank) にまとめています。

## うまくいかないとき

よくある症状と、公式ドキュメント（FAQ など）の案内を並べます。

| 症状 | 対応 |
|---|---|
| 見た目や動きが変わった | AAO の不具合として扱います。GitHub の Issues や NDMF の Discord などで報告します。公式には、報告用の Bug Report Helper のページもあります。 |
| メッシュが見えない | Root Bone を設定します。AAO の Merge Skinned Mesh は Root Bone を自動で設定しないため、手動で設定します。 |
| 統合したメッシュの明るさが違う | Anchor Override を設定します。 |
| 統合後に、マテリアルのアニメーションが衝突する | 既知の不具合です（GitHub の issue #340）。マテリアルを統合しないでください。マテリアルスロットを入れ替えるアニメーションがあるときは、Merge materials の「統合する」をオフにします。 |
| OSC のギミックで、PhysBone や Contact Receiver が効かない | AAO は、外部のツールから使われるものを使われていないと判断して、削除することがあります。Asset Description の「Parameters Read By External Tools」に設定します。 |
| VRCSDK のハードリミットを超えて、アップロードできない | VRCSDK 3.8.1 以降は、超えていてもビルドとアップロードを開始できます。VRCSDK を更新してください。 |
| Android（Quest）で、ビルド前のチェックがアップロードを止める | 回避策が公式ドキュメントの FAQ にあります。この記事では手順を省きます。 |

## ほかのツールとの併用

### Modular Avatar

- AAO のパッケージの依存には、Modular Avatar は含まれていません。
- 公式ドキュメントで Modular Avatar が出てくるのは、`MA Mesh Settings` コンポーネントの案内だけです。アバターのルートに置くと、Merge Skinned Mesh の Root Bone・Bounds・Anchor Override を全体にまとめて設定できます。
- 併用したときの処理の順序や、衝突する場合の扱いは、確認した公式ページに書かれていません。

### 同じ GameObject に置くとき

- Merge Material と Merge Skinned Mesh を同じ GameObject に置くと、別のメッシュのマテリアルも統合できます。
- Merge Material で作ったマテリアルは、Merge Skinned Mesh ではさらに統合されません。

### TexTransTool・MMS

TexTransTool（TTT）と Meshia Mesh Simplification（MMS）との併用については、確認した AAO の公式ページには記載がありませんでした。この記事では、併用の注意は書いていません。併用するときは、それぞれの公式の情報を確かめてください。

## AAO では減らせない項目

次の項目は、AAO の公式ドキュメントに、減らす機能の記載がありません。パフォーマンスランクの項目としては残ります。

- コンストレイントの数、コンストレイントの深さ
- ライトの数
- パーティクルシステムの数、最大パーティクルの数、メッシュパーティクルの三角形の数、パーティクルトレイル、パーティクルの衝突
- トレイルレンダラーの数
- Cloth メッシュの数、Cloth の最大頂点の数
- リジッドボディの数
- オーディオソースの数
- レイキャストの数

::: tip
「使われていないObjectを自動的に削除する」の対象は、公式ドキュメントに GameObject・Component・Texture と書かれています。ライトやパーティクルが削除の対象に入るかどうかは、明記されていません。
:::

次の項目は、関係する記載はあるものの、効果の範囲が公式ドキュメントで判断できません。

- Bounds（範囲）
- PhysBone の衝突判定の数
- Contact の数
- Animator の数
- Unity の Collider の数

AAO で減らせない項目は、ほかの方法で減らします。手順は [アバターを軽くする](/learn/vrchat/optimization) にまとめています。
