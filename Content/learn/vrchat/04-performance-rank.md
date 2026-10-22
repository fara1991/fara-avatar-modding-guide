---
title: パフォーマンスランク
reading: ぱふぉーまんすらんく
description: VRChat のアバターの重さを表すパフォーマンスランクの決まり方、PC と Android（Quest）の上限値、ランクが低いとどうなるか、どの項目から減らすかです。
difficulty: 初級
updated: 2026-10-11
---

## ランクの決まり方

VRChat は、アバターの重さを 5 段階で表します（[用語集: パフォーマンスランク](/glossary#performance-rank)）。

| ランク | 意味 |
|---|---|
| Excellent | とても軽い |
| Good | 軽い |
| Medium | ふつう |
| Poor | 重い |
| Very Poor | とても重い（Poor の上限を 1 つでも超えている） |

ランクは、三角形の数・マテリアルスロットの数・ボーンの数・PhysBone の数などの **項目ごと** に付き、**いちばん悪い項目のランク** がアバター全体のランクになります。三角形が少なくても、マテリアルスロットが 40 あれば Very Poor です。

ランクは VRChat SDK の **Builder** でアバターを選ぶと表示され、項目ごとの値も見られます。VRChat の中でも、アバターの詳細でランクを確かめられます。

## 上限の値

公式の [Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system/) にある値です（2026 年 10 月時点）。値は見直されることがあるので、**目安** として読み、最新の値は公式ページで確かめてください。表の数字は「このランクでいられる上限」で、Poor の列を超えた項目があると Very Poor です。

### PC（主な項目）

| 項目 | Excellent | Good | Medium | Poor |
|---|---|---|---|---|
| 三角形（Triangles） | 32,000 | 70,000 | 70,000 | 70,000 |
| テクスチャメモリ | 40 MB | 75 MB | 110 MB | 150 MB |
| Skinned Mesh | 1 | 2 | 8 | 16 |
| Mesh（ボーンで動かないメッシュ） | 4 | 8 | 16 | 24 |
| マテリアルスロット | 4 | 8 | 16 | 32 |
| ボーン | 75 | 150 | 256 | 400 |
| PhysBone のコンポーネント | 4 | 8 | 16 | 32 |
| PhysBone で動く Transform | 16 | 64 | 128 | 256 |
| PhysBone のコライダー | 4 | 8 | 16 | 32 |
| PhysBone の衝突判定の数 | 32 | 128 | 256 | 512 |
| Contact | 8 | 16 | 24 | 32 |
| Constraint の数 | 100 | 250 | 300 | 350 |
| Animator | 1 | 4 | 16 | 32 |
| パーティクルシステム | 0 | 4 | 8 | 16 |
| ライト | 0 | 0 | 0 | 1 |
| Cloth | 0 | 1 | 1 | 1 |
| Audio Source | 1 | 4 | 8 | 8 |

### Android（Quest・スマートフォン）（主な項目）

| 項目 | Excellent | Good | Medium | Poor |
|---|---|---|---|---|
| 三角形（Triangles） | 7,500 | 10,000 | 15,000 | 20,000 |
| テクスチャメモリ | 10 MB | 18 MB | 25 MB | 40 MB |
| Skinned Mesh | 1 | 1 | 2 | 2 |
| Mesh（ボーンで動かないメッシュ） | 1 | 1 | 2 | 2 |
| マテリアルスロット | 1 | 1 | 2 | 4 |
| ボーン | 75 | 90 | 150 | 150 |
| PhysBone のコンポーネント | 0 | 4 | 6 | 8 |
| PhysBone で動く Transform | 0 | 16 | 32 | 64 |
| PhysBone のコライダー | 0 | 4 | 8 | 16 |
| Contact | 2 | 4 | 8 | 16 |
| Animator | 1 | 1 | 1 | 2 |

ここに無い項目（Constraint の深さ、パーティクルの細かい項目、Bounds の大きさなど）も公式ページにあります。Android では、ライト・Cloth・Audio Source などのコンポーネント自体が使えません（[Android（Quest）対応](/learn/vrchat/android)）。

## ランクが低いとどうなるか

### PC

- 見る人は、VRChat の **Performance Options**（メインメニューの Safety タブの右上）で「このランクより下のアバターは表示しない」を決められる。PC の既定は Very Poor まで表示（実質、ランクでは隠さない）
- 表示しない設定の人には、三角形やマテリアルなどの項目が理由なら代わりのアバター（フォールバック）が、PhysBone などの項目が理由ならそのコンポーネントだけを外した姿が見える
- 人の多いイベントでは、主催者から軽いアバターをお願いされることもある

### Android（Quest・スマートフォン）

- 既定では **Medium より下は表示されない**。見る人が下げられるのは Poor までで、**Very Poor は表示できない**
- 三角形が 20,000 を超えると Very Poor になり、Android の人からは見えない
- PhysBone・Contact・Constraint には、超えるとそれらがすべて外される上限（Very Poor の値）がある

つまり、Android の人にも見てほしいなら、Android 版は **Poor 以内、できれば Medium 以内** に収める必要があります。

## どこから減らすか

Builder で、**いちばん悪い項目** を見て、そこから減らします。1 つの項目だけが Very Poor なら、それを直すだけでランクが大きく上がります。

| 悪い項目 | 減らし方 |
|---|---|
| 三角形 | 服の下で見えない体を消す、使わない衣装を外す、Blender でポリゴンを減らす（[トポロジーの整理](/learn/modeling/topology)） |
| マテリアルスロット | 使わない衣装を外す、同じマテリアルのメッシュをまとめる、テクスチャをまとめる（[マテリアルの注意点](/learn/materials/for-unity)） |
| Skinned Mesh | メッシュを結合する（Avatar Optimizer なら自動） |
| ボーン | 使わない衣装のボーンを外す、揺らさない髪のボーンをまとめる |
| PhysBone | 髪の PhysBone をまとめる、見えない部分の揺れものを外す（[TIPS](/tips#physbone-merge-components)） |
| テクスチャメモリ | テクスチャの Max Size を下げる、圧縮する（[TIPS](/tips#texture-too-large)） |

具体的な手順は [アバターを軽くする](/learn/vrchat/optimization) にまとめています。

::: warning
**無効にしただけ** のオブジェクトも、ランクの計算に入ります。使わない衣装や小物は、Tag を **EditorOnly** にするか削除します（[TIPS: EditorOnly タグ](/tips#editor-only-tag)）。
:::

## Very Poor でもよいか

PC では、Very Poor でもアップロードでき、見る人が制限していなければ表示されます。最近の販売アバターは、衣装の切り替えなどを含めると Very Poor になることも珍しくありません（[TIPS: Very Poor になる](/tips#very-poor)）。

ただし、見る人の PC に負担をかけ、設定によっては表示されません。人の多い場所に行くときは、衣装を 1 着にした **軽い版** を別にアップロードしておくと親切です。
