# Phase 3b 設計メモ: 練習ページの 3D の C# 化

計画書（`docs/wasm-migration-plan.md`）の §4.5・§5 Phase 3b・D15 の続き。Phase 3b は**任意・後続**で、本番公開（master）の条件には含まれない。ここに書くのは、着手前の調査と設計、最初の一歩（純粋なロジックの C# 化）の記録で、画面の挙動はこの段階では変えない。
印の意味: **[実測]** はこの作業で測った・確かめた値、**[試作]** は draft PR #70 の記録（この作業では再計測していない）、**[推測]** は根拠のある見積もりで未検証。

## 0. 要点

- **描画層の推奨は (A) WebGL の薄い自作層**（`[JSImport]` で WebGL 2 を呼ぶ小さな JS と、描画命令を作る C#）。KNI は採らない。理由は §3: 陰影を three.js に合わせられる（試作で面の明るさが ±2 以内。KNI は左の面が 23 対 70 で大きく外れる）、追加の配信物が小さい、シェーダーを自分で持てる、KNI は .NET 10 での動作が未確認で、試作の未解決の問題（Tick のばらつき）の原因が KNI 側にあるか自分たち側にあるかも未切り分け。ただし**判断の関門を §4 の段階 2 に置く**（実装前に、画像の比較で A を確定する）。
- **C# 化の単位は 4 層**: ① 純粋なロジック（`FaraAvatarModdingGuide.Practice`。この PR で着手）② 描画層（段階 2 で作る別プロジェクト）③ 画面の部品（Razor。HUD・お題の一覧・メニュー・Unity 風のパネル）④ 通信（共有ルームの WebSocket）。three.js を配信物から外せるのは、**記事内チュートリアルと Unity 風も C# に移ったあと**（`unity.js` と `tutorial.js` が three.js を使っているため）。それまでは three.js の同梱・importmap・CSP のハッシュは残る。
- **段階は 8（S0〜S7）**。各段階は小さな PR に分け、現行の three.js 版を残したまま**切り替えフラグ**で C# 版を試せる形で進める（ロールバックはフラグを戻すだけ）。最後の S7 で初めて three.js を消す。
- **見積もり**: S0（済・約 3 人日）を除く残りが 約 **51〜77 人日**（[推測]。計画書の 40〜60 より大きい。理由は §6）。FBX は**今の JS に読み込みの実装が無い**ので、移植ではなく新機能になり、別枠 5〜15 人日（[推測]）。
- **S0 の成果（済・#104・#105）**: 設計メモ（本書）と、純粋なロジック 約 1,100 行の C# 化。現行の `viewport.js`・`practice.js` を Node で**そのまま**動かした結果との突き合わせテスト（80 シナリオ・約 5,500 手順・お題 314 状態）が通る。画面は変わらない。
- **S1 の成果（済。§9）**: 編集モード（頂点・辺・面の選択、押し出し・差し込み・ベベル・ループカット・削除・溶解・マージ・面を張る・分離）、追加・複製・削除、取り消し/やり直し、ミラー・透過・統計・N パネル・メニュー、共同練習の口を含む**ビューポートの操作ロジックの全部**を C# に移した（`Practice` は 約 4,400 行になった）。現行の JS との突き合わせは、**本物の `new Viewport(...)` を Node で動かし**（描画と DOM だけ代用品）、編集モードの操作列 127 シナリオ・約 20,000 手順 + 練習 1 のお題 18 件の通し + S0 の 82 シナリオ・約 5,700 手順で、**1 手順ごとに 40 項目余りの状態**（メッシュの頂点・面・選択の順番まで）を比べる。画面・配信物は変わらない（`Web` への参照は付けていない）。

## 1. 現行 JS の棚卸し

`src/FaraAvatarModdingGuide.Web/wwwroot/js/`。brotli は [実測]（Node の既定品質）。

| ファイル | 行 | brotli | 責務 | 状態を持つもの | 外へ出すもの |
| --- | --- | --- | --- | --- | --- |
| `viewport.js` | 1939 | 20.0 KB | Blender 風の 3D ビュー（`Viewport`）。シーン・視点・選択・G/R/S・編集モード・メニュー・N パネル・取り消し・共同練習の同期口 | オブジェクト列、選択、op（操作中の状態）、視点（target・dist・theta・phi・ortho）、編集モード、取り消し/やり直しのスタック | イベント（`on("*")`。key / select / opstart / axis / numeric / confirm / cancel / opfail / view / add / delete / undo / mode …）、`roomState()`・`applyRemote()` |
| `editmesh.js` | 789 | 8.7 KB | 編集モードのメッシュ（頂点・辺・面）と操作（選択の伝播・押し出し・差し込み・ループカット・ベベル・結合・面張り・分離）、基本の形（SHAPES） | 頂点・面・孤立辺、選択集合 | `triangles()` ほか |
| `practice.js` | 118 | 2.1 KB | 練習 1 のお題（18 件）と進み、リセット | 達成フラグ、お題の番号 | DOM（お題の一覧） |
| `tutorial.js` | 545 | 7.5 KB | 記事内チュートリアルの進行（案内・ハイライト・キー表示・ヒント・やり直し・完了の保存・「画像で見る」） | 手順の番号、group、完了 | `storage.js` 経由の進捗 |
| `scenarios.js` | 864 | 10.2 KB | チュートリアルの手順の定義（11 シナリオ。文言・`done(e, vp)` などの述語・`skip(vp)`） | なし（データ + 述語） | `SCENARIOS` |
| `unity.js` | 1437 | 17.2 KB | Unity 風の画面（`UnityEditor`）。Hierarchy・Inspector・Project・メニュー・SDK パネルは HTML（innerHTML）、Scene ビューだけ three.js | ノード木、選択、ツール、取り消し、視点 | イベント |
| `unity-practice.js` | 85 | 1.6 KB | Unity 練習のお題（7 件） | お題の番号 | DOM |
| `room.js` | 369 | 4.9 KB | 共有ルームの接続・差分の送受信・カーソル/選択/視点の共有・ロック | WebSocket、ピア、ロック | `/api/rooms/<ID>/ws` |
| `practice-entry.js` | — | — | Blazor からの入口（始める・解放する・タッチ端末の案内）。**C# 化しても残る** | 同時に 1 つ | — |

- 3D の外観（three.js）: `MeshStandardMaterial`（色 0x9c9c9c・roughness 0.75・metalness 0・両面・polygonOffset）、`HemisphereLight(0xffffff, 0x444444, 1.1)` + `DirectionalLight(1.2, 位置 (4, -6, 8))`、背景 0x3d3d3d、`GridHelper`、線（`LineSegments`・`Line`・破線）、編集モードの点・辺・面の重ね描き、3D カーソル（リング、`depthTest: false`）、アンチエイリアス。three.js の参照は `viewport.js` 78 か所・`unity.js` 53 か所・`editmesh.js` 9 か所。
- three.js は数学にも使われている（Vector3・Quaternion・Raycaster・Box3・Plane）。**C# 化はこの数学の置き換えを含む**（§2）。
- Unity 風は「3D」というより**パネルの UI**が大半（innerHTML の HTML）。3D は Scene ビューのプリミティブ部品と選択の枠くらい。C# 化の費用は 3D 描画より UI の移植（Razor 化）が中心になる。
- **FBX**: `unity.js` の `Hoodie.fbx` は名前の付いたアセット（`type: "model"`）にすぎず、FBX を読み込む実装は無い（`grep` で loader・FileReader も無し）。メモリの「共有ルームのファイル照合・FBX は後で実装」は**未実装の将来機能**で、移植ではなく新機能。
- 入力経路: `bind()` が root に pointermove / pointerdown / pointerup / wheel / keydown / keyup / contextmenu を登録（wheel は `passive: false` で `preventDefault`）。`practice-entry.js` の `start/dispose` が同時 1 つだけを保証する。
- 描画ループ: `requestAnimationFrame`。画面外（IntersectionObserver）では描画しない。毎フレーム `updateCamera()`。

