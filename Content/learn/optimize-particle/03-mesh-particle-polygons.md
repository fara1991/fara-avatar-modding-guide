---
title: メッシュパーティクルの三角ポリゴン数の確かめ方と、減らす手がかり
reading: めっしゅぱーてぃくるのさんかくぽりごんすうのたしかめかたとへらすてがかり
description: メッシュパーティクルの三角ポリゴン数の上限と確かめ方、定義の式、減らす手がかり（放出数と頂点数）をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- 対象は、有効な Particle System が出すメッシュパーティクルの三角ポリゴン数
- 上限は、PC の Poor が 5,000、Android の Poor が 400
- 作業するときは、Mesh Read/Write を無効にしない（無効にすると、ランクは即座に Very Poor になる）
- 減らす手がかりは、式の maxEmission（放出数）と meshParticleVerts（頂点数）の 2 つ
:::

メッシュパーティクルの三角ポリゴン数は、有効（active）な Particle System（パーティクルシステム）が出すメッシュ状の粒子（メッシュパーティクル）の三角ポリゴンの数を数える項目です。SDK の「パフォーマンスの内訳」では「メッシュパーティクルの三角ポリゴン数」と表示され、ランキングの表の行名は Mesh Particle Active Polys です。上限は、PC の Poor が 5,000、Android の Poor が 400 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、次のランクになります。表示の下限（Minimum Displayed Performance Rank）より下のランクになると、All Particle Systems removed（すべての Particle System が取り除かれる）になります（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。PC の既定の表示の下限は Very Poor で、ほとんどのアバターは見えます。ただし、極端に最適化されていないアバターは、使えなくなることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「メッシュパーティクルの三角ポリゴン数」で、今の値と上限を見ます。[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の例の画面では、4,000,000（上限 5,000）と表示されていました。
- **Control Panel の数字**: 非破壊改変ツールを使った改変では、SDK の Control Panel のパフォーマンスランクは実際の値とずれます。実際の値は、Play モードの Actual Performance Window で確かめます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)）。
- **数え方の式**: 定義は、有効な Particle System が出すメッシュパーティクルの三角形（triangles）の合計数です。言い換えると、maxEmission × meshParticleVerts です。

## 減らし方

パーティクル全般では、使う Particle System の数を制限し、同時に放出される粒子の最大数を制限するのがおすすめです（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「メッシュパーティクルの三角ポリゴン数」の今の値と上限（PC の Poor が 5,000、Android の Poor が 400）を確かめます。
2. この式（maxEmission × meshParticleVerts）から、減らせる変数は「放出数」（式の maxEmission）と「メッシュの頂点数」（式の meshParticleVerts）です（**この記事の助言**{.lbl .lbl-adv}）。
3. 作業の前に、プロジェクトのバックアップを取り、対象のパーティクルの見た目と動きを記録します（**この記事の助言**{.lbl .lbl-adv}）。
4. 作業するときは、Mesh Read/Write を無効にしないでください（下の注意）。
5. 放出数（式の maxEmission）か、メッシュの頂点数（式の meshParticleVerts）を減らしたあとは、Play モードに入って見た目と動きが変わっていないかを確かめ、パフォーマンスの内訳をもう一度見て、数が変わったかを確かめます（**この記事の助言**{.lbl .lbl-adv}）。

## 注意・うまくいかないとき

::: warning
- **Mesh Read/Write**: アバターのいずれかの mesh（パーティクルシステムのメッシュを含む）で Mesh Read/Write を無効にすると、アバターの Triangles（三角ポリゴン数）の行は「Mesh Read/Write Disabled」と表示され、ランクは即座に Very Poor になります。アバターの実際の三角ポリゴン数にかかわらず、この扱いになります（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
:::

## 関連ページ

- [三角ポリゴンを減らす方法](/learn/optimize-mesh/triangles)
- [パーティクルトレイルを使わないようにする](/learn/optimize-particle/particle-trails)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
