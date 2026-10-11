---
title: アバターをアップロードする
description: VRChat SDK のコントロールパネルでログインし、Builder タブで名前・公開範囲・サムネイルを決めて、Build & Test で確かめてから Build & Publish でアップロードするまでです。
difficulty: 初級
updated: 2026-10-11
---

## アップロードできるアカウント

アップロードには **VRChat のアカウント**（vrchat.com のアカウント）が必要です。Steam や Meta のアカウントだけで遊んでいる場合は、VRChat のアカウントを作ってつなげておきます。

また、アップロードできるのは、トラストランクが **New User 以上** のアカウントです。Visitor のうちは、自分の PC で試す **Build & Test** だけ使えます。遊んでいるうちにランクは上がります。

## 流れ

1. Unity のメニュー **VRChat SDK > Show Control Panel** でコントロールパネルを開く
2. **Authentication** でログインする
3. **Builder** タブでアバターを選び、エラーがないか確かめる
4. 名前・説明・公開範囲・サムネイルなどを決める
5. **Build & Test** で、自分の PC の VRChat だけで試す
6. 問題なければ **Build & Publish** でアップロードする

アップロード前に確かめることは [アップロード前の確認](/learn/unity/pre-upload-check) のチェックリストにまとめています。

## 1. コントロールパネルを開いてログインする

Unity の上のメニューに **VRChat SDK** が出ていれば、SDK が入っています。**VRChat SDK > Show Control Panel** を押すとコントロールパネルが開きます。最初は **Authentication** の画面になるので、VRChat のアカウントでログインします。

| タブ | 何をするところか |
|---|---|
| Authentication | ログイン・ログアウト |
| Builder | アップロードするアバターを選び、確かめて、アップロードする |
| Content Manager | 自分がアップロードしたアバターの一覧。Blueprint ID の確認もここ |
| Settings | SDK の設定 |

VRChat SDK のメニューが出ないときは、Console に赤いエラーが出ていて、SDK が読み込まれていないことがほとんどです（[アップロードでつまずいたとき](/learn/vrchat/upload-errors)）。

## 2. Builder で確かめる

**Builder** タブを開くと、シーンにある（VRC Avatar Descriptor の付いた）アバターが選べます。アバターを選ぶと、パフォーマンスランクと、確かめた結果（Validations）が出ます。

- **赤いエラー** があるとアップロードできません。上から読み、**Auto Fix** のボタンがあるものは押して直します
- **黄色い警告** はアップロードはできますが、内容は読んでおきます
- パフォーマンスランクの見方は [パフォーマンスランク](/learn/vrchat/performance-rank) を見てください

## 3. 名前・公開範囲・サムネイル

| 項目 | 決めること |
|---|---|
| Name | アバターの名前（ゲーム内のアバター一覧に出る） |
| Description | 説明（任意） |
| Content Warnings | 内容の注意書き。当てはまるものを選ぶ |
| Visibility | **Private**（自分だけが使える）か **Public**（ほかの人もワールドのペデスタルなどから使える） |
| Thumbnail | サムネイル画像。あとから画像だけ差し替えることもできる |

::: warning
購入したアバターを **Public** にしてよいかは、アバターの規約で決まります。多くのアバターは、改変したものを含めて **Private** でしか使えません。迷ったら Private にしてください。
:::

## 4. Build & Test で試す

Builder の下の方で、ビルドの種類を **Build & Test**（テスト用のビルド）にして、ボタンを押します。ビルドが終わると、そのアバターが **自分の PC の VRChat にだけ** 読み込まれます。アップロードはされません（[Q&A](/qa#build-and-test-vs-upload)）。

1. VRChat を起動する（既に起動していれば、アバターのメニューを開き直す）
2. メインメニューの **アバター** の、SDK のテスト用アバターの欄（**SDK Test Avatars** など）に出ているものを選ぶ
3. 歩く・座る・表情・メニュー・揺れものを確かめる

衣装を着せたら、動かして服が体についてくるか、元の服が出ていないかを必ず確かめます。ここで直してからアップロードすると、手戻りが少なくなります。

::: tip
Build & Test はアップロードしないので、ファイルサイズの上限などの一部の検査は行われません。テストでは動いたのに Build & Publish で止まる、ということもあります。
:::

## 5. Build & Publish でアップロードする

ビルドの種類を **Build & Publish**（オンラインに公開するビルド）にし、プラットフォーム（PC なら **Windows**）を選び、権利を確認するチェックを入れて、**Build & Publish** を押します。終わると、VRChat のアバターのメニューに出てきます（反映まで少しかかることがあります）。

Android（Meta Quest など）向けにも出すときは、プラットフォームの切り替えが必要です（[Android（Quest）対応](/learn/vrchat/android)）。

## Blueprint ID と上書き

アップロードすると、アバターに **Blueprint ID**（`avtr_` で始まる ID）が付き、アバターのルートの **Pipeline Manager** に保存されます（[用語集](/glossary#blueprint-id)）。

- 同じ Blueprint ID のままもう一度アップロードすると、前のアバターが **上書き** される（更新）
- 別のアバターとして出したいときは、Pipeline Manager の **Detach** で ID を外してからアップロードする（[何て名前？](/howto#upload-as-new)）
- アバターを複製して色違いを作ったときは、ID も複製されているので、外し忘れると元のアバターを上書きしてしまう（[TIPS](/tips#overwrote-avatar)）

::: warning
Blueprint ID は、他人に知られると悪用されるおそれがあります。ID が写ったスクリーンショットを SNS などに載せないでください。
:::

## 更新するとき

改変を足したら、同じ Blueprint ID のまま Build & Publish すると、同じアバターが新しい内容に置き換わります。サムネイルを差し替えるだけなら、ビルドし直さなくても Builder から変えられます。

複数の衣装違いのアバターをまとめてアップロードし直したいときは、連続でアップロードするツールもあります（[TIPS](/tips#continuous-avatar-uploader)）。
