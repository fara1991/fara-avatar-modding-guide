---
title: 軽量化に使う 5 つのツールと早見表
reading: けいりょうかにつかういつつのつーるとはやみひょう
description: VRChat のアバターを軽くする 5 つのツール（MA・TTT・lilAvatarUtils・MMS・AAO）の役割と、11 項目のどれを減らせるかを一覧にします。
difficulty: 初級
updated: 2026-11-07
---

::: point
- 5 つのツールは、ツールごとに減らせる項目が違う（MA・TTT・lilAvatarUtils・MMS・AAO）
- ○は、そのツールに、その項目を減らす操作があることを表す
- ツールを入れる前に、対応する Unity の版と VRChat SDK の版を、各ツールのページで確かめる
:::

VRChat のアバターを軽くするツールは何種類もあり、ツールごとに減らせる項目が違います。この記事では、よく使われる 5 つのツールについて、何をするか、どの項目を減らせるかを整理します。

略称は、MA（Modular Avatar）、TTT（TexTransTool）、MMS（Meshia Mesh Simplification）、AAO（Avatar Optimizer）です。lilAvatarUtils は、そのまま書きます。

ツールを入れるときに出てくる VCC（VRChat Creator Companion）と ALCOM は、どちらもパッケージを管理するツールです。VPM（VRChat Package Manager。パッケージを配布・導入する仕組み）のパッケージの配布先を、この記事では「VPM リポジトリ」と書きます。ALCOM は、vrc-get（VRChat Package Manager のオープンソースのコマンドライン版）の GUI 版です（[vrc-get/vrc-get](https://github.com/vrc-get/vrc-get) の README の説明）。VCC について詳しくは、[用語集: VCC](/glossary#vcc) を見てください。

表の項目名は、画面に表示される名前に合わせています。項目の上限の値は、[パフォーマンスランク（Performance Rank）](/learn/vrchat/performance-rank) にあります。PhysBone は、ボーンを物理やほかのプレイヤーの影響で動かす仕組みです（髪や服の揺れなどに使われます。[用語集: PhysBone](/glossary#physbone)）。コンストレイントは、ある物の位置・回転・スケールを、別の物に紐づける仕組みです（[Unity マニュアル: コンストレイント](https://docs.unity3d.com/ja/2022.3/Manual/Constraints.html)）。

::: warning
ツールは更新されることがあります。版が変わっているときは、各ツールのページの最新の情報を優先してください。
:::

## 1. Modular Avatar（MA）

Modular Avatar は、衣装などを本体に組み立てるためのツール群です。衣装のボーンを本体に統合する Merge Armature や、アニメーターを統合する Merge Animator などがあります。アップロード時に、アセットを自動で導入・統合します。

- **版**: 1.18.7（2026 年 9 月 1 日。[changelog](https://modular-avatar.nadena.dev/ja/docs/changelog)）。テスト版は 1.19.0-beta.0 です。
- **入れ方**:
  - ALCOM: 「パッケージ管理」から「VPMリポジトリを追加」を選び、リポジトリ `https://vpm.nadena.dev/vpm.json` を追加します。プロジェクトの「管理」で Modular Avatar の「+」を押して、「適用」します。
  - VCC: ページのリンクからリポジトリを追加し、manage project で「+」を押します。
  - テスト版は、ALCOM から入れることをおすすめします。VCC では、MA のリポジトリを追加するときに、VCC のバグの影響でエラーが出ることがあります。
- **ライセンス**: MIT License（リポジトリの `COPYING.md`）。ただし `Editor/Images` の画像は MIT の対象外で、公式パッケージの一部としてのみ再配布できます。
- **対応**: Unity 2022.3（[VPM メタデータ](https://vpm.nadena.dev/vpm.json) の 1.18.7 の unity の値）。1.18.4（2026 年 8 月 29 日）で、Unity 6.1〜6.7 に対する実験的な対応が入りました。NDMF（ビルド時の処理を動かす仕組み）が必要です。
- **公式ページ**: https://modular-avatar.nadena.dev/ja （GitHub: https://github.com/bdunderscore/modular-avatar ）
- **減らせる主な項目**: ボーン（Merge Armature。名前が一致するボーンのみ）、三角ポリゴン（Mesh Cutter・Shape Changer。条件つき）。アニメーター（Merge Animator）は、「付属アニメーターを削除」で同じオブジェクトの Animator が排除されます。
- **注意**:
  - Mesh Cutter は、メッシュの一部を頂点フィルターで指定して、削除または非表示にできるコンポーネントです（[changelog](https://modular-avatar.nadena.dev/ja/docs/changelog) の 1.14.0 で実装。[Mesh Cutter](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/mesh-cutter)）。Mesh Cutter が常に有効（アクティブ）な場合は選んだポリゴンを完全に削除し、時々非アクティブになる場合は選んだポリゴンを非表示にします。どちらの場合も部位が見えなくなるので、見た目が変わります。時々非アクティブになる場合は、場合によっては Constraint を生成する必要があり、パフォーマンスランクの増加に寄与する可能性があるため、ご注意ください。
  - Merge Animator の「付属アニメーターを削除」をオンにすると、同じオブジェクトの Animator は削除されます。
  - 同じレイヤー種別（FX など）に「既存アニメーターを置き換える」設定の Merge Animator が 2 つ以上あると、エラーになります。
  - Merge Motion（Blend Tree 版）は、時間で変わるアニメーションには向きません。
  - 衣装を組み込む手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。

## 2. TexTransTool（TTT）

TexTransTool は、テクスチャを元のまま残して加工する、非破壊テクスチャ改変ツールです。デカール（テクスチャに画像を貼る機能）や PSD の取り込みなどがあります。アトラス化は、必要な部分だけを集めた 1 枚のテクスチャを作る機能です。VRAM（グラフィックボードのメモリー）を減らす目的で使います。

- **版**: v1.0.2（2026 年 6 月 26 日。GitHub のリリース）。
- **入れ方**: VCC か ALCOM から、パッケージ `net.rs64.tex-trans-tool` を入れます。VPM リポジトリは `https://vpm.rs64.net/vpm.json` です。サイトの「Download-VPM」ボタンからも追加できます。
- **ライセンス**: MIT License（Copyright (c) 2023 Reina_Sakiria）。ただし、`Image` 以下のファイルは MIT の対象外で、公式パッケージの一部としてのみ再配布が許可されています。
- **対応**: Unity 2022.3（VPM の情報）。アバターの改変に使うときは、NDMF が必要です。NDMF と同時に入れることを強くおすすめします。
- **公式ページ**: https://ttt.rs64.net （チュートリアル: https://ttt.rs64.net/docs/Tutorial ）
- **減らせる主な項目**: テクスチャメモリー（条件つき）
- **注意**:
  - テクスチャの圧縮は、NDMF のプレビューには反映されません。圧縮の結果を見るには、NDMF の Apply On Play（Play モードに入ったときに処理を行う）や Manual Bake（処理を適用した複製のアバターを手動で作る。NDM Framework の「Manual bake avatar」が、これにあたります）などを使います（[TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - アトラス化の出力をそのまま使うと、VRAM が増える場合があります（[TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - TextureConfigurator は実験的なコンポーネントです。予告なく機能が変わったり、削除されたりすることがあります（[TextureConfigurator](https://ttt.rs64.net/docs/Reference/TextureConfigurator)）。

## 3. lilAvatarUtils

lilAvatarUtils は、アバターの改変を助けるツールです。

一部のプロパティは編集でき、変更は「適用」ボタンでまとめて反映されます。ツールのタブでは不要なものを整理できます。

- **版**: 2.1.2（2025 年 8 月 11 日。[changelog](https://github.com/lilxyzw/lilAvatarUtils/blob/master/CHANGELOG.md) の記載）。
- **入れ方**: VCC は、「Add to VCC」ボタンで VPM リポジトリ `https://lilxyzw.github.io/vpm-repos/vpm.json` を追加します。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。
- **公式ページ**: https://lilxyzw.github.io/lilAvatarUtils/ja_JP/
- **減らせる主な項目**: テクスチャメモリー（△。Unity のインポート設定を変える作業の補助）。
- **注意**:
  - ツールのタブには、不要なものを削除する操作があります。マテリアルのクリーンアップ（割り当て中のシェーダーで使用されない不要なプロパティの削除）、AnimatorController のクリーンアップ（どこからも参照されない不要なサブアセットの削除）、Missing なコンポーネントの削除です（[ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。これらはアセットを変更する操作なので、作業の前にプロジェクトのバックアップを取るのが安全です。
  - 不要なテクスチャの割り当てが残っていると、使われないテクスチャがアバターに含まれて容量が大きくなることがあります。マテリアルのクリーンアップは、その整理にも使えます（[ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。
  - 各タブの「適用」ボタンで、変更したプロパティがまとめて反映されます。テクスチャのタブの「置き換え」は、アバターに存在する全マテリアルの該当テクスチャを一括で置き換えます（[テクスチャ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)、[マテリアル](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/MaterialsGUI.html)）。
  - 半透明マテリアルのレンダーキューを 2450 以下などに下げすぎると、ほかのマテリアルが消えやすくなります（[マテリアル](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/MaterialsGUI.html)）。

## 4. Meshia Mesh Simplification（MMS）

Meshia Mesh Simplification は、メッシュの三角ポリゴンの数を減らす（簡略化する）ツールです。アバター向けの手順では「Meshia Cascading Avatar Mesh Simplifier」を使い、目標の三角ポリゴン数を複数のメッシュに配分できます。まず「目標とするパフォーマンスランク」を選びます。NDMF 統合の項では「MeshiaMeshSimplifier」を使います。パッケージのソースでは、ランク名のついたプリセットから三角ポリゴン数を選ぶ形になっています（下の注を見てください）。

- **版**: 3.3.1（2026 年 11 月 4 日に公開。GitHub のリリース）。
- **入れ方**: VCC に作者の VPM リポジトリ `https://ramtype0.github.io/VpmRepository/index.json` を追加し、Manage Project から Manage Packages を開いて Meshia Mesh Simplification を追加します。案内ページ（HTML）は https://ramtype0.github.io/VpmRepository/ です。
- **ライセンス**: MIT License（`Copyright (c) 2025 Ram.Type-0`）。
- **対応**: Unity 2022.3（パッケージの情報）。VRChat 向けの版は 2022.1.1 です（パッケージの package.json の vrchatVersion）。
- **依存**:
  - パッケージには、NDMF 向けの部分が含まれています（パッケージのソース）。
  - VPM のメタデータ（package.json）では、anatawa12 さんが作ったローカライズ用パッケージ `com.anatawa12.custom-localization-for-editor-extension`（^1.2.1）に依存しています。これは AAO とは別のパッケージで、AAO の機能の説明には関係しません。
  - ほかに必要なツールは、次のとおりです。アバター改変で MMS と Meshia Cascading Avatar Mesh Simplifier の両方を使うときは [Modular Avatar](https://modular-avatar.nadena.dev/ja)、ワールド改変で MMS を使うときは NDMF です（[インストールの説明](https://github.com/RamType0/Meshia.MeshSimplification/blob/main/.docfx/docs/ja/vrchat/installation.md)）。
- **メニュー**: GameObject > Meshia Mesh Simplification > Meshia Cascading Avatar Mesh Simplifier です（アバターの直下にこの GameObject を作成します）。
- **公式ページ**: https://ramtype0.github.io/Meshia.MeshSimplification/ （README: https://github.com/RamType0/Meshia.MeshSimplification ）
- **減らせる主な項目**: 三角ポリゴンの数（目標の数を指定）
- **注意**:
  - Meshia Cascading Avatar Mesh Simplifier の目標の三角ポリゴン数の既定値は 70,000 です（パッケージのソース）。
  - プリセットは、パッケージのソースに次の値で入っています。
    - PC-Poor-Medium-Good: 70,000
    - PC-Excellent: 32,000
    - Mobile-Poor: 20,000
    - Mobile-Medium: 15,000
    - Mobile-Good: 10,000
    - Mobile-Excellent: 7,500
  - 軽量化後のメッシュに穴が目立つときは、「メッシュの端の辺を保持（Preserve Border Edges）」を有効にしてみてください（指先に穴が空いたりする場合も同じです。[Meshia Cascading Avatar Mesh Simplifier の使い方](https://github.com/RamType0/Meshia.MeshSimplification/blob/main/.docfx/docs/ja/vrchat/meshia-cascading-avatar-mesh-simplifier.md)）。この設定は、他の面に接していない辺（穴や切れ目の縁など）を削除しないようにします。
  - 関連するリリースは、次のとおりです（GitHub のリリース）。3.1.0（2025 年 12 月 28 日）で、辺の保持をボーンごとに設定できるようになりました。3.2.0（2026 年 6 月 16 日）で、手のボーンを既定で保持するようになりました。
  - 作業の前にバックアップを取るのが安全です。

## 5. Avatar Optimizer（AAO）

Avatar Optimizer は、アバターを自動で軽くする非破壊改変ツールです。Play モードに入るときか、アバターをビルドするときに処理が行われます。

Trace And Optimize を使って振る舞いや見た目が変わったら、他のコンポーネントのバグ挙動に依存していない限り、バグとして報告してください（[よくある質問（FAQ）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。

- **版**: 1.9.21（2026 年 10 月 30 日。[changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/) の日付）。
- **入れ方**: ALCOM から入れることをおすすめします。VCC では正しく動かないことがあるためです。ALCOM で VPM リポジトリ `https://vpm.anatawa12.com/vpm.json` を追加してから、`com.anatawa12.avatar-optimizer` を追加します。UnityPackage や vrc-get からの入れ方もあります。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK（Avatars）は、3.7.0 以上 3.11.0 未満です（VPM の情報）。NDMF が必要です。
- **公式ページ**: https://vpm.anatawa12.com/avatar-optimizer/ja/ （GitHub: https://github.com/anatawa12/AvatarOptimizer ）
- **減らせる主な項目**: テクスチャメモリー、三角ポリゴンの数（隠れた部分）、スキンドメッシュの数、マテリアルスロットの数、PHYSBONE のコンポーネント・トランスフォーム・コライダーの数、ボーンの数（自動と手動の両方で減らせます）
- **注意**:
  - Trace And Optimize は、アバターのルートに付けます（[基本的な使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。
  - Merge Skinned Mesh で統合すると、メッシュを個別にアニメーションでオン・オフできなくなります。マテリアルのアニメーションについて、次の点に注意してください。
    - [Merge Skinned Mesh](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-skinned-mesh/) のリファレンスの備考は『マテリアルに関するアニメーションは統合前のものがそのまま機能します』と説明していますが、[FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/) は『現時点では競合してしまう仕様です』と説明していて、食い違っています。統合したあとで、実際に動くかを確かめてください。
    - FAQ の「マテリアルプロパティに対するアニメーションが競合する」: この問題は既知のバグです。issue は [issue 340](https://github.com/anatawa12/AvatarOptimizer/issues/340) です。競合するときは警告が表示されます。詳しくは [Avatar Optimizer の記事](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」を見てください。
    - 同じ FAQ の「マテリアルスロットに対するアニメーションが競合する」: アニメーションでそれぞれのマテリアルを別のものに置き換える場合は、Merge Skinned Mesh の「マテリアルの統合」の「統合する」をオフにします。
  - 非破壊のツールを使うと、VRChat SDK の Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードに入ったときの Actual Performance Window で確かめます（anatawa12 さんの Gist Pack の一つ。[基本的な使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/) の「UploadせずにPerformance Rankを見る方法」）。
  - 「使われていないObjectを自動的に削除する」は、アニメーションなどを走査して、使われていない GameObject・コンポーネント・テクスチャなどを自動で削除する機能です（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。版によって削除の対象が増えています（下の表の注を見てください）。

## 項目 × ツールの早見表

各マスは、各ツールの資料の記述をもとに判定しています。

### 記号の意味

- **○**{.lbl .lbl-ok} **減らせる**: そのツールに、その項目を減らす操作があります。
- **△**{.lbl .lbl-unk} **条件つき**: 条件を満たすと減ります。または、減らす作業の判断に使えます。増える場合もあります。下の表の注を見てください。
- **×**{.lbl .lbl-ng} **減らせない**: 資料に明記されたものだけに使います。この表の該当は、TTT のマテリアルスロットの数だけです。対象は AtlasTexture のマテリアルの結合で、この結合ではマテリアルスロットは結合されません（[アトラス化のチュートリアル](https://ttt.rs64.net/docs/Tutorial/ReductionTextureMemoryByAtlasing)）。この × は、その 1 件だけの判定です。
- **―**: この表では、そのツールの欄に、その項目を減らす操作を挙げていません。

### メッシュ・テクスチャ・マテリアル

表の「テクスチャメモリー」は、テクスチャが使うメモリーの量を表す項目です（テクスチャは、システム RAM と GPU のメモリーの両方を使います。VRAM は GPU 側のメモリーです。出典: [Avatar Performance Ranking Stats](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| テクスチャメモリー | ― | **△**{.lbl .lbl-unk} | **△**{.lbl .lbl-unk} | ― | **○**{.lbl .lbl-ok} |
| 三角ポリゴンの数 | **△**{.lbl .lbl-unk} | ― | ― | **○**{.lbl .lbl-ok} | **○**{.lbl .lbl-ok} |
| スキンドメッシュの数 | ― | ― | ― | ― | **○**{.lbl .lbl-ok} |
| 基本メッシュの数 | ― | ― | ― | ― | **○**{.lbl .lbl-ok} |
| マテリアルスロットの数 | ― | **×**{.lbl .lbl-ng} | ― | ― | **○**{.lbl .lbl-ok} |

### PhysBone・コンタクト・アニメーター・ボーン

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| PHYSBONE コンポーネントの数 | ― | ― | ― | ― | **○**{.lbl .lbl-ok} |
| PHYSBONE トランスフォームの数 | ― | ― | ― | ― | **○**{.lbl .lbl-ok} |
| PHYSBONE コライダーの数 | ― | ― | ― | ― | **○**{.lbl .lbl-ok} |
| コンタクトの数 | ― | ― | ― | ― | **△**{.lbl .lbl-unk} |
| アニメーターの数 | **○**{.lbl .lbl-ok} | ― | ― | ― | **△**{.lbl .lbl-unk} |
| ボーンの数 | **△**{.lbl .lbl-unk} | ― | ― | ― | **○**{.lbl .lbl-ok} |

### 表の注

- **テクスチャメモリー**: TTT は、アトラス化・解像度・圧縮で減らせる場合があります。ただし、アトラス化の出力をそのまま使うと、増えることがあります。lilAvatarUtils は、テクスチャの一括置き換えや、不要なテクスチャや過剰な解像度のテクスチャを特定する用途に使えます。最大解像度（縦または横の最大サイズ）は、インポート時に縮小される値です（[テクスチャ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。元の画像ファイルの解像度は変わりません。AAO は、対応しているシェーダーのマテリアルについて、テクスチャのサイズを自動で小さくします。
- **三角ポリゴンの数**:
  - MA の Mesh Cutter は、「1. Modular Avatar」の注を見てください。
  - MA の Shape Changer は、「Delete」モードのとき、アニメーションされていないならポリゴンを削除して数を減らせます。アニメーションされているときは非表示になりますが、測定されるポリゴン数は減りません（[Shape Changer](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/shape-changer)）。
  - MMS は、目標の数を指定します。
  - AAO は、隠れた部分を、マスク・箱・UV タイル・BlendShape で削除します。見える部分を消すと、見た目が変わります。
- **スキンドメッシュの数・基本メッシュの数**: AAO の Merge Skinned Mesh は、スキンドメッシュを 1 つにまとめます。基本メッシュ（ボーンで動かないメッシュ）は、Merge Skinned Mesh の「メッシュレンダラー」の設定で、手動でまとめられます。
- **マテリアルスロットの数**: TTT の表の × は、AtlasTexture のマテリアルの結合では、マテリアルスロットは結合されないことを指します。AtlasTexture は、マテリアルをまとめる機能ですが、マテリアルスロットの結合はできません（[詳しくは「マテリアルスロットを減らす方法」](/learn/optimize-mesh/material-slots)）。AAO の Merge Skinned Mesh は、統合対象のメッシュで同じマテリアルを使っているマテリアルスロットを、既定で統合します（[FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- **PHYSBONE の項目（AAO）**: AAO の Merge PhysBone は、まとめるたびに PhysBone Affected Transforms の数が 1 つ増えます。一方、[Replace End Bone With Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/) は、end ボーンを Endpoint Position に置き換えて、PhysBone Affected Transforms の数を減らせます。
- **コンタクトの数（AAO）**: AAO の「使われていないObjectを自動的に削除する」（英語名は Remove Unused Objects）は、1.7.0（2024 年 4 月 30 日）から、Animator Controller で使われていないパラメーターを持つ PhysBone と Contact Receiver を削除するようになりました（[changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。条件つきで、自動で行われます。
- **アニメーターの数**: MA の Merge Animator は、「付属アニメーターを削除」をオンにすると、同じオブジェクト内の Animator が自動的に排除されます（詳しくは「アニメーターの数を減らす」）。AAO の「使われていないObjectを自動的に削除する」は、1.6.0（2023 年 11 月 27 日）から、Animator Controller のない Animator を、意味のないものとして削除するようになりました（changelog）。
- **ボーンの数**: MA の Merge Armature は、名前が一致するボーンを統合先にまとめます。名前が合わないと、新しいボーンが増えます。AAO は、使われていないObjectを削除します。

## ツールだけでは減らせない項目の考え方

- ツールで減らせない項目は、Unity の標準の操作で減らせることがあります。たとえば、使わないコンポーネントを取り除く、使わないオブジェクトを消す、といった方法です。
- まず、今どの項目が重いのかを見ることが大事です。VRChat SDK の Control Panel でランクを確かめ、いちばん悪い項目から減らします（[パフォーマンスランク](/learn/vrchat/performance-rank)）。
- 非破壊のツールを使うと、Control Panel のパフォーマンスランクはあてにならなくなります。実際の値は、Play モードの Actual Performance Window で確かめます。AAO を使ったあとのランクは、この方法で確かめてください。
- 減らす手順の例は、[アバターを軽くする](/learn/vrchat/optimization) にまとめています。
- ツールを入れたあとは、Build & Test で見た目と動きが変わっていないかを確かめてください。
- 複数のツールを併用するときは、想定どおりに動くか、実際に確かめてください（MA と AAO など）。
