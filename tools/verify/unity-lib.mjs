// Unity 風の画面の比較(unity-compare.mjs・tutorial-input-compare.mjs)の共通部品: ドラッグ・ギズモの位置・状態の読み取りと比べ
import { sleep } from "./tutorial-lib.mjs";

/** 1 回動かして、フレームが 1 回以上回るのを待つ(カメラは毎フレームで更新されるので、動かし方の細かさ(速さ)で、結果が変わらないようにする) */
export const stepTo = async (p, x, y) => { await p.mouse.move(x, y); await sleep(45); };
export const drag = async (p, from, to, n = 4, button = "left") => {
  await p.mouse.move(from[0], from[1]);
  await p.mouse.down({ button });
  for (let i = 1; i <= n; i++) await stepTo(p, from[0] + ((to[0] - from[0]) * i) / n, from[1] + ((to[1] - from[1]) * i) / n);
  await p.mouse.up({ button });
  await sleep(45);
};

/** JS 版のページで、ギズモの矢印・輪の画面上の位置(Scene ビューの左上から)を求める(両方で同じ座標を使う) */
export async function handlePoint(js, index) {
  return js.evaluate((i) => {
    const ed = window.__unity;
    ed.gizmo.updateMatrixWorld(true);
    const hit = ed.handles[i];
    hit.geometry.computeBoundingSphere();
    let c;
    if (hit.userData.handle.kind === "rotate") {
      let best = null;
      for (let k = 0; k < 16; k++) {
        const u = (k / 16) * Math.PI * 2;
        const w = hit.geometry.boundingSphere.center.clone().set(0.9 * Math.cos(u), 0, 0.9 * Math.sin(u)).applyMatrix4(hit.matrixWorld);
        if (!best || w.distanceTo(ed.camera.position) < best.distanceTo(ed.camera.position)) best = w;
      }
      c = best;
    } else c = hit.geometry.boundingSphere.center.clone().applyMatrix4(hit.matrixWorld);
    const v = c.project(ed.camera);
    const r = ed.el.scene.getBoundingClientRect();
    return { x: (v.x + 1) / 2 * r.width, y: (1 - v.y) / 2 * r.height };
  }, index);
}

/** Project のアセット(または Hierarchy の行)を、ほかの要素の上までドラッグして離す */
export async function dragTo(p, fromSel, toSel) {
  const a = await p.locator(fromSel).boundingBox();
  const b = await p.locator(toSel).boundingBox();
  await p.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
  await p.mouse.down();
  await p.mouse.move(a.x + a.width / 2 + 10, a.y + a.height / 2 - 10, { steps: 3 });
  await p.mouse.move(b.x + b.width / 2, b.y + b.height / 2, { steps: 6 });
  await p.mouse.up();
}

/** 状態(視点・カメラの位置・ノードの位置/回転/拡大率・選択・ツール)。JS 版は window.__unity から、C# 版は window.__unityCs(?debug=1)から、同じ形で */
export async function stateOf(p, cs) {
  if (cs) return p.evaluate(() => window.__unityCs);
  return p.evaluate(() => {
    const ed = window.__unity;
    const pathOf = (n) => [...ed.ancestors(n).reverse().map((a) => a.name), n.name].join("/");
    const num = (v) => (Object.is(v, -0) ? 0 : v);
    const vec = (v) => [num(v.x), num(v.y), num(v.z)];
    return {
      view: { target: vec(ed.view.target), dist: ed.view.dist, yaw: ed.view.yaw, pitch: ed.view.pitch }, camera: vec(ed.camera.position),
      selected: ed.get(ed.selected) ? pathOf(ed.get(ed.selected)) : null, tool: ed.tool,
      nodes: [...ed.nodes.values()].map((n) => ({ path: pathOf(n), p: n.p.map(num), r: n.r.map(num), s: n.s.map(num), active: n.active, tag: n.tag })),
    };
  });
}

/** 2 つの状態の違い(数は 1e-9 まで同じとみなす) */
export function stateDiff(a, b, path = "$", out = []) {
  if (out.length >= 6) return out;
  if (typeof a === "number" && typeof b === "number") { if (!(Math.abs(a - b) <= Number(process.env.STATE_TOL ?? 1e-9) * Math.max(1, Math.abs(a), Math.abs(b)))) out.push(`${path}: JS=${a} / C#=${b}`); }
  else if (Array.isArray(a) && Array.isArray(b)) { if (a.length !== b.length) out.push(`${path}: 長さ JS=${a.length} / C#=${b.length}`); else a.forEach((x, i) => stateDiff(x, b[i], `${path}[${i}]`, out)); }
  else if (a && b && typeof a === "object" && typeof b === "object") { for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) stateDiff(a[k], b[k], `${path}.${k}`, out); }
  else if (a !== b) out.push(`${path}: JS=${JSON.stringify(a)} / C#=${JSON.stringify(b)}`);
  return out;
}

