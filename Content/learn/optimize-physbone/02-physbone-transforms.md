---
title: PhysBone トランスフォームの数を減らす
reading: ふぃじっくぼーんとらんすふぉーむのかずをへらす
description: PhysBone トランスフォーム（Affected Transforms）の数の上限と確かめ方、Avatar Optimizer（AAO）の Replace End Bone With Endpoint Position などで減らす方法を、公式の資料に効果が書かれている範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- PhysBone トランスフォームの上限は、PC の Poor が 256、Android の Poor が 64
- 1 つのコンポーネントが 128 を超えるトランスフォームに影響するときは、チェーンを分けることを検討するよう公式が書いている
- Replace End Bone With Endpoint Position（AAO）は数を減らすが、揺れ方が変わることがある
:::

PhysBone トランスフォームの数は、PhysBone コンポーネントが影響するトランスフォーム（ボーン）の合計数を数える項目です（VRChat 公式の定義は "The total number of transforms affected by PhysBones components on the avatar." です）。上限は PC の Poor が 256、Android の Poor が 64 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) と同じく、表示される最低のランクの設定で PhysBone などが取り除かれること、Android のハード上限（64）は「Show Avatar」でも回避できないことです。また、公式は、1 つの PhysBone コンポーネントは同時に 256 トランスフォームまでしか影響できず、ルートも子も数えると書いています（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**{.lbl .lbl-none}: そのツールの公式の資料（日本語版のページと changelog）を検索して、書かれていないと確かめた、という意味です。できない・減らせないという意味ではありません。
- **この記事の助言**{.lbl .lbl-adv}: 公式の説明ではない、この記事の助言です。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main の資料）、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は日本語に訳した要約です。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「PHYSBONE トランスフォームの数」で、今の値と上限を見ます。例の画面では、409（上限 256）と表示されていました。
- **1 つのコンポーネントが影響するトランスフォームの数**: 上限の 256 は、1 つのコンポーネントについての上限でもあります。公式は、1 つのコンポーネントが 128 を超えるトランスフォームに影響するなら、チェーンを分けることを検討するよう書いています（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)）。
- **lilAvatarUtils の PhysBone タブの「ボーン数」**: 公式の定義は「揺れが計算されるボーン数」です。PhysBone トランスフォームの数と同じものだとは書かれていないため、この値をそのまま PhysBone トランスフォームの数として扱うことはできません（[PhysBones（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。同じタブの「ルートボーン」は、PhysBone の計算を行うボーンまたは Transform の親で、この配下の Transform に揺れが適用される、と説明されています。
- **無効にしたものも数えられます。** 公式は、無効化されているものも含めて、すべての GameObject とコンポーネントがパフォーマンスランクに数えられると書いています（[パフォーマンスランク（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

## 減らし方の考え方

VRChat 公式の PhysBones のページには、トランスフォームの数を減らす次の勧めがあります（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)。日本語への訳は要約です）。

- 原文: "If you _really_ need a number, you should consider splitting sets of chains when you're getting over 128 transforms affected by a single component." 訳: 本当に必要な場合は、1 つのコンポーネントが 128 を超えるトランスフォームに影響するときに、チェーンを分けることを検討してください。
- 同じ箇所の例: 256 ボーンのドレスが根元で分かれているなら、2〜3 個のコンポーネントに分けると動作する、とあります。
- 原文: "Try merging bones in the chain upward to their immediate parents." 訳: チェーン内のボーンを、直上の親にまとめてみてください。公式は、これは Cat's Blender Plugin などのツールでできる、とも書いています。
- 原文: "However, you should aim not to have that many transforms to animate in the first place." 訳: ただし、そもそも動かすトランスフォームを多くしないことを目指すべきです。

この記事の整理として、減らす方法を次のように分けます（この記事の整理。公式の分類ではありません）。

- **末端のボーンを Endpoint Position に置き換える**: AAO の Replace End Bone With Endpoint Position（手動）と、Trace And Optimize の自動の置き換え。
- **チェーンを分ける**: 公式の勧め（上の原文）。チェーンを分けるツールの機能は、この記事で確かめた範囲では見つかりませんでした（**公式に記載なし**{.lbl .lbl-none}）。
- **動かないボーンを整理する**: Trace And Optimize の「使われていないObjectを自動的に削除する」や AAO Merge Bone。数への効果は、ツールの節で書きます。

## ツール別の方法

### Modular Avatar（MA） {.tool .tool-ma}

**何をするか**: Merge Armature は、衣装などの PhysBone や Contact の「Target欄」を、統合先のボーンに書き換えます。公式の原文は「PhysBonesやContactはTarget欄が統合先のボーンに書き換えられます。」です（[Merge Armature（MA 公式）](https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature)）。

**注意**: PhysBone トランスフォームの数への効果は、**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: MA の日本語版の reference 一式と CHANGELOG）。PhysBone Blocker の説明にも、数える・減らすという記述はありません。

### TexTransTool（TTT） {.tool .tool-ttt}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 日本語版のページ 81 件。PhysBone・bone などの語で検索しました）。

### lilAvatarUtils {.tool .tool-lil}

**何をするか**: PhysBones タブで、アバターにある PhysBone の「ボーン数」（揺れが計算されるボーン数）と「ルートボーン」を見られます。ルートボーンは、PhysBone の計算を行うボーンまたは Transform の親で、この配下にある Transform に揺れが適用される、と説明されています（[PhysBones（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。

**注意**: 「トランスフォームの数」を表示する項目は、確かめた範囲では見つかりませんでした（**公式に記載なし**{.lbl .lbl-none}）。ボーン数の値を減らす機能の説明もありません。

### Meshia Mesh Simplification（MMS） {.tool .tool-mms}

**公式に記載なし**{.lbl .lbl-none}（確かめた範囲: 作者 RamType0 の GitHub リポジトリの README（英語・日本語）と、日本語のドキュメント）。PhysBone・bone などの語で検索しても、該当する記述はありませんでした。

### Avatar Optimizer（AAO） {.tool .tool-aao}

**AAO Replace End Bone With Endpoint Position（手動）**: PhysBone の end ボーン（末端のボーン）を、Endpoint Position（末端の位置を値で決める設定）に置き換えます。公式の原文は、「End BoneをEndpoint Positionに置き換えることにより、VRChatのPerformance Rankシステムにおける`PhysBone Affected Transforms`の数を減らすことができます。」です（[Replace End Bone With Endpoint Position（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/replace-end-bone-with-endpoint-position/)）。

- **設定**: Average（end ボーンのローカル座標の平均）か Override（Endpoint Position Override で指定した値）を選びます。
- **対象**: 同じ GameObject にある PhysBone コンポーネントが複数あるときは、その全てに設定が適用される、と公式は書いています。

**Trace And Optimize の「PhysBone を最適化する」（自動）**: 公式の原文は、「Endpoint Positionで問題なく表現できる場合に、endボーンを削除してEndpoint Positionに置き換えます。」です（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。同じ置き換えを自動で行うと書かれていますが、このページでは数への効果は書かれていません（**公式に記載なし**{.lbl .lbl-none}）。

**Trace And Optimize の「end ボーンを残す」（Preserve EndBone）**: 親が削除されていない end ボーンを、削除しないようにします。公式は、名前が end（大文字小文字の区別なし）で終わるボーンを end ボーンとして扱うと書いています。PhysBone トランスフォームの数への効果は、**公式に記載なし**{.lbl .lbl-none}。

**AAO Clear Endpoint Position**: 公式の原文は、「このコンポーネントは、Endpoint Positionの値を0に置換し、`_EndPhysBone`GameObjectを追加します。」です（[Clear Endpoint Position（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/clear-endpoint-position/)）。PhysBone トランスフォームの数への効果は、**公式に記載なし**{.lbl .lbl-none}。

- 注意: CHANGELOG の 1.7.0 には、新しく作る Merge PhysBone の Endpoint Position の既定を Clear から Copy に変えたと書かれ、その理由として「Clear の設定は PhysBone トランスフォームの数を増やす」（英語の原文の訳）と書かれています。これは Merge PhysBone の Endpoint Position の設定についての記述です。

**AAO Merge Bone**: GameObject を親の GameObject に統合して、元の GameObject を取り除きます。公式は、Trace And Optimize が似た処理を自動で行うため、大抵の場合は使う必要がないと書いています（[Merge Bone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-bone/)）。PhysBone トランスフォームの数への効果は、**公式に記載なし**{.lbl .lbl-none}。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE トランスフォームの数」を確かめます（上限は PC の Poor が 256、Android の Poor が 64）。
2. 1 つの PhysBone コンポーネントが、128 を超えるトランスフォームに影響していないかを確かめます。128 を超える場合は、チェーンを分けることを検討します（VRChat 公式の勧め）。
3. 末端のボーンを Endpoint Position で表せる場合は、AAO Replace End Bone With Endpoint Position を、その PhysBone がある GameObject に付けます。Average か Override を選びます。
4. Trace And Optimize の「PhysBone を最適化する」をオンにすると、自動の置き換えが行われることを確かめます。
5. 置き換えたあとは、先端の揺れ方が変わっていないかを、必ず Play モードで確かめます。
6. Play モードに入ったときのランクを確かめます（[AAO の結果の確認](/learn/optimize-aao/avatar-optimizer)）。

## 注意・うまくいかないとき

::: warning
- **先端の揺れ方が変わることがある**: Endpoint Position に置き換えると、先端の揺れ方が変わることがあります。この記事の注意として、置き換えたあとに確かめてください。
- **過去の不具合の修正**: CHANGELOG には、置き換えに関する修正が 3 つあります。1.9.2 では、アニメーションされている end ボーンが、意図せず Endpoint Position に置き換えられることがありました（修正済み）。1.9.4 では、場合によって誤って置き換えられることがありました（修正済み）。1.9.17 では、ボーンの位置が (0, 0, 0) のとき、PhysBone が壊れることがありました（修正済み）。使うときは、AAO のバージョンを確かめてください。
- **Merge PhysBone を使うと数が増える**: Merge PhysBone を 1 つ使うごとに、ルートの GameObject も数えられるため、PhysBone トランスフォームの数が 1 増えます（[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)）。
- **OSC で使う PhysBone や Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、ほかで使われていなければ削除するように設計されています。公式 FAQ（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）は、この仮定は正しいとは限らないと書いています。OSC で使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定するよう案内しています。PhysBone が効かなくなっていないかを、自動の最適化のあとに確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
:::

## 関連ページ

- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [ボーンの数を減らす](/learn/optimize-mesh/bones)