## 2. C# に移す単位

| 層 | 中身 | 置き場 | 依存 |
| --- | --- | --- | --- |
| ① 純粋なロジック | 数学（Vec3・Quat・Euler）、視点の軌道、レイ判定、G/R/S の状態機械、選択、入力 → 状態の更新、お題の判定、基本の形、（S1 で）編集モード・取り消し・追加/複製/削除・部屋の状態 | `src/FaraAvatarModdingGuide.Practice`（net10.0 のクラスライブラリ。**ブラウザにも Blazor にも依存しない**） | なし |
| ② 描画層 | WebGL 2 の薄い橋渡し（JS）と、シーン → 描画命令（C#） | 別プロジェクト（S2 で作る。例 `FaraAvatarModdingGuide.Practice.Rendering`。Razor クラスライブラリ） | ① |
| ③ 画面の部品 | HUD（状態表示・数値入力・視点名）、ツールボタン、メニュー、N パネル、お題の一覧、チュートリアルの案内、Unity 風のパネル | `Web`（Razor。MudBlazor は使ってもよいが、HUD は 3D の上に重ねる軽い要素なので素の Razor） | ①②③ |
| ④ 通信 | 共有ルーム（`ClientWebSocket` はブラウザの WebSocket に載る。プロトコルは変えない） | `Web` または ② の隣 | ① |

方針（この PR で決めて実装した）:

1. **計算は倍精度（`double`）**。three.js は倍精度で計算するので、`System.Numerics`（`float`）で書くと、お題の境界（`near` 0.05・`nearAngle` 1°）で判定がずれうる。倍精度の小さな型（`Vec3`・`Quat`）を自前で持ち、描画へ渡す境界でだけ `float` にする。試作 #70 の `Shared/` は `float` + `System.Numerics` で、`MoveOp` が現行と食い違う点（符号キーの扱い・Shift の「それ以外」・Ctrl の刻み・Shift の細かさ・表示の文字・レイの原点）があったので、**構造だけ参考にし、式は現行の JS から写した**。
2. **式・順序・表示の文字まで現行と同じ**にして、突き合わせで確かめる（§7）。表示の文字は `toFixed` の挙動（半分の丸め・負のゼロ）まで合わせた（`Js.ToFixed`）。
3. **入力は「イベントの値」で受ける**（`PointerMove` / `PointerDown` / `PointerUp` / `Wheel` / `KeyDown`(code, key, 修飾キー) / `KeyUp`）。DOM のイベントオブジェクトは持ち込まない。出力は状態（オブジェクト・選択・視点・表示の文字・軸線）と、現行と同じ名前のイベントの列（`TakeEvents()`）。チュートリアル（`scenarios.js` の述語）がこのイベントに依存しているので、イベントの形は変えない。
4. **扱えない入力は `false` を返す**（`KeyDown`）。段階的に移すときに、C# が扱えないキーを現行の JS に任せられる。
5. **描画に必要なもの（三角形の頂点）は `Float32` と同じ丸め**で持つ。three.js はジオメトリを `Float32Array` で持つので、レイ判定を同じ値で行う。

**Shared 部分の共有**: 計画書の「Shared 部分（判定の純粋なロジック）は C# で共有する」は、`Practice` を Web・Tests・（必要なら）Build が参照する形で満たす。例えば Build がチュートリアルの手順の定義（S5 で C# のデータになる）を検査できる。

**独立プロジェクト化**: 計画書 §3.1.1 の「本体の csproj と分ける」は、旧 SSR の csproj（ルート）が `**/*.cs` を取り込む問題への対策だった。Phase 5 でルートの csproj は消えたので衝突は起きない。`Practice` は `src/` に置いて sln に入れ、描画層の試験的なプロジェクトは `Web` から参照されない別プロジェクトとして育てる（S2）。

## 3. 描画層の比較: (A) 薄い WebGL 自作層 と (B) KNI

(C) 現状維持（three.js のまま、操作ロジックだけ C# 化して JS に描画を指示する）も比較に入れる。

| 観点 | (A) 薄い WebGL 層 | (B) KNI（nkast.Kni.Platform.Blazor.GL） | (C) three.js のまま |
| --- | --- | --- | --- |
| 追加の配信量 | 自分の C# + `gl.js` 数 KB。試作 `WebGl.dll` 18 KB（brotli）[試作]。**このサイトの WASM ランタイムは全ページで既に読み込む**（Phase 1〜5）ので、追加は自分のコード分だけ。この PR の `Practice.dll` は 79 KB / brotli 28 KB [実測]（約 1,100 行）。全面移植後の C# 全体は 100〜150 KB（brotli）程度 [推測] | 計画書 §4.5 は「約 +400 KB」[試作]。KNI/XNA のアセンブリ + nkast の JS | three.js 207 KB（brotli）[実測] を引き続き配る |
| three.js を外せるか | 外せる（S7。チュートリアルと Unity 風が移ったあと）。importmap・`lib/three`・CSP のハッシュ（`SiteBuilder.ImportMapHashes`）・LICENSE も消える | 外せる（同条件） | 外せない |
| 描画の差（半球光・面の明るさ） | シェーダーを自分で書くので合わせられる。試作で上面 111 対 112、左面 72 対 70、右面 80 対 80（±2）[試作] | `BasicEffect` は平行光 + 環境光で半球光が無く、左面 23 対 70 で大きく暗い [試作]。独自 Effect には MGCB のコンテンツパイプラインが要ると思われる（未確認） | 差なし |
| .NET 10 + Blazor WASM での動作 | `[JSImport]` は標準機能。試作は net9 で動作、WebGL は 10 でも標準 API のみ [推測: 未検証] | 試作は公式テンプレート（net8）を net9 に書き換えて動作。**.NET 10 は未確認**（WebGLazor は net10 必須とのこと [試作]、未試用） | — |
| フレーム時間 | 3D フレームの CPU 0.085〜0.089 ms（C# の `Frame`）、rAF 全体 0.15 ms（interop 込み）[試作] | `Draw` 0.14〜0.20 ms、rAF 全体 0.3〜13 ms でばらつく（**原因は未切り分け**）[試作] | 0.10 ms [試作] |
| 保守性 | シェーダー・行列・線・点の描画を自分で保つ（約 170 行で土台が動いた [試作]）。KNI のような抽象はないが、依存が無い | 抽象は付くが、MonoGame 系の用語（Game・Content・Effect）と、nkast 側の更新に追随する依存が増える。3 か月ごとの .NET 更新と KNI の更新の組み合わせが負担になりうる [推測] | 現状のまま |
| シェーダー・ライト | 自由（半球光・Lambert・sRGB 変換を実装済み [試作]） | 標準 Effect の範囲 | three.js 任せ |
| 線・点・重ね描き | `gl.LINES`・`gl.POINTS`・深度テストの切り替えを自分で書く（編集モードの重ね描きに必要） | 同様に自前（`VertexBuffer`・`BasicEffect`） | three.js 任せ |
| FBX | どの層でも新規実装（§1）。外部の C# ライブラリは WASM（トリミング・サイズ）の適合の確認が要る | 同じ | three.js の `FBXLoader` が使える（同梱の追加が必要）。**FBX を早く欲しいなら (C) が有利** |
| テスト容易性 | ロジック（①）は描画に依存しないので、単体テストと JS との突き合わせが書ける（この PR）。描画は Playwright + SwiftShader の画像比較 | 同じ。ただし入力が `Mouse.GetState()` のポーリング。ホイールの `preventDefault` ができない課題 | 現状（Playwright のみ） |
| ブラウザ互換 | WebGL 2（Edge で確認 [試作]。Firefox・Safari は未確認）。現行の three.js は WebGL 1/2 の両方 | WebGL 2 | 現行どおり |
| 既知の未解決 | 「新しいブラウザコンテキストで描画が始まらない」（**KNI だけでなく WebGL 版でも起きた** [試作]。KNI 固有ではない） | 同じ問題 + Tick のばらつき | なし |

