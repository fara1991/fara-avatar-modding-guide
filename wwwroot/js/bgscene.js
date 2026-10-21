// 背景の飾り（見た目だけ）: Blender の 3D ビューのような、奥へ延びる床の方眼と、床の上でゆっくり回る基本オブジェクト（立方体・球・アイコ球・円錐・円柱・トーラス）。
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
  function project(p) {
    var x = p[0] - cam.x, y = p[1] - cam.y, z = p[2] - cam.z;
    var cp = Math.cos(cam.pitch), sp = Math.sin(cam.pitch);
    var y2 = y * cp + z * sp, z2 = -y * sp + z * cp;
    var d = -z2;
    if (d < 0.2) return null;
    // 縦長の画面（スマホ）では、高さではなく幅に合わせる（高さに合わせると、右のオブジェクトが画面の外に出て、端しか見えない）
    var f = Math.max(W, H * 0.6) * 0.95;
    return [W * 0.5 + (x * f) / d, H * 0.38 - (y2 * f) / d, d];
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
  function uvSphere(r, seg, rings) {
    return grid(seg, rings, function (i, j) {
      var th = (i / seg) * Math.PI * 2, ph = (j / rings) * Math.PI;
      return [r * Math.sin(ph) * Math.cos(th), r * Math.cos(ph), r * Math.sin(ph) * Math.sin(th)];
    });
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
    for (var i = 0; i < seg; i++) m.f.push([c, i + 1, i]);
    return m;
  }
  function cylinder(r, h, seg) {
    var m = grid(seg, 1, function (i, j) { var th = (i / seg) * Math.PI * 2; return [r * Math.cos(th), j === 0 ? -h / 2 : h / 2, r * Math.sin(th)]; });
    var bot = m.v.length; m.v.push([0, -h / 2, 0]); var top = m.v.length; m.v.push([0, h / 2, 0]);
    for (var i = 0; i < seg; i++) { m.f.push([bot, i + 1, i]); m.f.push([top, seg + 1 + i, seg + 2 + i]); }
    return m;
  }
  function torus(R, r, seg, tube) {
    return grid(seg, tube, function (i, j) {
      var u = (i / seg) * Math.PI * 2, w = (j / tube) * Math.PI * 2;
      return [(R + r * Math.cos(w)) * Math.cos(u), r * Math.sin(w), (R + r * Math.cos(w)) * Math.sin(u)];
    });
  }

  // 置く物（x, z は床の上の位置。spin は回る速さ、tilt は傾き）。奥のものほど、画面の上に見える
  var OBJECTS = [
    { m: cone(0.5, 1.1, 16), x: 1.7, z: -4.4, y: 0.45, spin: 0.9, tilt: 0.12, hue: [168, 85, 247] },
    { m: icosphere(0.62), x: 3.3, z: -6.4, y: 0.5, spin: 0.5, tilt: 0.3, hue: [196, 181, 253] },
    { m: cube(), x: 2.7, z: -3.2, y: 0.42, spin: 0.7, tilt: 0.35, hue: [124, 58, 237] },
    { m: torus(0.5, 0.2, 18, 10), x: 4.6, z: -4.8, y: 0.6, spin: 0.6, tilt: 0.9, hue: [192, 132, 252] },
    { m: uvSphere(0.5, 14, 9), x: 0.9, z: -6.0, y: 0.42, spin: 0.35, tilt: 0.1, hue: [147, 114, 255] },
    { m: cylinder(0.38, 1.0, 16), x: 5.6, z: -3.6, y: 0.42, spin: 0.8, tilt: 0.2, hue: [168, 85, 247] },
  ];
  var OBJ_SCALE = 0.8;
  var LIGHT = (function () { var l = [-0.45, 0.8, 0.4], n = Math.hypot(l[0], l[1], l[2]); return [l[0] / n, l[1] / n, l[2] / n]; })();

  function drawObjects(t, k) {
    var tris = [];
    OBJECTS.forEach(function (o, idx) {
      var ay = t * 0.001 * o.spin * 0.6 + idx, ax = o.tilt + Math.sin(t * 0.0004 + idx) * 0.15;
      var cy = Math.cos(ay), sy = Math.sin(ay), cx = Math.cos(ax), sx = Math.sin(ax);
      var bob = Math.sin(t * 0.0008 + idx * 1.7) * 0.06;
      function tf(p) {
        var px = p[0] * OBJ_SCALE, py = p[1] * OBJ_SCALE, pz = p[2] * OBJ_SCALE;
        var x = px * cy + pz * sy, z = -px * sy + pz * cy, y = py;
        var y2 = y * cx - z * sx, z2 = y * sx + z * cx;
        return [x, y2, z2];
      }
      var wv = o.m.v.map(function (p) { var q = tf(p); return [o.x + q[0], o.y + bob + q[1], o.z + q[2]]; });
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

    // Blender の基本オブジェクトを、床の上に置いて、ゆっくり回す
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
