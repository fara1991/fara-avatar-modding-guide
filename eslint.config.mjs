// 静的解析（ESLint）の設定。共通のルールは kit の tools/lint/eslint.config.shared.mjs（docs/lint.md の 1）。
// 対象は JS だけ。external/（kit）は kit の CI で見る。画面の JS は、import/export があれば module、無ければ HTML の <script> で読む script。
import shared, { createSharedConfig } from "./external/fara-blazor-component/tools/lint/eslint.config.shared.mjs";

// 読むのは、ファイルの形（import/export の有無）で決めた。ブラウザの JS は、Blazor の wwwroot/js（動的な import() で読む module）と、index.html の <script>（script）
const WEB_JS = "src/FaraAvatarModdingGuide.Web/wwwroot/js";
const WEB_JS_MODULES = [
  `${WEB_JS}/gl.js`,
  `${WEB_JS}/practice-cs.js`,
  `${WEB_JS}/practice-entry.js`,
  `${WEB_JS}/room-cs.js`,
  `${WEB_JS}/scenarios.js`,
  `${WEB_JS}/tutorial.js`,
  `${WEB_JS}/unity-ui.js`,
];
const WEB_JS_SCRIPTS = [
  `${WEB_JS}/bgscene.js`,
  `${WEB_JS}/head-sync.js`,
  `${WEB_JS}/nav-menu.js`,
  `${WEB_JS}/notice.js`,
  `${WEB_JS}/page.js`,
  `${WEB_JS}/search-norm.js`,
  `${WEB_JS}/search-ui.js`,
  `${WEB_JS}/startup.js`,
  `${WEB_JS}/storage-migrate.js`,
  `${WEB_JS}/storage.js`,
  `${WEB_JS}/theme-default.js`,
  `${WEB_JS}/toc.js`,
];

export default [
  {
    // 生成物（公開・確認用の出力・比較の出力）と、凍結された基準（searchcore.reference.js は CLAUDE.md で書き換えない）
    ignores: [
      "dist/**",
      "publish/**",
      "publish-lab/**",
      "site-production/**",
      "site-develop/**",
      "*-compare-out/**",
      ".wrangler/**",
      "tests/js/fixtures/searchcore.reference.js",
    ],
  },
  // Node の JS（tools・worker・.github/scripts・tests/js など）。共通の既定（**/*.{js,mjs,cjs}）
  ...shared,
  // Worker（worker/）: Cloudflare の実行環境だけのグローバルを足す（Node には無い）。
  // WebSocketPair は、部屋（Durable Object）の WebSocket を作る。WebSocketRequestResponsePair は、ping・pong の自動応答（setWebSocketAutoResponse）
  ...createSharedConfig({
    files: ["worker/**/*.js"],
    environments: ["node"],
    extraGlobals: { WebSocketPair: "readonly", WebSocketRequestResponsePair: "readonly" },
  }),
  // tools/verify の比較は、page.evaluate に渡す関数をブラウザで動かす（Node の中に書く）。ブラウザのグローバルも要る
  ...createSharedConfig({
    files: ["tools/verify/**/*.mjs"],
    environments: ["node", "browser"],
  }),
  // 画面の JS（ES モジュール）
  ...createSharedConfig({
    files: [...WEB_JS_MODULES, "src/FaraAvatarModdingGuide.RenderLab/wwwroot/js/gl-lab.js"],
    environments: ["browser"],
  }),
  // 画面の JS（HTML の <script> で読む古い形）
  ...createSharedConfig({
    files: WEB_JS_SCRIPTS,
    environments: ["browser"],
    sourceType: "script",
  }),
];
