---
title: コンタクトの数を減らす
reading: こんたくとのかずをへらす
description: VRChat のパフォーマンス表の「コンタクトの数」が何を数えるか、上限、Local Only の扱い、公式に書かれている減らし方と注意、ツールごとに公式に書かれていることを整理します。
difficulty: 中級
updated: 2026-11-06
---

コンタクトは、アバターに付けた Contact（触れたときに反応する仕組み）の数を数える項目です。公式の定義は「アバター上の Contact の数。フィルタリングが "Local Only" に設定された receiver は数えない」です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。上限は、PC の Poor が 32、Android の Poor が 16 です。

| 項目 | PC（Excellent / Good / Medium / Poor） | Android（Excellent / Good / Medium / Poor） |
|---|---|---|
| コンタクトの数 | 8 / 16 / 24 / 32 | 2 / 4 / 8 / 16 |

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値です。超えると、公式の表では、表示ランクの下限（Minimum Displayed Performance Rank）を下回るアバターについて「All PhysBone, PhysBone Collider, and Contact components removed」（PhysBone・PhysBone コライダー・コンタクトのコンポーネントがすべて取り除かれる）と書かれています。Android では、ハード上限（16）を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれます。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: 公式の資料（日本語・英語のドキュメントの全ページと CHANGELOG）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

VRChat の公式ドキュメント（Avatar Performance Ranking System・Contacts）は英語のみで、日本語版のページはありません。この記事の日本語は、英語の原文を訳したものです。原文は、公式サイトのソース（GitHub の creator-docs）で確かめました。ライブの公式サイトの表示と違うときは、ライブの表示が正です。

この記事は、AAO 1.9.21 と、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「コンタクトの数」で、今の値と上限を見ます。
- **Filtering の Local Only**: 公式の定義（ranking のページ）が数えないとしているのは、Local Only に設定された **receiver** だけです。Local Only の receiver は、アバターで最大 256 個まで使えます（後述の注意）。
- **sender の Filtering**: Contacts のページの VRCContactSender の Filtering の節には、「If enabled, the contact doesn't reduce your avatar's performance rank.」（有効にすると、性能ランクを下げない）と書かれています（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。ランクの定義文が数えないとしているのは receiver だけなので、sender の扱いは、この節の記述を根拠にしてください。
- **Play モードのランク**: AAO を使ったあとは、SDK の Control Panel のランクがあてにならないと公式に書かれています。Play モードのランクの確かめ方は、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」を見てください。

## 減らし方の考え方

