---
title: PhysBone 衝突チェックの数を減らす
reading: ふぃじっくぼーんのしょうとつちぇっくのかずをへらす
description: VRChat のパフォーマンス表の「PhysBone 衝突チェックの数」が何を数えるか、上限、減らし方と注意、ツールごとの挙動をまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- PhysBone 衝突チェックの上限は、PC の Poor が 512、Android の Poor が 64
- 衝突チェックの数は、コライダーごとに影響しうる PhysBone トランスフォームの数を足し合わせた値
- AAO の Trace And Optimize は、全く同じ設定の PhysBone コライダーを 1 つに統合する
:::

PhysBone 衝突チェックの数は、アバターの PhysBone コライダーごとに、影響しうる PhysBone トランスフォームの数を足し合わせた値です（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の定義）。1 つのトランスフォームが複数のコライダーに影響されると、その分だけ重ねて数えられます。上限は、PC の Poor が 512、Android の Poor が 64 です。

| 項目 | PC（Excellent / Good / Medium / Poor） | Android（Excellent / Good / Medium / Poor） |
|---|---|---|
| PhysBone 衝突チェックの数 | 32 / 128 / 256 / 512 | 0 / 16 / 32 / 64 |

上限は、[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値です。超えると、表示ランクの下限（Minimum Displayed Performance Rank）を下回るアバターでは、PhysBone・PhysBone コライダー・コンタクトのコンポーネントがすべて取り除かれます（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。Android では、硬い上限を超えると、「Show Avatar」を選んでも制限対象のコンポーネントがすべて取り除かれます（注意の節を見てください）。

版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」**: 「PHYSBONE 衝突チェックの数」で、今の値と上限を見ます。
- **lilAvatarUtils の PhysBone タブ**: PhysBone ごとに「コリジョン数」を見られます。「コリジョン数」は衝突判定の計算回数で、ボーン数やコライダー数に応じて増えていき、数字が増えるほど負荷も増えます（[lilAvatarUtils の PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。この記事では、lilAvatarUtils の「コリジョン数」を、SDK の「PHYSBONE 衝突チェックの数」とは別の値として扱います。
- **同じタブの「ボーン数」と「コライダー数」**: 「ボーン数」は、揺れが計算されるボーン数です。「コライダー数」は、この PhysBone との当たり判定を計算するコライダー数です（[lilAvatarUtils の PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。
- **Play モードのランク**: 非破壊改変ツールを使うと、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなります（[基本の使い方](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）。Play モードのランクの確かめ方は、[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) の「結果の確認」を見てください。

## 減らし方の考え方

- **同じ設定のコライダーをまとめる**: AAO の Trace And Optimize で、全く同じ設定の PhysBone コライダーを 1 つにまとめます。
- **コライダーの数を減らす**: [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders) を見てください。
- **影響するトランスフォームを減らす**: [PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms) を見てください。

## ツール別の方法

### lilAvatarUtils {.tool .tool-lil}

- **何をするか**: PhysBone タブで、アバターの PhysBone の一覧を確認できます。PBコライダー タブでは、コライダーの一覧と「参照数」（このコライダーを参照している PhysBone の数）を確認できます（[lilAvatarUtils の PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)・[PBコライダー](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。
- **どう使うか**: 「コリジョン数」が大きい PhysBone を見つける候補にします。PhysBone タブは、パフォーマンスランクへの影響が大きい PhysBone コンポーネントの特定に役立ちます。PBコライダー タブは、どこからも参照されない無駄なコライダーの削減に役立ちます（[lilAvatarUtils の PhysBones](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)・[PBコライダー](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBoneCollidersGUI)）。
- **注意**: 「コリジョン数」は lilAvatarUtils の項目名で、SDK の「PHYSBONE 衝突チェックの数」とは別の値として扱います。

### Avatar Optimizer（AAO） {.tool .tool-aao}

- **何をするか**: Trace And Optimize の「PhysBone を最適化する」（Optimize PhysBone Settings）は、全く同じ設定の PhysBone コライダーを 1 つに統合します（[Trace And Optimize](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。
- **どう使うか**: 「PhysBone を最適化する」は、Trace And Optimize のチェックボックスで選びます（[Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer) を見てください）。AAO Merge PhysBone の「オーバーライド」でコライダーを Merge にすると、統合対象のコライダーの一覧をまとめられます（[Merge PhysBone](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。
- **注意**: 統合の条件は「全く同じ設定」です。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE 衝突チェックの数」の値と上限（PC の Poor が 512、Android の Poor が 64）を確かめます。
2. lilAvatarUtils の PhysBone タブで、「コリジョン数」が大きい PhysBone を見つけます。この数は lilAvatarUtils の項目なので、SDK の値とは別に読みます。
3. 同じ PhysBone の「コライダー数」と「ボーン数」を確かめます。
4. lilAvatarUtils の PBコライダー タブで「参照数」を見て、どの PhysBone からも参照されていないコライダーがないかを確かめます。
5. OSC で使う PhysBone や Contact Receiver がある場合は、先に「注意・うまくいかないとき」の OSC の項目を読んで、必要な設定を済ませます。AAO の Trace And Optimize の「PhysBone を最適化する」を使うときは、コライダーの統合が動きを変えないかを確かめてから使います。Merge PhysBone でコライダーをまとめるときは、統合対象のコライダーの一覧を確かめます。
6. Play モードのランク、または Build & Test で、SDK の値と見た目・動きを確かめます。

## 注意・うまくいかないとき

::: warning
- **無効にしたものも、ランクの数に入ります**: 無効化されているものも含めて、すべての GameObject とコンポーネントが、Avatar Performance Rank に数えられます（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。
- **Allow Collision**: Allow Collision を False にすると、この PhysBone は Colliders のリストに入ったコライダーとだけ衝突します（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。衝突の対象が変わるので、動きも変わります。Play モードで確かめてください。
- **Global Collision は 4 つまで（アバター）**: アバターでは、Global Collision を有効にした PhysBone コライダーは最大 4 つまでです。ワールドには上限がありません（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **Global Collision は Sphere と Capsule だけ**: Global Collision は、Sphere と Capsule のコライダーでのみ対応します（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **標準コライダーは性能ランクに影響しない**: 標準コライダー（Standard Colliders）は、性能ランクに影響しません（[PhysBones](https://creators.vrchat.com/common-components/physbones)）。
- **Android の上限**: モバイルの PhysBone 衝突チェックの硬い上限は 64 です。硬い上限を超えると、「Show Avatar」を選んでも、制限対象のコンポーネントがすべて取り除かれます。Android の表示の既定値は Medium で、Poor と Very Poor のアバターは表示されません（[Avatar Performance Ranking System](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の「Mobile Default Performance Rank Blocking」の節）。
- **AAO の版による修正**: PhysBone コライダーの統合・削除に関する修正が、何度かあります。AAO 1.9.12 では、外部のボーンを対象にするコライダーの統合で、切り替えの状態が違うものを統合することがある問題が修正されました。AAO 1.9.20 では、同じトランスフォームを持つ無効な PhysBone コライダーがほかにあると、コライダーが正しくなく削除されることがある問題が修正されました（[AAO の CHANGELOG](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。
- **Merge PhysBone の注意**: 統合すると、掴めるのは 1 か所だけになります。また、Merge PhysBone を 1 つ使うごとに、PhysBone トランスフォームの数が 1 増えます（ルートの GameObject も数えるため）（[Merge PhysBone](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。
:::

::: warning
**OSC で使う PhysBone・Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、使われていないものと仮定して、ほかの用途で使われていなければ削除するように設計されています。この仮定は正しいとは限らないため、Animator や Expression Menu を使わずに OSC ギミックで使う場合は、Asset Description ファイルを作成し、「Parameters Read By External Tools」を設定するとよいです（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)、[Asset Description](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。PhysBone が効かないときは、この設定を確かめてください。
:::

## 関連ページ

- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
- [アバターを軽くする](/learn/vrchat/optimization)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [コンタクトの数を減らす](/learn/optimize-physbone/contacts)
