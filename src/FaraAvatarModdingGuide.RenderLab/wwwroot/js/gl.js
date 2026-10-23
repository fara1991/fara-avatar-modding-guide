// Phase 3b S2: 練習の 3D の薄い WebGL 2 層(docs/phase3b-design.md の §3 (A) と §11)。C#(FaraAvatarModdingGuide.Render)の [JSImport] から呼ぶ。
// 役割は 3 つだけ: ① 頂点バッファを GPU に送る(変わったときだけ) ② 1 フレームぶんの描画の命令(C# の FramePacker が詰めた float の列)を順に描く ③ ポインター・ホイール・キーを数値にして C# に渡す。
// 何を・どの順で描くか(方眼・オブジェクト・輪郭・編集モードの点/線/面…)は C# が決める。ここは three.js の MeshStandardMaterial / LineBasicMaterial / PointsMaterial と同じ見た目の
// シェーダーと GL の状態の切り替えだけ(半球光 + 平行光 + 直接光の鏡面反射、sRGB 出力)。
// 描画のループもここ(requestAnimationFrame)。画面外・タブが隠れているあいだは描かない。隠れて始まった場合に備えて、始めたときに 1 フレーム描き、表示に戻ったときにも描く
// (「新しいブラウザコンテキストで描画が始まらない」の原因: 隠れたウィンドウでは rAF が呼ばれない。docs/phase3b-design.md の §11.2)。
// dispose() で WebGL のコンテキスト・リスナー・rAF・observer を全部解放する。コンテキストを失ったら(webglcontextlost)、復帰したとき(webglcontextrestored)に作り直して送り直す。

const HEADER = 24;      // FramePacker.HeaderFloats
const STRIDE = 28;      // FramePacker.ItemFloats
const LAYOUT_STRIDE = [3, 6, 6];   // BufferLayout.Position / PositionNormal / PositionColor の 1 頂点あたりの float
const BACKGROUND = [0x3d / 255, 0x3d / 255, 0x3d / 255];   // scene.background = 0x3d3d3d(sRGB の値のまま出力する)

const VERTEX = `#version 300 es
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aAttr;
uniform mat4 uViewProj;
uniform mat4 uWorld;
uniform float uPointSize;
out vec3 vNormal;
out vec3 vWorldPos;
out vec3 vColor;
void main() {
  vec4 wp = uWorld * vec4(aPos, 1.0);
  gl_Position = uViewProj * wp;
  vWorldPos = wp.xyz;
  // 法線は上 3x3 の逆行列の転置で変換する(G/R/S の軸固定で不均一なスケールになっても向きが合う。three.js の normalMatrix と同じ)
  vNormal = transpose(inverse(mat3(uWorld))) * aAttr;
  vColor = aAttr;
  gl_PointSize = uPointSize;
}`;