**推奨: (A)**。

1. 描画の再現性: 陰影を three.js に合わせられる（±2）。受け入れ条件の「描画の差をユーザーが受け入れる」を、差を小さくして出せる。KNI は左面が大きく暗く、直すには独自 Effect とコンテンツパイプラインの未確認の道が要る。
2. 配信量・依存: 追加は自分のコード分だけ。KNI は +約 400 KB と外部依存が増える。
3. 試作の未解決の問題の切り分け: 「新しいブラウザコンテキスト」の問題は WebGL 版にも出たので、**どちらを選んでも原因の特定が要る**。Tick のばらつきは KNI 固有の可能性が高く、(A) なら対象外になる。
4. .NET 10 の未確認が KNI にだけある。

**(A) を確定する前に確かめること（S2 の判断の関門）**:
- 新しいブラウザコンテキストで描画が始まらない原因。仮説（[推測]）: 新しいコンテキストで開いたタブが**非表示（`document.visibilityState === "hidden"`）で `requestAnimationFrame` が呼ばれない**。KNI と WebGL の両方が rAF で回るので、両方に出た事実と合う。確かめ方: 失敗した文脈で `visibilityState` と rAF の回数を記録、`page.bringToFront()`、`--disable-renderer-backgrounding --disable-background-timer-throttling`、`IntersectionObserver` で止めていないか（現行の three.js 版は画面外で描画を止める作りで、同じ罠がありうる）。
- Tick のばらつき（KNI を再検討する場合のみ）。仮説: `IsFixedTimeStep` の既定（固定刻み）で、60 Hz の rAF の揺らぎにより 1 フレームで `Update` が 0 回・2 回になる、または WASM の GC。確かめ方: `IsFixedTimeStep = false` にして、`Update` の回数と所要を記録。
- .NET の**AOT なし**（`wasm-tools` が無く、IL を解釈実行する。`dotnet publish` の警告のとおり）での毎フレームの C# の負荷。試作は 0.09〜0.15 ms で問題なかったが、編集モード（密なメッシュの押し出し・ベベルのマウス追従）とレイ判定（球 960 三角形 × 数個）で予算（1 フレーム 16 ms のうち 5 ms 以内）を測る。足りなければ、重い部分だけを小さくする（レイ判定の前の外接球テストなど）。
- 判断基準: S2 の最後に、(a) 3 面（上・左・右）の明るさが three.js と ±3 以内、(b) 新しいコンテキストで描画が始まる、(c) 1 フレームの CPU が予算内、の 3 つが満たせれば (A) を確定。(a) を満たせない場合に限って (B) を再検討する。

**インターフェース（A の形）**: `[JSImport]` / `[JSExport]`（`IJSUnmarshalledObjectReference` は .NET 7 で非推奨になった旧方式なので使わない [推測: 現行 SDK での扱いは S2 で確認]）。
- 描画: C# が頂点・インデックス・色・行列を `Span<byte>`/`float[]` で JS に渡し、`gl.js`（約 100 行）が WebGL 2 を呼ぶ。バッファはメッシュごとに持ち、**変わったときだけ**アップロードする（G/R/S は行列の更新だけ。編集モードは頂点の更新）。
- 入力: Blazor の `@onpointermove` は使わない。`gl.js` に**ネイティブのリスナー**（`passive: false` の wheel を含む）を置き、数値だけを `[JSExport]` のコールバックで C# に渡す（試作は Blazor のイベントを使い、wheel の `preventDefault` ができずページがスクロールした）。`pointercapture`・`contextmenu` の抑止もここ。
- 描画ループ: JS の rAF から C# の `Frame(time)` を呼ぶ。画面外・非表示では呼ばない。

## 4. 段階的な移行計画

### 4.1 並行運用とロールバック

- **現行の three.js 版は S7 まで消さない**。`PracticeHost.StartAsync(pcScript, options)` の `pcScript`（`practice.js` / `unity-practice.js` / `room.js`）を差し替える形で切り替える。C# 版の入口は `practice-cs.js`（`gl.js` を読んで .NET を起動する）。
- **切り替えフラグ**: `?engine=cs`（URL のクエリ。確認用の dev サイトで効く）→ 受け入れの後、既定を C# にして `?engine=js` で現行に戻せる形にする → S7 で JS を消す。フラグはページ側（`Practice.razor` など）で読み、`PracticeHost` に渡す pcScript と、お題の一覧を JS が描くか Razor が描くかを決める。
- 戻し方: S3 以降の PR を出したあと不具合が見つかったら、既定を JS に戻す 1 行の PR（または `?engine=js`）。three.js の同梱・importmap・CSP のハッシュは S7 まで残るので、戻しても動く。S7 だけは戻せない変更（ファイル削除）なので、**S3〜S6 を本番（master）に出して一定期間問題がないのを確かめてから**行う。
- 一度に動くのは 1 つ（`practice-entry.js` の既存の保証）。C# 版でも `dispose` で WebGL のコンテキスト・リスナー・rAF・WebSocket を必ず解放する。

### 4.2 段階（PR の分割と受け入れ基準）

| 段階 | 内容 | 受け入れ基準 | 見積もり（人日。[推測]） |
| --- | --- | --- | --- |
| **S0（この PR）** | 設計メモ。`Practice`（オブジェクトモードの G/R/S・選択・視点・レイ・お題・基本の形）。現行 JS との突き合わせテスト。Web からは呼ばない | dotnet test / node --test / `wrangler deploy --dry-run` / CI が成功。配信物は変わらない（Web に参照を付けない） | 済（約 3） |
| **S1** ロジックの完成（**済**。§9） | 編集モード（`editmesh.js`: 選択モード・選択の伝播・押し出し・差し込み・ループカット・ベベル・結合・面張り・分離・削除）、追加（Shift+A）・複製・削除・取り消し/やり直し・ミラー・X 線・N パネルの値、共同練習の口（`roomState`・`applyRemote`・`canTransform`・`getView/setView`）、`wheelNeedsFocus`。**チュートリアル用の補助**（`selectLoopWhere`・`editSelectWhere`・`findEdge`・`previewLoopCut`・`loopCutWhere`・`selectByName`・`setHighlight`）と、ほかの参加者の表示（`setRemotePeers`）は、使う側の段階（S4・S6）で移す | S0 と同じ突き合わせを、編集モードを含む操作列（乱数 + 練習 1 の 18 件のお題を全部こなす手順）に広げて一致。`practice-baseline.json` 相当の最終状態が一致 | 済（行数は S0 の約 2.3 倍。§9.5）。当初の見積もり 10〜15 |
| **S2** 描画層の試作と判断の関門 | 薄い WebGL 層（`gl.js` + C#）。練習 1 のシーン（方眼・軸線・オブジェクト・輪郭・3D カーソル・編集モードの点線面）を描く。画像比較（SwiftShader。3 面の明るさ・縁・線の太さ）。新しいコンテキストの問題と AOT なしの負荷の測定。§3 の判断基準で (A) を確定 | §3 の 3 条件。結果をこの文書に追記（ADR）。ユーザーの確認 | 8〜12 |
| **S3** `/practice` を C# で（フラグ付き） | ページ（Razor）+ HUD + お題の一覧を Razor で。`?engine=cs` で C# 版。`tools/verify/practice.mjs` を両エンジンで通す（`practice-baseline.json` と一致）。往復 20 回でリスナー・コンテキストが増えない。画像比較の結果を PR に載せる | Phase 3 と同じ操作（キー・マウス）ができる。**描画の差はユーザーが受け入れる**（画像で提示）。既定は JS のまま | 6〜9 |
| **S4** 記事内チュートリアル | `scenarios.js`（11 シナリオ）と `tutorial.js` を C# に。シナリオは C# のデータ + 述語（イベント → 達成）。「画像で見る」とタッチ端末の扱いは維持 | 全 11 シナリオを JS 版と C# 版で同じイベント列に通して、達成する手順の番号が一致するテスト（突き合わせ）。記事 2 本以上を画面で確認 | 8〜12 |
| **S5** Unity 風 | `unity.js` を Razor（Hierarchy・Inspector・Project・メニュー・SDK パネル）+ Scene ビュー（②の描画層）に。`unity-practice.js` のお題 7 件。ツリー操作・Setup Outfit・タグ・マテリアルのロジックは①に | `practice.mjs` の `unity` の基準と一致。階層・お題の状態をイベント列で JS と突き合わせ | 12〜18 |
| **S6** 共有ルーム | `room.js` を C#（`ClientWebSocket`）に。差分の送受信・ロック・カーソル・選択・視点の共有。`tools/room-check` と画面からの二人の確認 | 二人が入れる・追加/移動が届く・退室が消える。プロトコルは変えない | 5〜8 |
| **S7** three.js を外す | 既定を C# に。JS の `viewport.js`・`editmesh.js`・`unity.js`・`room.js`・`tutorial.js`・`scenarios.js`・`practice*.js`、`lib/three`、importmap、CSP のハッシュ、README/プライバシーの記述を消す | 全ページで違反ゼロ（CSP）。配信量の前後比較。ユーザーの承認（戻せない変更） | 2〜3 |
| FBX（別枠） | 新機能。読み込み方式（外部ライブラリ or 自前）の調査から | 別途 | 5〜15 |

