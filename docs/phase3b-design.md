# Phase 3b 設計メモ: 練習ページの 3D の C# 化

計画書（`docs/wasm-migration-plan.md`）の §4.5・§5 Phase 3b・D15 の続き。Phase 3b は**任意・後続**で、本番公開（master）の条件には含まれない。ここに書くのは、着手前の調査と設計、最初の一歩（純粋なロジックの C# 化）の記録で、画面の挙動はこの段階では変えない。
印の意味: **[実測]** はこの作業で測った・確かめた値、**[試作]** は draft PR #70 の記録（この作業では再計測していない）、**[推測]** は根拠のある見積もりで未検証。

## 0. 要点

- **描画層の推奨は (A) WebGL の薄い自作層**（`[JSImport]` で WebGL 2 を呼ぶ小さな JS と、描画命令を作る C#）。KNI は採らない。理由は §3: 陰影を three.js に合わせられる（試作で面の明るさが ±2 以内。KNI は左の面が 23 対 70 で大きく外れる）、追加の配信物が小さい、シェーダーを自分で持てる、KNI は .NET 10 での動作が未確認で、試作の未解決の問題（Tick のばらつき）の原因が KNI 側にあるか自分たち側にあるかも未切り分け。ただし**判断の関門を §4 の段階 2 に置く**（実装前に、画像の比較で A を確定する）。
- **C# 化の単位は 4 層**: ① 純粋なロジック（`FaraAvatarModdingGuide.Practice`。この PR で着手）② 描画層（段階 2 で作る別プロジェクト）③ 画面の部品（Razor。HUD・お題の一覧・メニュー・Unity 風のパネル）④ 通信（共有ルームの WebSocket）。three.js を配信物から外せるのは、**記事内チュートリアルと Unity 風も C# に移ったあと**（`unity.js` と `tutorial.js` が three.js を使っているため）。それまでは three.js の同梱・importmap・CSP のハッシュは残る。
- **S2 の成果（済。§11）**: 描画層 (A) を実装し、**§3 の判断基準 3 つを満たして (A) を確定**した。面の明るさは three.js と同じ値（上 112・左 70・右 80。差 0）、新しいコンテキストで描画が始まらない原因は**非表示のウィンドウで rAF が呼ばれないこと**と特定して対策済み（コンテキストそのものは無関係）、止まった画面・視点の操作の毎フレームの C# は 0.24 ms（AOT なし）。**重い操作は AOT なしだと予算を超える**（全体のボックス選択 212 ms。密なメッシュの全頂点を編集モードで G ドラッグ中の 1 フレームも 8.1 ms）ので、AOT は §11.6 の材料で相談する（勝手に有効化していない）。
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
| **S2**（**済**。§11）描画層の試作と判断の関門 | 薄い WebGL 層（`gl.js` + C#）。練習 1 のシーン（方眼・軸線・オブジェクト・輪郭・3D カーソル・編集モードの点線面）を描く。画像比較（SwiftShader。3 面の明るさ・縁・線の太さ）。新しいコンテキストの問題と AOT なしの負荷の測定。§3 の判断基準で (A) を確定 | §3 の 3 条件。結果をこの文書に追記（ADR）。ユーザーの確認 | 8〜12 |
| **S3**（**済**。§13）`/practice` を C# で（フラグ付き） | ページ（Razor）+ HUD + お題の一覧を Razor で。`?engine=cs` で C# 版。`tools/verify/practice.mjs` を両エンジンで通す（`practice-baseline.json` と一致）。往復 20 回でリスナー・コンテキストが増えない。画像比較の結果を PR に載せる | Phase 3 と同じ操作（キー・マウス）ができる。**描画の差はユーザーが受け入れる**（画像で提示）。既定は JS のまま | 6〜9 |
| **S4**（**済**。§14）記事内チュートリアル | `scenarios.js` の Blender 9 本と `tutorial.js` を C# に（`?engine=cs`。フラグ付き）。シナリオは C# のデータ + 述語（イベント → 達成）。「画像で見る」とタッチ端末の扱いは維持。Unity 2 本は S5（`unity.js` の画面に載っているため。**S5 で済**）。`guideLine`（案内の点線）の描画も S4 | 9 本を JS 版と C# 版で同じイベント列に通して、手順の定義・再生後の状態・案内・達成/失敗/ヒントの答えが一致するテスト。記事を画面で確認し、**Three.js 版と C# 版の画素・文字・DOM の差を 0 にする**（§14.3） | 8〜12 |
| **S5**（**済**。§15）Unity 風 | `unity.js` を Razor（Hierarchy・Inspector・Project・メニュー・SDK パネル）+ Scene ビュー（②の描画層）に。`unity-practice.js` のお題 7 件。ツリー操作・Setup Outfit・タグ・マテリアルのロジックは①に | `practice.mjs` の `unity` の基準と一致。階層・お題の状態をイベント列で JS と突き合わせ | 12〜18 |
| **S6**（**済**。§16）共有ルーム | `room.js` を C#（規則は `RoomSession`。WebSocket などは `room-cs.js`。§16.2 の変更点）に。差分の送受信・ロック・カーソル・選択・視点の共有。`tools/room-check` と画面からの二人の確認 | 二人が入れる・追加/移動が届く・退室が消える。プロトコルは変えない | 5〜8 |
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
2. **S1 以降の進め方**: 段階ごとに報告して続行の確認を取る形でよいか。（S1・S2 は完了。次は S3。§11.7）
3. （決定 2026-10-24・実施済）現行 JS の `-.` の不具合は JS 側も直した（#106。`tools/verify/practice.mjs` は 58 件すべて ok・基準は変わらない）。
4. **FBX**: 今は読み込みの実装が無く、新機能として別枠にする。優先度と、必要な機能（アバターの表示だけか、ファイル照合も含むか）。
5. **AOT（`wasm-tools`）**: 必要になった場合に導入してよいか（サイズが増える。SDK 全体に影響）。S2 の測定の結果は §11.6。**導入するかの判断はユーザーに相談する（未導入のまま）**。
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

## 11. S2（描画層）の記録

印: **[実測]** はこの作業で測った・確かめた値、**[推測]** は根拠のある見積もりで未検証。計測は Windows 11・Playwright 同梱の Chromium 1208（SwiftShader のソフトウェア描画）・.NET 10.0.402・**AOT なし**（`wasm-tools` 未導入）。配信物は変えていない（`Web` からは参照しない。サイトに出るファイルは増えない）。

### 11.1 作ったもの

| 場所 | 内容 |
| --- | --- |
| `src/FaraAvatarModdingGuide.Render`（新。純粋。JS・Blazor に依存しない） | `SceneRenderer`: `ViewportCore` の状態を読んで、1 フレームの描画の命令（`Frame`: 頂点バッファ・描く順の一覧・行列）を作る。方眼・軸線・オブジェクト（陰影付きの面・選択の輪郭 `EdgesGeometry(g, 20)`・ミラー）・3D カーソル・編集モードの点/線/選んだ面・軸固定の線・ループカットの線。描く順は three.js と同じ（不透明は renderOrder → 足した順、透明は奥から）。`FramePacker`: 1 回の JS 呼び出しで渡す float の列。`RenderBench`: AOT なしの負荷の測定場面 |
| `Practice` への小さな追加 | `Mat4.Identity/Multiply/Determinant/FromElements`、`OrbitView.Matrices()`、`SceneObject.TriNormals/Version/XrayApplied`、`ViewportCore.OverlayRebuilt/OverlayRemoved`（現行が `rebuildOverlay` を呼ぶ所だけで通知する。§9.4 の「古い状態」を再現するため）。操作の結果は変わらない（Practice.Tests 195 件はそのまま通る） |
| `src/FaraAvatarModdingGuide.RenderLab`（新。サイトには載せない） | 最小の Blazor WebAssembly（Web と同じ net10.0・同じパッケージの版・`InvariantGlobalization`）。`wwwroot/js/gl.js`（薄い WebGL 2 層。約 400 行。入力は pointercancel / lostpointercapture でもドラッグの終わりを C# に伝え、始めるのに失敗したら canvas・リスナー・tabindex を戻し、`uploadBuffer` / `draw` は長さ・種類を検査する）と `[JSImport]`。ポインター・ホイール・キーは `gl.js` のネイティブのリスナー（`passive: false` の wheel を含む）が数値にして C# に渡す。**S3 でこの中身を `/practice` に移す** |
| `tools/verify/gl-*.mjs` ほか | `gl-serve.mjs`（確認用の静的サーバー。本番の CSP を付けられる）、`gl-ref.html`（three.js 版だけを出す参照）、`gl-compare.mjs`（画像比較）、`gl-measure.mjs`（計測）、`gl-csp.mjs`、`gl-context-matrix.mjs`/`gl-probe.*`（§11.2） |
| `tests/FaraAvatarModdingGuide.Render.Tests`・`tests/js/practice-render-reference.mjs` | three.js との突き合わせ（§11.3）と単体テスト |

設計の要点: 描画は**状態を読むだけ**でロジックに書き戻さない。バッファは変わったときだけ送る（`SceneObject.Version` で形を、`OverlayRebuilt` で編集モードの点・線・面を）。1 フレームの命令は 1 回の `[JSImport]` 呼び出し（28 float × 描画数 + 24）。`[JSImport]` の関数引数に配列は渡せない（SYSLIB1072）ので、入力は種類ごとの数値だけの関数にした。

### 11.2 「新しいコンテキストで描画が始まらない」の原因（特定した）

**原因: 非表示のウィンドウでは `requestAnimationFrame` が呼ばれない。** WebGL のコンテキストも新しいコンテキストも関係ない。

[実測]（`tools/verify/gl-context-matrix.mjs`。CDP で直接操作。Playwright は常に「表示・フォーカスあり」にするので隠れてしまう。毎回新しい `--user-data-dir`。3 秒間の rAF の回数・`visibilityState`・`getContext` の戻り・context イベント・canvas の大きさを記録）:

1. **既定のタブと新しいコンテキスト（`Target.createBrowserContext` + `createTarget`）の差は無い**: Chromium（ヘッドレス）・Edge 155（ヘッドあり・ヘッドレス）・Chrome 155（ヘッドあり）× 生の WebGL・three.js 版 × 既定/新しい、の全部で rAF は 3 秒に 約 180 回、描画できた（H3 の「起動が終わらない」・H2 の「コンテキスト数」も該当しない）。
2. **隠れると rAF は 0 回**: 新しいコンテキストのウィンドウを最小化、または別のウィンドウ（最大化）で覆うと `visibilityState === "hidden"`・rAF 0 回（コンテキストは作れている）。表示に戻すと rAF は再開する（2.5 秒で 約 150 回）。覆われた場合は `--disable-features=CalculateNativeWinOcclusion` で起きなくなる。最小化は起動オプションでは防げない（仕様）。試作（#70）の計測は、別のウィンドウに覆われうる Edge の CDP の新しいウィンドウで行っていたので、これと合う [推測。当時の状態は再現していない]。
3. **別件の環境の問題**: システムの Edge/Chrome（ヘッドあり）に `tools/verify/lib.mjs` の `--use-angle=swiftshader --enable-unsafe-swiftshader` を付けると、three.js 版でも生の WebGL でも `BindToCurrentSequence failed` / `Web page caused context loss and was blocked` でコンテキストが作れない（S1 の観測と同じ）。実 GPU のまま（フラグなし）なら動く。**Playwright 同梱の Chromium ではどちらのフラグでも動く**。

対策（`gl.js`。レンダーラボで確かめた）: 始めたときに**同期で 1 フレーム描く**（隠れていても最初の 1 枚はできる）、`visibilitychange`（表示に戻ったとき）・`ResizeObserver`（0×0 から大きさが付いたとき）・`IntersectionObserver`（画面に入ったとき）でも描く、`webglcontextlost` / `restored` で CPU 側の頂点から作り直す。[実測] 隠れた状態（最小化・覆われた。ページを開く**前**に隠す）で始めたレンダーラボは、rAF 0 回のまま 1 枚描いており（`drawn=1`）、表示に戻すと描画が続く（§10.1 の 4(i) のとおり 3 回続けて確かめた。結果は PR に載せた）。**新しいコンテキスト 20 回（キャッシュなし）で 20 回とも描画が始まった**。`WEBGL_lose_context` で失わせて 300 ms 後に戻すと、描画が再開した。開始と停止を 20 回繰り返しても、canvas は 1 つ・動いている rAF は 1 つのまま（WebGL のコンテキストの作成は 20 回 = 開始と同じ数）。

### 11.3 three.js との突き合わせ（Node で本物の three.js のシーンと比較）

