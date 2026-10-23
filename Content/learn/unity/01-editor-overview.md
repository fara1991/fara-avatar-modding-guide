---
title: Unity の画面の見方
reading: ゆにてぃーのがめんのみかた
description: アバターの作業で使う Unity のウィンドウ（Hierarchy・Inspector・Project・Scene）と、Scene ビューの視点の動かし方です。
difficulty: 初級
updated: 2026-10-11
---

## アバターの作業で使うウィンドウ

Unity の画面はいくつものウィンドウに分かれています。アバターの作業でよく使うのは次の 5 つです。

| ウィンドウ | 何をするところか |
|---|---|
| **Scene** | シーンを見ながら編集する 3D ビュー。アバターや衣装の位置を確かめる |
| **Hierarchy** | シーンにあるもの（GameObject）を親子の木で表示する。衣装をアバターの中に入れるのもここ |
| **Inspector** | 選んだものの設定（コンポーネント）を表示・編集する。マテリアルや Modular Avatar の設定もここ |
| **Project** | プロジェクトのファイル（Prefab・FBX・マテリアル・テクスチャ）。ここから Hierarchy や Scene にドラッグして使う |
| **Console** | エラーや警告の一覧。赤いエラーが出ていたら、まずここを読む |

**Game** ビューはゲームを実行したときの画面です。アバターの作業ではあまり使いません（確かめるのは VRChat SDK の Build & Test で行います）。

下の練習用の画面で、ウィンドウの役割と視点の動かし方を試せます（本物の Unity ではなく、ブラウザで動く練習用の画面です）。

::: practice unity-basics
:::

## Scene ビューの操作

| 操作 | 動き | Blender では |
|---|---|---|
| <kbd>Alt</kbd>+左ドラッグ | 視点を回す | ホイールボタンでドラッグ |
| ホイールボタンでドラッグ | 視点を平行移動 | <kbd>Shift</kbd>+ホイールボタン |
| ホイール | ズーム | ホイール |
| 右ドラッグ（+ <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd>） | その場で見回す・飛ぶように移動 | （なし） |
| <kbd>F</kbd> | 選んだものを画面の中心に表示 | <kbd>Numpad .</kbd> |

Hierarchy でオブジェクトを **ダブルクリック** しても、<kbd>F</kbd> と同じく画面の中心に表示されます。アバターを見失ったときに便利です。

## ツール（Q / W / E / R）

| キー | ツール | Blender では |
|---|---|---|
| <kbd>Q</kbd> | 手のツール（左ドラッグで平行移動） | — |
| <kbd>W</kbd> | 移動（赤・緑・青の矢印） | <kbd>G</kbd> |
| <kbd>E</kbd> | 回転（輪） | <kbd>R</kbd> |
| <kbd>R</kbd> | 拡大縮小（先が四角の矢印） | <kbd>S</kbd> |

Blender と同じ文字でも意味が違うので注意してください（Unity の <kbd>R</kbd> は拡大縮小、Blender の <kbd>R</kbd> は回転）。

::: warning
Unity は **Y が上**、Blender は **Z が上** です。Blender で作ったものを入れると、Rotation の X に -90 などの値が入って見えることがあります。書き出し前の [トランスフォームの適用](/learn/basics/transform) で多くは防げます（[TIPS](/tips#fbx-rotation) も参照）。
:::

## Hierarchy と Prefab

- 名前が **青い** ものは **Prefab**（部品として保存されたもの）から置かれたものです。アバターや衣装は Prefab で配られることがほとんどです
- 名前の左の ▶ で中身を開きます。アバターの中には `Armature`（ボーン）と、体や服のメッシュが入っています
- ドラッグして別のオブジェクトの上で離すと、その **子** になります。衣装をアバターの子にするのが、Modular Avatar で着せる最初の手順です（[次の記事](/learn/unity/modular-avatar-outfit)）

## Inspector の見方

- いちばん上のチェックで **有効 / 無効**、その横が **名前**、下に **Tag**
- その下に **コンポーネント** が並びます。どの GameObject にも `Transform`（位置・回転・スケール）があり、メッシュなら `Skinned Mesh Renderer`、アバターのルートには `VRC Avatar Descriptor` が付いています
- **Add Component** から、Modular Avatar や VRChat のコンポーネントを追加します。名前の一部を入力すると絞り込めます
