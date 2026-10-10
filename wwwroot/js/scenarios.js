// 記事内チュートリアル（::: practice <名前>）の手順。キーが Markdown に書く名前。
// 書き方は Content/README.md の「操作の練習を記事に埋め込む」を参照。
//
// シナリオ: { title, scene?, numpad?, steps: [ステップ...] }
//   scene  最初に置くオブジェクト [{ kind, name?, p?: [x,y,z], r?: [度...], s?: [...] }]。省略時は立方体 1 つ
//   numpad true なら「テンキーを模倣」のチェックボックスを出す
//
// ステップ:
//   text       案内文（HTML 可。キーは <kbd>G</kbd>）
//   keys       画面下に出すキー（"Shift+A" のように + でつなぐと同時押し）。押すと光る
//   mouse      マウス操作の説明（キーの横に表示）
//   highlight  { object: 名前, label: 矢印の文字, axis: "x"|"y"|"z", hud: "view" }
//   done(e, vp, ctx)  イベント e で達成なら true。ctx はステップごとの作業用オブジェクト
//   hint(e, vp, ctx)  ヒントを出すなら文字列（手順は進まない）
//   fail(e, vp, ctx)  文字列を返すと、group の最初の手順からやり直しになる
//   skip(vp)   「次へ（スキップ）」や再開のときに、このステップを終えた状態にする処理
//   group      fail・「やり直す」で戻る単位（同じ group の最初のステップに戻る）
//   allowOp    true なら、このステップ中に G / R / S が始まっても注意を出さない
//
// e はビューポートのイベント（viewport.js の先頭のコメント参照）。

const OP_NAME = { G: "移動", R: "回転", S: "スケール" };
const UNIT = { G: " m", R: "°", S: " 倍" };

/** G / R / S → 軸 → 数値 → Enter を 1 キーずつ案内するステップ群 */
function opSteps({ key, axis = null, value, target = "Cube", group, first }) {
  const name = OP_NAME[key];
  const A = axis?.toUpperCase();
  const interrupted = e =>
    e.type === "cancel" ? "キャンセルされました。" :
    e.type === "confirm" ? "途中で確定されました。" : undefined;
  const steps = [{
    group, keys: [key], highlight: { object: target },
    text: first ?? `<kbd>${key}</kbd> を押して${name}を始めてください。`,
    done: e => e.type === "opstart" && e.kind === key,
    hint: e => e.type === "opstart" ? `${OP_NAME[e.kind]}が始まりました。<kbd>Esc</kbd> でキャンセルしてから <kbd>${key}</kbd> を押してください。` : undefined,
    skip: vp => vp.startOp(key),
  }];
  if (axis) steps.push({
    group, keys: [A], highlight: { object: target, axis },
    text: `<kbd>${A}</kbd> を押して、${A} 軸の方向だけに固定してください。${A} 軸の線が表示されます。`,
    done: e => e.type === "axis" && e.axis === axis && !e.exclude,
    hint: e =>
      e.type === "axis" && e.axis && e.axis !== axis ? `${e.axis.toUpperCase()} 軸になっています。<kbd>${A}</kbd> を押してください。` :
      e.type === "axis" && e.exclude ? `<kbd>Shift</kbd> を押すと「${A} 以外」になります。<kbd>${A}</kbd> だけを押してください。` :
      e.type === "numeric" ? `先に軸を固定します。<kbd>Backspace</kbd> で数字を消してから <kbd>${A}</kbd> を押してください。` : undefined,
    fail: interrupted,
    skip: vp => vp.setAxis(axis, false),
  });
  steps.push({
    group, keys: [...value], highlight: { object: target, axis },
    text: `数字で <kbd>${value}</kbd> と入力してください。${name}の量がちょうど ${value}${UNIT[key]} になります。`,
    done: e => e.type === "numeric" && e.value === value,
    hint: e =>
      e.type === "numeric" && !value.startsWith(e.value) ? `「${e.value}」になっています。<kbd>Backspace</kbd> で消して ${value} と入力してください。` :
      e.type === "axis" && axis ? `軸が変わりました。<kbd>${A}</kbd> を押して戻してください。` :
      e.type === "axis" ? "軸が固定されました。同じキーをもう一度押すと解除できます。" : undefined,
    fail: interrupted,
    skip: vp => {
      while (vp.op && vp.op.numeric) vp.typeNumeric("Backspace");
      for (const ch of value) vp.typeNumeric(ch);
    },
  });
  steps.push({
    group, keys: ["Enter"], highlight: { object: target },
    text: `<kbd>Enter</kbd> で確定してください（左クリックでも確定できます）。`,
    done: e => e.type === "confirm" && e.value === value && e.axis === axis,
    fail: e =>
      e.type === "cancel" ? "キャンセルされました。" :
      e.type === "confirm" ? "入力が変わったまま確定されました。" : undefined,
    hint: e => e.type === "numeric" && e.value !== value ? `入力が「${e.value}」になっています。${value} に戻してから確定してください。` : undefined,
    skip: vp => vp.confirmOp(),
  });
  return steps;
}

