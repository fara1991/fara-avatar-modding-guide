---
title: 表情と FX の基礎
description: アバターの表情やギミックを動かす Animator（Playable Layers・FX レイヤー）の仕組みと、Write Defaults、レイヤーの優先順位、デフォルトの表情の変え方、まばたきとの干渉、動かないときの調べ方です。
difficulty: 中級
updated: 2026-10-11
---

## Playable Layers

VRChat のアバターは、VRC Avatar Descriptor の **Playable Layers** に入れた 5 つの Animator Controller で動きます。上から順に適用され、下ほど後から上書きします。

| レイヤー | 何を動かすか |
|---|---|
| Base | 歩く・走る・しゃがむなどの移動のアニメーション（ボーンの動きだけ） |
| Additive | Base に重ねる動き（呼吸など） |
| Gesture | 手の形（ハンドジェスチャー） |
| Action | 体全体を上書きする動き（エモートなど） |
| FX | ボーン以外のすべて。**表情のシェイプキー**、オブジェクトの表示・非表示、マテリアルの変更など |

改変で触ることが多いのは **FX** です。表情もトグルも、たいてい FX のレイヤーで動いています。Animator ウィンドウ（**Window > Animation > Animator**）で FX の Animator Controller を開くと、レイヤーとステートが見られます。

## 表情はどう動いているか

多くのアバターの FX には、左手・右手のレイヤー（`Left Hand`、`Right Hand` など）があり、手の形（`GestureLeft` / `GestureRight` の値 0〜7）に応じて表情のアニメーションに切り替わります。

| 値 | 手の形 |
|---|---|
| 0 | 何もしない（Neutral） |
| 1 | 握りこぶし（Fist） |
| 2 | 手を開く（HandOpen） |
| 3 | 指さし（FingerPoint） |
| 4 | ピース（Victory） |
| 5 | ロックンロール（RockNRoll） |
| 6 | 手でピストル（HandGun） |
| 7 | 親指を立てる（ThumbsUp） |

表情のアニメーションは「顔のメッシュ名 / シェイプキー名」に値を書き込むものです。顔のメッシュ名やシェイプキー名を変えると、表情が動かなくなります（[Q&A](/qa#merged-mesh-expression-broken)）。

## デフォルトの表情を変える

Unity の Hierarchy で顔のシェイプキーを変えても、VRChat では元に戻ります。FX の手のレイヤーの待機状態（`Idle` など）で、表情を既定の値に戻すアニメーションが常に流れているためです（[Q&A: デフォルトの表情が戻る](/qa#default-face-reverts)）。

1. FX の Animator Controller を開き、手のレイヤーの待機のステートを選ぶ
2. Inspector の **Motion** に入っているアニメーション（`Face Default` のような名前）を確かめる
3. そのアニメーションを Animation ウィンドウで開き、変えたいシェイプキーの値を書き換える

元のアニメーションを直接書き換えると、アバターを入れ直したときに戻ります。複製したアニメーションを書き換えて、ステートの Motion を差し替える方が安全です。

## 目を閉じる表情とまばたき

目を閉じる（半目にする）表情を作っても、VRChat の **自動まばたき** と重なると、目が閉じすぎたり、表情が効かなかったりします。そのため多くのアバターは、表情のステートで自動まばたきを止めています（[Q&A: 目閉じが反映されない](/qa#expression-blink-not-working)）。

- 表情のステートに **VRC Animator Tracking Control** が付いていて、**Eyes & Eyelids** を **Animation** にしていると、その間はまばたきが止まる
- アバターによっては、独自のパラメーター（Avatar Parameter Driver）でまばたきを止めている
- 自分で目閉じの表情を足すときは、ほかの表情のステートと同じ設定にそろえる

## Write Defaults

**Write Defaults**（WD）は、ステートごとの設定で、「そのアニメーションで動かしていない値を、既定の値に戻すかどうか」を決めます（[用語集: Write Defaults](/glossary#write-defaults)）。

- VRChat の公式の Animator は **Off** で作られている
- 公式は、アバター全体で **On か Off のどちらかにそろえる** ことをすすめている。混ざっていると、思わぬ値が書き込まれる
- Motion が空（None）のステートや、空のアニメーションは、WD が On のように振る舞う

ギミックを足したら表情やトグルがおかしくなったときは、まず WD の混在を疑います。Modular Avatar の **MA Merge Animator** には、追加するアニメーターの WD をアバターに合わせる設定（**アバターの Write Defaults 設定に合わせる**）があります。

## レイヤーの優先順位

同じ値（同じオブジェクトの表示、同じシェイプキーなど）を複数のレイヤーが動かすときは、**下のレイヤーが勝ちます**（[TIPS](/tips#animator-layer-priority)）。

- 全部を元に戻す（リセットする）レイヤーは上に、個別に ON にするレイヤーは下に置く
- 「何をしても変わらない」ときは、下のレイヤーが同じ値を上書きしていないかを見る

## アニメーションの参照が切れたとき

オブジェクトの名前や階層を変えると、そのオブジェクトを動かしていたアニメーションのプロパティが **(Missing!)**（黄色）になります。Animation ウィンドウで、アバターを選んだ状態でそのクリップを開き、Missing のプロパティを選んで <kbd>F2</kbd> を押すと、パス（名前）を書き換えて直せます（[TIPS](/tips#animation-missing-path-f2)）。

## 動かないときの調べ方

| 方法 | 見られること |
|---|---|
| Unity の Play モード + Animator ウィンドウ | どのステートにいるか（青いバー）。パラメーターを手で変えて遷移を試せる |
| Gesture Manager などのエミュレーター | Unity の中で、VRChat のメニューや手の形を再現して試せる |
| VRChat のアクションメニューのデバッグ表示 | VRChat の中で、パラメーターの値の変化を見られる |
| Build & Test | 実際の VRChat での動き |

よくある原因は次のとおりです（[Q&A](/qa#animator-null-motion-stops)）。

- ステートの Motion が空になっている
- コピーしたつもりの Animator Controller が、別のアバターのものを書き換えていた
- パラメーター名の大文字・小文字が、Expression Parameters と Animator で違う
- 遷移の条件（Conditions）が間違っている、または Has Exit Time のせいで切り替わりが遅れる

## 自分で作る前に

表情やトグルを Animator で一から作るのは手間がかかり、WD の混在などの事故も起きやすくなります。オブジェクトやシェイプキーの切り替えなら、Modular Avatar の Object Toggle や Shape Changer で、アニメーションを書かずに作れます（[Expression メニューとパラメーター](/learn/vrchat-features/expressions)）。
