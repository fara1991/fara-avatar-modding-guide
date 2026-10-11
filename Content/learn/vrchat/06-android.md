---
title: Android（Quest）対応
description: Meta Quest やスマートフォンの VRChat でもアバターを見てもらうための、Android 版のアップロードです。プラットフォームの切り替え、使えるシェーダーとコンポーネント、PC 版と同じアバターにする方法、軽くする目安。
difficulty: 中級
updated: 2026-10-11
---

## Android 版が要る理由

Meta Quest（単体で動かすとき）や Android のスマートフォンの VRChat は、PC とは別の **Android 版** のアバターを読み込みます。PC 用にだけアップロードしたアバターは、Android の人からは代わりの姿（インポスターやフォールバック）でしか見えません。

Android 版を出すには、同じアバターを Android 向けにビルドして、もう一度アップロードします。PC 版と同じ **Blueprint ID** でアップロードすれば、1 つのアバターとして PC と Android の両方に対応します。

::: warning
アバターや衣装によっては、規約や作者の案内で Android 版の作り方（Quest 対応版のデータ）が決まっていることがあります。Quest 対応をうたっているアバターは、作者の Android 用 Prefab を使うのがいちばん確実です。
:::

## 準備: Android Build Support

Unity に **Android Build Support** のモジュールが入っている必要があります。VCC で Unity を入れた場合は、最初から入っています。Unity Hub の **Installs** で、使っている Unity の下に Android があるか確かめ、無ければ Unity Hub から追加します。

## 1. Android 用に軽くする

Android の上限はとても厳しく、PC 用のアバターをそのままビルドすると、ほぼ確実に Very Poor になります。Android では **Very Poor のアバターは表示されません**。目安は次のとおりです（値は [パフォーマンスランク](/learn/vrchat/performance-rank) の表と、公式ページを見てください）。

| 項目 | Medium に収める目安 | Poor の上限 |
|---|---|---|
| 三角形 | 15,000 | 20,000 |
| マテリアルスロット | 2 | 4 |
| Skinned Mesh | 2 | 2 |
| ボーン | 150 | 150 |
| PhysBone のコンポーネント | 6 | 8 |
| テクスチャメモリ | 25 MB | 40 MB |
| ダウンロードサイズ | 10 MB まで（これを超えるとアップロードできない） | |

減らし方:

- 衣装は 1 着だけにする。着替えのギミックは Android 版では外す
- 見えない体のメッシュを消し、Blender でポリゴンを減らす
- テクスチャを 1 枚にまとめ、マテリアルを 1〜2 個にする（Avatar Optimizer のメッシュとマテリアルの結合も使える。[アバターを軽くする](/learn/vrchat/optimization)）
- テクスチャの Max Size を 1024 以下にする

## 2. シェーダーを Android 用にする

Android では、lilToon などの PC 用のシェーダーは使えません。**VRChat/Mobile/** の中のシェーダーだけが使えます。

| シェーダー | 向いているもの |
|---|---|
| VRChat/Mobile/Toon Standard | トゥーン調のアバター（影や MatCap なども少し使える） |
| VRChat/Mobile/Toon Lit | 影なしのシンプルなトゥーン |
| VRChat/Mobile/Standard Lite | 写実寄りのもの |
| VRChat/Mobile/Matcap Lit | MatCap で質感を出すもの |

Android 版のマテリアルは、PC 版のマテリアルを **複製** して Android 用のシェーダーに変えます。PC 版のマテリアルをそのまま書き換えると、PC 版の見た目も変わってしまいます。透過（半透明）は使えないものが多いので、透ける部分はテクスチャで描くか、メッシュを消します。

## 3. 使えないコンポーネント

Android のアバターでは、次のものは動きません（アップロードすると外されるか、無視されます）。

- Cloth、ライト、カメラ、Audio Source
- Unity の物理（Rigidbody、Collider、Joint）
- Unity の Constraint（VRChat の **VRC Constraint** に変換すれば使える）
- Dynamic Bone（PhysBone を使う）
- パーティクルは大きく制限される

PhysBone・Contact・Constraint は使えますが、数の上限（Very Poor の値）を超えると、それらがすべて外されます。

## 4. プラットフォームを切り替えてビルドする

1. **VRChat SDK > Show Control Panel** の **Builder** タブを開く
2. ビルドのプラットフォームの選択で **Android** を選ぶ（**Windows** の選択を外す）。切り替えを確かめる画面が出たら **Switch**
3. 初めての切り替えは、アセットの読み込み直しに時間がかかる（大きなプロジェクトでは数十分）
4. Builder のエラー・警告とランクを確かめ、**Build & Publish**

SDK のバージョンによっては、Windows と Android を両方選んで、続けてビルド・アップロードできます。

::: tip
Android への切り替えは時間がかかるうえ、PC 版の作業に戻るたびにまた切り替えが要ります。Android 版の作業が多いときは、PC 版のプロジェクトをコピーして、Android 用のプロジェクトを分けるのも手です。
:::

## 5. PC 版と同じアバターにする

PC 版と Android 版を 1 つのアバターにするには、**同じ Blueprint ID** でアップロードします。

1. PC 版の Blueprint ID を、PC 版のアバターの **Pipeline Manager**（または **Content Manager** タブ）で確かめてコピーする
2. Android 版のアバター（別の Prefab を使っているなら、そのルート）の Pipeline Manager に、その ID を入れる（**Detach** してから貼り付けて **Attach**）
3. Android でビルドして **Build & Publish**

同じアバターとして扱われるには、**頭・手・足までのボーンの並び（パス）** と、ルートのボーンのスケール・回転が PC 版と同じである必要があります。髪やスカートなどの補助ボーンは、Android 版で減らしても構いません。

## Android 実機で試す

SDK のバージョンによっては、USB でつないだ Quest やスマートフォンに、Build & Test で直接テストのビルドを送れます。Quest は開発者モードと USB デバッグを有効にする必要があります。手順は公式の [Build & Test for Mobile](https://creators.vrchat.com/platforms/android/build-test-mobile/) を見てください。

## Android 版を出さない場合

Android 版が無いアバターは、Android の人からは **インポスター**（VRChat が自動で作る簡易な代わりの姿）で見えることがあります。インポスターは、VRChat のサイトのアバターのページから作れます（人型のアバターのみ）。詳しくは公式の [Avatar Impostors](https://creators.vrchat.com/avatars/avatar-impostors/) を見てください。
