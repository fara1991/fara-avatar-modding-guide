// 現行の viewport.js を、ブラウザ無しの Node で「本物のコンストラクタごと」動かすための代用品(practice-reference.mjs から使う)。
// 描画(WebGL)・画面の大きさの監視・描画のループ・DOM の要素だけを、同じ形で動く小さな代用品にする。three.js の数学・メッシュ・レイ判定・選択・取り消しなど、
// 動作に関わるコードは本物をそのまま動かす。
//   ・WebGLRenderer … 何も描かない。render() は本物と同じく scene.updateMatrixWorld() と camera.updateMatrixWorld() を呼ぶ(matrixWorld を最新にする)
//   ・requestAnimationFrame … 呼び出しを保存するだけ。pumpFrame(vp) で 1 フレーム進める(本物の animate が走る: カメラの更新・描画)
//   ・DOM … 要素の代用品(El)。HUD の文字・hidden・メニューのボタン・N パネルの入力欄・ツールのボタンを、本物のコードが触れる形で持つ
//   ・crypto.getRandomValues … 決まった列(部屋の ID を JS と C# で突き合わせるため)

/** 画面に出る要素の代用品 */
export class El {
  constructor(tag = "div", init = {}) {
    this.tagName = tag;
    this.children = [];
    this.parent = null;
    this.dataset = {};
    this.style = {};
    this.attrs = {};
    this.handlers = {};
    this.hidden = false;
    this.textContent = "";
    this.value = "";
    this.disabled = false;
    this.className = "";
    this.tabIndex = 0;
    const set = new Set();
    this.classList = {
      add: (...c) => c.forEach(x => set.add(x)),
      remove: (...c) => c.forEach(x => set.delete(x)),
      contains: c => set.has(c),
      toggle: (c, force) => { const on = force ?? !set.has(c); if (on) set.add(c); else set.delete(c); return on; },
    };
    Object.assign(this, init);
  }
  addEventListener(type, fn) { this.handlers[type] = fn; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  appendChild(c) { c.parent = this; this.children.push(c); return c; }
  insertBefore(c) { c.parent = this; this.children.unshift(c); return c; }
  replaceChildren(...nodes) { this.children = nodes; }
  insertAdjacentHTML() { }
  remove() { if (this.parent) this.parent.children = this.parent.children.filter(c => c !== this); this.parent = null; }
  focus() { document.activeElement = this; }
  closest() { return null; }
  get offsetWidth() { return 0; }
  get offsetHeight() { return 0; }
}

/** 表示している文字の行(setLines は文字と <br> を交互に置く) */
export function linesOf(el) { return el.children.filter(c => typeof c === "string"); }

const TOOLS = ["vert", "edge", "face", "xray", "stats", "mirror"];
const PROPS = [["position", "x"], ["position", "y"], ["position", "z"], ["rotation", "x"], ["rotation", "y"], ["rotation", "z"], ["scale", "x"], ["scale", "y"], ["scale", "z"]];

/** viewport.js の HUD_HTML に当たる要素を作って、root.querySelector で引けるようにする */
export function makeRoot(rect) {
  const root = new El("div");
  root.rect = { ...rect };
  root.clientWidth = rect.width;
  root.clientHeight = rect.height;
  root.getBoundingClientRect = () => ({ left: root.rect.left, top: root.rect.top, width: root.rect.width, height: root.rect.height });
  root.setPointerCapture = () => { };
  const q = {
    ".vp-mode": new El("div", { textContent: "オブジェクトモード" }),
    ".vp-view": new El("div", { textContent: "ユーザー・透視投影" }),
    ".vp-stats": new El("div", { hidden: true }),
    ".vp-status": new El("span"),
    ".vp-numeric": new El("span"),
    ".vp-tools": new El("div"),
    ".vp-tools-edit": new El("span", { hidden: true }),
    ".practice-npanel": new El("div", { hidden: true }),
    ".vp-marker": new El("div", { hidden: true }),
    ".vp-marker-label": new El("span"),
    ".vp-remote": null,
  };
  const tools = TOOLS.map(t => new El("button", { dataset: { tool: t } }));
  q[".vp-tools"].querySelectorAll = sel => (sel === "[data-tool]" ? tools : []);
  q[".vp-tools"].querySelector = sel => tools.find(b => sel === `[data-tool="${b.dataset.tool}"]`) ?? null;
  const name = new El("div", { textContent: "—" });
  const inputs = PROPS.map(([prop, axis]) => new El("input", { dataset: { prop, axis } }));
  const np = q[".practice-npanel"];
  np.querySelector = sel => (sel === ".vp-npanel-name" ? name : null);
  np.querySelectorAll = sel => (sel === "input" ? inputs : []);
  root.querySelector = sel => q[sel] ?? null;
  root.parts = { ...q, tools, inputs, name };
  return root;
}

/** グローバルの代用品を入れる(viewport.js を読み込む前に呼ぶ)。 */
export function installGlobals() {
  globalThis.window = { devicePixelRatio: 1 };
  globalThis.document = { createElement: tag => new El(tag), activeElement: null, getElementById: id => globalThis.__elements?.[id] ?? null };
  const frames = new Map();
  let nextId = 1;
  globalThis.requestAnimationFrame = fn => { const id = nextId++; frames.set(id, fn); return id; };
  globalThis.cancelAnimationFrame = id => { frames.delete(id); };
  globalThis.__frames = frames;
  class Observer { observe() { } disconnect() { } }
  globalThis.ResizeObserver = Observer;
  globalThis.IntersectionObserver = Observer;
  globalThis.__practiceFakeRenderer = class {
    constructor() { this.domElement = new El("canvas"); }
    setPixelRatio() { }
    setSize() { }
    dispose() { }
    forceContextLoss() { }
    render(scene, camera) {
      scene.updateMatrixWorld();
      if (camera.parent === null) camera.updateMatrixWorld();
    }
  };
}

/** 部屋の ID(randomId)に使う、決まった乱数: 32 ビットの線形合同法(C# の側も同じ式) */
export function seedRandomIds(seed) {
  let state = seed >>> 0;
  Object.defineProperty(globalThis, "crypto", {
    configurable: true,
    value: {
      getRandomValues(a) {
        for (let i = 0; i < a.length; i++) { state = (Math.imul(state, 1103515245) + 12345) >>> 0; a[i] = (state >>> 16) & 0xff; }
        return a;
      },
    },
  });
}

/** 保存した描画のループを 1 回進める(本物の animate が走る) */
export function pumpFrame(vp) {
  const fn = globalThis.__frames.get(vp.raf);
  if (!fn) return;
  globalThis.__frames.delete(vp.raf);
  fn(0);
}