`tests/js/practice-render-reference.mjs` が、S0・S1 と同じ操作列（`practice-cases.json` 82 シナリオ + `practice-edit-cases.json` 127 シナリオ）を**本物の `new Viewport(...)`** に通し、手順 0・**40 手順ごと**・最後（約 630 時点。**全手順ではない**）で three.js のシーンの中身を取り出す。C# は同じ操作を `ViewportCore` に通して `SceneRenderer` の出力と比べる。

**比べているもの**: カメラの行列、オブジェクトごとの matrixWorld・メッシュの位置と法線（面積 1e-4 未満の面の法線は除く。S1 と同じ理由）・輪郭の線と表示と色・ミラーの行列と表示、**材質と描画の設定（色・不透明度・透明（Blend）・深度テスト・深度書き込み・polygonOffset の有無と値・renderOrder・点の大きさ・頂点色・両面・表の向き（行列式が負なら CW）を、メッシュ・輪郭・ミラー・編集モードの点/線/面・軸線・ループカットの線・方眼・軸・カーソルのそれぞれで）**、編集モードの点/線/面の位置と色と表示、軸線・ループカットの線の行列と色、方眼・軸・カーソルの輪。数値は float の丸め（1e-6）・行列は 1e-9。`FramePacker` は全欄（ヘッダー 24・描画 1 つ 28）を単体テストで検証している。**Render.Tests 218 件**。入力が部品を一通り通していることも数えている（編集モードの点/線/面・透過・ミラー・軸線・ループカットの線・平行投影・複数オブジェクトの各件数の下限）。

**比べていないもの（「一致」と言えない範囲）**: 陰影（シェーダーの出力）・アンチエイリアス・GL の実際の状態（§11.4 の画像で見る）、**描く順そのもの**（three.js は renderOrder → material.id → z（手前から）→ id の順で、同じ renderOrder のあいだは材質ごとにまとまる。`SceneRenderer` は renderOrder → 足した順（透明は奥から）の近似で、three.js の内部の並びとは直接比べていない。深度テストで決まらない重なりだけが、違いうる）、サンプルしていない手順（40 手順の間）、面積 1e-4 未満の面の法線、`guideLine`・`remote-sel`（S4・S6）。

**変異で確かめた**（Render の C# を 1 か所ずつ壊して `dotnet test` を回す）: 編集面の不透明度 0.28→0.6、基本色、メッシュの polygonOffset の符号、メッシュ/ミラーの FrontFaceCw の除去、点の大きさ、編集線の renderOrder、カーソルの renderOrder、カーソルの DepthTest の 9 件すべてが落ちた（以前のテストは、これらの材質・状態を比べておらず生き残っていた）。

### 11.4 画像比較（`tools/verify/gl-compare.mjs`。three.js 版と同じシーン・同じ操作）

| 条件 | 結果 [実測] |
| --- | --- |
| **(a) 面の明るさ**（立方体の既定の視点。中心 11×11 ピクセルの平均） | 上 112・左 70・右 80、three.js と**同じ**（差 0）。基準は ±3。試作は ±2 だった。シェーダーは three.js r170 の MeshStandardMaterial（Lambert/π の半球光 + 平行光 + GGX の鏡面反射。半球光の上は three.js のまま +Y）、sRGB 出力 |
| 画素の差（どれかのチャンネルが 8 を超える画素）。**合否に含める**: 割合 0.15 % 以下・最大 40 以下（現状の最大 0.081 % / 25 に余裕を付けた値） | 12 シーン（既定の立方体・3 オブジェクト + 選択の輪郭・回した立方体と平面・編集モード（頂点全選択・面 1 つ・球の面）・透過表示・ミラー（編集モードで形を X に 2 ずらしてから付けるので、鏡像が実際に写る。最後の操作で C# 版の画素が 9,019 変わる）・**鏡面反射が効く視点（光の反対側から地平線すれすれに球を見る。GGX は最大 20 階調ほど。ふつうの視点では 1 階調未満で、画像では見分けられない）**・軸固定の G の線・平行投影・ループカットの線）で 0.000〜0.081 %。最大の差は 25（線の端の画素。アンチエイリアスの差）。**最大は透過表示（0.081 %。最大 9、平均 0.36）**: 半透明の面の重なりの順の差と見られる [推測] |
| `gl.js` を壊す変異で NG になるか（実際に確認） | 鏡面反射の削除 → `specular-grazing` が NG、polygonOffset 無効 → 8 シーンが NG、点の大きさ半分 → `edit-vert-all`・`loopcut-preview` が NG、半透明ブレンド無効 → 5 シーンが NG、`frontFace` を常に CCW（`FrontFaceCw` の除去と同じ）→ `mirror` が NG |
| スマホ幅 390（219 高） | 全シーン同じ水準（上 112、左と右の値も three.js と一致、画素の差 0〜0.054 %、最大 25） |

### 11.5 CSP・サイトの確認

- レンダーラボを**本番と同じ CSP**（Build が書いた `_headers` の `script-src 'self' 'wasm-unsafe-eval' …`）で配信し、PC（1280）・スマホ（390）で、クリック・Tab・A・G・Enter・ホイールを操作: **CSP 違反 0・コンソールのエラー/警告 0・失敗したリクエスト 0**（3 回）。ホイールでページはスクロールしない（試作の問題を解消）。キャンバスは大きさどおり（960×540 / 390×219）。
- サイト（Web）は変えていない: `dotnet build` / `dotnet test`（Content 26・Build 47・Web 196・Practice 195・Render 218）、`node --test`（105）、`verify-site`（本番・確認用）、`wrangler deploy --dry-run`（本番・dev）が通る。`practice.mjs`・`csp-check.mjs`（`wrangler dev` に対して）の結果は PR に載せた。

### 11.6 計測値と AOT の判断の材料（AOT なし）

**転送量**（brotli。Cloudflare が配信時に圧縮する。レンダーラボの publish と Web の publish のファイル一覧の差。ランタイム・System.* はサイトが全ページで既に読むので増えない）:

| 追加されるもの | 生 | brotli |
| --- | --- | --- |
| `FaraAvatarModdingGuide.Practice`（操作ロジック 約 4,400 行。WebCIL） | 214 KB | **73.7 KB** |
| `FaraAvatarModdingGuide.Render` | 48 KB | **18.4 KB** |
| `gl.js` | 17 KB | **6.2 KB** |
| `System.Runtime.InteropServices`（ファサード） | 9 KB | 2.9 KB |
| **合計（S3 以降、サイトに載ったとき）** | | **約 101 KB** |
| 参考: 消える three.js（S7） | 1,315 KB | **207 KB** |

（S3〜S6 が増える分は含まない。Practice は S0 の約 28 KB から、ロジックが 4 倍になって 73.7 KB。）

**起動**（localhost・キャッシュなし・SwiftShader。冷たい起動 5 回。`performance.mark` で区間に分けた）: 読み込み開始から最初の描画まで 228〜295 ms（中央 229 ms。1 回目だけ遅い）、同じコンテキストの再読み込み 172 ms。内訳（中央値）: 読み込み開始 → `Main` の開始 **104 ms**（ランタイムの起動。**サイトでは全ページが既に払っている**）、`Main` → `gl.js` の読み込み完了（ホストの組み立て + `ImportAsync`）**43 ms**、→ `gl.start()` の呼び出しまで（`ViewportCore`・`SceneRenderer` を作る。解釈実行の初回）**16 ms**、`start()` 内のコンテキストとシェーダー **5 ms**、最初のフレーム（C# の `Build`・送信・描画）**17 ms**（初回だけ。解釈実行の初回の呼び出しが遅い）。**3D のために増える起動は、中央値の足し算で約 80 ms**（43 + 16 + 5 + 17。区間ごとの中央値を足したもの）。区間の合計 185〜190 ms（`gl:firstPaint` の mark は読み込み開始から 188〜191 ms。1 回目は 261 ms）が、上の 229 ms と合わないのは、229 ms が「最初の描画を観測した時刻」（`setInterval` の 5 ms ごとの確認が、メインスレッドが空いてから走る）だから。`run-returned`（190 ms）のあと、`Main` の残り（`RunAsync` による Blazor の起動の仕上げ）にメインスレッドが約 40 ms 使われ、その間は確認が走らない [推測。この区間は mark で分けていない]。利用者に見えるのも、そのあとの合成（描画の反映）になる。レンダーラボ全体の展開後の転送は約 7.0 MB（ランタイム含む）。

**フレーム**（球 960 三角形。ページ内。C# の 1 フレーム = `Build` + 詰める + `uploadBuffer`/`draw` の呼び出し）: オブジェクトモード **0.24 ms**（JS から見た呼び出し全体 0.29 ms）、編集モードで全選択（何も変わらない間）0.37 ms。rAF は 60 回/秒のまま。`Build` + 詰めるだけでは 0.060 ms（オブジェクト）・0.066 ms（編集）。→ 止まった画面・視点の操作では、予算（1 フレーム 16 ms のうち 5 ms）に**十分収まる**。**ただし編集モードで密なメッシュ（球 960 三角形）の全頂点を G でドラッグしている間は、マウスの移動のたびに形が変わる**（現行 JS も毎回 `rebuild` → `rebuildOverlay` で作り直す）ので、`Build`（法線・輪郭 `EdgesGeometry`・点/線/面の作り直し）+ 送信が毎フレーム走る。下の表の `drag-edit-frame`（**ネイティブでも WASM でも下限**: `Build` + 詰める + 変わったバッファの float の数え上げまでで、`uploadBuffer`（MemoryView のコピーと `bufferData`）・`draw`・GL の呼び出しは含まない。実際のドラッグ中のフレームは、これに加えて 1 フレームあたり数 ms の JS/GL の分が乗る [推測。ページ内では測っていない]）。

**重い操作**（球 482 頂点・960 三角形。1 回あたりの ms。同じコードを、ネイティブ（この PC）と WebAssembly（AOT なし）で）:

| 操作 | ネイティブ | WASM（AOT なし） | 倍率 | 予算 5 ms |
| --- | --- | --- | --- | --- |
| オブジェクトのクリック | 0.09 | 0.53 | 6 | 内 |
| 頂点のクリック（編集モード） | 0.81 | 4.6 | 6 | **ぎりぎり** |
| **全体のボックス選択** | 21.5（17〜22。別の処理が走っていると 44） | **212** | 10 | **超える（42 倍）** |
| 全頂点を G で動かす（マウス 1 回。核のみ） | 0.24 | 1.5 | 6 | 内 |
| **全頂点を G でドラッグ中の 1 フレーム**（マウスの移動 + `Build` + 詰める + 変わったバッファの取り出し。形が毎回変わる） | 1.19 | **8.1** | 7 | **超える**（16 ms のフレームには収まる） |
| 全面の押し出しの開始（E） | 10.2 | **45** | 4 | **超える** |
| 取り消し 1 回 | 2.7 | **12.5** | 5 | **超える** |

**AOT の相談の材料**: ① 止まった画面・視点の操作の描画は AOT なしで十分（0.24 ms）。② 超えるのは、**ユーザーが 1 回の操作で 1 回だけ起こす重い処理**（ボックス選択・押し出しの開始・取り消し）と、**密なメッシュの全頂点のドラッグ中の毎フレーム（8.1 ms。16 ms には収まるが、予算 5 ms は超え、ほかの処理の余裕が小さい）**。重い 1 回の処理は、208 ms がクリックの遅れとして感じる程度（フレームが落ち続けるのではなく、1 回止まる）。ドラッグは、頂点が少ないメッシュ（立方体など。練習の多くのお題）なら問題にならない [推測。立方体の `drag-edit-frame` は測っていない]。③ AOT（`wasm-tools`）で数倍〜10 倍速くなると言われるが [推測。測っていない]、配信が増える（Practice 以外のアセンブリも WASM にコンパイルされ、数 MB 級になりうる）・CI と SDK に workload が要る・ビルドが遅くなる。④ **AOT を入れる前にできること**（§10.2-4）: 外接箱での絞り込み、隠れ判定のレイの結果の使い回し（ボックス選択の 208 ms の大半。頂点ごとに 960 三角形を試している）、`Restore` が全オブジェクトを作り直すのをやめる（取り消し 12.8 ms）。ネイティブで 17 ms を 3 ms 以下にできれば、AOT なしで 20〜30 ms になる [推測]。**判断はユーザーに相談する**（S3 に進む前でも後でもよい。S3 の画面はこの重い操作でも動く）。

### 11.7 判断基準 (§3) の結果と、確証が持てない点

