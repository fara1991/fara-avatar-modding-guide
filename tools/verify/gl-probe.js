// 「新しいブラウザコンテキストで描画が始まらない」の切り分け用(docs/phase3b-design.md §10.1)。  /probe.html?engine=raw|three
//   raw   = canvas + WebGL 2 + requestAnimationFrame だけ(.NET も three.js も無し)
//   three = 現行の viewport.js(three.js)
// window.__probeStart() を呼ぶと始まり、window.__probeRead() が 3 秒ぶんなどの観測値を返す。
const engine = new URLSearchParams(location.search).get("engine") || "raw";
const events = [];
const log = (type, detail = "") => events.push({ t: Math.round(performance.now()), type, detail });
const state = { engine, rafCalls: 0, firstRafAt: null, lastRafAt: null, vis: [], ctxResult: null, renderer: null };

document.addEventListener("visibilitychange", () => log("visibilitychange", document.visibilityState));
const frame = document.getElementById("frame");

const origRaf = window.requestAnimationFrame.bind(window);
window.requestAnimationFrame = (fn) => origRaf((t) => { state.rafCalls++; state.firstRafAt ??= performance.now(); state.lastRafAt = performance.now(); fn(t); });

async function startRaw() {
  const canvas = document.createElement("canvas");
  canvas.style.cssText = "display:block;width:100%;height:100%";
  frame.append(canvas);
  canvas.addEventListener("webglcontextcreationerror", (e) => log("webglcontextcreationerror", e.statusMessage));
  canvas.addEventListener("webglcontextlost", () => log("webglcontextlost"));
  canvas.addEventListener("webglcontextrestored", () => log("webglcontextrestored"));
  const gl = canvas.getContext("webgl2", { antialias: true, alpha: false });
  state.ctxResult = gl ? "ok" : "null";
  if (!gl) return;
  const ext = gl.getExtension("WEBGL_debug_renderer_info");
  state.renderer = ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : "(no debug info)";
  let n = 0;
  const loop = () => {
    requestAnimationFrame(loop);
    canvas.width = canvas.clientWidth; canvas.height = canvas.clientHeight;
    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.clearColor((n++ % 60) / 60, 0.3, 0.3, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);
  };
  loop();
}

async function startThree() {
  const { Viewport } = await import("/js/viewport.js");
  const vp = new Viewport(frame, { scene: [{ kind: "cube" }] });
  const canvas = frame.querySelector("canvas");
  state.ctxResult = canvas ? "canvas" : "no canvas";
  const gl = canvas?.getContext("webgl2");
  state.renderer = gl ? (gl.getExtension("WEBGL_debug_renderer_info") ? gl.getParameter(gl.getExtension("WEBGL_debug_renderer_info").UNMASKED_RENDERER_WEBGL) : "?") : "(context not readable)";
  window.__vp = vp;
}

window.__probeStart = async () => { await (engine === "three" ? startThree() : startRaw()); };
window.__probeRead = () => {
  const canvas = frame.querySelector("canvas");
  const gl = canvas?.getContext(engine === "raw" ? "webgl2" : "webgl2");
  return {
    engine, now: Math.round(performance.now()),
    visibilityState: document.visibilityState, hidden: document.hidden, hasFocus: document.hasFocus(),
    rafCalls: state.rafCalls, rafSpanMs: state.firstRafAt == null ? null : Math.round(state.lastRafAt - state.firstRafAt),
    ctx: state.ctxResult, renderer: state.renderer, lost: gl ? gl.isContextLost() : null,
    canvas: canvas ? { w: canvas.width, h: canvas.height, cw: canvas.clientWidth, ch: canvas.clientHeight } : null,
    events,
  };
};
