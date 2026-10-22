// ページの最初の読み込み前に差し込む計測用。WebGL の描画を含んだ requestAnimationFrame の回数と時間だけを数える
// （背景の 2D 演出などの rAF は含めない）。firstDraw は最初の drawArrays / drawElements の時刻（navigation start からの ms）
(() => {
  const m = (window.__m = { firstDraw: null, drawFrames: 0, cbMs: 0, cur: 0 });
  const raf = window.requestAnimationFrame.bind(window);
  window.requestAnimationFrame = cb => raf(t => {
    const s = performance.now();
    m.cur = 0;
    try { return cb(t); }
    finally {
      if (m.cur > 0) { m.drawFrames++; m.cbMs += performance.now() - s; }
    }
  });
  for (const P of [globalThis.WebGL2RenderingContext?.prototype, globalThis.WebGLRenderingContext?.prototype]) {
    if (!P) continue;
    for (const fn of ['drawArrays', 'drawElements']) {
      const o = P[fn];
      P[fn] = function (...a) {
        if (m.firstDraw === null) m.firstDraw = performance.now();
        m.cur++;
        return o.apply(this, a);
      };
    }
  }
})();
