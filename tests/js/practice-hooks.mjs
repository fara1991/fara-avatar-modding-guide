// practice-reference.mjs が module.register で登録する、Node 用の読み込みフック。
//   ・import "three" を、サイトに同梱している wwwroot/lib/three/three.module.js に向ける(ブラウザでは index.html の importmap が同じ役目をする)
//   ・practice.js の TASKS と Practice を取り出せるように、読み込むときだけ末尾に export を足す(配信するファイルは書き換えない)
//   ・unity-practice.js の TASKS と UnityPractice も同じ(S5)
//   ・viewport.js・unity.js の new THREE.WebGLRenderer(...) だけを、何も描かない代用品(practice-dom.mjs)に差し替える(読み込むときだけ。コンストラクタごと本物を動かすため)
let threeUrl = "";
export async function initialize(data) { threeUrl = data.threeUrl; }

export async function resolve(specifier, context, nextResolve) {
  if (specifier === "three") return { url: threeUrl, shortCircuit: true };
  return nextResolve(specifier, context);
}

export async function load(url, context, nextLoad) {
  const result = await nextLoad(url, context);
  if (url.endsWith("/wwwroot/js/practice.js")) {
    result.source = result.source.toString() + "\nexport { TASKS, Practice };\n";
  }
  if (url.endsWith("/wwwroot/js/viewport.js")) {
    const source = result.source.toString();
    if (!source.includes("new THREE.WebGLRenderer(")) throw new Error("viewport.js の new THREE.WebGLRenderer( が見つかりません。practice-hooks.mjs の差し替えを直してください");
    result.source = source.replace("new THREE.WebGLRenderer(", "new globalThis.__practiceFakeRenderer(");
  }
  if (url.endsWith("/wwwroot/js/unity.js")) {
    const source = result.source.toString();
    if (!source.includes("new THREE.WebGLRenderer(")) throw new Error("unity.js の new THREE.WebGLRenderer( が見つかりません。practice-hooks.mjs の差し替えを直してください");
    result.source = source.replace("new THREE.WebGLRenderer(", "new globalThis.__practiceFakeRenderer(");
  }
  if (url.endsWith("/wwwroot/js/unity-practice.js")) {
    result.source = result.source.toString() + "\nexport { TASKS, UnityPractice };\n";
  }
  return result;
}
