---
title: 軽量化に使う 5 つのツールと早見表
reading: けいりょうかにつかういつつのつーるとはやみひょう
description: VRChat のアバターを軽くする 5 つのツール（MA・TTT・lilAvatarUtils・MMS・AAO）の役割と、28 項目のどれを減らせるかを一覧にします。
difficulty: 初級
updated: 2026-11-05
---

VRChat のアバターを軽くするツールは何種類もあり、ツールごとに減らせる項目が違います。この記事では、よく使われる 5 つのツールについて、何をするか、どの項目を減らせるかを整理します。

略称は、MA（Modular Avatar）、TTT（TexTransTool）、MMS（Meshia Mesh Simplification）、AAO（Avatar Optimizer）です。lilAvatarUtils は、そのまま書きます。

ツールを入れるときに出てくる VCC と ALCOM は、どちらもパッケージを管理するツールです。VCC（VRChat Creator Companion）は公式のツールで、ALCOM は VCC と同じことができる非公式のツールです（[用語集: VCC](/glossary#vcc)）。

表の項目名は、画面に表示される名前に合わせています。項目の上限の値は、[パフォーマンスランク](/learn/vrchat/performance-rank) にあります。PhysBone は、髪や服を揺らす仕組みです（[用語集: PhysBone](/glossary#physbone)）。コンストレイントは、ある物の位置や向きを、別の物に合わせる仕組みです。

各ツールのコンポーネントを追加するメニュー名は、公式に記載なしのものが多いため、この記事ではコンポーネント名だけを書いています。

::: warning
ツールは更新されることがあります。この記事は **2026 年 11 月 5 日** に公式ページを確かめて書きました。版が変わっているときは、公式ページの最新の情報を優先してください。
:::

## 1. Modular Avatar（MA）

Modular Avatar は、衣装などを本体に組み立てるためのツール群です。衣装のボーンを本体に統合する Merge Armature や、アニメーターを統合する Merge Animator などがあります。公式は「非破壊」のツール群だと説明しています。非破壊とは、元のデータを書き換えずに、Play モードやアップロードのときに処理する、という意味です。

- **確認した版**: 1.18.7（2026 年 9 月 1 日。公式の[変更履歴](https://modular-avatar.nadena.dev/ja/docs/changelog)）。テスト版は 1.19.0-beta.0 です。
- **入れ方**:
  - ALCOM: 「パッケージ管理」から「VPMリポジトリを追加」を選び、リポジトリ `https://vpm.nadena.dev/vpm.json` を追加します。プロジェクトの「管理」で Modular Avatar の「+」を押して、「適用」します。
  - VCC: 公式ページのリンクからリポジトリを追加し、manage project で「+」を押します。
  - テスト版は、公式が ALCOM から入れるよう案内しています。VCC では依存関係の解決にバグがあると書かれています。
- **ライセンス**: MIT License。ただし `Editor/images` の画像は MIT の対象外で、公式パッケージの一部としてのみ再配布できます。
- **対応**: Unity 2022.3（main ブランチ 1.19.0-beta.0 の package.json の値）。1.18.x の値は未確認です。VRChat SDK の対応版は、公式に記載なしです。NDMF（ビルド時の処理を動かす仕組み）が必要です。
- **公式ページ**: https://modular-avatar.nadena.dev/ja （GitHub: https://github.com/bdunderscore/modular-avatar ）
- **減らせる主な項目**: ボーン（Merge Armature）、アニメーター（Merge Animator）、三角ポリゴン（Mesh Cutter・条件つき）
- **注意**:
  - Mesh Cutter は、メッシュの一部を削除または非表示にできるコンポーネントです（[変更履歴](https://modular-avatar.nadena.dev/ja/docs/changelog) の 1.14.0 で実装）。常にオンにしたとき、指定した部分のポリゴンが削除されます。部位が見えなくなるので、見た目も変わります。
  - Merge Animator の「付属アニメーターを削除」をオンにすると、同じオブジェクトの Animator は除外されます。同じレイヤーの種類で置き換えの設定が複数あると、エラーになります。
  - Merge Motion（Blend Tree 版）は、時間で変わるアニメーションには向きません。
  - 衣装を組み込む手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。

## 2. TexTransTool（TTT）

TexTransTool は、テクスチャを元のまま残して加工するツールです。公式は「非破壊テクスチャ改変ツール」と説明しています。デカール（テクスチャに画像を貼る機能）や PSD の取り込みなどがあります。アトラス化は、必要な部分だけを集めた 1 枚のテクスチャを作る機能です。VRAM（グラフィックボードのメモリー）を減らす目的で使います。

- **確認した版**: v1.0.2（2026 年 6 月 26 日。GitHub のリリース）。
- **入れ方**: VCC か ALCOM から、パッケージ `net.rs64.tex-trans-tool` を入れます。VPM リポジトリは `https://vpm.rs64.net/vpm.json` です。公式サイトの「Download-VPM」ボタンからも追加できます。
- **ライセンス**: 公式リポジトリの LICENSE.md（master）は、MIT License（Copyright (c) 2023 Reina_Sakiria）です。ただし、`Image` 以下のファイルは MIT の対象外で、公式パッケージの一部としてのみ再配布が許可されています。v1.0.2 のタグの LICENSE は未確認です。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK の対応版は、公式に記載なしです。アバターの改変に使うときは、NDMF が必須です。公式は、同時に入れることを強く推奨しています。
- **公式ページ**: https://ttt.rs64.net （チュートリアル: https://ttt.rs64.net/docs/Tutorial ）
- **減らせる主な項目**: テクスチャメモリー（条件つき）
- **注意**:
  - テクスチャの圧縮は、プレビューでは行われません。確かめるには、NDMF の Apply On Play や Manual Bake などを使います（[公式: AtlasTexture](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - アトラス化の出力をそのまま使うと、VRAM が増える場合があると書かれています（[公式: AtlasTexture](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - TextureConfigurator は実験的なコンポーネントです。予告なく機能が変わったり、削除されたりすることがあります（[公式: TextureConfigurator](https://ttt.rs64.net/docs/Reference/TextureConfigurator)）。
  - 元に戻す（Revert）方法は、未確認です。

## 3. lilAvatarUtils

lilAvatarUtils は、アバターの改変を助けるツールです。公式は「Utilities for avatar modification.」と書き、日本語のサイトでは「アバター改変補助ツール」と紹介しています。

テクスチャ、マテリアル、レンダラー、PhysBone、PBコライダーの一覧を、タブで確認できます。一部のプロパティは編集でき、変更は「適用」ボタンでまとめて反映されます。ライティングのタブでは見え方を確かめ、ツールのタブでは不要なものを整理できます。

- **確認した版**: 2.1.2（2025 年 8 月 11 日。公式の [CHANGELOG](https://github.com/lilxyzw/lilAvatarUtils/blob/master/CHANGELOG.md) の記載）。
- **入れ方**: VCC は、公式サイトの「Add to VCC」ボタンで VPM リポジトリ `https://lilxyzw.github.io/vpm-repos/vpm.json` を追加します。ALCOM での入れ方は、公式に記載なしです。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK の対応版は、公式に記載なしです。
- **メニュー**: Unity の `Tools/lilAvatarUtils` から開きます。
- **公式ページ**: https://lilxyzw.github.io/lilAvatarUtils/ja_JP/
- **減らせる主な項目**: テクスチャメモリー（テクスチャのタブで解像度などを確認できます）。PhysBone と PBコライダーは、一覧で確認するためのタブです。減らす操作は、公式に記載なしです。
- **注意**:
  - 元のアセットを書き換えるかどうかは、公式に記載なしです。「適用」ボタンで変更をまとめて反映するので、作業の前にプロジェクトのバックアップを取るのが安全です。
  - 半透明マテリアルのレンダーキューを 2450 以下などに下げすぎると、ほかのマテリアルが消えやすくなると書かれています（公式のマテリアルのタブの説明）。
  - Bounds の範囲に収まっていないメッシュは、画面内にあるはずでも消えたり、ライトが暗くなったりすることがあります（公式のレンダラーのタブの説明）。

## 4. Meshia Mesh Simplification（MMS）

Meshia Mesh Simplification は、メッシュの三角ポリゴンの数を減らす（簡略化する）ツールです。アバター向けの「Meshia Cascading Avatar Mesh Simplifier」を使うと、アバター全体の目標を決めて、各メッシュに数を配分できます。目標は、性能ランクを選ぶ形です。

- **確認した版**: 3.3.1（2026 年 11 月 4 日に公開。GitHub のリリース）。
- **入れ方**: VCC に作者の VPM リポジトリ `https://ramtype0.github.io/VpmRepository/` を追加し、Manage Project から Manage Packages を開いて Meshia Mesh Simplification を追加します。ALCOM での入れ方は、公式に記載なしです。
- **ライセンス**: MIT License（`Copyright (c) 2025 Ram.Type-0`）。
- **対応**: Unity 2022.3（パッケージの情報）。パッケージには `vrchatVersion: 2022.1.1` と書かれていますが、その意味は公式に記載なしです。
- **依存**: NDMF がプロジェクトに入っていると、MeshiaMeshSimplifier を使えます（公式の使い方）。ほかに必要なツールは、公式ドキュメントのインストール手順では未確認です。
- **公式ページ**: https://ramtype0.github.io/Meshia.MeshSimplification/ （README: https://github.com/RamType0/Meshia.MeshSimplification ）
- **減らせる主な項目**: 三角ポリゴンの数（目標の数を指定）
- **注意**:
  - 目標を下げすぎると、形が崩れやすくなります。どの数値から崩れるかは、公式に記載なしです。
  - 指先に穴があいたら、Preserve Border Edges を有効にすると公式が案内しています。
  - 非破壊かどうかは、公式に記載なしです。作業の前にバックアップを取るのが安全です。
  - Avatar Optimizer との連携は、公式に記載なしです。

## 5. Avatar Optimizer（AAO）

Avatar Optimizer は、アップロードのとき（ビルドのとき）や、Play モードに入るときに、アバターを自動で軽くする非破壊のツール群です。元のアバターは変わらず、ビルドのときの一時的なコピーにだけ働きます。公式は、見た目や動きを変えずに性能を上げることを目標にしていて、意図しない変化は不具合として扱うと、FAQ に書いています。

- **確認した版**: 1.9.21（2026 年 10 月 30 日）。
- **入れ方**: 公式は ALCOM から入れることを推奨しています。VCC では正しく動かないことがあるためです。ALCOM で VPM リポジトリ `https://vpm.anatawa12.com/vpm.json` を追加してから、`com.anatawa12.avatar-optimizer` を追加します。UnityPackage や vrc-get からの入れ方も書かれています。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK は、`com.vrchat.avatars` の 3.7.0 以上 3.11.0 未満と書かれています（VPM の情報）。NDMF が必要です。
- **公式ページ**: https://vpm.anatawa12.com/avatar-optimizer/ja/ （GitHub: https://github.com/anatawa12/AvatarOptimizer ）
- **減らせる主な項目**: テクスチャメモリー、三角ポリゴンの数（隠れた部分）、スキンドメッシュの数、マテリアルスロットの数、PHYSBONE のコンポーネント・トランスフォーム・コライダーの数、ボーンの数（自動と手動の両方で減らせます）
- **注意**:
  - Trace And Optimize は、アバターのルート（VRC Avatar Descriptor のある GameObject）に付けます。
  - Merge Skinned Mesh で統合すると、メッシュを個別にアニメーションでオン・オフできなくなります。
  - マテリアルをアニメーションで切り替えるアバターでは、マテリアルの統合と衝突することがあります。公式の FAQ に、既知の不具合として書かれています。
  - 公式ドキュメントでは、Control Panel の Performance Rank は、非破壊ツールでは信頼できないとされています。Play モードで確かめる方法が案内されています。
  - 「使われていないObjectを自動的に削除する」は、アニメーションなどを走査して、使われていない GameObject・コンポーネント・テクスチャなどを自動で削除する機能です（[公式: Trace and Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

## 項目 × ツールの早見表

各マスは、公式の資料に書かれていることをもとに判定しています。

### メッシュ・テクスチャ・マテリアル

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| テクスチャメモリー | ？ 記載なし | △ | ○ | ？ 記載なし | ○ |
| 三角ポリゴンの数 | △ | ？ 記載なし | 確認のみ | ○ | ○ |
| 範囲（BOUNDS） | ？ 未確認 | ？ 未確認 | 確認のみ | ？ 記載なし | ？ 未確認 |
| スキンドメッシュの数 | ？ 記載なし | ？ 記載なし | 確認のみ | ？ 記載なし | ○ |
| 基本メッシュの数 | ？ 記載なし | ？ 記載なし | 確認のみ | ？ 記載なし | ○ |
| マテリアルスロットの数 | ？ 未確認 | ？ 記載なし | 確認のみ | ？ 未確認 | ○ |

### PhysBone・コンタクト・コンストレイント・アニメーター・ボーン

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| PHYSBONE コンポーネントの数 | ？ 記載なし | ？ 記載なし | 確認のみ | ？ 記載なし | ○ |
| PHYSBONE トランスフォームの数 | ？ 未確認 | ？ 記載なし | 確認のみ | ？ 記載なし | ○ |
| PHYSBONE コライダーの数 | ？ 記載なし | ？ 記載なし | 確認のみ | ？ 記載なし | ○ |
| PHYSBONE 衝突チェックの数 | ？ 未確認 | ？ 記載なし | 確認のみ | ？ 記載なし | ？ 未確認 |
| コンタクトの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 未確認 |
| コンストレイントの数 | ？ 未確認 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| コンストレイントの深さ | ？ 未確認 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| アニメーターの数 | △ | ？ 記載なし | ？ 未確認 | ？ 記載なし | ？ 未確認 |
| ボーンの数 | △ | ？ 記載なし | ？ 未確認 | ？ 記載なし | ○ |

### そのほかの項目

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| ライトの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| パーティクルシステムの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| 最大パーティクルの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| メッシュパーティクルの三角ポリゴン数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 未確認 | ？ 記載なし |
| パーティクルトレイル | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| パーティクルの衝突 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| トレイルレンダラーの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| CLOTH メッシュの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| CLOTH の最大頂点の数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 未確認 | ？ 記載なし |
| コライダーの数 | ？ 記載なし | ？ 記載なし | 確認のみ | ？ 記載なし | ？ 未確認 |
| リジッドボディの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| オーディオソースの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |
| レイキャストの数 | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし | ？ 記載なし |

### 記号の意味

- **○ 減らせる**: そのツールに、その項目を減らす操作が書かれています。
- **△ 条件つき**: 減る場合もありますが、条件があります。増える場合もあります。下の表の注を見てください。
- **確認のみ**: 数や値を一覧で確認できます。減らす操作は、公式に記載なしです。
- **× 減らせない**: 公式の資料に、そのツールでは減らせない、またはその機能がないと明記されています。この表には、該当するものはありません。
- **？ 記載なし**（本文では「公式に記載なし」と書きます）: 公式の資料に、その項目を減らす機能が書かれていません。減らせないと決めつけてはいません。
- **？ 未確認**（本文では「未確認」と書きます）: 関係する記載はありますが、効果が分からない、またはこちらで確かめられていません。

### 表の注

- **テクスチャメモリー**: TTT は、アトラス化・解像度・圧縮で減らせる場合があります。ただし、アトラスの出力をそのまま使うと、増えることがあります。lilAvatarUtils は、テクスチャのタブで解像度や最大解像度を確認できます。最大解像度は、下げるとインポート時に縮小されると、公式に説明されています。AAO は、対応しているシェーダーのマテリアルについて、テクスチャのサイズを自動で小さくします。
- **三角ポリゴンの数**: MA の Mesh Cutter は、部位を削除します。部位が見えなくなるので、見た目が変わります。MMS は、目標の数を指定します。AAO は、隠れた部分を、マスク・箱・UV タイル・BlendShape で削除します。見える部分を消すと、見た目が変わります。lilAvatarUtils は、ポリゴン数を表示します。減らす操作は、公式に記載なしです。
- **範囲（BOUNDS）**: MA の Mesh Settings では、Bounds は「Root Bone」として指定したボーンを基準に決まります（公式の記載）。ランクへの効果は、未確認です。lilAvatarUtils のレンダラーのタブには Bounds の項目があります。編集の対象かは、未確認です。
- **スキンドメッシュの数・基本メッシュの数**: AAO の Merge Skinned Mesh は、スキンドメッシュを 1 つにまとめます。基本メッシュ（ボーンで動かないメッシュ）は、Merge Skinned Mesh の「メッシュレンダラー」の設定で、手動でまとめられます。自動の対象になるかは、未確認です。
- **マテリアルスロットの数**: TTT の AtlasTexture には、マテリアルを結合する設定（マテリアル設定）があります（公式の記載）。マテリアルスロットの数を減らすかどうかは、公式に記載なしです。
- **PHYSBONE の項目（AAO）**: AAO の Merge PhysBone は、まとめるたびに PhysBone Affected Transforms の数が 1 つ増えます（公式ページの記載）。一方、[Replace End Bone With Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/) は、End Bone を Endpoint Position に置き換えて、PhysBone Affected Transforms の数を減らせます（公式ページの記載）。
- **PHYSBONE コライダーの数（MA）**: MA の [PhysBone Blocker](https://modular-avatar.nadena.dev/ja/docs/reference/physbone-blocker) は、子オブジェクトから、親オブジェクトをルートとする PhysBone の影響を防ぎます。[Global Collider](https://modular-avatar.nadena.dev/ja/docs/reference/global-collider) は、VRChat のグローバルコライダーの数に上限があるため、既存のグローバルコライダー（指や手など）を差し替える機能です。どちらも、コライダーの数を減らす機能は、公式に記載なしです。
- **コライダーの数（lilAvatarUtils）**: PBコライダーのタブで一覧を確認できるのは、PhysBone 用のコライダーです。通常の Collider を含むかは、未確認です。
- **コンストレイントの数**: MA の [Convert Constraints](https://modular-avatar.nadena.dev/ja/docs/reference/convert-constraints) は、事前に変換すると性能が大きく上がると、公式が書いています。数値は、公式に記載なしです。
- **アニメーターの数**: MA の Merge Animator は、「付属アニメーターを削除」をオンにしたときに減らせます。AAO の「アニメーターの最適化」は、レイヤーを整理するもので、Animator の数は公式に記載なしです。
- **ボーンの数**: MA の Merge Armature は、名前が一致するボーンを統合先にまとめます。名前が合わないと、新しいボーンが増えます。AAO は、使われていない Object を削除します。
- **ライトの数（lilAvatarUtils）**: ライティングのタブは、見え方を確かめる機能です。ライトを減らす機能は、公式に記載なしです。
- **レイキャストの数（MA）**: Parameters の VRCRaycast 対応は、パラメーターの型としての対応です。数を減らす機能は、公式に記載なしです。

## ツールだけでは減らせない項目の考え方

- 表で「？」になっている項目は、公式に記載がないか、こちらで確かめられなかった項目です。
- ツールで減らせない項目は、Unity の標準の操作で減らせることがあります。たとえば、使わないコンポーネントを取り除く、使わないオブジェクトを消す、といった方法です。
- まず、今どの項目が重いのかを見ることが大事です。Builder でランクを確かめ、いちばん悪い項目から減らします（[パフォーマンスランク](/learn/vrchat/performance-rank)）。
- AAO の公式ドキュメントは、Control Panel の Performance Rank は非破壊ツールでは信頼できないとして、Play モードで確かめる方法を案内しています。AAO を使ったあとのランクは、この方法で確かめてください。
- 減らす手順の例は、[アバターを軽くする](/learn/vrchat/optimization) にまとめています。
- ツールを入れたあとは、Build & Test で見た目と動きが変わっていないかを確かめてください。
- 複数のツールを併用するとき、順序や衝突について公式に記載のない組み合わせがあります（MA と AAO など）。想定どおりに動くか、実際に確かめてください。