各段階の PR は develop 向け。S3 以降は、develop（avatar-dev）で確認してから master へ（ユーザーが行う）。S2 までは配信物が変わらない（C# の実験は Web に参照を付けない・フラグ無しでは読まない）。

### 4.3 検証の仕組み

- **ロジック**: 現行 JS を Node で動かした結果との突き合わせ（§7）。S1 以降も同じ入力ファイル（`tests/js/practice-cases.json`）に手順を足す。
- **描画**: Playwright + SwiftShader で同じシーンのスクリーンショットを撮り、three.js 版との画素差を測る（`tools/verify/` に追加。しきい値と許容領域はユーザーの受け入れの材料にする）。
- **画面の操作**: 既存の `tools/verify/practice.mjs`（基準 `practice-baseline.json`、往復 20 回の資源の増減）を `?engine=` で両方に通す。
- **性能**: 毎フレームの CPU とレイ判定の所要を、AOT なしのビルドで測る（S2）。

## 5. リスクと対策

| リスク | 影響 | 対策 |
| --- | --- | --- |
| AOT なし（IL の解釈実行）で編集モードの重い操作が遅い | 操作が重い | S2 で測る。遅い所だけ最適化（外接球の前処理、変更のある面だけの再計算）。最終手段は `wasm-tools` の AOT（サイズが増える）だが、SDK 全体の変更になるため、ユーザーに確認してから |
| 描画の差（半球光・縁・線の太さ・アンチエイリアス）がユーザーに受け入れられない | S3 で止まる | S2 で画像比較を先に出す。差が大きければ (A) のシェーダーを調整。それでも合わなければ (C)（描画だけ three.js）に退く |
| 新しいブラウザコンテキストで描画が始まらない（原因不明） | 一部の環境で真っ黒 | S2 の最初に原因を特定（§3 の仮説）。特定できるまで S3 に進まない |
| 計算の誤差でお題の境界が現行と変わる | お題が達成できない/早く達成される | 倍精度 + 式の写し + 境界の値の突き合わせ（済）。符号付きゼロ・「-0.000」の表示だけは、計算順序の誤差で現行と揃わない（意図した差。§7） |
| 現行の JS を直したのに C# と食い違う（二重管理） | 仕様のずれ | S0〜S6 の間は JS の変更をテストが検知する（`viewport.js`・`practice.js` を直すと突き合わせが失敗）。意図した変更なら C# も同じ PR で直す |
| `-` と `.` の入力で `-.` になる（現行は NaN でオブジェクトが消える不具合） | — | C# は「数値入力なし」として扱う（直した）。JS 側も S1 で直した（ユーザーの決定 2026-10-24。`viewport.js` の `applyOp`。入力が数値にならないあいだは数値入力なし） |
| three.js の `Box3.expandByObject` が非表示の mirror 子も数える（編集で左右非対称になったメッシュで `Home`・`Numpad .` の結果に効く） | 非対称のメッシュのフレーミング | C# は同じ扱いを実装済み（`SceneObject.WorldBounds`）。**対称な基本の形でしか現行と突き合わせていない**ので、S1（編集モード）で非対称のメッシュを突き合わせに加える |
| `Hoodie.fbx` の期待（将来のアバター編集） | スコープ膨張 | FBX は別枠にして、先に決める（読み込み方式は未定） |
| KNI を使わないことが後で覆る | 手戻り | 描画層は `Practice`（①）の外に出すので、差し替えは②だけ。①は描画に依存しない（この PR の構造） |
| .NET の更新（10 → 11）で JSImport の挙動が変わる | 保守 | 描画層の interop は小さく保つ。CI の `dotnet publish` + Playwright で検知 |

## 6. 見積もりの根拠

計画書の 40〜60 人日（試作の「2〜3 か月」から換算）を、棚卸しの行数と段階ごとに分け直した。

| 項目 | 根拠 | 人日 [推測] |
| --- | --- | --- |
| S0 | この PR（実績の目安） | 約 3 |
| S1 | `viewport.js` の残り 約 1,100 行と `editmesh.js` 789 行の移植 + 突き合わせの拡張。S0 の実績（約 1,100 行 + テスト基盤）の 1.5〜2 倍。**実績: §9.5**（行数は S0 の約 2.3 倍。人日では測っていない） | 10〜15 |
| S2 | 試作で 約 170 行の描画層が動いた。本番は線・点・面の重ね描き・輪郭・深度の切り替え・サイズ変更・解放が加わる。測定と判断を含む | 8〜12 |
| S3 | ページ・HUD・お題・フラグ・検証・画像比較 | 6〜9 |
| S4 | 864 + 545 行。述語の移植 + 突き合わせ | 8〜12 |
| S5 | 1,437 行の大半が HTML。Razor 化 + ロジック | 12〜18 |
| S6 | 369 行 + プロトコルの確認 | 5〜8 |
| S7 | 削除と確認 | 2〜3 |
| **合計（FBX を除く）** | S0 を含めて 54〜80。S0 を除く残りは 51〜77 | **54〜80** |

計画書の 40〜60 より大きくなるのは、記事内チュートリアル（S4）と Unity 風（S5）を、試作の見積もり（「Unity 風 数週間」など）より具体的な行数から厚く見たため。試作は練習 1 の一部だけを写した値の外挿だった。見積もりは各段階の終わりに実績で直す。

## 7. 最初の一歩（S0）の記録

### 7.1 作ったもの

`src/FaraAvatarModdingGuide.Practice`（net10.0、依存なし。約 1,100 行）:

