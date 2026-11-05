---
title: 軽量化に使う 5 つのツールと早見表
reading: けいりょうかにつかういつつのつーるとはやみひょう
description: VRChat のアバターを軽くする 5 つのツール（MA・TTT・lilAvatarUtils・MMS・AAO）の役割と、28 項目のどれを減らせるかを一覧にします。
difficulty: 初級
updated: 2026-11-06
---

VRChat のアバターを軽くするツールは何種類もあり、ツールごとに減らせる項目が違います。この記事では、よく使われる 5 つのツールについて、何をするか、どの項目を減らせるかを整理します。

略称は、MA（Modular Avatar）、TTT（TexTransTool）、MMS（Meshia Mesh Simplification）、AAO（Avatar Optimizer）です。lilAvatarUtils は、そのまま書きます。

ツールを入れるときに出てくる VCC（VRChat Creator Companion）と ALCOM は、どちらもパッケージを管理するツールです。VPM（VRChat Package Manager。パッケージを配布・導入する仕組み）のパッケージの配布先を、この記事では「VPM リポジトリ」と書いています。ALCOM は、vrc-get（VRChat Package Manager のオープンソースのコマンドライン版）の GUI 版です（[vrc-get/vrc-get](https://github.com/vrc-get/vrc-get) の README の説明）。VCC について詳しくは、[用語集: VCC](/glossary#vcc) を見てください。

表の項目名は、画面に表示される名前に合わせています。項目の上限の値は、[パフォーマンスランク（Performance Rank）](/learn/vrchat/performance-rank) にあります。PhysBone は、ボーンを物理やほかのプレイヤーの影響で動かす仕組みです（髪や服の揺れなどに使われます。[用語集: PhysBone](/glossary#physbone)）。コンストレイントは、ある物の位置・回転・スケールを、別の物に紐づける仕組みです（[Unity マニュアル: コンストレイント](https://docs.unity3d.com/ja/2022.3/Manual/Constraints.html)）。

各ツールのコンポーネントを追加するメニュー名は、公式のドキュメントか、パッケージのソースで確認できたものだけを、出典つきで書いています。それ以外は、コンポーネント名だけを書いています。

この記事で「未確認」は、この記事では確認していない、という意味です。無い・できないという意味ではありません。記号の意味は、「項目 × ツールの早見表」の前の凡例にあります。

::: warning
ツールは更新されることがあります。この記事は **2026 年 11 月 6 日** に公式ページを確かめて書きました。版が変わっているときは、公式ページの最新の情報を優先してください。
:::

## 1. Modular Avatar（MA）

Modular Avatar は、衣装などを本体に組み立てるためのツール群です。衣装のボーンを本体に統合する Merge Armature や、アニメーターを統合する Merge Animator などがあります。公式トップページは、アップロード時にアセットを自動で導入・統合すると説明しています。

- **確認した版**: 1.18.7（2026 年 9 月 1 日。公式の [changelog](https://modular-avatar.nadena.dev/ja/docs/changelog)）。テスト版は 1.19.0-beta.0 です。
- **入れ方**:
  - ALCOM: 「パッケージ管理」から「VPMリポジトリを追加」を選び、リポジトリ `https://vpm.nadena.dev/vpm.json` を追加します。プロジェクトの「管理」で Modular Avatar の「+」を押して、「適用」します。
  - VCC: 公式ページのリンクからリポジトリを追加し、manage project で「+」を押します。
  - テスト版は、公式が ALCOM から入れるよう案内しています。VCC では依存関係の解決にバグがあると書かれています。
- **ライセンス**: MIT License（リポジトリの `COPYING.md`）。ただし `Editor/Images` の画像は MIT の対象外で、公式パッケージの一部としてのみ再配布できます。
- **対応**: Unity 2022.3（[VPM メタデータ](https://vpm.nadena.dev/vpm.json) の 1.18.7 の unity の値）。changelog の 1.18.4（2026 年 8 月 29 日）に「Unity 6.1〜6.7 に対する実験的な対応」とあります。VRChat SDK の対応版は、未確認です。NDMF（ビルド時の処理を動かす仕組み）が必要です。
- **公式ページ**: https://modular-avatar.nadena.dev/ja （GitHub: https://github.com/bdunderscore/modular-avatar ）
- **減らせる主な項目**: ボーン（Merge Armature）、アニメーター（Merge Animator）、三角ポリゴン（Mesh Cutter・Shape Changer。条件つき）
- **注意**:
  - Mesh Cutter は、メッシュの一部を頂点フィルターで指定して、削除または非表示にできるコンポーネントです（[changelog](https://modular-avatar.nadena.dev/ja/docs/changelog) の 1.14.0 で実装。[公式: Mesh Cutter](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/mesh-cutter)）。Mesh Cutter が常にアクティブ（有効）なときは選んだポリゴンが完全に削除され、時々非アクティブ（無効）になるときは非表示になります。どちらの場合も部位が見えなくなるので、見た目が変わります。
  - Merge Animator の「付属アニメーターを削除」をオンにすると、同じオブジェクトの Animator は削除されます（公式は「排除」と書いています）。
  - 同じレイヤー種別（FX など）に「既存アニメーターを置き換える」設定の Merge Animator が 2 つ以上あると、エラーになります。
  - Merge Motion（Blend Tree 版）は、時間で変わるアニメーションには向きません。
  - 衣装を組み込む手順は、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) を見てください。

## 2. TexTransTool（TTT）

TexTransTool は、テクスチャを元のまま残して加工するツールです。公式は「非破壊テクスチャ改変ツール」と説明しています。デカール（テクスチャに画像を貼る機能）や PSD の取り込みなどがあります。アトラス化は、必要な部分だけを集めた 1 枚のテクスチャを作る機能です。VRAM（グラフィックボードのメモリー）を減らす目的で使います。

- **確認した版**: v1.0.2（2026 年 6 月 26 日。GitHub のリリース）。
- **入れ方**: VCC か ALCOM から、パッケージ `net.rs64.tex-trans-tool` を入れます。VPM リポジトリは `https://vpm.rs64.net/vpm.json` です。公式サイトの「Download-VPM」ボタンからも追加できます。
- **ライセンス**: MIT License（Copyright (c) 2023 Reina_Sakiria）。ただし、`Image` 以下のファイルは MIT の対象外で、公式パッケージの一部としてのみ再配布が許可されています。v1.0.2 の zip に入っている LICENSE.md で確認しました。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK の対応版は、未確認です。アバターの改変に使うときは、NDMF が必要です。公式は、NDMF と同時に入れることを強く推奨しています。
- **公式ページ**: https://ttt.rs64.net （チュートリアル: https://ttt.rs64.net/docs/Tutorial ）
- **減らせる主な項目**: テクスチャメモリー（条件つき）、マテリアルスロットの数（条件つき）
- **注意**:
  - テクスチャの圧縮は、プレビューでは行われません。圧縮の結果を見るには、NDMF の Apply On Play（Play モードに入ったときに処理を行う）や Manual Bake（処理を適用した複製のアバターを手動で作る。AAO の公式では、NDM Framework の「Manual bake avatar」と説明）などを使います（[公式: TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - アトラス化の出力をそのまま使うと、VRAM が増える場合があると書かれています（[公式: TextureFineTuning](https://ttt.rs64.net/docs/Reference/AtlasTexture/TextureFineTuning)）。
  - TextureConfigurator は実験的なコンポーネントです。予告なく機能が変わったり、削除されたりすることがあります（[公式: TextureConfigurator](https://ttt.rs64.net/docs/Reference/TextureConfigurator)）。
  - 元に戻す（Revert）方法は、未確認です。

## 3. lilAvatarUtils

lilAvatarUtils は、アバターの改変を助けるツールです。公式サイトは「アバター改変補助ツール」と紹介しています。

テクスチャ、マテリアル、レンダラー、PhysBone、PBコライダー、アニメーションの一覧を、タブで確認できます。一部のプロパティは編集でき、変更は「適用」ボタンでまとめて反映されます。ライティングのタブでは見え方を確かめ、ツールのタブでは不要なものを整理できます。

- **確認した版**: 2.1.2（2025 年 8 月 11 日。公式の [changelog](https://github.com/lilxyzw/lilAvatarUtils/blob/master/CHANGELOG.md) の記載）。
- **入れ方**: VCC は、公式サイトの「Add to VCC」ボタンで VPM リポジトリ `https://lilxyzw.github.io/vpm-repos/vpm.json` を追加します。ALCOM での入れ方は、未確認です。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK の対応版は、未確認です。
- **公式ページ**: https://lilxyzw.github.io/lilAvatarUtils/ja_JP/
- **減らせる主な項目**: テクスチャメモリー（△。Unity のインポート設定を変える作業の補助）。PhysBone と PBコライダーは、一覧で確認して、減らす候補を見つけるためのタブです。減らす操作の詳細は、この記事では未確認です。
- **注意**:
  - ツールのタブには、不要なものを削除する操作があります。マテリアルのクリーンアップ（使われていない不要なプロパティの削除）、AnimatorController のクリーンアップ（どこからも参照されない不要なサブアセットの削除）、Missing なコンポーネントの削除です（[公式: ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。これらはアセットを変更する操作なので、作業の前にプロジェクトのバックアップを取るのが安全です。
  - 不要なテクスチャの割り当てが残っていると、使われないテクスチャがアバターに含まれて容量が大きくなることがあります。マテリアルのクリーンアップは、その整理にも使えます（[公式: ツール](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/UtilsGUI.html)）。
  - 各タブの「適用」ボタンで、変更したプロパティがまとめて反映されます。テクスチャのタブの「置き換え」は、アバターに存在する全マテリアルの該当テクスチャを一括で置き換えます（[公式: テクスチャ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)、[公式: マテリアル](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/MaterialsGUI.html)）。
  - 半透明マテリアルのレンダーキューを 2450 以下などに下げすぎると、ほかのマテリアルが消えやすくなると書かれています（公式のマテリアルのタブの説明）。
  - 範囲（BOUNDS）に収まっていないメッシュは、画面内にあるはずでも消えたり、ライトが暗くなったりすることがあります（公式のレンダラーのタブの説明）。

## 4. Meshia Mesh Simplification（MMS）

Meshia Mesh Simplification は、メッシュの三角ポリゴンの数を減らす（簡略化する）ツールです。アバター向けの手順では「Meshia Cascading Avatar Mesh Simplifier」を使い、目標の三角ポリゴン数を複数のメッシュに配分できます。公式ドキュメントでは、まず「目標とするパフォーマンスランク」を選びます。NDMF 統合の項では「MeshiaMeshSimplifier」を使います。パッケージのソースでは、ランク名のついたプリセットから三角ポリゴン数を選ぶ形になっています（下の注を見てください）。

- **確認した版**: 3.3.1（2026 年 11 月 4 日に公開。GitHub のリリース）。
- **入れ方**: VCC に作者の VPM リポジトリ `https://ramtype0.github.io/VpmRepository/index.json` を追加し、Manage Project から Manage Packages を開いて Meshia Mesh Simplification を追加します。案内ページ（HTML）は https://ramtype0.github.io/VpmRepository/ です。VCC・ALCOM での実機確認は、まだしていません。ALCOM での入れ方は、未確認です。
- **ライセンス**: MIT License（`Copyright (c) 2025 Ram.Type-0`）。
- **対応**: Unity 2022.3（パッケージの情報）。VRChat 向けの版として 2022.1.1 と書かれていますが、その意味は未確認です。
- **依存**:
  - パッケージには、NDMF 向けの部分が含まれています（パッケージのソース）。NDMF が無いときの動作は、未確認です。
  - VPM のメタデータ（package.json）では、anatawa12 さんが作ったローカライズ用パッケージ `com.anatawa12.custom-localization-for-editor-extension`（^1.2.1）に依存しています。これは AAO とは別のパッケージで、AAO の機能の説明には関係しません。
  - ほかに必要なツールは、公式のインストールの説明で案内されています。アバター改変で両方（MMS と Cascading）を使うときは [Modular Avatar](https://modular-avatar.nadena.dev/ja)、ワールド改変で MMS を使うときは NDMF です（[インストールの説明](https://github.com/RamType0/Meshia.MeshSimplification/blob/main/.docfx/docs/ja/vrchat/installation.md)）。
- **メニュー**: GameObject > Meshia Mesh Simplification > Meshia Cascading Avatar Mesh Simplifier です（パッケージのソースの MenuItem で確認。公式ドキュメントは、アバターの直下にこの GameObject を作成する手順を案内しています）。
- **公式ページ**: https://ramtype0.github.io/Meshia.MeshSimplification/ （README: https://github.com/RamType0/Meshia.MeshSimplification ）
- **減らせる主な項目**: 三角ポリゴンの数（目標の数を指定）
- **注意**:
  - 目標の三角ポリゴン数の既定値は 70,000 です（パッケージのソース）。
  - プリセットは、パッケージのソースに次の値で入っています（公式ドキュメントの本文ではありません）。
    - PC-Poor-Medium-Good: 70,000
    - PC-Excellent: 32,000
    - Mobile-Poor: 20,000
    - Mobile-Medium: 15,000
    - Mobile-Good: 10,000
    - Mobile-Excellent: 7,500
  - 軽量化後のメッシュに穴が目立つときは、「メッシュの端の辺を保持（Preserve Border Edges）」を有効にしてみてください。公式ドキュメントにも同趣旨の説明があります（「指先に穴が空いたりする場合、有効化してみてください」と書かれています。[Meshia Cascading Avatar Mesh Simplifier の使い方](https://github.com/RamType0/Meshia.MeshSimplification/blob/main/.docfx/docs/ja/vrchat/meshia-cascading-avatar-mesh-simplifier.md)）。ツールチップ（パッケージのソース）には、「軽量化後のメッシュに穴が目立つ場合、このオプションを有効にしてみてください」とあります。この設定は、他の面に接していない辺（穴や切れ目の縁など）を削除しないようにします。
  - 関連するリリースは、次のとおりです（GitHub のリリース）。3.1.0（2025 年 12 月 28 日）で、辺の保持をボーンごとに設定できるようになりました。3.2.0（2026 年 6 月 16 日）で、手のボーンを既定で保持するようになりました（英語のリリースノート: Preserve hand bones by default）。
  - 非破壊かどうかは、未確認です。作業の前にバックアップを取るのが安全です。
  - Avatar Optimizer との連携は、未確認です。

## 5. Avatar Optimizer（AAO）

Avatar Optimizer は、アバターを自動で軽くする非破壊改変ツールです。公式は、Play モードに入るときか、アバターをビルドするときに処理が行われると説明しています。

公式の FAQ（よくある質問のページ）は、Trace And Optimize を使って振る舞いや見た目が変わったら、他のコンポーネントのバグ挙動に依存していない限り、バグとして報告してほしい、と書いています。

- **確認した版**: 1.9.21（2026 年 10 月 30 日。公式の [changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/) の日付）。
- **入れ方**: 公式は ALCOM から入れることを推奨しています。VCC では正しく動かないことがあるためです。ALCOM で VPM リポジトリ `https://vpm.anatawa12.com/vpm.json` を追加してから、`com.anatawa12.avatar-optimizer` を追加します。UnityPackage や vrc-get からの入れ方も書かれています。
- **ライセンス**: MIT License。
- **対応**: Unity 2022.3（VPM の情報）。VRChat SDK（Avatars）は、3.7.0 以上 3.11.0 未満と書かれています（VPM の情報）。NDMF が必要です。
- **公式ページ**: https://vpm.anatawa12.com/avatar-optimizer/ja/ （GitHub: https://github.com/anatawa12/AvatarOptimizer ）
- **減らせる主な項目**: テクスチャメモリー、三角ポリゴンの数（隠れた部分）、スキンドメッシュの数、マテリアルスロットの数、PHYSBONE のコンポーネント・トランスフォーム・コライダーの数、ボーンの数（自動と手動の両方で減らせます）
- **注意**:
  - Trace And Optimize は、アバターのルートに付けます（[公式: 基本的な使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。
  - Merge Skinned Mesh で統合すると、メッシュを個別にアニメーションでオン・オフできなくなります。マテリアルのアニメーションについて、公式の資料の書き方は次のように食い違っています。
    - [公式: Merge Skinned Mesh](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-skinned-mesh/) の備考は、「マテリアルに関するアニメーションは統合前のものがそのまま機能します」と書いています。
    - [公式 FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/) には、「マテリアルプロパティに対するアニメーションが競合する」という項目があります。「この問題は既知のバグであり、現時点では競合してしまう仕様です」と書かれていて、issue は [issue 340](https://github.com/anatawa12/AvatarOptimizer/issues/340) です。競合するときは警告が表示されます。詳しくは [Avatar Optimizer の記事](/learn/optimize-aao/avatar-optimizer) の「うまくいかないとき」を見てください。
    - 同じ FAQ の「マテリアルスロットに対するアニメーションが競合する」の項目には、アニメーションでそれぞれのマテリアルを別のものに置き換える場合は、Merge Skinned Mesh の「マテリアルの統合」の「統合する」をオフにする、とあります。
    - 公式の中で書き方が食い違う（備考は「そのまま機能する」、FAQ は競合する場合がある）ので、統合するときは実際に確かめてください。
  - 公式の[基本的な使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)（tutorial/basic-usage の「UploadせずにPerformance Rankを見る方法」）では、VRChat SDK の Control Panel のパフォーマンスランクは、非破壊改変ツールを使うと、あてにならなくなると書かれています。Play モードに入ったときのランクは、anatawa12 さんの Gist Pack（Unity 用の補助ツールの集まり。複数の gist で構成され、その一つが Actual Performance Window）で確認する方法が案内されています。
  - 「使われていないObjectを自動的に削除する」は、アニメーションなどを走査して、使われていない GameObject・コンポーネント・テクスチャなどを自動で削除する機能です（[公式: Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。版によって削除の対象が増えています（下の表の注を見てください）。

## 項目 × ツールの早見表

各マスは、公式の資料に書かれていることをもとに判定しています。

### 記号の意味

- **○ 減らせる**: そのツールに、その項目を減らす操作が書かれています。
- **△ 条件つき**: 条件を満たすと減ると書かれています。または、減らす作業の判断に使えると書かれています。増える場合もあります。下の表の注を見てください。
- **確認のみ**: 数や値を一覧で確認できると書かれています。減らす操作は、この記事では未確認です。
- **× 減らせない**: 公式に明記されたものだけに使います。この表には該当がありません。
- **？ 未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。
- **？ 公式に記載なし**: その項目について、そのツールの公式の資料（日本語版の公式ドキュメントの各ページと changelog）を検索して、本当に無いと確かめたものだけに使います。この表では、AAO の列だけが該当します。AAO の Trace And Optimize の説明（「使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します」）は種類を限らない一般的な記述なので、コンストレイントやライトなどは、件数を減らす機能としての個別の記載が無い、という意味です。

### メッシュ・テクスチャ・マテリアル

表の「テクスチャメモリー」は、テクスチャが使うメモリーの推定値です（[公式の統計表（Avatar Performance Ranking Stats）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) では、システム RAM と GPU のメモリーの両方を使うと説明。VRAM は GPU 側のメモリー）。

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| テクスチャメモリー | ？ 未確認 | △ | △ | ？ 未確認 | ○ |
| 三角ポリゴンの数 | △ | ？ 未確認 | 確認のみ | ○ | ○ |
| 範囲（BOUNDS） | ？ 未確認 | ？ 未確認 | 確認のみ | ？ 未確認 | ？ 未確認 |
| スキンドメッシュの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ○ |
| 基本メッシュの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ○ |
| マテリアルスロットの数 | ？ 未確認 | △ | 確認のみ | ？ 未確認 | ○ |

### PhysBone・コンタクト・コンストレイント・アニメーター・ボーン

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| PHYSBONE コンポーネントの数 | ？ 未確認 | ？ 未確認 | △ | ？ 未確認 | ○ |
| PHYSBONE トランスフォームの数 | ？ 未確認 | ？ 未確認 | 確認のみ | ？ 未確認 | ○ |
| PHYSBONE コライダーの数 | ？ 未確認 | ？ 未確認 | △ | ？ 未確認 | ○ |
| PHYSBONE 衝突チェックの数 | ？ 未確認 | ？ 未確認 | 確認のみ | ？ 未確認 | ？ 未確認 |
| コンタクトの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | △ |
| コンストレイントの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| コンストレイントの深さ | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| アニメーターの数 | △ | ？ 未確認 | ？ 未確認 | ？ 未確認 | △ |
| ボーンの数 | △ | ？ 未確認 | ？ 未確認 | ？ 未確認 | ○ |

### そのほかの項目

| 項目 | MA | TTT | lilAvatarUtils | MMS | AAO |
|---|---|---|---|---|---|
| ライトの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| パーティクルシステムの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| 最大パーティクルの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| メッシュパーティクルの三角ポリゴン数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| パーティクルトレイル | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| パーティクルの衝突 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| トレイルレンダラーの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| CLOTH メッシュの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| CLOTH の最大頂点の数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| コライダーの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 |
| リジッドボディの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| オーディオソースの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 公式に記載なし |
| レイキャストの数 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 | ？ 未確認 |

### 表の注

- **テクスチャメモリー**: TTT は、アトラス化・解像度・圧縮で減らせる場合があります。ただし、アトラス化の出力をそのまま使うと、増えることがあります。lilAvatarUtils の公式の説明は、テクスチャの一括置き換えと、テクスチャを VRAM 容量順に並べて Unity 側でインポート設定を変える作業です。減らすのは Unity のインポート設定で、ツール内で減らす操作ではありません（[公式: テクスチャ](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/TexturesGUI.html)）。どの項目をツール内で編集できるかは、未確認です。最大解像度（縦または横の最大サイズ）を下げると、インポート時に縮小されます。元の画像ファイルは変わりません。AAO は、対応しているシェーダーのマテリアルについて、テクスチャのサイズを自動で小さくします。
- **三角ポリゴンの数**:
  - MA の Mesh Cutter は、「1. Modular Avatar」の注を見てください。
  - MA の Shape Changer は、「Delete」モードのとき、アニメーションされていないならポリゴンを削除して数を減らせます。アニメーションされているときは非表示になりますが、測定されるポリゴン数は減りません（[公式: Shape Changer](https://modular-avatar.nadena.dev/ja/docs/reference/reaction/shape-changer)）。
  - MMS は、目標の数を指定します。
  - AAO は、隠れた部分を、マスク・箱・UV タイル・BlendShape で削除します。見える部分を消すと、見た目が変わります。
  - lilAvatarUtils は、ポリゴン数を表示します。減らす操作は、この記事では未確認です。
- **範囲（BOUNDS）**: MA の Mesh Settings では、範囲（BOUNDS）は「Root Bone」として指定したボーンを基準に決まります（公式の記載）。ランクへの効果は、未確認です。lilAvatarUtils のレンダラーのタブには Bounds の項目があります。編集できるかは、未確認です。
- **スキンドメッシュの数・基本メッシュの数**: AAO の Merge Skinned Mesh は、スキンドメッシュを 1 つにまとめます。基本メッシュ（ボーンで動かないメッシュ）は、Merge Skinned Mesh の「メッシュレンダラー」の設定で、手動でまとめられます。自動の対象になるかは、未確認です。
- **マテリアルスロットの数**: TTT の AtlasTexture には、マテリアルの結合を設定する「マテリアル設定」があります（[公式: AtlasTexture](https://ttt.rs64.net/docs/Reference/AtlasTexture)）。AtlasTexture を使うマテリアルに限ります。AAO の Merge Skinned Mesh は、統合対象のメッシュで同じマテリアルを使っているマテリアルスロットを、既定で統合します（[公式 FAQ](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）。
- **PHYSBONE の項目（AAO）**: AAO の Merge PhysBone は、まとめるたびに PhysBone Affected Transforms の数が 1 つ増えます（公式ページの記載）。一方、[Replace End Bone With Endpoint Position](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/) は、end ボーンを Endpoint Position に置き換えて、PhysBone Affected Transforms の数を減らせます（公式ページの記載）。
- **PHYSBONE トランスフォームの数（lilAvatarUtils）**: 公式の項目は「ボーン数」（揺れが計算されるボーン数）です。Transform の数と同じかは未確認です。
- **PHYSBONE の項目（lilAvatarUtils）**: PhysBone のタブは、親が同じ PhysBone を見つけて、コンポーネントを統一できる候補を確認するのに役立つと書かれています。PBコライダーのタブは、どこからも参照されない無駄なコライダーの削減に役立つと書かれています。統一や削除の操作は、この記事では未確認です。
- **PHYSBONE コライダーの数（MA）**: MA の [PhysBone Blocker](https://modular-avatar.nadena.dev/ja/docs/reference/physbone-blocker) は、子オブジェクトから、親オブジェクトをルートとする PhysBone の影響を防ぎます。[Global Collider](https://modular-avatar.nadena.dev/ja/docs/reference/global-collider) は、VRChat のグローバルコライダーの数に上限があるため、既存のグローバルコライダー（指や手など）を差し替える機能です。どちらも、コライダーの数を減らす機能としては、未確認です。
- **コライダーの数（lilAvatarUtils）**: PBコライダーのタブで一覧を確認できるのは、PhysBone 用のコライダーです。通常の Collider を含むかは、未確認です。
- **コンタクトの数（AAO）**: AAO の「使われていないObjectを自動的に削除する」（英語名は Remove Unused Objects）は、1.7.0（2024 年 4 月 30 日）から、Animator Controller で使われていないパラメーターを持つ PhysBone と Contact Receiver を削除するようになりました（公式の [changelog](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。条件つきで、自動で行われます。
- **コンストレイントの数**: MA の [Convert Constraints](https://modular-avatar.nadena.dev/ja/docs/reference/convert-constraints) は、あらかじめ変換するとパフォーマンスが大幅に向上すると、公式が書いています。公式は性能向上のみを書いており、件数への効果は未確認です。AAO の changelog には、1.6.12（2024 年 4 月 9 日）の Removed に「Activeness Optimization for Constraint component」（理由は「The constraint component is too complex to optimize correctly and reliably」。複雑すぎて、正しく確実に最適化できない）があります。1.7.12 の changelog には、「VRCConstraints with Target might be removed unexpectedly」（Target を持つ VRCConstraints が意図せず削除される）という修正の記録もあります。件数を減らす機能としての記載ではありません。そのため、AAO の列は「？ 公式に記載なし」としています。
- **パーティクルシステムの数（AAO）**: AAO の changelog には、件数を減らす機能としての記載はありません。使われていない Particle System の扱いについては、削除まわりの修正の記録があります。1.6.0（2023 年 11 月 27 日）の「Remove Unused Object may break ParticleSystem」と、1.8.10（2025 年 5 月 4 日）の「Fixed Particle Systems are not correctly removed」です。
- **アニメーターの数**: MA の Merge Animator は、「付属アニメーターを削除」をオンにしたときに減らせます。AAO の「使われていないObjectを自動的に削除する」は、1.6.0（2023 年 11 月 27 日）から、Animator Controller のない Animator を、意味のないものとして削除するようになりました（公式の changelog）。
- **ボーンの数**: MA の Merge Armature は、名前が一致するボーンを統合先にまとめます。名前が合わないと、新しいボーンが増えます。AAO は、使われていないObjectを削除します。
- **ライトの数（lilAvatarUtils）**: ライティングのタブは、見え方を確かめる機能です。ライトを減らす機能は、未確認です。
- **レイキャストの数（AAO）**: AAO の changelog 1.9.9（2026 年 4 月 18 日）は、VRCRaycast への対応と、RaycastCount のパフォーマンスカテゴリへの対応を挙げています。ただし、対応によって数が減るかは、この記事では未確認です。

## ツールだけでは減らせない項目の考え方

- 表の「？ 未確認」の意味は、「項目 × ツールの早見表」の前の凡例を見てください。
- ツールで減らせない項目（または、この記事では確認していない項目）は、Unity の標準の操作で減らせることがあります。たとえば、使わないコンポーネントを取り除く、使わないオブジェクトを消す、といった方法です。
- まず、今どの項目が重いのかを見ることが大事です。VRChat SDK の Control Panel でランクを確かめ、いちばん悪い項目から減らします（[パフォーマンスランク](/learn/vrchat/performance-rank)）。
- AAO の公式ドキュメントは、Control Panel のパフォーマンスランクは非破壊改変ツールを使うとあてにならないとしています。Play モードに入ったときのランクを、anatawa12 さんの Gist Pack の Actual Performance Window で確かめる方法を案内しています。AAO を使ったあとのランクは、この方法で確かめてください。
- 減らす手順の例は、[アバターを軽くする](/learn/vrchat/optimization) にまとめています。
- ツールを入れたあとは、Build & Test で見た目と動きが変わっていないかを確かめてください。
- 複数のツールを併用するとき、順序や衝突について、この記事では確認していない組み合わせがあります（MA と AAO など）。想定どおりに動くか、実際に確かめてください。
