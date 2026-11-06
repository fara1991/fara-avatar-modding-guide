---
title: PhysBone コライダーの数を減らす
reading: ふぃじっくぼーんこらいだーのかずをへらす
description: PhysBone コライダーの数の上限と確かめ方、Avatar Optimizer（AAO）で同じ設定のコライダーをまとめる方法と注意点を、公式の資料に書かれた範囲でまとめます。VRChat 公式の Global Collision の上限も書きます。
difficulty: 中級
updated: 2026-11-06
---

PhysBone コライダーの数は、アバター上の PhysBone コライダー（スクリプト）の数を数える項目です（VRChat 公式の定義は "The number of PhysBone collider scripts on the avatar." です）。上限は PC の Poor が 32、Android の Poor が 16 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えたときの扱いは、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) と同じです。表示される最低のランクの設定で PhysBone コライダーなどが取り除かれること、Android のハード上限（16）は「Show Avatar」でも回避できないことを、公式は書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: そのツールの公式の資料（日本語版のページと changelog）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

この記事は、AAO 1.9.21、Modular Avatar（GitHub の main。正式版 1.18.7 との差は未確認）、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は日本語に訳した要約です。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「PHYSBONE コライダーの数」で、今の値と上限を見ます。例の画面では、7（上限 32）と表示されていました。
- **標準コライダーを数えるかどうか**: 公式は、アバター記述子の「Colliders」にある標準コライダー（Standard Colliders）は、性能ランクに影響しないと書いています（原文: "These colliders do not affect the performance rating."）。ただし、この標準コライダーが PhysBone コライダーの数に入るかは、公式の本文からは断定できません（未確認）。
- **lilAvatarUtils の PBコライダー タブ**: 公式の説明では、「コライダーの参照元の特定や、どこからも参照されない無駄なコライダーの削減に役立ちます。」とされています。「参照数」は、このコライダーを参照している PhysBone の数です（[PhysBoneCollidersGUI（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。
- **Global Collision の数**: 公式は、アバターでは Global Collision を有効にしたコライダーは最大 4 つだと書いています。ワールドには制限がないとも書かれています（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)）。
- **無効にしたものも数えられます。** 公式は、無効化されているものも含めて、すべての GameObject とコンポーネントがパフォーマンスランクに数えられると書いています。

## 減らし方の考え方

VRChat 公式の PhysBones のページには、コライダーに関連する仕組みの記述があります（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)。日本語への訳は要約です）。

- **Global Collision**: 公式は、Global Collision は Sphere と Capsule のコライダーにだけ対応すると書いています。