| ファイル | 現行 JS の対応 | 内容 |
| --- | --- | --- |
| `Maths.cs` | three.js の Vector3・Quaternion・Euler、`toFixed`・`Math.round` | 倍精度の `Vec3`・`Quat`、`ToEulerXYZ`（`rotation` の再計算）、`Js.ToFixed`・`Js.Round`・`Js.ParseNumeric` |
| `MeshShapes.cs` | `editmesh.js` の SHAPES・`triangles()` | 基本の形 6 種（plane・cube・sphere・cylinder・cone・torus）の頂点・面・三角形（Float32 の丸め） |
| `SceneObject.cs` | `makeObject` の mesh | 位置・回転（quaternion）・スケール、`WorldBounds`（`Box3.expandByObject` と同じ。mirror 子を含む） |
| `OrbitView.cs` | `view`・`updateCamera`・`numpadView`・`orbitBy`・`frame`・`ndc`・`toScreen`・`worldPerPixel`・`unprojectOnPlane` | 視点の軌道、カメラの軸（`lookAt`）、レイ（`setFromCamera`）、投影、ドラッグの回転/移動/ズーム、ホイール |
| `Picking.cs` | `Raycaster.intersectObjects`（両面） | 三角形ごとのレイ判定、最も近いオブジェクト |
| `TransformOp.cs` | `startOp`・`setAxis`・`typeNumeric`・`applyOp`（G/R/S）・`cancelOp` | 軸固定（Shift で「それ以外」・再押しで解除・押し出しは法線）、数値入力、Ctrl の刻み、Shift の細かさ、表示の文字、軸線 |
| `PracticeTasks.cs` | `practice.js` の TASKS・`checkTasks`・`near`・`nearAngle` | お題 18 件（文言も同じ）と、順に進める進行 |
| `ViewportCore.cs` | `bind`・`onKey`・`onDrag`・`finishBox`・`select*`・`confirmOp` | オブジェクトモードの入力 → 状態の更新とイベント。**対応**: クリック/Shift+クリック/ボックス選択、中ボタン・Alt+左のドラッグ、ホイール、G/R/S、テンキー（模倣を含む）、Home、A・Alt+A・Ctrl+I。**未対応は `KeyDown` が false**（追加・複製・削除・取り消し・編集モード・メニュー・N パネル・ループカット） |

### 7.2 突き合わせの方式（`SearchParityTests` と同じ考え方）

- `tests/js/practice-reference.mjs`: 現行の `viewport.js`・`practice.js`・`editmesh.js` を **Node でそのまま**読み込み、`Object.create(Viewport.prototype)` で本物のメソッドを借りる（コンストラクタは WebGL と DOM が要るので呼ばない）。描画だけに関わるもの（輪郭・パネル・メニュー・取り消しの記録）だけを空の代用品にする。`bind()` が登録する**本物のハンドラー**に、模擬のイベント（pointer / wheel / key）を渡して動かす。つまりクリックの選択・ドラッグの視点操作・G/R/S・数値入力・テンキーは現行のコードそのまま。
  - three.js は `wwwroot/lib/three/three.module.js`（ブラウザでは importmap が指す同じファイル）。`module.register` の読み込みフック（`practice-hooks.mjs`）で `import "three"` を向け、`practice.js` の `TASKS`/`Practice` を取り出すために**読み込むときだけ**末尾に export を足す。配信するファイルは書き換えない。
  - 実際のページは描画のたびにカメラを更新するので、イベントの合間に `updateCamera()` を呼ぶ。
- `tests/js/practice-cases.generate.mjs`（決まった乱数の種）が `practice-cases.json` を作る。**JS と C# が同じファイルを読む**。内容: お題 314 状態（境界の値を含む）、お題の進行 2 列、シナリオ 80（手書き 20 + 乱数 60。合計約 5,500 手順）。
- `tests/FaraAvatarModdingGuide.Practice.Tests`（`dotnet test` の中で Node を起動。CI は `actions/setup-node` 済み）が、各手順のあとの状態（位置・quaternion・スケール・Euler・選択・視点・カメラの位置/軸/平行投影の範囲・表示の文字・数値入力の表示・操作中の状態・軸線・取り消しの数・出たイベント）と、確認用のレイ・平面の交点・ピック・投影・1 ピクセルの長さを比べる。数値は 1e-9 の相対誤差まで、文字・真偽は完全一致。
- **効くことの確認（変異テスト）**: パンの係数 0.0016 → 0.0017、Shift の細かさ 0.1 → 0.2、フレーミングの 1.6 → 1.5、お題の許容値 0.05 → 0.06、球の頂点の計算、片面だけのレイ判定、のそれぞれでテストが失敗することを確認した。

### 7.3 意図した差と、突き合わせていないこと

- **意図した差**: ① （解消済み）数値入力が `-.`（符号のあとに小数点だけ）のとき、現行は `NaN` になってオブジェクトが消える。C# は「数値入力なし」として扱う。**S1 で JS 側も直した**（`parseFloat` が NaN のときは数値入力なし）ので、この差は無くなり、突き合わせの入力にも含めている（手書きのシナリオと乱数）。② 状態表示の `-0.000` と `0.000`: 値が 1e-16 程度の誤差のとき、符号が計算の順序（three.js の行列と C# の式）で変わり、`toFixed` が `-0.000` を出すかが分かれる。表示の比較では両者を同じとみなす（値そのものは 1e-9 で比べている）。
- **突き合わせの対象外（独立レビューと追加の確認で分かったこと）**: ① R を、マウスが回転の中心の画面位置にちょうど重なった状態で始めると、角度は atan2(≈0, ≈0) の誤差で決まり、実装ごとに違う（突き合わせの入力は中心を外している）。② （S1 で解消）`rotation` は JS が設定した値をそのまま持つので、C# も `SceneObject.SetRotation` で設定したオイラー角を保ち、quaternion を変えたときだけ求め直す（N パネルの |y| > 90° の入力、取り消し・部屋の同期の `rotation.set` が JS と一致する）。③ JS のカメラはフレーム単位で更新される（同じフレーム内の視点変更の直後のイベントは古いカメラを使う）。C# は常に最新（実際のページでは差が出ない）。④ `MousePx` の範囲判定は JS が `clientWidth/Height`、C# は `getBoundingClientRect` の幅・高さ（枠線があると差が出る。接続時に確認）。⑤ 空のシーン・操作中のテンキー数字・Ctrl+Z は入力に少ない/無い。
- **突き合わせていない（S0 の時点）**: ホイールの `wheelNeedsFocus`、共同練習の `canTransform`、`ResizeObserver` による再計算、`Home` の非対称メッシュ、取り消し、追加・複製・削除、編集モード、メニュー、N パネル。**S1 で全部広げた**（§9.3）。S1 のあとに残る対象外は §9.4。
- **配信物に関する確認 [実測]**: `Web` が `Practice` を参照するだけで（呼ばなくても）、配信物が増えた（`FaraAvatarModdingGuide.Practice.*.wasm` 79 KB（brotli 28 KB）が加わり、ほかのアセンブリのファイル名のハッシュも変わる。ファイル数 100 → 101、合計 11.71 MB → 11.79 MB、brotli（品質 9）の合計で +32 KB）。この段階は「画面も配信物も変えない」ので、**`Web` への参照は付けていない**。参照は、`Practice` を最初に呼ぶ段階（S3）で付ける。

### 7.4 検証の結果

- `dotnet test`（ソリューション全体）、`node --test`（既存の `tests/js/*.test.mjs`）、`wrangler deploy --dry-run`、最新コミットの CI は PR に記載。

## 8. ユーザーへの確認事項

1. **描画層は (A) 薄い WebGL 層でよいか**（KNI は採らない）。KNI の Tick のばらつきは、KNI を採らない場合は原因の切り分けを「対象外」としてよいか。「新しいブラウザコンテキストで描画が始まらない」問題は、A でも S2 の最初に必ず原因を特定する（それでよいか）。
2. **S1 以降の進め方**: 段階ごとに報告して続行の確認を取る形でよいか。（S1 は完了。次は S2 を提案。§10）
3. （決定 2026-10-24・実施済）現行 JS の `-.` の不具合は JS 側も直した（#106。`tools/verify/practice.mjs` は 58 件すべて ok・基準は変わらない）。
4. **FBX**: 今は読み込みの実装が無く、新機能として別枠にする。優先度と、必要な機能（アバターの表示だけか、ファイル照合も含むか）。
5. **AOT（`wasm-tools`）**: 必要になった場合に導入してよいか（サイズが増える。SDK 全体に影響）。S2 の測定で必要か判断する。
6. **three.js を消す時期（S7）**: 戻せない変更なので、S3〜S6 を本番に出して一定期間（目安）問題がないのを確かめてからでよいか。

## 9. S1（ロジックの完成）の記録

印: **[実測]** はこの作業で測った・確かめた値。S1 は 3 本の PR（#106 JS の `-.` の修正 → ロジックの C# 化 → 突き合わせテストと本書）に分けた。順に取り込む。

