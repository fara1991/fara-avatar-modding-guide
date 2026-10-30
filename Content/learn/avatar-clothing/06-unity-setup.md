---
title: Unity で着せる（Modular Avatar）
reading: ゆにてぃーできせるもじゅらーあばたー
description: 書き出した服を Unity に取り込み、Modular Avatar でアバターに統合して VRChat にアップロードするまでです。
difficulty: 中級
updated: 2026-10-10
---

## 使うもの

| もの | 入手先 |
|---|---|
| Unity（VRChat が指定するバージョン） | VRChat Creator Companion（VCC）または ALCOM から |
| VRChat SDK（Avatars） | VCC / ALCOM でプロジェクトに追加 |
| **Modular Avatar** | VCC / ALCOM の「Packages」で追加（リポジトリは [modular-avatar.nadena.dev](https://modular-avatar.nadena.dev/ja) の手順で登録） |
| lilToon などのシェーダー | 任意。アバター付属のものと同じにするときれい |

Modular Avatar は、服のアーマチュアをアバターのアーマチュアに **自動で統合** するツールです。手動でボーンを 1 本ずつ付け替える必要がなくなり、服の着脱も簡単になります。

この記事には Unity の画面写真がありません。項目名で探してください。

::: tip
衣装を入れる → Setup Outfit → 元の服を外す → マテリアル → Blendshape Sync → Build & Test の流れは、[Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) の練習用の画面で 1 つずつ試せます。
:::

## 1. 服を取り込む

1. Unity で、アバターがセットアップ済みのプロジェクトを開く
2. `Project` ウィンドウの `Assets` の下に `Clothing/Shirt` フォルダを作り、`Shirt.fbx` と `Textures` フォルダをドラッグして入れる
3. `Shirt.fbx` を選んで `Inspector` の **Rig** タブを開き、**Animation Type** を **Humanoid** ではなく **Generic** にして **Apply**

::: tip
服は Humanoid にしません。Humanoid にすると Unity がボーンを人型として解釈し、アバター側の設定と干渉することがあります。Modular Avatar はボーン名で統合するので Generic で十分です。
:::

## 2. マテリアルを作る

1. `Assets/Clothing/Shirt` で右クリック > **Create > Material**、名前を `Shirt`
2. `Inspector` で **Shader** をアバターと同じもの（lilToon など）に変える
3. **Main Color / Texture**（シェーダーによって名前が違う）に `Shirt_Color.png` をドラッグ

FBX のマテリアルは、既定では FBX の中に埋め込まれたものが使われます。作ったマテリアルを使うには、`Shirt.fbx` の `Inspector` の **Materials** タブで、**Remapped Materials** の該当欄に `Shirt` を入れて **Apply** します。

## 3. アバターに入れる

1. `Hierarchy` にあるアバターのオブジェクトの下に、`Project` から `Shirt.fbx` をドラッグして入れる（アバターの直下）
2. 位置・回転・スケールが 0 / 0 / 1 になっていることを確認する。ずれていたら Blender 側でトランスフォームの適用が漏れている

この時点では、服のアーマチュアとアバターのアーマチュアは別々です。

## 4. Setup Outfit で統合する

1. `Hierarchy` で `Shirt` を右クリック
2. **Modular Avatar > Setup Outfit** を選ぶ

これだけです。服のアーマチュアに **MA Merge Armature** コンポーネントが付き、ボーン名を照合してアバターのアーマチュアに対応付けられます。服のいちばん上には **MA Outfit Root** と **MA Mesh Settings**（ライトの基準と表示範囲をアバターに合わせる）も付きます。

- 再生ボタンを押して **Play Mode** に入ると、実際に統合された状態になる。アバターを動かす（`Hierarchy` でアバターのボーンを回す）と服がついてくるか確認する
- Play Mode を抜けると元の状態に戻る。アップロード時に自動で統合されるので、**自分でボーンを付け替える必要はない**

::: warning
Setup Outfit が「衣装の Hips ボーンを発見できませんでした」と言う場合は、服のアーマチュアの中に、アバターの Hips に対応するボーンが見つかっていません。Blender でアバターのアーマチュアを一緒に書き出していれば見つかるはずなので、ボーン名と階層を確かめて FBX を書き出し直してください。ボーンの名前の前後に余計な文字が付いているだけなら、MA Merge Armature の **前置詞 / 後置詞**（Prefix / Suffix）で照合できます（統合先を設定すると自動で入ります）。
:::

## 5. 体形シェイプキーを同期する

アバターに体形のシェイプキー（BlendShape）があり、服にも同じ名前のものを作ってある場合は、次で連動させます（[シェイプキー](/learn/rigging/shape-keys)）。

1. `Shirt` のメッシュオブジェクトを選び、`Inspector` の **Add Component > Modular Avatar > MA Blendshape Sync**
2. **+** を押し、一覧からアバターのメッシュとシェイプキーをダブルクリックして追加
3. 服側の名前が同じなら、右の欄は空のままでよい

## 6. 元の服を消す

アバターに最初から入っている服のメッシュは、`Hierarchy` で非アクティブ（チェックを外す）にし、**Tag** を **EditorOnly** にします。非アクティブにしただけのメッシュもアップロードに含まれ、パフォーマンスランクの計算にも入るためです。元のデータを残しておきたいので、ここでは削除しません。

着替えをギミック（メニューでの切り替え）にしたい場合は、Modular Avatar の **Create Toggle**（MA Menu Item と MA Object Toggle）で作れます。この記事では扱いません。

## 7. 確認してアップロード

1. **VRChat SDK > Show Control Panel** を開き、ログインする
2. **Builder** タブで、アバターのパフォーマンスランクを確認する。服を足して **Poor** 以下になっていたら、三角形数・マテリアル数・テクスチャサイズを見直す（[トポロジーの整理](/learn/modeling/topology)、[Unity 向けの注意点](/learn/materials/for-unity)）
3. **Build & Test** でローカルテストし、VRChat で動きを確認する
4. 問題なければ **Build & Publish** でアップロードする

## よくあるつまずき

- **服が大きい / 小さい / 横倒し**: Blender でトランスフォームが未適用です。[FBX に書き出す](/learn/avatar-clothing/export-fbx) のチェックリストを見てください
- **服が真っ白 / ピンク**: マテリアルの割り当てとシェーダーを確認してください。ピンクは「シェーダーが見つからない」です
- **服が動かない**: Play Mode に入っていないか、Setup Outfit がされていません。`Shirt` のアーマチュアに MA Merge Armature が付いているか確認してください
- **関節で体が貫通する**: ウェイトのずれです。Blender で [ウェイトの転送](/learn/avatar-clothing/weight-transfer) をやり直すか、服側を少し膨らませてください
- **ランクが Very Poor になった**: PC では、三角形 70,000・マテリアルスロット 32・ボーン 400 など、Poor の上限を 1 つでも超えると Very Poor です（2026 年 10 月時点の公式の値。変わることがあります）。PC では Very Poor でもアップロードできますが、見る人の設定によっては表示されず、Android では表示されません。アバター全体で超えないように減らします