/** オブジェクトをクリックして選択するステップ */
function clickStep(name, text, group) {
  return {
    group, text, mouse: "左クリック", highlight: { object: name, label: "クリック" },
    done: e => e.type === "select" && e.active === name && e.selected.length === 1,
    hint: e => e.type === "select" ? `${name}（矢印のもの）だけをクリックしてください。` : undefined,
    skip: vp => vp.selectByName(name),
  };
}

/** テンキーで視点を切り替えるステップ */
function numpadStep(key, ctrl, viewName, text) {
  const label = `${ctrl ? "Ctrl+" : ""}Numpad ${key}`;
  return {
    keys: [label], highlight: { hud: "view" }, text,
    done: e => e.type === "view" && e.action === "axis" && e.name === viewName,
    hint: (e, vp) =>
      e.type === "key" && e.code === `Digit${key}` && !vp.options.emulateNumpad?.() ? "テンキーがない場合は、下の「テンキーを模倣」をオンにすると数字キーで同じ操作ができます。" :
      e.type === "view" && e.action === "axis" ? `「${e.name}」からの視点になりました。<kbd>${label}</kbd> を押してください。` : undefined,
    skip: vp => vp.numpadView(key, ctrl),
  };
}

export const SCENARIOS = {
  // getting-started/navigation（前半）
  "navigation-mouse": {
    title: "マウスで視点を動かす",
    steps: [
      {
        text: "ビューポートの上で <strong>ホイールボタン（中ボタン）を押したまま</strong> ドラッグして、視点をぐるっと回してください。",
        mouse: "ホイールボタンでドラッグ（または Alt+左ドラッグ）",
        done: (e, vp, ctx) => e.type === "view" && e.action === "orbit" && (ctx.total = (ctx.total ?? 0) + e.distance) >= 150,
        hint: e =>
          e.type === "view" && e.action === "orbit" ? "その調子です。もう少し大きく回してみましょう。" :
          e.type === "select" ? "左クリックは選択です。ホイールボタンを押し込んだままドラッグしてください。" : undefined,
        skip: vp => vp.orbitBy(0.8, -0.2),
      },
      {
        text: "<kbd>Shift</kbd> を押しながらホイールボタンでドラッグして、視点を平行移動してください。",
        keys: ["Shift"], mouse: "ホイールボタンでドラッグ",
        done: (e, vp, ctx) => e.type === "view" && e.action === "pan" && (ctx.total = (ctx.total ?? 0) + e.distance) >= 100,
        hint: e => e.type === "view" && e.action === "orbit" ? "視点が回りました。<kbd>Shift</kbd> を押したままドラッグしてください。" : undefined,
        skip: vp => { vp.view.target.x += 1.5; },
      },
      {
        text: "ホイールを回して、近づいたり離れたりしてください（3 回ほど）。",
        mouse: "ホイールを回す",
        done: (e, vp, ctx) => e.type === "view" && e.action === "zoom" && (ctx.count = (ctx.count ?? 0) + 1) >= 3,
        skip: vp => { vp.view.dist *= 0.7; },
      },
      {
        text: "<kbd>Home</kbd> を押して、シーン全体が見える位置に戻してください。",
        keys: ["Home"],
        done: e => e.type === "view" && e.action === "home",
        skip: vp => vp.frame(vp.objects),
      },
    ],
  },

  // getting-started/navigation（後半）
  "navigation-numpad": {
    title: "テンキーで視点を切り替える",
    numpad: true,
    steps: [
      numpadStep("1", false, "前", "<kbd>Numpad 1</kbd> で正面（前）から見てください。左上の表示が「前・平行投影」に変わります。"),
      numpadStep("3", false, "右", "<kbd>Numpad 3</kbd> で右から見てください。"),
      numpadStep("7", false, "上", "<kbd>Numpad 7</kbd> で真上から見てください。"),
      numpadStep("1", true, "後", "<kbd>Ctrl</kbd>+<kbd>Numpad 1</kbd> で反対側（後ろ）から見てください。"),
      {
        text: "今は平行投影です。<kbd>Numpad 5</kbd> で透視投影に切り替えてください。",
        keys: ["Numpad 5"], highlight: { hud: "view" },
        done: e => e.type === "view" && e.action === "ortho" && !e.ortho,
        hint: e => e.type === "view" && e.action === "ortho" ? "平行投影に戻りました。もう一度 <kbd>Numpad 5</kbd> を押してください。" : undefined,
        skip: vp => { if (vp.ortho) vp.numpadView("5"); },
      },
      clickStep("Cube", "立方体をクリックして選択してください。", "frame"),
      {
        group: "frame",
        text: "<kbd>Numpad .</kbd> で、選択した立方体を画面の中心に大きく表示してください。",
        keys: ["Numpad ."], highlight: { object: "Cube" },
        done: e => e.type === "view" && e.action === "frame",
        skip: vp => vp.numpadView("Decimal"),
      },
    ],
  },

  // basics/select
  select: {
    title: "選択・追加・削除・複製",
    scene: [
      { kind: "cube", name: "Cube.002", p: [-3.5, 0, 0] },
      { kind: "cube", name: "Cube", p: [0, 0, 0] },
      { kind: "cube", name: "Cube.001", p: [3.5, 0, 0] },
    ],
    steps: [
      clickStep("Cube", "真ん中の立方体（Cube）をクリックして選択してください。オレンジの枠が付きます。", "shift"),
      {
        group: "shift",
        text: "<kbd>Shift</kbd> を押しながら右の立方体をクリックして、選択に追加してください。最後に選んだものが明るい枠の「アクティブ」になります。",
        keys: ["Shift"], mouse: "左クリック", highlight: { object: "Cube.001", label: "Shift+クリック" },
        done: e => e.type === "select" && e.selected.length === 2 && e.selected.includes("Cube") && e.active === "Cube.001",
        fail: e => e.type === "select" && e.source === "click" ? "Shift なしでクリックすると、選択が入れ替わります。" : undefined,
        hint: e => e.type === "select" ? "右の立方体（矢印のもの）を Shift+クリックしてください。" : undefined,
        skip: vp => vp.selectByName("Cube.001", true),
      },
      {
        text: "何もない背景をクリックして、選択を解除してください。",
        mouse: "背景を左クリック",
        done: e => e.type === "select" && e.source === "click" && e.selected.length === 0,
        hint: e => e.type === "select" && e.selected.length ? "オブジェクトのない、背景の部分をクリックしてください。" : undefined,
        skip: vp => vp.selectAll(false),
      },
      {
        text: "<kbd>A</kbd> を押して、すべてを選択してください。",
        keys: ["A"],
        done: e => e.type === "select" && e.source === "all",
        skip: vp => vp.selectAll(true),
      },
      {
        text: "<kbd>Alt</kbd>+<kbd>A</kbd> で、選択をすべて解除してください。",
        keys: ["Alt+A"],
        done: e => e.type === "select" && e.source === "none",
        skip: vp => vp.selectAll(false),
      },
      {
        text: "何もないところから <strong>左ドラッグ</strong> して、立方体を 2 つ以上囲んでください（ボックス選択）。",
        mouse: "左ドラッグで囲む",
        done: e => e.type === "select" && e.source === "box" && e.selected.length >= 2,
        hint: e =>
          e.type === "select" && e.source === "box" ? `囲めたのは ${e.selected.length} 個でした。立方体の中心が枠に入るように、大きめに囲んでください。` :
          e.type === "select" ? "クリックではなく、押したままドラッグして四角で囲みます。" : undefined,
        skip: vp => vp.selectAll(true),
      },
      {
        group: "add",
        text: "<kbd>Shift</kbd>+<kbd>A</kbd> で追加メニューを開いてください。",
        keys: ["Shift+A"],
        done: e => e.type === "menu" && e.menu === "add",
        skip: vp => vp.showAddMenu(),
      },
      {
        group: "add",
        text: "メニューから「UV球」をクリックしてください。新しいものは 3D カーソル（中央の白い輪）の位置に置かれます。",
        mouse: "メニューをクリック",
        done: e => e.type === "add",
        hint: e => e.type === "select" ? "メニューが閉じました。もう一度 <kbd>Shift</kbd>+<kbd>A</kbd> で開いてください。" : undefined,
        skip: vp => { vp.closeMenu(); vp.addPrimitive("sphere"); },
      },
      {
        group: "delete",
        text: "追加したものが選択されたままです。<kbd>X</kbd> を押して削除メニューを出してください。",
        keys: ["X"],
        done: e => e.type === "menu" && e.menu === "delete",
        fail: e => e.type === "delete" ? "<kbd>Delete</kbd> キーは確認なしで削除します。今回は <kbd>X</kbd> を試しましょう。" : undefined,
        skip: vp => vp.showDeleteMenu(),
      },
      {
        group: "delete",
        text: "「削除」をクリックしてください。",
        mouse: "メニューをクリック",
        done: e => e.type === "delete",
        skip: vp => { vp.closeMenu(); vp.deleteSelected(); },
      },
      clickStep("Cube", "真ん中の立方体をクリックして選択してください。", "dup"),
      {
        group: "dup", allowOp: true,
        text: "<kbd>Shift</kbd>+<kbd>D</kbd> で複製してください。複製したものがマウスについてきます。",
        keys: ["Shift+D"], highlight: { object: "Cube" },
        done: e => e.type === "duplicate",
        skip: vp => vp.duplicate(),
      },
      {
        group: "dup",
        text: "マウスを動かして置き場所を決め、<strong>左クリック</strong>で確定してください（<kbd>Esc</kbd> だと元の位置に重なって置かれます）。",
        mouse: "動かして左クリック",
        done: e => e.type === "confirm" || e.type === "cancel",
        skip: vp => vp.confirmOp(),
      },
      {
        text: "<kbd>Ctrl</kbd>+<kbd>Z</kbd> で、複製を元に戻してください。",
        keys: ["Ctrl+Z"],
        done: e => e.type === "undo",
        skip: vp => vp.undo(),
      },
    ],
  },

  // basics/transform
  transform: {
    title: "移動・回転・スケール",
    steps: [
      clickStep("Cube", "まず立方体をクリックして選択してください。", "move-cancel"),
      {
        group: "move-cancel", allowOp: true, keys: ["G"], highlight: { object: "Cube" },
        text: "<kbd>G</kbd> を押して移動を始めてください。マウスを動かすと立方体がついてきます。",
        done: e => e.type === "opstart" && e.kind === "G",
        skip: vp => vp.startOp("G"),
      },
      {
        group: "move-cancel", keys: ["Esc"], mouse: "または右クリック",
        text: "マウスを動かしてみたら、<kbd>Esc</kbd>（または右クリック）でキャンセルしてください。元の位置に戻ります。",
        done: e => e.type === "cancel",
        fail: e => e.type === "confirm" ? "確定されました。キャンセルを試すので、もう一度やりましょう。" : undefined,
        skip: vp => vp.cancelOp(),
      },
      ...opSteps({ key: "G", axis: "z", value: "2", group: "move", first: "今度は正確に動かします。<kbd>G</kbd> を押して移動を始めてください。" }),
      ...opSteps({ key: "R", axis: "z", value: "45", group: "rotate", first: "<kbd>R</kbd> を押して回転を始めてください。" }),
      ...opSteps({ key: "S", value: "0.5", group: "scale", first: "<kbd>S</kbd> を押してスケールを始めてください。軸を固定しなければ、全体が同じ比率で変わります。" }),
      {
        keys: ["N"],
        text: "<kbd>N</kbd> でサイドバーを開いてください。位置 Z が 2、回転 Z が 45°、スケールが 0.5 になっているはずです。",
        done: e => e.type === "npanel" && e.open,
        skip: vp => vp.toggleNPanel(true),
      },
      {
        keys: ["Ctrl+Z"],
        text: "最後に <kbd>Ctrl</kbd>+<kbd>Z</kbd> で、直前のスケールを元に戻してください。",
        done: e => e.type === "undo",
        skip: vp => vp.undo(),
      },
    ],
  },
};
