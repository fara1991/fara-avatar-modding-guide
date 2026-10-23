// 描画の命令(C# の FaraAvatarModdingGuide.Render)の突き合わせ用の「正解」を、現行の viewport.js(three.js)が作るシーンから取り出す。
// 使い方: node practice-render-reference.mjs <cases.json> <out.json> [間隔]
//   cases.json は practice-edit-cases.json / practice-cases.json と同じ入力(S1 の操作列)。practice-reference.mjs の createHarness・applyStep で本物の Viewport に通し、
//   手順 0(最初)・間隔ごと・最後で、three.js のシーンが持つジオメトリ・色・行列・カメラの行列を出す(WebGL は practice-dom.mjs の代用品なので描かない。シーンの中身だけを見る)。
// Float32Array は base64 で出す(出力を小さくするのと、値をそのまま渡すため)。色は three.js が線形で持つ値のまま(C# 側が sRGB から線形に直して比べる)。
import { writeFileSync, readFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";
import { createHarness, applyStep } from "./practice-reference.mjs";
import { pumpFrame } from "./practice-dom.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(here, "../../src/FaraAvatarModdingGuide.Web/wwwroot");

const b64 = arr => (arr ? Buffer.from(new Float32Array(arr).buffer).toString("base64") : null);
const attr = (geo, name) => (geo?.attributes?.[name] ? b64(geo.attributes[name].array) : null);
const mat = m => Array.from(m.elements);
/** 材質と描く順の設定(C# の DrawItem と比べる): 色(sRGB の 16 進)・不透明度・透明・深度・polygonOffset・renderOrder・点の大きさ */
const props = (object, m) => ({
  color: m.color.getHex(), opacity: m.opacity, transparent: m.transparent, depthTest: m.depthTest, depthWrite: m.depthWrite,
  polygonOffset: m.polygonOffset, factor: m.polygonOffsetFactor, units: m.polygonOffsetUnits, renderOrder: object.renderOrder,
  size: m.isPointsMaterial ? m.size : null, sizeAttenuation: m.isPointsMaterial ? m.sizeAttenuation : null, vertexColors: !!m.vertexColors, side: m.side,
});

/** 方眼・軸・3D カーソル(最初から置くもの。ケースによらない) */
function staticShot(vp) {
  const kids = vp.scene.children;
  const grid = kids.find(o => o.type === "GridHelper");
  const lines = kids.filter(o => o.type === "Line" && o !== vp.axisLine && o !== vp.guideLine);
  const ring = vp.cursor;
  const index = ring.geometry.index.array;
  const rp = ring.geometry.attributes.position.array;
  const expanded = [];
  for (const i of index) expanded.push(rp[i * 3], rp[i * 3 + 1], rp[i * 3 + 2]);
  return {
    grid: { pos: attr(grid.geometry, "position"), col: attr(grid.geometry, "color"), world: mat(grid.matrixWorld), props: props(grid, grid.material) },
    axes: lines.map(l => ({ pos: attr(l.geometry, "position"), world: mat(l.matrixWorld), color: l.material.color.getHex(), props: props(l, l.material) })),
    ringProps: props(ring, ring.material),
    axisLineProps: props(vp.axisLine, vp.axisLine.material),
    ring: b64(expanded),
    axisLinePos: attr(vp.axisLine.geometry, "position"),
  };
}

function shot(h, step) {
  const { vp } = h;
  pumpFrame(vp);
  const cam = vp.camera;
  const op = vp.op;
  return {
    step,
    camera: { view: mat(cam.matrixWorldInverse), proj: mat(cam.projectionMatrix), ortho: vp.ortho },
    objects: vp.objects.map(o => {
      const outline = o.getObjectByName("outline");
      const mirror = o.getObjectByName("mirror");
      const overlay = o.getObjectByName("edit-overlay");
      let ov = null;
      if (overlay) {
        const [lines, points, faces] = overlay.children;
        ov = {
          linesPos: attr(lines.geometry, "position"), linesCol: attr(lines.geometry, "color"),
          pointsPos: attr(points.geometry, "position"), pointsCol: attr(points.geometry, "color"), pointsVisible: points.visible,
          facesPos: attr(faces.geometry, "position"),
          depthTest: lines.material.depthTest, pointsDepthTest: points.material.depthTest, facesDepthTest: faces.material.depthTest,
          linesProps: props(lines, lines.material), pointsProps: props(points, points.material), facesProps: props(faces, faces.material),
        };
      }
      return {
        name: o.name, world: mat(o.matrixWorld),
        meshPos: attr(o.geometry, "position"), meshNor: attr(o.geometry, "normal"),
        outlinePos: attr(outline.geometry, "position"), outlineVisible: outline.visible, outlineColor: outline.material.color.getHex(),
        mirrorVisible: mirror.visible, mirrorWorld: mat(mirror.matrixWorld),
        meshProps: props(o, o.material), outlineProps: props(outline, outline.material), mirrorProps: props(mirror, mirror.material),
        transparent: o.material.transparent, opacity: o.material.opacity, depthWrite: o.material.depthWrite,
        overlay: ov,
      };
    }),
    axisLine: { props: props(vp.axisLine, vp.axisLine.material), visible: vp.axisLine.visible, world: mat(vp.axisLine.matrixWorld), color: vp.axisLine.material.color.getHex() },
    loopPreview: vp.loopcut?.preview ? attr(vp.loopcut.preview.geometry, "position") : null,
    loopPreviewWorld: vp.loopcut?.preview ? mat(vp.loopcut.preview.matrixWorld) : null,
    loopPreviewProps: vp.loopcut?.preview ? props(vp.loopcut.preview, vp.loopcut.preview.material) : null,
    hasOp: !!op,
  };
}

export function run(cases, every = 80) {
  let statics = null;
  const scenarios = (cases.scenarios ?? []).map(c => {
    const h = createHarness(c);
    statics ??= staticShot(h.vp);
    const total = c.steps.length;
    const shots = [shot(h, 0)];
    c.steps.forEach((s, i) => {
      applyStep(h, s);
      const n = i + 1;
      if (n % every === 0 || n === total) shots.push(shot(h, n));
    });
    return { name: c.name, shots };
  });
  return { statics, scenarios };
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url && process.argv[2] && process.argv[3]) {
  const cases = JSON.parse(readFileSync(process.argv[2], "utf8"));
  writeFileSync(process.argv[3], JSON.stringify(run(cases, Number(process.argv[4] || 80))));
}
