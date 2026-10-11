---
title: VCC でプロジェクトを作る
description: VRChat Creator Companion（VCC）で、アバター用の Unity プロジェクトを作り、VRChat SDK や Modular Avatar などのパッケージを追加・更新する方法です。
difficulty: 初級
updated: 2026-10-11
---

## VCC とは

**VRChat Creator Companion**（VCC）は、VRChat 用の Unity プロジェクトを作り、VRChat SDK などのパッケージを管理する公式のツールです（[用語集: VCC](/glossary#vcc)）。

- アバター用のプロジェクトを、VRChat SDK 入りの状態で作れる
- Modular Avatar や lilToon などのパッケージを、一覧から追加・更新できる
- VRChat が指定する Unity のバージョンを案内してくれる

非公式ですが、同じことができる **ALCOM** というツールもあります。どちらを使っても、できあがるプロジェクトは同じです。この記事は VCC の画面で説明します。

## インストール

1. VRChat の公式サイトのダウンロードページ（[vrchat.com/home/download](https://vrchat.com/home/download)）から VCC を入れる
2. 初めて起動すると、Unity（Unity Hub と、VRChat が指定するバージョンの Unity エディター）が入っているかを確かめ、無ければインストールを案内してくれる
3. Unity Hub で Unity のライセンスを有効にする（個人なら無料の Personal で構いません）

::: warning
Unity は、**VRChat が指定するバージョン** を使います。2026 年 10 月時点の指定は **Unity 2022.3.22f1** です。これより新しいバージョン（Unity 6 など）でアップロードすると、VRChat で読み込めなくなります。指定は変わることがあるので、[VRChat の公式ページ（Current Unity Version）](https://creators.vrchat.com/sdk/current-unity-version/) で確かめてください（[TIPS: Unity のバージョンが違うと言われる](/tips#unity-version)）。
:::

## アバター用のプロジェクトを作る

1. VCC の左の **New** を押す
2. テンプレートの **Avatar** を選ぶ
3. プロジェクトの名前と保存先を決めて作る
4. 一覧に出たプロジェクトを開く（**Open Project**）と、Unity が起動する

プロジェクトの名前は英数字にしておくと、ツールのトラブルが少なくなります。保存先も、日本語や空白を含まないフォルダが安全です。

::: tip
プロジェクトは **アバターごと** に分けるのがおすすめです。1 つのプロジェクトにたくさんのアバターや衣装を入れると、読み込みが遅くなり、ツールのバージョンの食い違いも起きやすくなります（[TIPS: Unity プロジェクトの分け方](/tips#unity-project-organization)）。
:::

### 既にあるプロジェクトを一覧に入れる

別の PC から持ってきたプロジェクトや、バックアップから戻したプロジェクトは、左の **Add** でフォルダを選ぶと一覧に入ります。

## パッケージを追加する

VCC の **Projects** の一覧で、プロジェクトの **Manage Project** を押すと、そのプロジェクトに入っているパッケージと、追加できるパッケージが一覧で出ます。**+** のアイコンで最新版が入り、バージョンの欄で別のバージョンを選べます。新しいバージョンがあれば **Latest Version** で更新できます。ボタンの形や並びは VCC のバージョンで変わるので、見つからないときは [VCC の公式ドキュメント](https://vcc.docs.vrchat.com/) を見てください。

最初から入っている公式のパッケージは次のとおりです。

| パッケージ | 中身 |
|---|---|
| VRChat SDK - Base | 共通の部分。Avatars と一緒に入る |
| VRChat SDK - Avatars | アバターのアップロードに使う SDK |

### コミュニティのパッケージ（Modular Avatar など）

Modular Avatar や Avatar Optimizer のような、VRChat 以外の人が作ったパッケージは、先に **リポジトリ**（配布元の一覧）を VCC に登録してから追加します。

- 配布元のサイトに **Add to VCC** のボタンがあれば、押すだけで登録できる
- 手で登録するときは、VCC の **Settings > Packages > Add Repository** に配布元が案内している URL を入れて **Add**。内容を確かめる画面が出るので、確かめてから登録する

| ツール | 用途 | 配布元 |
|---|---|---|
| Modular Avatar | 衣装の着せ替え、メニューの追加 | [modular-avatar.nadena.dev](https://modular-avatar.nadena.dev/ja/) |
| Avatar Optimizer | アップロード時の軽量化 | [vpm.anatawa12.com/avatar-optimizer](https://vpm.anatawa12.com/avatar-optimizer/ja/) |
| lilToon | トゥーンシェーダー | 配布元の案内に従う（unitypackage でも入る） |

登録すると、プロジェクトのパッケージの一覧に出てくるので、そこから追加します。

::: warning
リポジトリを登録すると、そこから配られるものを信用してプロジェクトに入れることになります。登録するのは、配布元がはっきりしているものだけにしてください。
:::

## アバターを入れる

プロジェクトが開いたら、アバターを入れます。

1. アバターの説明書にある **シェーダー**（lilToon など）を先に入れる
2. アバターの **.unitypackage** を Project ウィンドウにドラッグするか、**Assets > Import Package > Custom Package** で読み込む
3. Project のアバターのフォルダにある **Prefab**（青い箱のアイコン）を Hierarchy にドラッグする

シェーダーを入れ忘れると、アバターがピンク色になります（[TIPS: マテリアルがピンクになる](/tips#pink-material)）。FBX（モデルファイル）ではなく Prefab を置くのがポイントです。Prefab には VRC Avatar Descriptor や表情・揺れものの設定が入っています。

ここまでできたら、[アバターをアップロードする](/learn/vrchat/upload) に進みます。

## パッケージを更新するとき

- 更新の前にバックアップを取る。VCC の **Make Backup** で、プロジェクトを zip にして保存できる（保存先は VCC の Settings で変えられる。戻すときは zip を展開して **Add** で一覧に入れる）。Unity を閉じてからフォルダをコピーしてもよい
- Modular Avatar などは、VRChat SDK の新しいバージョンに合わせて更新が出ることがある。片方だけ古いとエラーになるので、まとめて更新する
- 更新後に Console に赤いエラーが出たら、Unity を再起動してから読む（[Q&A: ツールがメニューに出てこない](/qa#unity-tool-not-in-menu)）

## 古いプロジェクトを移す

VCC を使う前の、SDK を unitypackage で入れていたプロジェクトは、VCC の一覧に追加すると **Migrate**（移行）を案内されます。**Migrate a copy** を選ぶと、元のプロジェクトを残したまま、移行したコピーが作られます。元のプロジェクトを直接書き換える **Migrate in place** は、バックアップがあるときだけにしてください。
