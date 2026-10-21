// 背景の飾り（見た目だけ）: Blender の 3D ビューのような、奥へ延びる床の方眼と、ゆっくり回る針金（ワイヤーフレーム）のアバター。
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
    var f = Math.max(W, H) * 0.95;
    return [W * 0.5 + (x * f) / d, H * 0.38 - (y2 * f) / d, d];
  }

  function line(a, b, alpha) {
    var p = project(a), q = project(b);
    if (!p || !q) return;
    ctx.globalAlpha = alpha;
    ctx.beginPath(); ctx.moveTo(p[0] * dpr, p[1] * dpr); ctx.lineTo(q[0] * dpr, q[1] * dpr); ctx.stroke();
  }

  // ---- 針金のアバター（箱と球だけで作った、簡単な人型） ----
  var BOXES = [
    // [中心 x, y, z, 幅, 高さ, 奥行き]
    [0, 1.28, 0, 0.5, 0.62, 0.26],    // 胴
    [0, 0.8, 0, 0.42, 0.26, 0.24],    // 腰
    [-0.36, 1.25, 0, 0.14, 0.56, 0.14], [0.36, 1.25, 0, 0.14, 0.56, 0.14],     // 腕
    [-0.14, 0.34, 0, 0.17, 0.74, 0.17], [0.14, 0.34, 0, 0.17, 0.74, 0.17],     // 脚
  ];
  var CUBE_EDGES = [[0, 1], [1, 3], [3, 2], [2, 0], [4, 5], [5, 7], [7, 6], [6, 4], [0, 4], [1, 5], [2, 6], [3, 7]];

  function boxVerts(b) {
    var v = [];
    for (var i = 0; i < 8; i++) v.push([b[0] + ((i & 1) ? b[3] : -b[3]) / 2, b[1] + ((i & 2) ? b[4] : -b[4]) / 2, b[2] + ((i & 4) ? b[5] : -b[5]) / 2]);
    return v;
  }

  function drawFigure(angle, ox, oz, alpha) {
    var ca = Math.cos(angle), sa = Math.sin(angle);
    function tf(p) { return [ox + p[0] * ca + p[2] * sa, p[1], oz - p[0] * sa + p[2] * ca]; }
    BOXES.forEach(function (b) {
      var v = boxVerts(b).map(tf);
      CUBE_EDGES.forEach(function (e) { line(v[e[0]], v[e[1]], alpha); });
    });
    // 頭（3 つの輪で球を表す）
    var cx = 0, cy = 1.82, cz = 0, r = 0.19, n = 20;
    for (var ring = 0; ring < 3; ring++) {
      var prev = null;
      for (var i = 0; i <= n; i++) {
        var t = (i / n) * Math.PI * 2, c = Math.cos(t) * r, s = Math.sin(t) * r, p;
        if (ring === 0) p = [cx + c, cy + s, cz];
        else if (ring === 1) p = [cx + c, cy, cz + s];
        else p = [cx, cy + c, cz + s];
        p = tf(p);
        if (prev) line(prev, p, alpha);
        prev = p;
      }
    }
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

    // 針金のアバター: 右寄りに置いて、ゆっくり回る
    drawFigure(t * 0.00035, 2.9, -1.2, 0.7 * k);
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
