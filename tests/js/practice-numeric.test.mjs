// 数値入力が "-."(符号のあとに小数点だけ)のとき、G / R / S が NaN にならず「数値入力なし」(マウスの動き)として扱われる。
// 現行の viewport.js を practice-reference.mjs の方式(本物のメソッドとハンドラーを Node で動かす)で動かす。C# との一致は ParityTests。
import test from "node:test";
import assert from "node:assert/strict";
import { runScenario } from "./practice-reference.mjs";

const ROOT = { left: 37, top: 61, width: 912, height: 529 };
const cx = ROOT.left + ROOT.width / 2, cy = ROOT.top + ROOT.height / 2;
const view = { target: [0, 0, 0], dist: 11, theta: -Math.PI / 4 - Math.PI / 2, phi: (63 * Math.PI) / 180, ortho: false, name: "ユーザー" };
const key = (code, k) => ({ e: "key", code, key: k ?? code });
const move = (x, y) => ({ e: "move", x, y });

function numericOf(kindKey, mouse) {
  const steps = [move(cx - 90, cy + 60), key(kindKey), move(...mouse), key("Minus", "-"), key("Period", "."), move(mouse[0] + 7, mouse[1] + 3), key("Digit5", "5"), key("Backspace"), key("Backspace"), key("Enter")];
  return runScenario({ name: kindKey, root: ROOT, emulate: false, view, objects: [{ kind: "cube", name: "Cube" }], selected: [0], probes: [], steps }).steps;
}

for (const [k, mouse] of [["KeyG", [cx + 80, cy + 20]], ["KeyR", [cx + 20, cy - 70]], ["KeyS", [cx + 130, cy + 10]]]) {
  test(`${k}: 「-.」のあいだは数値入力なし(NaN にならない)`, () => {
    const steps = numericOf(k, mouse);
    const dotted = steps[5]; // 最初の状態のあと 5 手目 = "-" と "." を入力したあと
    assert.equal(dotted.op.numeric, "-.");
    const cube = dotted.objects[0];
    for (const n of [...cube.p, ...cube.q, ...cube.s, ...cube.euler]) assert.ok(Number.isFinite(n), `${k}: ${JSON.stringify(cube)}`);
    assert.ok(!dotted.status.includes("NaN"), dotted.status);
    // 数値入力なしなので、マウスを動かすと値が変わる(数値で固定されていない)
    const moved = steps[6].objects[0];
    assert.notDeepEqual(moved.p.concat(moved.q, moved.s), cube.p.concat(cube.q, cube.s));
    // 最後の Enter で確定しても、オブジェクトは消えず有限の値のまま
    const last = steps[steps.length - 1].objects[0];
    for (const n of [...last.p, ...last.q, ...last.s]) assert.ok(Number.isFinite(n));
  });
}

test("「-.5」と打てば -0.5 として使われる(G の X 方向)", () => {
  const steps = [move(cx, cy), key("KeyG"), key("Minus", "-"), key("Period", "."), key("Digit5", "5"), key("Enter")];
  const out = runScenario({ name: "g", root: ROOT, emulate: false, view, objects: [{ kind: "cube", name: "Cube" }], selected: [0], probes: [], steps }).steps;
  assert.deepEqual(out[out.length - 1].objects[0].p, [-0.5, 0, 0]);
});
