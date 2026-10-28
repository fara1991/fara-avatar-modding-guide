// maintenance-deploy.mjs のテスト。  node --test .github/scripts/maintenance-deploy.test.mjs
// 4 つのリポジトリで同じファイルなので、このテストも同じ内容にしている。
import { test } from "node:test";
import assert from "node:assert/strict";
import { checkMessage, settingLine, describeBinding, summaryText, MESSAGE_MAX } from "./maintenance-deploy.mjs";

test("checkMessage: 200 文字までは通し、201 文字は止める（絵文字は 1 文字として数える）", () => {
  assert.equal(checkMessage(""), null);
  assert.equal(checkMessage("a".repeat(MESSAGE_MAX)), null);
  assert.match(checkMessage("a".repeat(MESSAGE_MAX + 1)), /200 文字/);
  assert.equal(checkMessage("😀".repeat(MESSAGE_MAX)), null);
  assert.match(checkMessage("😀".repeat(MESSAGE_MAX + 1)), /201 文字/);
});

test("checkMessage: 改行（LF・CR）は止める", () => {
  assert.match(checkMessage("一行目\n二行目"), /改行/);
  assert.match(checkMessage("一行目\r二行目"), /改行/);
});

test("checkMessage: 改行は pokemon と同じ集合（NEL・行区切り U+2028・段落区切り U+2029）も止める", () => {
  for (const cp of [0x85, 0x2028, 0x2029]) {
    assert.match(checkMessage(`一行目${String.fromCodePoint(cp)}二行目`), /改行/, cp.toString(16));
  }
  assert.equal(checkMessage("タブ\tは通す"), null);
});

test("settingLine: 空は「変更なし」、バッククォートは \" に直す", () => {
  assert.equal(settingLine("", ""), "- 設定した値: MAINTENANCE_MODE=`(変更なし)` / MAINTENANCE_MESSAGE=`(変更なし)`");
  assert.equal(settingLine("notice", "a`b`c"), "- 設定した値: MAINTENANCE_MODE=`notice` / MAINTENANCE_MESSAGE=`a\"b\"c`");
});

test("describeBinding: 文字の変数は JSON の文字（バッククォートは ' に）。シークレットは (型)。無ければ (未設定)", () => {
  const bindings = [{ name: "MAINTENANCE_MODE", type: "plain_text", text: "notice" }, { name: "X", type: "secret_text" }, { name: "M", type: "plain_text", text: "a`b" }];
  assert.equal(describeBinding(bindings, "MAINTENANCE_MODE"), "\"notice\"");
  assert.equal(describeBinding(bindings, "X"), "(secret_text)");
  assert.equal(describeBinding(bindings, "M"), "\"a'b\"");
  assert.equal(describeBinding(bindings, "NONE"), "(未設定)");
});

test("summaryText: 反映後の値を読んで出す。取得できないときも、設定した値の行は出し、トークンは出さない", async () => {
  const env = { WORKER_NAME: "fara-avatar-dev", CLOUDFLARE_ACCOUNT_ID: "acc", CLOUDFLARE_API_TOKEN: "super-secret", MAINTENANCE_MODE_INPUT: "notice", MAINTENANCE_MESSAGE_INPUT: "更新します" };
  const ok = await summaryText(env, { fetchImpl: async (url, init) => {
    assert.equal(url, "https://api.cloudflare.com/client/v4/accounts/acc/workers/scripts/fara-avatar-dev/settings");
    assert.equal(init.headers.Authorization, "Bearer super-secret");
    return new Response(JSON.stringify({ result: { bindings: [{ name: "MAINTENANCE_MODE", type: "plain_text", text: "notice" }] } }));
  } });
  assert.match(ok, /### メンテナンス表示 \(fara-avatar-dev\)/);
  assert.match(ok, /反映後の現在値: MAINTENANCE_MODE=`"notice"` \/ MAINTENANCE_MESSAGE=`\(未設定\)`/);
  const failed = await summaryText(env, { fetchImpl: async () => { throw new Error("network super-secret"); } });
  assert.match(failed, /取得できませんでした/);
  assert.match(failed, /設定した値/);
  assert.ok(!failed.includes("super-secret"));
});
