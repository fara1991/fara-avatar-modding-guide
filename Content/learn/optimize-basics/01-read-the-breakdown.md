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
- **未確認**: この記事では確認していない（無い・できないという意味ではない）。

## 画面の読み方の決まり

この記事の「推定」は、画面の見た目や、公式の近い文言から読んだものです。確かめた事実ではありません。

### 「/」の右の数字は、PC の Poor の上限

「287,318/70,000」のように `/` で並んでいる項目は、左がこのアバターの値、右が **PC の Poor の上限** です。Poor の上限は、Poor でいられる最大の値です。公式では、どれか 1 項目でも上限を超えると、次のランクになると書かれています。数値の項目では、右の数字はすべて PC の Poor の上限と一致します。「FALSE/あり」の項目は、True が Medium と Poor の両方の上限だと読めます（推定。後の節を参照）。左の値が右の数字を超えている項目は、Poor の上限を超えています。

### 色の意味

画面には凡例（色の説明）が出ていません。公式には、次の一文があります。

> The color of the text matches the rank that the particular stat "drags" the rank down to.
> （文字の色は、その項目がランクを引き下げる先のランクに一致する、という意味です）

この記事の緑・黄・赤の区分は、この記述と、[パフォーマンスランク](/learn/vrchat/performance-rank) の表の上限の範囲から読んだものです。

- 緑: Excellent または Good の範囲
- 黄: Medium の範囲
- 赤: Poor の範囲、またはそれを超えた範囲

同じ緑でも明るさが違う項目があります。Excellent と Good のどちらかは、[パフォーマンスランク](/learn/vrchat/performance-rank) の表と比べて確かめてください。この記事で確かめた範囲では、画面の色（見た目）はすべての項目で上の区分と一致していました。

赤には、Poor の範囲と、Poor の上限を超えた範囲（Very Poor）の両方が入ります。この 2 つが色で分かれているかは、未確認です。

### 数字の無い項目

