---
title: VRM に書き出す
description: 着せ替えたアバターを、配信や VRM 対応のゲームで使う VRM にする流れです。正規化、表情（BlendShape）、揺れもの（SpringBone）、一人称の設定、MToon への置き換え、容量の減らし方。
difficulty: 上級
updated: 2026-10-11
---

## VRM とは

**VRM** は、人型のアバターを 1 つのファイルにまとめる形式です。VRChat では使いませんが、配信用のアプリや、VRM を読み込めるゲームでアバターを使うときに必要になります。Unity では **UniVRM** というパッケージで書き出します。

VRChat 用のアバターとは、使う部品が違います。

| 役割 | VRChat | VRM（0.x） |
|---|---|---|
| アバターの設定 | VRC Avatar Descriptor | VRM Meta などのコンポーネント |
| 揺れもの | PhysBone | SpringBone |
| 表情 | FX レイヤーのアニメーション | BlendShape Proxy（クリップにシェイプキーを登録） |
| シェーダー | lilToon など自由 | MToon（それ以外は Standard 扱い） |
| 一人称で隠すもの | （自動で頭を隠す） | First Person の設定 |

VRM には 0.x と 1.0 の 2 つの版があり、UniVRM はどちらも書き出せます（0.x は **VRM0 > Export VRM 0.x...**、1.0 は **Export VRM 1.0...** のメニュー）。読み込むアプリがどちらに対応しているかを先に確かめてください。1.0 では、表情（0.x の BlendShape）が **Expression** という名前に変わるなど、設定の場所が違います。この記事は、改変の相談でよく出た 0.x の流れを中心に書いています。メニューの場所や項目名は UniVRM のバージョンで変わるので、[UniVRM の説明書](https://vrm.dev/) と合わせて読んでください。

::: warning
アバターや衣装の規約で、VRM への変換や、VRChat 以外での利用（配信、ゲーム）が認められているか確認してください。VRM の中には作者情報や利用条件を書く欄（Meta）があるので、規約に合わせて設定します。
:::

## 流れ（正規化 → 設定 → もう一度書き出す）

1. Unity でアバターに衣装を着せる
2. **1 回目の書き出し（正規化）**: 書き出しの画面で **Pose Freeze**（正規化）をオンにして書き出すと、ポーズや回転がそろった（正規化された）VRM ができる
3. できた VRM を読み込み、表情（BlendShape Proxy）、揺れもの（SpringBone）、一人称（First Person）を設定する
4. **2 回目の書き出し** で完成

正規化のあとでモデルを直すのは難しいので、**正規化の前** に着せ替えを済ませておきます（[Q&A: 着せ替えたアバターを VRM にする流れ](/qa#vrm-export-flow)）。

## 1. 正規化の前に確かめること

- **着せ替えのボーンの入れ方** が正しい。Modular Avatar の Merge Armature はアップロード（ビルド）のときに統合する仕組みなので、そのままでは VRM の書き出しに反映されません。衣装のボーンをアバターのボーンの子に入れる、Blender で統合する、など VRM の書き出しで使える形にしておきます（[Q&A](/qa#vrm-normalize-pitfalls)）。アバターを右クリックして **Modular Avatar > Manual bake avatar** を実行すると、Modular Avatar の処理を済ませたアバターの複製が作られるので、それを元にする方法もあります（VRM 向けの使い方は公式には書かれていないので、結果を確かめてください）
- **使わないボーン・オブジェクトは削除せず非表示**: Unity でボーンを削除すると「必要なボーンがない」エラーになることがあります。正規化の前は非表示にしておくと、正規化で消えます（[Q&A](/qa#remove-unused-bones-after-dressing)）
- **ヒールのある靴**: 正規化後に Armature の高さを上げて、ヒールが床に付くようにします（[Q&A](/qa#heel-shoes-fitting)）

::: tip
髪型や下着など、どの衣装でも使う最低限の状態で一度正規化し、表情と揺れものを設定した「VRM 用の素体」を作っておくと、以降は衣装と衣装の揺れものを足すだけで済みます。表情の設定（BlendShapeAvatar）は、同じアバターなら使い回せます。
:::

## 2. 表情（BlendShape Proxy）

VRM の表情は、VRM Blend Shape Proxy の **BlendShapeAvatar** で、Blink（まばたき）や A・I・U・E・O（口）などのクリップごとに、顔のメッシュのシェイプキーを登録して作ります（[Q&A: VRM で表情が動かない](/qa#vrm-blendshape-setup)）。

- まばたきが動かないときは、Blink のクリップにシェイプキーが登録されているかを確かめる
- フェイストラッキング用の細かいシェイプキー（パーフェクトシンクなど）は、名前を大文字小文字まで規格どおりにする（[TIPS](/tips#perfect-sync-tips)）

## 3. 揺れもの（SpringBone）

VRChat の PhysBone は VRM では使えないので、SpringBone を設定し直します。

- 揺らすボーンの根元を Root Bones に入れ、コライダーを設定する
- 空欄（null）が残っていると、書き出せても他のアプリで読み込めないことがある（[Q&A](/qa#vrm-load-error-null)）
- ゲーム内で強く移動させられるとスカートが破綻するときは、Center の設定を試す（[TIPS](/tips#vrm-springbone-center)）
- SpringBone には PhysBone の Inside コライダーがないので、細い紐やスカートの貫通は、大きめのコライダー 2 つで V 字を作る方法が使える（[TIPS](/tips#collider-v-shape)）

## 4. 一人称（First Person）

自分の視点で、自分の顔や髪が視界をふさがないように、メッシュごとに表示を決めます（[Q&A: 自分の体が見えない](/qa#vrm-firstperson-settings)）。

| メッシュ | 設定の例 |
|---|---|
| 体・手 | Both（どちらでも表示） |
| 顔・髪・帽子 | ThirdPersonOnly（一人称では隠す） |

設定は VRM First Person の Renderers で、メッシュごとに **Auto / Both / ThirdPersonOnly / FirstPersonOnly** から選びます。**Auto**（頭のボーンとの関係で自動で分ける）はアプリによって負荷が大きく増えることがあるので、手で設定するのがおすすめです。

## 5. シェーダーを MToon にする

VRM 0.x は **MToon** シェーダーが前提で、lilToon などのままだと Standard として書き出され、金属のような見た目になります（[Q&A: MToon への変換](/qa#vrm-mtoon-conversion)）。

- lilToon には、近い見た目の MToon マテリアルを作る変換の機能があります。色調補正をしている場合は、先にメインカラーに焼き込んでから変換します
- MatCap のマスクなど、MToon にない機能は再現されません
- 白っぽくなるときは、リムライトとエミッションを黒くすると改善します（[Q&A](/qa#rim-light-glow)）

## 6. 容量と負荷を減らす

| やること | 効果 | 関連 |
|---|---|---|
| 書き出しの設定の **ReduceBlendshape** で、表情に使っていない BlendShape を書き出さない（0.x） | 容量が大きく減る | [TIPS](/tips#vrm-file-size) |
| 顔のメッシュをほかと結合しない | シェイプキーがほかのメッシュに複製されて巨大になるのを防ぐ | [TIPS](/tips#vrm-file-size) |
| テクスチャを 2048 → 1024 にする | 容量が減る | [TIPS](/tips#texture-too-large) |
| 揺れものとボーンを減らす | 負荷が減る | [TIPS](/tips#vrm-file-size) |
| First Person を手で設定する | 負荷が減る | 上の 4. |

## VRChat 用のアバターから変換するツール

VRChat 用に設定したアバター（PhysBone や表情の設定）を読み替えて VRM にするツールもあります（[Q&A](/qa#vrm-converter-merge-option)）。手早く作れますが、メッシュを 1 つに結合する設定になっていることや、コライダーの互換性がない部分があるので、結果は必ず確かめてください。
