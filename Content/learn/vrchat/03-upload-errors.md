---
title: アップロードでつまずいたとき
description: VRChat SDK のメニューが出ない、Builder に赤いエラーが出る、Blueprint ID のエラー、ファイルサイズの超過、アップロードしたのに反映されないなど、よくあるつまずきと直し方です。
difficulty: 初級
updated: 2026-10-11
---

## まず Console を見る

どのつまずきでも、最初に見るのは Unity の **Console** ウィンドウです（無ければ **Window > General > Console**）。赤いエラーがあれば、**いちばん上のもの** から読みます。下のエラーは、上のエラーの巻き添えで出ていることがよくあります。

- エラーをクリックすると、下に詳しい内容が出る
- **Clear** を押してから同じ操作をすると、その操作で出たエラーだけが残る
- エラーの文をそのまま検索すると、同じ症状の解決策が見つかることが多い

## 症状から探す

| 症状 | よくある原因 | 直し方 |
|---|---|---|
| メニューに **VRChat SDK** が出ない・コントロールパネルが開かない | Console にコンパイルエラーがあり、SDK が読み込まれていない | 下の「VRChat SDK のメニューが出ない」 |
| Builder にアバターが出ない | アバターのルートに VRC Avatar Descriptor がない、シーンに置いていない | Prefab を Hierarchy に置く。FBX ではなく Prefab を使う |
| Builder に **赤いエラー** がある | 検査で問題が見つかった | エラーを上から読み、**Auto Fix** があれば押す（[TIPS](/tips#cannot-upload)） |
| アップロードのボタンが押せない | ログインしていない、赤いエラーがある、権利の確認にチェックがない | Authentication でログイン、エラーを直す、チェックを入れる |
| アップロードできないアカウントだと言われる | トラストランクが Visitor | New User 以上になるまでは Build & Test だけ使える |
| Blueprint ID のエラー（自分のものではない ID） | 購入したアバターや、ほかの人のプロジェクトの ID が残っている | 下の「Blueprint ID のエラー」 |
| ファイルサイズが大きすぎて止まる | テクスチャが大きい、使わないものが入っている | 下の「サイズの上限を超えた」 |
| アップロードしたのに VRChat に出ない | 別のアカウントでログインしている、反映待ち、Build & Test を押していた | 下の「アップロードしたのに見つからない」 |
| 前のアバターが置き換わってしまった | Blueprint ID が同じまま | [TIPS: 別のアバターに上書きしてしまった](/tips#overwrote-avatar) |
| 全体がピンク | シェーダーが入っていない | [TIPS](/tips#pink-material) |

## VRChat SDK のメニューが出ない

Unity は、プロジェクトのスクリプトのどれか 1 つでもコンパイルエラーがあると、ほかのエディター拡張（VRChat SDK や Modular Avatar のメニュー）も読み込みません。

1. Console の赤いエラーを読み、どのフォルダ（ツール）のスクリプトで出ているかを見る
2. そのツールが古い・必要なパッケージが足りない・Unity のバージョンに合っていない、のどれかであることが多い。ツールを更新するか、いったんプロジェクトから外す
3. VCC で VRChat SDK を、推奨されているバージョンにする（[VCC でプロジェクトを作る](/learn/vrchat/vcc-project)）
4. Unity を再起動する

詳しくは [Q&A: ツールをインポートしたのにメニューに出てこない](/qa#unity-tool-not-in-menu) を見てください。

## Builder の赤いエラー

よく出るものと直し方です。エラーの文は SDK のバージョンで変わるので、だいたいの意味で探してください。

| エラーの内容 | 直し方 |
|---|---|
| Avatar Descriptor がない | アバターの Prefab を使う。自作のときはルートに **VRC Avatar Descriptor** を付ける |
| アバターが Humanoid ではない | アバターの FBX の **Rig** タブで **Animation Type** を **Humanoid** にして **Apply** |
| Unity の Constraint が使われている | **Auto Fix** で VRChat の Constraint（VRC Constraint）に変換する |
| ほかのアバターが選ばれている・複数ある | シーンに置くアバターを 1 体にするか、Builder で目的のアバターを選ぶ |
| サイズや数の上限を超えている | 下の「サイズの上限を超えた」と [パフォーマンスランク](/learn/vrchat/performance-rank) |

## Blueprint ID のエラー

アバターのルートの **Pipeline Manager** に、自分のアカウントのものではない Blueprint ID が残っていると、アップロードで失敗します（「Error saving blueprint」や「Attempted to load the data for an avatar we do not own」など）。

1. アバターのルートを選び、Inspector の **Pipeline Manager** を見る
2. Blueprint ID が入っていたら **Detach** を押して外す
3. もう一度 Build & Publish する（新しいアバターとしてアップロードされる）

自分のアバターを更新したいのに ID が外れてしまったときは、**Content Manager** タブでそのアバターの ID を確かめ、Pipeline Manager で **Attach** して入れ直します（[Q&A: Error saving blueprint](/qa#blueprint-id-error)）。

## サイズの上限を超えた

VRChat には、アバターのファイルサイズの上限があります（2026 年 10 月時点の公式ページの値。変わることがあります）。

| プラットフォーム | ダウンロードサイズ | 展開後のサイズ |
|---|---|---|
| PC | 200 MB | 500 MB |
| Android（Quest など） | 10 MB | 40 MB |

上限を超えると、SDK がビルドのときに知らせて、アップロードを止めます。Build & Test では上限は検査されません。出典は [Avatar Size Limits](https://creators.vrchat.com/avatars/avatar-size-limits) です。

減らし方は次のとおりです。

- テクスチャの **Max Size** を下げる（4096 → 2048 → 1024）。いちばん効く（[TIPS](/tips#texture-too-large)）
- 使わない衣装・小物を **EditorOnly** にするか削除する。無効にしただけのものもアップロードに含まれる（[TIPS](/tips#editor-only-tag)）
- 使っていないシェイプキーやメッシュを減らす。Avatar Optimizer を使うと自動で減らせる（[アバターを軽くする](/learn/vrchat/optimization)）

## アップロードしたのに見つからない

- Build & Test を押していないか（テスト用のビルドはアップロードされない。[Q&A](/qa#build-and-test-vs-upload)）
- SDK と VRChat で、同じアカウントにログインしているか
- **Content Manager** タブに、そのアバターが出ているか
- アップロードしたプラットフォームと、遊んでいるプラットフォームが同じか（PC でだけアップロードしたアバターは、Quest では使えない）
- 少し時間をおいて、アバターのメニューを開き直す

## Unity のバージョンが違うと言われる

VRChat は Unity のバージョンを決めています（2026 年 10 月時点は 2022.3.22f1）。VCC が案内するバージョンを Unity Hub で入れ、プロジェクトをそのバージョンで開き直します（[TIPS: Unity のバージョンが違うと言われる](/tips#unity-version)）。指定より新しい Unity でアップロードしたアバターは、VRChat で読み込めません。

## それでも直らないとき

- VRChat の公式の [SDK Troubleshooting](https://creators.vrchat.com/sdk/sdk-troubleshooting) を見る
- 新しいプロジェクトにアバターだけを入れてアップロードしてみる。そこで通るなら、元のプロジェクトのほかのツールが原因
- Console のエラーの文と、使っている SDK・Unity・ツールのバージョンを書き留めてから質問すると、答えてもらいやすくなります
