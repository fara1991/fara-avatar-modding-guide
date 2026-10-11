---
title: アバターを軽くする（Avatar Optimizer）
description: パフォーマンスランクとファイルサイズを改善する手順です。使わないものを外す、テクスチャを小さくする、PhysBone をまとめる、Avatar Optimizer（AAO）でアップロード時に自動で軽くする、軽い版を用意する。
difficulty: 中級
updated: 2026-10-11
---

## 進め方

1. Builder で **どの項目が悪いか** を見る（[パフォーマンスランク](/learn/vrchat/performance-rank)）
2. 効果の大きいものから減らす
3. Build & Test で、見た目と動きが変わっていないか確かめる

効果の大きい順の目安は次のとおりです（[Q&A: アバターを軽くするには](/qa#avatar-lightweight)）。

| 順 | やること | 効く項目 |
|---|---|---|
| 1 | 着替えの衣装を減らす | 三角形・マテリアル・メッシュ・ボーン・PhysBone のすべて |
| 2 | 見えない部分（服の下の体、使わない小物）を消す | 三角形・メッシュ |
| 3 | テクスチャを小さくする | テクスチャメモリ・ファイルサイズ |
| 4 | PhysBone をまとめる・減らす | PhysBone |
| 5 | メッシュとマテリアルをまとめる（AAO） | Skinned Mesh・マテリアルスロット |

## 1. 使わないものを外す

- 使わない衣装・小物は、無効にするだけでなく、Tag を **EditorOnly** にするか削除する。無効にしただけのものもアップロードに含まれ、ランクの計算にも入る（[TIPS](/tips#editor-only-tag)）
- 作業用に置いておきたいものは、アバターの外（Hierarchy のいちばん上の階層）に出すか、EditorOnly にする
- 衣装をたくさん切り替えるアバターは、衣装ごとに別のアバターとしてアップロードする方が軽くなることも多い

## 2. テクスチャを小さくする

テクスチャはファイルサイズとテクスチャメモリにいちばん効きます。

1. Project ウィンドウでテクスチャを選ぶ
2. Inspector の **Max Size** を下げる（4096 → 2048、小物や目立たない部分は 1024 以下）
3. **Compression** を圧縮ありにして **Apply**

顔や目など目立つ部分は、下げすぎるとぼやけるので、Build & Test で見て決めます（[TIPS: ダウンロードサイズが大きい](/tips#texture-too-large)）。

## 3. PhysBone をまとめる

髪の束ごとに PhysBone が付いていると、コンポーネント数がすぐ増えます。

- 前髪・横髪・後ろ髪ごとに空のオブジェクトを作り、髪束のルートボーンをその子にして、空のオブジェクトに PhysBone を 1 つ付ける（[TIPS: 髪の PhysBone をまとめる](/tips#physbone-merge-components)）
- 揺らさなくてよい髪の毛先は、ボーンごとまとめる（[何て名前？](/howto#reduce-bones)）
- 見えない部分（服の下など）の揺れものは外す

PhysBone の項目の意味は [PhysBone と Contact](/learn/vrchat-features/physbone-contact) にあります。

## 4. Avatar Optimizer（AAO）で自動で軽くする

**Avatar Optimizer**（AAO）は、アップロードのとき（ビルドの途中）に、アバターを自動で軽くするツールです。元のシーンやデータは変わらないので、外せばいつでも元に戻ります。Modular Avatar と同じ NDMF という仕組みで動きます（[用語集: NDMF](/glossary#ndmf)）。

### 入れる

VCC に AAO のリポジトリ（`https://vpm.anatawa12.com/vpm.json`）を登録し、プロジェクトに **Avatar Optimizer** を追加します。公式サイトの [vpm.anatawa12.com/avatar-optimizer](https://vpm.anatawa12.com/avatar-optimizer/ja/) に追加用のリンクがあります（[VCC でプロジェクトを作る](/learn/vrchat/vcc-project)）。

### Trace And Optimize（まずこれだけ）

アバターのルートを選び、**Add Component > Avatar Optimizer > AAO Trace And Optimize** を付けます。これだけで、アップロードのときに次のことを自動で行います。

- 使われていないオブジェクト・ボーン・コンポーネントを消す
- 動かさないブレンドシェイプ（シェイプキー）を固定・削除する
- PhysBone を整理する（使われていないコライダーの統合など）
- アニメーター（FX など）を最適化する
- まとめられる Skinned Mesh を結合する
- 対応しているシェーダーのテクスチャを整理する

見た目が変わらないように作られているので、付けたら Build & Test で確かめるだけです。見た目が変わったときは、Trace And Optimize の設定で該当する項目をオフにして、どの最適化が原因か切り分けます。

### もっと減らしたいとき

| コンポーネント | 使いどころ |
|---|---|
| **AAO Merge Skinned Mesh** | 複数のメッシュを 1 つにまとめる。多くは Trace And Optimize で足りる。同じマテリアルのスロットをまとめると、マテリアルスロットも減る（その代わり、まとめたものを個別に ON/OFF できなくなる） |
| **AAO Remove Mesh By BlendShape** | 体の「貫通防止（体を縮める）」シェイプキーで動く部分のメッシュを、アップロード時に消す。服の下の体を Blender を使わずに消せる |
| **AAO Remove Mesh By Box** | 箱で囲んだ範囲のメッシュを消す |
| **AAO Freeze BlendShapes** | 値を変えないシェイプキーを固定して消す。多くは Trace And Optimize で足りる |
| **AAO Merge PhysBone** | 複数の PhysBone を 1 つにまとめる |

それぞれ Add Component の **Avatar Optimizer** の中にあります。使い方の詳細は [AAO の公式ドキュメント](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/) を見てください。

::: tip
AAO で何が消えたかは、アップロード前に確かめられます。アバターを右クリックして **NDM Framework > Manual bake avatar** を実行すると、処理したあとのアバターの複製がシーンに作られます。確かめたら、その複製は削除します。
:::

### メッシュ結合と表情

自分でメッシュを結合すると、表情のアニメーションが「メッシュ名 / シェイプキー名」で参照しているため、表情が動かなくなることがあります（[Q&A](/qa#merged-mesh-expression-broken)）。AAO の結合は参照を自動で付け替えるので、この問題は起きにくくなっています。

## 5. 軽い版を用意する

衣装の切り替えや凝ったギミックを全部入れたアバターは、どうしても重くなります。

- 普段用（全部入り）と、人の多い場所用（衣装 1 着、ギミック少なめ）を別のアバターとしてアップロードしておく
- 軽い版は、普段用のアバターを複製して、使わないものを EditorOnly にして作る。**複製したら Blueprint ID を Detach** して、別のアバターとしてアップロードする（[TIPS](/tips#overwrote-avatar)）
- Android 版も同じ考え方で作る（[Android（Quest）対応](/learn/vrchat/android)）

## Blender でできること

Unity で減らしきれないときは、Blender でメッシュ自体を軽くします。

- 見えない部分の頂点を消す（シェイプキーがあるメッシュは頂点を消すと壊れることがあるので、AAO の Remove Mesh By BlendShape の方が安全なこともある）
- ポリゴンを減らす（[何て名前？: ポリゴン数を減らしたい](/howto#decimate)、[トポロジーの整理](/learn/modeling/topology)）
- 揺らさない髪のボーンをまとめる（[何て名前？: 揺らさない髪のボーンを減らしたい](/howto#reduce-bones)）
