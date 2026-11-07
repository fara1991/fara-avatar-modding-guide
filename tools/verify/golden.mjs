// 基準（golden）の読み書き。Phase 3b S7 で three.js を外すための仕組み（docs/phase3b-design.md の §17）。
//   GOLDEN_MODE=capture … 現行の JavaScript 版（three.js。?engine=js）を動かして、その結果を tests/visual/golden/ に保存する（three.js を残した版でだけ動く）
//   GOLDEN_MODE=compare … 既定。保存した基準を読んで、C# 版（?engine=cs）と比べる（three.js が無くても動く）
//   GOLDEN_DIR=<フォルダ> … 既定はリポジトリの tests/visual/golden（別の場所から実行するときは、絶対パスで指定する）
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const GOLDEN_MODE = process.env.GOLDEN_MODE === "capture" ? "capture" : "compare";
export const GOLDEN_DIR = process.env.GOLDEN_DIR || path.resolve(here, "../../tests/visual/golden");

function file(group, name, ext) {
  return path.join(GOLDEN_DIR, group, `${name}.${ext}`);
}

/** 基準を保存する（capture のときだけ）。buf は Buffer か文字列 */
export function saveGolden(group, name, ext, buf) {
  const p = file(group, name, ext);
  mkdirSync(path.dirname(p), { recursive: true });
  writeFileSync(p, buf);
}

/** 基準を読む。無ければ、作り方を書いて止まる */
export function loadGolden(group, name, ext) {
  const p = file(group, name, ext);
  if (!existsSync(p)) throw new Error(`基準がありません: ${p}（GOLDEN_MODE=capture で、three.js の版から作ってください）`);
  return readFileSync(p);
}

/**
 * 基準を作る（GOLDEN_MODE=capture）前の検査。配信先（NEW）が、three.js の版（viewport.js と lib/three/three.module.js）を配っているかを確かめる。
 * 無ければ throw する（C# 版の出力で基準を上書きしないため）。作るのは、three.js の版が残っている commit だけ（docs/phase3b-design.md の §17）。
 */
export async function assertCaptureTarget(base) {
  for (const file of ["/js/viewport.js", "/lib/three/three.module.js"]) {
    const res = await fetch(base.replace(/\/$/, "") + file);
    if (res.status !== 200) throw new Error(`基準を作れません: ${base}${file} が ${res.status}。three.js の版（commit 8262089 以前）を配信してください。C# 版の出力で基準を上書きしないため止めます`);
  }
}
