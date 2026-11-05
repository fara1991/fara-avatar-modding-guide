---
title: パフォーマンスの内訳の見方（どの項目が重いか）
reading: ぱふぉーまんすのうちわけのみかたどのこうもくがおもいか
description: VRChat SDK の「パフォーマンスの内訳」の画面で、各項目が何を数えているか、数字と色の意味、どの項目から手をつけるかを説明します。
difficulty: 初級
updated: 2026-11-06
---

## はじめに

VRChat のアバターを作ると、SDK（VRChat の公式の制作ツール）の画面で「パフォーマンスの内訳」を見られます。項目が多く、どこから直せばよいか迷いやすい画面です。この記事では、画面の 3 枚の画像を順に見ながら、次のことを説明します。

- 各項目が何を数えているか
- 数字と色が何を表しているか
- どの項目から手をつけるか

ランクの決まりは [パフォーマンスランク](/learn/vrchat/performance-rank)、減らし方の手順は [アバターを軽くする](/learn/vrchat/optimization) にまとめています。

この記事で使う言葉は、次のとおりです。

- **公式**: VRChat の公式ドキュメント（creators.vrchat.com と docs.vrchat.com）を指します。
- **公式に記載なし**: 公式の全ページ（と CHANGELOG）を検索して、本当に無いと確かめたときだけ使う言葉です。この記事では、そこまでの検索はしていないため、この言葉は使いません。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

## 画面の読み方の決まり

この記事の「推定」は、画面の見た目や、公式の近い文言から読んだものです。確かめた事実ではありません。

### 「/」の右の数字は、PC の Poor の上限

「287,318/70,000」のように `/` で並んでいる項目は、左がこのアバターの値、右が **PC の Poor の上限** です。Poor の上限は、Poor でいられる最大の値です。公式では、どれか 1 項目でも上限を超えると、次のランクになると書かれています。この画面に出ている右の数字は、すべて PC の Poor の上限と一致します。左の値が右の数字を超えている項目は、Poor の上限を超えています。

### 色の意味

画面に凡例（色の説明）はありません。次の色の意味は、値が [パフォーマンスランク](/learn/vrchat/performance-rank) の表の上限のどの範囲に入るかから読んだものです。

- 緑: Excellent または Good の範囲
- 黄: Medium の範囲
- 赤: Poor の範囲、またはそれを超えた範囲

同じ緑でも明るさが違う項目があります。Excellent と Good のどちらかは、[パフォーマンスランク](/learn/vrchat/performance-rank) の表と比べて確かめてください。この記事で確かめた範囲では、画面の色はすべての項目で上の区分と一致していました。

赤には、Poor の範囲と、Poor の上限を超えた範囲（Very Poor）の両方が入ります。この 2 つが色で分かれているかは、未確認です。

### 数字の無い項目

