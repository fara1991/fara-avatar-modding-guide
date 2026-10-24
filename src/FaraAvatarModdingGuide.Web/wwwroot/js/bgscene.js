// 背景の飾り（見た目だけ）: Blender の 3D ビューのような、奥へ延びる床の方眼と、宙に散らばってゆっくり回る基本オブジェクト（立方体・球・アイコ球・円錐・円柱・トーラス）。
// - 図形の配置は固定の乱数（シード付き）で決めるので、読み込みのたびに変わらない。画面の中で高さ・奥行きがばらつき、互いに重ならない
// - 回る軸・向き（正逆）・速さ、ゆるやかに漂う向き・幅・周期は、図形ごとにばらばら
// 画面の後ろ（z-index: -1）に置いた canvas に描く。操作には関わらない。
// - 動きを減らす設定（prefers-reduced-motion）のときは、止めた 1 枚だけを描く
// - タブが裏に回ったら止める。負荷を抑えるため、描くのは 30fps まで、解像度の倍率は最大 1.5
// - 色は、ライト・ダークのテーマ（html の data-bs-theme）に合わせる
(function () {
  "use strict";
  if (!document.body || document.getElementById("site-scene")) return;

  var canvas = document.createElement("canvas");
  canvas.id = "site-scene";
  canvas.setAttribute("aria-hidden", "true");
  document.body.insertBefore(canvas, document.body.firstChild);
  var ctx = canvas.getContext("2d");
  if (!ctx) return;

  var reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
  var W = 0, H = 0, dpr = 1;
  var dark = true;

  function readTheme() { dark = document.documentElement.getAttribute("data-bs-theme") !== "light"; }
  function resize() {
    dpr = Math.min(window.devicePixelRatio || 1, 1.5);
    W = window.innerWidth; H = window.innerHeight;
    canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
    canvas.style.width = W + "px"; canvas.style.height = H + "px";
  }

  // ---- 3D（カメラは、床の少し上から、奥を見る） ----
  var cam = { x: 0, y: 1.3, z: 6, pitch: 0.05 };
  var HORIZON = 0.38;   // 画面の高さのうち、地平線の位置（上から）
  function focal(w, h) { return Math.max(w, h * 0.6) * 0.95; }
  function projectWH(p, w, h) {
    var x = p[0] - cam.x, y = p[1] - cam.y, z = p[2] - cam.z;
    var cp = Math.cos(cam.pitch), sp = Math.sin(cam.pitch);
    var y2 = y * cp + z * sp, z2 = -y * sp + z * cp;
    var d = -z2;
    if (d < 0.2) return null;
    // 縦長の画面（スマホ）では、高さではなく幅に合わせる（高さに合わせると、右のオブジェクトが画面の外に出て、端しか見えない）
    var f = focal(w, h);
    return [w * 0.5 + (x * f) / d, h * HORIZON - (y2 * f) / d, d];
  }
  function project(p) { return projectWH(p, W, H); }
  // 画面の位置（u, v は 0〜1）と、カメラからの奥行き d から、世界の座標へ戻す（project の逆）
  function unproject(u, v, d, w, h) {
    var f = focal(w, h), cp = Math.cos(cam.pitch), sp = Math.sin(cam.pitch);
    var y2 = (h * HORIZON - v * h) * d / f;
    return [cam.x + (u * w - w * 0.5) * d / f, cam.y + y2 * cp + d * sp, cam.z + y2 * sp - d * cp];
  }

  function line(a, b, alpha) {
    var p = project(a), q = project(b);
    if (!p || !q) return;
    ctx.globalAlpha = alpha;
    ctx.beginPath(); ctx.moveTo(p[0] * dpr, p[1] * dpr); ctx.lineTo(q[0] * dpr, q[1] * dpr); ctx.stroke();
  }

  // ---- Blender の基本オブジェクト（立方体・UV 球・アイコ球・円錐・円柱・トーラス）を、面ごとの陰影で描く ----
  function cube() {
    var v = [], f = [];
    for (var i = 0; i < 8; i++) v.push([(i & 1) ? 0.5 : -0.5, (i & 2) ? 0.5 : -0.5, (i & 4) ? 0.5 : -0.5]);
    [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]].forEach(function (q) { f.push([q[0], q[1], q[2]], [q[0], q[2], q[3]]); });
    return { v: v, f: f };
  }
  function grid(nu, nv, pos, wrapU, wrapV) {
    // (nu+1)×(nv+1) の点の格子を、三角形にする。pos(i, j) が座標
    var v = [], f = [];
    for (var j = 0; j <= nv; j++) for (var i = 0; i <= nu; i++) v.push(pos(i, j));
    var w = nu + 1;
    for (var j2 = 0; j2 < nv; j2++) for (var i2 = 0; i2 < nu; i2++) {
      var a0 = j2 * w + i2, b0 = a0 + 1, c0 = a0 + w, d0 = c0 + 1;
      f.push([a0, c0, b0], [b0, c0, d0]);
    }
    return { v: v, f: f };
  }
  // 面の向き（巻き順）を逆にする。裏向きの面は描かないので、外向きになっていないと、手前の面が抜けて向こう側の内側が見える
  function flip(m) { m.f = m.f.map(function (t) { return [t[0], t[2], t[1]]; }); return m; }
  function uvSphere(r, seg, rings) {
    return flip(grid(seg, rings, function (i, j) {
      var th = (i / seg) * Math.PI * 2, ph = (j / rings) * Math.PI;
      return [r * Math.sin(ph) * Math.cos(th), r * Math.cos(ph), r * Math.sin(ph) * Math.sin(th)];
    }));
  }
  function icosphere(r) {
    var t = (1 + Math.sqrt(5)) / 2, n = Math.sqrt(1 + t * t);
    var v = [[-1, t, 0], [1, t, 0], [-1, -t, 0], [1, -t, 0], [0, -1, t], [0, 1, t], [0, -1, -t], [0, 1, -t], [t, 0, -1], [t, 0, 1], [-t, 0, -1], [-t, 0, 1]].map(function (p) { return [p[0] / n * r, p[1] / n * r, p[2] / n * r]; });
    var f = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8], [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]];
    return { v: v, f: f };
  }
  function cone(r, h, seg) {
    var m = grid(seg, 1, function (i, j) { var th = (i / seg) * Math.PI * 2; return j === 0 ? [r * Math.cos(th), -h / 2, r * Math.sin(th)] : [0, h / 2, 0]; });
    var c = m.v.length; m.v.push([0, -h / 2, 0]);
    for (var i = 0; i < seg; i++) m.f.push([c, i, i + 1]);
    return m;
  }
  function cylinder(r, h, seg) {
    var m = grid(seg, 1, function (i, j) { var th = (i / seg) * Math.PI * 2; return [r * Math.cos(th), j === 0 ? -h / 2 : h / 2, r * Math.sin(th)]; });
    var bot = m.v.length; m.v.push([0, -h / 2, 0]); var top = m.v.length; m.v.push([0, h / 2, 0]);
    for (var i = 0; i < seg; i++) { m.f.push([bot, i, i + 1]); m.f.push([top, seg + 2 + i, seg + 1 + i]); }
    return m;
  }
  function torus(R, r, seg, tube) {
    return grid(seg, tube, function (i, j) {
      var u = (i / seg) * Math.PI * 2, w = (j / tube) * Math.PI * 2;
      return [(R + r * Math.cos(w)) * Math.cos(u), r * Math.sin(w), (R + r * Math.cos(w)) * Math.sin(u)];
    });
  }

  // 置く物の形と色。br は、形をすっぽり包む球の半径（重ならない距離を測るのに使う）。配置・回り方・漂い方は、下の layout が決める
  var SHAPES = [
    { m: cone(0.5, 1.1, 16), br: 0.62, hue: [168, 85, 247] },
    { m: icosphere(0.62), br: 0.62, hue: [196, 181, 253] },
    { m: cube(), br: 0.87, hue: [124, 58, 237] },
    { m: torus(0.5, 0.2, 18, 10), br: 0.7, hue: [192, 132, 252] },
    { m: uvSphere(0.5, 14, 9), br: 0.5, hue: [147, 114, 255] },
    { m: cylinder(0.38, 1.0, 16), br: 0.55, hue: [168, 85, 247] },
    { m: cube(), br: 0.87, hue: [147, 114, 255] },
    { m: icosphere(0.58), br: 0.58, hue: [196, 181, 253] },
    { m: cone(0.45, 1.0, 16), br: 0.56, hue: [192, 132, 252] },
    { m: torus(0.5, 0.2, 18, 10), br: 0.7, hue: [168, 85, 247] },
  ];
  // 広い画面（PC）と縦長の画面（スマホ）で、図形の大きさと、配置を決めるときの基準の画面の大きさ（配置は 0〜1 の画面の位置で持つので、実際の大きさが違っても崩れない）
  var MODES = {
    wide: { ref: [1440, 900], hMax: 1440, scale: 0.8, count: 10 },
    tall: { ref: [390, 844], hMax: 844, scale: 1.25, count: 8 },
  };
  var SEED = 20240611;
  var LIGHT = (function () { var l = [-0.45, 0.8, 0.4], n = Math.hypot(l[0], l[1], l[2]); return [l[0] / n, l[1] / n, l[2] / n]; })();

  // 固定のシードの乱数（mulberry32）。同じシードなら、いつも同じ並びになる
  function rng(seed) {
    var a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function randAxis(r) {
    var z = r() * 2 - 1, th = r() * Math.PI * 2, s = Math.sqrt(1 - z * z);
    return [s * Math.cos(th), z, s * Math.sin(th)];
  }
  // 軸 a のまわりに ang だけ回す 3×3 の行列（ロドリゲスの公式。行優先）
  function axisAngle(a, ang) {
    var c = Math.cos(ang), s = Math.sin(ang), k = 1 - c, x = a[0], y = a[1], z = a[2];
    return [c + x * x * k, x * y * k - z * s, x * z * k + y * s,
            x * y * k + z * s, c + y * y * k, y * z * k - x * s,
            x * z * k - y * s, y * z * k + x * s, c + z * z * k];
  }
  function mul(A, B) {
    var R = [];
    for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) R.push(A[i * 3] * B[j] + A[i * 3 + 1] * B[3 + j] + A[i * 3 + 2] * B[6 + j]);
    return R;
  }

  // 図形ごとの配置・回り方・漂い方を、固定のシードで決める。
  // 配置: 画面の位置（u, v）・奥行き d をばらして候補を作り、漂う幅の分も含めて、互いに重ならない候補（画面の上で、大きさの和より離れているもの）のうち、縦の離れを重く見て一番離れたものを選ぶ（横一列になりにくい）
  // 動き: 回る軸（向きは図形ごとにばらばら）・正逆・速さ、漂う向き・幅・周期も、図形ごとにばらばら
  function layout(mode) {
    var cfg = MODES[mode], w = cfg.ref[0], h = cfg.ref[1], f = focal(w, h), r = rng(SEED + (mode === "tall" ? 7919 : 0));
    var placed = [], bands = [];
    // 高さが揃わないよう、画面の高さを図形の数に等分し、図形ごとに別の帯をあてがう（帯の順はシードで混ぜる）
    for (var b = 0; b < cfg.count; b++) bands.push(b);
    for (var b2 = bands.length - 1; b2 > 0; b2--) { var sw = Math.floor(r() * (b2 + 1)), tmp = bands[b2]; bands[b2] = bands[sw]; bands[sw] = tmp; }
    for (var i = 0; i < cfg.count; i++) {
      var sh = SHAPES[i % SHAPES.length];
      var amp = [0.12 + 0.22 * r(), 0.08 + 0.14 * r(), 0.1 + 0.2 * r()];   // 漂う幅（世界の x, y, z）
      var best = null;
      for (var k = 0; k < 300; k++) {
        var u = 0.08 + 0.84 * r(), d = 5.2 + 6.8 * r(), v = 0.07 + 0.55 * (bands[i] + 0.5 + (r() - 0.5) * 1.6) / cfg.count;
        var cp = Math.cos(cam.pitch), sp = Math.sin(cam.pitch);
        // この位置の、世界の高さ。画面の縦横比が変わっても（hMax まで）床にめり込まないようにする
        var yw = cam.y + (HORIZON - v) * (v > HORIZON ? cfg.hMax : h) * d / f * cp + d * sp;
        if (yw < 0.45 + amp[1]) continue;
        var pr = sh.br * 0.9 * cfg.scale * f / d, reserve = Math.max(amp[0], amp[1], amp[2]) * f / d;   // 画面の上での半径と、漂う幅
        var sx = u * w, sy = v * h, edge = pr / 0.9 + reserve;
        if (sx < edge || sx > w - edge || sy < edge * 0.9) continue;   // 画面の端で欠けない
        var gap = 1e9, spread = 1e9;   // gap: 実際の画面の上の隙間（10px 以上が必須）。spread: 縦を重く見た隙間（大きいほど、縦にもばらける）
        for (var j = 0; j < placed.length; j++) {
          var o = placed[j], sum = pr + reserve + o.pr + o.reserve;
          gap = Math.min(gap, Math.hypot(sx - o.sx, sy - o.sy) - sum);
          spread = Math.min(spread, Math.hypot((sx - o.sx) * 0.75, (sy - o.sy) * 1.3) - sum);
        }
        var score = gap >= 10 ? 1e6 + spread : gap;
        if (!best || score > best.score) best = { score: score, gap: gap, u: u, v: v, d: d, yw: yw, sx: sx, sy: sy, pr: pr, reserve: reserve };
      }
      if (!best || best.gap < 10) continue;   // 重ならずに置ける場所がなければ、その図形は置かない
      var dirs = [r() < 0.5 ? -1 : 1, r() < 0.5 ? -1 : 1, r() < 0.5 ? -1 : 1];
      placed.push({
        m: sh.m, hue: sh.hue, br: sh.br, u: best.u, v: best.v, d: best.d, sx: best.sx, sy: best.sy, pr: best.pr, reserve: best.reserve, gap: best.gap,
        base: axisAngle(randAxis(r), r() * Math.PI * 2),   // 最初の向き
        axis: randAxis(r), spin: (r() < 0.5 ? -1 : 1) * (0.12 + 0.4 * r()), phase: r() * Math.PI * 2,   // 回る軸・向き・速さ（rad/秒）
        amp: amp, drift: [0, 1, 2].map(function (n) { return { w: dirs[n] * Math.PI * 2 / (16 + 34 * r()), p: r() * Math.PI * 2 }; }),   // 漂う向き（符号）・周期（16〜50 秒）・位相
      });
    }
    return placed;
  }
  var layouts = {};
  function mode() { return W >= H ? "wide" : "tall"; }
  function objects() { var m = mode(); return layouts[m] || (layouts[m] = layout(m)); }

  function drawObjects(t, k) {
    var tris = [], sec = t * 0.001, s = MODES[mode()].scale;
    objects().forEach(function (o) {
      var R = mul(axisAngle(o.axis, o.phase + o.spin * sec), o.base);
      var c = unproject(o.u, o.v, o.d, W, H);
      var cx = c[0] + o.amp[0] * Math.sin(o.drift[0].w * sec + o.drift[0].p),
          cy = c[1] + o.amp[1] * Math.sin(o.drift[1].w * sec + o.drift[1].p),
          cz = c[2] + o.amp[2] * Math.sin(o.drift[2].w * sec + o.drift[2].p);
      var wv = o.m.v.map(function (p) {
        var px = p[0] * s, py = p[1] * s, pz = p[2] * s;
        return [cx + R[0] * px + R[1] * py + R[2] * pz, cy + R[3] * px + R[4] * py + R[5] * pz, cz + R[6] * px + R[7] * py + R[8] * pz];
      });
      o.m.f.forEach(function (tri) {
        var A = wv[tri[0]], B = wv[tri[1]], C = wv[tri[2]];
        var ux = B[0] - A[0], uy = B[1] - A[1], uz = B[2] - A[2], vx = C[0] - A[0], vy = C[1] - A[1], vz = C[2] - A[2];
        var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, nl = Math.hypot(nx, ny, nz) || 1;
        nx /= nl; ny /= nl; nz /= nl;
        // 裏向きの面は描かない（カメラへ向かう方向との内積）
        var mx = (A[0] + B[0] + C[0]) / 3 - cam.x, my = (A[1] + B[1] + C[1]) / 3 - cam.y, mz = (A[2] + B[2] + C[2]) / 3 - cam.z;
        if (nx * mx + ny * my + nz * mz >= 0) return;
        var pa = project(A), pb = project(B), pc = project(C);
        if (!pa || !pb || !pc) return;
        var lit = Math.max(0, nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]);
        tris.push({ p: [pa, pb, pc], d: (pa[2] + pb[2] + pc[2]) / 3, lit: lit, hue: o.hue });
      });
    });
    tris.sort(function (a, b) { return b.d - a.d; });   // 奥から手前へ
    ctx.lineWidth = 0.8 * dpr; ctx.lineJoin = "round";
    tris.forEach(function (tr) {
      var shade = 0.32 + 0.68 * tr.lit, h = tr.hue;
      ctx.globalAlpha = 0.5 * k;
      ctx.fillStyle = "rgb(" + Math.round(h[0] * shade) + "," + Math.round(h[1] * shade) + "," + Math.round(h[2] * shade) + ")";
      ctx.beginPath();
      ctx.moveTo(tr.p[0][0] * dpr, tr.p[0][1] * dpr); ctx.lineTo(tr.p[1][0] * dpr, tr.p[1][1] * dpr); ctx.lineTo(tr.p[2][0] * dpr, tr.p[2][1] * dpr);
      ctx.closePath(); ctx.fill();
      // 面のつなぎ目に、細い線（Blender の「ワイヤーのオーバーレイ」のように）
      ctx.globalAlpha = 0.18 * k; ctx.strokeStyle = dark ? "rgb(230,215,255)" : "rgb(60,30,130)"; ctx.stroke();
    });
    ctx.globalAlpha = 1;
  }

  function draw(t) {
    var color = dark ? "179,136,255" : "101,31,255";
    var k = dark ? 1 : 0.55;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.lineWidth = 1.1 * dpr;
    ctx.strokeStyle = "rgb(" + color + ")";

    // 床の方眼: 手前へ、ゆっくり流れる
    var scroll = (t * 0.00035) % 1;
    for (var i = -18; i <= 18; i++) {
      var a = Math.max(0, 1 - Math.abs(i) / 18) * 0.26 * k;
      line([i, 0, -34], [i, 0, 5], a);
    }
    for (var j = -34; j <= 5; j++) {
      var z = j + scroll;
      var near = (z + 34) / 39;
      line([-18, 0, z], [18, 0, z], (0.05 + 0.24 * near) * k);
    }
    // 床の中心の太い線（Blender の X 軸（赤）・Y 軸（緑）の代わりに、淡い紫で）
    line([-18, 0, 0], [18, 0, 0], 0.4 * k);
    line([0, 0, -34], [0, 0, 5], 0.4 * k);

    // Blender の基本オブジェクトを、宙に散らして、ゆっくり回す・漂わせる
    drawObjects(t, k);
    ctx.globalAlpha = 1;
  }

  var last = 0, raf = 0;
  function frame(now) {
    raf = requestAnimationFrame(frame);
    if (document.hidden || now - last < 33) return;
    last = now;
    draw(now);
  }

  function start() {
    readTheme(); resize();
    cancelAnimationFrame(raf);
    if (reduce.matches) { draw(4000); return; }
    raf = requestAnimationFrame(frame);
  }

  window.addEventListener("resize", function () { resize(); if (reduce.matches) draw(4000); });
  new MutationObserver(function () { readTheme(); if (reduce.matches) draw(4000); }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-bs-theme"] });
  if (reduce.addEventListener) reduce.addEventListener("change", start);
  start();
})();
