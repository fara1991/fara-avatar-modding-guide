// 三角関数の 1 ulp の誤差に強い列だけを残す(practice-cases.generate.mjs と practice-edit-cases.generate.mjs が使う)。
//
// 現行の JS(V8)と C#(.NET)と CI の環境(Node 22 / .NET の libm)で、Math.sin / cos / tan / atan2 / asin の最後の 1 桁が違うことがある(実測: sin / cos で 2〜3%)。
// 本物の入力にはどうでもよい差だが、軸にそろえた平行投影の隠れ判定・ぴったり重なった頂点のどちらが選ばれるか・ほぼ平行な面の向き・回転の中心に重なったマウスのように、
// 丸めの誤差で結果が決まる状態は、突き合わせの入力にすると環境によって落ちる。そこで、同じ手順を「三角関数の結果を 1 ulp ずらした JS」でも動かして、
// 結果(スナップショット)が変わった最初の手順で列を打ち切る(その手順は誤差に弱い)。ずらし方を変えて 8 回確かめる。最後の確認(probes)も、変わるものは外す。
import { createHarness, applyStep, snapshot as takeSnapshot, probe } from "./practice-reference.mjs";
import { withTrigNoise } from "./practice-noise.mjs";

export function firstDiff(a, b, p = "$") {
  if (a === b) return null;
  if (a == null && b == null) return null;
  if (typeof a === "number" && typeof b === "number" && Number.isNaN(a) && Number.isNaN(b)) return null;
  if (typeof a === "number" && typeof b === "number") return Math.abs(a - b) <= 1e-9 * Math.max(1, Math.abs(a), Math.abs(b)) ? null : `${p}: ${a} / ${b}`;
  if (typeof a === "string" && typeof b === "string") {
    const norm = x => x.replace(/-(0\.0+)(?![0-9])/g, "$1");
    return /\.(status|value)$/.test(p) && norm(a) === norm(b) ? null : `${p}: ${a} / ${b}`;
  }
  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length !== b.length) return `${p}: 長さ ${a.length} / ${b.length}`;
    for (let i = 0; i < a.length; i++) { const d = firstDiff(a[i], b[i], `${p}[${i}]`); if (d) return d; }
    return null;
  }
  if (a && b && typeof a === "object" && typeof b === "object" && !Array.isArray(a) && !Array.isArray(b)) {
    for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) { const d = firstDiff(a[k], b[k], `${p}.${k}`); if (d) return d; }
    return null;
  }
  return `${p}: ${JSON.stringify(a)} / ${JSON.stringify(b)}`;
}

const SEEDS = [11, 22, 33, 44, 55, 66, 77, 88];
const PERCENT = 25; // 実際の違いは 2〜3% だが、強めにして見落としを減らす

function run(c, steps) {
  const h = createHarness({ ...c, steps: [] });
  h.fullMesh = true;
  const out = [takeSnapshot(h)];
  for (const s of steps) { const prevented = applyStep(h, s); out.push(takeSnapshot(h, { prevented })); }
  return { out, h };
}

/** 誤差に弱くならずに最後まで進める手順の数(全部なら steps.length) */
function robustLength(c) {
  const normal = run(c, c.steps).out;
  let safe = c.steps.length, why = "";
  for (const seed of SEEDS) {
    let noisy = null;
    try { noisy = withTrigNoise(seed, PERCENT, () => run(c, c.steps.slice(0, safe)).out); } catch (err) { why = `例外 ${String(err.message).split("\n")[0]}`; }
    if (noisy === null) {
      // 例外が出たら、1 手順ずつ確かめて、出る直前までを安全とする
      for (let k = 1; k <= safe; k++) {
        let ok = true;
        try { withTrigNoise(seed, PERCENT, () => run(c, c.steps.slice(0, k))); } catch { ok = false; }
        if (!ok) { safe = k - 1; break; }
      }
      continue;
    }
    for (let k = 0; k < noisy.length; k++) {
      const d = firstDiff(normal[k], noisy[k]);
      if (d) { safe = Math.min(safe, k - 1); why = `手順 ${k}: ${d.slice(0, 120)}`; break; }
    }
  }
  return { safe: Math.max(safe, 0), why };
}

/** 最後の確認(probes)のうち、誤差で結果が変わるものを外す */
function stableProbes(c) {
  if (!c.probes?.length) return c.probes ?? [];
  const normal = run(c, c.steps).h;
  const base = c.probes.map(p => probe(normal.vp, p));
  const keep = c.probes.map(() => true);
  for (const seed of SEEDS) {
    let got;
    try { got = withTrigNoise(seed, PERCENT, () => { const h = run(c, c.steps).h; return c.probes.map(p => probe(h.vp, p)); }); } catch { keep.fill(false); continue; }
    got.forEach((g, i) => { if (firstDiff(base[i], g)) keep[i] = false; });
  }
  return c.probes.filter((_, i) => keep[i]);
}

/** scenarios(各 { name, steps, probes, ... })を、誤差に強い長さに切り詰める。切り詰めた一覧を標準出力に出す。 */
export function makeRobust(scenarios) {
  const truncated = [];
  let droppedProbes = 0;
  for (const c of scenarios) {
    const { safe, why } = robustLength(c);
    if (safe < c.steps.length) { truncated.push(`${c.name}: ${c.steps.length} → ${safe} 手順(${why})`); c.steps.length = safe; }
    const probes = stableProbes(c);
    droppedProbes += (c.probes?.length ?? 0) - probes.length;
    c.probes = probes;
  }
  if (truncated.length) { console.log(`誤差に弱い手順で打ち切ったシナリオ: ${truncated.length}`); for (const t of truncated) console.log("  切り詰め:", t); }
  if (droppedProbes) console.log(`誤差で変わる最後の確認(probes)を外した数: ${droppedProbes}`);
}