- (a) 面の明るさ ±3 以内 → **差 0**。(b) 新しいコンテキストで描画が始まる → **20/20**（原因は非表示の rAF。§11.2）。(c) 毎フレームの CPU が予算内 → 止まった画面・視点の操作は **0.24 ms**（満たす）。**編集モードで 960 三角形の全頂点を G ドラッグ中は 8.1 ms で、5 ms の予算を超える（16 ms のフレームには収まる）**ので、(c) は「通常の操作では満たす。密なメッシュのドラッグは超える」。基準は §3 のとおり「重い部分だけを小さくする」で対処する対象（輪郭 `EdgesGeometry` を形が変わるたびに作り直さない・法線を変わった面だけ更新する・AOT）で、**描画層 (A) の選択を変える結果ではない**（KNI でも同じ C# のロジックが走る）。(a)(b) は満たしたので **(A) を確定**（KNI は採らない）。
- 確証が持てない点: ① 試作の「描画が始まらない」の当時の状態は再現していない（原因の特定は、隠れたウィンドウで rAF が止まることの実測と、試作の計測の仕方からの推定）。② 計測は SwiftShader（ソフトウェア描画）の Chromium で、GPU・Firefox・Safari は未確認（WebGL 2 の標準 API だけを使っている）。③ 透過表示の画素の差（最大 9）の原因は推定（**S4 で特定して直した**: 透明で両面の材質を裏 → 表の 2 回に分けて描く。§14.3 ⑧）。④ 軸固定の点線ガイド（`guideLine`。チュートリアル用）・ほかの参加者の枠（`remote-sel`）は S4・S6 で足す。⑤ 初回の実行で `Failed to load resource: 404` が 1 回出たが、URL を記録して 3 回やり直した結果は 0 件（一過性と見られる）。
- S3 の前提: `gl.js`・`Lab.cs` の入力の橋渡しを `/practice` のページ（Razor）に移し、HUD・お題を Razor で作る。**`Lab.ParseQuery`（`?scene=`・`?view=`・`?angles=`・`window.__labControl`・ベンチの口）はレンダーラボの確認用で、S3 には持ち込まない**（`/practice` の最初のシーンは `Practice.razor` が決める。`?engine=cs` のフラグは §4.1）。`SceneRenderer` は `ViewportCore` を作ったあと、操作の前に作る（`OverlayRebuilt` の通知を取りこぼさないため。クラスの契約に書いた）。

## 12. AOT なしの軽量化の記録（S2 の計測 §11.6 のあと。ユーザー判断 2026-10-24）

「まず AOT なしの軽量化（§10.2-4・§11.6 の 4 点）を行い、再計測してから AOT を判断する」。**AOT は有効にしていない**。結果が変わらないことは、S0・S1 の突き合わせ（Practice.Tests 195 件）・S2 の突き合わせ（Render.Tests 218 件）がそのまま通ることと、新しい等価テスト（§12.2）で確かめた。

### 12.1 やったこと

| 項目 | 内容 |
| --- | --- |
| **隠れ判定のレイの使い回し・早期終了**（`Picking.Prepare` / `Nearest`、`IsVisible`） | 隠れ判定は「いちばん近い当たりが p より手前か」だけが要るのに、従来は全オブジェクトの全当たりを一覧にして並べ替え、しかもレイごとに全オブジェクトの逆行列を作り直していた。オブジェクトごとの行列・逆行列・外接球を 1 回だけ作り（`Prepared`）、当たりは持たずにいちばん近いものだけを持ち（同じ距離は先のオブジェクト・先の三角形。従来の安定な並べ替えと同じ）、面を指定しないときは「p より手前の当たり」が 1 つ見つかった時点で止める |
| **外接箱（画面に投影した格子）での絞り込み**（`Picking.PickIndex`、`EditBox`） | ボックス選択は候補ごとに 1 本のレイ（頂点 482 個 × 三角形 960 個）。三角形を画面（NDC）に投影した外接箱を 32×32 の格子に入れ、レイの点の格子の中の三角形だけを `RayTriangle` にかける。捨てるのは「そのレイが通りえない」三角形だけ（投影の丸めより十分大きい余裕 1e-6 を付けて広めに残す。カメラの面をまたぐ・画面の外の三角形は、いつも候補に残す）。**残った三角形は従来と同じ判定**。頂点・辺のクリック（`PickVert` / `PickEdge`）は、格子を作る費用が回収できないので、`Prepared` の使い回しと早期終了だけ |
| **`Restore` の作り直し** | 全オブジェクトを作り直すのをやめることは**していない**。`EmVert.Id`（辺のキーの順）・選択の順番・取り消しの記録が、作り直しと同じ値になることを S1 の突き合わせが守っているため、作り直さない（形が同じなら使い回す）には、メッシュの変更を全部検出する仕組みが要り、危険が大きい。代わりに、作り直しの中身の無駄を取った: `EditMesh.ToData` の `Faces.IndexOf`（選んだ面の数の 2 乗）を辞書に、`Flush` の `Verts.Contains` / `Faces.Contains`（選んだ数 × 全体）を `HashSet` に、`SceneObject.Rebuild` を LINQ なしの 1 回の走査（面の法線は面ごとに 1 回）に |
| **ドラッグ中の輪郭（EdgesGeometry）の再生成回避**（`SceneRenderer`） | 輪郭を描くのは「選択中で、編集中でない」オブジェクトだけなのに、形が変わるたびに作っていた（球で 0.74 ms/回）。描くときにだけ作る（編集モードで頂点を動かし続けている間は作らない）。編集モードの点・線・面の作り直しも、辺ごとの配列の確保をやめた |

### 12.2 等価の確かめ

- S0・S1 の突き合わせ（本物の `viewport.js` の結果と、約 24,000 手順を 1 手順ごとに比較。ボックス選択・クリック・隠れ判定の境目のシナリオを含む）と、Render.Tests 218 件が、変更前と同じ入力のまま通る。
- 新しい `PickingEquivalenceTests`: 従来の `Raycast` の先頭と、`Nearest`（格子なし・あり）が、**同じオブジェクト・同じ三角形・距離がビットまで同じ**であること。3 通りのシーン × 透視・平行投影（前・右・上のように軸にそろったものを含む）・地平線すれすれ・下から × カメラ距離 9・2.2・0.6（物体の中・三角形がカメラの面をまたぐ）、画面の内外のランダムな点と頂点の真上（丸めの境目）。当たり 4,000 件以上・外れ 500 件以上。早期終了の版が「先頭の距離との比較」と同じ答えであることも確かめる。
- 変異: 格子の余裕を狭める・同じ距離を `<=` にする・カメラの面をまたぐ三角形を候補から外す、の 3 件を入れると落ちる（同じ距離のとき先の三角形になるよう候補を並べ直す処理は、正確に同じ距離になる入力が無く、検出できていない）。

### 12.3 再計測（前後。同じ条件）

球 482 頂点・960 三角形。1 回あたりの ms。WebAssembly は AOT なし・SwiftShader の Chromium（§11 と同じ）。ネイティブはこの PC の `dotnet test`（別の処理が走っていると遅くなる。ボックス選択の変更前は 17〜38 でばらついた）。

| 操作 | WASM 前 | WASM 後 | ネイティブ 前 | ネイティブ 後 | 予算 5 ms（WASM 後） |
| --- | --- | --- | --- | --- | --- |
| オブジェクトのクリック | 0.54 | 0.54 | 0.087 | 0.086 | 内 |
| 頂点のクリック（編集） | 4.39 | **3.66** | 0.83 | 0.63 | 内 |
| **全体のボックス選択** | **210** | **23.7**（約 9 分の 1） | 17〜38 | **4.2** | 超える（ただし 1 回の操作） |
| 全頂点を G で動かす（核のみ） | 1.36 | 0.72 | 0.23 | 0.11 | 内 |
| **ドラッグ中の 1 フレーム**（`drag-edit-frame`。移動 + `Build` + 詰める。GL 側を含まない下限） | 7.33 | **3.35** | 1.22 | 0.59 | 内 |
| **全面の押し出しの開始** | **45.4** | **26.4** | 9.99 | 5.26 | 超える（1 回の操作） |
| **取り消し 1 回** | **12.3** | **5.7** | 2.58 | 1.19 | ぎりぎり |

**GL の命令の発行まで含めたページ内のドラッグ**（新設の `gl-measure` の `dragFramePage`: 球の全頂点を編集モードで G ドラッグ。240 回のマウス移動。1 回の移動 = ポインターのハンドラー（操作・形の作り直し）+ そのあとのフレーム（`Build`・送信・`draw` の呼び出し）。GPU の実行時間は含まない）: 1 移動あたり **12.06 ms → 7.93 ms**（ハンドラー 11.26 → 7.18、フレーム 0.34 → 0.35）。ハンドラーの時間が `drag-edit-frame` より大きいのは、ページでは移動のたびに JS から C# の呼び出しが入るため [推測]。

### 12.4 AOT の判断の材料（更新）

- 軽量化で、重い 1 回の操作は **4〜9 倍速く**なった（ボックス選択 210 → 24 ms、取り消し 12 → 5.7 ms、押し出しの開始 45 → 26 ms）。止まった画面の描画は変わらず 0.24 ms。
- なお予算 5 ms を超えるのは、**押し出しの開始 26 ms（`EditMesh.Extrude` と、押し出し後の `Flush`・`RemoveUnused` の LINQ が中心と見ている [推測。プロファイルは取っていない]。未着手）**、ボックス選択 24 ms、密なメッシュのドラッグ（7.9 ms/移動）。いずれも 1 回の操作の「ちょっとした待ち」か、16 ms に収まる範囲で、**AOT なしでも S3 の画面は使える水準**。
- 次の手（AOT の前）: `Extrude` / `Inset` / `Bevel` の LINQ・`List.Contains` の掃除、`EditMesh.Edges()` のキャッシュの無効化の見直し（`Changed()` が辺の一覧を毎回作り直す）、ドラッグ中の点・線・面を「動いた頂点だけ」書き換える（形が同じで位置だけ変わるなら、バッファの位置だけを更新する）。それでも足りなければ AOT（配信が数 MB 級に増える・CI に workload が要る）を、測った数値を添えてユーザーに相談する。

## 13. S3（`/practice` を C# で。`?engine=cs`）の記録

印: **[実測]** はこの作業で測った値。既定は今までの Three.js 版のまま（フラグなしの `/practice`・`/practice/unity`・`/practice/room`・記事内チュートリアルは変わらない）。

### 13.1 作ったもの

- **切り替え**: `/practice?engine=cs` で C# 版。`?debug=1` を足すと、確認用の状態（`window.__practice`）を JS 版と同じ形で出す。タッチだけの端末は、C# 版を使わず今までの案内の画面にする（`practice-entry.js` の `isTouchOnly`）。戻し方は、URL から `?engine=cs` を外すだけ（既定が JS なので、本番でも何も変わらない）。
- **遅れて読み込む**: `Practice.dll`・`Render.dll` は `BlazorWebAssemblyLazyLoad`（`LazyAssemblyLoader`）で、`?engine=cs` のときだけ読み込む。[実測] 通常のページ（`/`・`/practice`）では、`Practice`・`Render`・`gl.js`・`practice-cs.js` のどれも要求されない。`?engine=cs` では、`Practice` 214 KB（brotli 73.8 KB）・`Render` 49 KB（18.7 KB）・`gl.js` 20 KB（6.9 KB）・`practice-cs.js` 1.5 KB（0.7 KB）が増え、three.js（1,315 KB。207 KB）・`viewport.js`・`editmesh.js`・`practice.js` が要らなくなる。ページ全体の展開後の転送量は、JS 版 12.95 MB → C# 版 11.80 MB。
- **Razor（`Pages/Practice.razor`）**: HUD（モード・視点・統計・頂点/辺/面/透過/統計/ミラーのボタン・N パネル・メニュー・ボックス選択の枠・ステータスバー）とお題の一覧を Razor で描く。クラス名・構造は `viewport.js` の `HUD_HTML` と同じ（既存の CSS をそのまま使う）。状態は `PracticeHud`（単純な型。`Services/PracticeHud.cs`）で受け取り、`Practice` の型は画面から直接使わない（遅れて読むため。読み込み前に画面の描画の関数が `Practice` の型を要求しないように、窓口 `IPracticeCs` と入口 `PracticeCsLoader` を挟んだ）。canvas は、Blazor が描く HUD の兄弟に外から足さず、中身が空の `#practice-gl` に `gl.js` が入れる（`gl.js` の `start` の `canvasHostId`）。
- **エンジン（`Services/PracticeCs.cs` の `PracticeCsEngine`）**: `ViewportCore` → `SceneRenderer`（操作の前に作る契約）→ `PracticeSession`。`gl.js` が数値にしたポインター・ホイール・キーを `ViewportCore` に渡し、入力のたびに「画面に出る状態の署名」が変わったときだけ `Changed` を出して Razor を描き直す（マウスの移動の大半は何も変わらない）。`Lab.ParseQuery`（`?scene=` など）・`__labControl`・ベンチの口は持ち込んでいない。
- `gl.js` は `Web/wwwroot/js/` に移し、レンダーラボ（`RenderLab`）はそのファイルをリンクして使う（コピーは持たない）。`gl.js` は、サイドバーの入力欄・ボタン・メニューの上の操作を、ビューの操作にしない（viewport.js と同じ）。
- `tools/verify/practice.mjs` に `ENGINE=cs`（`/practice` を C# 版で動かす）、`tools/verify/practice-compare.mjs`（同じ操作で、Three.js 版と C# 版のビューポートの画素を比べる）を追加した。`Web.Tests` は、`Practice` が名前空間と衝突する（`FaraAvatarModdingGuide.Practice`）ので `Web.Pages.Practice` と書くように直した。

