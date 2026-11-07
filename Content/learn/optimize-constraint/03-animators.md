---
title: アニメーターの数を減らす
reading: あにめーたーのかずをへらす
description: アバターにある Animator コンポーネントの数の上限と確かめ方、Modular Avatar の Merge Animator と AAO の Trace And Optimize で扱える範囲を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- アニメーターの数の上限は、PC の Poor が 32、Android の Poor が 2
- 無効にしたものも数えられるので、Animator を無効にするだけでは数は減らない
- Playable Layers のスロットは Animation Controller だけだと公式が書いている
:::

アニメーターの数は、アバターにある Animator コンポーネントの数を数える項目です（VRChat 公式の定義は "The number of Animators on the avatar." です）。上限は PC の Poor が 32、Android の Poor が 2 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、公式の表では、表示される最低のランクを設定しているときに、ルート以外のすべてのアニメーターが取り除かれると書かれています（"All animators (aside from root animator) removed"。[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の英語の原文の要約）。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は日本語に訳した要約です。版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「アニメーターの数」で、今の値と上限を見ます。例の画面では、2（上限 32）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[パフォーマンスランク（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。Animator を無効にするだけでは、数は減りません。
- **数の数え方**: ランキングの文書は、"This will always be at least 1 due to the root animator being counted. This means that for the Excellent ranking, you can have no additional animators." と書いています（訳: ルートのアニメーターも数えるため、数は常に 1 以上です。Excellent では、ルート以外のアニメーターを追加できません）。Playable Layers の文書は、"you've got five root animators to play with" と書いています（訳: ルートのアニメーターとして、5 つ使えます）。同じ文書は、"all of these Playable Layers are put together into a combined Animator. This Animator is the root, main animator of your avatar." とも書いています（訳: これらの Playable Layers は、1 つの組み合わせたアニメーターにまとめられます。そのアニメーターが、アバターのルートの主要なアニメーターです）。
- **AAO を使うときの数**: AAO の公式ドキュメントは、非破壊改変ツールを使う改変では、VRChat SDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。Play モードに入ったときのランクは、anatawa12's Gist Pack の Actual Performance Window で確かめます（同じ箇所。手順は [AAO の結果の確認](/learn/optimize-aao/avatar-optimizer) を見てください）。

## 減らし方の考え方

VRChat 公式の Playable Layers の文書は、アバターのアニメーターについて次のように書いています（[Playable Layers（VRChat 公式）](https://creators.vrchat.com/avatars/playable-layers/)）。

- 原文: "This means that there is no reason to add any additional animators on your avatar." 訳: これは、アバターに追加のアニメーターを足す理由がない、という意味です。
- 原文: "We only support the use of Animation Controllers in Playable Layer slots." 訳: Playable Layer のスロットで使えるのは、Animation Controller だけです。
- 原文: "you should never use the same controller in multiple Playable Layers." 訳: 同じコントローラーを複数の Playable Layers で使うべきではありません。

公式の勧めは、「追加のアニメーターを増やさず、Playable Layers の仕組みの中にまとめる」という方向です。アニメーターを減らす具体的な手段は、次のツール別の方法を見てください。ほかの項目の減らし方の考え方は、[軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview) にまとめています。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**何をするか**: Merge Animator は、指定したアニメーターをアバターの指定したレイヤーに追加するコンポーネントです（公式: 「Merge Animatorコンポーネントは、指定したアニメーターをアバターの指定したレイヤーに追加します。」）。

**どう使うか**: 公式は、既存のアニメーターを指定したものに替えるときは、「既存アニメーターを置き換える」にすると書いています。これで、VRChat アバターディスクリプタに設定されたアニメーターが、指定したアニメーターに置き換わります。「非推奨の場合」の節では、既存のレイヤーをそのままにして、指定したコントローラーを追加するだけだと書かれています。

**注意**:

- 「付属アニメーターを削除」を入れると、同じオブジェクト内の Animator が自動的に排除（削除）されると、公式は書いています（[Merge Animator（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/merge-animator)）。
- ページは「レイヤーに追加する」「統合する」と書いています。統合後にアニメーターの数が減るかは、この記事の手順 6 で確かめてください。
- MA のトップページ（[Modular Avatar（MA 公式）](https://modular-avatar.nadena.dev/ja/)）は「アバターのFXアニメーター等を分割し、実行時に統合しよう。」と書いています。
- 未リリース（CHANGELOG の `[Unreleased]` の [MA の変更履歴](https://github.com/bdunderscore/modular-avatar/blob/main/CHANGELOG-jp.md)）の変更として、Reactive Component の生成をブレンドツリー方式に変えて、アニメーターの処理負荷を軽減すると書かれています。未リリースの変更です。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**何をするか**: AAO の Trace And Optimize の「アニメーターの最適化」（Optimize Animator）は、Animator Controller（アニメーションの動きを決めるアセット）のレイヤーを整理する機能です。公式は、次の 5 つを挙げています（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。

- AnyState 式のレイヤーを Entry-Exit 式に変換する
- 完全グラフ構造のレイヤーを Entry-Exit 式に変換する
- Entry-Exit 式のレイヤーを BlendTree に変換する
- BlendTree を統合する
- 使われていないレイヤーを削除する

公式は、「最適化処理の詳細な仕様は、将来的に変更される可能性があります。」とも書いています。これらは Animator Controller の中身を整理する機能です。

**注意**: AAO の CHANGELOG（1.6.0）には、「Remove Unused Objects removes meaningless Animators and Renderers」（使われていないObjectの削除で、意味のない Animator と Renderer が消える）と書かれています。その下位行には、"Renderers without Mesh and Animators without AnimatorController is meaningless"（Mesh の無い Renderer と、AnimatorController の無い Animator は意味がない）とあります（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「アニメーターの数」を確かめます（上限は PC の Poor が 32、Android の Poor が 2）。
2. アバターの中で、ルート以外の Animator があるオブジェクトを探します。Unity の画面の基本は [Unity の画面の見方](/learn/unity/editor-overview) を見てください。
3. 追加のアニメーターを、Playable Layers の仕組みの中にまとめられないかを確かめます。Playable Layer のスロットには Animation Controller だけを使い、同じコントローラーを複数のレイヤーで使わないようにします（公式の勧め）。
4. Modular Avatar の Merge Animator で、指定したレイヤーに統合するものを決めます。既存のアニメーターを替えるかどうかは、下の「注意・うまくいかないとき」の「置き換え」の注意を見て決めます。同じオブジェクトの Animator は、「付属アニメーターを削除」で扱えます。統合で数が減るかは、手順 6 で確かめます。
5. AAO を使う場合は、先に「注意・うまくいかないとき」の OSC の注意（FAQ）と、AAO の削除の効果についての注意を読みます。
6. 処理のあと、SDK の「アニメーターの数」と、Play モードでの Actual Performance Window の値を比べます。あわせて、アニメーションの動きが変わっていないかを確かめます。

## 注意・うまくいかないとき

::: warning
- **Playable Layers のスロットには Animation Controller だけ**: 公式は、Animation Controller だけを使うよう書いています。ほかの種類のコントローラーを使うと、エラーになるか、アップロードできなくなります（原文: "Do not use any other type of controller-- you will run into errors or will be unable to upload the content."）。
- **無効にしても数は減りません**: 公式は、無効化されたものも数えると書いています（上の「減らす前に確かめること」を見てください）。
- **Merge Animator の「置き換える」**: 公式は、同じレイヤー種別で複数のアニメーターを「置き換える」設定にするとエラーになると書いています（[Merge Animator（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/merge-animator)）。既存のアニメーターを替えるときは、同じレイヤー種別の「置き換える」を 1 つだけにしてください。
- **AAO の削除の効果**: CHANGELOG 1.6.0 には、意味のない Animator が削除されると書かれています（上の AAO の節を見てください）。使う場合は、適用の前後でアニメーターの数と動きを確かめてください。
- **OSC で使うコンポーネントがある場合**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。公式 FAQ（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）は、そのようなコンポーネントを使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools を設定するよう案内しています。自動の最適化のあとは、動くかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
- **未リリースの変更**: 上の Modular Avatar の節の、Reactive Component の処理負荷の改善は、未リリースです。
:::

## 関連ページ

- [ライトの数を減らす](/learn/optimize-constraint/lights)
- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [Unity の画面の見方](/learn/unity/editor-overview)
