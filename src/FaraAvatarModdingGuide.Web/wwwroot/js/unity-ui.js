// Unity 風の画面(C# 版。Components/UnityEditorView.razor。Phase 3b S5)の、DOM を測る・ドラッグする部分。画面(Razor)の状態は C# が持つので、ここは unity.js の次の部分だけを受け持つ:
//   bind / unbind    … Hierarchy・Project のポインター操作(▶ の開閉・クリック・右クリック・ドラッグで追加 / 付け替え / マテリアルの設定)。ドラッグ中の影・ドロップ先の枠も作る(beginDrag / dropTarget)
//   positionPopup    … ポップアップを、実際の大きさで、エディターからはみ出さない位置に出す(showPopup)
//   hidePopup        … 閉じたあと、次に出すまで見えなくする(古い位置のまま一瞬出ないように)
//   placeLabel       … 案内の吹き出し(.ue-hl-label)を、光らせている要素の上に置く(applyHighlight)
//   focusRoot / elementExists
// C# へは、Razor 部品の [JSInvokable] を DotNetObjectReference で呼ぶ(ref.invokeMethodAsync)。

const bound = new Map();

export function elementExists(id) {
  return !!document.getElementById(id);
}

/** ?debug=1 のとき、C# 版の状態(視点・カメラ・ノードの位置など)を window.__unityCs に置く(tools/verify/unity-compare.mjs が JS 版の window.__unity と比べる) */
export function setDebug(json) {
  window.__unityCs = JSON.parse(json);
}

export function setDebugGeo(json) {
  window.__unityCsGeo = JSON.parse(json);
}

export function setDebugFrame(json) {
  window.__unityCsFrame = JSON.parse(json);
}

export function focusRoot(id) {
  document.getElementById(id)?.focus({ preventScroll: true });
}

export function bind(rootId, ref) {
  unbind(rootId);
  const root = document.getElementById(rootId);
  if (!root) return;
  const abort = new AbortController();
  const { signal } = abort;
  const popup = root.querySelector(".ue-popup");
  if (popup) popup.style.visibility = "hidden";
  root.addEventListener("pointerdown", (e) => {
    if (e.target.closest(".ue-hier-list")) onHierarchy(root, ref, e, signal);
    else if (e.target.closest(".ue-proj-grid")) onProject(root, ref, e, signal);
  }, { signal });
  bound.set(rootId, { abort });
}

export function unbind(rootId) {
  const b = bound.get(rootId);
  if (!b) return;
  b.abort.abort();
  bound.delete(rootId);
}

// ---------------------------------------------------------------- Hierarchy・Project のポインター
function onHierarchy(root, ref, e, signal) {
  const fold = e.target.closest("[data-fold]");
  const row = e.target.closest("[data-node]");
  if (e.button === 2 && row) { e.preventDefault(); ref.invokeMethodAsync("ContextAt", row.dataset.node, e.clientX, e.clientY); return; }
  if (e.button !== 0) return;
  if (fold && fold.querySelector("i")) { ref.invokeMethodAsync("ToggleFold", fold.dataset.fold); return; }
  if (row) { beginDrag(root, ref, e, { kind: "node", id: row.dataset.node, label: row.querySelector(".ue-row-name")?.textContent ?? "" }, signal); return; }
  ref.invokeMethodAsync("ClearSelection");
}

function onProject(root, ref, e, signal) {
  const a = e.target.closest("[data-asset]");
  if (a && e.button === 0) beginDrag(root, ref, e, { kind: "asset", asset: a.dataset.asset, label: a.querySelector("span")?.textContent ?? "" }, signal);
}

/** ドラッグ(Hierarchy・Project → Hierarchy / Inspector / Scene)。5 px 動くまではクリック */
function beginDrag(root, ref, e, payload, signal) {
  const start = { x: e.clientX, y: e.clientY };
  const host = root.querySelector(".ue-ghost-host") ?? root;
  let ghost = null;
  const clearDrop = () => { for (const el of root.querySelectorAll(".ue-drop")) el.classList.remove("ue-drop"); };
  const move = (ev) => {
    if (!ghost && Math.hypot(ev.clientX - start.x, ev.clientY - start.y) < 5) return;
    if (!ghost) {
      ghost = document.createElement("div");
      ghost.className = "ue-ghost";
      ghost.textContent = payload.label;
      host.appendChild(ghost);
    }
    const base = root.getBoundingClientRect();
    ghost.style.left = `${ev.clientX - base.left + 10}px`;
    ghost.style.top = `${ev.clientY - base.top + 6}px`;
    clearDrop();
    dropTarget(root, ev)?.el?.classList.add("ue-drop");
  };
  const up = (ev) => {
    window.removeEventListener("pointermove", move);
    window.removeEventListener("pointerup", up);
    clearDrop();
    if (!ghost) {
      // ドラッグしなかった = クリック
      if (payload.kind === "node") ref.invokeMethodAsync("SelectRow", payload.id);
      else ref.invokeMethodAsync("ClickAsset", payload.asset);
      return;
    }
    ghost.remove();
    const target = dropTarget(root, ev);
    if (!target) return;
    if (payload.kind === "node") {
      if (target.kind === "node" || target.kind === "root") ref.invokeMethodAsync("DropNode", payload.id, target.kind, target.id ?? null);
      return;
    }
    const sv = root.querySelector(".ue-scene-view").getBoundingClientRect();
    ref.invokeMethodAsync("DropAsset", payload.asset, target.kind, target.id ?? null, target.slot ?? 0, ev.clientX - sv.left, ev.clientY - sv.top);
  };
  window.addEventListener("pointermove", move, { signal });
  window.addEventListener("pointerup", up, { signal });
}

