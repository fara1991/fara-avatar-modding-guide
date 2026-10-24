import { PNG } from "pngjs"; import { readFileSync, writeFileSync } from "node:fs";
const p = PNG.sync.read(readFileSync(process.argv[2])); const w = p.width / 3;
const [cx, cy, r, sc] = process.argv.slice(4).map(Number);
const out = new PNG({ width: (2 * r) * sc * 2 + 10, height: 2 * r * sc });
for (let k = 0; k < 2; k++) for (let y = 0; y < 2 * r; y++) for (let x = 0; x < 2 * r; x++) {
  const si = ((cy - r + y) * p.width + (k * w) + cx - r + x) * 4;
  for (let yy = 0; yy < sc; yy++) for (let xx = 0; xx < sc; xx++) { const di = ((y * sc + yy) * out.width + k * (2 * r * sc + 10) + x * sc + xx) * 4; for (let c = 0; c < 4; c++) out.data[di + c] = p.data[si + c]; }
}
writeFileSync(process.argv[3], PNG.sync.write(out));
