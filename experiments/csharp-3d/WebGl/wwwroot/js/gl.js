// WebGL 2 の薄い橋渡し（C# の JSImport から呼ぶ）。シェーダーの中身・行列・描画の順番は C# 側（GlRenderer.cs）が決める。
// 頂点は 1 本のバッファに、1 頂点 9 個の float（位置 3・法線 3・色 3）で並べる。
window.glBridge = (() => {
  let gl = null;
  let program = null;
  const uniforms = new Map();

  function compile(type, src) {
    const sh = gl.createShader(type);
    gl.shaderSource(sh, src);
    gl.compileShader(sh);
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) {
      throw new Error(gl.getShaderInfoLog(sh));
    }
    return sh;
  }

  function init(canvasId, vsSrc, fsSrc) {
    const canvas = document.getElementById(canvasId);
    gl = canvas.getContext('webgl2', { antialias: true, alpha: false });
    if (!gl) return false;
    program = gl.createProgram();
    gl.attachShader(program, compile(gl.VERTEX_SHADER, vsSrc));
    gl.attachShader(program, compile(gl.FRAGMENT_SHADER, fsSrc));
    // 属性の場所は GLSL の layout(location) で決めてあるので、リンク後に取り直さない
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
    gl.useProgram(program);
    const vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
    const stride = 9 * 4;
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 3, gl.FLOAT, false, stride, 12);
    gl.enableVertexAttribArray(2);
    gl.vertexAttribPointer(2, 3, gl.FLOAT, false, stride, 24);
    gl.enable(gl.DEPTH_TEST);
    return true;
  }

  // C# の byte[]（Uint8Array）を、そのまま float として解釈する
  function uploadVertices(bytes) {
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(bytes.buffer, bytes.byteOffset, bytes.byteLength / 4), gl.STATIC_DRAW);
  }

  function setViewport(w, h) {
    const canvas = gl.canvas;
    if (canvas.width !== w) canvas.width = w;
    if (canvas.height !== h) canvas.height = h;
    gl.viewport(0, 0, w, h);
  }

  // 表示サイズ（CSS px と同じ。KNI 版の canvas 属性と同じ扱い）
  function canvasWidth(id) { return document.getElementById(id).clientWidth; }
  function canvasHeight(id) { return document.getElementById(id).clientHeight; }

  function clear(r, g, b) {
    gl.clearColor(r, g, b, 1);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
  }

  function loc(name) {
    let l = uniforms.get(name);
    if (l === undefined) {
      l = gl.getUniformLocation(program, name);
      uniforms.set(name, l);
    }
    return l;
  }

  function uniformMat4(name, bytes) {
    gl.uniformMatrix4fv(loc(name), false, new Float32Array(bytes.buffer, bytes.byteOffset, 16));
  }

  function uniformVec3(name, x, y, z) {
    gl.uniform3f(loc(name), x, y, z);
  }

  function uniformInt(name, v) {
    gl.uniform1i(loc(name), v);
  }

  // mode: 0 = 三角形、1 = 線
  function drawRange(mode, first, count) {
    gl.drawArrays(mode === 0 ? gl.TRIANGLES : gl.LINES, first, count);
  }

  return { init, uploadVertices, setViewport, canvasWidth, canvasHeight, clear, uniformMat4, uniformVec3, uniformInt, drawRange };
})();
