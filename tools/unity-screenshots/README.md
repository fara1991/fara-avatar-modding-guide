# Unity の画面のスクリーンショットを撮る

記事に載せる Unity の画面（Inspector・全体）を、**新しく作った VRChat 用のプロジェクト**で撮る手順です。普段使いのプロジェクト（アバター・衣装・ワールドなど、個人の作業）は使いません。写る画面に、個人のアセットやアカウントの情報が入らないようにするためです。

## 流れ

1. `make-project.ps1 -Project <新しい場所>`: VCC のアバター用テンプレートから、新しいプロジェクトを作る。VRChat SDK・NDMF・Modular Avatar・AAO・APL は、VCC の手元のキャッシュ（`%LOCALAPPDATA%\VRChatCreatorCompanion\Repos`）から展開する（記事の版は、スクリプトの中の `$packages`）。リポジトリの外（`E:\Projects\fara\Temp\` の下など）に作る。
2. `capture-unity.ps1`: そのプロジェクトを Unity 2022.3.22f1 で開き、`ShotRunner.cs`（プロジェクトの `Assets/Editor` に入る）がシーンを作って、準備ができるたびに合図（`ready-<名前>.flag`）を出す。スクリプトは、**起動した Unity の PID のウィンドウだけ**を `PrintWindow` で画像にして、合図を返す（`ack-<名前>.flag`）。終わったら Unity は自分で終了する（終了しないときは、自分が起動した PID だけを止める）。
3. `process.ps1`: 撮った画像から、Inspector を切り抜き、全体の画像はアカウントのボタンを隠して、ペインに番号と枠を付ける。記事の `images/` に書き出す。
4. 撮った画像は、**1 枚ずつ画像で見て**、個人の情報（アカウント名・パス・通知など）や、他のアプリが写っていないかを確かめてから、記事に載せる。

## 注意

- 撮影の間（数十秒）、Unity のウィンドウが画面の上に出る（最前面・フォーカスは奪わない）。見えていないウィンドウは、Unity が描画しないため。PC を使っているときは、あらかじめ伝える。
- Unity の起動時に、組み込みのシェーダーのコンパイルが失敗して、画面が全面マゼンタになることが、まれにある。`capture-unity.ps1` は、マゼンタなら撮り直す。直らないときは、もう一度実行する。
- スクリプトは ASCII のみで書く（Windows PowerShell 5.1 は、文字コードの指定がないファイルを ANSI で読み、日本語のコメントが構文エラーの原因になる）。
- Unity 内の表示は、OS の言語に合わせて日本語になる。記事は、画面の表示（日本語）と、公式の名前（英語）の対応を、パッケージの表示名の資源（`Resources/InspectorContext_*.asset` など）で確かめて書く。

## 衣装の記事（avatar-clothing/06）の画面

`ShotRunnerClothing.cs` が、仮の服の FBX（`make-shirt-fbx.py` で Blender から書き出す）の Rig タブと、MA Merge Armature・MA Blendshape Sync の Inspector を撮る。

1. `make-project.ps1 -Project <新しい場所> -Runner ShotRunnerClothing.cs`
2. `blender -b --factory-startup --python make-shirt-fbx.py -- <出力先>\Shirt.fbx` で FBX を作り、プロジェクトの `Assets/Clothing/Shirt/Shirt.fbx` に置く
3. `capture-unity.ps1 -Project <新しい場所> -Tags model-rig,merge-armature,blendshape-sync`
4. `python crop-clothing.py <新しい場所>\ShotsOut` で Inspector を切り出す（Pillow が要る）

- Rig タブへの切り替えは、`AssetImporterTabbedEditor` の `activeTab`（非公開）をリフレクションで設定している。Unity のバージョンが変わると動かないことがある
- 画面の警告（「アバター内に配置する必要があります」）が写らないよう、仮のアバターに `VRCAvatarDescriptor` を付けている