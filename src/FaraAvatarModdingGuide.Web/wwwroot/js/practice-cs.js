// /practice の 3D の C# 版(?engine=cs。Services/PracticeCs.cs)の、JavaScript 側の小さな補助。
//   focusRoot(id)       … ビューにフォーカスを戻す(ボタンを押したあと)
//   exposeDebug / setDebugState / clearDebug … ?debug=1 のとき、window.__practice(JS 版の viewport.js のインスタンスと同じ形の、状態の読み取り口)を作る。
//     tools/verify/practice.mjs が、両方のエンジンで同じ読み方(objects[].userData.kind / position / rotation / scale、mode、ortho、view.name、select(o))をする

export function focusRoot(id) {
  document.getElementById(id)?.focus({ preventScroll: true });
}

let selectByIndex = null;

export function exposeDebug(select) {
  selectByIndex = select;
  window.__practice = {
    objects: [], mode: "object", ortho: false, view: { name: "" },
    select(o) { if (o && typeof o.__index === "number") selectByIndex(o.__index); },
  };
}

export function setDebugState(json) {
  const p = window.__practice;
  if (!p) return;
  const s = JSON.parse(json);
  p.objects = s.objects.map((o, i) => ({
    __index: i, userData: { kind: o.kind },
    position: { x: o.p[0], y: o.p[1], z: o.p[2] }, rotation: { x: o.r[0], y: o.r[1], z: o.r[2] }, scale: { x: o.s[0], y: o.s[1], z: o.s[2] },
  }));
  p.mode = s.mode;
  p.ortho = s.ortho;
  p.view = { name: s.view };
}

export function clearDebug() {
  delete window.__practice;
  selectByIndex = null;
}
