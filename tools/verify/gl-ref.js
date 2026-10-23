// 現行の three.js 版のビューポート(viewport.js)だけを出す参照ページ。レンダーラボ(/lab/)と同じ ?scene=…&view=… で最初のシーンを決める。
import { Viewport } from "/js/viewport.js";
const q = new URLSearchParams(location.search);
const options = {};
if (q.get("scene")) options.scene = JSON.parse(q.get("scene"));
if (q.get("view")) options.view = JSON.parse(q.get("view"));
const vp = new Viewport(document.getElementById("viewport"), options);
if (q.get("angles")) { const [theta, phi] = JSON.parse(q.get("angles")); vp.view.theta = theta; vp.view.phi = phi; }
window.__vp = vp;
window.__ready = true;