const FRAGMENT = `#version 300 es
precision highp float;
in vec3 vNormal;
in vec3 vWorldPos;
in vec3 vColor;
uniform int uShading;      // 0 = 陰影(MeshStandardMaterial)  1 = 1 色  2 = 頂点の色
uniform vec4 uColor;       // 色(sRGB)と不透明度
uniform vec3 uCamera;
out vec4 outColor;

const float PI = 3.141592653589793;
const float EPS = 1e-6;
// three.js の ColorManagement と同じ式(sRGB <-> 線形)
vec3 toLinear(vec3 c) { return mix(pow(c * 0.9478672985781991 + 0.0521327014218009, vec3(2.4)), c * 0.0773993808049536, lessThanEqual(c, vec3(0.04045))); }
vec3 toSrgb(vec3 c) { return mix(pow(c, vec3(1.0 / 2.4)) * 1.055 - 0.055, c * 12.92, lessThanEqual(c, vec3(0.0031308))); }

// MeshStandardMaterial(roughness 0.75, metalness 0)の直接光の鏡面反射(three.js r170 の BRDF_GGX)
float D_GGX(float alpha, float dotNH) { float a2 = alpha * alpha; float d = dotNH * dotNH * (a2 - 1.0) + 1.0; return a2 / (PI * d * d); }
float V_GGX(float alpha, float dotNL, float dotNV) {
  float a2 = alpha * alpha;
  float gv = dotNL * sqrt(a2 + (1.0 - a2) * dotNV * dotNV);
  float gl = dotNV * sqrt(a2 + (1.0 - a2) * dotNL * dotNL);
  return 0.5 / max(gv + gl, EPS);
}
vec3 lit(vec3 baseSrgb, vec3 n, vec3 v) {
  vec3 albedo = toLinear(baseSrgb);
  // HemisphereLight(0xffffff, 0x444444, 1.1): three.js の up は +Y のまま(このシーンの上は +Z だが、ライトの向きは変えていない)
  vec3 ground = toLinear(vec3(68.0 / 255.0)) * 1.1;
  vec3 hemi = mix(ground, vec3(1.1), 0.5 * n.y + 0.5);
  // DirectionalLight(0xffffff, 1.2) を (4, -6, 8) から原点へ
  vec3 l = normalize(vec3(4.0, -6.0, 8.0));
  float dotNL = clamp(dot(n, l), 0.0, 1.0);
  vec3 irradiance = vec3(dotNL * 1.2);
  vec3 diffuse = (hemi + irradiance) * albedo / PI;
  float roughness = 0.75, alpha = roughness * roughness;
  vec3 h = normalize(l + v);
  float dotNV = clamp(abs(dot(n, v)) + 1e-5, 0.0, 1.0);
  float dotNH = clamp(dot(n, h), 0.0, 1.0);
  float dotVH = clamp(dot(v, h), 0.0, 1.0);
  float fresnel = exp2((-5.55473 * dotVH - 6.98316) * dotVH);
  float F = 0.04 * (1.0 - fresnel) + fresnel;
  vec3 specular = irradiance * (F * V_GGX(alpha, dotNL, dotNV) * D_GGX(alpha, dotNH));
  return diffuse + specular;
}

void main() {
  if (uShading == 0) {
    vec3 n = normalize(vNormal);
    if (!gl_FrontFacing) n = -n;   // DoubleSide: 裏から見た面は法線を反転する
    vec3 v = normalize(uCamera - vWorldPos);
    outColor = vec4(toSrgb(lit(uColor.rgb, n, v)), uColor.a);
  } else if (uShading == 1) {
    outColor = uColor;
  } else {
    outColor = vec4(vColor, 1.0);
  }
}`;

const PRIMITIVES = (gl) => [gl.TRIANGLES, gl.LINES, gl.LINE_STRIP, gl.POINTS];

/**
 * 始める。root の中に canvas を作り、ポインター・ホイール・キーを C# に渡す。戻り値は WebGL 2 が使えたか。座標は root の左上を原点にして渡す(C# の ViewRect は (0, 0, 幅, 高さ)。
 * source-generated な [JSImport] は配列を引数にした関数を渡せないので、イベントの種類ごとに数値の引数だけの関数にしてある。
 *   frame(cssWidth, cssHeight)                 描くとき(C# が uploadBuffer / releaseBuffer / draw を呼ぶ)
 *   pointerDown(button | modifiers << 4, x, y) → preventDefault するか     pointerMove(x, y, modifiers)     pointerUp(x, y)
 *   wheel(deltaY) → preventDefault するか       key(code, key, modifiers) → preventDefault するか   keyup(modifiers)   focus(boolean)
 * modifiers: 1 = Shift, 2 = Ctrl, 4 = Alt, 8 = Meta。
 */
