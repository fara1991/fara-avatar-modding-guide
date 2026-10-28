// デプロイ（deploy.yml・deploy-develop.yml）で使う、メンテナンス表示の入力の検査と、ジョブサマリーの中身。
//
//   node .github/scripts/maintenance-deploy.mjs check-message   … MAINTENANCE_MESSAGE_INPUT を確かめる（200 文字まで・改行なし）。違えば 1 で止める（--var に渡す前）
//   node .github/scripts/maintenance-deploy.mjs summary         … 設定した値と反映後の現在値を標準出力に書く（呼び出し側が $GITHUB_STEP_SUMMARY へ追記する）。取得できなくても 0
//
// 環境変数: MAINTENANCE_MESSAGE_INPUT, MAINTENANCE_MODE_INPUT, WORKER_NAME, CLOUDFLARE_ACCOUNT_ID, CLOUDFLARE_API_TOKEN
// トークンは出さない。文面は出してよい（バッククォートは " に直す。反映後の値のバッククォートは ' に直す）。

export const MESSAGE_MAX = 200;

/** 文面の検査。問題があれば理由の文、無ければ null（空は「変更しない」なので問題なし）。 */
export function checkMessage(message) {
  const length = [...message].length;   // 文字数はコードポイントで数える（絵文字 1 つ = 1 文字）
  if (length > MESSAGE_MAX) return `メンテナンスの文は ${MESSAGE_MAX} 文字までです（いま ${length} 文字）`;
  if (/[\r\n]/.test(message)) return "メンテナンスの文に改行は入れられません";
  return null;
}

/** 設定した値の行（手動実行の入力そのもの。空は「変更なし」）。 */
export function settingLine(mode, message) {
  const msg = message.replaceAll("`", "\"");
  return `- 設定した値: MAINTENANCE_MODE=\`${mode || "(変更なし)"}\` / MAINTENANCE_MESSAGE=\`${msg || "(変更なし)"}\``;
}

/** Worker の設定（bindings）から、名前の値を表示用の文字にする。文字の変数は JSON の文字、シークレットなどは (型)、無ければ (未設定)。 */
export function describeBinding(bindings, name) {
  const binding = bindings.find((b) => b && b.name === name);
  if (!binding) return "(未設定)";
  return binding.type === "plain_text" ? JSON.stringify(String(binding.text ?? "")).replaceAll("`", "'") : `(${binding.type})`;
}

/**
 * ジョブサマリーの中身を作る。反映後の値は Cloudflare の API から読む（失敗しても、設定した値の行は出す）。
 * @returns {Promise<string>} Markdown の行（改行で終わる）
 */
export async function summaryText(env, { fetchImpl = fetch } = {}) {
  const name = env.WORKER_NAME ?? "";
  const lines = [`### メンテナンス表示 (${name})`, settingLine(env.MAINTENANCE_MODE_INPUT ?? "", env.MAINTENANCE_MESSAGE_INPUT ?? "")];
  try {
    const url = `https://api.cloudflare.com/client/v4/accounts/${encodeURIComponent(env.CLOUDFLARE_ACCOUNT_ID ?? "")}/workers/scripts/${encodeURIComponent(name)}/settings`;
    const res = await fetchImpl(url, { headers: { Authorization: `Bearer ${env.CLOUDFLARE_API_TOKEN ?? ""}` } });
    const bindings = (await res.json())?.result?.bindings ?? [];
    lines.push(`- 反映後の現在値: MAINTENANCE_MODE=\`${describeBinding(bindings, "MAINTENANCE_MODE")}\` / MAINTENANCE_MESSAGE=\`${describeBinding(bindings, "MAINTENANCE_MESSAGE")}\``);
  } catch {
    lines.push("- 反映後の現在値: 取得できませんでした");
  }
  return lines.join("\n") + "\n";
}

// コマンドラインから実行されたとき
if (process.argv[1]?.endsWith("maintenance-deploy.mjs")) {
  const command = process.argv[2];
  if (command === "check-message") {
    const problem = checkMessage(process.env.MAINTENANCE_MESSAGE_INPUT ?? "");
    if (problem) {
      console.error(`::error::${problem}`);
      process.exit(1);
    }
  } else if (command === "summary") {
    process.stdout.write(await summaryText(process.env));
  } else {
    console.error("使い方: node .github/scripts/maintenance-deploy.mjs check-message | summary");
    process.exit(2);
  }
}
