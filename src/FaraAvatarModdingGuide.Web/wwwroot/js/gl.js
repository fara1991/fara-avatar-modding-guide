// Phase 3b S2: 練習の 3D の薄い WebGL 2 層(docs/phase3b-design.md の §3 (A) と §11)。C#(FaraAvatarModdingGuide.Render)の [JSImport] から呼ぶ。
// 役割は 3 つだけ: ① 頂点バッファを GPU に送る(変わったときだけ) ② 1 フレームぶんの描画の命令(C# の FramePacker が詰めた float の列)を順に描く ③ ポインター・ホイール・キーを数値にして C# に渡す。
// 何を・どの順で描くか(方眼・オブジェクト・輪郭・編集モードの点/線/面…)は C# が決める。ここは three.js の MeshStandardMaterial / LineBasicMaterial / PointsMaterial と同じ見た目の
// シェーダーと GL の状態の切り替えだけ(半球光 + 平行光 + 直接光の鏡面反射、sRGB 出力)。
// 描画のループもここ(requestAnimationFrame)。画面外・タブが隠れているあいだは描かない。隠れて始まった場合に備えて、始めたときに 1 フレーム描き、表示に戻ったときにも描く
// (「新しいブラウザコンテキストで描画が始まらない」の原因: 隠れたウィンドウでは rAF が呼ばれない。docs/phase3b-design.md の §11.2)。
// dispose() で WebGL のコンテキスト・リスナー・rAF・observer を全部解放する。コンテキストを失ったら(webglcontextlost)、復帰したとき(webglcontextrestored)に作り直して送り直す。

const HEADER = 32;      // FramePacker.HeaderFloats
const STRIDE = 53;      // FramePacker.ItemFloats
const LAYOUT_STRIDE = [3, 6, 6];   // BufferLayout.Position / PositionNormal / PositionColor の 1 頂点あたりの float
const BACKGROUND = [0x3d / 255, 0x3d / 255, 0x3d / 255];   // scene.background = 0x3d3d3d(sRGB の値のまま出力する)

const VERTEX = `#version 300 es
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aAttr;
uniform mat4 uProj;
uniform mat4 uModelView;
uniform mat3 uNormalMatrix;   // ModelView の法線行列(three.js の normalMatrix)
uniform float uPointSize;
out vec3 vNormal;           // ビュー空間(three.js と同じ。補間してから使う)
out vec3 vViewPosition;
out vec3 vColor;
out float vDist;
void main() {
  vec4 mvPosition = uModelView * vec4(aPos, 1.0);   // three.js の project_vertex と同じ順(ModelView を先に掛けてから射影)。順が違うと、同じ平面の面・線の深度の丸めがずれて、重なった所のちらつき方が変わる
  gl_Position = uProj * mvPosition;
  vViewPosition = -mvPosition.xyz;
  // 法線は、three.js と同じに、ビュー空間で(normalMatrix = ModelView の上 3x3 の逆行列の転置)正規化してから補間する。
  // 細い三角形(球・カプセルの極)では、補間が三角形の外へ大きく外挿されるので、空間・手順が違うと、その画素の明るさが変わる
  vNormal = normalize(uNormalMatrix * aAttr);
  vColor = aAttr;
  vDist = aPos.x + 40.0;   // 点線(shading 3)の位置。X 方向に -40〜40 を 1 刻みで作った線なので、three.js の lineDistance(積算の長さ)と同じ
  gl_PointSize = uPointSize;
}`;

