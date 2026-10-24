import { PNG } from "pngjs"; import { readFileSync, readdirSync } from "node:fs";
for (const dir of process.argv.slice(2)) for (const f of readdirSync(dir)) {
  const p = PNG.sync.read(readFileSync(dir + "/" + f)); const w = p.width / 3; const pts = [];
  for (let y = 0; y < p.height; y++) for (let x = 0; x < w; x++) {
    const i = (y * p.width + x) * 4, j = (y * p.width + w + x) * 4;
    const d = Math.max(Math.abs(p.data[i] - p.data[j]), Math.abs(p.data[i+1] - p.data[j+1]), Math.abs(p.data[i+2] - p.data[j+2]));
    if (d > 8) pts.push(`(${x},${y})d${d} js=${p.data[i]},${p.data[i+1]},${p.data[i+2]} cs=${p.data[j]},${p.data[j+1]},${p.data[j+2]}`);
  }
  console.log(dir, f, pts.length, pts.slice(0, 6).join(" "));
}