VRChat 公式には、コライダーの数を減らす手順は見つかりませんでした（[PhysBones](https://creators.vrchat.com/common-components/physbones) のページを確かめた範囲）。この記事の整理として、減らす方法は次のとおりです（この記事の整理。公式の分類ではありません）。

- **同じ設定のコライダーをまとめる**: AAO Trace And Optimize（自動）と、AAO Merge PhysBone の Colliders の Merge（手動）。

## ツール別の方法

### Modular Avatar（MA）

**何をするか**: MA の Global Collider は、コライダーを追従させるための部品です。Root Transform は、この MA Global Collider 自身のフィールドで、コライダーをアバターの別の部分に追従させるために設定します（[Global Collider（MA 公式）](https://github.com/bdunderscore/modular-avatar/blob/main/docs~/i18n/ja/docusaurus-plugin-content-docs/current/reference/global-collider.md)）。

**注意**: PhysBone コライダーの数を減らす機能は、公式に記載なし（確かめた範囲: MA の日本語版の reference 一式と CHANGELOG）。MA の資料には、「６つ以上Global Colliderを使用すると、人差し指のコライダーが上書きされる」とも書かれていますが、これは MA の Global Collider についての記述で、VRChat の Global Collision の上限（4 つ）とは別の資料の記述です。両者の関係は、この記事では確かめていません（未確認）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: 日本語版のページ 81 件。PhysBone・Contact などの語で検索しました）。

### lilAvatarUtils

**何をするか**: PBコライダー タブで、アバターのコライダーを一覧し、どの PhysBone から参照されているかを見られます（[PhysBoneCollidersGUI（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。

**注意**: タブの説明は、無駄なコライダーの削減に役立つと書いていますが、削減そのものを行う機能の操作手順は、この記事では確認していません（未確認）。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: 作者 RamType0 の GitHub リポジトリの README（英語・日本語）と、日本語のドキュメント。公式サイトからの直接の確認は未確認です）。Collider・コンタクトなどの語で検索しても、該当する記述はありませんでした。

### Avatar Optimizer（AAO）

**Trace And Optimize（同じ設定のコライダーの統合）**: 公式の原文は、「全く同じ設定のPhysBone Colliderを1つに統合します。」です（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。英語版も、"Merges PhysBone Colliders with the exactly same settings into one PhysBone Collider." です。

- ここで統合されるのは「全く同じ設定」のコライダーです。「似た設定」のコライダーを統合するという記述は、公式にはありません。
- 変更履歴では、1.6.0 に、同じ設定の Floor コライダーは 1 つの Floor コライダーにまとめられる、と書かれています（[CHANGELOG（AAO）](https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/95c231830cefb62dde91e134642c70942f92ed2a/CHANGELOG.md)）。
- 1.9.14 には、VRCSDK 3.10.4 に合わせて PhysBone Global Collider の対応が追加された、と書かれています（原文の綴り "Colider" のまま）。

**AAO Merge PhysBone の Colliders（手動）**: 公式は、コライダーについては「Merge」を選ぶと、統合対象のコライダーの一覧をまとめられる、と書いています（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。この一覧をまとめたときに、コライダーのスクリプトの数がどう変わるかは、公式の記述からは断定できません（未確認）。Merge PhysBone の使い方は、[PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components) を見てください。

**コライダーの自動削除に関する記録**: 変更履歴には、PhysBone コライダーが誤って削除されることの修正が 3 つあります。1.9.0 は「一部の PhysBone コライダーが、意図せず削除される」ことの修正です。1.9.12 は、コライダーが外部のボーンを対象にしているとき、Merge PhysBone のコライダーが、切り替えの状態が違うものを統合することがあった、という修正です。1.9.20 は、同じ Transform を持つ、無効な PhysBone コライダーがほかにあると、コライダーが誤って削除されることがあった、という修正です（いずれも [CHANGELOG（AAO）](https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/95c231830cefb62dde91e134642c70942f92ed2a/CHANGELOG.md)）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE コライダーの数」を確かめます（上限は PC の Poor が 32、Android の Poor が 16）。
2. lilAvatarUtils の PBコライダー タブで、「参照数」が 0 のコライダー（どこからも参照されないもの）がないかを見ます。
3. OSC で使う PhysBone や Contact Receiver がある場合は、先に「注意・うまくいかないとき」の OSC の項目を読んで、必要な設定を済ませます。そのうえで、アバターのルートに AAO Trace And Optimize を付け、「PhysBone を最適化する」がオンになっているかを確かめます。全く同じ設定のコライダーが、統合の対象になります。
4. まとめきれないものは、AAO Merge PhysBone の Colliders を「Merge」にして、統合対象のコライダーの一覧をまとめます。
5. Global Collision を使うコライダーの数が、アバターの上限（4 つ）を超えていないかを確かめます。
6. Play モードに入って、パフォーマンスランクと、コライダーの当たり方が変わっていないかを確かめます。

## 注意・うまくいかないとき

::: warning
- **全く同じ設定だけが統合の対象**: Trace And Optimize の統合は、「全く同じ設定」のコライダーだけです。少し違う設定のコライダーは、そのままでは統合されません。
- **Limits は数を減らす機能の説明ではありません**: 公式が Limits について「far more performant than a collider」（コライダーよりはるかに軽い）と書いているのは、頭に髪が食い込むのを防ぐ場面での性能の話です。コライダーの数を減らす根拠としては書かれていません。
- **Global Collision の上限**: 公式は、アバターでは Global Collision を有効にしたコライダーは最大 4 つ、Sphere と Capsule のコライダーだけに対応すると書いています。上限を超えたときの動作は、公式のドキュメントでは見つからなかったため、未確認です。
- **標準コライダーの扱い**: 標準コライダーは性能ランクに影響しないと公式は書いていますが、PhysBone コライダーの数に入るかは未確認です。
- **OSC で使う PhysBone や Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、ほかで使われていなければ削除するように設計されています。公式 FAQ（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）は、この仮定は正しいとは限らないと書いています。FAQ が挙げているのは PhysBone と Contact Receiver で、コライダーについては書かれていません。Animator や Expression Menu を使わずに、OSC のギミックで PhysBone や Contact Receiver を使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定するよう案内しています（[Asset Description（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。OSC を使うアバターでは、自動の最適化のあとに、PhysBone と Contact が動くかを確かめてください（この記事の助言）。
- **AAO の修正の記録**: 上の 1.9.0・1.9.12・1.9.20 の修正があります。使うときは、AAO のバージョンを確かめてください。
:::

## 関連ページ

- [PhysBone コンポーネントの数を減らす](/learn/optimize-physbone/physbone-components)
- [PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