### 9.1 作ったもの

`src/FaraAvatarModdingGuide.Practice` は 約 1,320 行 → 約 4,400 行になった（net10.0、依存なし。Web からはまだ呼ばない）。

| ファイル | 現行 JS の対応 | 内容 |
| --- | --- | --- |
| `EditMesh.cs`（新） | `editmesh.js` の `EditMesh` 全部 | 頂点・面・孤立辺・辺の一覧（`changed()` まで使い回す古い一覧の挙動も同じ）、選択の伝播（`flush`）、ループ/リング/つながり/拡大/縮小、押し出し・差し込み・ループカット・ベベル・削除/溶解・マージ・面を張る・分離、`triangles` |
| `OrderedSet.cs`（新） | JavaScript の `Set` | 追加した順を保つ集合。マージで残す頂点・面を張る順・`toData()` の並びが、選択の順番で変わるため。`HashSet` は使えない |
| `ViewportCore.cs` / `.Scene.cs` / `.Edit.cs`（拡張） | `viewport.js` の `bind`・`onKey`・`commonKey`・`startOp`・`cancelOp`・`confirmOp`・`select*`・`setMode`・`undo`/`redo`・`snapshot`/`restore`・`resetScene`・`addObject`・`duplicate`・`roomState`/`applyRemote`/`loadRoomScene`・`getView`/`setView`・`pointAt`・`editInfo`・メニュー・N パネル・ツールボタン・`wheelNeedsFocus`・`canTransform`・編集モードの選択と操作 | 画面に出るもの（左下の文字・HUD・統計・ボタンの状態・N パネルの入力欄・メニュー・ループカットのプレビュー・軸線）は状態として読める。ポインター・キーの関数は「preventDefault を呼んだか」を返す |
| `TransformOp.cs`（拡張） | `op` と `applyOp` | G/R/S に加えて I・B・slide、編集モード（ワールド座標で動かして逆行列でローカルへ戻す）、キャンセル |
| `SceneObject.cs`（作り直し） | `makeObject` の mesh | `EditMesh` を持つ。`rotation` のオイラー角は設定した値を保つ（§7.3 ②）。`rebuild()` で作った形（三角形・外接箱・外接球）でレイ判定する |
| `Maths.cs` / `OrbitView.cs` / `Picking.cs`（作り直し） | three.js の `Matrix4`・`Quaternion`・`Raycaster`・カメラ | **カメラとレイ判定を three.js と同じ手順に写した**（lookAt → クォータニオン → compose、射影行列と逆行列、`Mesh.raycast` の外接球 → ローカルへ → 外接箱 → 三角形）。理由は §9.2 |
| `PracticeTasks.cs`（拡張） | `practice.js` の `Practice` | `PracticeSession`: イベントが出た瞬間に状態を見てお題を進める |

写し方の方針: 式と順序を JS から写す（倍精度、`divideScalar` は 1/s を掛ける、`lerp` は x += (v - x) × t、`Array.splice` の負の添字、`Set`/`Map` の順番）。JS の不具合も挙動のとおりに写す（§9.4）。

### 9.2 突き合わせの土台の作り直し

S0 は `Object.create(Viewport.prototype)` で一部のメソッドを空にしていた。編集モードは、メッシュの再構築・ジオメトリ（Float32）・レイ判定・取り消し・統計・メニューが絡むので、**S1 は `new Viewport(...)` を本物のコンストラクタごと動かす**。

- `tests/js/practice-dom.mjs`（新）: 描画（`WebGLRenderer`）・`requestAnimationFrame`・`ResizeObserver`・DOM の要素（HUD・メニューのボタン・N パネルの入力欄・ツールのボタン）・`crypto.getRandomValues`（部屋の ID を決まった列にする）の代用品。`render()` は本物と同じく `scene.updateMatrixWorld()` を呼ぶ。`practice-hooks.mjs` が、読み込むときだけ `new THREE.WebGLRenderer(` を代用品へ差し替える（配信するファイルは書き換えない。見つからなければ失敗する）。
- `tests/js/practice-reference.mjs`（作り直し）: `createHarness`・`applyStep`・`snapshot`。S0 の出力と見比べて、S0 の全手順で（初期の状態表示の文字を除いて）同じ結果になることを確かめた。手順の種類は、ポインター・ホイール・キー・ツールボタン・N パネルの変更・メニュー項目・フォーカス・リサイズ・リセット・部屋の反映・視点の設定。
- **カメラとレイ判定を three.js と同じ手順に写した理由 [実測]**: 軸にそろえた平行投影（テンキー）では、隠れているかの判定のレイが立方体の頂点・辺の上をぴったり通り、「当たるか」が最後の桁の丸めで決まる（JS 自身が前面図で、後ろの頂点を 8 個中 7 個選べたりする）。S0 の解析的な式（誤差 1e-16）では、その判定が JS と食い違った。three.js と同じ行列の手順・同じ式の順にして、外接球・外接箱のふるいまで写すと、レイの始点・向き・当たりの距離が JS とビットまで一致した。
- **三角関数の誤差への備え [実測]**: V8（Node 24）と .NET の `Math.sin`/`cos` は、入力の約 2〜3% で最後の 1 桁が違う（正しい丸めとの不一致は、Node 3.3%・.NET 3.2%、互いの不一致 2.3%。BigInt の高精度の参照値で確かめた）。CI は Node 22 と Linux の libm なので、さらに違いうる。そこで `practice-edit-cases.generate.mjs` は、できあがった操作列を **「三角関数の結果を入力に応じて 1 ulp ずらした JS」で 8 通り動かし、結果が変わった最初の手順で列を打ち切る**。ぴったり重なった頂点のどちらが選ばれるか・ほぼ平行な面の向き・角度の同じ頂点の並べ替え・全部の面を押し出す向きのように、丸めの誤差で決まる状態は、突き合わせの入力に入れない（打ち切った数と最初に変わった項目は生成器が標準出力に出す）。ほぼ重なった頂点（0 ≤ 距離 < 1e-6）は、距離でマージして消してから続ける。最後の確認（probes）も、変わるものは外す。確認用に、JS 側をずらして C# と突き合わせられる（`PRACTICE_TRIG_NOISE=<シード>:<割合 %>`。`practice-noise.mjs`）。ずらし方 8 通り（生成用）とは別のシード（割合 3〜12%）を複数回試して、195 件すべて成功した。

### 9.3 突き合わせの網羅 [実測]

入力は `tests/js/practice-edit-cases.json`（`node tests/js/practice-edit-cases.generate.mjs` で作る。決まった乱数の種）。**JS を動かしながら作る**ので、頂点・辺・面の画面上の位置を JS の状態から調べて、そこをクリック/ドラッグする。

