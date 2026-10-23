# Phase 3b 設計メモ: 練習ページの 3D の C# 化

計画書（`docs/wasm-migration-plan.md`）の §4.5・§5 Phase 3b・D15 の続き。Phase 3b は**任意・後続**で、本番公開（master）の条件には含まれない。ここに書くのは、着手前の調査と設計、最初の一歩（純粋なロジックの C# 化）の記録で、画面の挙動はこの段階では変えない。
印の意味: **[実測]** はこの作業で測った・確かめた値、**[試作]** は draft PR #70 の記録（この作業では再計測していない）、**[推測]** は根拠のある見積もりで未検証。

## 0. 要点

- **描画層の推奨は (A) WebGL の薄い自作層**（`[JSImport]` で WebGL 2 を呼ぶ小さな JS と、描画命令を作る C#）。KNI は採らない。理由は §3: 陰影を three.js に合わせられる（試作で面の明るさが ±2 以内。KNI は左の面が 23 対 70 で大きく外れる）、追加の配信物が小さい、シェーダーを自分で持てる、KNI は .NET 10 での動作が未確認で、試作の未解決の問題（Tick のばらつき）の原因が KNI 側にあるか自分たち側にあるかも未切り分け。ただし**判断の関門を §4 の段階 2 に置く**（実装前に、画像の比較で A を確定する）。
- **C# 化の単位は 4 層**: ① 純粋なロジック（`FaraAvatarModdingGuide.Practice`。この PR で着手）② 描画層（段階 2 で作る別プロジェクト）③ 画面の部品（Razor。HUD・お題の一覧・メニュー・Unity 風のパネル）④ 通信（共有ルームの WebSocket）。three.js を配信物から外せるのは、**記事内チュートリアルと Unity 風も C# に移ったあと**（`unity.js` と `tutorial.js` が three.js を使っているため）。それまでは three.js の同梱・importmap・CSP のハッシュは残る。
- **段階は 8（S0〜S7）**。各段階は小さな PR に分け、現行の three.js 版を残したまま**切り替えフラグ**で C# 版を試せる形で進める（ロールバックはフラグを戻すだけ）。最後の S7 で初めて three.js を消す。
- **見積もり**: S0（済・約 3 人日）を除く残りが 約 **51〜77 人日**（[推測]。計画書の 40〜60 より大きい。理由は §6）。FBX は**今の JS に読み込みの実装が無い**ので、移植ではなく新機能になり、別枠 5〜15 人日（[推測]）。
- **この PR（S0）の成果**: 設計メモ（本書）と、純粋なロジック 約 1,100 行の C# 化。現行の `viewport.js`・`practice.js` を Node で**そのまま**動かした結果との突き合わせテスト（80 シナリオ・約 5,500 手順・お題 314 状態）が通る。画面は変わらない。

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
| **S1** ロジックの完成 | 編集モード（`editmesh.js`: 選択モード・選択の伝播・押し出し・差し込み・ループカット・ベベル・結合・面張り・分離・削除）、追加（Shift+A）・複製・削除・取り消し/やり直し・ミラー・X 線・N パネルの値、共同練習の口（`roomState`・`applyRemote`・`canTransform`・`getView/setView`）、`wheelNeedsFocus` | S0 と同じ突き合わせを、編集モードを含む操作列（乱数 + 練習 1 の 18 件のお題を全部こなす手順）に広げて一致。`practice-baseline.json` 相当の最終状態が一致 | 10〜15 |
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
| `-` と `.` の入力で `-.` になる（現行は NaN でオブジェクトが消える不具合） | — | C# は「数値入力なし」として扱う（直した）。現行の JS の不具合は S7 まで残る。S1 で JS 側を直すかをユーザーに確認 |
| three.js の `Box3.expandByObject` が非表示の mirror 子も数える（編集で左右非対称になったメッシュで `Home`・`Numpad .` の結果に効く） | 非対称のメッシュのフレーミング | C# は同じ扱いを実装済み（`SceneObject.WorldBounds`）。**対称な基本の形でしか現行と突き合わせていない**ので、S1（編集モード）で非対称のメッシュを突き合わせに加える |
| `Hoodie.fbx` の期待（将来のアバター編集） | スコープ膨張 | FBX は別枠にして、先に決める（読み込み方式は未定） |
| KNI を使わないことが後で覆る | 手戻り | 描画層は `Practice`（①）の外に出すので、差し替えは②だけ。①は描画に依存しない（この PR の構造） |
| .NET の更新（10 → 11）で JSImport の挙動が変わる | 保守 | 描画層の interop は小さく保つ。CI の `dotnet publish` + Playwright で検知 |