### 13.2 受け入れ（設計 §4.2 の S3）の結果

| 基準 | 結果 [実測] |
| --- | --- |
| `practice.mjs` を両エンジンで通す（基準 `practice-baseline.json` と一致） | JS 版（既定）: 全部 ok（58 件）。**C# 版（`ENGINE=cs`）の `/practice` の操作**: 位置・回転・スケール・視点・お題の進み（6 件達成・次は「Shift+D で複製」）が基準と**一致**（キー・マウス・Shift+A のメニューからの追加・Ctrl+Z・Numpad・テンキー模倣・`?debug=1` の選択）。CSP 違反 0・コンソールのエラー/警告 0 |
| 往復 20 回でリスナー・コンテキストが増えない | C# 版で `/practice?engine=cs` ⇄ `/learn` ⇄ unity ⇄ room ⇄ 記事内チュートリアルを 20 往復: WebGL の context・ResizeObserver・IntersectionObserver・setInterval・WebSocket・リスナー・canvas・DOM の節・Documents・rAF のどれも増えない。離れたあと動いている context は 0。CSP 違反 0・エラー 0 |
| 画像比較（Three.js 版と C# 版、同じ操作） | 9 シーン（既定・立方体の選択・G Z 2 の移動・Shift+A のメニュー・編集モードの面選択・透過 + 統計・N パネル・平行投影の前面・ボックス選択のドラッグ中）で、ビューポート全体（3D + HUD の文字・ボタン）の画素の差（チャンネル差 8 超）0.000〜0.001 %、最大 16。合否の閾値は 0.1 % / 60 |
| Phase 3 と同じ操作（キー・マウス）ができる | 上のとおり。`/practice/unity`・記事内チュートリアル・共有ルーム・スマホ幅の案内は、C# 版でない（既定のまま）ことも `practice.mjs`（`ENGINE=cs`）の他の項目で確認 |
| 既定は JS のまま | `/practice`（フラグなし）は Three.js 版。`csp-check.mjs`（46 画面）: CSP 違反 0・エラー/警告 0 |

[実測] 起動（localhost・キャッシュなし・SwiftShader。サイトの起動を含む）: `/practice` のキャンバスが出るまで、JS 版 2.2〜2.8 秒・C# 版 2.4〜2.8 秒（4 回ずつ。差は誤差の範囲）。C# 版の 1 フレーム（`Build` + 送信 + `draw` の呼び出し。止まった画面）は 0.54〜0.62 ms/フレーム（レンダーラボの 0.24 ms より大きい。ページには Blazor・MudBlazor も動いているため [推測]）。

### 13.3 確証が持てない点・S4 以降への申し送り

- **レイアウトの小さな差**: メニューの位置の補正は、メニューの大きさを項目の数からの見積もりで決める（viewport.js は実測）。画像比較の 9 シーンでは差が出ていないが、細かい位置は未確認。（**S4 で対応**: 描いたあとに JS が実測して位置を直す。`practice-details.mjs` で値を比べて一致。§14）
- **HUD の更新のタイミング**: Razor の描き直しは、入力のあとの次の描画の機会になる（JS 版は DOM を同期で書き換える）。`practice.mjs` では差が出ていない。
- **確認用の `window.__practice`**: C# 版は `objects`・`mode`・`ortho`・`view.name`・`select(o)` だけ（`practice.mjs` が読む分）。viewport.js のインスタンスの全部ではない。
- **S4 以降**: 記事内チュートリアル（`tutorial.js`・`scenarios.js`）はまだ Three.js。three.js を外せるのは S7（S3〜S6 が本番で一定期間動いてから）。`Practice`・`Render` の遅れた読み込みは、S4 以降で記事内チュートリアルにも使うときに、「記事を開いたとき」に読む形へ広げる。
- **Lab.ParseQuery は持ち込まない**（§11.7 の申し送りのとおり）。`gl.js` の `pointercancel` / `lostpointercapture` の対応は、S2 のレビューで入れたものがそのまま効く。

### 13.4 レビュー指摘への対応（head 464fe51 のあと）

- **低速読み込み中の「離れて戻る」**: `StartCsAsync` の各 `await` のあとで破棄済みを確かめ、破棄済みなら作った C# 版を `Dispose` して抜ける。`Practice`・`Render` の遅れた読み込みは `static` の 1 つの `Task` に共有する（同じ wasm を二重に読むと、本文の読み込みが衝突した）。失敗したときは共有を捨てて、次に開いたときにやり直せる。`tools/verify/practice-race.mjs`（`Practice`・`Render` の応答を 3.5 秒遅らせ → `/practice?engine=cs` → 1.2 秒後に `/learn` → 0.5 秒後に戻る）で、wasm が 1 回ずつだけ読まれ・コンソールのエラー/警告 0・canvas 1 つ・HUD が出ることを、3 回続けて確かめた。
- **`practice.mjs` の検査を追加**（両エンジンで同じ期待）: `.vp-mode`・`.vp-view`・`.vp-status`・`.vp-numeric` の存在と最初の文言、G → Z → 2 のあとの `.vp-status`（「移動…」）・`.vp-numeric`（「入力: 2」）、Tab で「編集モード」、**Tab と Ctrl+Z の `defaultPrevented`**、ビューをクリックしたあと・キー操作のあと・「統計」ボタンのあと・「最初からやり直す」のあとの**フォーカスがビューにあること**。`gl.js` のキーの `preventDefault` を外す変異を入れると、C# 版の Tab・Ctrl+Z・フォーカスの 3 件が落ちる。
- **`practice-compare.mjs` の閾値**: 正常時の最大差は 49〜55（HUD の文字の端・線の端の画素）で、60 は近すぎた。**主の基準は画素の差の割合 0.1 %**（正常時は 0.001 % 以下）とし、最大差は 90 に余裕を持たせた（最大差だけでは、数画素の端の差で落ちやすく、壊れた描画の検出は割合のほうが強い）。
- **`ViewportCore` のイベントの一覧**: 入力のたびに `TakeEvents()` で捨てる（C# 版は `EventEmitted` だけを使うので、一覧が増え続けない）。
- **`?engine=` だけが変わるページ内の移動**（アドレスの書き換え・戻る）: `NavigationManager.LocationChanged` で、`/practice` のまま `engine` の指定が変わったら、ページを読み込み直す（エンジンの切り替えは始め直しが要るため）。
- **`gl.js` の確認用のグローバル**（`window.__glDiag`・`__glLose`）: サイトの `/practice` では公開しない。`exposeDebugGlobals()` を、レンダーラボ（`Lab.Run`）だけが呼ぶ。`report`・`exposeControl` は `gl.js` に残る（呼ばない限り何も公開しない）。
- **残課題**: `gl.js` にレンダーラボ専用の関数（`report`・`exposeControl`・`getQuery`・`loseContext` など）が残っている。S4 以降で、サイトに出す `gl.js` からラボ専用の部分を分けたほうがよい。

## 14. S4（記事内チュートリアルを C# で。`?engine=cs`）の記録

印: **[実測]** はこの作業で測った値。既定は今までの Three.js 版のまま（フラグなしの記事は変わらない）。

### 14.1 作ったもの

- **`Practice`（純粋）**: `Tutorial.cs`（手順・シナリオのモデル）、`TutorialScenarios.cs`（Blender の 9 本。`scenarios.js` の述語を移した）、`TutorialSession.cs`（手順の進行: 達成・失敗・ヒント・再生 = `ResetScene` + 手順ごとの `Skip`・グループの先頭に戻す）、`ViewportCore.Tutorial.cs`（チュートリアル用の補助: `selectLoopWhere`・`editSelectWhere`・`findEdge`・`previewLoopCut`・`loopCutWhere`・`selectByName`・`setHighlight`・案内の矢印と点線・`Silent`）。`viewport.js` と同じに、枠のある要素の大きさを 2 通りに分けて持つ（`clientWidth` の整数 = 縦横比・マウスの範囲判定、`getBoundingClientRect` の小数 = 座標の変換。§14.3 ①）。
- **`Render`**: `guideLine`（案内の軸の点線。点滅する半透明。`Shading.Dashed`。S2 で残していたもの）。`SceneRenderer.Build(timeMs)`。透明で両面の面は**裏 → 表の 2 回**に分けて描く（`DrawFlags.TwoPass`。three.js と同じ。§14.3 ⑧）。頂点は three.js と同じ「射影 × (ModelView × 位置)」で変換する（ModelView = ビュー × ワールドを double で掛ける。`FramePacker` の並びは、1 描画あたり 28 → 44 float。§14.3 ⑦）。
- **Razor**: `Components/TutorialEmbed.razor`（記事内の埋め込み 1 つ: 題・進み・吹き出し・ビュー・キー表示・ボタン・画像で見る・完了）、`Components/PracticeHudView.razor`（S3 の HUD を `/practice` と共有）。`Blazor.rootComponents.add` で、記事の Markdown から出た `.practice-embed[data-scenario]` に置く（`RegisterForJavaScript<TutorialEmbed>("tutorial-embed")`）。記事を離れる・替わるときは `dispose()`。
- **`Services/TutorialController.cs`**（`tutorial.js` の `Tutorial` クラスに当たる）、`CsViewEngine.cs`（`/practice` のエンジンと共通の基底）。WebGL を使うのはページ内で 1 つだけ（別の埋め込みが始まったら手放す）。ホイールはフォーカス中だけ効く。
- **遅れて読み込む**: `Practice`・`Render` は S3 と同じ `BlazorWebAssemblyLazyLoad`。記事を `?engine=cs` で開いたときだけ読む（`PracticeCsAssemblies`。同じ wasm を二重に読まない）。`tutorial.js` は C# 版にあるシナリオを飛ばす（`:not([data-cs])`）。ほか（Unity の 2 本）は今までの `tutorial.js`。
- **確認の道具**: `tools/verify/tutorial-compare.mjs`（9 本を「次へ（スキップ）」で 1 手順ずつ進めながら、毎回、Three.js 版と C# 版の埋め込み全体の画素と文字を比べる）、`tutorial-dom.mjs`（DOM の骨格の比較）、`practice-details.mjs`（`/practice` の細部: カーソル・フォーカス・枠・ボックス選択の枠・メニューの位置・HUD の文字・ツールの点灯を、25 の場面で値で比べる）。`practice.mjs` の `ENGINE=cs` は、記事内チュートリアルと往復も C# 版で動かす。
- **突き合わせ**: `tests/js/tutorial-reference.mjs`（本物の `scenarios.js` を Node で動かす）と `TutorialParityTests`（手順の定義・再生後の状態・案内・イベントの一覧に対する達成/失敗/ヒントの答え）、`ViewGeometryTests`、`practice-cs.js` の `CS_SCENARIOS` と移したシナリオの一致。

### 14.2 受け入れ（§4.2 の S4）の結果

| 基準 | 結果 [実測] |
| --- | --- |
| 同じイベント列で、達成する手順が一致 | `TutorialParityTests`（`Practice.Tests` は全体で 239 件）が全部通る。本物の `scenarios.js` との違いを、変異（距離の閾値・手順の判定・ヒントの文言）で入れると落ちることを確かめた。突き合わせで、`scenarios.js` の「始まりました」のメッセージが `undefined` になる穴（G/R/S 以外の操作）を見つけ、両方「別の操作」に直した |
| 記事を画面で確認 | 9 本すべて（記事 7 本）を、`tutorial-compare.mjs` で全手順（129 手順 + 最初の画面）、Three.js 版と並べて比べた。§14.3 |
| タッチ端末・「画像で見る」 | `practice.mjs`（`ENGINE=cs`）で、「画像で見る」の画像がすべて読める・3D に戻すと context は 1 つ・別の記事へ移ると前の分は解放される。タッチだけの端末は、今までの案内（`isTouchOnly`） |
| 往復 | `ENGINE=cs` の往復 20 回（記事は `?engine=cs`）で、context・ResizeObserver・IntersectionObserver・setInterval・WebSocket・canvas・DOM の節・Documents・rAF が増えない。リスナーは、最初の 1 往復で 1 個増え（遅れて読む入口）、そのあとは増えない |
| CSP | `csp-check.mjs`（46 画面）違反 0・エラー/警告 0 |