function dropTarget(root, ev) {
  const el = document.elementFromPoint(ev.clientX, ev.clientY);
  if (!el || !root.contains(el)) return null;
  const row = el.closest("[data-node]");
  if (row) return { kind: "node", id: row.dataset.node, el: row };
  if (el.closest(".ue-hierarchy")) return { kind: "root", el: root.querySelector(".ue-hier-list") };
  const slot = el.closest("[data-slot]");
  if (slot) return { kind: "slot", slot: +slot.dataset.slot, el: slot };
  if (el.closest(".ue-inspector")) return { kind: "inspector", el: root.querySelector(".ue-insp-body") };
  if (el.closest(".ue-scene-view")) return { kind: "scene", el: root.querySelector(".ue-scene-view") };
  return null;
}

// ---------------------------------------------------------------- ポップアップ・案内の吹き出し
/** ポップアップを、出す位置の決め方(point = クライアント座標 / below = 要素の左下 / row = Hierarchy の行の左 + 40 と下)から、実際の大きさで、はみ出さない位置に置く(unity.js の showPopup) */
export function positionPopup(rootId, kind, anchorKind, x, y, selector) {
  const root = document.getElementById(rootId);
  const p = root?.querySelector(".ue-popup");
  if (!p) return;
  let ax = x, ay = y;
  if (anchorKind === "below") {
    const el = root.querySelector(selector);
    if (el) { const r = el.getBoundingClientRect(); ax = r.left; ay = r.bottom; }
  } else if (anchorKind === "row") {
    const el = root.querySelector(`[data-node="${CSS.escape(selector)}"]`) ?? root.querySelector(".ue-hier-list");
    const r = el.getBoundingClientRect();
    ax = r.left + 40; ay = r.bottom;
  }
  const base = root.getBoundingClientRect();
  let w = p.offsetWidth, h = p.offsetHeight;
  if (kind === "add") {
    // unity.js は、一覧(.ue-addlist)を入れる前に位置を決める。その大きさを、複製で測る
    const clone = p.cloneNode(true);
    clone.removeAttribute("id");
    clone.querySelector(".ue-addlist")?.replaceChildren();
    clone.style.cssText = "visibility:hidden;left:0;top:0";
    p.parentNode.insertBefore(clone, p);
    w = clone.offsetWidth; h = clone.offsetHeight;
    clone.remove();
  }
  p.style.left = `${Math.max(0, Math.min(ax - base.left, base.width - w - 4))}px`;
  p.style.top = `${Math.max(0, Math.min(ay - base.top, base.height - h - 4))}px`;
  p.style.visibility = "";
  if (kind === "add") p.querySelector(".ue-addsearch")?.focus();
}

export function hidePopup(rootId) {
  const p = document.getElementById(rootId)?.querySelector(".ue-popup");
  if (p) p.style.visibility = "hidden";
}

/** 案内の吹き出しを、光らせている要素の上に置く(unity.js の applyHighlight) */
export function placeLabel(rootId, kind, key) {
  const root = document.getElementById(rootId);
  const label = root?.querySelector(".ue-hl-label");
  if (!label) return;
  const q = (s) => root.querySelector(s);
  const esc = (s) => CSS.escape(s);
  const target =
    kind === "hierarchy" ? q(`[data-node="${esc(key)}"]`) :
    kind === "project" ? q(`[data-asset="${esc(key)}"]`) :
    kind === "inspector" ? q(`.ue-insp-body [data-hl="${esc(key)}"]`) :
    kind === "menu" ? q(`[data-menu="${esc(key)}"]`) :
    kind === "sdk" ? q(`.ue-sdk [data-hl="${esc(key)}"]`) :
    kind === "tool" ? q(`[data-tool="${esc(key)}"]`) :
    kind === "popup" ? q(`.ue-popup [data-hl="${esc(key)}"]`) :
    kind === "scene" ? q(".ue-scene-view") : null;
  if (!target || target.offsetParent === null) return;
  const r = target.getBoundingClientRect(), base = root.getBoundingClientRect();
  label.style.left = `${Math.max(4, Math.min(r.left - base.left + r.width / 2, base.width - 60))}px`;
  label.style.top = `${Math.max(2, r.top - base.top - 4)}px`;
}