export function start(rootId, frame, pointerDown, pointerMove, pointerUp, wheel, key, keyup, focus) {
  performance.mark("gl:start");
  dispose();
  const root = document.getElementById(rootId);
  if (!root) throw new Error("#" + rootId + " がありません");
  const abort = new AbortController();
  const { signal } = abort;
  const canvas = document.createElement("canvas");
  canvas.style.cssText = "display:block;width:100%;height:100%";
  root.insertBefore(canvas, root.firstChild);
  root.tabIndex = 0;
  const s = {
    root, canvas, abort, frame,
    gl: null, program: null, loc: null, buffers: new Map(), copies: new Map(), items: new Float32Array(0),
    raf: 0, onScreen: true, lost: false, disposed: false, pixelRatio: 1,
    stats: { frames: 0, drawn: 0, skippedHidden: 0, callbackMs: 0, lastDrawItems: 0, contextCreated: 0, contextLost: 0, contextRestored: 0 },
    observers: [],
  };
  state = s;

  canvas.addEventListener("webglcontextlost", (e) => { e.preventDefault(); s.lost = true; s.stats.contextLost++; }, { signal });
  canvas.addEventListener("webglcontextrestored", () => { if (init(s)) { restore(s); s.lost = false; s.stats.contextRestored++; render(s, performance.now()); } }, { signal });
  if (!init(s)) {
    abort.abort();
    canvas.remove();
    state = null;
    return false;
  }
  performance.mark("gl:inited");

  const mods = (e) => (e.shiftKey ? 1 : 0) | (e.ctrlKey ? 2 : 0) | (e.altKey ? 4 : 0) | (e.metaKey ? 8 : 0);
  root.addEventListener("contextmenu", (e) => e.preventDefault(), { signal });
  const at = (e) => { const r = root.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
  root.addEventListener("pointerdown", (e) => {
    root.focus({ preventScroll: true });
    root.setPointerCapture(e.pointerId);
    const [x, y] = at(e);
    if (pointerDown(e.button | (mods(e) << 4), x, y)) e.preventDefault();
  }, { signal });
  root.addEventListener("pointermove", (e) => { const [x, y] = at(e); pointerMove(x, y, mods(e)); }, { signal });
  root.addEventListener("pointerup", (e) => {
    if (root.hasPointerCapture(e.pointerId)) root.releasePointerCapture(e.pointerId);
    const [x, y] = at(e);
    pointerUp(x, y);
  }, { signal });
  // ホイールは passive: false にしないと preventDefault できず、ページがスクロールしてしまう(試作ではここで失敗した)
  root.addEventListener("wheel", (e) => { if (wheel(e.deltaY)) e.preventDefault(); }, { passive: false, signal });
  root.addEventListener("keydown", (e) => { if (key(e.code, e.key, mods(e))) e.preventDefault(); }, { signal });
  root.addEventListener("keyup", (e) => { keyup(mods(e)); }, { signal });
  root.addEventListener("focus", () => focus(true), { signal });
  root.addEventListener("blur", () => focus(false), { signal });

  // 画面外にあるあいだは描かない。見えるようになったときに 1 フレーム描く
  const io = new IntersectionObserver((entries) => {
    const was = s.onScreen;
    s.onScreen = entries[entries.length - 1].isIntersecting;
    if (s.onScreen && !was) render(s, performance.now());
  });
  io.observe(root);
  s.observers.push(io);
  // サイズが変わったら(0×0 から大きさが付いたときも)描く。次の rAF を待たない
  const ro = new ResizeObserver(() => render(s, performance.now()));
  ro.observe(root);
  s.observers.push(ro);
  document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible") render(s, performance.now()); }, { signal });

  const loop = (t) => {
    s.raf = requestAnimationFrame(loop);
    s.stats.rafCalls = (s.stats.rafCalls || 0) + 1;
    if (!s.onScreen) { s.stats.skippedHidden++; return; }
    render(s, t);
  };
  s.raf = requestAnimationFrame(loop);
  render(s, performance.now());   // 隠れたタブで始まっても、最初の 1 枚は描いておく
  return true;
}

let state = null;

function compile(gl, type, src) {
  const sh = gl.createShader(type);
  gl.shaderSource(sh, src);
  gl.compileShader(sh);
  if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(sh));
  return sh;
}