### 14.3 見た目・操作の細かい差の洗い出しと、直した原因

S3 の `?engine=cs` と Three.js 版の差を、(a) 画素（`practice-compare.mjs`・`tutorial-compare.mjs`・`gl-compare.mjs`）、(b) 文字・DOM（`tutorial-dom.mjs`）、(c) 細部の値（`practice-details.mjs`）で洗い出し、**閾値で隠さずに原因を直した**。S4 の初版の `tutorial-compare` では、`transform` の 17 手順すべてが不合格だった（差が 8 を超える画素 0.29〜1.36 %・最大 50〜190。方眼・軸・立方体の縁の 1 画素のずれ、マーカーの 2 px のずれ）。

| # | 差（見え方） | 原因 | 直し方 |
| --- | --- | --- | --- |
| ① | 方眼・軸・立方体の縁が 1 画素ずれる（0.3〜1.4 %） | 記事のビューには 1 px の枠があり、`viewport.js` は縦横比・`worldPerPixel`・`mousePx` に **`clientWidth/Height`（枠を除いた整数）**、座標の変換（`ndc`・`toScreen`）に **`getBoundingClientRect`（枠を含む小数）** を使う。C# は 1 つの矩形（丸め）で両方を計算していた | `ViewportCore.Resize(rect, clientW, clientH)`。`gl.js` は `frame(rectW, rectH, clientW * 65536 + clientH)`（コールバックの引数は 3 個まで）。`ViewGeometryTests` |
| ② | マーカー（「クリック」）の位置が 2 px ずれる。0.01 px の違いで枠の画素が変わる | ①の矩形の違い + Razor の `Px()` が小数 2 桁に丸めていた | ①と、`Px()` を JS の `${v}px` と同じ最短の桁に |
| ③ | 進みバーの幅の文字（`43.4783%` と `43.48%`） | 同上の丸め | 同じ桁に |
| ④ | メニューを（再生で）開く位置が違う | `ViewportCore` を作るときの仮の大きさ（960 x 540）の中央を、最後のマウス位置に使っていた。`viewport.js` はクライアント座標で覚えるので、ページをスクロールしたあとはビューの外になり、`clientWidth / 2` に戻る（チュートリアルは、覆いのあいだマウスが届かないので、これが普通の経路） | マウスが一度も入っていなければ `(ClientWidth / 2, ClientHeight / 2)`。`ViewGeometryTests` |
| ⑤ | 「または右クリック」の吹き出しが 1/64 px ずれ、アイコンの 2 画素が違う | Razor が文字を 2 つの文字ノード（`" "` と値）に分けて出し、レイアウトの丸めが変わる | 1 つの文字列にする |
| ⑥ | 同じ記事に 2 本あるとき、見えているほうに 3D が出ない（C# 版だけ） | 埋め込みごとの観察（IntersectionObserver）の通知は、「入った」が「出た」より先に届くことがあり、入った側は「ほかのビューが見えている」と思って待つだけで、あとで始め直さなかった | 持っていたものが画面から出たとき、見えて待っているものが始める。`practice.mjs` の記事の往復・`tutorial-compare` で確認 |
| ⑦ | 押し出し 0 のときなど、同じ平面に重なる面・線のちらつき方が違う | 頂点の変換の式の順が違い、深度の丸めが three.js とずれた（three.js は `projectionMatrix * (modelViewMatrix * 位置)`。ModelView は double で掛けてから float） | `FramePacker` が `ModelView = View × World` を double で作り、`gl.js` は同じ式 |
| ⑧ | 透過表示の球の面の明るさ（最大 9。§11.7 ③「原因は推定」だったもの） | three.js は、透明で両面の材質を**裏 → 表**の 2 回に分けて描く。C# は 1 回で、三角形の並び順に重なった | `DrawFlags.TwoPass`（`gl.js` は `cullFace(FRONT)` → `cullFace(BACK)`）。`RenderParityTests` に、three.js の材質（transparent && 両面）との一致を追加（外すと 36 件が落ちる） |
| ⑨ | DOM の違い（`data-act`・`data-engine`・画像表示の前へ/次へ・件数の初期値） | Razor の作りの違い | 同じにした（`tutorial-dom.mjs` の 3 本 × 5 状態が一致） |
| ⑩ | （描画の差ではなかった）画像比較が時々 0.27 % 落ちる | ページの背景の 3D（`#site-scene`。動く）が、枠の半透明の下に透けていた。マーカーの CSS アニメーションは、始まった時刻で位相が決まる | 比較スクリプトで背景を隠し、アニメーションを止める（`prefers-reduced-motion`。CSS は両方で同じ） |

前後の数値（差が 8 を超える画素の割合 / 最大差）:

| 比較 | 直す前 | 直したあと |
| --- | --- | --- |
| 記事内チュートリアル 9 本・129 手順 + 最初の画面（`tutorial-compare.mjs`） | `transform` は全手順が不合格（0.29〜1.36 % / 50〜190）。そのほかも手順ごとに不合格 | **全手順 0.000 % / 最大 1**（閾値は 0 % / 8 に厳しくした） |
| `/practice` 9 場面（`practice-compare.mjs`） | 0.000 % / 最大 3〜55（背景の動きで時々 0.27 %） | 0.000 % / 最大 3（閾値は 0.1 % / 90 から 0.001 % / 8 へ） |
| レンダーラボ 12 場面（`gl-compare.mjs`。S2） | 0.000〜0.081 % / 最大 25 | **0.000 % / 最大 1**（閾値は 0.15 % / 40 から 0.001 % / 8 へ） |
| DOM（記事の 3 本 × 5 状態） | `data-act` などの違い | 一致 |
| `/practice` の細部 25 場面（カーソル・フォーカス・枠・メニュー位置・HUD の文字…。`practice-details.mjs`） | — | 一致 |

