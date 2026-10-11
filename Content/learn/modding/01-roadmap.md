---
title: はじめての改変ロードマップ
description: 買ったアバターをアップロードするところから、色変え・対応衣装・衣装の ON/OFF、Blender が必要になる改変まで、どの順に進めればよいかの道しるべです。
difficulty: 初級
updated: 2026-10-11
---

## 先に Unity、Blender はあとから

アバター改変は **Unity から** 始めるのがおすすめです。アップロード、色の変更、対応衣装の着せ替え、メニューでの ON/OFF は、どれも Unity（と画像編集ソフト）だけでできます。Blender が必要になるのは、衣装が肌に埋まる・体形が合わないなど、**メッシュそのものを直す** 必要が出てからです（[Q&A: Unity と Blender のどちらから始める？](/qa#start-unity-or-blender)）。

| 段階 | やること | 使うもの | 記事 |
|---|---|---|---|
| 1 | Unity の画面に慣れる | Unity | [Unity の画面の見方](/learn/unity/editor-overview) |
| 2 | 買ったアバターをそのままアップロードする | VCC・VRChat SDK | [VCC でプロジェクトを作る](/learn/vrchat/vcc-project)、[アップロードする](/learn/vrchat/upload) |
| 3 | 髪や服の色を変える | lilToon・画像編集ソフト | [テクスチャの色を変える](/learn/unity/recolor) |
| 4 | 対応衣装を着せる | Modular Avatar | [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) |
| 5 | 衣装や小物をメニューで ON/OFF する | Modular Avatar | [Expression メニューとパラメーター](/learn/vrchat-features/expressions) |
| 6 | 重さ（パフォーマンスランク）を確かめて軽くする | VRChat SDK・Avatar Optimizer | [パフォーマンスランク](/learn/vrchat/performance-rank)、[アバターを軽くする](/learn/vrchat/optimization) |
| 7 | 非対応衣装を着せる・体形を直す | Blender | [非対応衣装を Blender で着せる](/learn/modding/unsupported-outfit) |

1 つ進むごとに **Build & Test** で VRChat に入って確かめると、どこで壊れたかがすぐわかります。

## 1. Unity の基本の言葉

最初に、次の言葉だけ押さえておけば十分です（詳しくは [Q&A: Project / Hierarchy / Inspector とは？](/qa#unity-editor-basics)）。

| 言葉 | 意味 |
|---|---|
| Project | プロジェクトのファイル置き場。unitypackage を読み込むとここに入る |
| Hierarchy | シーンに置いたものの一覧。アバターはここに置いて作業する |
| Inspector | 選んだものの設定。コンポーネントの追加もここ |
| Prefab | 部品として保存されたもの。アバターは FBX ではなく Prefab を置く |
| 子にする | Hierarchy でドラッグして、別のものの中に入れること。衣装はアバターの子にする |

## 2. まずはそのままアップロードする

改変する前に、買ったアバターを **何も変えずに** アップロードしてみます。ここでつまずく原因（Unity のバージョン、シェーダーの入れ忘れ、ログイン）を先に片付けておくと、あとで改変したときに「改変のせいか、最初からか」で迷いません。

1. VCC で Avatars のプロジェクトを作る（[VCC でプロジェクトを作る](/learn/vrchat/vcc-project)）
2. アバターの説明書にあるシェーダー（lilToon など）を入れてから、アバターの unitypackage を読み込む
3. アバターの Prefab を Hierarchy に置き、VRChat SDK の Builder からアップロードする（[アップロードする](/learn/vrchat/upload)）

## 3. 色を変える

色変えは、テクスチャを描き直さなくても **lilToon の色調補正** でできます。テクスチャ自体を変えたいときは、作者が配っている改変用の PSD を画像編集ソフトで編集します（[テクスチャの色を変える](/learn/unity/recolor)）。マテリアルは複製してから触ると、いつでも元に戻せます。

## 4. 対応衣装を着せる

商品ページに「○○対応」と書いてある衣装は、衣装の Prefab をアバターの子に入れて **Setup Outfit** を実行するだけで着せられます（[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit)）。元の服は無効にして Tag を EditorOnly にします。この流れは、ブラウザの [Unity の練習](/practice/unity) でも試せます。

## 5. メニューで ON/OFF する

帽子や上着をあとから出し入れしたいときは、Modular Avatar の Object Toggle でメニューを作ります。アニメーションやパラメーターを手で書かなくても、VRChat の Expression メニューに項目が追加されます（[Expression メニューとパラメーター](/learn/vrchat-features/expressions)）。

## 6. 重さを確かめる

衣装を足していくと、パフォーマンスランクが下がっていきます。Builder でどの項目が重いかを確かめ、使わないものを外したり、Avatar Optimizer で自動で軽くしたりします（[パフォーマンスランク](/learn/vrchat/performance-rank)、[アバターを軽くする](/learn/vrchat/optimization)）。

## 7. Blender が必要になったら

次のようなときは Blender の出番です。

- 衣装を着せると、肌が衣装を突き抜ける・衣装が体に埋まる
- 対応アバターが書かれていない衣装（非対応衣装）を着せたい
- 体形を変えたい、見えない部分のメッシュを消したい

Blender の操作に慣れていなければ、目次の Blender の区切り（[インストール](/learn/getting-started/install) から [リギング](/learn/rigging/armature) まで）を先に読んでください。基本のキー操作は、ブラウザの [操作の練習](/practice) で試せます。非対応衣装を着せる流れは [非対応衣装を Blender で着せる](/learn/modding/unsupported-outfit)、うまくいかないときは [服が動かない・凹む・突き抜けるときのチェックリスト](/learn/modding/outfit-checklist) にまとめています。

::: warning
改変の前に、アバターと衣装の **利用規約** を確認してください。改変の可否、他のアバターへの流用、素体のウェイトを使ってよいかなどは商品ごとに決まっています（[TIPS: 利用規約を確認する](/tips#check-license-terms)）。
:::

## 困ったときは

- 操作やメニューの名前がわからない: [あの操作、何て名前？](/howto)
- よくあるつまずき: [TIPS](/tips)
- よく出る質問: [改変 Q&A](/qa)
- 言葉の意味: [用語集](/glossary)
