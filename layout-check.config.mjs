// レイアウトの自動計測の設定（kit の tools/layout-check。説明は external/fara-site-kit/docs/layout-check.md）。
// 計測の前に、公開用の静的出力を site-production に作る（README の「レイアウトの計測」）。npm run layout-check が配信と計測をまとめて行う。
export default {
  // npm run layout-check が、site-production を配った先の URL を入れる（直接 CLI で動かすときは既定の値）
  baseURL: process.env.LAYOUT_CHECK_BASE_URL ?? "http://127.0.0.1:8870",
  pages: [
    { path: "/", name: "トップ" },
    { path: "/learn", name: "学習の一覧" },
    { path: "/learn/basics/select", name: "記事（選択）" },
    { path: "/learn/vrchat/upload-errors", name: "記事（アップロードのエラー）" },
    { path: "/report", name: "問い合わせ" },
    { path: "/privacy", name: "プライバシー" },
    // 存在しないページ。kit の serve-static は /404 を 404.html として返す（Cloudflare の 404-page と同じ見た目）
    { path: "/404", name: "404（存在しないページ）" },
  ],
  viewports: [360, 390, 768, 1024, 1440],
  viewportHeight: 800,
  // Blazor が描画した後にだけある要素。これを待ってから測る（静的の本文だけを測らない）
  readySelector: ".mud-layout",
  menuEsc: { buttonSelector: ".fsk-menu-toggle" },
  rules: {
    "no-horizontal-scroll": true,
    "single-h1": true,
    "focus-visible": true,
    "no-offscreen-focus": true,
    "tap-target": true,
    "min-font-size": true,
    "menu-esc": true,
  },
  tapTarget: { minSize: 44, maxWidth: 600 },
  minFontSize: { min: 12, ignoreSelectors: [".material-icons"] },
  focus: { maxStops: 40 },
  timeoutMs: 30000,
  // undefined = playwright-core の同梱の chromium。ローカルで Edge を使うときは LAYOUT_CHECK_CHANNEL=msedge
  browser: { channel: process.env.LAYOUT_CHECK_CHANNEL || undefined },
  // 除外は、既知の例外だけを「セレクター・ルール名・理由」つきで書く（kit の docs/layout-check.md の 3）。
  // 次は、いずれも直し先を kit に持つもの。kit の次のタグで直したら、対応する行を消す（kit の follow-up に書いてある）。
  exclusions: [
    {
      rule: "no-offscreen-focus",
      selector: "#fsk-main a, #fsk-main button, #fsk-main summary, #fsk-main input, #fsk-main select, #fsk-main textarea, #fsk-main .practice-viewport, footer a",
      reason: "計測の時間差。画面の下の項目に Tab で入ると、ブラウザは滑らかにスクロールする（kit の計測は reducedMotion を指定できず、スクロールの途中の位置を測る）。ドロワー（.fsk-sidebar）とヘッダーは、この除外の対象外。練習の 3D のビュー（記事内）も同じ理由",
    },
    {
      rule: "no-offscreen-focus",
      selector: ".fsk-skip-link",
      reason: "本文へ移動のリンク。フォーカスで上から出す演出（transition）の途中を測るため。kit の SiteLayout の部品",
    },
    {
      rule: "tap-target",
      selector: "a.fsk-footer-social",
      reason: "kit の SiteFooter の SNS のアイコン（22px）。直すのは kit（押せる範囲を広げる）。サイト側の CSS で広げると、フッターの並びが kit の見た目と変わる",
    },
    {
      rule: "tap-target",
      selector: ".article-body td a",
      reason: "表のセルの中の文字リンク（TIPS などの参照）。段落の中の文字リンクと同じく、前後に文字がないので kit の文中の判定に入らない。44px にすると表の行が大きく崩れる。WCAG のインラインの文字リンクの扱いに合わせる",
    },
    {
      rule: "tap-target",
      selector: ".breadcrumb-item > a",
      reason: "パンくずの項目。スマホでは 44px の高さ・幅を取っているが、中心の上下の点が項目の外（li の行の箱）に当たる。原因は未確認（インラインの箱のずれ）。直すのは別の PR で確かめてから（docs の「既知の除外」）",
    },
    {
      rule: "tap-target",
      selector: "button.mud-button-root.fsk-report-submit",
      page: "/report",
      reason: "kit の問い合わせの送信ボタン（MudButton）。高さを 48px にしても、中心の上下の点が自分に当たらない（原因は未確認。MudBlazor の部品の内側の要素が当たる）。kit で確かめて直す（kit の follow-up）",
    },
    {
      rule: "focus-visible",
      selector: ".mud-input-slot",
      page: "/report",
      reason: "MudBlazor の入力欄（kit の ReportFormView）。入力欄の outline は MudBlazor が 0 にし、フォーカスの表示は枠の下線の色で出す。下線の表示は、計測では見ないので目視で確かめる（kit の docs/layout-check.md の「限界」）",
    },
  ],
};