- **127 シナリオ・約 18,800 手順**（手書き 51 + お題の通し 1 + 乱数 短 60 + 長 14。800 手順以上が 5 本・300 手順以上が 11 本）。S0 の 82 シナリオ・約 5,650 手順・お題 314 状態もそのまま通る（新しい土台で。S0 の入力にも誤差に強い列だけを残す処理を足した）。
- 手書きの内訳: 編集モードの出入り、押し出し（面の領域・辺・頂点、軸・数値・Esc・取り消し）、差し込み、ベベル（成功と、辺なし/面が 2 つでない辺/開いたメッシュの失敗）、ループカット（本数・位置調整・数値・中央にもどす・キャンセル・四角形なし）、削除/溶解/マージ/面を張る/分離、選択（クリック・ボックス・Alt+クリックのループ・Ctrl+Alt+クリックのリング・L・Ctrl+L・拡大/縮小・全選択/解除/反転・選択モードの変換）、取り消し/やり直し（50 件の上限・redo の破棄・モードの復元）、追加（6 種）・複製・削除、ミラー・統計・透過・N パネル（不正な値・|y| > 90°）、Home/テンキーの視点（編集モードの点・線の箱を含む）、軸にそろえた平行投影での選択、クリックで拾う半径（14 / 12 ピクセル）、Alt+クリックの動き出し（5 ピクセル）、隠れ判定の許容（0.002）、`wheelNeedsFocus`、`canTransform`、共同練習（`applyRemote`・`loadRoomScene`・`roomState`・`setView`）、リセット。
- **お題 18 件の通し**: 実際のイベントと `checkTasks` の経路で、練習 1 の 18 件を順に達成する操作列（`practice.js` の `Practice` を本物のまま動かす）。テストは JS 側が 18 件目まで進んだことも確かめる。
- JS の出力でのイベント（`key` を除く）の数 [実測]: 選択 `click` 709・`shift-click` 284・`box` 269・`loop` 116・`ring` 79・`linked` 62・`grow` 48・`shrink` 45・`all` 374・`none` 624・`invert` 118、`opstart` G 450・R 72・S 75・I 99・B 55・slide 104・loopcut 305、`confirm` G 343・R 47・S 54・I 84・B 36・slide 104、`cancel` G/R/S/I/B 各 15〜106・loopcut 195、押し出し `region` 137・`edge` 50・`vert` 50、`fill` face 47・edge 3、`merge` center 34・cursor 28・distance 218、`delete` vert/edge/face/dissolve 各 20〜31、`separate` 43、`add` 6 種各 18〜23、`duplicate` 44、`undo` 392・`redo` 37、`mode` 430、`selectmode` 802、`view` 538、`opfail` 約 1,000（B 222・E 205・I 240・G/R/S 各 70〜81・Tab 110）、状態表示の文字 119 通り。`prevented`（preventDefault を呼んだ）は 18,946 手順中 9,145。
- 1 手順ごとに比べるもの（40 項目余り）: オブジェクトごとの位置・回転（quaternion とオイラー角）・スケール・名前・部屋の ID・ミラー・輪郭の表示・**メッシュの頂点/面/孤立辺/選択した頂点・面・辺（選んだ順）**、選択・アクティブ、モード・選択モード・編集中のオブジェクト・透過・統計、視点・カメラ、左下の文字・数値入力の文字・視点/モードの表示・統計の行・ボタンの状態・N パネルの入力欄、操作中の状態（種類・軸・数値・押し出しの法線・差し込み/ベベルの上限・ループカットの本数）、ループカットのプレビュー（辺・端点）、メニュー、軸線、取り消し/やり直しの数、フォーカス、`editInfo`（選んだ面の中心・法線・統計）、`roomState`、お題の進み、出たイベント、preventDefault。数値は 1e-9 の相対誤差まで、文字・真偽は完全一致。メッシュは前の手順から変わったものだけ JS が出力する（出力が 100 MB ほどに収まるようにするため。オブジェクトが増え続けないよう、乱数の列は 6 個までにしている）。面積がほとんど 0 の面の法線だけは比べない（向きが丸めの誤差で決まる）。
- **変異テスト [実測]**: 次の 42 の変更（C# の係数・許容値・面の向き・選択の順番・上限など）を 1 つずつ入れて、突き合わせが失敗することを確かめた。**41 件を検出**。検出できなかった 1 件は外接球のふるいを消す変更で、ふるいは外接箱・三角形の判定を速くするだけの条件（外接球に入らないレイは三角形にも当たらない）なので、結果が変わらない（等価）。最初の実行で見逃した 5 件（隠れ判定の最小の許容 0.002・クリックの半径 14 と 12・ボックス選択の境界・Alt+クリックの動き出し 5）は、境目を試すシナリオを足して検出できるようにした。
  差し込みの最小 sin（0.1）・厚さの上限（0.95）、ベベルの幅の上限（0.45）・`sinBetween` の下限（0.2）・k = 2 の頂点の順、隠れ判定の許容（最小 0.002・比 0.002）、マージの距離（1e-4）・残す頂点、お題の法線（0.9）、押し出しの側面の向き・頂点の孤立辺の向き、面を張る向きの反転・並べ替えの昇降、`flush`(辺) の導き方、ループカットの面の向き・本数の上限（20）・ホイールの向き・位置調整の範囲（0.98）、溶解の頂点、辺リングの四角形だけ、拡大、つながりの探索、クリックの半径（頂点 14・辺 12）、ボックス選択の境界、Alt+クリックの動き出し（5）、取り消しの上限（50）・やり直しの記録・復元のアクティブ、`duplicate` の取り消しのまとめ、N パネルの桁、パンの係数、差し込みのマウスの向き、平行投影の near、`Home` の対象、編集中の点・線の箱、外接箱のふるい、`rotation` のオイラー角を保つ、編集に入るときの `flush`、部屋の ID の乱数の消費数。
- 追加の単体テスト（`EditMeshTests`）: `OrderedSet`・`Splice`・`parseFloat`・メッシュの出し入れ（6 種）・押し出し/差し込み/ループカット/ベベルのあとも閉じた多様体（オイラーの標数 2）・溶解/マージ/分離・カメラ行列・**`tools/verify/practice.mjs` の `/practice` の操作を C# に通すと `practice-baseline.json` と同じになる**（物体の位置/回転/スケール・モード・視点・お題の達成数と次のお題）。ブラウザでの実行は C# 版を画面に接続する S3 で。
- テストの数: `Practice.Tests` は 33 → 195 件。`dotnet test` の所要は 1 分半ほど（うち Node で JS を動かすのが 8 秒 + 7 秒）。

### 9.4 S1 のあとに残る対象外と、見つけた現行の挙動

- **意図した差**: `duplicate()` の直後の移動が `canTransform` で始められないと、JS は `this.op.fromDuplicate` で TypeError になり、複製の取り消しの記録も消える。C# は例外にせず、複製と取り消しの記録を残す（複製は新しいオブジェクトを選び直すので、部屋では起きない）。
- **JS の不具合・癖で、C# も同じにしたもの**（直すかは別の判断）: ① `emit("extrude", { type })` は `{ type, ...detail }` でイベントを作るので、イベントの種類が `region`/`edge`/`vert` に上書きされ、`extrude` として届かない（誰も `extrude` を待っていないので実害なし）。② マージのあと、選択の集合（`fsel`/`esel`）に消えた面・辺が残る（統計の「面 a/b」や `editInfo` に出る。次の選択操作で消える）。③ 軸にそろえた平行投影で、隠れているかの判定が最後の桁の丸めで決まる（前面図で、後ろの頂点がボックス選択で選べたり選べなかったりする）。④ 全部の面を押し出す（法線の和が 0）と向きが丸めの誤差で決まる。⑤ ぴったり重なった頂点が 1 ulp だけずれると、クリックでどちらが選ばれるか・面を張る順・面の向きが丸めで決まる（「距離でマージ」で解消できる）。
- **突き合わせの対象外**: チュートリアル用の補助（S4）とほかの参加者の表示（S6）。編集モードの点・線・面の重ね描きの中身（`rebuildOverlay`）は、S2 の描画層が `EditMesh` から作る（現行は `rebuild` / `setSelectMode` / `setXray` / `setMode` でだけ作り直す。**古い状態が出る場面があれば JS と同じにする**必要がある）。マウスの座標の範囲判定（§7.3 ④）と、フレーム単位のカメラ（③）は S3 で接続するときに確認。ボックス選択の境界が頂点の画面位置にぴったり重なる場合と、「ほぼ平行」な状態は、三角関数の誤差に弱いので入力に入れていない。
- 現行の `viewport.js` は S1 では `-.` の 1 行だけを直した（#106）。それ以外は直していない。

### 9.5 見積もりとの差

