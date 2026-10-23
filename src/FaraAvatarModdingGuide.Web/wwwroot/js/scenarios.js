// 記事内チュートリアル（::: practice <名前>）の手順。キーが Markdown に書く名前。
// 書き方は Content/README.md の「操作の練習を記事に埋め込む」を参照。
//
// シナリオ: { title, engine?, scene?, view?, numpad?, steps: [ステップ...] }
//   engine "unity" なら Unity の練習画面（wwwroot/js/unity.js）を使う。既定は Blender の 3D ビュー（viewport.js）
//   scene  最初に置くオブジェクト [{ kind, name?, p?: [x,y,z], r?: [度...], s?: [...] }]。省略時は立方体 1 つ
//   view   最初の視点 { dist: 距離, target: [x,y,z] }（省略時は Blender の既定。編集モードは近めにすると選びやすい）
//   scene  Unity の場合は { instances: ["Avatar"] }（最初に置く Prefab）
//   Unity の highlight は { hierarchy: 名前, project: アセット, inspector: "active" | "tag" | "add-component" | "material-slot-0" | "p-0",
//                          menu: "VRChat SDK", popup: "setup-outfit" | "sdk-panel", sdk: "build-test", tool: "move", scene: true, label }
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
  const hl = (extra = {}) => (target ? { object: target, ...extra } : extra);
  const name = OP_NAME[key];
  const A = axis?.toUpperCase();
  const interrupted = e =>
    e.type === "cancel" ? "キャンセルされました。" :
    e.type === "confirm" ? "途中で確定されました。" : undefined;
  const steps = [{
    group, keys: [key], highlight: hl(),
    text: first ?? `<kbd>${key}</kbd> を押して${name}を始めてください。`,
    done: e => e.type === "opstart" && e.kind === key,
    hint: e => e.type === "opstart" ? `${OP_NAME[e.kind]}が始まりました。<kbd>Esc</kbd> でキャンセルしてから <kbd>${key}</kbd> を押してください。` : undefined,
    skip: vp => vp.startOp(key),
  }];
  if (axis) steps.push({
    group, keys: [A], highlight: hl({ axis }),
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
    group, keys: [...value], highlight: hl({ axis }),
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
    group, keys: ["Enter"], highlight: hl(),
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


// ---------------------------------------------------------------- 編集モード用
const MODE_KEY = { vert: "1", edge: "2", face: "3" };
const MODE_NAME = { vert: "頂点", edge: "辺", face: "面" };

/** Tab で編集モード（on）/ オブジェクトモードへ */
function tabStep(on, text, group) {
  return {
    group, keys: ["Tab"], text,
    done: e => e.type === "mode" && e.mode === (on ? "edit" : "object"),
    hint: e => e.type === "opfail" && e.kind === "Tab" ? "先にオブジェクトをクリックして選択してください。" : undefined,
    skip: vp => vp.toggleEditMode(on),
  };
}

/** 1 / 2 / 3 で選択モード */
function selectModeStep(mode, text, group) {
  return {
    group, keys: [MODE_KEY[mode]], highlight: { tool: mode },
    text: text ?? `<kbd>${MODE_KEY[mode]}</kbd> を押して${MODE_NAME[mode]}選択モードにしてください（上の「${MODE_NAME[mode]}」ボタンでも切り替えられます）。`,
    done: e => e.type === "selectmode" && e.mode === mode,
    hint: e => e.type === "view" && e.action === "axis" ? "「テンキーを模倣」がオンだと、数字キーは視点の切り替えになります。上のボタンで切り替えてください。" : undefined,
    skip: vp => vp.setSelectMode(mode),
  };
}

/** 編集モードの操作（E / I / Ctrl+B）→ 数値 → Enter */
function editOpSteps({ keys, kind, extrude = false, value, unit, name, group, first, start }) {
  const isStart = e => e.type === "opstart" && e.kind === kind && (!extrude || e.extrude);
  const interrupted = e =>
    e.type === "cancel" ? "キャンセルされました。" :
    e.type === "confirm" ? "途中で確定されました。" : undefined;
  return [
    {
      group, keys: [keys], text: first,
      done: isStart,
      hint: e =>
        e.type === "opfail" ? (e.reason === "noselection" ? "先に面や辺を選択してください。" : e.reason === "objectmode" ? "<kbd>Tab</kbd> で編集モードに入ってください。" : e.reason) :
        e.type === "opstart" ? "別の操作が始まりました。<kbd>Esc</kbd> でキャンセルしてください。" : undefined,
      skip: start,
    },
    {
      group, keys: [...value],
      text: `数字で <kbd>${value}</kbd> と入力してください（${name}が ${value}${unit} になります）。`,
      done: e => e.type === "numeric" && e.value === value,
      hint: e => e.type === "numeric" && !value.startsWith(e.value) ? `「${e.value}」になっています。<kbd>Backspace</kbd> で消して ${value} と入力してください。` : undefined,
      fail: interrupted,
      skip: vp => { while (vp.op && vp.op.numeric) vp.typeNumeric("Backspace"); for (const ch of value) vp.typeNumeric(ch); },
    },
    {
      group, keys: ["Enter"], text: "<kbd>Enter</kbd> で確定してください（左クリックでも確定できます）。",
      done: e => e.type === "confirm" && e.kind === kind && e.value === value,
      fail: e => e.type === "cancel" ? "キャンセルされました。" : e.type === "confirm" ? "入力が変わったまま確定されました。" : undefined,
      skip: vp => vp.confirmOp(),
    },
  ];
}

/** ワールド座標の点 at の近くの面・辺をクリックで選ぶ */
function editClickStep({ text, at, label = "クリック", group, done, hint, skip, mouse = "左クリック" }) {
  return { group, text, mouse, highlight: { at, label }, done, hint, skip };
}

/** Ctrl+R → ホイールで本数 → クリック → 位置（右クリックで中央） */
function loopCutSteps({ at, cuts = 1, edge, group = "loopcut", where, center = false }) {
  const steps = [{
    group, keys: ["Ctrl+R"], highlight: { at, label: "ここに置く" },
    text: `<kbd>Ctrl</kbd>+<kbd>R</kbd> を押して、${where}にマウスを置いてください。黄色い線でカットの位置が表示されます。`,
    done: e => e.type === "opstart" && e.kind === "loopcut",
    hint: e => e.type === "opfail" && e.reason === "objectmode" ? "<kbd>Tab</kbd> で編集モードに入ってください。" : undefined,
    skip: vp => vp.previewLoopCut(edge),
  }];
  if (cuts > 1) steps.push({
    group, mouse: "ホイールを回す", highlight: { at, label: "ホイール" },
    text: `黄色い線が出ている状態でホイールを回して、${cuts} 本にしてください。`,
    done: e => e.type === "cuts" && e.cuts === cuts,
    hint: e => e.type === "cuts" ? `今は ${e.cuts} 本です。${e.cuts > cuts ? "逆に回して減らして" : "もう少し回して"}ください。` : undefined,
    fail: e => e.type === "cancel" && e.kind === "loopcut" ? "ループカットをやめました。" : undefined,
    skip: vp => vp.setCuts(cuts),
  });
  steps.push({
    group, mouse: "左クリック", highlight: { at, label: "クリック" },
    text: "黄色い線が出ている状態で <strong>左クリック</strong> して、辺を入れてください。",
    done: e => e.type === "loopcut" && e.cuts === cuts,
    hint: e => e.type === "loopcut" ? `${e.cuts} 本で入りました。` : undefined,
    fail: e => e.type === "cancel" && e.kind === "loopcut" ? "ループカットをやめました。" : e.type === "loopcut" && e.cuts !== cuts ? `本数が ${cuts} 本ではありませんでした。` : undefined,
    skip: vp => vp.commitLoopCut(vp.findEdge(edge)),
  });
  steps.push({
    group, mouse: center ? "右クリック" : "動かしてクリック / 右クリック",
    text: center
      ? "続けて位置を決めます。今回は <strong>右クリック</strong>（または <kbd>Esc</kbd>）で、ちょうど真ん中に入れてください。"
      : "マウスを動かすと辺が滑ります。クリックで確定、<strong>右クリック</strong> だとちょうど真ん中に入ります。",
    done: e => e.type === "confirm" && e.kind === "slide" && (!center || e.centered),
    fail: e => e.type === "confirm" && e.kind === "slide" ? "真ん中からずれました。右クリックで真ん中に入れましょう。" : undefined,
    skip: vp => vp.cancelOp(),
  });
  return steps;
}

const isEdit = e => e.type === "select" && e.edit;

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

  // modeling/edit-mode
  "edit-mode": {
    title: "編集モードの選択",
    view: { dist: 9 },
    scene: [{ kind: "sphere", name: "Sphere", s: [2.4, 2.4, 2.4] }],
    steps: [
      clickStep("Sphere", "まず球をクリックして選択してください。"),
      tabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。左上が「編集モード」になり、頂点が点で表示されます。"),
      selectModeStep("edge"),
      {
        group: "loop", mouse: "Alt+左クリック", keys: ["Alt"], highlight: { at: [-1.84, -1.51, 0], label: "Alt+クリック" },
        text: "赤道（真ん中の横の辺）の上で <kbd>Alt</kbd>+クリックして、一周ぶんの辺ループを選んでください。",
        done: e => isEdit(e) && e.source === "loop" && e.counts.edges >= 30,
        hint: e =>
          isEdit(e) && e.source === "loop" ? "縦方向のループになりました。横向きの辺の上で Alt+クリックしてください。" :
          isEdit(e) ? "<kbd>Alt</kbd> を押したままクリックします。" : undefined,
        skip: vp => vp.selectLoopWhere(({ c, d }) => Math.abs(c.z) < 0.01 && Math.abs(d.z) < 0.01),
      },
      ...opSteps({ key: "S", value: "1.2", target: null, group: "loop-scale", first: "選んだループだけを大きくします。<kbd>S</kbd> を押してください。" }),
      selectModeStep("face"),
      {
        keys: ["A"], text: "<kbd>A</kbd> で、すべての面を選択してください。",
        done: e => isEdit(e) && e.source === "all",
        skip: vp => vp.selectAll(true),
      },
      {
        keys: ["Alt+A"], text: "<kbd>Alt</kbd>+<kbd>A</kbd> で、選択を解除してください。",
        done: e => isEdit(e) && e.source === "none",
        skip: vp => vp.selectAll(false),
      },
      {
        keys: ["Alt+Z"], highlight: { tool: "xray" },
        text: "<kbd>Alt</kbd>+<kbd>Z</kbd> で透過表示にしてください。裏側の面も選べるようになります。",
        done: e => e.type === "xray" && e.on,
        skip: vp => vp.setXray(true),
      },
      {
        mouse: "左ドラッグで囲む",
        text: "何もないところから左ドラッグして、球の <strong>上半分</strong> を囲んでください（裏側の面も選ばれます）。",
        done: e => isEdit(e) && e.source === "box" && e.counts.faces >= 100,
        hint: e => isEdit(e) && e.source === "box" ? `選べたのは ${e.counts.faces} 面でした。もっと大きく囲んでください。` : undefined,
        skip: vp => vp.editSelectWhere(({ center }) => center.z > 0),
      },
      {
        keys: ["Alt+Z"], highlight: { tool: "xray" },
        text: "もう一度 <kbd>Alt</kbd>+<kbd>Z</kbd> で、透過表示を元に戻してください。",
        done: e => e.type === "xray" && !e.on,
        skip: vp => vp.setXray(false),
      },
      tabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
    ],
  },

  // modeling/extrude-inset-loopcut
  "extrude-inset-loopcut": {
    title: "押し出し・差し込み・ループカット",
    view: { dist: 7.5, target: [0, 0, 0.4] },
    steps: [
      clickStep("Cube", "立方体をクリックして選択してください。"),
      tabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
      selectModeStep("face"),
      editClickStep({
        group: "top", at: [0, 0, 1], text: "立方体の <strong>上の面</strong> をクリックして、その面だけを選んでください。",
        done: (e, vp) => isEdit(e) && e.counts.faces === 1 && vp.editInfo().faces[0]?.normal.z > 0.9,
        hint: e => isEdit(e) ? "上の面（矢印のところ）だけをクリックしてください。" : undefined,
        skip: vp => vp.editSelectWhere(({ normal }) => normal.z > 0.9),
      }),
      ...editOpSteps({
        keys: "E", kind: "G", extrude: true, value: "1", unit: " m", name: "押し出す長さ", group: "extrude",
        first: "<kbd>E</kbd> を押して押し出してください。面が引き出され、マウスで長さが変わります（面の向きに固定されています）。",
        start: vp => vp.extrude(),
      }),
      ...editOpSteps({
        keys: "I", kind: "I", value: "0.3", unit: " m", name: "差し込みの幅", group: "inset",
        first: "続けて <kbd>I</kbd> を押して、面の内側に面を差し込んでください。マウスを面の中心に近づけるほど幅が広がります。",
        start: vp => vp.inset(),
      }),
      ...editOpSteps({
        keys: "E", kind: "G", extrude: true, value: "-0.5", unit: " m", name: "押し出す長さ", group: "dent",
        first: "内側の面を <kbd>E</kbd> で押し出し、マイナスの値で <strong>へこませ</strong> ます。<kbd>E</kbd> を押してください。",
        start: vp => vp.extrude(),
      }),
      ...loopCutSteps({
        at: [-1, -1, 0], cuts: 2, where: "手前の左の縦の辺",
        edge: ({ c, d }) => Math.abs(d.z) > 0.9 && c.x < -0.9 && c.y < -0.9 && Math.abs(c.z) < 0.1,
      }),
      tabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
    ],
  },

  // modeling/bevel-knife-merge
  "bevel-merge": {
    title: "ベベル・マージ・面を張る",
    view: { dist: 7 },
    steps: [
      clickStep("Cube", "立方体をクリックして選択してください。"),
      tabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
      selectModeStep("edge"),
      editClickStep({
        group: "edge", at: [0, -1, 1], text: "上の面の <strong>手前の辺</strong> をクリックして、その辺だけを選んでください。",
        done: e => isEdit(e) && e.counts.edges === 1,
        hint: e => isEdit(e) ? "辺を 1 本だけ選びます。矢印の辺をクリックしてください。" : undefined,
        skip: vp => vp.editSelectWhere(({ c }) => c.y < -0.9 && c.z > 0.9),
      }),
      ...editOpSteps({
        keys: "Ctrl+B", kind: "B", value: "0.3", unit: " m", name: "面取りの幅", group: "bevel",
        first: "<kbd>Ctrl</kbd>+<kbd>B</kbd> でベベルを始めてください。マウスを離すほど面取りの幅が広がります。",
        start: vp => vp.bevel(),
      }),
      selectModeStep("face"),
      editClickStep({
        group: "dup", at: [0, 0.2, 1], text: "上の面をクリックして選んでください。",
        done: (e, vp) => isEdit(e) && e.counts.faces === 1 && vp.editInfo().faces[0]?.normal.z > 0.9,
        hint: e => isEdit(e) ? "上の面だけをクリックしてください。" : undefined,
        skip: vp => vp.editSelectWhere(({ normal, center }) => normal.z > 0.9 && center.z > 0.9),
      }),
      {
        group: "dup", keys: ["E"],
        text: "わざと重複した頂点を作ります。<kbd>E</kbd> を押してください。",
        done: e => e.type === "opstart" && e.extrude,
        skip: vp => vp.extrude(),
      },
      {
        group: "dup", keys: ["Esc"],
        text: "すぐに <kbd>Esc</kbd> でキャンセルしてください。見た目は戻りますが、押し出した面は同じ位置に <strong>重なって残って</strong> います。",
        done: e => e.type === "cancel" && e.extrude,
        fail: e => e.type === "confirm" ? "確定されました。もう一度やりましょう。" : undefined,
        skip: vp => vp.cancelOp(),
      },
      selectModeStep("vert"),
      {
        keys: ["A"], text: "<kbd>A</kbd> ですべての頂点を選択してください。",
        done: e => isEdit(e) && e.source === "all",
        skip: vp => vp.selectAll(true),
      },
      {
        group: "merge", keys: ["M"], text: "<kbd>M</kbd> でマージのメニューを出してください。",
        done: e => e.type === "menu" && e.menu === "merge",
        skip: vp => vp.showMergeMenu(),
      },
      {
        group: "merge", mouse: "メニューをクリック",
        text: "「距離で」をクリックしてください。重なった頂点がまとめられ、下に消えた数が出ます。",
        done: e => e.type === "merge" && e.how === "distance" && e.removed > 0,
        fail: e => e.type === "merge" && e.how !== "distance" ? "「距離で」以外を選びました。" : undefined,
        hint: e => e.type === "merge" ? "重なった頂点がありませんでした。" : undefined,
        skip: vp => { vp.closeMenu(); vp.mergeEdit("distance"); },
      },
      selectModeStep("face"),
      editClickStep({
        group: "hole", at: [0, 0.2, 1], text: "もう一度、上の面をクリックして選んでください。",
        done: (e, vp) => isEdit(e) && e.counts.faces === 1 && vp.editInfo().faces[0]?.normal.z > 0.9,
        hint: e => isEdit(e) ? "上の面だけをクリックしてください。" : undefined,
        skip: vp => vp.editSelectWhere(({ normal, center }) => normal.z > 0.9 && center.z > 0.9),
      }),
      {
        group: "hole", keys: ["X"], text: "<kbd>X</kbd> で削除のメニューを出してください。",
        done: e => e.type === "menu" && e.menu === "delete",
        skip: vp => vp.showDeleteMenu(),
      },
      {
        group: "hole", mouse: "メニューをクリック", text: "「面」をクリックして、上の面を削除してください。穴が開きます。",
        done: e => e.type === "delete" && e.edit && e.what === "face",
        fail: e => e.type === "delete" ? "「面」以外で削除しました。" : undefined,
        skip: vp => { vp.closeMenu(); vp.deleteEdit("face"); },
      },
      selectModeStep("edge"),
      {
        group: "fill", mouse: "Alt+左クリック", keys: ["Alt"], highlight: { at: [0, -0.7, 1], label: "Alt+クリック" },
        text: "穴のふちの辺を <kbd>Alt</kbd>+クリックして、ふちを一周選んでください。",
        done: e => isEdit(e) && e.source === "loop" && e.counts.edges === 4,
        hint: e => isEdit(e) ? "穴のふち（矢印の辺）を Alt+クリックしてください。" : undefined,
        skip: vp => vp.selectLoopWhere(({ c, d }) => c.z > 0.99 && c.y < -0.6 && Math.abs(d.x) > 0.9),
      },
      {
        group: "fill", keys: ["F"], text: "<kbd>F</kbd> で穴に面を張ってください。",
        done: e => e.type === "fill" && e.made === "face",
        skip: vp => vp.fillEdit(),
      },
      tabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
    ],
  },

  // modeling/mirror-smooth
  mirror: {
    title: "半分を消してミラーで作る",
    view: { dist: 7 },
    numpad: true,
    steps: [
      clickStep("Cube", "立方体をクリックして選択してください。"),
      tabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。"),
      ...loopCutSteps({
        at: [0.4, -1, 1], center: true, where: "上の面の手前の辺（左右にのびる辺）",
        edge: ({ c, d }) => Math.abs(d.x) > 0.9 && c.y < -0.9 && c.z > 0.9,
      }),
      selectModeStep("face"),
      numpadStep("1", false, "前", "<kbd>Numpad 1</kbd> で正面から見てください（テンキーがなければ下の「テンキーを模倣」をオンにして <kbd>1</kbd>）。"),
      {
        keys: ["Alt+Z"], highlight: { tool: "xray" },
        text: "<kbd>Alt</kbd>+<kbd>Z</kbd> で透過表示にしてください。裏側の面もまとめて囲めるようになります。",
        done: e => e.type === "xray" && e.on,
        skip: vp => vp.setXray(true),
      },
      {
        group: "half", mouse: "左ドラッグで囲む",
        text: "真ん中の線より <strong>左側</strong>（X がマイナス側）の面を、ドラッグで囲んで選んでください。",
        done: (e, vp) => isEdit(e) && e.source === "box" && e.counts.faces === 5 && vp.editInfo().faces.every(f => f.center.x < 0),
        hint: (e, vp) => isEdit(e) && e.source === "box"
          ? (vp.editInfo().faces.some(f => f.center.x > 0) ? "右側の面まで入っています。真ん中の線より左だけを囲んでください。" : `選べたのは ${e.counts.faces} 面でした（左半分は 5 面）。上下にも大きめに囲んでください。`)
          : undefined,
        skip: vp => vp.editSelectWhere(({ center }) => center.x < -0.01),
      },
      {
        group: "half", keys: ["X"], text: "<kbd>X</kbd> で削除のメニューを出してください。",
        done: e => e.type === "menu" && e.menu === "delete",
        skip: vp => vp.showDeleteMenu(),
      },
      {
        group: "half", mouse: "メニューをクリック", text: "「面」をクリックして、左半分を削除してください。",
        done: (e, vp) => e.type === "delete" && e.edit && e.what === "face" && vp.editInfo().verts.every(p => p.x > -0.001),
        fail: e => e.type === "delete" ? "左半分がきれいに消えませんでした。" : undefined,
        skip: vp => { vp.closeMenu(); vp.deleteEdit("face"); },
      },
      tabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
      {
        highlight: { tool: "mirror" }, mouse: "上の「ミラー」をクリック",
        text: "上の「ミラー」ボタンを押して、ミラー モディファイアーを付けてください（Blender ではプロパティのモディファイアーから追加します）。左半分が鏡写しで表示されます。",
        done: e => e.type === "mirror" && e.on,
        skip: vp => vp.setMirror(true),
      },
    ],
  },

  // modeling/topology
  topology: {
    title: "統計を見ながら辺を足す・溶解する",
    view: { dist: 7 },
    steps: [
      clickStep("Cube", "立方体をクリックして選択してください。"),
      {
        highlight: { tool: "stats" }, mouse: "上の「統計」をクリック",
        text: "上の「統計」ボタンを押して、頂点・辺・面・三角形の数を表示してください（Blender ではオーバーレイ > 統計）。",
        done: e => e.type === "stats" && e.on,
        skip: vp => vp.setStats(true),
      },
      tabStep(true, "<kbd>Tab</kbd> で編集モードに入ってください。統計が「選択 / 全体」の表示になります。"),
      ...loopCutSteps({
        at: [-1, -1, 0], center: true, where: "手前の左の縦の辺",
        edge: ({ c, d }) => Math.abs(d.z) > 0.9 && c.x < -0.9 && c.y < -0.9 && Math.abs(c.z) < 0.1,
      }),
      selectModeStep("edge", "<kbd>2</kbd> で辺選択モードにしてください。面が 6 → 10 に増えたことを統計で確かめましょう。"),
      {
        group: "dissolve", mouse: "Alt+左クリック", keys: ["Alt"], highlight: { at: [0, -1, 0], label: "Alt+クリック" },
        text: "今入れた辺（手前の真ん中の横の辺）を <kbd>Alt</kbd>+クリックして、ループを一周選んでください。",
        done: e => isEdit(e) && e.source === "loop" && e.counts.edges === 4,
        hint: e => isEdit(e) ? "真ん中の横の辺（矢印）を Alt+クリックしてください。" : undefined,
        skip: vp => vp.selectLoopWhere(({ c, d }) => Math.abs(c.z) < 0.01 && c.y < -0.9 && Math.abs(d.x) > 0.9),
      },
      {
        group: "dissolve", keys: ["X"], text: "<kbd>X</kbd> で削除のメニューを出してください。",
        done: e => e.type === "menu" && e.menu === "delete",
        skip: vp => vp.showDeleteMenu(),
      },
      {
        group: "dissolve", mouse: "メニューをクリック",
        text: "「辺を溶解」をクリックしてください。穴を開けずに辺だけが消え、面の数が元の 6 に戻ります。",
        done: e => e.type === "delete" && e.edit && e.what === "dissolve",
        fail: e => e.type === "delete" ? "「辺を溶解」以外で削除したので、穴が開きました。" : undefined,
        skip: vp => { vp.closeMenu(); vp.deleteEdit("dissolve"); },
      },
      tabStep(false, "<kbd>Tab</kbd> でオブジェクトモードに戻ってください。"),
    ],
  },

  // ---------------------------------------------------------------- Unity（engine: "unity"）
  // unity/editor-overview
  "unity-basics": {
    title: "Unity の画面を動かしてみる",
    engine: "unity",
    scene: { instances: ["Avatar"] },
    steps: [
      {
        mouse: "▶ をクリック", highlight: { hierarchy: "Avatar", label: "▶ をクリック" },
        text: "Hierarchy の「Avatar」の左にある ▶ をクリックして、中身を開いてください。",
        done: e => e.type === "expand" && e.name === "Avatar" && e.expanded,
        skip: ed => ed.expandByName("Avatar"),
      },
      {
        mouse: "左クリック", highlight: { hierarchy: "Body", label: "クリック" },
        text: "「Body」をクリックして選んでください。右の Inspector に、そのオブジェクトの設定（コンポーネント）が表示されます。",
        done: e => e.type === "select" && e.name === "Body",
        hint: e => e.type === "select" ? "Avatar の中の「Body」をクリックしてください。" : undefined,
        skip: ed => ed.selectByName("Avatar/Body"),
      },
      {
        keys: ["F"], highlight: { scene: true, label: "ここにマウスを置いて F" },
        text: "Scene ビューにマウスを置いて <kbd>F</kbd> を押すと、選んだものが画面の中心に大きく表示されます（Frame Selected）。",
        done: e => e.type === "view" && e.action === "frame",
        skip: ed => ed.frameSelected(),
      },
      {
        keys: ["Alt"], mouse: "Alt+左ドラッグ",
        text: "<kbd>Alt</kbd> を押しながら左ドラッグで、視点を回してください（Blender のホイールボタンのドラッグにあたります）。",
        done: e => e.type === "view" && e.action === "orbit",
        skip: ed => ed.orbitBy(0.6),
      },
      {
        mouse: "ホイールを回す",
        text: "ホイールを回して、近づいたり離れたりしてください。",
        done: (e, ed, ctx) => e.type === "view" && e.action === "zoom" && (ctx.n = (ctx.n ?? 0) + 1) >= 2,
        skip: ed => { ed.view.dist *= 0.8; },
      },
      {
        mouse: "ホイールボタンでドラッグ",
        text: "ホイールボタン（中ボタン）を押したままドラッグして、視点を平行移動してください。",
        done: e => e.type === "view" && e.action === "pan",
        skip: ed => { ed.view.target.x += 0.2; },
      },
      {
        mouse: "左クリック", highlight: { hierarchy: "Avatar", label: "クリック" },
        text: "Hierarchy で「Avatar」をクリックして選んでください。",
        done: e => e.type === "select" && e.name === "Avatar",
        skip: ed => ed.selectByName("Avatar"),
      },
      {
        keys: ["W"], highlight: { tool: "move" },
        text: "<kbd>W</kbd> で移動ツールにしてください。Scene に赤・緑・青の矢印（X / Y / Z）が出ます。",
        done: e => e.type === "tool" && e.tool === "move",
        skip: ed => ed.setTool("move"),
      },
      {
        mouse: "赤い矢印をドラッグ", highlight: { scene: true, label: "赤い矢印をドラッグ" },
        text: "赤い矢印をドラッグして、アバターを横（X 方向）に動かしてください。",
        done: e => e.type === "transform" && e.kind === "move",
        hint: e => e.type === "select" && !e.name ? "矢印の先の三角の部分をつかんでドラッグしてください。" : undefined,
        skip: ed => { const n = ed.findByName("Avatar"); ed.setTransformValue(n.id, "p", 0, 0.5); },
      },
      {
        highlight: { inspector: "p-0", label: "0 と入力して Enter" },
        text: "Inspector の Transform の <strong>Position の X</strong> に <kbd>0</kbd> と入力して <kbd>Enter</kbd>。元の位置に戻ります。数値で正確に置けるのは Blender のサイドバー（<kbd>N</kbd>）と同じです。",
        done: e => e.type === "edit" && e.prop === "p" && e.axis === 0 && e.value === 0,
        hint: e => e.type === "edit" ? "Position の X（いちばん左の欄）に 0 を入れてください。" : undefined,
        skip: ed => { const n = ed.findByName("Avatar"); ed.setTransformValue(n.id, "p", 0, 0); },
      },
      {
        keys: ["E"], highlight: { tool: "rotate" },
        text: "<kbd>E</kbd> で回転ツールにしてください（輪をドラッグで回転）。",
        done: e => e.type === "tool" && e.tool === "rotate",
        skip: ed => ed.setTool("rotate"),
      },
      {
        keys: ["R"], highlight: { tool: "scale" },
        text: "<kbd>R</kbd> で拡大縮小ツールにしてください（Blender の S にあたります）。",
        done: e => e.type === "tool" && e.tool === "scale",
        skip: ed => ed.setTool("scale"),
      },
      {
        keys: ["Q"], highlight: { tool: "hand" },
        text: "<kbd>Q</kbd> で手のツールにしてください。左ドラッグで視点を平行移動できます。",
        done: e => e.type === "tool" && e.tool === "hand",
        skip: ed => ed.setTool("hand"),
      },
    ],
  },

  // unity/modular-avatar-outfit
  "ma-outfit": {
    title: "Modular Avatar で衣装を着せる",
    engine: "unity",
    scene: { instances: ["Avatar"] },
    steps: [
      {
        group: "put", mouse: "Avatar の上へドラッグ", highlight: { project: "Hoodie", label: "Hierarchy の Avatar へドラッグ" },
        text: "下の Project ウィンドウの衣装「Hoodie」を、Hierarchy の「Avatar」の <strong>上</strong> までドラッグして離してください。衣装がアバターの中（子）に入ります。",
        done: e => (e.type === "instantiate" && e.asset === "Hoodie" && e.parentName === "Avatar") || (e.type === "reparent" && e.name === "Hoodie" && e.parentName === "Avatar"),
        hint: e =>
          e.type === "instantiate" && !e.parentName ? "いちばん外（ルート）に置かれました。Hierarchy の Hoodie を、Avatar の上にドラッグして入れてください。" :
          e.type === "instantiate" ? `「${e.parentName}」の中に入りました。Ctrl+Z で戻して、Avatar の上で離してください。` : undefined,
        skip: ed => ed.instantiate("Hoodie", ed.findByName("Avatar").id),
      },
      {
        group: "setup", mouse: "右クリック", highlight: { hierarchy: "Hoodie", label: "右クリック" },
        text: "Hierarchy の「Hoodie」を <strong>右クリック</strong> してください。",
        done: e => e.type === "contextmenu" && e.name === "Hoodie",
        hint: e => e.type === "contextmenu" ? "「Hoodie」（衣装のいちばん上）で右クリックしてください。" : undefined,
        skip: ed => ed.contextMenuByName("Avatar/Hoodie"),
      },
      {
        group: "setup", mouse: "メニューをクリック", highlight: { popup: "setup-outfit", label: "クリック" },
        text: "メニューの <strong>Modular Avatar ▸ Setup Outfit</strong> を選んでください。衣装のアーマチュアに <strong>MA Merge Armature</strong> が付き、アップロード時にアバターのボーンへ統合されます。",
        done: e => e.type === "setupOutfit" && e.ok,
        hint: e => e.type === "setupOutfit" ? e.reason : undefined,
        fail: e => e.type === "select" ? "メニューが閉じました。" : undefined,
        skip: ed => ed.setupOutfit(ed.findByName("Avatar/Hoodie").id),
      },
      {
        mouse: "左クリック", highlight: { hierarchy: "Shirt_Default", label: "クリック" },
        text: "次は、アバターが最初から着ている服を外します。Hierarchy の「Avatar」の中の <strong>Shirt_Default</strong> を選んでください。",
        done: e => e.type === "select" && e.name === "Shirt_Default",
        hint: e => e.type === "select" ? "Avatar の ▶ を開いて、「Shirt_Default」を選んでください。" : undefined,
        skip: ed => ed.selectByName("Avatar/Shirt_Default"),
      },
      {
        mouse: "チェックを外す", highlight: { inspector: "active", label: "チェックを外す" },
        text: "Inspector の左上のチェックを外して、Shirt_Default を無効にしてください。Scene から消えます。",
        done: e => e.type === "active" && e.name === "Shirt_Default" && !e.active,
        skip: ed => ed.setActive(ed.findByName("Avatar/Shirt_Default").id, false),
      },
      {
        mouse: "Tag を選ぶ", highlight: { inspector: "tag", label: "EditorOnly を選ぶ" },
        text: "無効にしただけでは、アップロードに含まれて重さ（ランク）にも数えられます。<strong>Tag</strong> を <strong>EditorOnly</strong> にして、アップロードから外してください。",
        done: e => e.type === "tag" && e.name === "Shirt_Default" && e.tag === "EditorOnly",
        hint: e => e.type === "tag" ? "Tag の一覧から EditorOnly を選んでください。" : undefined,
        skip: ed => ed.setTag(ed.findByName("Avatar/Shirt_Default").id, "EditorOnly"),
      },
      {
        mouse: "左クリック", highlight: { hierarchy: "Hoodie", label: "▶ を開いて Hoodie_Mesh を選ぶ" },
        text: "衣装の見た目（マテリアル）を変えます。Hoodie の ▶ を開いて <strong>Hoodie_Mesh</strong> を選んでください（Scene でパーカーをクリックしても選べます）。",
        done: e => e.type === "select" && e.name === "Hoodie_Mesh",
        hint: e => e.type === "select" ? "Hoodie の中の「Hoodie_Mesh」を選んでください。" : undefined,
        skip: ed => ed.selectByName("Hoodie/Hoodie_Mesh"),
      },
      {
        mouse: "Element 0 へドラッグ", highlight: { project: "Hoodie_lilToon", label: "Inspector の Element 0 へドラッグ" },
        text: "Project の <strong>Hoodie_lilToon</strong> を、Inspector の Skinned Mesh Renderer の <strong>Materials &gt; Element 0</strong> の欄までドラッグしてください。",
        done: e => e.type === "material" && e.name === "Hoodie_Mesh" && /lilToon|Red/.test(e.material),
        hint: e => e.type === "material" ? "Hoodie_Mesh を選んだ状態で、Element 0 の欄で離してください。" : undefined,
        skip: ed => ed.assignMaterial(ed.findByName("Hoodie/Hoodie_Mesh").id, 0, "Hoodie_lilToon"),
      },
      {
        mouse: "Add Component", highlight: { inspector: "add-component", label: "クリックして blend と入力" },
        text: "体型の変更（ブレンドシェイプ）に服を合わせます。Inspector の <strong>Add Component</strong> を押し、<kbd>blend</kbd> と入力して <strong>MA Blendshape Sync</strong> を選んでください。",
        done: e => e.type === "componentAdded" && e.component === "MABlendshapeSync",
        hint: e => e.type === "componentAdded" ? "MA Blendshape Sync を追加してください。" : undefined,
        skip: ed => ed.addComponent(ed.findByName("Hoodie/Hoodie_Mesh").id, "MABlendshapeSync"),
      },
      {
        group: "sdk", mouse: "メニューをクリック", highlight: { menu: "VRChat SDK", label: "クリック" },
        text: "上のメニューの <strong>VRChat SDK</strong> を開いてください。",
        done: e => e.type === "menu" && e.menu === "VRChat SDK",
        skip: ed => { const r = ed.root.querySelector('[data-menu="VRChat SDK"]').getBoundingClientRect(); ed.openMenu("VRChat SDK", r.left, r.bottom); },
      },
      {
        group: "sdk", mouse: "クリック", highlight: { popup: "sdk-panel", label: "クリック" },
        text: "<strong>Show Control Panel</strong> を選ぶと、アップロード用の画面（Builder）が開きます。パフォーマンスランクと、確認のメッセージを見てください。",
        done: e => e.type === "sdk" && e.open,
        skip: ed => ed.openSDK(),
      },
      {
        mouse: "クリック", highlight: { sdk: "build-test", label: "クリック" },
        text: "<strong>Build &amp; Test</strong> を押して、自分の PC の VRChat だけで試します（練習なので実際には送信しません）。腕を振ったとき、衣装がついてくれば成功です。",
        done: e => e.type === "build" && e.kind === "test",
        skip: ed => ed.build("test"),
      },
    ],
  },
};
