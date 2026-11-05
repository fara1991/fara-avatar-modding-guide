---
title: Avatar Optimizer（AAO）でできること・使い方
reading: あばたーおぷてぃまいざーでできることつかいかた
description: Avatar Optimizer（AAO）が自動で行うことと、手動で使うコンポーネント、入れ方、結果の確かめ方、うまくいかないときの対応です。
difficulty: 中級
updated: 2026-11-06
---

## Avatar Optimizer（AAO）とは

**Avatar Optimizer**（AAO）は、VRChat のアバターを軽くするための機能をまとめたツールです。作者は anatawa12 さんで、ライセンスは MIT です。公式のトップページには、「アバター軽量化用のちょっとした非破壊ユーティリティ群」と書かれています。

AAO の目的は、**アバターの見た目をほとんど変えずに、性能を上げること**です。公式ドキュメント（基本コンセプト）には、意図しない動作の変化はバグとして扱うと書かれています。

::: tip
この記事は、AAO **1.9.21**（2026 年 10 月 30 日リリース）を確認して書きました。取得日は 2026 年 11 月 6 日です。版が上がると挙動が変わることがあるので、使うときは [リリース一覧](https://github.com/anatawa12/AvatarOptimizer/releases) も確かめてください。

この記事では、次の言葉を使います。

- **公式に記載なし**: 公式ドキュメント（日本語版の各ページと changelog）を検索して、書かれていないことを確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

「リファレンス本文」は各コンポーネントのページ、「changelog」は公式ドキュメント内の変更履歴を指します。
:::

### 非破壊とは

AAO は **非破壊** です。プレハブ（Prefab）を展開せず、コンポーネントを追加するだけで使えます。プロジェクトの中のアバターは変わりません。AAO は、Play モードに入るときや、アバターをビルドするときに作られる一時的なコピーにだけ作用します。

「非破壊」の考え方は、[Avatar Optimizer のドキュメント（基本概念）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/basic-concept/) に書かれています。

### 対応環境

AAO の依存関係（VPM リポジトリのメタデータ。パッケージの説明情報）は、次のとおりです。

| 項目 | 内容 |
|---|---|
| Unity | 2022.3 |
| VRChat SDK（Avatars） | 3.7.0 以上 3.11.0 未満 |
| NDMF（NDM Framework） | 1.8.0 以上 2.0.0 未満 |

- Unity 2019 は、AAO 1.8.0 でサポートが終わりました。2019 で使うときは 1.7.x を使います（changelog の 1.8.0 に「Unity 2019 Support」の削除と「2019 のユーザーは 1.7.x を使う」と記載）。
- Unity 6000.x には、1.9.18 から最初の対応（changelog の英語表記は「Initial minimal support for Unity 6000.x」）が入りました。changelog には、英語で「Those versions are not yet officially supported, but something should start working.」（まだ公式には対応していないが、動き始めるはず）と書かれています。

::: warning
上の表は、VPM リポジトリのメタデータが根拠です。版ごとの対応は、changelog にも書かれています（VRChat SDK 3.7.0・3.8.1・3.10.6、Unity 2019・2022・6000.x など）。
:::

## 入れ方

AAO は、VPM リポジトリ（パッケージの配布先）を使って公開されています。そのため、VPM に対応したツールなら、どれでも入れられます。

### ALCOM で入れる（推奨）

1. ALCOM（VPM のツールの一つ）で、VPM リポジトリ `https://vpm.anatawa12.com/vpm.json` を追加します。
2. プロジェクトに、パッケージ `com.anatawa12.avatar-optimizer` を追加します。表示名は `AAO: Avatar Optimizer` です。

公式ページでは、ALCOM を使うことが推奨されています。VCC（VRChat Creator Companion）に存在するバグなどにより、正しく動かない可能性があるためです。VCC でも同じリンクから追加できますが、正しく動かない可能性があります。VCC の画面の細かい手順は、この記事では未確認です。

### そのほかの方法

- **UnityPackage**: インストーラーの unitypackage を Unity にインポートするだけで入れられます。ALCOM から追加する方法と同じ結果になります。公式では、この方法は VPAI という仕組みで実現されていると書かれています。「[アセットの配布について](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/distributing-prefabs/)」のページには、「VPAI インストーラーは、Avatar OptimizerのようなVPMパッケージを、unitypackageをインポートするだけでインストールするためのツールです。」と書かれています。
- **vrc-get**（コマンドラインで VPM のパッケージを入れるツール）: 次のコマンドで入れられます。`/path/to/your-unity-project` は、Unity プロジェクトのフォルダーのパスに置き換えます。

```
vrc-get repo add https://vpm.anatawa12.com/vpm.json
cd /path/to/your-unity-project
vrc-get install com.anatawa12.avatar-optimizer
```

- **VPM CLI**（VPM / VCC のコマンドラインツール）: 次のコマンドで入れられます。

```
vpm add repo https://vpm.anatawa12.com/vpm.json
cd /path/to/your-unity-project
vpm add package com.anatawa12.avatar-optimizer
```

## 何が減るか（パフォーマンスランクの項目との対応）

AAO は、パフォーマンスランクの項目のうち、一部を減らせます。PhysBone（髪や服などを揺らす VRChat の仕組み）や、スキンドメッシュ（ボーンで変形するメッシュ）などの項目が対象です。自動で行われるものと、コンポーネントを付けて手動で行うものがあります。表の項目名は、画面に表示される名前に合わせています。項目の上限の値は、[パフォーマンスランク](/learn/vrchat/performance-rank) にあります。

| 項目 | AAO での扱い | 主な機能 |
|---|---|---|
| テクスチャメモリー | 自動と手動（AAO Merge Material の対応づけは、この記事の整理で、公式に記載なし） | 自動: Optimize Texture ／ 手動: AAO Max Texture Size、AAO Merge Material |
| 三角ポリゴンの数 | 自動と手動 | 自動: Automatically Remove Zero Sized Polygons ／ 手動: AAO Remove Mesh By Mask・Box・UV Tile・BlendShape |
| スキンドメッシュの数 | 自動と手動 | 自動: Merge Skinned Meshes ／ 手動: AAO Merge Skinned Mesh |
| 基本メッシュの数 | 手動 | AAO Merge Skinned Mesh のメッシュレンダラー（統合対象の MeshRenderer） ／ 自動の対象になるかは未確認 |
| マテリアルスロットの数 | 自動と手動 | 自動: Merge Skinned Meshes、Allow Shuffling Material Slots ／ 手動: AAO Merge Skinned Mesh のマテリアルの統合、AAO Merge Material |
| ボーンの数 | 自動と手動 | 自動: Remove unused Objects ／ 手動: AAO Merge Bone |
| PHYSBONE コンポーネントの数 | 自動と手動 | 自動: Optimize PhysBone Settings、Remove unused Objects ／ 手動: AAO Merge PhysBone |
| PHYSBONE トランスフォームの数 | 自動と手動（AAO Merge Bone の対応づけは、この記事の整理で、公式に記載なし） | 自動: Optimize PhysBone Settings ／ 手動: AAO Replace End Bone With Endpoint Position、AAO Merge Bone |
| PHYSBONE コライダーの数 | 自動と手動（コライダーの Merge の対応づけは、この記事の整理で、公式に記載なし） | 自動: Optimize PhysBone Settings ／ 手動: AAO Merge PhysBone のコライダー（Merge） |
| アニメーターの数 | 自動（条件つき） | 自動: Remove unused Objects（Animator Controller が無い Animator） ／ Optimize Animator の効果は未確認 |
| 範囲（BOUNDS） | 未確認 | AAO Merge Skinned Mesh は範囲（BOUNDS）を設定しますが、減らす効果は未確認です |
| PHYSBONE 衝突チェックの数 | 未確認 | Optimize PhysBone Settings はコライダーを統合しますが、衝突チェックの数への効果は未確認です |
| コンタクトの数 | 自動（条件つき） | 自動: Remove unused Objects（Animator Controller で使われていないパラメーターを持つ Contact Receiver） |
| そのほかの項目 | 未確認 | 後述の「AAO の機能一覧に、公式に記載なしの項目」を見てください |

## Trace And Optimize でできること

AAO を使うときは、まず **AAO Trace And Optimize** を付けます。アバター全体を走査（スキャン）して、できる範囲で自動的に最適化を行うコンポーネントです。行う最適化は、チェックボックスで選べます。

公式ドキュメントでは、このコンポーネントは見た目に影響を与えないことを前提に、慎重に作られていると書かれています。見た目や機能に問題が出た場合は、AAO のバグとして報告してほしい、とも書かれています。

### 付け方

- アバターのルート（VRC Avatar Descriptor がある GameObject）に付けます。
- 種別は Avatar Global Component です。
- コンポーネント名（Unity 上の表示名）は `AAO Trace And Optimize` です（GitHub のソースの AddComponentMenu で確認）。

### 自動で行われること

| オプション | 自動で行うこと | 主に効く項目 |
|---|---|---|
| BlendShape を最適化する（Optimize BlendShape） | BlendShape を自動で固定・除去・統合し、数を減らします。 | この記事の表では対応づけていません |
| 使われていないObjectを自動的に削除する（Remove unused Objects） | アニメーションなどを調べて、使われていない Object（GameObject・コンポーネント・テクスチャなど）を削除します。 | ボーンの数、テクスチャメモリー、PHYSBONE コンポーネントの数、PHYSBONE トランスフォームの数、アニメーターの数、コンタクトの数（条件つき） |
| PhysBone を最適化する（Optimize PhysBone Settings） | 次の 4 つの処理を行います（下の一覧）。 | PHYSBONE コンポーネントの数、PHYSBONE トランスフォームの数、コライダーの数 |
| アニメーターの最適化（Optimize Animator） | Animator Controller（アニメーションの動きを決めるアセット）のレイヤーを整理します（下の説明）。 | レイヤーの構成。アニメーターの数への効果は未確認 |
| スキンドメッシュレンダラーを統合する（Merge Skinned Meshes） | 分かれている必要のないスキンドメッシュレンダラーを統合します（自動では統合しないことがあり、そのときは手動の AAO Merge Skinned Mesh を使います）。 | スキンドメッシュの数、マテリアルスロット |
| テクスチャを最適化する（Optimize Texture） | 見た目に影響を与えずに、UV パッキング（UV の配置を詰めること）とテクスチャサイズの縮小を行います（対応シェーダーのマテリアルだけが対象です）。 | テクスチャメモリー |
| 面積がゼロのポリゴンを自動的に削除する（Automatically Remove Zero Sized Polygons） | 面積がゼロのポリゴンを削除します。高度な最適化の一つです。 | 三角ポリゴンの数 |

表のオプション名は、公式ドキュメントの日本語表記です。（ ）内は英語版ドキュメントの表記で、Unity 上の表示は未確認です。

**使われていないObjectを自動的に削除する**（Remove unused Objects）について、補足します。

- 原文には「切り替えるものと一緒に使われていて、他の方法で使われていないPhysBoneコンポーネントを自動的に切り替えるようにします」と書かれています。日本語版では「切り替えるもの」が何を指すかは曖昧ですが、英語版（「if they are only used by toggled objects」。切り替えられるオブジェクトだけで使われている場合）と changelog の 1.6.0 の説明（服を Simple Toggle で切り替えると、PhysBone も自動で切り替わる）で、意味が分かります。
- changelog の 1.6.0 には、Mesh の無い Renderer と、Animator Controller の無い Animator は、意味がないものとして削除されると書かれています。
- changelog の 1.7.0 には、パラメーターが定義されていても Animator Controller で使われていない PhysBone と Contact Receiver は、削除されると書かれています。

**PhysBone を最適化する**（Optimize PhysBone Settings）の内容は、次のとおりです。

- 全く同じ設定の PhysBone Collider を、1 つに統合します。
- 不要な場合に `Is Animated` のチェックを外します。
- 掴めないものなど、統合しても動作が変わらない複数の PhysBone を、1 つに統合します。「掴める」は、PhysBone のボーンをユーザーが掴んで動かせることです（VRChat の仕様）。
- Endpoint Position（end ボーンを使わず、先端の位置を値で決める設定）で問題なく表現できる場合に、end ボーン（PhysBone の末端のボーン）を削除して Endpoint Position に置き換えます。

**アニメーターの最適化**（Optimize Animator）の内容は、次のとおりです。最適化の詳細な仕様は、将来変わる可能性があると書かれています。

- AnyState のレイヤーと完全グラフのレイヤーを、Entry-Exit に変換します。
- Diamond/Linear の Entry-Exit を、BlendTree に変換します。
- 複数の BlendTree レイヤーを、1 つの Direct BlendTree にまとめます。
- State も Transition もないレイヤーを削除します。

いくつかの項目には、次の設定や注意があります。

- **end ボーンを残す**（Preserve EndBone）: 親が残っている end ボーンを、削除しないようにします。公式では、名前が end（大文字小文字の区別なし）で終わるボーンを end ボーンとして扱うと書かれています。
- **マテリアルスロットの前後関係を変えることを許可する**（Allow Shuffling Material Slots）: マテリアルスロットの順番を変えることを許可し、描画の負荷を下げる場合があります。公式には、作者の知る限り、マテリアルスロットの前後関係は特に意味を持たない、と書かれています。
- **MMD ワールドとの互換性**（MMD World Compatibility）: MMD（MikuMikuDance。3D モデルを動かすためのフリーソフト）のワールドで使われる BlendShape を固定しないなど、MMD ワールドとの互換性を考えた軽量化を行います。
- **Debug Options**: バグの回避などに使う高度な設定です。公式には、不安定で、不用意に変更するべきではないと書かれています。

::: tip
「使われていないObjectを自動的に削除する」の対象は、公式では「GameObject・コンポーネント・テクスチャなど」と書かれています。そのため、未使用のライト・パーティクル・AudioSource・Rigidbody・Constraint などのコンポーネントも、対象になり得ます。種類ごとの細かい対象は、この記事では未確認です。
:::

::: tip
既定でオンかオフかは、この記事では一覧にしていません。changelog で分かるものは次のとおりです。

- **面積がゼロのポリゴンを自動的に削除する**: 1.7.1 で Advanced Optimizations に移され、既定ではオンにならない（not enabled by default）と書かれています。一部のシェーダーやアニメーションを壊すおそれがあるためです。
- **マテリアルスロットの前後関係を変えることを許可する**: 1.9.0 で既定オンになりました。

画面で、オンとオフの状態を確かめてください。

古い記事や資料では、Trace And Optimize が「Automatic Configuration」と呼ばれていることがあります。
:::

::: warning
「面積がゼロのポリゴンを自動的に削除する」は、一部のシェーダーやアニメーションによるスケール変化を壊す可能性があるため、注意して使ってください。
:::

### 除外できるか

Trace And Optimize には、一部の GameObject を最適化の対象から外す **Exclusions**（除外）という機能があります。changelog の 1.5.0 に「Added Exclusions for exclude some GameObjects from optimization」（一部の GameObject を最適化から除外するための Exclusions を追加）と書かれています。changelog では、高度な設定の欄に追加されたとも書かれています。その後、1.6.7・1.8.2・1.9.2 に Exclusions の修正があります。

ただし、設定の詳細（対象の単位など）は、この記事では未確認です。

代わりに、次の方法があります。

- 「end ボーンを残す」（Preserve EndBone）で、end ボーンを残せます。
- 項目はチェックボックスで個別にオフにできます。どの項目が原因かを探すときに使えます。（公式の記載ではなく、この記事の方法です。）

## 手動で使うコンポーネント

Trace And Optimize の自動の処理では足りないときや、消す範囲を自分で決めたいときに使います。公式ドキュメントでは、多くのコンポーネントについて、Trace And Optimize が同様の処理を自動で行うため、大抵の場合は使う必要がないと書かれています。

コンポーネント名は、公式ドキュメントの表記（`AAO` で始まる名前）で書いています。Unity 上の表示名（AddComponentMenu。GitHub の AAO のソース v1.9.21 で確認）は、次の点が表記と違います。`AAO Freeze BlendShapes`（ドキュメントは AAO Freeze BlendShape）、`AAO Merge Toon Lit Material`（ドキュメントは AAO Merge ToonLit Material。Toon と Lit の間に空白）。検索するときは、Unity 上の表示名で探してください。

### まとめる（統合する）

- **AAO Merge Skinned Mesh**: 1 つ以上の SkinnedMeshRenderer と MeshRenderer を、1 つの SkinnedMeshRenderer にまとめます。SkinnedMeshRenderer を統合すると、メッシュを変形させる処理の回数が減り、負荷が軽くなります。同じマテリアルを使うマテリアルスロットもまとめられるため、描画の負荷も減らせます。
  - 追加する場所: メッシュを指定していない SkinnedMeshRenderer を持つ、新しい GameObject に追加します。種別は Source Edit Skinned Mesh Component です。
  - 設定（公式の日本語表記。（ ）内は英語版ドキュメントの表記）:
    - スキンメッシュレンダラー（Skinned Renderers）: 統合対象の SkinnedMeshRenderer の一覧です。
    - メッシュレンダラー（Basic Renderers）: 統合対象の MeshRenderer の一覧です。
    - 空のレンダラーオブジェクトを削除する（Remove Empty Renderer GameObject）: 統合対象の SkinnedMeshRenderer が付いている GameObject に、ほかのコンポーネントがなければ、その GameObject をヒエラルキーから取り除きます。既定はオンです（changelog の 0.4.4）。
    - 有効無効状態が統合先と異なるレンダラーを統合しない（Skip Enablement Mismatched Renderers）: 統合先と有効無効の状態が違うレンダラーを、ビルド時に統合対象から外します。既定は、新しく追加した Merge Skinned Mesh ではオン（changelog の 1.6.0）でしたが、1.8.0 で既定オフに変わりました（Inspector で有効にできます）。
    - 有効無効状態に関するアニメーションをコピーする（Copy Enablement Animation）: 統合対象の有効無効に関するアニメーションを、統合先の SkinnedMeshRenderer にコピーします。
    - BlendShape モード（BlendShape Mode）: BlendShape の扱い方を選びます。既定は、重複を避けるために BlendShape 名を自動で変えるモードです。
    - マテリアルの統合（Merge Materials）: 同じマテリアルを使うマテリアルスロットを、1 つにまとめるかを選びます。
  - 注意: 統合すると、個々のメッシュをアニメーションでオン・オフできなくなります。このコンポーネントが設定するのは、メッシュ・マテリアル・BlendShape・範囲（BOUNDS）です。そのほかの設定（Anchor Override など）は変わりません。そのため、AAO Merge Skinned Mesh と同じ GameObject にある SkinnedMeshRenderer で設定します。Root Bone（SkinnedMeshRenderer の基準になるボーン）も、自動では設定されません（FAQ の案内）。
  - Cloth の付いた SkinnedMeshRenderer は、統合元にするとエラーになります（changelog の 1.7.0）。Cloth のある Renderer は、BlendShape の自動最適化（changelog では AutoFreezeBlendShape）の対象から外れます（changelog の 1.7.0）。
  - 有効無効状態に関するアニメーションは、1 種類だけコピーできます。複数の GameObject（または GameObject と Renderer 自身）がアニメーションされている場合は、エラーになり、ビルドが止まります（changelog の 1.8.0）。
- **AAO Merge Bone**: GameObject を親の GameObject に統合し、元の GameObject を取り除きます。統合対象に付いているほかのコンポーネントも削除されます。
  - 設定の「名前の競合を避ける」を使うと、統合時に子の GameObject の名前を変えて、名前の重複によるアニメーションの破損を防げます。
  - 親にも Merge Bone があると、両方とも、さらに親の GameObject に統合されます。
  - Trace And Optimize が似た処理を自動で行うため、大抵の場合は使う必要はありません。
- **AAO Merge PhysBone**: 複数の PhysBone を 1 つにまとめます。統合後の MultiChildType は Ignore になります。
  - 新しい GameObject に追加します。統合対象の PhysBone は、同じ GameObject の子である必要があります。その代わりに「このGameObjectの子にする」（Make Children of Me）を使うこともできます。
  - 設定の「オーバーライド」では、各項目を Copy（すべての統合対象で値が同じ場合のみ有効）か Override（新しい値を設定）から選びます。コライダーは Merge を選ぶと、統合対象のコライダーの一覧をまとめられます。Endpoint Position は Clear を選ぶと、Clear Endpoint Position を使えます。角度制限は Fix を選ぶと、Roll の値をそろえられます。
  - Endpoint Position を Clear にすると、PhysBone Affected Transforms が増えます（changelog の 1.7.0）。そのため、新しく作る Merge PhysBone の Endpoint Position の既定は Copy です（同じく 1.7.0）。
  - ルートの GameObject も PhysBone Affected Transforms に数えられるため、Merge PhysBone を 1 つ使うごとに 1 増えます。公式には、これは VRChat のパフォーマンスランクの仕組みのバグである可能性がある、と書かれています。
  - 統合後は、掴めるのは 1 か所だけになります。

### 消す（見えない部分を削除する）

::: warning
Remove Mesh 系のコンポーネントは、削除する範囲を間違えると、見える部分が消えたり、隠れる部分が残ったりします。
:::

- **AAO Remove Mesh By Mask**: マスクテクスチャで指定した範囲のポリゴンを削除します。服で隠れて見えない部分などを消すと、見た目に影響させずに描画負荷や BlendShape の処理負荷を減らせます。
  - 追加する場所: SkinnedMeshRenderer か MeshRenderer のある GameObject です。
  - 設定: マテリアルスロットの一覧（対象を選びます）、マスクテクスチャ（削除する範囲を色で指定する画像）、削除モード（`Remove Black` か `Remove White`）です。
  - 注意: マスクテクスチャは、物（アイテム）ごとに色が異なります。削除モードを間違えると、別の部分が消えます。アルファマスク（透明度で範囲を指定する画像）や、gatosyocora さんの MeshDeleterWithTexture 用のマスクも使えます。
- **AAO Remove Mesh By BlendShape**: 選んだ BlendShape で動く頂点と、その頂点を含むポリゴンを削除します。多くの素体（アバターのベースになる体のメッシュ）に入っている貫通防止用の BlendShape を使うと、簡単にメッシュを削除できると、公式に書かれています。
  - 追加する場所: SkinnedMeshRenderer のある GameObject です。種別は Modifying Edit Skinned Mesh Component です。
  - 設定: BlendShape の一覧、許容差、プレビューの設定（切り替えた BlendShape の値を自動で 100 か 0 にする設定）です。
  - 許容差: 選んだ BlendShape が頂点を許容差より大きく動かしていたら、その頂点を削除します（公式の定義。実際には、その頂点を含むポリゴンが削除されます）。通常モードでは、許容差を大きくすると削除される範囲が減り、小さくすると増えます。
  - 公式の例（「消えて欲しい箇所が消えない場合は許容差の値を大きく、消えて欲しくない箇所が消えてしまう場合は許容差の値を小さくしましょう」）は、この通常モードとは逆に読めます。例の向きは、反転モード（changelog の 1.9.0。ソースの `invertSelection`）の挙動に合います。公式のリファレンスには反転の説明がありません。プレビューで、削除される範囲が増えるか減るかを確かめてから調整してください。
- **AAO Remove Mesh By Box**: 1 つ以上の箱で指定した範囲のポリゴンを削除します。設定の詳細は、[公式ドキュメント](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-box/) で確かめてください。
- **AAO Remove Mesh By UV Tile**: UV タイルで指定した範囲のポリゴンを削除します。Poiyomi や lilToon の UV Tile Discard の Vertex モードのように動作します。設定の詳細は、[公式ドキュメント](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/remove-mesh-by-uv-tile/) で確かめてください。

### BlendShape

- **AAO Freeze BlendShape**: BlendShape をメッシュに固定して、除去します。設定は、固定して除去する BlendShape の一覧だけです。
  - 値が 0 でない BlendShape は処理の負荷になります。アニメーションで値を変えない BlendShape を固定すると、負荷が軽くなります。
  - 値が常に 0 の BlendShape を固定しても、アバターの容量を削減できます。
  - 固定すると、アニメーションで値を変えられなくなります。
  - Trace And Optimize が自動で同様の処理を行うため、大抵の場合は使う必要はありません。
- **AAO Rename BlendShape**: メッシュの BlendShape の名前を変えます。公式のリファレンスには、「変更後の名前が重複している場合、エラーになります」と書かれています。

### PhysBone・テクスチャ・マテリアル

- **AAO Replace End Bone With Endpoint Position**: PhysBone の end ボーン（末端のボーン）を、Endpoint Position の設定に置き換えます。公式には、これにより PhysBone Affected Transforms の数を減らせると書かれています。
  - 設定は、Average（end ボーンのローカル座標の平均）か Override（Endpoint Position Override で指定した値）です。
  - 同じ GameObject にあるすべての PhysBone に適用されます。
  - この記事の注意として、先端の揺れ方が変わることがあるため、確かめてください。
- **AAO Clear Endpoint Position**: PhysBone の Endpoint Position の値を 0 にし、`_EndPhysBone` という GameObject を追加します。コンポーネント単体の効果は、この記事では未確認です。なお、changelog の 1.7.0 には、Merge PhysBone の Endpoint Position を「Clear」にすると PhysBone Affected Transforms が増える、と書かれています。
- **AAO Max Texture Size**: テクスチャサイズの上限を設定します。特定のミップマップ（縮小した画像を段階ごとに用意したもの）のレベルを減らして縮小しますが、元のテクスチャフォーマットと設定は保持されます。任意の GameObject に付けられ、その子孫すべてに適用されます。
  - 複数の AAO Max Texture Size があるときは、最も近い親の設定が適用されます。
  - 1 つのテクスチャが、異なる設定の AAO Max Texture Size から同時に影響を受けるときは、最も大きいサイズの設定が使われます。
  - ミップマップのないテクスチャや、目標の大きさまで縮小するのに十分なミップマップのレベルがないテクスチャは、スキップされて警告が出ます。Crunch 圧縮（テクスチャの圧縮方式の一つ）のテクスチャも、スキップされて警告が出ます。
- **AAO Merge Material**: 同じシェーダーを使うマテリアルを、テクスチャを手動で並べて 1 つのマテリアルにまとめます。
  - 追加する場所: SkinnedMeshRenderer か MeshRenderer のある GameObject です。
  - 注意: AAO Merge Material で作ったマテリアルを使うマテリアルスロットは、AAO Merge Skinned Mesh では統合されません。別々のメッシュのマテリアルも統合したいときは、AAO Merge Skinned Mesh と同じ GameObject に AAO Merge Material を追加します。
  - 制限事項: マテリアルを置き換えるアニメーションには対応していません。公式には、「ParallaxやUVスクロールのような、テクスチャの参照される位置が使用時に変化するようなシェーダ機能はサポートされていません」と書かれています。パララックス（見る角度に応じて、模様がずれて奥行きに見える表現）も、この中に含まれます。

### 非推奨のコンポーネント

::: warning
次のコンポーネントは、公式ドキュメントで非推奨とされています。新しく使うのは避け、代わりのものを使ってください。

- **AAO Merge ToonLit Material**: 対応シェーダーは `VRChat/Mobile/Toon Lit` だけです（現時点）。公式には、このコンポーネントの動作が改善されることはない、と書かれています。代わりに、AAO Merge Material の使用が推奨されています。
- **AAO Make Children**: アバターの最適化とは関係がない、と書かれています。将来は、別の非破壊のアバター改変ユーティリティのコンポーネントに置き換えられる予定です。
- **AAO UnusedBonesByReferencesTool**: 代わりに、Trace And Optimize の「使われていないObjectを自動的に削除する」を使うよう書かれています。
:::

::: tip
AAO Remove Zero Sized Polygon は、上の非推奨の一覧には入っていません。公式では、Trace And Optimize によって自動で追加されるため、このコンポーネントを手動で追加するより、Trace And Optimize を使うことをお勧めします、と書かれています。
:::

## 結果の確認

AAO の処理は、VRChat SDK の Control Panel では確かめにくいものです。次の方法で確かめます。

::: tip
公式ドキュメントは、非破壊のツールを使うアバターでは、Control Panel のパフォーマンスランクはあてにならなくなると書いています。そのため、Control Panel の数字はそのままでは信頼しないでください。
:::

1. **Play モードで確かめる**: 公式ドキュメントは、Play モードに入ったときのパフォーマンスランクを、anatawa12 さんの Gist Pack の Actual Performance Window で確認できると書いています。
2. **複製で処理後の姿を見る**: アバターの GameObject を右クリックして、`NDM Framework` > `Manual bake avatar` を選びます。AAO などの処理を適用した複製ができ、元のアバターは変わりません。見た目と動きを比べてください。公式ドキュメントは、VRChat 向けのアバターを VRM 形式で出力したい場合などに、この方法を使えると書いています。

ランクの見方は、[パフォーマンスランク](/learn/vrchat/performance-rank) にまとめています。

## うまくいかないとき

よくある症状と、公式ドキュメントの[よくある質問（FAQ）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/) の案内を並べます。

| 症状 | 対応 |
|---|---|
| 見た目や動きが変わった | Trace And Optimize で見た目や動きが変わった場合は、AAO の不具合の可能性があります。ただし、ほかの AAO コンポーネントのバグの挙動に頼っている場合は除きます。報告してください。報告先は、[GitHub Issues](https://github.com/anatawa12/AvatarOptimizer/issues) や NDMF の Discord などです。公式には、報告用の Bug Report Helper のページもあります。 |
| メッシュが見えない | 公式 FAQ によると、多くの場合は、統合先のメッシュ（AAO Merge Skinned Mesh と一緒に付いている SkinnedMeshRenderer）の Root Bone を指定し忘れていることが原因です。AAO Merge Skinned Mesh は Root Bone を自動で設定しないため、手動で設定します。Modular Avatar を使っている場合は、アバターのルートに MA Mesh Settings を追加すると、アバター全体の Root Bone と範囲（BOUNDS）を設定できます。 |
| 統合したメッシュの明るさが違う | 公式 FAQ によると、多くの場合は、統合先のメッシュの Anchor Override を指定し忘れていることが原因です。AAO Merge Skinned Mesh は Anchor Override を自動で設定しないため、手動で設定します。Modular Avatar を使っている場合は、アバターのルートに MA Mesh Settings を追加すると、アバター全体の Anchor Override を設定できます。 |
| 統合後に、マテリアルプロパティのアニメーションが競合する | 既知の不具合で、現時点では競合します。解決を求める issue は、[#340](https://github.com/anatawa12/AvatarOptimizer/issues/340)（マテリアルのアニメーションパラメータの競合の解決。open）です。公式 FAQ では、マテリアルプロパティに対してアニメーションされるメッシュを統合するときは、競合しないよう注意するよう書かれています。競合するときは警告が表示されます。 |
| 統合後に、マテリアルスロットのアニメーションが競合する | マテリアルを入れ替えるアニメーションがあるときは、AAO Merge Skinned Mesh の「マテリアルの統合」の「統合する」をオフにします（公式 FAQ の方法）。 |
| OSC（ゲームや外部ツールと値をやりとりする通信の仕組み）のギミックで、PhysBone や Contact Receiver が効かない | AAO の Trace And Optimize は、OSC で使われているかを判別できないため、使われていないように見えるものを削除することがあります。公式 FAQ では、Asset Description ファイルを作成し、Parameters Read By External Tools に設定する方法が書かれています。 |
| VRChat SDK のハードリミットを超えて、アップロードできない | VRChat SDK 3.8.1 以降は、シーン上のアバターがハードリミット（アバターの上限値）を超えていても、ビルドとアップロードを開始できます。公式では、VRChat SDK を最新版に更新することが推奨されています。 |
| Android（Quest）で、ビルド前のチェックがアップロードを止める | 回避策が、公式のよくある質問（FAQ）にあります。この記事では手順を省きます。 |

## ほかのツールとの併用

### Modular Avatar

- AAO のパッケージの依存には、Modular Avatar は含まれていません。
- 公式の FAQ と Merge Skinned Mesh の項では、Modular Avatar の MA Mesh Settings コンポーネントの案内があります。アバターのルートに置くと、アバター全体の Root Bone と範囲（BOUNDS）を設定できます（FAQ の「非表示」の項）。Anchor Override も、アバター全体をまとめて設定できます（FAQ の「明るさ」の項）。
- 処理の順序: changelog の 1.1.0 には、「modular avatar will be applied before AvatarOptimizer main logic and after removing EditorOnly」（Modular Avatar は、EditorOnly の削除のあと、AAO の主な処理より前に適用される）と書かれています。これは、1.1.0 で Play モードでの適用の仕組みを作り直したときの記述です。
  - 現在の既定は、changelog の 1.9.0 の「Avatar Optimizer will run as late as possible in NDMF Pipeline by default」（既定では、AAO は NDMF の処理の中で、できるだけ遅く実行される）です。
  - Modular Avatar との現在の順序は、この記事では未確認です。
- 衝突する場合の扱いは、この記事では未確認です。

### 同じ GameObject に置くとき

- AAO Merge Material と AAO Merge Skinned Mesh を同じ GameObject に置くと、別のメッシュのマテリアルも統合できます。
- AAO Merge Material で作ったマテリアルは、AAO Merge Skinned Mesh ではさらに統合されません。

### TexTransTool・MMS

TexTransTool（TTT）については、changelog の 1.8.0 に「Improve RemoveMeshByMask compability with Tex Trans Tool」（RemoveMeshByMask と TTT の互換性の改善）があります。併用の注意は、この記事では扱いません。Meshia Mesh Simplification（MMS）との併用は、日本語版の公式ドキュメントと changelog を検索して、公式に記載なしでした。併用するときは、それぞれの公式の情報を確かめてください。

## AAO の機能一覧に、公式に記載なしの項目

次の項目は、日本語版の公式ドキュメントと changelog を検索して、AAO による効果の記載が無いことを確かめました（公式に記載なし）。効果が無いという意味ではありません。パフォーマンスランクの項目としては残ります。

- コンストレイントの数、コンストレイントの深さ
- ライトの数
- パーティクルシステムの数、最大パーティクルの数、メッシュパーティクルの三角ポリゴン数、パーティクルトレイル、パーティクルの衝突
- トレイルレンダラーの数
- CLOTH メッシュの数、CLOTH の最大頂点の数
- リジッドボディの数
- オーディオソースの数

補足として、次の記載があります。

- コンストレイント: changelog の 1.6.12 に、Constraint の有効無効の最適化が「Removed」（削除）と書かれています。理由は、「正しく確実に最適化するには複雑すぎる」です。
- パーティクル: changelog には、パーティクルシステムに関する不具合の修正が書かれています。効果の記載ではありません。

::: tip
「使われていないObjectを自動的に削除する」の対象には、未使用のライトやパーティクルなどのコンポーネントも含まれることがあります。詳しくは、上の「自動で行われること」の注意を見てください。
:::

次の項目は、関係する記載はあるものの、効果の範囲は未確認です。

- 範囲（BOUNDS）: changelog には、Merge Skinned Mesh で範囲を自動で計算する記載があります。減らす効果の記載ではありません。
- PHYSBONE 衝突チェックの数
- コライダーの数
- レイキャスト（光線を飛ばして、当たりを調べる処理）の数: changelog の 1.9.9 に、RaycastCount の performance category への対応の記載があります。減らせるかは未確認です。

これらの項目は、AAO 以外の方法も含めて、[アバターを軽くする](/learn/vrchat/optimization) の手順を見てください。