const FRAGMENT = `#version 300 es
precision highp float;
in vec3 vNormal;
in vec3 vViewPosition;
in vec3 vColor;
in float vDist;
uniform int uShading;      // 0 = 陰影(MeshStandardMaterial)  1 = 1 色  2 = 頂点の色  3 = 1 色の点線(0.3 描いて 0.2 空ける)
uniform vec4 uColor;       // 色(sRGB)と不透明度
uniform vec3 uLightDir;    // 平行光の向き(光源の向き。ビュー空間。正規化済み)
uniform vec3 uHemiDir;     // 半球光の上の向き(ビュー空間。正規化済み)
uniform float uGround;     // 半球光の下の色(灰色の濃さ。sRGB)
uniform float uHemi;       // 半球光の強さ
uniform float uDirI;       // 平行光の強さ
uniform float uRough;      // 粗さ(MeshStandardMaterial の roughness)
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
  // HemisphereLight(0xffffff, 下の色, 強さ): three.js の up は +Y のまま(Blender の練習のシーンの上は +Z だが、ライトの向きは変えていない)
  vec3 ground = toLinear(vec3(uGround)) * uHemi;
  vec3 hemi = mix(ground, vec3(uHemi), 0.5 * dot(n, uHemiDir) + 0.5);
  // DirectionalLight(0xffffff, 強さ) を uLightDir から原点へ
  vec3 l = uLightDir;
  float dotNL = clamp(dot(n, l), 0.0, 1.0);
  vec3 irradiance = vec3(dotNL * uDirI);
  vec3 diffuse = (hemi + irradiance) * albedo / PI;
  float roughness = uRough, alpha = roughness * roughness;
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
    vec3 v = normalize(vViewPosition);
    outColor = vec4(toSrgb(lit(uColor.rgb, n, v)), uColor.a);
  } else if (uShading == 3) {
    if (mod(vDist, 0.5) > 0.3) discard;
    outColor = uColor;
  } else if (uShading == 1) {
    outColor = uColor;
  } else {
    outColor = vec4(vColor, uColor.a);
  }
}`;

