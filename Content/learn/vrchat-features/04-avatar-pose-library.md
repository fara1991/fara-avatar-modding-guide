---
title: AvatarPoseLibrary（APL）でポーズを設定する
reading: あばたーぽーずらいぶらりーでぽーずをせっていする
description: AvatarPoseLibrary（APL）の入れ方、既存の AnimationClip をポーズとして登録する手順、VRChat のメニューでの使い方をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- APL は既存の AnimationClip を登録して呼び出すツールなので、ポーズ自体は別に作った AnimationClip を用意する
- 依存パッケージの版は導入ページと package.json で書き方が違い、パラメーター数への影響は、Unity-Manual の書き方だけ向きが逆
- Resources/ のファイルは、公式パッケージの一部としてだけ再配布できるので、ポーズのデータを配布する前に LICENSE を読む
:::

[AvatarPoseLibrary（APL）](https://github.com/HhotateA/AvatarPoseLibrary) は、Unity 上で VRChat 向けのアバター用のポーズを管理・適用するためのツールです（[README](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/README.md)）。この記事では、APL の入れ方と、ポーズを登録する手順、VRChat での使い方を説明します。パラメーター（Expression Parameters）への影響と、注意点もまとめます。

::: warning
APL は、既存の AnimationClip（Unity のアニメーションの動きを保存したファイル）を登録して、VRChat のメニューから呼び出すツールです。

そのため、ポーズそのものは、別の方法で作った AnimationClip を用意してください。

この記事では、ポーズを作って保存する方法（Scene ビューで関節を動かして保存するなど）は扱いません。
:::

[![APL でポーズを設定する流れ。VCC にリポジトリを追加、プロジェクトに追加、アバターの子に空の GameObject、コンポーネントを付ける、カテゴリーとポーズを登録、アップロードして使う、の 6 つ](images/avatar-pose-library-flow.png)](/learn/vrchat-features/images/avatar-pose-library-flow.png)
*APL でポーズを設定する全体の流れ（導入から使うまで。README・wiki の手順を、この記事で 6 つにまとめたもの。ポーズの設定の細かい手順は、「ポーズを設定する」の 1〜6）*

## この記事の版

この記事は、APL の [1.2.44](https://github.com/HhotateA/AvatarPoseLibrary/releases/tag/1.2.44) を基に書いています。このリリースは 2026 年 9 月 21 日（日本時間。UTC では 9 月 20 日）に公開されました。対応する Unity は 2022.3 です（リポジトリの [package.json](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Packages/com.hhotatea.avatar-pose-library/package.json) の `unity` の項目）。

## 用意するもの

- **Unity 2022.3**（APL の対応版）
- **VRChat のアバター SDK（`com.vrchat.avatars`）**
- **NDMF（`nadena.dev.ndmf`）**（Non-Destructive Modular Framework の略。アバターを非破壊で改変するための基盤です）
- **Modular Avatar（`nadena.dev.modular-avatar`）**（衣装などを、アバターを壊さずに組み込むツールです）。APL は Modular Avatar を前提として設計されています（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。

依存パッケージの版は、導入ページ（[How-to-Install](https://github.com/HhotateA/AvatarPoseLibrary/wiki/How-to-Install)）と APL の package.json で書き方が違います。表の左の列は導入ページの版で、右の列は package.json の条件です。依存パッケージは、APL より先に Unity のプロジェクトに入れておきます。

| パッケージ | 導入ページの記載（[How-to-Install](https://github.com/HhotateA/AvatarPoseLibrary/wiki/How-to-Install)） | リポジトリの package.json |
|---|---|---|
| `com.vrchat.avatars` | `"com.vrchat.avatars": "3.8.0"` | `">=3.7.0"` |
| `nadena.dev.ndmf` | `"nadena.dev.ndmf": "1.7.9"` | `">=1.5.0"` |
| `nadena.dev.modular-avatar` | `"nadena.dev.modular-avatar": "1.12.5"` | `">=1.10.0"` |

導入ページの NDMF 1.7.9 は、AAO（Avatar Optimizer）の要件（1.8.0 以上）より低いため、AAO と一緒に使う場合は、NDMF を 1.8.0 以上 2.0.0 未満にしてください（AAO の要件。[AAO の記事](/learn/optimize-aao/avatar-optimizer) を参照）。

::: warning
導入の前に、Unity のプロジェクトのバックアップを取ってください。
:::

## 入れ方

### VCC から入れる

APL は、[VCC（VRChat Creator Companion）](/learn/vrchat/vcc-project) から入れるのがおすすめです（[README](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/README.md)）。手順は次のとおりです。

1. VCC を開き、**Settings** に移動します。
2. **Packages** タブをクリックします。
3. **Add Repository** をクリックします。
4. 入力欄に、次の URL を貼り付けます。
   ```text
   https://HhotateA.github.io/AvatarPoseLibrary/index.json
   ```
5. **Add** をクリックします。
6. リポジトリの情報を確認し、**I Understand** をクリックします。
7. 任意のプロジェクトの **Manage Project** から APL を追加します。

APL の公式サイトにある「VCCに追加」ボタンから入れる方法もあります。その場合は、「Creator Companion を開く」を選び、「I Understand、AddRepository」をクリックします（[Beginner-Tutorials](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Beginner-Tutorials)）。

### UnityPackage から入れる

1. 先に、「用意するもの」の依存パッケージを、Unity のプロジェクトに入れておきます。
2. [リリースのページ](https://github.com/HhotateA/AvatarPoseLibrary/releases/latest/) から [UnityPackage](/glossary#unitypackage) を取得します。
3. Unity のプロジェクトを開き、UnityPackage をドラッグ＆ドロップします。
4. 追加されるファイルを確かめて、**Import** をクリックします。

## ポーズを設定する

::: warning
アバターを変更する前に、プロジェクトのバックアップを取ってください。今のアバターの動き（表情やジェスチャーなど）も、録画やスクリーンショットで記録しておくと、後で比べられます（**この記事の助言**{.lbl .lbl-adv}）。
:::

手順は、wiki の [Beginner-Tutorials](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Beginner-Tutorials) の流れに、[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual) の項目の説明を合わせたものです。

1. アバターの [Prefab](/glossary#prefab) を、Hierarchy ウィンドウにドラッグ＆ドロップします。
2. Hierarchy 上で右クリックし、**Create Empty** を選びます。作った GameObject は、アバターの子にします（アバターの直下に生成されます。[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual)）。
3. その GameObject を選び、Inspector で **Add Component** をクリックします。検索欄に「Avatar Pose Library」と入力し、表示されたコンポーネントを選びます。
4. コンポーネントの右上の **+** をクリックして、カテゴリーを追加します。
5. カテゴリーの中に、ポーズを登録します。各項目は、次の表のとおりです。
6. 複数のポーズをまとめて追加するときは、**Drop Area** に AnimationClip をまとめてドラッグ＆ドロップします。

| 項目 | 説明（[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual) の要約） |
|---|---|
| Library Name | VRChat のメニューに表示される名前です。この名前が同じコンポーネントは、アップロード時に統合されます。 |
| Mode Settings | Enable Height、Enable Speed、Enable Mirror、Enable Trackings を切り替えます。同じ Library Name では、単一の設定になります。 |
| Category Icon / Category Name | カテゴリーごとのメニューのアイコンと名前です。 |
| Pose Name | ポーズのメニュー名です。初期値は AnimationClip の名前です。 |
| Pose Icon | ポーズのメニューのアイコンです。Auto Thumbnail が有効なときは設定できません。 |
| Auto Thumbnail | アップロード時に、サムネイルを自動で撮影し、アバターの画像に差し替えます。 |
| Animation Clip | ポーズに関連付けられた AnimationClip です。 |
| Tracking Settings | ポーズ再生中のトラッキングの設定です。VRChat のメニューからも変更できます。 |
| Is Loop | ポーズをループ再生します。静的なアニメーションでは、初期設定で有効になっています。 |
| Motion Speed | 再生速度の初期値です。 |
| Drop Area | AnimationClip をまとめてドラッグ＆ドロップすると、ポーズを一括で生成します。 |

Mode Settings の 4 つの項目は、次のとおりです（[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual)）。

- Enable Height: ポーズ再生中の、アバターの地面の高さを設定できます。
- Enable Speed: ポーズの再生速度を変更できます。有効にすると、静的なアニメーションも自動的に生物らしいランダムなアニメーションに変換されます。
- Enable Mirror: ポーズを左右反転できます。
- Enable Trackings: トラッキングメニューを表示します。

[![APL の設定の入れ子。アバターの Prefab の下に空の GameObject、その中に Avatar Pose Library コンポーネント、その中にカテゴリー、カテゴリーの中にポーズ](images/avatar-pose-library-structure.png)](/learn/vrchat-features/images/avatar-pose-library-structure.png)
*設定の入れ子（Unity-Manual の説明を、この記事で図にしたもの）*

[![APL のコンポーネントの Inspector。メニュー名、高さ調整機能などのチェック、カテゴリーリスト、ポーズリスト、各ポーズのアニメーション・トラッキング系・ループ再生・再生速度](images/avatar-pose-library-inspector.png)](/learn/vrchat-features/images/avatar-pose-library-inspector.png)
*新しく作った VRChat 用のプロジェクトに APL 1.2.44 を入れて、Unity 2022.3.22f1 で開いた Inspector（日本語の表示）。カテゴリー「Sit」に 2 つのポーズを登録し、ポーズを開いた状態。アニメーションは、空のクリップを仮に入れている*

日本語の画面では、項目の名前が日本語で表示されます。

| 画面の表示（日本語） | wiki の名前（英語） |
|---|---|
| メニュー名 | Library Name |
| カテゴリーリスト ／ カテゴリー名 | Category List ／ Category Name |
| ポーズリスト ／ すべて開く ／ すべて閉じる | Pose List ／ Open All ／ Close All |
| アニメーション | Animation Clip |
| トラッキング系 | Tracking Settings |
| ループ再生 ／ 再生速度 | Is Loop ／ Motion Speed |
| サムネ自動撮影 | Auto Thumbnail（パッケージの英語の表示は「Auto」） |
| ここにアニメーションをドロップ | Drop Area（パッケージの英語の表示は「Drop AnimationClips here to add Poses」） |
| 高さ調整機能 ／ スピード調整機能 ／ ミラー機能 ／ トラッキング機能 | Enable Height ／ Enable Speed ／ Enable Mirror ／ Enable Trackings（パッケージの英語の表示は Enable Tracking） |

::: tip
Drop Area のツールチップには、AnimationClip をドロップすると Pose を生成できると書かれています（英語の表示は "You can generate poses by dropping animation clips here in bulk." です）。この記事では、Drop Area に入れる手順を書いています。

ポーズ行の右クリックメニューには「Create Pose（新規作成）」の項目もあります。この項目は、空の項目を 1 つ追加します。この記事では使いません。
:::

## 保存して共有する

設定が済んだ GameObject は、Project タブにドラッグ＆ドロップすると Prefab になります。この Prefab と AnimationClip をセットで UnityPackage に出力すると、APL を持つ人同士で設定を共有できます（[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual)）。配布する前に、下の「注意・うまくいかないとき」にあるライセンスの注意を読んでください。

Prefab を使う側は、次の手順で入れます（[Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual) の「Prefabの使用」）。

1. APL を含む依存パッケージが、Unity のプロジェクトに入っているかを確かめます。
2. APL 対応の UnityPackage を Import します。
3. バックアップを取ってから、設定済みの Prefab を、アバターの直下にドラッグ＆ドロップします。
4. アバターをアップロードし、導入を確認します。

## VRChat での使い方

アップロードしたアバターでは、VRChat のアクションメニューに Library Name の項目が出ます（[VRC-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/VRC-Manual)）。使い方は次のとおりです。

- **カテゴリー**: メインメニューは、ポーズのカテゴリーごとに分かれています。
- **ポーズの選択**: 1 度押すとポーズが有効になり、もう一度押すと無効になります（[VRC-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/VRC-Manual)）。ポーズを選ぶと、すぐに再生されます（[Beginner-Tutorials](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Beginner-Tutorials)）。
- **設定**: リセット、高さ、再生速度、左右反転、トラッキング設定があります。再生速度は、50% が通常の速度です。リセットは、再生中のポーズを中断し、設定も初期化します。
- **トラッキング**: 各部位を、アニメーション優先かトラッキング優先かを選べます。「移動抑制」を有効にすると、コントローラーでの移動が無効になります。

Quest の場合は、左手のメニューから「表情」や「パラメータ」を選ぶところから始めます（[Beginner-Tutorials](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Beginner-Tutorials)）。

## パラメーター（Expression Parameters）への影響

パラメーター数への影響について、資料の記載と、APL 1.2.44 の実装を並べます。

- [Unity-Manual](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Unity-Manual) の Mode Settings の説明は、有効にするとパラメーター数が削減される書き方で、次のとおりです。
  - Enable Height: 8 減る
  - Enable Speed: 8 減る
  - Enable Mirror: 1 減る
  - Enable Trackings: パラメーター数は減らない
- [Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A) では、「パラメーター数が心配です。」への答えとして、少しでも削減したい場合は、Enable Height、Enable Speed、Enable Mirror の 3 項目を無効にすると、17 パラメーター減ります。

Unity-Manual の説明は、有効にするとパラメーター数が削減される書き方です。Q&A（無効にすると削減）と実装（有効にすると追加）とは向きが逆です。

APL 1.2.44 の実装では、Height と Speed は有効にするとそれぞれ Float のパラメーターを 1 つ追加します（同期されるのは、Height が 8 ビット、Speed が 8 ビット）。Mirror は有効にすると Bool のパラメーターを 1 つ追加します（同期されるのは 1 ビット）。MirrorCycleOffset（Float）は同期しません（localOnly）。Unity-Manual の 8・8・1 と、Q&A の 17 は、同期されるパラメーターのビット数（Float 8、Float 8、Bool 1）と一致します。

APL は、複数のポーズを 1 つの Int パラメーターで制御する設計です（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。APL 1.2.44 の実装では、フラグ用の Int のパラメーターを 2 つ追加し、ポーズの番号用の Int は 255 ポーズごとに 1 つ追加されます。ポーズ数の上限は、最新版で 65280 ポーズ（16 パラメータ）です（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A) の記載です）。

::: tip
パラメーターの数は、VRChat SDK の Expression Parameters の表示で確かめてください（**この記事の助言**{.lbl .lbl-adv}）。Expression Parameters の基本は、[Expression メニューとパラメーター](/learn/vrchat-features/expressions) を見てください。
:::

## 注意・うまくいかないとき

::: warning
- **非破壊について**: APL は非破壊で改変するので、アンインストールすれば問題なく動作する、というのが作者の説明です（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。念のため、アップロードの前にバックアップを取ってください。
- **名前の衝突**: APL が作る Animator（アニメーションの切り替えを制御する Unity の部品）のレイヤーやパラメーターは、ユニークな名前空間で管理されています。ただし、特殊な構成のアバターでは、手動の調整が必要になることがあります（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。
- **テレメトリ（利用状況データ）**: Unity Editor からは、利用状況のデータが送られます（[Analytics.md](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Analytics.md)、[privacy.html](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Website/privacy.html)）。初回起動時に「最小限のみ」か「詳細送信を許可」を選びます（画面のボタンは「最小限」「許可」）。変更は `Tools > Avatar Pose Library > Privacy Settings` からできます。どちらのモードでもイベントは送信されます。ただし、ビルド失敗時のエラーレポートは、最小限モードでは確認して許可された場合だけ送信されます（[Analytics.md](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Analytics.md)）。送られる内容は、モードによって違います。
- **ライセンス**: README では、このプロジェクトは MIT ライセンスで提供されます（[README](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/README.md)）。一方、[LICENSE](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/LICENSE) には、次の条文があります。「Files under Resources/ may only be redistributed as part of the official AvatarPoseLibrary package. Please remove any modifications. All other files are licensed under the MIT License.」つまり、Resources/ のファイルは公式パッケージの一部としてだけ再配布でき、それ以外は MIT ライセンスです。README の「許諾なしで再配布OK」は、Resources/ には当てはまりません。ポーズのデータを配布する前に、ルートの `LICENSE` を読んでください（**この記事の助言**{.lbl .lbl-adv}）。
- **サポート対象外の機能**: wiki の [Secret](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Secret) には、メニューの階層化などの機能があります。これらの機能はサポート対象外です。この記事の手順には入れていません。
:::

### 既知の問題

- **ほかのツールとの併用**: 可愛いポーズツール（ゆにスタジオ様）の「他のAnimatorControllerのトラッキング機能を統合する」機能を使うと、この機能を使うと APL の Animator が壊れるという報告があります。原因は調査できていません（[Troubleshooting](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Troubleshooting)）。APL 側に対策アップデートの予定はないので、この機能をオフにするのがおすすめです（[Troubleshooting](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Troubleshooting)）。
- **リップシンクや表情が固定される**: WriteDefault などの値によって、動作が不安定になることがあります（[Troubleshooting](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Troubleshooting)）。Write Defaults の説明は、[表情と FX の基礎](/learn/vrchat-features/fx-basics) を見てください。WriteDefault の設定を見直すことと、APL の Inspector のプリセットのメニューにある「ポーズのみ」を試すことがおすすめです（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。AnimationClip に含まれる表情のキーを削除すると、正常に動く可能性があります（[Troubleshooting](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Troubleshooting)）。元のクリップは直接消さず、複製してから試してください（**この記事の助言**{.lbl .lbl-adv}）。
- **頭の固定を解除すると腰が浮く**: 頭の固定を解除すると、腰が浮くことがあります。VRChat の仕様による可能性があります。頭の位置に合わせて、高さや姿勢を微調整するとよいです（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。
- **フルトラ（全身トラッキング）で棒立ちになる**: 設定項目のフルトラッキング時の移動アニメーションを無効にしていると、棒立ちになることがあります。移動アニメーションを有効にすると、改善することがあります（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。
- **VRChat 上で動かない**: VRChat の整合性チェックを試すとよいです。改善しないときは、[作者の Discord](https://discord.gg/j393GwU4Es) で相談できます（[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。

### ビルド時間とデータ量

ポーズ数とビルド時間の目安は、次のとおりです（提供: aster 様、[Q&A](https://github.com/HhotateA/AvatarPoseLibrary/wiki/Q&A)）。APL なしは 4 秒、256 ポーズは 16 秒、1024 ポーズは 1 分 59 秒です。ポーズ 1 種が 100kb ほどだとして、ポーズごとにデータ量とビルド時間が増えるため、実質的な上限の目安は 1000 前後です。

## 関連ページ

- [Expression メニューとパラメーター](/learn/vrchat-features/expressions)
- [表情と FX の基礎](/learn/vrchat-features/fx-basics)
- [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)
- [VCC でプロジェクトを作る](/learn/vrchat/vcc-project)
