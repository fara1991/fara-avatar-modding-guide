---
title: PhysBone と Contact
reading: ふぃじっくぼーんとこんたくと
description: 髪やスカートを揺らす PhysBone の考え方（ボーンは点、Root Transform、Endpoint Position、Multi Child Type）、揺れ方の調整、コライダー、掴めないときの確認と、触れたことを検知する Contact の基本です。
difficulty: 中級
updated: 2026-10-11
---

## PhysBone とは

**VRC Phys Bone** は、髪・スカート・しっぽなどのボーンを、重力や動きに合わせて揺らす VRChat のコンポーネントです（[用語集: PhysBone](/glossary#physbone)）。ほかの人が掴んだり、ポーズを付けたりもできます。以前は有料アセットの DynamicBone が使われていましたが、今は PhysBone が標準です。

多くのアバターには最初から設定されているので、改変で触るのは「衣装の揺れものを足す」「揺れ方を変える」「数を減らす」ときです。

## 考え方: ボーンは「点」

Blender のボーンには長さがありますが、Unity のボーンは **位置と向きを持った点** です。ボーンの向き（どちらに伸びているか）は、**次の点（子ボーン）がどこにあるか** で決まります（[用語集: リーフボーン](/glossary#leaf-bone)）。

- 子のないボーン 1 本に PhysBone を付けても、どちらに伸びているかがわからないので **揺れない**
- 末端に `〜_end` のような点（リーフボーン）があれば、その手前のボーンまで揺れる
- リーフボーンが無いときは、PhysBone の **Endpoint Position** で、末端の点を仮想的に足す（例: Y を 0.1 にすると、最後のボーンの先に 10 cm の点ができる）

## 主な項目

### Transforms（どのボーンを揺らすか）

| 項目 | 意味 |
|---|---|
| Root Transform | 揺らすボーンの根元。**ここに指定したボーンより下** が揺れる。空欄なら、PhysBone を付けたオブジェクト自身が根元になる |
| Ignore Transforms | 揺らさないボーン（とその子）を外す |
| Endpoint Position | 末端に仮想の点を足す（上の「ボーンは点」） |
| Multi Child Type | 根元の直下に子が複数あるとき、根元をどう扱うか。**Ignore**（根元は動かさない、既定）・**First**（最初の子に合わせる）・**Average**（平均） |

Root Transform に指定したボーン自体は動きません。揺れるのはその子からです。根元から揺らしたいのに揺れないときは、Root Transform に 1 つ上のボーンを指定しているか確かめます（[Q&A: Root Transform・Endpoint Position・Multi Child Type](/qa#physbone-root-endpoint)）。

### Forces（揺れ方）

| 項目 | 意味 | 調整の目安 |
|---|---|---|
| Integration Type | 計算の種類。**Simplified**（簡単）か **Advanced**（細かく調整できる） | 迷ったら Simplified |
| Pull | 元の形に戻ろうとする力 | 大きいほど硬く、すぐ戻る |
| Spring（Simplified） / Momentum（Advanced） | 揺れの続きやすさ | 大きいほど揺れが長く続く |
| Stiffness（Advanced） | 元の向きを保とうとする強さ | 髪の形を崩したくないときに |
| Gravity | 下に引っ張る力 | 長い髪・スカートは少し入れる |
| Gravity Falloff | 元の形が下を向いているほど重力を弱める | 垂れた髪が伸びきらないように |
| Immobile | アバター自身の移動の影響をどれだけ無視するか | 歩くと暴れるときに上げる |

髪が揺れない・暴れるときの調整の目安は [TIPS: PhysBone が揺れない・揺れすぎる](/tips#physbone-not-swinging) にあります。

### Limits（動ける範囲）

**Angle**（円すいの範囲）、**Hinge**（1 方向だけに曲がる）、**Polar** などで、ボーンが動ける角度を制限します。スカートが脚に食い込む、髪が顔に刺さるときに使います。根元のボーンだけ角度 0 にして固定する、といった使い方もできます。

### Collision（ぶつかる）

**Radius** でボーンの太さを決め、**Colliders** にコライダー（VRC Phys Bone Collider）を入れると、そのコライダーに当たって止まります。

- コライダーの形は **Sphere**（球）・**Capsule**（カプセル）・**Plane**（平面）
- ボーンをコライダーの内側に閉じ込めたいときは、コライダーの **Inside Bounds** をオンにする
- 細い紐やスカートがどうしても脚を貫通するときは、大きめのコライダー 2 つで V 字の谷を作り、ボーンをその上に乗せる方法もある（[TIPS](/tips#collider-v-shape)）

### Grab & Pose（掴む・ポーズ）

**Allow Grabbing**（掴める）、**Allow Posing**（掴んだ形のまま固定できる）を決めます。自分だけ・ほかの人だけ、に絞ることもできます。

### Options（パラメーター）

**Parameter** に名前（例: `Tail`）を入れると、`Tail_IsGrabbed`（掴まれているか）、`Tail_Angle`（曲がり具合）、`Tail_Stretch` などの値が自動で作られ、FX のアニメーションで使えます。しっぽを掴まれたら表情を変える、といったギミックに使います。

## 数を減らす

PhysBone のコンポーネント数・動く Transform の数・コライダーの数は、パフォーマンスランクに入ります（PC では PhysBone のコンポーネント 8 個までが Good の目安。[パフォーマンスランク](/learn/vrchat/performance-rank)）。

- 髪の束ごとに付いている PhysBone は、空のオブジェクトの下にまとめて 1 つにする（[TIPS: 髪の PhysBone をまとめる](/tips#physbone-merge-components)）。まとめた空のオブジェクトの直下に子が複数できるので、**Multi Child Type** の扱いに注意する
- 1 つの PhysBone で動かせる Transform は **256 個まで**
- Avatar Optimizer の **Merge PhysBone** や Trace And Optimize でも整理できる（[アバターを軽くする](/learn/vrchat/optimization)）

## うまくいかないとき

| 症状 | 確かめること | 関連 |
|---|---|---|
| まったく揺れない | Root Transform の位置、子のない単一ボーンでないか（Endpoint Position）、Pull が大きすぎないか | [Q&A](/qa#physbone-root-endpoint) |
| 衣装を着せたら揺れなくなった | PhysBone が消えていないか、無効になっていないか、FX のアニメーションで OFF にされていないか | [Q&A](/qa#physbone-lost-after-dressing) |
| Unity では掴めるのに VRChat で掴めない | Allow Grabbing、アニメーションでの切り替え、SDK のバージョン | [Q&A](/qa#physbone-grab-not-working) |
| 最初に隠していたものを出すと位置がずれる | 最初は表示しておき、アニメーションで隠す | [Q&A](/qa#physbone-initially-off-object) |
| 胸の揺れが大きすぎる | Pull・Spring の調整。Constraint と組み合わせて揺れを弱める方法もある | [TIPS](/tips#breast-physbone-dampen) |

Unity の Play モードで揺れを確かめながら値を調整できますが、Play モードを抜けると値は元に戻ります。調整した値を残す方法は [TIPS](/tips#save-playmode-values-prefab) を見てください。

## Contact（触れたことを検知する）

**Contact** は、アバターのどこかが「触れた」ことを検知して、パラメーターの値に変える仕組みです。頭をなでられたら表情を変える、手を合わせたらエフェクトを出す、といったギミックに使います。

| コンポーネント | 役割 |
|---|---|
| **VRC Contact Sender** | 「触れる側」。形（球・カプセル）と **Collision Tags**（どの種類の接触か）を持つ |
| **VRC Contact Receiver** | 「触れられる側」。同じ Collision Tags の Sender が入ってくると、**Parameter** に値を書き込む |

Receiver の **Receiver Type** で、値の出し方を選びます。

| Receiver Type | 値 |
|---|---|
| Constant | 触れている間 1（true） |
| OnEnter | 触れた瞬間だけ 1 |
| Proximity | 近づくほど 0 → 1 に増える（Float） |

- VRChat はすべてのアバターに、頭・胴・手・足・指の Sender を最初から持たせている。Receiver の Collision Tags でそれらを選べば、自分で Sender を作らなくても「手で触れられた」を検知できる
- **Allow Self**（自分の体で反応する）、**Allow Others**（ほかの人で反応する）で、誰に反応するかを決める
- Contact の数もパフォーマンスランクに入る。**Local Only**（自分の中だけで検知）にしたものは、ランクに入らない

作った値を表情などにつなぐには、FX のアニメーションが必要です（[表情と FX の基礎](/learn/vrchat-features/fx-basics)）。項目の詳細は公式の [PhysBones](https://creators.vrchat.com/avatars/avatar-dynamics/physbones/) と [Contacts](https://creators.vrchat.com/avatars/avatar-dynamics/contacts/) を見てください。

## Constraint

補助ボーンをほかのボーンの動きに追従させるには、**VRC Constraint**（VRC Parent Constraint、VRC Rotation Constraint など）を使います。Unity の Constraint はゲーム内で自動で VRChat の Constraint に変換されますが、Builder の **Auto Fix** で、アップロード前に変換しておくのがおすすめです。Android では Unity の Constraint は動きません（[Android（Quest）対応](/learn/vrchat/android)）。
