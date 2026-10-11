---
title: アップロード前の確認
description: VRChat にアップロードする前に確かめること（Avatar Descriptor、パフォーマンスランク、使わないものの整理、Build & Test、Blueprint ID）です。
difficulty: 初級
updated: 2026-10-11
---

## チェックリスト

| 確かめること | どこで | 関連 |
|---|---|---|
| アバターのルートに **VRC Avatar Descriptor** がある | アバターの Inspector | [用語集](/glossary#avatar-descriptor) |
| **View Position**（目の位置）が両目の間にある | VRC Avatar Descriptor > View | [何て名前？](/howto#set-view-position) |
| 使わない服や小物が **EditorOnly** か、削除されている | 各オブジェクトの Tag | [TIPS](/tips#original-clothes-show) |
| **パフォーマンスランク** が目標の範囲 | VRChat SDK > Builder | [TIPS](/tips#very-poor) |
| テクスチャの **Max Size** が必要以上に大きくない | テクスチャの Inspector | [TIPS](/tips#texture-too-large) |
| Builder に **赤いエラー** がない | VRChat SDK > Builder | [TIPS](/tips#cannot-upload) |
| **Build & Test** で動かして、服が体についてくる | VRChat のアバターのメニューの SDK Test Avatars | [Modular Avatar で衣装を着せる](/learn/unity/modular-avatar-outfit) |
| 別のアバターとして出すなら **Blueprint ID** を外した | Pipeline Manager > Detach | [TIPS](/tips#overwrote-avatar) |

## パフォーマンスランク

VRChat は、アバターの重さを **Excellent / Good / Medium / Poor / Very Poor** で表します。三角形の数・マテリアルの数・Skinned Mesh の数・ボーンの数・PhysBone の数などの項目ごとにランクが付き、**いちばん悪い項目** がアバターのランクになります。

- ランクは VRChat SDK の **Builder** で、アバターを選ぶと表示されます
- Very Poor のアバターは、ほかの人の設定によっては表示されません。Android（Quest など）では Very Poor のアバターは表示されません
- 減らし方は [トポロジーの整理](/learn/modeling/topology) と [TIPS](/tips#very-poor) を参照

## Build & Test から Build & Publish へ

1. **VRChat SDK > Show Control Panel** で Builder を開く（先に Authentication でログイン）
2. **Build & Test** で、自分の PC の VRChat にだけ読み込んで確かめる
3. 問題なければ **Build & Publish**。名前・説明・サムネイル・公開範囲を設定してアップロードする

::: warning
既存のアバターから複製して作ったアバターは、Blueprint ID が同じままです。そのままアップロードすると、元のアバターに **上書き** されます。
:::
