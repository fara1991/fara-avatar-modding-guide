---
title: Modular Avatar で衣装を着せる
reading: もじゅらーあばたーできしょうをきせる
description: 衣装をアバターの中に入れて Setup Outfit を実行し、元の服を外し、マテリアルとブレンドシェイプを整えて Build & Test で確かめるまでの流れです。
difficulty: 初級
updated: 2026-10-11
---

## 流れ

対応アバター向けに配られている衣装（Prefab）を、**Modular Avatar**（MA）で着せる基本の流れです。

1. 衣装の Prefab を、Hierarchy のアバターの **中（子）** に入れる
2. 衣装を右クリック > **Modular Avatar > Setup Outfit**
3. アバターが最初から着ている服を外す（無効にして、Tag を **EditorOnly** に）
4. 必要ならマテリアルを差し替える
5. 体形のブレンドシェイプに服を合わせる（**MA Blendshape Sync**）
6. VRChat SDK の **Build & Test** で、動かして確かめる

下の練習用の画面で、この流れを 1 つずつ試せます。本物の Unity ではなく、人形・衣装・SDK の画面は練習用のシミュレーションです。

::: practice ma-outfit
:::

## 1. 衣装をアバターの中に入れる

Project ウィンドウの衣装の Prefab を、Hierarchy のアバターの **上** にドラッグして離します。アバターの下に字下げされて入れば成功です。いちばん外（ルート）に置いてしまったら、Hierarchy で衣装をアバターの上にドラッグし直します。

::: tip
衣装の説明に「対応アバター」が書いてあります。対応していないアバターに着せると、ボーンの位置や名前が合わず、きれいに着られません。
:::

## 2. Setup Outfit

衣装（Prefab のいちばん上）を右クリックして **Modular Avatar > Setup Outfit** を選びます。衣装の `Armature` に **MA Merge Armature**、衣装のいちばん上に **MA Outfit Root** と **MA Mesh Settings** が付きます。アップロードするとき（ビルド時）に、衣装のボーンが同じ名前のアバターのボーンに統合されます。元のシーンは変わらないので、あとから外すのも簡単です。

服がついてこないときは [TIPS: Modular Avatar で服が体についてこない](/tips#ma-bone-mismatch) を見てください。

## 3. 元の服を外す

アバターの中の元の服のメッシュを選び、Inspector の左上のチェックを外して **無効** にします。さらに **Tag** を **EditorOnly** にすると、アップロードに含まれなくなります。

::: warning
無効にしただけのメッシュも、アップロードには含まれ、パフォーマンスランクの計算（ポリゴン数など）にも入ります。使わない服は EditorOnly にするか、削除してください。
:::

## 4. マテリアル

色違いなどのマテリアルが付いている衣装は、Project のマテリアルを Inspector の **Skinned Mesh Renderer > Materials** の欄にドラッグして差し替えます。マテリアルがピンクになるときは、シェーダー（lilToon など）が入っていません（[マテリアルとシェーダー](/learn/unity/liltoon-basics)）。

## 5. 体形に服を合わせる

アバター本体にブレンドシェイプ（胸の大きさなど）があり、衣装にも同じ名前のブレンドシェイプがあるときは、衣装のメッシュに **Add Component > Modular Avatar > MA Blendshape Sync** を付けて、アバターの値に合わせます。

## 6. Build & Test で確かめる

メニューの **VRChat SDK > Show Control Panel** で Builder を開き、**Build & Test** で自分の PC の VRChat だけに読み込んで、動かして確かめます。問題なければ **Build & Publish** でアップロードします（[アップロード前の確認](/learn/unity/pre-upload-check)）。
