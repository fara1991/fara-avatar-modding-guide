// 記事内チュートリアル(C# の FaraAvatarModdingGuide.Practice の Tutorial*)の突き合わせ用の「正解」を、現行の scenarios.js を本物のまま Node で動かして作る(S4)。
// 使い方: node tutorial-reference.mjs <out.json>
//   Blender の 3D ビューのシナリオ 9 本(Unity の 2 本は S5)について、次を出す:
//     definitions … 手順の定義(題・テンキー・視点・最初のシーン、手順ごとの group・text・keys・mouse・highlight・allowOp と、hint / fail / skip の有無)
//     replays     … 最初から i 番目の手順の直前まで再生した(tutorial.js の replayTo と同じ: silent・resetScene・skip を順に)あとの、ビューの状態(practice-reference.mjs の snapshot と同じ形)
//     highlights  … 手順 i の案内(setHighlight)を出したあとの、矢印・点線・ボタンの強調(本物の updateMarker・updateGuide の結果)
//     answers     … 手順 i の直前の状態で、イベントの一覧(events)の 1 つずつを done / fail / hint に渡した答え(ctx は手順ごとに引き継ぐ)
//   events はイベントの一覧(viewport.js の emit が出す形のバリエーション)。C# 側も同じ一覧を、同じ順で通す。
import { writeFileSync } from "node:fs";
import { SCENARIOS } from "../../src/FaraAvatarModdingGuide.Web/wwwroot/js/scenarios.js";
import { createHarness, snapshot } from "./practice-reference.mjs";
import { pumpFrame } from "./practice-dom.mjs";

const BLENDER = Object.entries(SCENARIOS).filter(([, s]) => s.engine !== "unity").map(([name]) => name);

/** イベントの一覧。選択の名前・値・軸・種類の組み合わせを一通り */
export function buildEvents() {
  const ev = [];
  const add = (type, detail = {}) => ev.push({ type, ...detail });
  for (const code of ["KeyA", "Digit1", "Digit3", "Numpad1", "NumpadDecimal", "Tab", "Enter", "Escape", "ShiftLeft", "ControlLeft", "AltLeft", "Home", "KeyG", "KeyX", "Backspace", "Digit5", "Minus", "Period"]) {
    add("key", { code, key: code.startsWith("Key") ? code.slice(3).toLowerCase() : code === "Period" ? "." : code === "Minus" ? "-" : "", shift: false, ctrl: false, alt: false });
  }
  const selections = [[], ["Cube"], ["Cube.001"], ["Cube", "Cube.001"], ["Cube.002", "Cube"], ["Sphere"], ["Cube", "Cube.001", "Cube.002"]];
  for (const source of ["click", "shift-click", "box", "all", "none", "loop", "ring", "linked", "grow", "shrink"]) {
    for (const selected of selections) {
      const active = selected.length ? selected[selected.length - 1] : null;
      add("select", { source, selected, active });
      add("select", { source, selected, active: selected[0] ?? null });
    }
    for (const counts of [{ verts: 0, edges: 0, faces: 0 }, { verts: 4, edges: 1, faces: 0 }, { verts: 8, edges: 4, faces: 1 }, { verts: 30, edges: 30, faces: 0 }, { verts: 60, edges: 100, faces: 100 }, { verts: 99, edges: 190, faces: 99 }, { verts: 10, edges: 12, faces: 5 }, { verts: 4, edges: 4, faces: 1 }]) {
      add("select", { source, selected: ["Cube"], active: "Cube", edit: true, counts, selectMode: "vert" });
    }
  }
  // 距離の合計(ctx)で達成になる手順のために、合計が 100・150 のちょうど手前・ちょうどになる並びにする(orbit 20+60+60+5+5 = 150、pan 30+60+5+5 = 100)
  for (const [action, extra] of [["orbit", { distance: 20 }], ["orbit", { distance: 60 }], ["orbit", { distance: 60 }], ["orbit", { distance: 5 }], ["orbit", { distance: 5 }], ["orbit", { distance: 40 }],
    ["pan", { distance: 30 }], ["pan", { distance: 60 }], ["pan", { distance: 5 }], ["pan", { distance: 5 }], ["pan", { distance: 40 }], ["zoom", { delta: 1 }], ["zoom", { delta: -1 }], ["zoom", { delta: 1 }], ["zoom", { delta: 1 }],
    ["home", {}], ["frame", {}], ["ortho", { ortho: true }], ["ortho", { ortho: false }]]) {
    add("view", { action, name: "ユーザー", ortho: false, ...extra });
  }
  for (const name of ["前", "右", "上", "後", "左", "下", "ユーザー"]) add("view", { action: "axis", name, ortho: true });
  for (const kind of ["G", "R", "S", "I", "B", "slide", "loopcut"]) {
    for (const extrude of [false, true]) add("opstart", { kind, edit: kind !== "G" || extrude, extrude });
  }
  for (const axis of ["x", "y", "z", null, "n"]) for (const exclude of [false, true]) add("axis", { kind: "G", axis, exclude });
  for (const value of ["", "2", "0.5", "45", "4", "1.2", "1", "-0.5", "-", "0.3", "0", "20", "0."]) add("numeric", { kind: "G", value });
  for (const kind of ["G", "R", "S", "I", "B", "slide"]) {
    for (const value of ["2", "45", "0.5", "1.2", "1", "-0.5", "0.3", "", "0", "7"]) {
      for (const axis of ["z", null, "x"]) add("confirm", { kind, axis, exclude: false, value, edit: kind !== "G", extrude: kind === "G" && value === "1", centered: kind === "slide" && value === "0" });
    }
  }
  for (const kind of ["G", "R", "S", "I", "B", "loopcut"]) for (const extrude of [false, true]) add("cancel", { kind, edit: true, extrude });
  for (const reason of ["noselection", "objectmode", "ベベルできません"]) for (const kind of ["G", "E", "Tab", "I", "B", "loopcut"]) add("opfail", { kind, reason });
  for (const mode of ["edit", "object"]) add("mode", { mode, object: "Cube" });
  for (const mode of ["vert", "edge", "face"]) add("selectmode", { mode });
  for (const menu of ["add", "delete", "merge", "separate"]) add("menu", { menu });
  add("add", { kind: "sphere", name: "Sphere" });
  add("add", { kind: "cube", name: "Cube.003" });
  add("duplicate", { names: ["Cube.003"] });
  add("delete", { names: ["Sphere"] });
  for (const what of ["face", "vert", "edge", "dissolve"]) for (const edit of [true, false]) add("delete", { edit, what, names: [] });
  add("undo"); add("redo");
  for (const on of [true, false]) { add("xray", { on }); add("stats", { on }); add("mirror", { on, object: "Cube" }); add("npanel", { open: on }); }
  for (const cuts of [1, 2, 3]) { add("loopcut", { cuts }); add("cuts", { cuts }); }
  for (const how of ["distance", "center", "cursor"]) for (const removed of [0, 3]) add("merge", { how, removed });
  for (const made of ["face", "edge"]) add("fill", { made });
  add("separate", { name: "Cube.004" });
  add("extrude", { type: "region" });
  add("edit", { prop: "position", axis: "x", value: 1 });
  add("inset"); add("bevel");
  return ev;
}