ダウンロードサイズと非圧縮サイズは、「/」の右の数字がなく、色の段階もありません。値だけが灰色で出ます。サイズの上限は、ランクの表とは別に、公式の [Avatar Size Limits](https://creators.vrchat.com/avatars/avatar-size-limits) にあります。PC は Download 200 MB・Uncompressed 500 MB、Android は Download 10 MB・Uncompressed 40 MB です（ページの最終更新は 2026 年 10 月 7 日）。この画面の値（Windows で 37.12 MB・138.38 MB）は、どちらも PC の上限より小さい値です。公式では、Build & Test を使う場合は、サイズの上限は適用されないと書かれています。

テクスチャメモリーも、画面には上限（「/」の右の数字）が出ません（93.50 MB と値だけが出ます）。公式のランクの上限は、下の表のとおりです。そのため、色で段階を読みます。

### 「使用不可」（Android・iOS）

Android と iOS の欄に出る「使用不可」（灰色）の意味について、この記事では、画面の文言と完全に一致する定義を確認していません。近い文言は次の 2 つあり、どちらかを指していると推定します。

- **そのプラットフォームでは表示できないアバターです。** 公式の [Avatar Fallback System](https://docs.vrchat.com/docs/avatar-fallback-system) には「Avatar not available for your platform.」（そのプラットフォームでは使えない）という表示の説明があります。Windows 向けのアバターは Quest/Android では表示できず、作者が PC と Quest/Android の両方のアバターをアップロードすると解決できる、と書かれています。
- **Very Poor で、モバイルでは表示されないことがある。** 公式の [Performance Ranks](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) には、モバイルの Very Poor のアバターは VRChat で表示されないことがある（原文: "may not display at all"）と書かれています。例として、三角ポリゴンが 20,000 を超えるモバイルのアバターは、ユーザーには見えない（原文: "users can't see it"）とされています。「Show Avatar」を選べば、強制的に表示することはできます。このアバターは、Android の三角ポリゴンの上限（20,000）を大きく超えているため、こちらの読み方も当てはまります。

公式の Performance Ranks には、将来、モバイルの Very Poor のアバターが Android・iOS から無くなることや、「Show Avatar」が使えなくなることがある、という注意もあります。

iOS については、Avatar Fallback System のページで確認できたのは、フォールバックの条件として「currently Windows and Android」（現在は Windows と Android）と書かれていることだけです。そのため、iOS の「使用不可」の意味は未確認です。一方、Performance Ranks のページは、Android と iOS のモバイル向けの上限を同じ表で示しています。

### 「FALSE/あり」（パーティクルトレイル・パーティクルの衝突）

この 2 項目は、数字ではなく「FALSE/あり」と出ます。左は、このアバターの値です。公式では、パーティクルシステムの 1 つにでも Particle Trails（トレイル。尾を引く表示）や Particle Collision（衝突）が有効になっていると True になります。FALSE は、そうしたものが 1 つも有効になっていないことを表すと読めます。

右の「あり」は、PC の Poor の上限（True）に当たると読めます。公式の表では、Excellent と Good は False、Medium と Poor は True です。ただし、画面の「あり」という文言の定義は未確認のため、この読み方は推定です。

### 表の読み方

- 表の「公式の定義・補足」は、公式の統計表の説明（定義）と、公式の注記（補足）をまとめたものです。「補足: ―」は、この記事の表では補足を載せていない項目です（公式の統計表の説明に補足が見当たらない項目）。
- 「上限（PC / Android）」は、左が PC、右が Android の Poor の上限です。「モバイル（Android・iOS）では無効」は、公式の Mobile Removed Components に挙げられた項目です。
- 「減らし方（公式の勧め）」の列は、公式の減らし方の勧めを出典つきで書いています。出典は、Avatar Optimization Tips（公式の勧め）、Avatar Size Limits（サイズの減らし方）、Performance Ranks › Avatar Performance Ranking Stats › Face Tracking のいずれかです。公式は勧めを書いているだけで、効果の大きさは書かれていないため、この列には書きません。「未確認」は、この記事では確認していない項目です（無い・できないという意味ではない）。

## 1. プラットフォーム欄とメッシュ系

[![パフォーマンスの内訳の上の部分（プラットフォーム欄と、ダウンロードサイズからマテリアルスロットまで）](images/performance-breakdown-1-platforms-and-mesh.png)](/learn/optimize-basics/images/performance-breakdown-1-platforms-and-mesh.png)
*パフォーマンスの内訳の上の部分。プラットフォーム欄と、ダウンロードサイズからマテリアルスロットの数まで*

### プラットフォーム欄

- **Windows（現在のプラットフォーム）**: いま見ているパソコン向けの判定です。この画面では **Very Poor** です。
- **Android・iOS**: 「使用不可」（灰色）です。読み方は前の節のとおりです。

### 項目

| 項目（画面の表示） | 公式の定義・補足 | 上限（PC / Android） | 減らし方（公式の勧め） |
|---|---|---|---|
| ダウンロードサイズ | 圧縮後のアバターの大きさ（asset bundle を圧縮したサイズ）。補足: SDK は、ビルド時に上限を超えるとアップロードを止める | ランクの表の項目ではない（サイズの上限は上の節を参照） | サイズを減らす手順が公式にある（Avatar Size Limits。テクスチャの最大サイズ、音声の長さと品質、アニメーションのキーフレーム、ブレンドシェイプなど） |
| テクスチャメモリー | テクスチャが使うメモリーの推定値。補足: テクスチャは、システム RAM と GPU のメモリーの両方を使う | 150 MB / 40 MB | 公式の勧め（Avatar Optimization Tips）: Unity のインポート設定で Max Size を下げる。高解像度のテクスチャを何枚も使わない |
| 非圧縮サイズ | 展開（非圧縮）したあとの asset bundle の大きさ。補足: 個々のアセットは展開せず、bundle 全体で数える。テクスチャの圧縮を High Quality にすると、ダウンロードと非圧縮の両方が増える | ランクの表の項目ではない（サイズの上限は上の節を参照） | サイズを減らす手順は同上 |
| 三角ポリゴンの数 | モデルの三角ポリゴンの数（公式の項目名は Triangles。以前は誤って「Polygons」と呼ばれていた）。補足: Mesh Read/Write を、いずれかの mesh（particle systems を含む）で無効にすると、この項目は「Mesh Read/Write Disabled」と表示され、ランクは即座に Very Poor になる（下の注意を参照） | 70,000 / 20,000 | 公式の勧め（Avatar Optimization Tips）: 法線マップの焼き込み、decimation（間引き）や retopology（作り直し）によるメッシュの簡略化を検討する |
| 範囲（BOUNDS） | アバター全体の大きさ（3 方向の寸法）。Trail Renderer と Line Renderer は計算に入らない。補足: 大きいと、常時は見えない大きなアニメーションがある可能性がある。範囲（BOUNDS）は、アバターの表示が制限される理由にはなりません（公式の注記） | 5 × 6 × 5 m / 5 × 6 × 5 m | 未確認 |
| スキンドメッシュの数 | Skinned Mesh Renderer（ボーンで動くメッシュ）のコンポーネントの数。補足: ― | 16 / 2 | 公式の勧め（Avatar Optimization Tips）: メッシュを統合する。Skinned Mesh Renderer は 1 つまで、静的（Static）メッシュは 3 つまでを推奨（ランクの上限とは別の推奨値） |
| 基本メッシュの数 | ボーンで動かない Mesh Renderer のコンポーネントの数。補足: ― | 24 / 2 | 公式の勧め（Avatar Optimization Tips）: メッシュを統合する。静的（Static）メッシュは 3 つまでを推奨（ランクの上限とは別の推奨値） |
| マテリアルスロットの数 | マテリアルを割り当てる枠の数。補足: パーティクルシステムは 1 枠、トレイル付きのパーティクルシステムは 2 枠、Line Renderer は 1 枠を使う | 32 / 4 | 公式の勧め（Avatar Optimization Tips）: マテリアルが 10 を超えるなら、テクスチャアトラス（Texture Atlasing。複数のテクスチャを 1 枚にまとめたもの）を検討する |

- 範囲（BOUNDS）の上限（5 × 6 × 5 m）の単位は m です。画面の値（200.00, 169.42, 225.89）には、単位が表示されていません。
- Android の欄について: 公式の脚注によると、PhysBone（ボーンを物理やほかのプレイヤーの影響で動かす仕組み（髪や服の揺れなどに使われる））・コンタクト（Contact。触れたときに反応する仕組み）・VRChat のコンストレイント（Constraint。別のオブジェクトに合わせて、位置・回転・大きさを動かす部品）は、モバイル（Android・iOS）で Very Poor の値（Poor の上限を超えた値）になると、Show Avatar の状態にかかわらず、すべて取り除かれます。別の条件として、公式の Mobile Avatar Component Limits があります。PhysBone の数 8、PhysBones の影響を受けるトランスフォーム 64、コライダー 16、衝突チェック 64、コンタクト 16、コンストレイント 150、コンストレイントの依存の深さ 50 を超えると、制限の対象の部品がすべて取り除かれ、「Show Avatar」を選んでも回避できません。
- 描画の呼び出し（ドローコール）は、メッシュごとに 1 回ずつ必要になるのが基本で、減らすと描画の時間が短くなります（Unity マニュアル）。

### この画面の読み方

- 三角ポリゴンの数（287,318/70,000）、範囲（BOUNDS）、スキンドメッシュの数（18/16）、基本メッシュの数（29/24）、マテリアルスロットの数（88/32）は、いずれも上限を超えています（赤）。マテリアルスロットは、上限の約 2.75 倍です。
- テクスチャメモリー（93.50 MB）は黄色で、Medium の範囲です。
- ダウンロードサイズと非圧縮サイズは、「/」の右の数字がなく、色の段階も、分母もありません。

## 2. PhysBone・コンタクト・コンストレイント・パーティクルなど

アニメーター（Animator）は、アニメーションを制御する部品です。PhysBone（ボーンを物理やほかのプレイヤーの影響で動かす仕組み（髪や服の揺れなどに使われる））・コンタクト（Contact。触れたときに反応する仕組み）・コンストレイント（Constraint。別のオブジェクトに合わせて、位置・回転・大きさを動かす部品）は、次の表に出てきます。モバイルで取り除かれる場合の注意は、上の「Android の欄について」を参照してください。

[![パフォーマンスの内訳の中の部分（PhysBone、コンタクト、コンストレイント、アニメーター、ボーン、ライト、パーティクル）](images/performance-breakdown-2-physbone-and-particles.png)](/learn/optimize-basics/images/performance-breakdown-2-physbone-and-particles.png)
*パフォーマンスの内訳の中の部分。PhysBone からパーティクルトレイルまで*

### 項目

| 項目（画面の表示） | 公式の定義・補足 | 上限（PC / Android） | 減らし方（公式の勧め） |
|---|---|---|---|
| PHYSBONE コンポーネントの数 | PhysBone コンポーネントの数。補足: ― | 32 / 8 | 未確認 |
| PHYSBONE トランスフォームの数 | PhysBone が影響を与えるトランスフォームの数（公式の項目名は PhysBones Affected Transforms）。補足: ― | 256 / 64 | 未確認 |
| PHYSBONE コライダーの数 | PhysBone のコライダーのスクリプトの数。補足: ― | 32 / 16 | 未確認 |
| PHYSBONE 衝突チェックの数 | 各コライダーが影響しうるトランスフォーム数の合計。補足: 同じトランスフォームが複数のコライダーに影響されると、その分だけ重ねて数える | 512 / 64 | 未確認 |
| コンタクトの数 | Contact の数（フィルタリングが「Local Only」の Receiver は数えない）。補足: ― | 32 / 16 | 未確認 |
| コンストレイントの数 | VRChat のコンストレイントと Unity のコンストレイントの総数。補足: ― | 350 / 150 | 公式の勧め（Avatar Optimization Tips）: Unity の Constraint は使わず、VRChat の Constraint を使う |
| コンストレイントの深さ | すべてのコンストレイントの依存関係の、最も深いつながり。補足: ― | 100 / 50 | 公式の考え方（Constraints）: いちばん長いつながりを短くする（詳しくは「コンストレイントの深さを減らす」） |
| アニメーターの数 | Animator の数（ルートの Animator も数えるため、最低 1）。補足: Excellent では、ルート以外の Animator を追加できない | 32 / 2 | 未確認 |
| ボーンの数 | アバターのリグ（骨組み）のボーンの数。補足: ― | 400 / 150 | 公式の勧め（Avatar Optimization Tips）: 使わないボーンは、削除して親のボーンに統合することを検討する |
| ライトの数 | Light コンポーネントの数。補足: ― | 1 / モバイル（Android・iOS）では無効 | 公式の勧め（Avatar Optimization Tips）: 常時点灯のライトは使わない。アバターではライトを使わないことを勧めている |
| パーティクルシステムの数 | Particle System コンポーネントの数。補足: ― | 16 / 2 | 公式の勧め（Avatar Optimization Tips）: パーティクルシステムの数を制限する |
| 最大パーティクルの数 | 各 Particle System の maxParticles（最大粒子数）の合計。補足: ― | 2,500 / 200 | 公式の勧め（Avatar Optimization Tips）: 同時に放出する粒子の最大数を制限する |
| メッシュパーティクルの三角ポリゴン数 | 有効（active）なパーティクルシステムが出すメッシュパーティクルの三角ポリゴンの数（maxEmission × meshParticleVerts）。補足: ― | 5,000 / 400 | 公式の勧め（Avatar Optimization Tips）: 粒子の放出の数を減らす（見出しは「Reduce the emission amount」。粒子全般の勧めで、三角ポリゴン数への対応は推定） |
| パーティクルトレイル | Particle Trails を有効にしたパーティクルシステムが、アバターに 1 つでもあるか（True / False）。補足: ― | True / True | 未確認 |

### この画面の読み方

- PHYSBONE コンポーネントの数（170/32）と、PHYSBONE トランスフォームの数（409/256）は、上限を超えています。
- PHYSBONE 衝突チェックの数（329/512）は、上限以下です。ただし、Medium の値を超えているので赤です（Medium の値は、[パフォーマンスランク](/learn/vrchat/performance-rank) の表を参照）。
- ボーンの数（432/400）は、上限をわずかに超えています。
- パーティクルシステムの数（20/16）は、上限を超えています。
- 最大パーティクルの数（622,022/2,500）と、メッシュパーティクルの三角ポリゴン数（4,000,000/5,000）は、上限の 200 倍以上です。
- ライトの数（1/1）は、上限ちょうどです。Poor の範囲なので赤です。
- PHYSBONE コライダーの数（7/32）、コンタクトの数（2/32）、コンストレイントの数（199/350）、コンストレイントの深さ（34/100）、アニメーターの数（2/32）は、上限以下で緑です。
- パーティクルトレイルは「FALSE/あり」で、緑です。

## 3. その他の項目

[![パフォーマンスの内訳の下の部分（パーティクルの衝突、トレイルレンダラー、CLOTH、コライダー、リジッドボディ、オーディオソース、レイキャスト、FORWARDED PARAMETERS、FACE TRACKING OPERATIONS）](images/performance-breakdown-3-others.png)](/learn/optimize-basics/images/performance-breakdown-3-others.png)
*パフォーマンスの内訳の下の部分。パーティクルの衝突から FACE TRACKING OPERATIONS まで*

### 項目

| 項目（画面の表示） | 公式の定義・補足 | 上限（PC / Android） | 減らし方（公式の勧め） |
|---|---|---|---|
| パーティクルの衝突 | Particle Collision を有効にしたパーティクルシステムが、アバターに 1 つでもあるか（True / False）。補足: ― | True / True | 公式の勧め（Avatar Optimization Tips）: 多くの粒子を出すときは、衝突（collision）を使わない |
| トレイルレンダラーの数 | Trail Renderer の数（尾を引く線を描く部品）。補足: 範囲（BOUNDS）の計算には入らない | 8 / 1 | 未確認 |
| CLOTH メッシュの数 | Cloth コンポーネントの数（布の動きを物理で計算する部品）。補足: Cloth は、Skinned Mesh Renderer と組み合わせて使う（Unity マニュアル） | 1 / モバイル（Android・iOS）では無効 | 公式の勧め（Avatar Optimization Tips）: Cloth の使用を控える。約 200 の頂点を超えるメッシュには使わない |
| CLOTH の最大頂点の数 | 公式の項目名は Total Cloth Vertices。公式の統計表（Performance Ranks）には定義の行を確認できなかった（未確認）。「最大」の意味も未確認。補足: ― | 200 / モバイル（Android・iOS）では無効 | 未確認 |
| コライダーの数 | 公式の項目名は Physics Colliders。公式の統計表（Performance Ranks）には定義の行を確認できなかった（未確認）。Unity の Collider（物理の衝突の形を決める部品）の数と推定。補足: ― | 8 / モバイル（Android・iOS）では無効 | 未確認 |
| リジッドボディの数 | 公式の項目名は Physics Rigidbodies。公式の統計表（Performance Ranks）には定義の行を確認できなかった（未確認）。Unity の Rigidbody（物理演算で動く部品）の数と推定。補足: ― | 8 / モバイル（Android・iOS）では無効 | 未確認 |
| オーディオソースの数 | 公式の項目名は Audio Sources。公式の統計表（Performance Ranks）には定義の行を確認できなかった（未確認）。Unity の Audio Source（音を鳴らす部品）の数と推定。補足: ― | 8 / モバイル（Android・iOS）では無効 | 未確認 |
| ラインレンダラーの数（画面には出ない項目） | アバターにある Line Renderer の数（公式の項目名は Line Renderers）。補足: ― | 8 / 1 | 未確認 |
| レイキャストの数 | VRCRaycast コンポーネントの数。補足: 全プラットフォームで 1 アバターあたり 80 個が上限（固定の上限、公式の脚注） | 15 / 8 | 未確認 |
| FORWARDED PARAMETERS | Face Tracking の Parameter Behavior で「Forward to Animator」を使う Avatar Parameter の数。補足: Forward to Animator は、その値で Animator の同名のパラメーターを更新する | 40 / 40 | 公式の勧め（Performance Ranks › Avatar Performance Ranking Stats › Face Tracking）: Animator に必要な値だけを転送する |
| FACE TRACKING OPERATIONS | Face Tracking の計算の作業量の推定値。Blendshapes Set ごとの tracking または viseme の mapping を、対象の BlendShape を持つ Face Tracking Meshes のメッシュごとに 1 回ずつ数える。Parameter Behavior の入力や、Custom mode の Expression の入力・演算子・関数も数える（数値の定数は数えない）。補足: 例は、表の下の箇条書きを参照 | 16,000 / 16,000 | 公式の勧め（Performance Ranks › Avatar Performance Ranking Stats › Face Tracking）: 直接の BlendShape の対応づけと、Face Tracking 内の計算を優先する |

- FACE TRACKING OPERATIONS の公式の例: 1 つの Blendshapes Set に 100 の対応づけがあり、対象の 2 つのメッシュに同じ BlendShape がある場合は 200。同じ Blendshapes Set をもう 1 つ足すと、Default Weight が 0 でも、さらに 200。
- Custom expression には、別の上限（expression limits）があります。公式では、この上限はランクに影響しないと書かれています。

### この画面の読み方

- トレイルレンダラーの数（5/8）は、上限以下ですが、Medium の値を超えているので赤です（Medium の値は、[パフォーマンスランク](/learn/vrchat/performance-rank) の表を参照）。
- リジッドボディの数（2/8）は、Medium の範囲なので黄色です。
- 画面の濃い緑は Good、明るい緑は Excellent に当たるように見えます（推定）。コライダーの数（1/8）とオーディオソースの数（2/8）は、Good の範囲なので、濃い緑です。
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
**無効にしただけ** のオブジェクトも、ランクの計算に入ると公式に書かれています。EditorOnly にすると、ビルドから外れます（Unity マニュアル）。SDK のランクの計算から外れるかは未確認です。確実なのは、使わないものを削除することです。以下はこの記事の助言です。使わない衣装や小物は、Tag を EditorOnly にするか、削除してください。

**Mesh Read/Write**（Unity の Read/Write Enabled。スクリプトからメッシュのデータを読み書きできるようにする設定）を、いずれかの mesh（particle systems を含む）で無効にすると、ランクは即座に Very Poor になります（公式の記述）。
:::

## 参考

- [Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)
- [Avatar Size Limits（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-size-limits)
- [Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)
- [Avatar Fallback System（VRChat 公式）](https://docs.vrchat.com/docs/avatar-fallback-system)。このページの冒頭には警告があります（要旨）。フォールバックは impostor に置き換えられつつあり、Web では選べるが VRChat の中では選べない、という内容です。
- [Tags（Unity マニュアル）](https://docs.unity3d.com/Manual/Tags.html)・[FBX Importer（Unity マニュアル。Read/Write）](https://docs.unity3d.com/Manual/FBXImporter-Model.html)・[描画の呼び出しの最適化（Unity マニュアル）](https://docs.unity3d.com/Manual/optimizing-draw-calls.html)

この記事は 2026 年 11 月 6 日に確認した公式ページを元にしています。公式の Avatar Performance Ranking System は、ページ末尾に 2026 年 10 月 21 日の最終更新の記載があり、Avatar Size Limits は 2026 年 10 月 7 日の最終更新の記載があり、Avatar Optimization Tips も 2026 年 10 月 7 日の最終更新の記載があります。上限の値は見直されることがあるので、最新の値は公式ページで確かめてください。