/** コンテキスト・シェーダー・uniform の場所を用意する(失ったあとの作り直しでも呼ぶ) */
function init(s) {
  const gl = s.canvas.getContext("webgl2", { antialias: true, alpha: false });
  if (!gl) return false;
  s.stats.contextCreated++;
  s.gl = gl;
  const program = gl.createProgram();
  gl.attachShader(program, compile(gl, gl.VERTEX_SHADER, VERTEX));
  gl.attachShader(program, compile(gl, gl.FRAGMENT_SHADER, FRAGMENT));
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  s.program = program;
  s.loc = Object.fromEntries(["uViewProj", "uWorld", "uPointSize", "uShading", "uColor", "uCamera"].map((n) => [n, gl.getUniformLocation(program, n)]));
  s.buffers = new Map();
  gl.useProgram(program);
  gl.disable(gl.CULL_FACE);
  gl.depthFunc(gl.LEQUAL);
  gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
  const info = gl.getExtension("WEBGL_debug_renderer_info");
  s.renderer = info ? gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : "";
  return true;
}

/** コンテキストを作り直したあと、CPU 側に持っている頂点を送り直す */
function restore(s) {
  for (const [id, c] of s.copies) upload(s, id, c.layout, c.data, false);
}

function upload(s, id, layout, floats, keep = true) {
  const gl = s.gl;
  release(s, id, false);
  const stride = LAYOUT_STRIDE[layout];
  const buffer = gl.createBuffer();
  const vao = gl.createVertexArray();
  gl.bindVertexArray(vao);
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.bufferData(gl.ARRAY_BUFFER, floats, gl.DYNAMIC_DRAW);
  gl.enableVertexAttribArray(0);
  gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride * 4, 0);
  if (stride === 6) {
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride * 4, 12);
  }
  gl.bindVertexArray(null);
  s.buffers.set(id, { buffer, vao, count: floats.length / stride, layout });
  if (keep) s.copies.set(id, { layout, data: floats });
}

function release(s, id, forget = true) {
  const b = s.buffers.get(id);
  if (b) { s.gl.deleteBuffer(b.buffer); s.gl.deleteVertexArray(b.vao); s.buffers.delete(id); }
  if (forget) s.copies.delete(id);
}

/** C# から: 頂点バッファを送る(id ごとに置き換える)。bytes は Span<byte> の MemoryView(呼び出しの間だけ有効なので、コピーを取る) */
export function uploadBuffer(id, layout, bytes) {
  const s = state;
  if (!s || s.disposed) return;
  const copy = new Float32Array(bytes.slice().buffer);
  s.copies.set(id, { layout, data: copy });
  if (!s.lost) upload(s, id, layout, copy, false);
}

export function releaseBuffer(id) {
  const s = state;
  if (s && !s.disposed) release(s, id);
}

/** C# から: 1 フレームを描く。bytes は FramePacker の float の列 */
export function draw(bytes) {
  const s = state;
  if (!s || s.disposed || s.lost) return;
  const n = bytes.byteLength >> 2;
  if (s.items.length < n) s.items = new Float32Array(Math.max(n, s.items.length * 2, 1024));
  bytes.copyTo(new Uint8Array(s.items.buffer, 0, n * 4));
  paint(s, s.items, n);
}

function paint(s, f, n) {
  const gl = s.gl;
  const count = f[19];
  const modes = PRIMITIVES(gl);
  const dpr = s.pixelRatio;
  gl.viewport(0, 0, s.canvas.width, s.canvas.height);
  gl.depthMask(true);
  gl.disable(gl.SCISSOR_TEST);
  gl.clearColor(BACKGROUND[0], BACKGROUND[1], BACKGROUND[2], 1);
  gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
  gl.uniformMatrix4fv(s.loc.uViewProj, false, f.subarray(0, 16));
  gl.uniform3f(s.loc.uCamera, f[16], f[17], f[18]);
  let lastVao = null;
  for (let i = 0; i < count; i++) {
    const o = HEADER + i * STRIDE;
    const b = s.buffers.get(f[o]);
    if (!b || b.count === 0) continue;
    if (b.vao !== lastVao) { gl.bindVertexArray(b.vao); lastVao = b.vao; }
    const flags = f[o + 3];
    if (flags & 1) gl.enable(gl.DEPTH_TEST); else gl.disable(gl.DEPTH_TEST);
    gl.depthMask((flags & 2) !== 0);
    if (flags & 4) gl.enable(gl.BLEND); else gl.disable(gl.BLEND);
    if (flags & 16) { gl.enable(gl.POLYGON_OFFSET_FILL); gl.polygonOffset(f[o + 4], f[o + 5]); } else gl.disable(gl.POLYGON_OFFSET_FILL);
    gl.frontFace((flags & 8) ? gl.CW : gl.CCW);
    gl.uniformMatrix4fv(s.loc.uWorld, false, f.subarray(o + 12, o + 28));
    gl.uniform1i(s.loc.uShading, f[o + 2]);
    gl.uniform4f(s.loc.uColor, f[o + 8], f[o + 9], f[o + 10], f[o + 6]);
    gl.uniform1f(s.loc.uPointSize, f[o + 7] * dpr);
    gl.drawArrays(modes[f[o + 1]], 0, b.count);
  }
  gl.bindVertexArray(null);
  s.stats.drawn++;
  if (s.stats.drawn === 1) performance.mark("gl:firstPaint");
  s.stats.lastDrawItems = count;
}