const eventsList = buildEvents();

function caseFor(name, scenario) {
  return {
    name, seed: 1, emulate: false,
    root: { left: 0, top: 0, width: 960, height: 540 },
    objects: scenario.scene ?? [{ kind: "cube" }],
    options: { view: scenario.view, wheelNeedsFocus: true },
    steps: [],
  };
}

/** tutorial.js の replayTo と同じ */
function replayTo(h, scenario, i, keepStatus = false) {
  const vp = h.vp;
  vp.silent = true;
  vp.resetScene(scenario.scene);
  for (let k = 0; k < i && k < scenario.steps.length; k++) scenario.steps[k].skip?.(vp);
  vp.silent = false;
  if (!keepStatus) vp.setStatus("案内にしたがって操作してください");
}

const num = v => (Object.is(v, -0) ? 0 : v);

function highlightState(vp) {
  pumpFrame(vp);
  const m = vp.marker;
  const t = /translate\(([-\d.e]+)px, ([-\d.e]+)px\)/.exec(m.el.style.transform ?? "");
  const guide = vp.guideLine;
  return {
    markerHidden: !!m.el.hidden,
    markerLabel: m.label.textContent,
    markerScreen: t && !m.el.hidden ? [num(Number(t[1])), num(Number(t[2]))] : null,
    guideVisible: !!guide.visible,
    guideWorld: guide.visible ? Array.from(guide.matrixWorld.elements).map(num) : null,
    guideColor: guide.visible ? guide.material.color.getHex() : null,
    hudViewHighlight: vp.hud.view.classList.contains("is-highlight"),
    toolHighlight: [...vp.hud.tools.querySelectorAll("[data-tool]")].filter(b => b.classList.contains("is-highlight")).map(b => b.dataset.tool),
  };
}

export function run() {
  const out = { events: eventsList, scenarios: {} };
  for (const name of BLENDER) {
    const scenario = SCENARIOS[name];
    const def = {
      title: scenario.title, numpad: !!scenario.numpad, view: scenario.view ?? null, scene: scenario.scene ?? null,
      steps: scenario.steps.map(s => ({
        group: s.group ?? null, text: s.text, keys: s.keys ?? [], mouse: s.mouse ?? null, highlight: s.highlight ?? null, allowOp: !!s.allowOp,
        hasHint: !!s.hint, hasFail: !!s.fail, hasSkip: !!s.skip,
      })),
    };
    const n = scenario.steps.length;
    const replays = [], highlights = [], answers = [];
    for (let i = 0; i <= n; i++) {
      const h = createHarness(caseFor(name, scenario));
      h.fullMesh = true;
      replayTo(h, scenario, i);
      replays.push(snapshot(h));
      if (i < n) {
        highlights.push(replayHighlight(h, scenario, i));
        // この手順の直前の状態で、イベントの一覧を通す(ctx は手順ごとに、一覧を通すあいだ引き継ぐ)
        const step = scenario.steps[i];
        const ctx = {};
        const list = [];
        for (const e of eventsList) {
          let done, fail, hint;
          try { done = !!step.done(e, h.vp, ctx); } catch (err) { done = "throw:" + err.message; }
          try { fail = step.fail?.(e, h.vp, ctx); } catch (err) { fail = "throw:" + err.message; }
          try { hint = step.hint?.(e, h.vp, ctx); } catch (err) { hint = "throw:" + err.message; }
          list.push([done, fail ? String(fail) : null, hint ? String(hint) : null]);
        }
        answers.push(list);
      }
    }
    out.scenarios[name] = { definition: def, replays, highlights, answers };
  }
  return out;
}

/** 手順 i の案内を出したあとの状態(renderStep の setHighlight) */
function replayHighlight(h, scenario, i) {
  h.vp.setHighlight(scenario.steps[i].highlight ?? {});
  return highlightState(h.vp);
}

if (process.argv[1]?.endsWith("tutorial-reference.mjs") && process.argv[2]) {
  writeFileSync(process.argv[2], JSON.stringify(run()));
}
