---
title: ライトの数を減らす
reading: らいとのかずをへらす
description: アバターにある Light コンポーネントの数の上限と、Android で無効になる点、公式が勧める減らし方を、各ツールの公式の資料に書かれた範囲でまとめます。
difficulty: 初級
updated: 2026-11-06
---

ライトの数は、アバターにある Light コンポーネントの数を数える項目です（VRChat 公式の定義は "The number of Light components on the avatar." です）。上限は PC の Poor が 1 です（Excellent・Good・Medium は 0）（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。Android（Quest を含む）では、ライトは無効になります。

上限を超えると、公式の表では、表示される最低のランクを設定しているときに、すべてのライトが取り除かれると書かれています（"All Lights removed"。[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の英語の原文の要約）。ランキングの文書の例では、上限を超えたためにライトとパーティクルシステムが無効になると説明されています。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog。VRChat は英語の原文）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、TexTransTool・lilAvatarUtils・Meshia Mesh Simplification の公式の資料を 2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は日本語に訳した要約です。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「ライトの数」で、今の値と上限を見ます。例の画面では、1（上限 1）と表示されていました。上限ちょうどのため、Poor の範囲になります。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[パフォーマンスランク（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。ライトを消すだけでは、数は減りません。
- **PC の既定の設定**: 公式は、PC の表示の下限の既定値は Very Poor で、既定ではパフォーマンスを理由にコンポーネントが影響を受けないと書いています。上限を超えたライトが取り除かれるのは、表示の下限を変えたときです。
- **上限を超えたとき（Very Poor）**: ランク表には Very Poor の列がありません。Poor の上限（1）を超えて、ライトが 2 つ以上になったときの扱いは、この表からは読み取れないため、未確認です。
- **AAO を使うときの数**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRChat SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。Play モードに入ったときのランクは、anatawa12's Gist Pack の Actual Performance Window で確かめます（[AAO の結果の確認](/learn/optimize-aao/avatar-optimizer) を見てください）。

## 減らし方の考え方

VRChat 公式の Avatar Optimization Tips は、ライトについて次のように書いています（[Avatar Optimization Tips（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-optimizing-tips/)。訳は要約です）。

- 原文: "Lights on avatars are real-time, and as such, are exceedingly expensive." 訳: アバターのライトはリアルタイムで計算されるため、非常に負荷が大きいです。
- 原文: "Adding a light to your avatar means that everything that your Light touches will render with *double* the draw calls." 訳: ライトを追加すると、そのライトが当たるものは、描画の呼び出し（ドローコール）が 2 倍になります。
- 原文: "Do not use Lights that are always on. Try using an Animation Override to turn a flashlight on and off, or alternately, do not use Lights at all." 訳: 常に点いているライトは使わないでください。懐中電灯の点灯と消灯には Animation Override を使うことを試してください。または、ライトをまったく使わないでください。
- 原文: "If you do use a Light, turn off Shadows for the Light." 訳: ライトを使うなら、そのライトの Shadows をオフにしてください。
- 原文: "Particle Systems can be configured to have a light on for a number of particles. *Never do this!*" 訳: パーティクルシステムは、一部のパーティクルにライトを付けられますが、絶対にしないでください。
- 原文: "In total, VRChat recommends that you do not use Lights of any type on avatars at all." 訳: 全体として、VRChat は、アバターにどの種類のライトも使わないことを勧めています。

公式の勧めは、ライトを減らすことです。Animation Override で点灯・消灯しても、ライトは「減らす前に確かめること」のとおり、数に残ります。数を減らすには、ライトのコンポーネント自体を使わないことが必要です。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: 日本語版のリファレンス一式と CHANGELOG）。「ライト」の語は、MA Mesh Settings のライトプローブの記述（バウンズとライトプローブアンカーの設定）だけで、ライトの数を扱う記述はありませんでした。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: 日本語版の公式ドキュメントと、リポジトリの CHANGELOG）。CHANGELOG の「light」は、画像の合成モードの名前（LightenColorOnly など）だけで、アバターのライトの話ではありません。

### lilAvatarUtils

公式に記載なし（確かめた範囲: 日本語版のドキュメントの 9 ページと CHANGELOG）。ライティングのタブ（LightingTestGUI）は、Unity のデフォルトシーンでは気付けない特殊な照明の状況で、アバターの見た目を確認するためのプレビューです（[公式: LightingTestGUI](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/LightingTestGUI.html)）。「ライトなし」「スポットライト」などは、テスト用の照明パターンの名前で、アバターが持つライトの数ではありません。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: 作者の GitHub リポジトリの README（英語と日本語）と、日本語の docfx のドキュメント）。ライトの語を検索しましたが、該当する記述は見つかりませんでした。

### Avatar Optimizer（AAO）

**何をするか**: AAO に、ライトの数を減らす機能の記載はありません（公式に記載なし。ja と en の全ページで、ライトの意味の語は見つかりませんでした）。

AAO の Trace And Optimize の「使われていないObjectを自動的に削除する」は、公式では次のように書かれています（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

- 原文（ja）: 「アニメーションなどを走査して、使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します。」
- 原文（en）: "By scanning animation etc., automatically removes unused Objects (e.g. GameObjects, Components, Textures)."

Light は名指しされていません。未使用のライトが、この自動の削除の対象に含まれるかどうかは、公式に記載なしです。

**注意**: AAO の CHANGELOG（1.9.15）には、AutoMergeSkinnedMesh の生成レンダラーのレイヤーを選ぶ修正（#1749）があります。その説明には、"This prevents layer-based cameras, lights, and shader gimmicks work correctly in Play mode after optimization." と書かれています（[CHANGELOG（AAO）](https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/95c231830cefb62dde91e134642c70942f92ed2a/CHANGELOG.md)）。ライトという語が出るのは、この修正の説明だけです。ライトを減らす機能の記載ではありません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「ライトの数」を確かめます（上限は PC の Poor が 1。Android では無効）。
2. アバターで使っているライトを確かめます。見た目に必要ないライトは、Light コンポーネントを外します。公式は、ライトをまったく使わないことを勧めています。
3. 残すライトは、常に点いたままにしないようにします。懐中電灯のように必要なときだけ点けたい場合は、Animation Override で点灯と消灯を切り替える方法を、公式が例に挙げています。ただし、消しても数には残ります。
4. 残すライトで、影（Shadows）が必要ないものは、Shadows をオフにします（公式の勧め）。
5. パーティクルシステムのライトの設定は使わないようにします（公式は「絶対にしないでください」と書いています）。
6. 処理のあと、SDK の「ライトの数」と、Play モードでの Actual Performance Window の値を比べます。

## 注意・うまくいかないとき

::: warning
- **Android ではライトは無効**: 公式は、Android と Quest のアバターでは、ライトが完全に無効になると書いています（"Completely disabled for avatars on Android and Quest."）。Android 向けのアバターでは、ライトを当てにしないでください。
- **無効にしても数は減りません**: 公式は、無効化されたものも数えると書いています。
- **Poor の上限は 1**: 2 つ目のライトが、ランクにどう響くかは未確認です。
- **AAO のライトへの効果は公式に記載なし**: AAO の Trace And Optimize で、ライトが自動で減るとは、公式には書かれていません。手順には含めていません。AAO の Trace And Optimize を使う場合は、先に OSC の注意（[よくある質問（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）を読んでください。AAO は、OSC（外部ツールと値をやりとりする仕組み）で使われている PhysBone や Contact Receiver を判別できないため、Asset Description の設定が必要になることがあります。
- **パーティクルのライト**: 公式は、ライトの付いたパーティクルは、それぞれがリアルタイムライトとして数えられると書いています。
:::

## 関連ページ

- [アニメーターの数を減らす](/learn/optimize-constraint/animators)
- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
