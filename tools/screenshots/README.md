# 記事用スクリーンショットの自動撮影

`Content/learn/<category>/images/*.png` は、Blender を自動操作して撮っています。記事の手順を変えたとき、Blender のバージョンを上げたときに撮り直せます。

## 使い方（Windows）

```powershell
.\tools\screenshots\capture.ps1                      # 全部（1〜2 分。Blender のウィンドウが開いて勝手に動く）
.\tools\screenshots\capture.ps1 -Only loopcut,mirror-half   # 名前を指定して撮り直す
```

- Blender 4.5 が `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe` にある前提。違う場所なら `-Blender` で指定
- 切り抜きと番号付けに Windows の Python と Pillow を使う: `py -3 -m pip install pillow`
- 普段の Blender の設定は変更しない（`%TEMP%\fara-avatar-guide-shots\config` に撮影用の設定を作る）
- 撮影中はマウスやキーボードに触らない（メニューを開いて撮るショットがあるため）

## 仕組み

| ファイル | 役割 |
|---|---|
| `capture.ps1` | 撮影用の設定を用意し、Blender と後処理を順に実行する |
| `mkprefs.py` | 日本語 UI・スプラッシュなしの `userpref.blend` を作る |
| `capture.py` | ショットの定義と撮影本体。`@shot(カテゴリ, 名前, ...)` で登録した関数がシーンを組み立て、指定エリアを撮る |
| `postprocess.py` | `post.json` に従って切り抜き・番号付けを行う |

ショットを足すときは `capture.py` の末尾付近に `@shot(...)` 付きの関数を追加し、記事から `images/<名前>.png` で参照します。

- `area="WINDOW"` でウィンドウ全体、`"VIEW_3D"` / `"PROPERTIES"` / `"OUTLINER"` などでそのエリアだけ
- `menu="VIEW3D_MT_add"` でメニューを開いた状態を撮る（`pie=True` でパイメニュー）
- `props="MODIFIER"` でプロパティエディターのタブを切り替える
- `window="FILE"` / `"PREFS"` で、ファイルブラウザやプリファレンスのウィンドウを撮る
- 戻り値の `crop` / `labels` / `crop_fn` で後処理を指定する

記事の例に使う人型モデル（`build_mannequin`）とシャツ（`build_shirt`）はスクリプトの中で組み立てているので、外部のファイルは要りません。