- 当初の見積もり（10〜15 人日 [推測]）に対し、人日では測っていない（エージェントの 1 回の作業）。**行数**は S0 の約 2.3 倍（`Practice` が 約 1,320 → 約 4,400 行）で、見積もりの前提（1.5〜2 倍）より少し大きい。**突き合わせの土台の作り直し**（本物のコンストラクタを動かす、カメラとレイ判定を three.js と同じ手順にする、三角関数の誤差に強い列だけを残す生成器、変異テスト）に作業の半分近くを使った。この土台は S4（シナリオの突き合わせ）・S5（Unity 風）にも使える。
- 残りの見積もり（S2〜S7）は、この結果では変えない。ただし S3 以降の描画は「JS と同じ状態になる」ことを土台が保証するので、描画の画像比較に集中できる。

## 10. S2 の準備

### 10.1 S2 の最初にやること: 「新しいコンテキストで描画が始まらない」問題の切り分け

**記録された症状**（draft PR #70 の (e)）: Edge 155 で CDP 計測中、新しいブラウザコンテキスト（`Target.createBrowserContext`）では KNI・WebGL の両方で描画が始まらない状態になった（既定のタブでは正常）。再計測できず、原因は未特定。

**S1 の作業で得た関連の観測 [実測]**: three.js 版の `/practice` でも、**システムの Chrome**（`C:\Program Files\Google\Chrome`）+ `--use-angle=swiftshader --enable-unsafe-swiftshader` では `THREE.WebGLRenderer: A WebGL context could not be created. Reason: Could not create a WebGL context, VENDOR = 0x10de, DEVICE = 0x2684 ... Web page caused context loss and was blocked` で canvas が作れず、`tools/verify/practice.mjs` が「`.practice-viewport canvas` を待って 60 秒で失敗」した。**Playwright 同梱の Chromium**（`ms-playwright/chromium-1208`）では同じページが動いた。→ 「描画が始まらない」の少なくとも一部は、コードではなく**ブラウザ/GPU の環境側**（コンテキストの生成の失敗・GPU プロセス・ページごとのコンテキスト喪失のブロック）で起きうる。

**手順（結果は本書に追記する。特定するまで S3 に進まない）**:

0. **対照を決める**: 同じ環境・同じ操作で**現行の three.js 版 `/practice` も動くか**を最初に調べる。three.js 版でも同じ症状が出るなら、C# の問題ではなく環境の問題で、S2 の完了条件は「three.js 版と同じ条件で動く」になる。
1. **再現環境**: `experiment/csharp-3d` を `git worktree` で取り出し、`WebGl` を `dotnet publish` して静的配信（`measure/static.mjs`）。ブラウザは PR #70 と同じ Edge 155（CDP: `--remote-debugging-port`）と、Playwright 同梱の Chromium の両方。新しいコンテキストは CDP の `Target.createBrowserContext` + `Target.createTarget` で作り、既定のタブと**同じ計測スクリプト**で比べる。
2. **失敗した文脈と成功した文脈の両方で同じ値を記録する**（3 秒間）: `document.visibilityState`・`document.hasFocus()`・`document.hidden`、`requestAnimationFrame` の呼び出し回数と `performance.now()` の進み、`canvas.getContext("webgl2")` の戻り（null か）と `webglcontextcreationerror` / `webglcontextlost` / `webglcontextrestored` の `statusMessage`、`gl.isContextLost()`、`WEBGL_debug_renderer_info` の `UNMASKED_RENDERER_WEBGL`、キャンバスの `clientWidth/Height`（0×0 のままではないか）、コンソールのエラー/警告、`Blazor` / `dotnet.js` の起動の完了（Cache Storage が空の文脈で、起動用ファイルの取得が終わるか）。
3. **仮説を 1 つずつ確かめる**（[推測]。変える値は 1 回に 1 つ）:
   - H1 非表示タブ → rAF が呼ばれない: `visibilityState` と rAF の回数。確かめ方は `Page.bringToFront`・`Emulation.setFocusEmulationEnabled`、起動オプション `--disable-renderer-backgrounding --disable-background-timer-throttling --disable-backgrounding-occluded-windows`、Windows の遮蔽判定 `--disable-features=CalculateNativeWinOcclusion`。
   - H2 WebGL コンテキストの生成の失敗・上限: `getContext` の戻りと `webglcontextcreationerror`。直前までのコンテキストを閉じずに新しい文脈を作った数（Chrome はページごと・全体でコンテキスト数に上限があり、超えると古いものを捨てる / 「Web page caused context loss and was blocked」でブロックする）。`--use-angle=swiftshader` と `--disable-gpu`、既定の GPU の比較。上の [実測] と同じ症状かを見る。
   - H3 Blazor/WASM の起動が新しい文脈で終わらない: 起動の完了の目印（`Blazor.start` の完了、最初の `[JSExport]` 呼び出し）の有無。HTTP キャッシュを無効にした場合の Cache Storage（`dotnet-resources-*`）への保存と整合性の検証。
   - H4 大きさが 0 のまま描画しない: 現行の `viewport.js` の `resize()` は幅か高さが 0 だと何もせず、画面外のあいだ描画を止める（`IntersectionObserver` の `onScreen`）。C# 側（`ViewportCore.Resize` は 0 のとき縦横比を変えない）と gl.js の描画ループが同じ罠にはまらないか。新しい文脈でウィンドウが最小化/未サイズのとき、`ResizeObserver` の初回通知が 0×0 で、その後に通知が来ないと描画が始まらない。
4. **特定できたら**: (i) 再現手順を `tools/verify/` に足して 3 回続けて再現/非再現を確かめる、(ii) `gl.js` に対策（可視状態の変化・サイズが 0 でなくなったとき・`webglcontextlost` からの復帰で描画を再開）を入れる、(iii) 新しい文脈 20 回で全部描画が始まることを確かめる（`practice.mjs` の往復 20 回と同じ作り）。
5. **特定できないとき**: S3 に進まず、ユーザーに報告して「three.js 版でも同じ症状が出る環境は対象外にする」かを判断してもらう（§5 のとおり）。

### 10.2 S2 への提案

1. 上の切り分けを**最初に**やる（描画層を作り込む前に。環境の問題なら、描画層の設計が変わらない）。
2. 描画層が読むもの: `ViewportCore` の状態（`Objects`・`Selected`/`Active`/`EditObj`・`Mode`/`SelectMode`・`Xray`・`Tools`・`View`・`AxisLine`・`LoopCut.Preview`・`Op`）と、`SceneObject` の `Triangles`/`TriFace`/`MatrixWorld`/`Mirror`、`EditMesh`（編集モードの点・線・選んだ面は描画層が `EditMesh` から作る）。描画は**状態を読むだけ**にして、ロジックに書き戻さない。三角形の法線は、面ごとに `EditMesh.FaceNormal`（描画層で Float32 にする）。
3. `rebuildOverlay` の呼ばれ方（§9.4）を、S2 の画像比較の入力に含める（`setXray`・`setSelectMode`・`rebuild` の直後だけ作り直す現行の挙動が、画面で見えるか）。
4. AOT なしの負荷は S2 で測る。ネイティブ .NET での参考値 [実測]: UV 球（482 頂点・960 三角形）で、頂点モードのクリック 約 1 ms、**全体のボックス選択 約 42 ms**（隠れ判定のレイを頂点ごとに三角形 960 個と）、全頂点を G で動かす 1 回のマウス移動 約 0.15 ms、全面の押し出しの開始 約 4 ms、取り消し 1 回 約 13 ms。WASM の解釈実行（AOT なし）は数倍〜数十倍遅い [推測] ので、ボックス選択と取り消しが予算（1 フレーム 16 ms のうち 5 ms）を超える可能性がある。重いなら、三角形の前に外接箱で絞る（外接球は既にある）、隠れ判定の頂点ごとのレイの結果を使い回す（同じレイを頂点の重なりで何度も撃たない）、`Restore` が全オブジェクトを作り直すのをやめる（編集中のオブジェクトだけ）などを検討する。AOT（`wasm-tools`）の導入は、測った結果を見てユーザーに相談する（§8-5）。
5. 画像比較（SwiftShader）は、Playwright 同梱の Chromium で行う（システムの Chrome は上のとおり WebGL を作れなかった）。