ダウンロードサイズと非圧縮サイズには、「/」の右の数字も色もありません。値だけが灰色で出ます。サイズの上限は、ランクの表とは別に、公式の [Avatar Size Limits](https://creators.vrchat.com/avatars/avatar-size-limits) にあります。PC は Download 200 MB・Uncompressed 500 MB、Android は Download 10 MB・Uncompressed 40 MB です（ページの最終更新は 2026 年 10 月 7 日）。この画面の値（Windows で 37.12 MB・138.38 MB）は、どちらも PC の上限より小さい値です。

テクスチャメモリーも、「/」の右の数字は出ていません（93.50 MB と値だけが出ます）。そのため、色で段階を読みます。

### 「使用不可」（Android・iOS）

Android と iOS の欄に出る「使用不可」（灰色）の意味について、この記事では、画面の文言と完全に一致する定義を確認していません。近い文言は次の 2 つあり、どちらかを指していると推定します。

- **そのプラットフォームでは表示できないアバターである。** 公式の [Avatar Fallback System](https://docs.vrchat.com/docs/avatar-fallback-system) には「Avatar not available for your platform.」（そのプラットフォームでは使えない）という表示の説明があります。Windows 向けのアバターは Quest/Android では表示できず、作者が PC と Quest/Android の両方のアバターをアップロードすると解決できる、と書かれています。
- **Very Poor で、モバイルでは表示されない。** 公式の [Performance Ranks](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) には、モバイルの Very Poor のアバターは VRChat で表示されない（ユーザーからは見えない）と書かれています。「Show Avatar」を選べば、強制的に表示することはできます。このアバターは、Android の三角ポリゴンの上限（20,000）を大きく超えているため、こちらの読み方も当てはまります。

公式の Performance Ranks には、将来、モバイルの Very Poor のアバターが Android・iOS から無くなることや、「Show Avatar」が使えなくなることがある、という注意もあります。

iOS については、Avatar Fallback System のページに iOS の記述はなく、フォールバックの条件として「currently Windows and Android」（現在は Windows と Android）と書かれています。そのため、iOS の「使用不可」の意味は未確認です。一方、Performance Ranks のページは、Android と iOS のモバイル向けの上限を同じ表で示しています。

### 「FALSE/あり」（パーティクルトレイル・パーティクルの衝突）

この 2 項目は、数字ではなく「FALSE/あり」と出ます。左は、このアバターの値です。公式では、有効なパーティクルシステムの 1 つにでもトレイル（尾を引く表示）や衝突があると True になります。FALSE は、そうしたものが 1 つも無いことを表すと読めます。

右の「あり」は、PC の Poor の上限（True）に当たると読めます。公式の表では、Excellent と Good は False、Medium と Poor は True です。ただし、画面の「あり」という文言の定義は未確認のため、この読み方は推定です。

## 1. プラットフォーム欄とメッシュ系

[![パフォーマンスの内訳の上の部分（プラットフォーム欄と、ダウンロードサイズからマテリアルスロットまで）](images/performance-breakdown-1-platforms-and-mesh.png)](/learn/optimize-basics/images/performance-breakdown-1-platforms-and-mesh.png)
*パフォーマンスの内訳の上の部分。プラットフォーム欄と、ダウンロードサイズからマテリアルスロットの数まで*

### プラットフォーム欄

- **Windows（現在のプラットフォーム）**: いま見ているパソコン向けの判定です。この画面では **Very Poor** です。
- **Android・iOS**: 「使用不可」（灰色）です。読み方は前の節のとおりです。

### 項目

| 項目（画面の表示） | 何を数えているか（公式の定義） | PC の Poor の上限 | Android の Poor の上限 | 公式の補足 | 減らす効果（公式の記述） |
|---|---|---|---|---|---|
| ダウンロードサイズ | 圧縮後のアバターの大きさ（asset bundle を圧縮したサイズ） | ランクの表には無い（サイズの上限は上の節を参照） | ランクの表には無い（同左） | SDK は、ビルド時に上限を超えるとアップロードを止める | サイズを減らす手順が公式にある（テクスチャの最大サイズ、音声、アニメーション、ブレンドシェイプなど） |
| テクスチャメモリー | テクスチャが使うメモリーの推定値 | 150 MB | 40 MB | テクスチャは、システム RAM と GPU のメモリーの両方を使う | 未確認 |
| 非圧縮サイズ | 展開（非圧縮）したあとの asset bundle の大きさ | ランクの表には無い（サイズの上限は上の節を参照） | ランクの表には無い（同左） | 個々のアセットは展開せず、bundle 全体で数える。テクスチャの圧縮を High Quality にすると、ダウンロードと非圧縮の両方が増える | サイズを減らす手順は同上 |
| 三角ポリゴンの数 | モデルの三角形の数（公式の項目名は Triangles。以前は Polygons と呼ばれていた） | 70,000 | 20,000 | Mesh Read/Write を 1 つでも無効にすると、この項目は「Mesh Read/Write Disabled」と表示され、ランクは即座に Very Poor になる（下の注意を参照） | 未確認 |
| 範囲（BOUNDS） | アバター全体の大きさ（3 方向の寸法）。Trail Renderer と Line Renderer は計算に入らない | 5 × 6 × 5 m | 5 × 6 × 5 m | 大きいと、常時は見えない大きなアニメーションがある可能性がある。この項目は、表示を制限される理由にはならない（公式の注記） | 未確認 |
| スキンドメッシュの数 | Skinned Mesh Renderer（ボーンで動くメッシュ）のコンポーネントの数 | 16 | 2 | ― | 未確認 |
| 基本メッシュの数 | ボーンで動かない Mesh Renderer のコンポーネントの数 | 24 | 2 | ― | 未確認 |
| マテリアルスロットの数 | マテリアルを割り当てる枠の数 | 32 | 4 | パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠、Line Renderer は 1 枠を使う | 枠が増えると、サブメッシュの作成と、描画の呼び出し（ドローコール）の追加につながる |

- 範囲（BOUNDS）の上限（5m x 6m x 5m）の単位は m です。画面の値（200.00, 169.42, 225.89）には、単位が表示されていません。
- Android の欄について: 公式の脚注によると、PhysBone・コンタクト（Contact）・VRChat のコンストレイントは、Android で Very Poor の値（Poor の上限を超えた値）になると、Show Avatar の状態にかかわらず、すべて取り除かれます。
- 描画の呼び出し（ドローコール）は、メッシュごとに 1 回ずつ必要になるのが基本で、減らすと描画の時間が短くなります（Unity マニュアル）。

### この画面の読み方

- 三角ポリゴンの数（287,318/70,000）、範囲（BOUNDS）、スキンドメッシュの数（18/16）、基本メッシュの数（29/24）、マテリアルスロットの数（88/32）は、いずれも上限を超えています（赤）。マテリアルスロットは、上限の約 2.75 倍です。
- テクスチャメモリー（93.50 MB）は黄色で、Medium の範囲です。
- ダウンロードサイズと非圧縮サイズは、「/」の右の数字がないため、色も分母もありません。

## 2. PhysBone・コンタクト・コンストレイント・パーティクルなど

PhysBone は、髪や服を揺らす仕組みです。コンタクト（Contact）は、触れたときに反応する仕組みです。コンストレイント（Constraint）は、別のオブジェクトに合わせて、位置・回転・大きさを動かす Unity の部品です。アニメーター（Animator）は、アニメーションを制御する部品です。

[![パフォーマンスの内訳の中の部分（PhysBone、コンタクト、コンストレイント、アニメーター、ボーン、ライト、パーティクル）](images/performance-breakdown-2-physbone-and-particles.png)](/learn/optimize-basics/images/performance-breakdown-2-physbone-and-particles.png)
*パフォーマンスの内訳の中の部分。PhysBone からパーティクルトレイルまで*

### 項目

| 項目（画面の表示） | 何を数えているか（公式の定義） | PC の Poor の上限 | Android の Poor の上限 | 公式の補足 | 減らす効果（公式の記述） |
|---|---|---|---|---|---|
| PHYSBONE コンポーネントの数 | PhysBone コンポーネントの数 | 32 | 8 | ― | 未確認 |
| PHYSBONE トランスフォームの数 | PhysBone が影響を与えるトランスフォームの数（公式の項目名は PhysBones Affected Transforms） | 256 | 64 | ― | 未確認 |
| PHYSBONE コライダーの数 | PhysBone のコライダーのスクリプトの数 | 32 | 16 | ― | 未確認 |
| PHYSBONE 衝突チェックの数 | 各コライダーが影響しうるトランスフォーム数の合計 | 512 | 64 | 同じトランスフォームが複数のコライダーに影響されると、その分だけ重ねて数える | 未確認 |
| コンタクトの数 | Contact の数（フィルタリングが「Local Only」の Receiver は数えない） | 32 | 16 | ― | 未確認 |
| コンストレイントの数 | VRChat のコンストレイントと Unity のコンストレイントの総数 | 350 | 150 | ― | 未確認 |
| コンストレイントの深さ | すべてのコンストレイントの依存関係の、最も深いつながり | 100 | 50 | ― | 未確認 |
| アニメーターの数 | Animator の数（ルートの Animator も数えるため、最低 1） | 32 | 2 | Excellent では、ルート以外の Animator を追加できない | 未確認 |
| ボーンの数 | アバターのリグ（骨組み）のボーンの数 | 400 | 150 | ― | 未確認 |
| ライトの数 | Light コンポーネントの数 | 1 | Android では無効（公式の Mobile Removed Components） | ― | 未確認 |
| パーティクルシステムの数 | Particle System コンポーネントの数 | 16 | 2 | ― | 未確認 |
| 最大パーティクルの数 | 各 Particle System の maxParticles（最大粒子数）の合計 | 2,500 | 200 | ― | 未確認 |
| メッシュパーティクルの三角ポリゴン数 | 有効なメッシュパーティクルの三角形の数（maxEmission × meshParticleVerts） | 5,000 | 400 | ― | 未確認 |
| パーティクルトレイル | 有効なパーティクルシステムに 1 つでもトレイルがあるか（True / False） | True | True | ― | 未確認 |

### この画面の読み方

- PHYSBONE コンポーネントの数（170/32）と、PHYSBONE トランスフォームの数（409/256）は、上限を超えています。
- PHYSBONE 衝突チェックの数（329/512）は、上限以下です。ただし、Medium の上限（256）を超えているので赤です。
- ボーンの数（432/400）は、上限をわずかに超えています。
- 最大パーティクルの数（622,022/2,500）と、メッシュパーティクルの三角ポリゴン数（4,000,000/5,000）は、上限の 200 倍以上です。
- ライトの数（1/1）は、上限ちょうどです。Poor の範囲なので赤です。
- PHYSBONE コライダーの数（7/32）、コンタクトの数（2/32）、コンストレイントの数（199/350）、コンストレイントの深さ（34/100）、アニメーターの数（2/32）は、上限以下で緑です。
- パーティクルトレイルは「FALSE/あり」で、緑です。

## 3. その他の項目

[![パフォーマンスの内訳の下の部分（パーティクルの衝突、トレイルレンダラー、CLOTH、コライダー、リジッドボディ、オーディオソース、レイキャスト、FORWARDED PARAMETERS、FACE TRACKING OPERATIONS）](images/performance-breakdown-3-others.png)](/learn/optimize-basics/images/performance-breakdown-3-others.png)
*パフォーマンスの内訳の下の部分。パーティクルの衝突から FACE TRACKING OPERATIONS まで*

### 項目

| 項目（画面の表示） | 何を数えているか（公式の定義） | PC の Poor の上限 | Android の Poor の上限 | 公式の補足 | 減らす効果（公式の記述） |
|---|---|---|---|---|---|
| パーティクルの衝突 | 有効なパーティクルシステムに 1 つでも衝突（Collision）があるか（True / False） | True | True | ― | 未確認 |
| トレイルレンダラーの数 | Trail Renderer の数（尾を引く線を描く部品） | 8 | 1 | 範囲（BOUNDS）の計算には入らない | 未確認 |
| CLOTH メッシュの数 | Cloth コンポーネントの数（布の動きを物理で計算する部品） | 1 | Android では無効（公式の Mobile Removed Components） | Cloth は、Skinned Mesh Renderer と組み合わせて使う（Unity マニュアル） | 未確認 |
| CLOTH の最大頂点の数 | 公式の項目名は Total Cloth Vertices。定義の行は、公式の統計表に見当たらない。「最大」の意味は未確認 | 200 | Android では無効（公式の Mobile Removed Components） | ― | 未確認 |
| コライダーの数 | 公式の項目名は Physics Colliders。定義の行は見当たらず、Unity の Collider（物理の衝突の形を決める部品）の数かは未確認（推定） | 8 | Android では無効（公式の Mobile Removed Components） | ― | 未確認 |
| リジッドボディの数 | 公式の項目名は Physics Rigidbodies。定義の行は見当たらず、Unity の Rigidbody（物理演算で動く部品）の数かは未確認（推定） | 8 | Android では無効（公式の Mobile Removed Components） | ― | 未確認 |
| オーディオソースの数 | 公式の項目名は Audio Sources。定義の行は見当たらず、Unity の Audio Source（音を鳴らす部品）の数と推定 | 8 | Android では無効（公式の Mobile Removed Components） | ― | 未確認 |
| レイキャストの数 | VRCRaycast コンポーネントの数 | 15 | 8 | 全プラットフォームで 1 アバターあたり 80 個が上限（固定の上限、公式の脚注） | 未確認 |
| FORWARDED PARAMETERS | Face Tracking の Parameter Behavior で「Forward to Animator」を使う Avatar Parameter の数 | 40 | 40 | Forward to Animator は、その値で Animator の同名のパラメーターを更新する | Animator に必要な値だけを転送する（公式の勧め） |
| FACE TRACKING OPERATIONS | Face Tracking の計算の作業量の推定値。各 BlendShape の対応づけは、対象の BlendShape を持つ Face Tracking Meshes のメッシュごとに 1 回ずつ数える。Parameter Behavior の入力や、Custom mode の Expression の入力・演算子・関数も数える（数値の定数は数えない） | 16,000 | 16,000 | 公式の例: 1 つの Blendshapes Set に 100 の対応づけがあり、対象の 2 つのメッシュに同じ BlendShape がある場合は 200。同じ Blendshapes Set をもう 1 つ足すと、Default Weight が 0 でも、さらに 200。Custom expression の上限は別で、ランクには影響しない | 直接の BlendShape の対応づけと、Face Tracking 内の計算を優先する（公式の勧め） |

### この画面の読み方

- トレイルレンダラーの数（5/8）は、上限以下ですが、Medium の上限（4）を超えているので赤です。
- リジッドボディの数（2/8）は、Medium の範囲なので黄色です。
- コライダーの数（1/8）とオーディオソースの数（2/8）は、Good の範囲なので、濃い緑です。
- CLOTH メッシュの数（0/1）、CLOTH の最大頂点の数（0/200）、レイキャストの数（0/15）、FORWARDED PARAMETERS（0/40）、FACE TRACKING OPERATIONS（0/16,000）は、値が 0 で、緑です。
- パーティクルの衝突は「FALSE/あり」で、緑です。読み方は、前の節の「FALSE/あり」と同じです。

## 赤い項目から、どう手をつけるか

### まず、Very Poor の原因を見つける

公式では、どれか 1 項目でも Poor の上限を超えると、次のランク（Very Poor）になると書かれています。つまり、左の値が右の上限を超えている項目が、Very Poor の原因です。この画面では、次の 11 項目が当てはまります。

- 三角ポリゴンの数（287,318/70,000）
- 範囲（BOUNDS）
- スキンドメッシュの数（18/16）
- 基本メッシュの数（29/24）
- マテリアルスロットの数（88/32）
- PHYSBONE コンポーネントの数（170/32）
- PHYSBONE トランスフォームの数（409/256）
- ボーンの数（432/400）
- パーティクルシステムの数（20/16）
- 最大パーティクルの数（622,022/2,500）
- メッシュパーティクルの三角ポリゴン数（4,000,000/5,000）

PHYSBONE 衝突チェックの数（329/512）、ライトの数（1/1）、トレイルレンダラーの数（5/8）は、赤ですが上限を超えていません。Poor の範囲に入っている項目です。

### 手をつける順番（目安）

公式の順番は、この記事では確認していません（未確認）。次は、この画面を見たうえでの、この記事の目安です。

1. **上限の何倍も超えている項目から確かめる。** 最大パーティクルの数やメッシュパーティクルの三角ポリゴン数は、上限の 200 倍以上です。公式では、メッシュパーティクルの三角ポリゴン数は、maxEmission と meshParticleVerts の積とされています。パーティクルの設定を見直すと、大きく下がる可能性があります（この記事の推定）。
2. **数の多い PhysBone や、ボーン・メッシュ系を減らす。** 手順は [アバターを軽くする](/learn/vrchat/optimization) にまとめています。
3. **1 つ直すたびに、パフォーマンスの内訳をもう一度見る。** 赤い項目の数が減ったか、どの項目が変わったかを確かめます。

::: warning
**無効にしただけ** のオブジェクトも、ランクの計算に入ります（公式の記述）。使わない衣装や小物は、Tag を EditorOnly にするか削除してください。EditorOnly は Unity のタグで、このタグを付けたオブジェクトは、Unity でビルドするときに削除されます（Unity マニュアル）。ただし、EditorOnly にしたものが SDK のランクの計算から外れるかは、この記事では未確認です。これは、この記事の助言です。

**Mesh Read/Write**（Unity の Read/Write Enabled。スクリプトからメッシュのデータを読み書きできるようにする設定）を 1 つでも無効にすると、ランクは即座に Very Poor になります（公式の記述）。
:::

## 参考

- [Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)
- [Avatar Size Limits（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-size-limits)
- [Avatar Fallback System（VRChat 公式）](https://docs.vrchat.com/docs/avatar-fallback-system)。このページの冒頭には、「フォールバックは impostor に置き換えられつつあり、Web では選べるが VRChat の中では選べない」という警告があります。
- [Tags（Unity マニュアル）](https://docs.unity3d.com/Manual/Tags.html)・[FBX Importer（Unity マニュアル。Read/Write）](https://docs.unity3d.com/Manual/FBXImporter-Model.html)・[描画の呼び出しの最適化（Unity マニュアル）](https://docs.unity3d.com/Manual/optimizing-draw-calls.html)

この記事は 2026 年 11 月 5 日時点の公式ページを元にしています。公式の Avatar Performance Ranking System は、ページ末尾に 2026 年 10 月 21 日の最終更新の記載があり、Avatar Size Limits は 2026 年 10 月 7 日の最終更新の記載があります。上限の値は見直されることがあるので、最新の値は公式ページで確かめてください。