/** 1 フレーム: 大きさを合わせて、C# に描かせる */
function render(s, t) {
  if (s.disposed || s.lost) return;
  const { root, canvas } = s;
  const w = root.clientWidth, h = root.clientHeight;
  if (w === 0 || h === 0) return;   // 大きさが付いたとき(ResizeObserver)に描く
  s.pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  const cw = Math.floor(w * s.pixelRatio), ch = Math.floor(h * s.pixelRatio);
  if (canvas.width !== cw) canvas.width = cw;
  if (canvas.height !== ch) canvas.height = ch;
  const t0 = performance.now();
  s.stats.frames++;
  s.frame(w, h);
  s.stats.callbackMs += performance.now() - t0;
}

/** 解放する(WebGL のコンテキスト・リスナー・rAF・observer。DOM の canvas も消す) */
export function dispose() {
  const s = state;
  if (!s) return;
  s.disposed = true;
  state = null;
  cancelAnimationFrame(s.raf);
  s.abort.abort();
  for (const o of s.observers) o.disconnect();
  if (s.gl && !s.lost) {
    for (const id of [...s.buffers.keys()]) release(s, id, false);
    s.gl.deleteProgram(s.program);
    s.gl.getExtension("WEBGL_lose_context")?.loseContext();
  }
  s.copies.clear();
  s.canvas.remove();
}

/** 調べる用: いまの状態の要約 */
export function diag() {
  const s = state;
  const base = { visibilityState: document.visibilityState, hasFocus: document.hasFocus(), raf: s ? s.stats.rafCalls || 0 : 0 };
  if (!s) return JSON.stringify({ ...base, started: false });
  return JSON.stringify({
    ...base, started: true, lost: s.lost, onScreen: s.onScreen, renderer: s.renderer, pixelRatio: s.pixelRatio,
    canvas: { w: s.canvas.width, h: s.canvas.height, cw: s.root.clientWidth, ch: s.root.clientHeight },
    buffers: s.buffers.size, ...s.stats,
  });
}

/** 取り出した計測の結果を置く場所(C# の Report が書き、Playwright が読む) */
export function report(name, json) {
  (globalThis.__labReport ??= {})[name] = JSON.parse(json);
}

/** C# の制御用の関数(ベンチ・停止・再開)を、ページから呼べるようにする */
export function exposeControl(control) {
  globalThis.__labControl = control;
}

export function getQuery() { return location.search; }

/** 呼び出し 1 回の時間を測るための、いまの時刻(ms) */
export function now() { return performance.now(); }

/** 調べる用: コンテキストを人為的に失わせる / 戻す(WEBGL_lose_context) */
export function loseContext(restoreAfterMs) {
  const s = state;
  const ext = s?.gl?.getExtension("WEBGL_lose_context");
  if (!ext) return false;
  ext.loseContext();
  if (restoreAfterMs >= 0) setTimeout(() => ext.restoreContext(), restoreAfterMs);
  return true;
}

globalThis.__glDiag = diag;
globalThis.__glLose = loseContext;
