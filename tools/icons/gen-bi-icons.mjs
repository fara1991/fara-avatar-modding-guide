// 練習の JavaScript(unity.js・tutorial.js など)が <i class="bi bi-xxx"> で出すアイコンを、CSS だけで描く css/bi-icons.css を書き出す。
// Bootstrap Icons のフォント(CDN)をやめたので、必要なものだけを SVG(data: URI)にして、mask-image で currentColor に塗る。
//   node tools/icons/gen-bi-icons.mjs <bootstrap-icons の icons フォルダ> <出力する css>
// 例: npm i bootstrap-icons@1.11.3 を作業用のフォルダで実行して、その node_modules/bootstrap-icons/icons を渡す。
// Bootstrap Icons は MIT ライセンス(https://github.com/twbs/icons)。使うアイコンの一覧は ICONS。JavaScript に新しい bi-* を足したら、ここにも足す。
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const ICONS = [
    "arrow-counterclockwise", "arrow-repeat", "arrows-angle-expand", "arrows-move", "badge-3d", "box", "box-fill", "boxes", "camera-video",
    "caret-down-fill", "caret-right-fill", "check-circle-fill", "check-lg", "chevron-left", "chevron-right", "circle-fill", "controller",
    "diagram-3", "dot", "folder2", "grid-3x3", "hand-index", "image", "images", "info-circle", "list-nested", "mouse", "pause-fill",
    "person", "person-badge", "person-plus", "play-fill", "skip-end-fill", "skip-forward-fill", "sun", "trash", "trophy", "unity",
];

const [iconsDir, out] = process.argv.slice(2);
if (!iconsDir || !out) { console.error("usage: gen-bi-icons.mjs <icons dir> <out css>"); process.exit(2); }

const rules = ICONS.map((name) => {
    const svg = readFileSync(join(iconsDir, `${name}.svg`), "utf8").trim()
        .replace(/\s+/g, " ").replace(/ class="[^"]*"/, "").replace(/ fill="currentColor"/, "");
    const uri = "data:image/svg+xml," + svg.replace(/"/g, "'").replace(/[<>#%]/g, (c) => encodeURIComponent(c));
    return `.bi-${name} { --bi: url("${uri}"); }`;
});

const css = `/* 練習の JavaScript が出すアイコン(<i class="bi bi-xxx">)。Bootstrap Icons 1.11.3 から、使うものだけを取り出した(MIT ライセンス)。
   tools/icons/gen-bi-icons.mjs で書き出す。直接は直さない。文字と同じ大きさ・色(currentColor)で、aria-hidden のまま使う。 */
.bi {
    display: inline-block;
    width: 1em;
    height: 1em;
    vertical-align: -0.125em;
    background-color: currentColor;
    -webkit-mask: var(--bi) center / contain no-repeat;
    mask: var(--bi) center / contain no-repeat;
}

${rules.join("\n")}
`;
writeFileSync(out, css, "utf8");
console.log(`${ICONS.length} icons -> ${out}`);
