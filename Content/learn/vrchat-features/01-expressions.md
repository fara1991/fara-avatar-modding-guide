---
title: Expression メニューとパラメーター
description: VRChat の中で開くメニュー（Expression Menu）と、その状態を持つパラメーター（Expression Parameters）の基本と、Modular Avatar で衣装や小物の ON/OFF を作る方法です。
difficulty: 初級
updated: 2026-10-11
---

## 3 つの部品

VRChat のアバターのギミック（衣装の ON/OFF、表情の切り替えなど）は、次の 3 つでできています（[用語集: Expressions](/glossary#expressions)）。

| 部品 | 役割 | 例 |
|---|---|---|
| **Expression Menu** | VRChat の中で開くメニュー（アクションメニュー）の項目 | 「帽子」のトグル |
| **Expression Parameters** | メニューで変わる値。ほかの人にも同期できる | `Hat`（Bool） |
| **Animator（FX レイヤー）** | 値に応じてアバターを変えるアニメーション | `Hat` が true なら帽子を表示 |

メニューで値を変える → 値に応じて FX のアニメーションが切り替わる → アバターの見た目が変わる、という流れです。3 つはアバターの **VRC Avatar Descriptor** の **Expressions** と **Playable Layers** で指定します。

自分で 3 つとも作ることもできますが、**Modular Avatar を使えば、オブジェクトの ON/OFF などはアニメーションもパラメーターも書かずに作れます**。まずは下の「Modular Avatar で ON/OFF を作る」から始めるのがおすすめです。

## Modular Avatar で ON/OFF を作る

帽子や上着を、メニューから出し入れできるようにします。

1. Hierarchy でアバター（ルート）を右クリックし、**Modular Avatar > Create Toggle** を選ぶ
2. アバターの中に、トグル用のオブジェクトが作られる。名前をメニューに出したい名前（「帽子」など）に変える
3. 作られたオブジェクトの Inspector の **MA Object Toggle** で **+** を押し、切り替えたいオブジェクト（帽子）を入れる
   - チェックが **付いている** と、メニューを ON にしたときに **表示** される
   - チェックを **外す** と、メニューを ON にしたときに **隠れる**
4. **MA Menu Item** の設定で、最初の状態や保存を決める（下の表）
5. Build & Test で、VRChat の中でメニューを開いて確かめる

作られたオブジェクトには、**MA Menu Installer**（メニューへの組み込み）、**MA Menu Item**（メニューの項目）、**MA Object Toggle**（切り替える対象）の 3 つが付いています。アップロードのときに、Modular Avatar が Expression Menu・Parameters・FX のアニメーションを自動で作って組み込みます。元のメニューやアニメーターは書き換わりません。

| MA Menu Item の項目（日本語 UI） | 意味 |
|---|---|
| 初期設定にする（Is Default） | アバターを読み込んだときに ON にしておくか |
| 保存する（Saved） | ワールドを移動したりアバターを着直したりしても、状態を覚えておくか |
| 同期する（Synced） | ほかの人にも同じ状態が見えるか。普通はオン |

::: tip
帽子と、帽子をかぶったときだけ隠したい髪のように、複数のものを同時に切り替えるときは、Object Toggle に両方を入れて、片方のチェックだけを外します。複数のオブジェクトを選んで **Modular Avatar > Create Toggle for Selection** を使うと、選んだものごとのトグルをまとめて作れます。
:::

Modular Avatar には、オブジェクトの ON/OFF のほかに、シェイプキーを変える **MA Shape Changer**、マテリアルを替える **MA Material Setter** などもあり、同じようにメニュー項目と組み合わせて使えます。詳しくは [Modular Avatar のドキュメント](https://modular-avatar.nadena.dev/ja/docs/tutorials/object_toggle) を見てください。

## Expression Menu

VRChat のアクションメニュー（円形のメニュー）に並ぶ項目です。1 つのメニューに置ける項目は **8 個まで** で、それ以上はサブメニューに分けます（Modular Avatar のメニューは、あふれた分を自動で「次へ」に送ります）。

| 項目の種類（Type） | 動き | 使いどころ |
|---|---|---|
| Toggle | 押すたびに ON / OFF | 衣装・小物の出し入れ |
| Button | 押している間だけ ON | 一時的なエフェクト |
| Sub Menu | 別のメニューを開く | 項目の整理 |
| Two Axis Puppet | スティックの 2 方向で 2 つの値を動かす | 表情のブレンド |
| Four Axis Puppet | スティックの 4 方向で 4 つの値を動かす | 表情のブレンド |
| Radial Puppet | 円を回して 0〜1 の値を動かす | 明るさ、体形の調整 |

## Expression Parameters

メニューやアニメーションで使う値の一覧です。

| 型 | 値 | 同期に使う量 |
|---|---|---|
| Bool | true / false | 1 ビット |
| Int | 0〜255 | 8 ビット |
| Float | -1.0〜1.0 | 8 ビット |

- 各パラメーターには **Default**（初期値）、**Saved**（保存する）、**Synced**（ほかの人に同期する）がある
- **同期する** パラメーターの合計は **256 ビットまで**。Bool なら 256 個、Int・Float なら 32 個ぶん。ギミックを足していくと、ここが足りなくなることがある
- 同期しない（自分の中だけで使う）パラメーターは、この合計に入らない
- VRChat が最初から用意している値（`GestureLeft`、`IsLocal`、`Viseme`、`AFK` など）は、Animator にパラメーターを足すだけで使え、合計にも入らない

値の詳細は公式の [Animator Parameters](https://creators.vrchat.com/avatars/animator-parameters/) と [Expression Menu and Controls](https://creators.vrchat.com/avatars/expression-menu-and-controls/) を見てください。

::: warning
ギミックを入れたらメニューが出ない・動かないときは、まず Builder の警告で **同期パラメーターの合計が 256 を超えていないか** を見ます。超えているとアップロードできません。使わないギミックを外すか、同期しなくてよいものの Synced を外します。
:::

## メニューを手で作る場合

Modular Avatar を使わずに作る場合は、次の 4 つを自分で用意します。

1. Project で右クリック > **Create > VRChat > Avatars > Expression Parameters** でパラメーターを作り、`Hat`（Bool）を足す
2. **Create > VRChat > Avatars > Expressions Menu** でメニューを作り、Toggle の項目を足して Parameter に `Hat` を選ぶ
3. FX の Animator Controller に `Hat` パラメーターを足し、帽子を表示する・隠すアニメーションのステートと、`Hat` で切り替わる遷移を作る
4. VRC Avatar Descriptor の **Expressions** にメニューとパラメーターを、**Playable Layers** の FX に Animator Controller を入れる

FX のレイヤーや Write Defaults の考え方は [表情と FX の基礎](/learn/vrchat-features/fx-basics) にあります。

## うまくいかないとき

| 症状 | 確かめること |
|---|---|
| メニューに項目が出ない | Menu Item が Menu Installer と同じオブジェクト（または Menu Group の中）にあるか。アバターの中にあるか |
| 押しても見た目が変わらない | Object Toggle に対象を入れたか。チェックの向き（表示 / 隠す）が逆になっていないか |
| 自分には見えるがほかの人には見えない | 同期する（Synced）がオフになっていないか |
| ワールドを移動すると戻る | 保存する（Saved）がオフになっていないか |
| 手で作ったトグルだけ動かない | FX のレイヤー・パラメーター名・アニメーションのパス・Write Defaults（[表情と FX の基礎](/learn/vrchat-features/fx-basics)） |

[何て名前？: 衣装や小物をメニューで ON / OFF したい](/howto#toggle-objects) にも手順をまとめています。
