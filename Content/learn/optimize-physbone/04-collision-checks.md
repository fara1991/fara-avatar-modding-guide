---
title: PhysBone の衝突チェックの数を減らす
reading: ふぃじっくぼーんのしょうとつちぇっくのかずをへらす
description: VRChat のパフォーマンス表の「PhysBone 衝突チェックの数」が何を数えるか、上限、公式に書かれている減らし方と注意、ツールごとに公式に書かれていることを整理します。
difficulty: 中級
updated: 2026-11-06
---

PhysBone 衝突チェックの数は、アバターの PhysBone コライダーごとに、影響しうる PhysBone トランスフォームの数を足し合わせた値です（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。1 つのトランスフォームが複数のコライダーに影響されると、その分だけ重ねて数えられます。上限は、PC の Poor が 512、Android の Poor が 64 です。

| 項目 | PC（Excellent / Good / Medium / Poor） | Android（Excellent / Good / Medium / Poor） |
|---|---|---|
| PhysBone 衝突チェックの数 | 32 / 128 / 256 / 512 | 0 / 16 / 32 / 64 |

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値です。超えると、公式の表では、表示ランクの下限（Minimum Displayed Performance Rank）を下回るアバターについて「All PhysBone, PhysBone Collider, and Contact components removed」（PhysBone・PhysBone コライダー・コンタクトのコンポーネントがすべて取り除かれる）と書かれています。Android では、ハード上限を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれます（注意の節を見てください）。

::: tip
この記事で使う言葉は、次のとおりです。

- **公式に記載なし**: 公式の資料（日本語・英語のドキュメントの全ページと CHANGELOG）を検索して、書かれていないと確かめた、という意味です。
- **未確認**: この記事では確認していない、という意味です。無い・できないという意味ではありません。

VRChat の公式ドキュメント（Avatar Performance Ranking System・PhysBones）は英語のみで、日本語版のページはありません。この記事の日本語は、英語の原文を訳したものです。原文は、公式サイトのソース（GitHub の creator-docs）で確かめました。ライブの公式サイトの表示と違うときは、ライブの表示が正です。

この記事は、AAO 1.9.21 と、ほかのツールの公式の資料を 2026 年 11 月 6 日に確かめて書きました。版が上がると挙動が変わることがあります。
:::

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「PHYSBONE 衝突チェックの数」で、今の値と上限を見ます。
- **lilAvatarUtils の PhysBone タブ**: PhysBone ごとに「コリジョン数」を見られます。lilAvatarUtils の公式の説明は、「衝突判定の計算回数です。ボーン数やコライダー数に応じてこの数字は増えていき、数字が増えるほど負荷も増えます。」です（[PhysBones（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。これは lilAvatarUtils の項目の定義です。SDK の「PHYSBONE 衝突チェックの数」と同じだとは、公式に書かれていません。この記事では、同じ数として扱いません。
- **同じタブの「ボーン数」と「コライダー数」**: 公式の説明では、ボーン数は「揺れが計算されるボーン数」、コライダー数は「このPhysBoneとの当たり判定を計算するコライダー数」です（同じページ）。
- **Play モードのランク**: AAO を使ったあとは、SDK の Control Panel のランクがあてにならないと公式に書かれています。Play モードのランクの確かめ方は、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」を見てください。

## 減らし方の考え方

VRChat の公式には、衝突チェックの数を直接減らす手順は見つかりませんでした（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) と [PhysBones](https://creators.vrchat.com/common-components/physbones) のページを確かめた範囲）。以下は、公式のページに書かれている、PhysBone の衝突に関係する記述です。どれも、衝突チェックの数がどれだけ減るかは書かれていません。

- **定義の読み方**: 公式の定義には、「1 つのトランスフォームが複数のコライダーに影響されると、二重以上に数えられる」とあります。この定義から、重なって影響するコライダーが多いほど、数は増えると読めます（この記事の読み方です。公式の勧めではありません）。
- **Allow Collision**: 公式の PhysBones のページには、「If set to **False**, this PhysBone will only collide with the colliders that are listed in the `Colliders` list.」（False にすると、この PhysBone は Colliders のリストに入ったコライダーとだけ衝突します）と書かれています（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)）。衝突チェックの数への効果は、未確認です。
- **Limits**: 公式には、Limits を設定すると PhysBone のチェーンが動ける量を制限できると書かれています。また、髪が頭に埋まるのを避ける用途では、コライダーより「**far** more performant」（はるかに軽い）とも書かれています（同じページ）。ただし、この記述は衝突チェックの数の話ではありません。

