---
title: PhysBone コンポーネントの数を減らす
reading: ふぃじっくぼーんこんぽーねんとのかずをへらす
description: PhysBone コンポーネントの数の上限と確かめ方、Avatar Optimizer（AAO）の Merge PhysBone と Trace And Optimize で減らす方法と注意点を、公式の資料に書かれた範囲でまとめます。
difficulty: 中級
updated: 2026-11-07
---

::: point
- PhysBone コンポーネントの上限は、PC の Poor が 32、Android の Poor が 8
- 無効にしたものも数えられるので、無効にするだけでは数は減らない
- AAO の Merge PhysBone で 1 つにまとめると、掴めるのは 1 か所だけになる
:::

PhysBone コンポーネントの数は、アバターにある PhysBone コンポーネントの数を数える項目です（VRChat 公式の定義は "The number of PhysBone components on the avatar." です）。上限は PC の Poor が 32、Android の Poor が 8 です（[パフォーマンスの内訳の見方](/learn/optimize-basics/read-the-breakdown) の表と同じ値）。

上限を超えると、公式の表では、表示される最低のランクを設定しているときに、その下限に届かないアバターの PhysBone・PhysBone コライダー・コンタクトが取り除かれると書かれています。Android では、ハード上限（8）を超えると、「Show Avatar」を選んでも回避できず、制限の対象のコンポーネントがすべて取り除かれると公式は書いています（[Avatar Performance Ranking System（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system) の英語の原文の要約）。

VRChat の公式ドキュメントは英語の原文だけなので、ここでの引用は日本語に訳した要約です。版が上がると挙動が変わることがあります。

## 減らす前に確かめること