- 公式のコンタクトのページには、「When using contacts on an avatar, the amount of contacts you use will affect its performance rank.」（アバターでコンタクトを使うと、使う数が性能ランクに影響する）と書かれています（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。
- 公式が挙げている減らし方は、**Local Only** を有効にすることです（[Contacts（VRChat 公式）](https://creators.vrchat.com/common-components/contacts)）。公式の定義では、Local Only の receiver は数えません。sender でも、有効にすると性能ランクを下げないと書かれています。
- Advanced Parameters については、公式の [Contacts](https://creators.vrchat.com/common-components/contacts) のページに「Advanced parameters can be expensive to calculate. Only enable the ones you need.」（Advanced Parameters は計算のコストが高い。必要なものだけ有効にする）と書かれています。

手順全体は、[アバターを軽くする](/learn/vrchat/optimization) を見てください。

## ツール別の方法

### Modular Avatar（MA）

- **何をするか**: MA Rename Collision Tags は、同じ GameObject の下にある Contacts のタグを、それに応じて名前を変えます（[Modular Avatar 公式（日本語）](https://modular-avatar.nadena.dev/ja)）。
- **どう使うか**: MA Parameters の接頭辞設定は、VRC Contact Receiver の parameter もリネームの対象にします（更新履歴の #2189）。[Merge Armature](https://modular-avatar.nadena.dev/ja/docs/reference/merge-armature) は、PhysBone と Contact の Target 欄を、統合先のボーンに書き換えます。
- **注意**: `MA Rename Collision Tags` を Contacts の `Root Transform` の親 GameObject に置いても、その Contacts のタグは変更されないと、公式に書かれています。
- コンタクトの数を減らす、または数えることについては、公式に記載なし（確かめた範囲: Modular Avatar の日本語版のリファレンス一式と、更新履歴の日本語版）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: [TexTransTool の日本語版のドキュメント](https://ttt.rs64.net/docs/) の 81 ページで、Contact・コンタクト・PhysBone・bone・フィジックスの語を検索し、ヒットなし）。

### lilAvatarUtils

公式に記載なし（確かめた範囲: PhysBonesGUI・PhysBoneCollidersGUI の本文と、AvatarUtils・UtilsGUI のページ。Contact・コンタクトの語はなく、コンタクトの数を表示する項目はありません）。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: [README](https://github.com/RamType0/Meshia.MeshSimplification) と、日本語版のドキュメント（はじめに・インストール・VRChat 向けのページ）。Contact・コンタクトの語で検索し、ヒットなし）。

### Avatar Optimizer（AAO）

- **何をするか**: Trace And Optimize の「使われていないObjectを自動的に削除する」（英語版の名前は Remove Unused Objects）は、CHANGELOG の 1.7.0 で、「Remove Unused Objects now removes PhysBones and Contact Receivers with parameters defined but not used by Animator Controllers」と変更されました。Animator Controller で使われていないパラメーターが定義されている Contact Receiver は、削除されます（[AAO の CHANGELOG](https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/95c231830cefb62dde91e134642c70942f92ed2a/CHANGELOG.md)）。
- **どう使うか**: Trace And Optimize の「使われていないObjectを自動的に削除する」を選ぶと、この自動の処理が行われます（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **注意**:
  - AAO の機能として Contact の数を減らすことは、公式に記載なし（確かめた範囲: 日本語・英語の公式ドキュメントの全ページと CHANGELOG。Trace And Optimize の日本語のページには、Contact の語がありません）。
  - 上の CHANGELOG の 1.7.0 の記録は、条件つきで Contact Receiver を削除するものです。条件に当たらない Contact は、この機能の対象になりません。
  - CHANGELOG の 1.9.19 には「Contacts for bHaptics are removed」（PR 1791）とあります。1 行だけでは、削除が不具合だったのか、修正だったのかは確定しないため、未確認です。
  - CHANGELOG の 1.9.21 にある「Material Contacts Support」は、VRCSDK 3.10.6 の機能への対応です。コンタクトを減らす機能ではありません。

## 手順

1. SDK の「パフォーマンスの内訳」で、「コンタクトの数」の値と上限（PC の Poor が 32、Android の Poor が 16）を確かめます。
2. Contact の Filtering を見て、Local Only が付いているものと付いていないものを分けます。Local Only の receiver は、アバターで最大 256 個です。
3. Advanced Parameters が有効になっているものを確かめ、必要なものだけを有効にします（公式の説明）。
4. AAO の Trace And Optimize を使うときは、「使われていないObjectを自動的に削除する」をオンにして、Animator Controller で使われていない Contact Receiver が消えていないかを確かめます。
5. Contact Receiver が OSC で使われているときは、後ろの「注意・うまくいかないとき」の設定を先に行います。
6. Play モードのランク、または Build & Test で、SDK の値と Contact の動きを確かめます。

## 注意・うまくいかないとき

::: warning
- **Local Only の receiver は 256 個まで**: 公式の [Contacts](https://creators.vrchat.com/common-components/contacts) のページには、Local Only を有効にした receiver は、アバターで最大 256 個まで使えると書かれています。超えると、アバターをアップロードできません（原文: 「If you exceed this limit, you cannot upload the avatar.」）。
- **Android の上限**: モバイルのハード上限は、コンタクトが 16 です。公式の [Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) には、ハード上限を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれると書かれています。
- **タグは 16 個まで**: 公式の [Contacts](https://creators.vrchat.com/common-components/contacts) のページには、Contact は最大 16 個のタグを持てると書かれています（「A Contact can have up to 16 tags.」）。
- **標準コライダーは性能ランクに影響しない**: Contacts のページの Standard Colliders の節には、「These colliders do not affect the performance rating.」（標準コライダーは性能ランクに影響しない）と書かれています。ただし、この節がコンタクトの数にどう関わるかは、この記事では未確認です。
- **無効のものも数えられます**: 公式の [Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) には、無効化されているものも性能ランクに数えられると書かれています。
:::

::: warning
**OSC で使う Contact Receiver・PhysBone**: AAO の Trace And Optimize は、Contact Receiver や PhysBone が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。公式の FAQ には、次のように書かれています。

- 「技術的な理由から、`AAO Trace and Optimize`コンポーネントはPhysBoneやContact ReceiverコンポーネントがOSCギミックで使用されているかどうかを判別することができません。」
- そのため、使われていないものと仮定して、ほかの用途で使われていなければ削除するよう設計されています。
- この仮定は正しいとは限らないため、Animator や Expression Menu を使わずに OSC ギミックで使う場合は、Asset Description ファイルを作成し、「Parameters Read By External Tools」を設定してください。

OSC で使う Contact Receiver が効かないときは、この設定を確かめてください（[よくある質問（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)、[Asset Description（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [PhysBone の衝突チェックの数を減らす](/learn/optimize-physbone/collision-checks)