手順全体は、[アバターを軽くする](/learn/vrchat/optimization) を見てください。

## ツール別の方法

### Modular Avatar（MA）

公式に記載なし（確かめた範囲: [Modular Avatar の日本語版のリファレンス](https://modular-avatar.nadena.dev/ja) の一式と、更新履歴の日本語版。PhysBone やコライダーの数を数える・減らす記述は見つかりませんでした）。

### TexTransTool（TTT）

公式に記載なし（確かめた範囲: [TexTransTool の日本語版のドキュメント](https://ttt.rs64.net/docs/) の 81 ページで、PhysBone・Contact・bone・コンタクト・フィジックスの語を検索し、ヒットなし）。

### lilAvatarUtils

- **何をするか**: PhysBone タブで、アバターの PhysBone の一覧を確認できます。PBコライダー タブでは、コライダーの一覧と「参照数」（このコライダーを参照している PhysBone の数）を確認できます（[PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)・[PBコライダー](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。
- **どう使うか**: 「コリジョン数」が大きい PhysBone を見つける候補にします。公式の説明では、PhysBone タブは「パフォーマンスランクへの影響が大きいPhysBoneコンポーネントの特定」に役立ちます。PBコライダー タブは、「どこからも参照されない無駄なコライダーの削減」に役立つと書かれています。
- **注意**: 「コリジョン数」は lilAvatarUtils の項目名です。SDK の「PHYSBONE 衝突チェックの数」と同じとは書かれていません。減らす操作（統合や削除）の詳細は、この記事では未確認です。

### Meshia Mesh Simplification（MMS）

公式に記載なし（確かめた範囲: [README](https://github.com/RamType0/Meshia.MeshSimplification) と、日本語版のドキュメント（はじめに・インストール・VRChat 向けのページ）。PhysBone・Contact・Collider・コンタクト・フィジックス・ボーンの語で検索し、ヒットなし）。

### Avatar Optimizer（AAO）

- **何をするか**: Trace And Optimize の「PhysBoneを最適化する」（英語版の名前は Optimize PhysBone Settings）は、全く同じ設定の PhysBone コライダーを 1 つに統合します。公式の原文は「全く同じ設定のPhysBone Colliderを1つに統合します。」です（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **どう使うか**: 「PhysBoneを最適化する」は、Trace And Optimize のチェックボックスで選びます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) を見てください）。AAO Merge PhysBone の「オーバーライド」でコライダーを Merge にすると、統合対象のコライダーの一覧をまとめられます（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。
- **注意**:
  - 衝突チェックの数への効果は、公式に記載なし（確かめた範囲: 日本語・英語の公式ドキュメントの全ページと CHANGELOG を、「衝突」「Collision」「Collision Check」「CollisionCheck」で検索し、ヒットなし）。
  - 公式の言い方は「全く同じ設定」だけです。どの条件まで統合されるかは、この記事では未確認です。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE 衝突チェックの数」の値と上限（PC の Poor が 512、Android の Poor が 64）を確かめます。
2. lilAvatarUtils の PhysBone タブで、「コリジョン数」が大きい PhysBone を見つけます。この数は lilAvatarUtils の項目なので、SDK の値とは別に読みます。
3. 同じ PhysBone の「コライダー数」と「ボーン数」を確かめます。
4. lilAvatarUtils の PBコライダー タブで「参照数」を見て、どの PhysBone からも参照されていないコライダーがないかを確かめます。
5. Allow Collision を False にするかを決めます。False にすると、Colliders のリストに入ったコライダーとだけ衝突します（公式の説明）。動きが変わるので、Play モードで確かめてください。
6. AAO の Trace And Optimize の「PhysBoneを最適化する」を使うときは、コライダーの統合が動きを変えないかを確かめてから使います。Merge PhysBone でコライダーをまとめるときは、統合対象のコライダーの一覧を確かめます。
7. Play モードのランク、または Build & Test で、SDK の値と見た目・動きを確かめます。

## 注意・うまくいかないとき

::: warning
- **無効のものも数えられます**: 公式の Performance Ranks には、「All GameObjects and Components, **including those that are currently disabled**, count towards the Avatar Performance Rank.」（無効化されているものも含めて数える）と書かれています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **Global Collision は 4 つまで（アバター）**: 公式の [PhysBones](https://creators.vrchat.com/common-components/physbones) のページには、アバターでは Global Collision を有効にした PhysBone コライダーが最大 4 つまでと書かれています。ワールドには上限がありません。上限を超えたときの動作は、未確認です。
- **Global Collision は Sphere と Capsule だけ**: 公式の [PhysBones](https://creators.vrchat.com/common-components/physbones) のページには、Global Collision は Sphere と Capsule のコライダーでのみサポートされると書かれています。
- **標準コライダーは性能ランクに影響しない**: 公式の [PhysBones](https://creators.vrchat.com/common-components/physbones) のページには、標準コライダー（Standard Colliders）は性能ランクに影響しないと書かれています。ただし、PhysBone コライダーの数に含まれるかどうかは、本文からは確定できないため、この記事では未確認です。
- **Android の上限**: モバイルの PhysBone 衝突チェックのハード上限は 64 です。公式の [Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) には、モバイルでハード上限を超えると、「Show Avatar」を選んでも、制限対象のコンポーネントがすべて取り除かれると書かれています。Android の表示の既定値は Medium で、Poor のアバターは表示されません。
- **AAO の版による修正**: CHANGELOG には、PhysBone コライダーの統合・削除に関する修正が何度かあります。1.9.12 では「MergePhysBone Collider may merge differently toggled PhysBoneColliders when colliders are targeting external bones」（外部のボーンを対象にするコライダーの統合で、切り替えの状態が違うものを統合することがある）、1.9.20 では「PhysBone Colliders can be removed incorrectly when there are other disabled PhysBone Colliders with same transform」（同じトランスフォームを持つ無効なコライダーがあると、正しくなく削除されることがある）が修正されています（[AAO の CHANGELOG](https://raw.githubusercontent.com/anatawa12/AvatarOptimizer/95c231830cefb62dde91e134642c70942f92ed2a/CHANGELOG.md)）。
- **Merge PhysBone の注意**: 統合すると、掴めるのは 1 か所だけになります。また、Merge PhysBone を 1 つ使うごとに、PhysBone トランスフォームの数が 1 増えます（ルートの GameObject も数えるため）（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。
:::

::: warning
**OSC で使う PhysBone・Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。公式の FAQ には、次のように書かれています。

- 「技術的な理由から、`AAO Trace and Optimize`コンポーネントはPhysBoneやContact ReceiverコンポーネントがOSCギミックで使用されているかどうかを判別することができません。」
- そのため、使われていないものと仮定して、ほかの用途で使われていなければ削除するよう設計されています。
- この仮定は正しいとは限らないため、Animator や Expression Menu を使わずに OSC ギミックで使う場合は、Asset Description ファイルを作成し、「Parameters Read By External Tools」を設定してください。

PhysBone が効かないときは、この設定を確かめてください（[よくある質問（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)、[Asset Description（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [コンタクトの数を減らす](/learn/optimize-physbone/contacts)
