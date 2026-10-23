// 「別の libm」のつもりの、三角関数の 1 ulp の誤差。practice-edit-cases.generate.mjs(誤差に強い手順だけを残す)と、
// practice-reference.mjs(環境変数 PRACTICE_TRIG_NOISE=<シード>:<割合 %> で、ローカルの確認用に JS 側をずらして C# と突き合わせる。CI では使わない)から使う。
//
// V8(Node 24)と .NET と CI(Node 22・Linux)の Math.sin / cos / tan / atan2 / asin は、入力の 2〜3% で最後の 1 桁が違う。
// 本物の入力ではどうでもよい差だが、丸めの誤差で結果が決まる状態(ぴったり重なった頂点のどちらが選ばれるか、ほぼ平行なレイが当たるか、角度が同じ頂点の並べ替えなど)があると、
// 環境によって突き合わせが落ちる。同じ入力には必ず同じ結果を返す(入力のビット列・関数名・シードで決まる)ずらし方なので、「別の libm」として働く。

const f64 = new Float64Array(1), u64 = new BigUint64Array(f64.buffer), words = new Uint32Array(f64.buffer);

/** y を隣の double にする(up なら大きいほう)。 */
export function nudge(y, up) {
  if (!Number.isFinite(y) || y === 0) return y;
  f64[0] = y;
  u64[0] += (y > 0) === up ? 1n : -1n;
  return f64[0];
}

/** fn を動かすあいだ、Math の三角関数を percent % の入力で 1 ulp ずらす。 */
export function withTrigNoise(seed, percent, fn) {
  const names = ["sin", "cos", "tan", "atan2", "asin", "acos", "atan"];
  const saved = Object.fromEntries(names.map(n => [n, Math[n]]));
  const hash = (name, args) => {
    let h = (seed * 2654435761 + name.length * 40503 + name.charCodeAt(0) * 7919) >>> 0;
    for (const a of args) {
      f64[0] = a;
      h = Math.imul(h ^ words[0], 2246822519) >>> 0;
      h = Math.imul(h ^ (h >>> 15) ^ words[1], 3266489917) >>> 0;
      h ^= h >>> 13;
    }
    return h >>> 0;
  };
  for (const n of names) {
    Math[n] = (...args) => {
      const y = saved[n](...args);
      const h = hash(n, args);
      return h % 100 < percent ? nudge(y, (h >>> 8) % 2 === 0) : y;
    };
  }
  try { return fn(); } finally { for (const n of names) Math[n] = saved[n]; }
}
