// practice-reference.mjs が module.register で登録する、Node 用の読み込みフック。
//   ・import "three" を、サイトに同梱している wwwroot/lib/three/three.module.js に向ける(ブラウザでは index.html の importmap が同じ役目をする)
//   ・practice.js の TASKS と Practice を取り出せるように、読み込むときだけ末尾に export を足す(配信するファイルは書き換えない)
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
  return result;
}
