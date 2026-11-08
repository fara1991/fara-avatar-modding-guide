namespace FaraAvatarModdingGuide.Practice;

/// <summary>ビューポートが出すイベント(viewport.js の emit)。Detail は JavaScript のイベントオブジェクトと同じ名前の値(文字・真偽・数・文字の配列・入れ子の辞書)。</summary>
public sealed record ViewEvent(string Type, IReadOnlyDictionary<string, object?> Detail);

/// <summary>ポインターやキーの修飾キー。</summary>
public readonly record struct Modifiers(bool Shift = false, bool Ctrl = false, bool Alt = false, bool Meta = false);

/// <summary>3D ビューが画面のどこにあるか(getBoundingClientRect と同じ。ページ上の座標)。</summary>
public readonly record struct ViewRect(double Left, double Top, double Width, double Height);

/// <summary>最初に置くオブジェクト(Viewport の options.scene の 1 件)。P は位置、R は度、S はスケール。</summary>
public sealed record SceneSpec(string Kind, string? Name = null, double[]? P = null, double[]? R = null, double[]? S = null);

/// <summary>Viewport のコンストラクタの options に当たるもの。</summary>
public sealed class ViewportOptions
{
    /// <summary>最初に置くオブジェクト(省略すると立方体 1 つ)。</summary>
    public IReadOnlyList<SceneSpec>? Scene { get; init; }
    /// <summary>数字キーをテンキーとして扱うか。</summary>
    public Func<bool>? EmulateNumpad { get; init; }
    /// <summary>true ならフォーカス中だけホイールでズームする(記事内でページのスクロールを邪魔しない)。</summary>
    public bool WheelNeedsFocus { get; init; }
    /// <summary>最初の視点の距離(0 や省略は既定の 11)。</summary>
    public double? ViewDist { get; init; }
    /// <summary>最初の注視点(省略は原点)。</summary>
    public double[]? ViewTarget { get; init; }
    /// <summary>共同練習: ほかの人が操作中のオブジェクトを動かせないようにする。動かせない理由の文を返す(動かせるなら null)。</summary>
    public Func<IReadOnlyList<SceneObject>, string?>? CanTransform { get; init; }
    /// <summary>オブジェクトの部屋 ID(userData.rid)を作る。省略するとランダムな 8 文字(a-z0-9)。テストでは決まった列にする。</summary>
    public Func<string>? NewId { get; init; }
}

/// <summary>メニュー(右クリックの代わりに X・Shift+A・M・P で出る小さな一覧)の 1 項目。</summary>
public sealed record MenuItem(string Label, Action Run);

/// <summary>いま開いているメニュー。画面に出すときは Title と Items の Label。選んだら <see cref="ViewportCore.ChooseMenu"/>。</summary>
public sealed record MenuState(string Title, IReadOnlyList<MenuItem> Items);

/// <summary>ループカット(Ctrl+R)のプレビュー中の状態。Edge はマウスの下の辺、Preview は入る線の端点(ローカル座標。2 点ずつ)。</summary>
public sealed class LoopCutState
{
    public int Cuts { get; set; } = 1;
    public EmEdge? Edge { get; set; }
    public List<Vec3>? Preview { get; set; }
}

/// <summary>画面上部のボタン(頂点・辺・面・透過・統計・ミラー)の状態。</summary>
public sealed record ToolState(bool On, bool Disabled);

/// <summary>N パネルの 1 行(位置・回転・スケールの X / Y / Z)。Value は入力欄の文字。</summary>
public sealed record NPanelRow(string Prop, string Axis, string Value, bool Disabled);

/// <summary>N パネル(アイテム)。</summary>
public sealed record NPanelState(bool Hidden, string Name, IReadOnlyList<NPanelRow> Rows);

/// <summary>編集モードで選んでいる面(ワールド座標の中心と法線)。Area はローカル座標での面の Newell ベクトルの長さ(面積の 2 倍。ほとんど 0 の面は法線の向きが丸めの誤差で決まる)。</summary>
public sealed record EditFace(Vec3 Center, Vec3 Normal, int Sides, double Area);

/// <summary>editInfo(): 編集中のメッシュの選んだ面と、全頂点の位置(ワールド座標)と、数。</summary>
public sealed record EditInfo(IReadOnlyList<EditFace> Faces, IReadOnlyList<Vec3> Verts, (int Verts, int Edges, int Faces, int Tris)? Stats);

/// <summary>共同練習の部屋で共有するオブジェクト 1 件(roomState)。値は小数 4 桁に丸める。</summary>
public sealed record RoomObject(string Id, string Kind, string Name, double[] P, double[] R, double[] S);

/// <summary>視点(getView / setView)。</summary>
public sealed record ViewState(double[] Target, double Dist, double Theta, double Phi, bool Ortho, string Name);
