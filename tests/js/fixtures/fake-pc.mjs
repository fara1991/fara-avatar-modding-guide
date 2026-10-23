// practice-entry.js のテスト用の、PC 用の練習スクリプトの代わり(viewport.js は three を読むので Node では読めない)
export const log = globalThis.__fakePcLog ??= [];

export function start(options) {
    log.push(["start", options]);
    return { dispose() { log.push(["dispose", options]); } };
}
