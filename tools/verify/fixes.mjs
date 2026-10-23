import { launch, newPage } from "./lib.mjs";
const B = process.env.NEW || "http://127.0.0.1:8787";
const b = await launch(); let fails = 0;
const ok = (c, m) => { if (!c) { fails++; console.log("FAIL", m); } else console.log("ok  ", m); };
{ // content.json が {} → 起動せず、エラー表示・静的本文が残る
  const p = await newPage(b);
  await p.route("**/data/content.json", (r) => r.fulfill({ status: 200, contentType: "application/json", body: "{}" }));
  await p.goto(B + "/links"); await p.waitForFunction(() => !document.getElementById("boot-indicator"), null, { timeout: 15000 });
  ok(await p.evaluate(() => getComputedStyle(document.getElementById("blazor-error-ui")).display) === "block", "content {}: error ui shown");
  ok((await p.locator(".link-item").count()) > 50 && (await p.locator(".mud-layout").count()) === 0, "content {}: static body stays, app not mounted");
  await p.context().close();
}
{ // filter.json 失敗 → 入力しても全件のまま
  const p = await newPage(b);
  await p.route("**/data/filter.json", (r) => r.fulfill({ status: 404, body: "x" }));
  await p.goto(B + "/tips"); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(500);
  const n = await p.locator("[data-tip-item]").count();
  await p.locator(".tips-filter input").fill("ピンク"); await p.waitForTimeout(300);
  ok((await p.evaluate(() => [...document.querySelectorAll("[data-tip-item]")].filter((e) => !e.hidden).length)) === n, "filter.json missing: typing keeps all items");
  await p.context().close();
}
{ // 未知の記事の URL(直接) → サイドバーに強調なし
  const p = await newPage(b);
  await p.goto(B + "/learn/basics/no-such-article"); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(500);
  ok(/見つかりません/.test(await p.locator(".fsk-content").innerText()), "unknown article: not found view");
  ok((await p.locator("a.mud-nav-link.active").count()) === 0, "unknown article: no active sidebar item");
  await p.context().close();
}
{ // #id: 見えている項目では絞り込みを外さない
  const p = await newPage(b);
  await p.goto(B + "/tips?tag=Unity"); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(300);
  const id = await p.evaluate(() => [...document.querySelectorAll("[data-tip-item]")].find((e) => !e.hidden).querySelector("article").id);
  await p.goto(B + "/tips?tag=Unity#" + id); await p.waitForSelector(".mud-layout"); await p.waitForTimeout(600);
  ok((await p.locator(".tips-filter button.btn-primary").innerText()).trim() === "Unity", "#id on a visible item keeps the tag filter");
  await p.context().close();
}
console.log(fails ? `${fails} FAILED` : "all ok"); await b.close();