// スカイ(Unity 風の Scene の背景。scene.background = CanvasTexture と同じ: 全画面の板に、画像を貼る)
const SKY_VERTEX = `#version 300 es
out vec2 vUv;
void main() {
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
  vUv = p;
  gl_Position = vec4(p * 2.0 - 1.0, 1.0, 1.0);
}`;
const SKY_FRAGMENT = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uTex;
out vec4 outColor;
vec3 toSrgb(vec3 c) { return mix(pow(c, vec3(1.0 / 2.4)) * 1.055 - 0.055, c * 12.92, lessThanEqual(c, vec3(0.0031308))); }
void main() {
  vec4 t = texture(uTex, vUv);
  outColor = vec4(toSrgb(t.rgb), 1.0);
}`;

const PRIMITIVES = (gl) => [gl.TRIANGLES, gl.LINES, gl.LINE_STRIP, gl.POINTS];

/**
 * 始める。root の中に canvas を作り、ポインター・ホイール・キーを C# に渡す。戻り値は WebGL 2 が使えたか。座標は root の左上を原点にして渡す(C# の ViewRect は (0, 0, 幅, 高さ)。
 * source-generated な [JSImport] は配列を引数にした関数を渡せないので、イベントの種類ごとに数値の引数だけの関数にしてある。
 *   frame(cssWidth, cssHeight, clientW * 65536 + clientH)               描くとき(C# が uploadBuffer / releaseBuffer / draw を呼ぶ)
 *   pointerDown(button | modifiers << 4, x, y) → preventDefault するか     pointerMove(x, y, modifiers)     pointerUp(x, y)
 *   wheel(deltaY) → preventDefault するか       key(code, key, modifiers) → preventDefault するか   keyup(modifiers)   focus(boolean)
 * modifiers: 1 = Shift, 2 = Ctrl, 4 = Alt, 8 = Meta。
 * keyRootId: キー・フォーカスを受ける要素(省略は root)。Unity 風の画面は、ポインター・ホイールを Scene ビュー(root)で、キーをエディター全体(keyRootId)で受ける。
 * keyIgnore: キーを C# に渡さない要素の selector(省略は "input, button" = Blender の HUD。Unity 風は "input, select, textarea")。
 * data-gl-ignore を付けた要素(の中)のポインター操作は、ビューの操作にしない(Scene の上に出る Build の表示など)。
 */
export function start(rootId, frame, pointerDown, pointerMove, pointerUp, wheel, key, keyup, focus, canvasHostId, keyRootId, keyIgnore) {
  performance.mark("gl:start");
  dispose();
  const root = document.getElementById(rootId);
  if (!root) throw new Error("#" + rootId + " がありません");
  // canvas を入れる要素。Blazor が中身を描く要素(HUD などを子に持つ)に外から canvas を足すと、Blazor の子の数え方が狂うので、中身が空の別の要素を指定できる
  const host = (canvasHostId && document.getElementById(canvasHostId)) || root;
  const abort = new AbortController();
  const { signal } = abort;
  const canvas = document.createElement("canvas");
  canvas.style.cssText = "display:block;width:100%;height:100%";
  host.insertBefore(canvas, host.firstChild);
  const keyRoot = (keyRootId && document.getElementById(keyRootId)) || root;
  const prevTabIndex = keyRoot.getAttribute("tabindex");
  keyRoot.tabIndex = 0;
  const s = {
    prevTabIndex,
    root, keyRoot, canvas, abort, frame,
    gl: null, program: null, loc: null, buffers: new Map(), copies: new Map(), items: new Float32Array(0),
    raf: 0, onScreen: true, lost: false, disposed: false, pixelRatio: 1,
    stats: { frames: 0, drawn: 0, skippedHidden: 0, callbackMs: 0, lastDrawItems: 0, contextCreated: 0, contextLost: 0, contextRestored: 0 },
    observers: [],
  };
  state = s;

  canvas.addEventListener("webglcontextlost", (e) => { e.preventDefault(); s.lost = true; s.stats.contextLost++; }, { signal });
  canvas.addEventListener("webglcontextrestored", () => { if (init(s)) { restore(s); s.lost = false; s.stats.contextRestored++; render(s, performance.now()); } }, { signal });
  let ready = false;
  try {
    ready = init(s);
  } catch (e) {
    cleanupFailedStart(s);   // シェーダーのコンパイル・リンクの失敗など。canvas・リスナー・state を残さない
    throw e;
  }
  if (!ready) {
    cleanupFailedStart(s);
    return false;
  }
  performance.mark("gl:inited");

  const mods = (e) => (e.shiftKey ? 1 : 0) | (e.ctrlKey ? 2 : 0) | (e.altKey ? 4 : 0) | (e.metaKey ? 8 : 0);
  root.addEventListener("contextmenu", (e) => e.preventDefault(), { signal });
  const at = (e) => { const r = root.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
  // 押している間(down から up まで)の印。pointerup が来ないまま終わる場合(pointercancel・lostpointercapture)も、C# にドラッグの終わりを伝える
  let down = false, lastX = 0, lastY = 0;
  const finish = (x, y) => { if (!down) return; down = false; pointerUp(x, y); };
  // サイドバーの入力欄・ボタン・メニューの上の操作は、ビューの操作にしない
  const onUi = (e) => !!e.target.closest?.("input, button, [data-gl-ignore]");
  const keyIgnored = (e) => !!e.target.closest?.(keyIgnore || "input, button");
  root.addEventListener("pointerdown", (e) => {
    if (onUi(e)) return;
    keyRoot.focus({ preventScroll: true });
    root.setPointerCapture(e.pointerId);
    const [x, y] = at(e);
    lastX = x; lastY = y; down = true;
    if (pointerDown(e.button | (mods(e) << 4), x, y)) e.preventDefault();
  }, { signal });
  root.addEventListener("pointermove", (e) => { const [x, y] = at(e); lastX = x; lastY = y; pointerMove(x, y, mods(e)); }, { signal });
  root.addEventListener("pointerup", (e) => {
    if (root.hasPointerCapture(e.pointerId)) root.releasePointerCapture(e.pointerId);
    const [x, y] = at(e);
    finish(x, y);
  }, { signal });
  root.addEventListener("pointercancel", () => finish(lastX, lastY), { signal });
  root.addEventListener("lostpointercapture", () => finish(lastX, lastY), { signal });
  // ホイールは passive: false にしないと preventDefault できず、ページがスクロールしてしまう(試作ではここで失敗した)
  root.addEventListener("wheel", (e) => { if (wheel(e.deltaY)) e.preventDefault(); }, { passive: false, signal });
  keyRoot.addEventListener("keydown", (e) => { if (keyIgnored(e)) return; if (key(e.code, e.key, mods(e))) e.preventDefault(); }, { signal });
  keyRoot.addEventListener("keyup", (e) => { keyup(mods(e)); }, { signal });
  keyRoot.addEventListener("focus", () => focus(true), { signal });
  keyRoot.addEventListener("blur", () => focus(false), { signal });

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

/** 始めるのに失敗したとき: 作った canvas・リスナー・tabindex を戻して、state を空にする */
function cleanupFailedStart(s) {
  s.abort.abort();
  s.canvas.remove();
  restoreTabIndex(s);
  if (state === s) state = null;
}

function restoreTabIndex(s) {
  if (s.prevTabIndex === null) s.keyRoot.removeAttribute("tabindex"); else s.keyRoot.setAttribute("tabindex", s.prevTabIndex);
}

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
  s.loc = Object.fromEntries(["uProj", "uModelView", "uNormalMatrix", "uPointSize", "uShading", "uColor", "uLightDir", "uHemiDir", "uGround", "uHemi", "uDirI", "uRough"].map((n) => [n, gl.getUniformLocation(program, n)]));
  s.sky = null;   // スカイは、使うとき(Unity 風の画面)に作る
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
  const stride = LAYOUT_STRIDE[layout];
  if (stride === undefined || bytes.byteLength % (stride * 4) !== 0) {
    throw new Error(`uploadBuffer: 不正なバッファ(id ${id}, layout ${layout}, ${bytes.byteLength} バイト)`);
  }
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
  if (n < HEADER || (n - HEADER) % STRIDE !== 0 || (n - HEADER) / STRIDE < 0) throw new Error(`draw: 長さが合いません(${n} float)`);
  if (s.items.length < n) s.items = new Float32Array(Math.max(n, s.items.length * 2, 1024));
  bytes.copyTo(new Uint8Array(s.items.buffer, 0, n * 4));
  if (s.items[19] !== (n - HEADER) / STRIDE) throw new Error(`draw: 描画の数 ${s.items[19]} と長さ(${(n - HEADER) / STRIDE} 個ぶん)が合いません`);
  paint(s, s.items, n);
}

/** スカイ: Unity の既定の空に近いグラデーションの画像(2 x 256。three.js の CanvasTexture(sRGB・上下反転・ミップマップ)と同じ設定)を、画面いっぱいに貼る */
function drawSky(s) {
  const gl = s.gl;
  if (!s.sky) {
    const program = gl.createProgram();
    gl.attachShader(program, compile(gl, gl.VERTEX_SHADER, SKY_VERTEX));
    gl.attachShader(program, compile(gl, gl.FRAGMENT_SHADER, SKY_FRAGMENT));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
    const c = document.createElement("canvas");
    c.width = 2; c.height = 256;
    const g = c.getContext("2d");
    const grad = g.createLinearGradient(0, 0, 0, 256);
    grad.addColorStop(0, "#5f7ea3");
    grad.addColorStop(0.5, "#a9b8c8");
    grad.addColorStop(0.52, "#8a8580");
    grad.addColorStop(1, "#5a5652");
    g.fillStyle = grad;
    g.fillRect(0, 0, 2, 256);
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.SRGB8_ALPHA8, gl.RGBA, gl.UNSIGNED_BYTE, c);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.generateMipmap(gl.TEXTURE_2D);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    s.sky = { program, texture, vao: gl.createVertexArray(), tex: gl.getUniformLocation(program, "uTex") };
  }
  gl.disable(gl.DEPTH_TEST);
  gl.disable(gl.BLEND);
  gl.disable(gl.CULL_FACE);
  gl.depthMask(false);
  gl.useProgram(s.sky.program);
  gl.bindVertexArray(s.sky.vao);
  gl.activeTexture(gl.TEXTURE0);
  gl.bindTexture(gl.TEXTURE_2D, s.sky.texture);
  gl.uniform1i(s.sky.tex, 0);
  gl.drawArrays(gl.TRIANGLES, 0, 3);
  gl.bindVertexArray(null);
  gl.useProgram(s.program);
  gl.depthMask(true);
}

function paint(s, f) {
  const gl = s.gl;
  const count = f[19];
  const modes = PRIMITIVES(gl);
  const dpr = s.pixelRatio;
  // three.js は、描画バッファは floor(幅 × pixelRatio)、ビューポートは round(幅 × pixelRatio)(小数の DPR で 1 画素大きいことがある。はみ出した分は切れる)。同じにしないと、DPR 1.25 などで画面全体が 0.1 % ずれる
  gl.viewport(0, 0, s.vw ?? s.canvas.width, s.vh ?? s.canvas.height);
  gl.depthMask(true);
  gl.disable(gl.SCISSOR_TEST);
  gl.clearColor(BACKGROUND[0], BACKGROUND[1], BACKGROUND[2], 1);
  gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
  if (f[20] === 1) drawSky(s);   // 背景: 1 = スカイ(0 = 単色)
  gl.uniformMatrix4fv(s.loc.uProj, false, f.subarray(0, 16));
  gl.uniform3f(s.loc.uLightDir, f[21], f[22], f[23]);
  gl.uniform3f(s.loc.uHemiDir, f[28], f[29], f[30]);
  gl.uniform1f(s.loc.uGround, f[24]);
  gl.uniform1f(s.loc.uHemi, f[25]);
  gl.uniform1f(s.loc.uDirI, f[26]);
  gl.uniform1f(s.loc.uRough, f[27]);
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
    if (flags & 64) { gl.enable(gl.CULL_FACE); gl.cullFace(gl.BACK); } else if (!(flags & 32)) gl.disable(gl.CULL_FACE);   // 64 = 表だけ(FrontSide)
    gl.uniformMatrix4fv(s.loc.uModelView, false, f.subarray(o + 28, o + 44));
    gl.uniformMatrix3fv(s.loc.uNormalMatrix, false, f.subarray(o + 44, o + 53));
    gl.uniform1i(s.loc.uShading, f[o + 2]);
    gl.uniform4f(s.loc.uColor, f[o + 8], f[o + 9], f[o + 10], f[o + 6]);
    gl.uniform1f(s.loc.uPointSize, f[o + 7] * dpr);
    if (flags & 32) {
      // 透明で両面: 裏の面 → 表の面の 2 回(three.js と同じ。frontFace を先に決めてあるので、鏡映でも「裏」は裏)
      gl.enable(gl.CULL_FACE);
      gl.cullFace(gl.FRONT);
      gl.drawArrays(modes[f[o + 1]], 0, b.count);
      gl.cullFace(gl.BACK);
      gl.drawArrays(modes[f[o + 1]], 0, b.count);
      gl.disable(gl.CULL_FACE);
    } else {
      gl.drawArrays(modes[f[o + 1]], 0, b.count);
    }
  }
  gl.bindVertexArray(null);
  s.stats.drawn++;
  if (s.stats.drawn === 1) performance.mark("gl:firstPaint");
  s.stats.lastDrawItems = count;
}

/** 1 フレーム: 大きさを合わせて、C# に描かせる */
function render(s) {
  if (s.disposed || s.lost) return;
  const { root, canvas } = s;
  const w = root.clientWidth, h = root.clientHeight;
  if (w === 0 || h === 0) return;   // 大きさが付いたとき(ResizeObserver)に描く
  s.pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  const cw = Math.floor(w * s.pixelRatio), ch = Math.floor(h * s.pixelRatio);
  if (canvas.width !== cw) canvas.width = cw;
  if (canvas.height !== ch) canvas.height = ch;
  s.vw = Math.round(w * s.pixelRatio);
  s.vh = Math.round(h * s.pixelRatio);
  const t0 = performance.now();
  s.stats.frames++;
  // 座標の変換は getBoundingClientRect の小数の幅・高さ(viewport.js の ndc / toScreen と同じ)。C# が、縦横比には丸めた整数を使う(resize() は clientWidth / clientHeight)
  const r = root.getBoundingClientRect();
  s.frame(r.width, r.height, w * 65536 + h);
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
    if (s.sky) { s.gl.deleteProgram(s.sky.program); s.gl.deleteTexture(s.sky.texture); s.gl.deleteVertexArray(s.sky.vao); }
    s.gl.getExtension("WEBGL_lose_context")?.loseContext();
  }
  s.copies.clear();
  s.canvas.remove();
  restoreTabIndex(s);
}

/** レンダーラボ(gl-lab.js)が、状態の要約・コンテキストの喪失の確認に使う。サイトの画面は呼ばない */
export function inspectState() { return state; }