- **SDK の「パフォーマンスの内訳」** の「PHYSBONE コンポーネントの数」で、今の値と上限を見ます。例の画面では、170（上限 32）と表示されていました。
- **無効にしたものも数えられます。** 公式は、「現在無効化されているものを含め、すべての GameObject とコンポーネントが、パフォーマンスランクに数えられる」（原文: "including those that are currently disabled"）と書いています（[パフォーマンスランク（VRChat 公式）](https://creators.vrchat.com/avatars/avatar-performance-ranking-system)）。使わない PhysBone は、無効にするだけでは数が減らない、という意味です。
- **lilAvatarUtils の PhysBone タブ**: 公式の説明では、「パフォーマンスランクへの影響が大きいPhysBoneコンポーネントの特定を行ったり、ルートボーンや設定がほぼ共通で統合できそうなPhysBoneを発見するのに役立ちます。」とされています（[PhysBones（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。
- **AAO を使うときのランク**: AAO の公式ドキュメント（[基本の使い方（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/tutorial/basic-usage/)）は、非破壊改変ツールを使う改変では、VRCSDK の Control Panel のパフォーマンスランクはあてにならなくなると書いています。Play モードに入ったときのランクは、[AAO の結果の確認](/learn/optimize-aao/avatar-optimizer) の節のとおり、Actual Performance Window で確かめます。

## 減らし方の考え方

VRChat 公式の PhysBones のページには、コンポーネントの使い方について次の勧めがあります（[PhysBones（VRChat 公式）](https://creators.vrchat.com/common-components/physbones)。日本語への訳は要約です）。

- 原文: "Because of the multi-threaded nature of PhysBones, it isn't always the most efficient to put all bones into a single chain." 訳: PhysBone はマルチスレッドで動くため、すべてのボーンを 1 つのチェーンにまとめるのが、いつも一番効率的とは限りません。
- 同じ箇所には、コンポーネントは少ないほうがよいとあり、アバターやワールドのオブジェクトに少数ある程度なら、Dynamic Bones のときほど悪くないとも書かれています。
- 原文: "if you're just dealing with something on the order of 32 bones... don't worry about it." 訳: 約 32 ボーン程度を扱うだけなら、気にしなくてよい。
- 公式は、これは厳密なルールではないとも明記しています（原文: "These aren't strict rules!"）。

公式の勧めは、「少ないほうがよい」という方向です。そのため、この記事の整理として、減らす方法を次の 2 つに分けます。

- **手動でまとめる**: AAO Merge PhysBone で、複数の PhysBone を 1 つにまとめる。
- **自動で整理する**: AAO Trace And Optimize の「PhysBone を最適化する」と「使われていないObjectを自動的に削除する」を使う。

## ツール別の方法

### lilAvatarUtils {.tool .tool-lil}

**何をするか**: PhysBones タブで、アバターにあるすべての PhysBone を一覧できます。一部の項目は編集でき、変更したものは左上の適用ボタンで一括して反映されます（[PhysBones（lilAvatarUtils 公式）](https://lilxyzw.github.io/lilAvatarUtils/ja_JP/docs/PhysBonesGUI)）。

**どう使うか**: 「親オブジェクト」の項目について、公式は「親が同じ場合はコンポーネントを統一してコンポーネント数を削減できる場合があります。」と書いています。統合できそうなものを見つける手がかりとして使えます。

### Avatar Optimizer（AAO） {.tool .tool-aao}

AAO の PhysBone の機能は、次の 3 つに分かれます。ほかに、コライダーの統合（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)）と、トランスフォームの置き換え（[PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)）があります。

**AAO Merge PhysBone（手動）**: 複数の PhysBone を 1 つにまとめます。公式の原文は「このコンポーネントは、複数のPhysBoneコンポーネントを1つに統合します。」で、統合後は MultiChildType が Ignore になると書かれています（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。

- **追加する場所**: 新規の GameObject に追加します（公式: 「このコンポーネントは新規GameObjectに追加してください。」）。
- **統合対象**: 公式は「統合対象は同じGameObjectの子である必要があります。」と書いています。代わりに「このGameObjectの子にする」を使うこともできます。
- **オーバーライド**: 各項目は、Copy（すべての統合対象で値が同じときだけ有効）か Override（新しい値を設定）から選べます。コライダーの Merge（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders) で説明）、Endpoint Position の Clear（[PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms) で説明）、角度制限の Fix も選べます。
- **統合後**: 掴めるのは 1 か所だけになります。公式は、PhysBone コンポーネント 1 つに対して掴めるのは 1 か所だけという VRChat の仕様があるためだと書いています。

**Trace And Optimize の「PhysBone を最適化する」（Optimize PhysBone Settings）**: 公式の原文は、「掴めないものなど、統合しても動作が変化しない複数のPhysBoneを1つに統合します。」です（[Trace And Optimize（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/trace-and-optimize/)）。英語版も、"if they can be merged without behavioral change, e.g. if they are not grabbable" と、統合しても動作が変わらないことを条件にしています。

- 公式の文言は、掴めない PhysBone などを対象にしています。「全く同じ設定」の統合が書かれているのは、コライダーについてです（[PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders) で説明）。
- CHANGELOG の 1.9.0 には、掴める PhysBone が見つからないときに、PhysBone を自動で統合する（"Automatically merge PhysBone when no grabbing PhysBone is detected"）と書かれています（[CHANGELOG（AAO）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/changelog/)）。
- このオプションには、Is Animated の整理（不要なときにチェックを外す）と、Endpoint Position の置き換え（[PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms) で説明）も含まれます。

**使われていないObjectを自動的に削除する**（Remove unused Objects）: 公式の原文は、「アニメーションなどを走査して、使われていないObject(GameObjectやコンポーネント、テクスチャなど)を自動的に削除します。」です。PhysBone については、次の記述があります。

- CHANGELOG の 1.7.0 には、パラメーターが定義されていても Animator Controller で使われていない PhysBone と Contact Receiver は削除される、と書かれています（英語の原文: "Remove Unused Objects now removes PhysBones and Contact Receivers with parameters defined but not used by Animator Controllers"）。
- 切り替えるオブジェクトだけで使われている PhysBone は、自動で切り替わるように書かれています（英語版: "if they are only used by toggled objects"）。

## 手順

1. SDK の「パフォーマンスの内訳」で、「PHYSBONE コンポーネントの数」を確かめます（上限は PC の Poor が 32、Android の Poor が 8）。
2. lilAvatarUtils の PhysBones タブで、パフォーマンスランクへの影響が大きい PhysBone を見つけます。「ルートボーン」や設定が近いものも探します。
3. OSC で使う PhysBone や Contact Receiver がある場合は、先に「注意・うまくいかないとき」の OSC の項目を読み、必要な Asset Description の設定を済ませます。そのうえで、アバターのルートに AAO Trace And Optimize を付け、「使われていないObjectを自動的に削除する」と「PhysBone を最適化する」が、チェックボックスでオンになっているかを確かめます。
4. まだ残るものは、AAO Merge PhysBone を新規の GameObject に追加します（公式:「このコンポーネントは新規GameObjectに追加してください。」）。統合対象の PhysBone は、上の「統合対象」の条件（同じ GameObject の子）を満たすように置きます。
5. Merge PhysBone の各項目（Copy か Override）を確かめます。統合後は、掴める場所が 1 か所になることを、先に確かめておきます。
6. Play モードに入って、パフォーマンスランクと、揺れ方や掴み方が変わっていないかを確かめます。

## 注意・うまくいかないとき

::: warning
- **OSC で使う PhysBone や Contact Receiver**: AAO の Trace And Optimize は、PhysBone や Contact Receiver が OSC（外部ツールと値をやりとりする仕組み）のギミックで使われているかを判別できません。そのため、ほかで使われていなければ削除するように設計されています。公式 FAQ（[よくある質問](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/faq/)）は、この仮定は正しいとは限らないと書いています。Animator や Expression Menu を使わずに、OSC のギミックで PhysBone や Contact Receiver を使う場合は、Asset Description ファイルを作り、Parameters Read By External Tools に設定するよう案内しています（[Asset Description（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/developers/asset-description/)）。OSC を使うアバターでは、自動の最適化のあとに、PhysBone と Contact が動くかを確かめてください（**この記事の助言**{.lbl .lbl-adv}）。
- **統合後は掴めるのが 1 か所だけ**: 統合前は、それぞれの PhysBone のボーンを同時に掴めますが、統合後は 1 つのボーンしか掴めません（[Merge PhysBone（AAO 公式）](https://vpm.anatawa12.com/avatar-optimizer/ja/docs/reference/merge-physbone/)）。
- **PhysBone トランスフォームの数が 1 つ増える**: Merge PhysBone を 1 つ使うごとに、ルートの GameObject も PhysBone トランスフォームに数えられるため、数が 1 増えます。公式は、これは VRChat のパフォーマンスランクの仕組みのバグである可能性がある、と書いています。減らす目的のときは、この増え方を考えてください（[PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)）。
- **Merge PhysBone の条件**: CHANGELOG の 1.7.0 には、統合元が 1 つだけのときはエラーになること、チェーンの長さがそろっていないと警告が出ることが書かれています。
- **修正の記録**: 1.9.11 には、自動の Merge PhysBone が、親の PhysBone があるときに動きを変えることがあった、という修正があります。1.9.13 には、別のコンポーネントでアニメーションされる親の Transform がある場合に、統合で動きが壊れることがあった、という修正があります。AAO のバージョンを確かめて使ってください。
:::

## 関連ページ

- [PhysBone トランスフォームの数を減らす](/learn/optimize-physbone/physbone-transforms)
- [PhysBone コライダーの数を減らす](/learn/optimize-physbone/physbone-colliders)
- [パフォーマンスの内訳の見方（どの項目が重いか）](/learn/optimize-basics/read-the-breakdown)
- [軽量化に使う 5 つのツールと早見表](/learn/optimize-basics/tools-overview)
- [Avatar Optimizer（AAO）でできること・使い方](/learn/optimize-aao/avatar-optimizer)
- [PhysBone と Contact](/learn/vrchat-features/physbone-contact)
- [パフォーマンスランク](/learn/vrchat/performance-rank)
