// 静的 HTML(WebAssembly を止めた状態)と、起動後の本文(#fsk-main の中)の DOM の差。 NEW=... node dom-diff.mjs [パス...]   ALL=1 で全 76 ページ
// 比べ方: Blazor のコメント(<!--!-->)・タグの前後の空白・絞り込みのボタンの data-tag(静的だけが持つ)の違いを無視し、JavaScript が起動後に中身を入れる枠(3D・チュートリアル・進捗の印・絞り込み)は、枠の中身を落として比べる。
// 差があれば、最初の差の前後を出す。練習 3 ページが既定(PR #91 のレビューで、/practice/unity の差の原因を調べるため)。
import { launch, newPage } from "./lib.mjs";
const NEW = (process.env.NEW || "http://127.0.0.1:8787").replace(/\/$/, "");
let paths = process.argv.slice(2);
if (paths.length === 0) paths = ["/practice", "/practice/unity", "/practice/room"];
if (process.env.ALL === "1") {
  const content = await (await fetch(NEW + "/data/content.json")).json();
  paths = ["/", "/learn", "/shortcuts", "/addons", "/practice", "/practice/room", "/practice/unity", "/glossary", "/howto", "/tips", "/qa", "/links", "/changelog", "/report", "/privacy", "/search",
    ...content.categories.map((c) => `/learn/${c.id}`), ...content.articles.map((a) => a.url)];
}
const grab = () => {
  const main = document.querySelector("#fsk-main");
  if (!main) return null;
  const copy = main.cloneNode(true);
  // JavaScript が起動後に中身を入れる枠は、中身を落とす
  for (const sel of [".practice-embed", "[data-scenario]", "#practice-viewport", "#unity-practice", "#room-viewport", "#practice-tasks", "#unity-tasks", ".category-progress", "[data-progress-page] .badge-done", ".fsk-notice"]) copy.querySelectorAll(sel).forEach((e) => { e.innerHTML = ""; e.removeAttribute("class"); e.removeAttribute("data-engine"); });
  return copy.innerHTML.replace(/<!--.*?-->/g, "").replace(/ data-tag="[^"]*"/g, "").replace(/\s+/g, " ").replace(/\s*([<>])\s*/g, "$1").replace(/ (class|id|style)=""/g, "").trim();
};
const b = await launch();
let bad = 0;
for (const path of paths) {
  const s = await newPage(b, { width: 1280, height: 800 });
  await s.route("**/_framework/blazor.webassembly.js", (r) => r.abort());
  await s.goto(NEW + path, { waitUntil: "load" });
  const before = await s.evaluate(grab);
  await s.context().close();
  const a = await newPage(b, { width: 1280, height: 800 });
  await a.goto(NEW + path, { waitUntil: "load" });
  await a.waitForSelector(".fsk-page", { timeout: 60000 });
  await a.waitForTimeout(1200);
  const after = await a.evaluate(grab);
  await a.context().close();
  if (before === after) { console.log("ok  " + path); continue; }
  bad++;
  let i = 0; while (i < before.length && i < after.length && before[i] === after[i]) i++;
  console.log(`DIFF ${path} (静的 ${before.length} 文字 / 起動後 ${after.length} 文字)\n  静的   : …${before.slice(Math.max(0, i - 60), i + 140)}\n  起動後 : …${after.slice(Math.max(0, i - 60), i + 140)}`);
}
await b.close();
console.log(bad ? `差あり ${bad} / ${paths.length}` : `全 ${paths.length} ページ、差なし`);
process.exit(bad ? 1 : 0);