レビュー(PR #115)のあとに足したもの:

- **実入力の突き合わせ**（`tutorial-input-compare.mjs`）: `transform` を、Three.js 版と C# 版で、同じ本物のマウス・キー（立方体のクリック → G → マウス移動 → Esc → G Z 2 Enter → R Z 45 Enter → S 0.5 Enter → N → Ctrl+Z）で操作し、毎回、画素と文字を比べる（23 スナップショット。`tutorial-compare.mjs` は「次へ（スキップ）」で進めるので、本物のイベントの経路を通らない）。これで **C# 版だけの不具合**が見つかった: ポインター・キーの JS のコールバックの中から始めた `Task.Delay` が、待ち時間が過ぎても再開せず、「できました！」が消えない・失敗のやり直し（400 ms 後）が起きなかった（`Task.Delay(300).ContinueWith` も `Task.Run` 経由も再開しなかった。原因は特定していない）。JS の `setTimeout` を使う `PracticeCsJsTutorial.DelayAsync` に替えた。
- フォーカスが外れたときの覆い: ビューの中のボタンを押すと focusout のすぐあとに focusin が来るので、`tutorial.js` と同じに 1 フレーム待ってから外す（覆いが一瞬出なくなった）。
- `?engine=` の判定は、`Practice.razor` と `ArticlePage.razor` で同じ完全一致（`NavigationQuery.IsCsEngine`）。読み込み中に記事を離れたときの `TutorialEmbed` の登録・module の解放漏れを直した。
- `tutorial-compare.mjs` は、kit の `/api/maintenance` の requestfailed（2 秒で打ち切る既知の仕様）を問題に数えない（`csp-check.mjs` と同じ）。

直せていない・意図して違える点:

- **マウスの最後の位置（メニュー）**: `viewport.js` は、最後のマウス位置をクライアント座標で覚えるので、マウスが一度もビューに入っていないとき、メニューの位置がページのスクロール位置で変わる（1 px 程度のずれ、ビューの外なら中央）。C# は常に中央にする（意図した差。再現できる動きにするため）。マウスが入ったあとは同じ。
- **Unity の 2 本**は C# にしていない（S5。`unity.js` の画面に載っている）。`window.__practice`（確認用）は、`practice.mjs` が読む分だけ（S3 のまま）。

### 14.4 計測 [実測]

SwiftShader の Chromium・AOT なし・localhost（wrangler dev）。

- **配信物の大きさ（IL の dll。brotli 圧縮）**: `Web` 258,048 → 305,152 バイト（brotli 80.0 → 94.4 KB、**+14.4 KB。全ページが読む**。`TutorialEmbed`・`PracticeHudView` を主のアセンブリに置いたため。ルートコンポーネントは、遅れて読むアセンブリには置けない）、`Practice` 219,136 → 294,912（brotli 75.8 → 96.2 KB、+20.5 KB。遅れて読む）、`Render` 50,176 → 51,200（+0.4 KB。遅れて読む）。JS: `gl.js` 20.1 → 21.5 KB、`practice-cs.js` 1.5 → 9.6 KB（`?engine=cs` のときだけ）。
- **記事（`/learn/basics/transform`）を開いて、3D の canvas が出るまで**（3 回）: Three.js 版 2.00 / 2.56 / 2.56 秒、C# 版 2.73 / 2.76 / 2.64 秒。ページの転送量（ブラウザが測った encodedBodySize の合計）は、Three.js 版 4,889 KB、C# 版 4,745 KB（three.js の 260 KB を読まず、`Practice` 116 KB・`Render` 22 KB・`gl.js` 8 KB・`practice-cs.js` 4 KB を読む）。通常の記事（フラグなし）が `Practice`・`Render`・`gl.js`・`practice-cs.js` を読まないことは、S3 のとおり。
- **「次へ（スキップ）」1 回（再生）**（`bevel-merge` の 22 回。クリック → 表示の更新まで。3 回）: Three.js 版 中央値 0.7〜0.9 ms・最大 24〜28 ms・合計 78〜81 ms、C# 版 中央値 7.2〜7.5 ms・最大 46〜64 ms・合計 274〜279 ms（Razor の描き直しを含む）。
- テスト件数: `Practice.Tests` 239・`Render.Tests` 220・`Build.Tests` 47・`Web.Tests` 196・node 120。

### 14.5 確証が持てない点・S5 以降への申し送り

- 実機の GPU・Firefox・Safari は未確認（SwiftShader の Chromium のみ）。**dev サイトでの確認**（実ブラウザ。特にタッチ端末の「画像で見る」と、Windows・Mac の Chrome での操作）が要る。
- Razor の描き直しは、イベントのあとの次の描画の機会（JS 版は DOM を同期で書き換える）。画像比較・`practice-details` では差が出ていない。
- 同じ記事に 2 本あるとき、「別のビューが見えているあいだは奪わない」の挙動は JS 版と同じ（クリックで切り替え）。通知の順の入れ替わり（§14.3 ⑥）への対応は、ブラウザの通知の順に依存するので、実機で見たほうがよい。
- ~~`gl.js` のレンダーラボ専用の関数を分ける課題（§13.4）は残っている。~~ **S5 で分けた**（§15.1。`RenderLab/wwwroot/js/gl-lab.js`）。three.js を外せるのは S7（S3〜S6 が本番で一定期間動いてから）。Web の主のアセンブリが 14 KB（brotli）増えた件は、S7 で `tutorial.js` などを消すときに、全体の収支で見る。

## 15. S5（Unity 風の画面と記事内の Unity チュートリアルを C# で。`?engine=cs`）の記録

印: **[実測]** はこの作業で測った値。既定は今までの Three.js 版のまま（フラグなしの `/practice/unity`・記事は変わらない）。

### 15.1 作ったもの

- **`Practice`（純粋）**: `UnityEditorCore`（`unity.js` の `UnityEditor` のロジック全部。選択・開閉・追加・付け替え・削除・有効/無効・Tag・マテリアル・コンポーネント・Setup Outfit・ツール・Transform の入力・Frame Selected・視点（回す・平行移動・ズーム・見回し）・SDK の集計とビルド・腕を振るポーズ・ポップアップ・ドロップ・キー・取り消し。`.Scene.cs` に Scene ビューのポインター・ギズモ・視点、`.Model.cs` に Razor 用の表示モデル）、`UnityObj3D`（three.js の `Object3D` の意味どおりの階層・行列・attach・Euler YXZ・クォータニオン。ワールド行列は `updateMatrixWorld` が呼ばれたときだけ更新される「古い行列」の挙動まで同じ）、`UnityGeometry`（three r170 の Sphere・Capsule・Box・Cylinder・Cone・Torus と translate・rotateX）、`UnityData`（ASSETS・COMPONENTS・ボーン・ランク）、`UnityPractice`（お題 7 件。`unity-practice.js`）、`UnityTutorial`・`UnityTutorialScenarios`（`scenarios.js` の `unity-basics` 13 手順・`ma-outfit` 12 手順。述語・ヒント・失敗・スキップ）。
- **`Render`**: `UnitySceneRenderer`（部品＝不透明を作った順 → 方眼（半透明 0.55）→ ギズモ（別シーンの render。半透明 0.95・奥から））。`FrameLighting.Unity`・`FrameBackground.Sky`（`canvas` のグラデーションのテクスチャ。全画面の三角形）・`DrawFlags.CullBack`。three.js の `MeshStandardMaterial` の見た目に合わせて、**頂点シェーダーが視空間の法線（`uNormalMatrix`）・視空間の光の向き**を使う（§15.3 ①）。`FramePacker` は 1 フレームあたり、ヘッダー 20 → 32 float、1 描画 44 → 53 float。
- **Razor**: `Components/UnityEditorView.razor`（`unity.js` が `root.innerHTML` に作っていた DOM・クラス名と同じ。Hierarchy・Scene・Inspector・Project・メニュー・SDK パネル・ポップアップ・案内の吹き出し）、`Pages/PracticeUnity.razor`（`?engine=cs` の経路と、お題の一覧・リセット）、`Components/TutorialEmbed.razor`（Unity のとき、`.tut-vp` に `UnityEditorView`）。画面は Practice の型を直接使わず、`Services/UnityHud.cs` の単純な型だけを読む（遅れて読み込むため）。
- **`Services`**: `UnityCsEngine`（エディター・描画・`gl.js` とのやり取り・HUD の作成）、`UnityTutorialController`（`tutorial.js` の `Tutorial`。`TutorialController` と同じ作りで、Scene ビューの WebGL は画面が DOM を描いたあと（`UnityEditorView` の最初の描画のあと）に始まるので、`OnUnityStarted` を待つ）。JS 側は `wwwroot/js/unity-ui.js`（Hierarchy・Project のドラッグとドロップ先の枠・ポップアップとアバターの吹き出しの位置・フォーカス）。`practice-cs.js` の `CS_SCENARIOS` に `unity-basics`・`ma-outfit` を足した（`data-engine="unity"`・画像の高さ 840）。
- **`gl.js`**: ①視空間のライティング（上記）・空の背景・`CullBack`・キーを受ける要素と無視する要素（`keyRoot`・`keyIgnore`・`data-gl-ignore`）。②**レンダーラボ専用の関数を分けた**（§13.4・§14.5 の申し送り）: `diag`・`report`・`exposeControl`・`getQuery`・`now`・`loseContext`・`exposeDebugGlobals` は `RenderLab/wwwroot/js/gl-lab.js` へ。サイトに出す `gl.js` にラボ専用のコードは無い。
- **確認の道具**: `tools/verify/unity-compare.mjs`（`/practice/unity` を 38 の本物のマウス・キーの操作で、Three.js 版と並べて、画素・DOM の骨格・お題・ステータス・状態（視点・カメラ・ノードの位置/回転/拡大率・選択・ツール）を比べる）、`unity-lib.mjs`（ドラッグ・ギズモの位置・状態の比べ。`unity-compare` と `tutorial-input-compare` が共有）、`tutorial-compare.mjs`・`tutorial-dom.mjs`・`tutorial-input-compare.mjs` を Unity の 2 本へ拡張、`practice.mjs` の `ENGINE=cs` が Unity の操作（Hoodie を Avatar へドラッグ）も C# 版で動かす、`csp-check.mjs` に `?engine=cs` の 3 画面を追加。
- **突き合わせ**: `tests/js/unity-reference.mjs`（本物の `unity.js`・`unity-practice.js`・`scenarios.js` を Node で動かす。描画（WebGL）だけ代用品。`setTimeout`・`performance.now` は仮想の時計）・`unity-cases.generate.mjs`（99 ケース・2,448 操作: 台本・ギズモの 3 種 × ハンドル × 変種・視点・Scene のクリックの格子・乱数）、`UnityParityTests`（101 件。操作のたびに状態・イベント・preventDefault・クリックの下にあるもの・ワールド行列・ジオメトリを 1e-9 で比べる。変異テストで、距離・順序・判定を変えると落ちることを確認）、`UnityTutorialParityTests`（9 件。手順の定義・再生後の状態・イベント一覧に対する達成/失敗/ヒントの答え・お題の文）、`UnitySceneRendererTests`（6 件）。

### 15.2 受け入れ（§4.2 の S5）の結果

| 基準 | 結果 [実測] |
| --- | --- |
| `practice.mjs` の `unity` の基準と一致 | `ENGINE=cs node practice.mjs` も `すべて ok`（階層・お題の進みが基準と一致。往復 20 回・CSP・コンソールも） |
| 階層・お題の状態をイベント列で JS と突き合わせ | `UnityParityTests` 101 件（上記）。`unity-compare.mjs` の 38 状態で、状態・お題も一致 |
| 見た目 | `unity-compare.mjs` 38 状態: **0.000 % / 最大 1**（閾値 0 % / 8）。`tutorial-compare.mjs` は Blender 9 本 + Unity 2 本の全手順（156 比較）で **0.000 % / 最大 1**。`tutorial-dom.mjs` は Unity 2 本 × 5 状態で一致。390 px 幅も一致（横スクロールなし） |
| 実入力 | `tutorial-input-compare.mjs`: `transform` 23・`unity-basics` 20（ヒントが出る誤操作・ホイール 2 回・Alt ドラッグ・中ボタン・矢印のドラッグ・Position の入力）・`ma-outfit` 23（ルートに落とす（ヒント）・Ctrl+Z・メニューを開いて別の行をクリック（失敗 → やり直し）・Setup Outfit・Tag・マテリアルのドラッグ・Add Component の検索・SDK・Build & Test）。画素 0.000 % / 最大 8 以下・文字・状態が一致（最後は「完了」まで進む） |
| そのほか | `practice-compare.mjs`（Blender 9 場面。`gl.js` を直したあと）0.000 % / 最大 3、`gl-compare.mjs`（ラボ 12 場面。`gl-lab.js` に分けたあと）0.000 % / 最大 1、`csp-check.mjs` 52 画面で違反 0 |

### 15.3 見た目・操作の細かい差の洗い出しと、直した原因

初版は `unity-compare` で、膝の縫い目に 2〜4 画素の差（最大 100 前後）があった。閾値で隠さず、原因を直した。

| # | 差（見え方） | 原因 | 直し方 |
| --- | --- | --- | --- |
| ① | 膝・肘などの縫い目に 2〜4 画素の線 | 旋回体（Lathe）の極に近い**細長い三角形**で、varying の補間が外挿になる。three.js は法線を**視空間**（`normalMatrix` を掛けて頂点シェーダーで正規化）で補間し、光も視空間で計算する。C# 版はワールド空間の法線・光で、補間の誤差が出る場所が違った | `FramePacker` に法線行列（3×3）と視空間の光の向きを足し、頂点シェーダーを three.js と同じ式に（`gl_Position = uProj * (uModelView * pos)`・`vViewPosition`）。Blender 側の描画（`practice-compare`・`gl-compare`）は変わらない（Lit のまま。ライティングの種類は `FrameLighting`） |
| ② | 空の背景のグラデーション | three.js は `canvas` のグラデーションを `SRGB8_ALPHA8`・`flipY`・ミップマップのテクスチャにして、全画面に描く | `gl.js` の `drawSky`（同じテクスチャ） |
| ③ | 透明で両面の面・方眼の重なり | 描く順・深度の書き込み・`TwoPass` | `UnitySceneRenderer` の順（不透明 → 方眼 → ギズモ。透明は奥から）・`CullBack`（FrontSide） |
| ④ | Inspector の名前の文字が、案内の枠が付くと JS 版だけ LCD のアンチエイリアス（0.27 %。最大 165） | C# 版は canvas を空の `div.practice-gl`（`position:absolute`）に入れる（Blazor の子の数え方が狂わないように）。JS 版は Scene ビューの直下。`position:absolute` の層が間に入ると、Chromium が兄弟の文字の描き方を変える | `.ue-scene-view > .practice-gl { display: contents }`（Unity の画面だけ） |
| ⑤ | 「できました！」の吹き出しが、片方にだけ写る（実入力の比較） | 吹き出しは 1.5 秒で消え、丸のアニメーションは約 1 秒。撮る時刻が境目だった（描画の差ではない） | 比較スクリプトが、両方で `.tut-msg.is-ok` が消え、アニメーションが終わってから撮る |
| ⑥ | DOM: 「画像で見る」に切り替えたあと `.tut-vp` の `tabindex`・`aria-label` が残る（JS 版）、「完了」のあと隠した吹き出しの文字が残る（JS 版） | JS 版は `dispose` で子だけ空にする・`applyHighlight` は表示するときだけ文字を書く | 同じ（始めたあとの空の `.tut-vp` に属性・最後の吹き出しの文字を保持） |
| ⑦ | 入力欄（Position）に打った文字・フォーカス | `unity.js` は欄からの入力のあと欄を作り直さない・フォーカスを `keepFocus` で戻す | 打った文字はそのまま見せ（値が別の理由で変わったら状態の値）、フォーカスは `FocusRoot` で戻す |
| ⑧ | 腕を振るポーズ・ビルドの表示の時刻 | 時計（`performance.now`・`setTimeout`）で決まる | 比較では時計を止める。C# のスケジュールは JS の `setTimeout`（`PracticeCsJsTutorial.SetTimer`）、チュートリアル内の待ちは `DelayAsync`（`Task.Delay` は JS コールバックの中で再開しない） |
| ⑨ | 同じページに Unity と Blender の埋め込みがあると、Blender を始めたあと Unity の DOM だけが残り、Unity が二度と始まらない（レビュー M2。`tools/verify/practice-mixed.mjs` で再現） | 「いま WebGL を使っているチュートリアル」の登録（`active`）が `TutorialController` と `UnityTutorialController` で別の static で、片方が始まっても、もう片方が手放されなかった（gl.js の `start()` は前の WebGL を壊す） | 共通の登録 `ActiveGlView`（`Claim` が、新しいものが始まる前に、いまのものを `Suspend` する）。`practice-mixed.mjs`（`ma-outfit` の記事に `transform` の埋め込みを足して、Unity ↔ Blender を 5 回切り替え: canvas は 1 つ・もう一方に 3D ビューが残らない・Unity の DOM は Unity が持つときだけ）と `ActiveGlViewTests`。直す前は、Blender を始めたところで落ちる |
| ⑩ | 実機の GPU・DPR が 1 でない画面で、画面全体の 3D（人形・方眼）が 1.1〜1.3 % ずれる（レビュー M1。`DPR=1.25 GPU=1`。SwiftShader でも DPR 1.25 で再現。Blender の `transform` も 0.12 %） | three.js は描画バッファを `floor(幅 × DPR)`、ビューポートを `round(幅 × DPR)` にする（小数の DPR で 1 画素大きく、はみ出した分は切れる）。`gl.js` はバッファの大きさをビューポートにしていた | `gl.js` のビューポートを `round(幅 × DPR)` に。DPR 1.5・2 は 0 % / 最大 1、1.25 は 0.003〜0.012 % / 最大 29 の残り（下の §15.5） |

### 15.4 計測 [実測]

SwiftShader の Chromium・AOT なし・localhost（wrangler dev）。前は `develop`（#115 のマージ）を `git worktree` で同じ条件で publish したもの。

- **配信物の大きさ（IL の dll。brotli）**: `Web` 95,178 → 119,987 バイト（brotli q11。独立レビューの再計測。**+24.8 KB。全ページが読む**。`UnityEditorView`・`UnityCsEngine`・`UnityTutorialController` が主のアセンブリにあるため）、`Practice` 294,677 → 412,949（93.8 → 130.9 KB、+37.1 KB。遅れて読む）、`Render` 50,965 → 59,157（18.8 → 21.5 KB、+2.7 KB。遅れて読む）。JS（brotli）: `gl.js` 21.5 → 25.3 KB（7.5 → 8.3 KB）、`unity-ui.js` 9.2 KB（2.7 KB。`?engine=cs` のときだけ）、`practice-cs.js` 9.6 → 10.0 KB、`practice.css` 32.0 → 34.0 KB。`unity.js` は変えていない（71 KB。S7 まで残る）。
- **`/practice/unity` を開いて canvas が出るまで**（3 回）: Three.js 版 1.54 / 2.60 / 2.73 秒、C# 版 2.61 / 2.68 / 2.80 秒。転送量は 4,197 KB と 4,126 KB（C# 版は three.js・`unity.js` を読まず、`Practice`・`Render`・`gl.js`・`unity-ui.js` を読む）。記事（`/learn/unity/modular-avatar-outfit`）は 2.86 / 2.77 / 2.85 秒（JS）と 2.63 / 2.70 / 2.77 秒（C#）。
- **「次へ（スキップ）」1 回（再生）**（`ma-outfit` の 11 回。クリック → 表示の更新まで）: Three.js 版 中央値 180 ms・最大 447 ms、C# 版 中央値 293 ms・最大 566 ms（Razor の描き直しを含む）。
- テスト件数: `Practice.Tests` 349・`Render.Tests` 226・`Build.Tests` 47・`Web.Tests` 196・`Content.Tests` 26・node 52（検索の索引のテストは `SEARCH_INDEX_FILE` が要る）。

### 15.5 確証が持てない点・S6 以降への申し送り

- 実機の GPU・Firefox・Safari は未確認（SwiftShader の Chromium のみ）。**dev サイトでの確認**（特に Windows・Mac の Chrome での Unity の操作の手触り: ドラッグ・Alt ドラッグ・ホイール・右クリックのメニュー・入力欄、タッチ端末の「画像で見る」）が要る。
- **主のアセンブリ（`Web`）が全ページで +24.8 KB（brotli q11。95,178 → 119,987 バイト）**。S4 の +14 KB と合わせて +39 KB。Razor の部品はルートコンポーネントとして主のアセンブリに置く必要があった（遅れて読むアセンブリには置けない）。S7 で `unity.js`・`tutorial.js` などを消すときに、全体の収支で見る（three.js の 260 KB を外せる）。`UnityEditorView` を別アセンブリに移せるかは未調査。
- **Razor の描き直しのタイミング**: イベントのあとの次の描画の機会（JS 版は DOM を同期で書き換える）。画像比較・実入力の比較では差が出ていない。
- Hierarchy の行の「ドラッグ中の影」は DOM を JS（`unity-ui.js`）が作る（Razor の外）。ドラッグの経路は `unity-compare`・`tutorial-input-compare`（Project → Avatar・Project → Element 0・Project → Scene）で確かめたが、Hierarchy の行どうしの付け替え（reparent）の実入力は、ロジックの突き合わせ（`UnityParityTests`）だけ。
- 三角関数の最後の桁（float の 1 ulp）の違いが、ジオメトリの位置に 2e-7 まで出る。画素には出ていない（`unity-compare` 0.000 %）。
- `tutorial.js` に、`?debug=1` のとき `window.__unity` を置く 1 行を足した（確認用。`tutorial-input-compare` が、ギズモの位置と状態の比べに使う）。
- **画像比較の 0.000 % は、SwiftShader（既定。DPR 1）が前提**。実 GPU（`GPU=1`。RTX 4090・D3D11）でも DPR 1・1.5・2 は一致する（0 % / 最大 1）。`DPR=1.25` では、`ma-outfit` の手順 11（SDK のメニュー）と `unity-basics` の手順 9（Inspector の区切りの線 1 本）に、0.003〜0.012 %（最大 29。1 本の線・12 画素四方）の差が残り、`ma-outfit` は実行によっては Scene の上端・下端の 1 行（0.1 %）が違う（GPU のときだけ。原因は未特定）。レビューで見つかった「案内枠つきの Unity の手順の Hierarchy の行の文字の差（0.15〜1.29 %）」は、この小数の DPR のずれ（⑩）が主で、**CSS で消す対象の文字の描き方の差は再現できなかった**（`.ue-hl` の枠のある行の文字は、SwiftShader・GPU・DPR 1〜2 のどれでも JS 版と同じ。動きを減らさない設定 `MOTION=1` では枠の脈動の位相が違うので比べない）。`tutorial-compare.mjs` は、描画が実 GPU だと分かると、この前提を表示する。`GPU_TOLERANT=1` は、実 GPU 用に、案内の枠（`.ue-hl`）の中の画素の差を無視し、閾値を 0.2 % / 40 に緩める（DOM の文字は別に比べる。通常の SwiftShader の厳しさ 0 % / 8 は変えない）。
- **Unity の Hierarchy の行どうしの付け替えの実入力**は、`unity-compare.mjs` に「行 → Scene」のドラッグを足した（何も起きないことの比べ）。両エンジンの描画の完了（ドラッグの影・ドロップ先の枠が消え、2 フレーム進む）を待ってから撮る（以前は、4 回に 2 回、最大 14 の揺れがあった）。4 回連続で一致。
- 次は S6（共有ルーム）。S7 で three.js・`unity.js`・`tutorial.js`・`scenarios.js` などを消す。

### 15.6 レビュー指摘への対応（head 5c01b89 のあと）

- **M2**: ⑨（`ActiveGlView`・`practice-mixed.mjs`）。**M1**: ⑩と §15.5（SwiftShader 前提の明記・`GPU_TOLERANT`・`GPU=1`・`DPR`・`HEADED` の環境変数）。
- **L1**（テストの穴）: `unity-cases.generate.mjs` に、SDK の集計（親が EditorOnly のときの子の除外・スキンドメッシュの数え方・PhysBone の数え方・アバターが無いとき）・Undo の上限（50）・`AssignMaterial` の番号の境目（枚数ちょうど = 末尾に足す）の 3 ケース（102 ケース・2,617 操作）。変異（Undo の上限 50 → 49・EditorOnly を親から伝えない・番号の境目を `>=` に・PhysBone を無効な親で数えない・スキンドメッシュを有効なものだけ数える）で、それぞれ落ちることを確かめた。UI から起こせる「親に EditorOnly を付けたときの SDK の集計」は、`unity-compare.mjs` の実入力にも 1 場面。
- **L3**: `AssignMaterial` は、番号が 0..枚数の外なら何もしない（`unity.js` は範囲外でも配列を広げる（穴ができる）が、Inspector の欄からは起こせない。1e9 で固まらないための意図した差）。`UnityRobustnessTests`。
- **L5**: JS モジュールの読み込み（`gl.js`・`practice-cs.js`・`unity-ui.js`）は、失敗した Task を static に残さず、次の呼び出しでやり直す（`RetryableTask`。`PracticeCsAssemblies` と同じ決まり）。`RetryableTaskTests`。
- **L2**・**L4**: §15.5・§15.4。

## 16. S6（共有ルーム `/practice/room` を C# で。`?engine=cs`）の記録

印: **[実測]** はこの作業で測った値。既定は今までの room.js のまま（`?engine=cs` を付けたときだけ C# 版。タッチだけの端末は、今までの案内の画面）。

### 16.1 作ったもの

- **`Practice`（純粋。`RoomSession.cs`・`RoomWire.cs`・`ViewportCore.Room.cs`）**: `RoomSession` は `room.js` の状態と規則（参加者・差分（`server` の対応表）・ロック・追従・途中経過・カーソル・視点の間引き・名前の入力の 500 ms・切断の再試行 6 回・退室）。`room.js` の関数と同じ順で書いた（`onMessage`・`onLocal`・`syncOut`・`tick`・`connect` の `close`）。通信とタイマーは `IRoomTransport`（テストでは仮想の時計）。`RoomWire` は JSON.stringify と同じ文字（キーの順・数の表記・非 ASCII はそのまま）を作る。`ViewportCore.Room.cs` は、参加者の枠の色（選択は先の人、操作中は後で上書き）・カーソルの画面の位置（`room.js` の hidden の条件と同じ）・途中経過の適用。
- **`Render`**: `SceneRenderer` が、ほかの人の選択・操作中のオブジェクトに、その人の色の枠（深度テストなし・半透明 0.9・renderOrder 8。`room.js` の `remote-sel`）を描く。ほかの人がいないときは描かない（練習の描画は変わらない）。
- **`Web`**: `Services/RoomCs.cs`（`RoomCsEngine : CsViewEngine`。ビューと HUD は練習と同じ `PracticeHudView`。`IRoomTransport` の実装 `RoomTransport` は `RoomJs`（JSImport））、`wwwroot/js/room-cs.js`（WebSocket・タイマー・localStorage・ポインター・クリップボード・`window.__room`。規則は持たない）、`Pages/PracticeRoom.razor`（`?engine=cs` の分岐。参加者の一覧とステータスの markup は JavaScript 版と同じ構造・クラス名）。`CsViewEngine` に `OnAfterFrame` の口を 1 つ足した（既定は何もしない）。
- **確認**: `tools/verify/room-compare.mjs`（JS 版と C# 版に、同じ台本を通す）、`tools/verify/room-mixed.mjs`（JS 版と C# 版の二人を同じ部屋に入れる）、`practice.mjs` の room（`ENGINE=cs` と `ENGINE=js`）と `ONLY=trips`（C# の部屋も往復に入れた）、`csp-check.mjs`（C# の部屋の 2 画面を追加）、`RoomSessionTests`（34 件）。

### 16.2 JavaScript 版からの変更点（意図した差）

1. **通信の層だけ JavaScript（`room-cs.js`）に残した**。設計 §2 の表は `ClientWebSocket` と書いたが、WebSocket・`setInterval`・`setTimeout`・`pagehide` は JavaScript のコールバックで動かし、規則だけ C# に置いた。理由: 練習の `Task.Delay` が JavaScript のコールバックの中で再開しなかった問題（§14）と同じ経路を避けるため。WebSocket の close コード（4001・1008・1006・4000）の扱いは `RoomSession` が決めるので、JavaScript 側は文字を渡すだけ。
2. **`window.__room` は形だけ同じ**（`vp.objects[].userData.kind`。確認用。`?debug=1` のときだけ）。
3. **表示名の確定は、入力のたびに「最後の入力の値」を記録し、最後の入力から 500 ms のあとに決める**（room.js は、タイマーの時点で入力欄の値を読む。結果は同じ）。タイマーは 1 本（100 ms ごとの確認）で、入力のたびにタイマーを作らない。入力欄の `value` は初期値だけを渡し、以後は Blazor が書き換えない（入力中の文字を戻さない）。
4. **`-.` の NaN**: JS 版は S1 で直し済み（`viewport.js` の数値入力は、`parseFloat` が NaN のあいだ「数値入力なし」）。C# 版も `TransformOp` の数値入力で、数値にならないあいだは「数値入力なし」。ここでは直していない。`room-compare.mjs` の `minus-dot` の場面は、G の中で `-` と `.` を打った後の画面が JS 版と一致することを確かめる（NaN で対象が消える不具合が起きれば、この場面の画素が違う）。

### 16.3 受け入れ（§4.2 の S6）の結果

| 基準 | 結果 [実測] |
| --- | --- |
| 二人が入れる・追加/移動が届く・退室が消える | `room-mixed.mjs`: JS 版（A）と C# 版（B）を同じ部屋に入れ、A が動かした立方体が B に、B が動かした立方体が A に、画素 **0.000 % / 最大 0**。参加者の一覧が一致 |
| プロトコルは変えない | 変更なし（`worker/` は触っていない）。C# 版が送る文字は、同じ台本で JS 版と **hello・name・select・lock・unlock・ops が完全に一致**（`room-compare.mjs`。2 回実行で一致）。cursor・preview・view は timing で数が変わるので数だけ比べる（cursor 8〜11・view 2） |
| 比較（閾値 0 % / 8） | `room-compare.mjs`: 9 場面（ほかの人の選択・ロック中の G・`-`/`.` の G・ほかの人のカーソル・球の追加・こちらの移動中・確定・視点に合わせる・退室）で、**どの場面も画素 0.000 % / 最大 1**、状態・参加者・追従の文字が一致（2 回実行） |
| そのほか | `practice.mjs` の room（`ENGINE=cs`・`ENGINE=js`）すべて ok。`ONLY=trips`（20 往復。C# の部屋を含む）: gl・ResizeObserver・IntersectionObserver・interval・WebSocket・canvas・listeners が増えない。`csp-check.mjs` 54 画面（C# の部屋 2 画面を含む）で CSP 違反 0・コンソール 0。`practice-compare.mjs`（Blender の練習。共有の描画を触ったため）0.000 % / 最大 1 |
| テスト | `dotnet test -c Release` 全件成功（Practice.Tests は RoomSessionTests 34 件を含む）。`node --test`（worker・tests/js。`SEARCH_INDEX_FILE=dist/data/search.json`）106 件成功 |

### 16.4 計測 [実測]

- 配信物（brotli q11。`origin/develop` と同じ方法で、同じ設定の publish。`/practice/room` を含む全ページが読むのは `Web` のみ）:
  - `Web`（主のアセンブリ）120,445 → **127,628 バイト（+7,183 = +7.0 KB）**。S5 の +24.8 KB の後の上乗せ（`PracticeRoom.razor`・`RoomCs.cs`）。全ページに効く
  - `Practice`（`?engine=cs` のときだけ読む）138,553 → 147,713（+9,160）。`Render` 21,899 → 21,933（+34）
  - `room-cs.js` 2,096（`?engine=cs` の部屋だけ）。`gl.js`・`practice-cs.js` は変えていない
- 本体は、`?engine=cs` の部屋の起動の時間は測っていない（S5 までの測り方は、Blender の練習と Unity の画面。部屋は時間ではなく同期の正しさを優先した）。

### 16.5 確証が持てない点・申し送り

- **Node の突き合わせ（room.js と `RoomSession` を、仮想の時計と偽物の WebSocket で同じ入力に通す）は作っていない**。同じ台本を、本物の二つの画面（JS 版と C# 版）に通す比較（`room-compare`）と、`RoomSessionTests` 34 件（room.js を読んで書いた期待値）で代えた。`lockdenied`・`rejected`・4001・1008・再試行 6 回・名前の 20 字・stale な接続は単体テストで確かめた。網羅は Node の突き合わせより弱い（次の段階で必要なら作る）。
- 比べたのは SwiftShader の Chromium だけ。実機の GPU・Firefox・Safari・二台の実機の回線は未確認。dev サイト（avatar-dev）で、二人の PC（Chrome）から、追加・移動・ロック・追従・退室・Ctrl+Z・表示名の変更・切断（オフラインにして戻す）を確認してほしい。
- 切断・再接続の文言（4000・1006）は、単体テストで規則を確かめただけで、実際に切ったところは `room-compare` では見ていない。
- 参加者の一覧（`Razor`）は、JavaScript 版と同じ文字・クラス名だが、タグ単位の DOM 差は比べていない（`dom-diff` は練習 3 ページの静的 HTML が対象）。
- S7 への申し送り: `room.js` は `?engine=js` のための既定として残す（S7 で消す）。`viewport.js` の `roomState`・`applyRemote`・`setRemotePeers` も、S7 で消える。`RoomSession` は three.js に依存しない（S7 のあとも残る）。

### 16.6 レビュー指摘への対応（PR #120 の独立レビュー）

- **M1（部屋の切り替えと読み込みの並走）**: `StartCsAsync` は、読み込みの前と後に「ページを離れた・部屋が替わった」を確かめ、古ければ作ったエンジンを破棄して戻る。代入の前に既存の C# 版を `StopCs` で解放する。確認: `tools/verify/room-race.mjs`（Practice の wasm を 4 秒遅らせ、読み込みの途中で部屋を替える。開いている WebSocket は B の 1 本だけ・canvas は 1 つ）。
- **M2（不正な文字）**: サーバーの文字は、形（欠けた項目・数でない要素・短い配列・想定外の型）を全部確かめてから適用する。不正なものは、状態を 1 つも変えずに捨てる（`OnMessage` は JSON の失敗と想定外の例外も捨てる）。`ApplyPreview` は長さも確かめる。
- **M3（テスト）**: `RoomSessionTests` を 34 件に（不正な JSON・欠けた項目・壊れた配列、preview が操作中のものを飛ばす条件、取り消しのときに途中経過のものを最後の形で送る（force）、join での置き換え、peer で自分の名前が変わる、too many objects、今の接続でない socket、rejected の真偽、ロックの値が文字でない場合）。`room-compare.mjs` の固定の待ちは、名前の確定（相手の `peer` の受信）とロックの反映（「操作中のため」の文字）を条件待ちにした。残りの固定の待ち（解除の受信・描画の落ち着き）は、観測できる文字が無いので短い待ちのまま。
- **L1**: `OnOpen` は今の接続のものだけ扱う（古い接続の hello は送らない）。**L2**: `rejected` は JavaScript の真偽（0・空の文字・null は偽）。**L3**: `locks` は値が文字のものだけ使う。**L4**: コメント（`RoomCsEngine.Dispose` の説明）を、実際の順序に合わせた。**L7**: 名前の入力欄の `value` は初期値だけ（上記 3）。**L8**: §16.2 の 3・4 を、実際に確かめた範囲に書き直した。
- **L5（JSMarshalAs<JSType.Function> の寿命）**: 調べた範囲: JSImport の関数引数は、JavaScript 側の関数（クロージャ）が managed の delegate を保持し、関数が解放されると managed 側の参照も解ける。JavaScript 側の保持は `room-cs.js` の `timers`（繰り返しのタイマー・`cancelTimer` まで）と、`attach`・`setHandlers` の登録。一回限りのタイマーは発火後に `timers` から外れる。名前の入力は、入力のたびにタイマーを作っていたので、1 本の繰り返しの確認（100 ms）に変えた。.NET の仕様の細部（解放の時点）は、この作業では確かめていない。実際の動作は `room-compare`・`room-mixed`・`room-race`・`practice.mjs` の往復（gl・ws・interval・listeners が増えない）で確かめた。
- **L6**: 対応不要（指摘どおり）。

## 17. S7（three.js を外す）の記録

- **決定（ユーザー 2026-10-25）**: S6 と S7 を並行で進め、本番（master）への反映はユーザーが行う。S7 の PR は develop 向け（`migration/wasm` は origin に無い。#119 も develop 向け）。**本番での実績（S3〜S6 を本番に出して一定期間の確認）の前に three.js を外した**。リスクは PR 本文に書く。
- **段階**: (a) 既定を C# 版にする（`?engine=` の指定は無くなり、`engine=js` も効かない。`?engine=js` の JS 版は残さない）／ (b) three.js の 1 人用の練習を消す（`practice.js`・`unity.js`・`unity-practice.js`。`tutorial.js` は「画像で見る」だけにした）／ (c) **S6 まで残すもの**: 共有ルーム（`room.js`・`viewport.js`・`editmesh.js`）、`lib/three`・importmap・CSP のハッシュ（`SiteBuilder.ImportMapHashes`）、README の一文。S6 が入ったら、これらを消して S7 を完了する。

### 17.1 基準の凍結（three.js の版を消す前）

three.js の版（commit `8262089`）から作った基準を、ファイルにした。以後の比較は、基準と C# 版を比べる。

- 画像・DOM・文字・状態（`tests/visual/golden/`）: `practice-compare`・`practice-details`・`tutorial-compare`・`tutorial-input-compare`・`unity-compare`・`tutorial-dom`。PNG と JSON。約 25 MB（PNG は圧縮のまま。`tutorial-compare` が 12 MB）。
- Node の基準（`tests/js/golden/*.json.gz`）: `practice-basic`・`practice-edit`（C# の `ParityTests`・`EditParityTests`）、`practice-tutorial-render`・`render-basic`・`render-edit`・`render-tutorial`（描画の突き合わせ）、`unity`（`UnityParityTests`）、`tutorial`（`TutorialParityTests`）。gzip で約 17 MB。C# のテストは Node を動かさず、これを読む。
- 作り方（three.js の版が要る。今は作れない）: `git worktree add <dir> 8262089`、`dotnet publish` と Build（CLAUDE.md の手順）で、リポジトリの外に配信する（作業用の静的サーバーで配った。CSP は `_headers` の `/*` を付ける）。その配信に対して、`GOLDEN_MODE=capture` で `tools/verify/*` を動かす（`tools/verify/README.md`）。Node の基準は `tests/js/*-reference.mjs`（8262089 にある）を `node` で動かして `.json.gz` にする。
- 確かめ: 8262089 の基準に対して、C# 版の `tutorial-compare`（既定の比べ方）が、一時的に変えた C# の文言（「左クリック」→「右クリック」）で NG になることを確かめた（3 手順で、文字の差と画素の差 0.023 %・最大 217）。変えた文言は戻した。
- 併せて直したもの: `UnityData.PartMesh` の形のキャッシュ（`Dictionary` → `ConcurrentDictionary`）。テストが並列に動くと壊れた（ブラウザは 1 スレッドなので、本番では起きない）。

### 17.2 配信量（brotli q11。`publish/wwwroot` の js・css・dll・wasm・json・html。`data/` と画像は除く）

| 対象 | 前（8262089） | 後（この PR） | 差 |
| --- | --- | --- | --- |
| 合計 | 3,505,743 B | 3,483,445 B | **−22,298 B（約 −22 KB）** |
| `unity.js` | 17,614 B | 0 | −17,614 |
| `practice.js` | 2,168 B | 0 | −2,168 |
| `unity-practice.js` | 1,591 B | 0 | −1,591 |
| `tutorial.js` | 7,773 B | 6,856 B | −917 |
| `practice-entry.js` | 1,682 B | 1,730 B | +48 |
| `lib/three/three.module.js` | 207,529 B | 207,529 B | 0（S6 まで残る） |

S6 のあとに消せる分（見込み。同じ方法）: `room.js` 4,986 + `viewport.js` 20,575 + `editmesh.js` 8,860 + `three.module.js` 207,048 = **約 241 KB**（この PR の −22 KB と合わせると、three.js 系の JS は、この PR の前から約 264 KB 減る）。

### 17.3 検証（この PR の状態。本番の配信ではなく、`publish` と Build の出力を `tests/` の静的サーバーで配った）

- `dotnet test -c Release`: 857 件（Content 26・Build 47・Web 200・Practice 358・Render 226）すべて成功。
- `node --test`（CLAUDE.md の一覧）: 121 件すべて成功（`SEARCH_INDEX_FILE` を Build の出力に向けた）。
- 基準との比較（既定の比べ方。C# 版と凍結した基準）: `practice-compare` 9/9、`practice-details` 25/25、`unity-compare` 44 件、`tutorial-input-compare` 66 件、`tutorial-compare` 156 件、`tutorial-dom` 25 件（うち 1 回目は待ちがタイムアウト。単独でやり直して通った。環境の揺れとして記録）。すべて ok。
- `dom-diff`（静的 HTML と起動後）: `/practice`・`/practice/unity`・`/practice/room` の 3 ページで差なし。
- `csp-check`（`_headers` の CSP を付けた配信）: CSP 違反 0。NG は `/report`（Worker の API が無い）と `/practice/room`（WebSocket が無い）の 404。Worker の無い静的サーバーのため。
- スマートフォン幅（390・タッチ。`probe-touch` の手順）: 記事（`transform`・`editor-overview`・`edit-mode`）と `/practice` の「画像で見る」は、画像 6 枚が読め、Practice・Render の wasm は読まれない（base と同じ）。**一度、C# 版の記事内チュートリアルを読んでしまう不具合があった（`startArticle` が常に C# 版を始めたため）。タッチだけの端末では C# 版を始めないように直した。** 差: 3D に切り替える「3D ビューで操作する」ボタンは、スマートフォンでは出ない（base では出て、押すと 3D が始まった。この版の tutorial.js は画像だけ）。
- `practice.mjs`（`ENGINE=cs`）: 基準（`practice-baseline.json`）との 49 件が ok。**共有ルームの部分は、Worker の WebSocket が無いため動かせなかった（S6 のあとに、wrangler の配信で確かめる）**。

### 17.4 この PR で消したもの・残したもの

- 消した: `wwwroot/js/practice.js`・`unity.js`・`unity-practice.js`。Node の基準を作る `tests/js/` の生成スクリプト（`practice-reference.mjs`・`practice-render-reference.mjs`・`unity-reference.mjs`・`tutorial-reference.mjs`・`practice-dom.mjs`・`practice-hooks.mjs`・`*-cases.generate.mjs`・`practice-fragile.mjs`・`practice-noise.mjs`・`practice-numeric.test.mjs`。作り方は 8262089 にある）。`PageDomParityTests` の `/practice`・`/practice/unity` は、bUnit では C# 版が始まらない（WebGL も WASM も無い）ので外し、`dom-diff`（ブラウザ）で見る。
- 残したもの（S6 で消す）: `room.js`・`viewport.js`・`editmesh.js`・`lib/three`・importmap・CSP のハッシュ・README の Three.js の一文。`tests/js/practice-dom.mjs`・`practice-hooks.mjs` は、今は使われていない（S6 の Node の突き合わせが使うので残した。S6 がマージされたら消す）。
- 残した（後で整理する）: `tutorial.js` の手順の処理（`replayTo`・`onEvent`・`skip` など）は、3D（`this.vp`）が無いので到達しない。画像の作成（`tools/tutorial-images`・`captureStep`）は、3D の版が無いので動かない（作成済みの画像は残る）。`tools/verify/practice.mjs` の `ENGINE=js` は、もう JS 版を作らない（`ENGINE=cs` と同じ）。

### 17.5 確証が持てない点・S6 への申し送り

- 本番での実績がないまま three.js の 1 人用の練習を消した（ユーザーの指示）。問題が見つかったときの戻し方は、この PR を戻す（`git revert`）か、8262089 の JS を取り戻す。`?engine=js` は無い。
- 比較の基準は SwiftShader（DPR 1）前提。実 GPU・小数の DPR の差は、`tools/verify/README.md`（§15.5）のとおり。
- S6（共有ルーム）: `room.js` を C# にしたら、`viewport.js`・`editmesh.js`・`lib/three`・importmap・CSP のハッシュ・README の一文を消す（S7 の完了）。