## 6. 見積もりの根拠

計画書の 40〜60 人日（試作の「2〜3 か月」から換算）を、棚卸しの行数と段階ごとに分け直した。

| 項目 | 根拠 | 人日 [推測] |
| --- | --- | --- |
| S0 | この PR（実績の目安） | 約 3 |
| S1 | `viewport.js` の残り 約 1,100 行と `editmesh.js` 789 行の移植 + 突き合わせの拡張。S0 の実績（約 1,100 行 + テスト基盤）の 1.5〜2 倍 | 10〜15 |
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

- **意図した差**: ① 数値入力が `-.`（符号のあとに小数点だけ）のとき、現行は `NaN` になってオブジェクトが消える。C# は「数値入力なし」として扱う（突き合わせの入力から外した）。② 状態表示の `-0.000` と `0.000`: 値が 1e-16 程度の誤差のとき、符号が計算の順序（three.js の行列と C# の式）で変わり、`toFixed` が `-0.000` を出すかが分かれる。表示の比較では両者を同じとみなす（値そのものは 1e-9 で比べている）。
- **突き合わせていない**: ホイールの `wheelNeedsFocus`（記事内のみ）、共同練習の `canTransform`、`ResizeObserver` による再計算（縦横比はコンストラクタと `Resize`）、`Home` の非対称メッシュ、取り消し（`undoStack` は数を合わせるだけ）、追加・複製・削除、編集モード、メニュー、N パネル。S1 で順に広げる。
- **配信物に関する確認 [実測]**: `Web` が `Practice` を参照するだけで（呼ばなくても）、配信物が増えた（`FaraAvatarModdingGuide.Practice.*.wasm` 79 KB（brotli 28 KB）が加わり、ほかのアセンブリのファイル名のハッシュも変わる。ファイル数 100 → 101、合計 11.71 MB → 11.79 MB、brotli（品質 9）の合計で +32 KB）。この段階は「画面も配信物も変えない」ので、**`Web` への参照は付けていない**。参照は、`Practice` を最初に呼ぶ段階（S3）で付ける。

### 7.4 検証の結果

- `dotnet test`（ソリューション全体）、`node --test`（既存の `tests/js/*.test.mjs`）、`wrangler deploy --dry-run`、最新コミットの CI は PR に記載。

## 8. ユーザーへの確認事項

1. **描画層は (A) 薄い WebGL 層でよいか**（KNI は採らない）。KNI の Tick のばらつきは、KNI を採らない場合は原因の切り分けを「対象外」としてよいか。「新しいブラウザコンテキストで描画が始まらない」問題は、A でも S2 の最初に必ず原因を特定する（それでよいか）。
2. **S1 以降の進め方**: 段階ごとに報告して続行の確認を取る形でよいか。次は S1（ロジックの完成。編集モードの移植）を提案。
3. **現行 JS の `-.` の不具合**（数値入力で NaN → オブジェクトが消える）を、S1 で JS 側も直すか（C# 側は直してある）。
4. **FBX**: 今は読み込みの実装が無く、新機能として別枠にする。優先度と、必要な機能（アバターの表示だけか、ファイル照合も含むか）。
5. **AOT（`wasm-tools`）**: 必要になった場合に導入してよいか（サイズが増える。SDK 全体に影響）。S2 の測定で必要か判断する。
6. **three.js を消す時期（S7）**: 戻せない変更なので、S3〜S6 を本番に出して一定期間（目安）問題がないのを確かめてからでよいか。
