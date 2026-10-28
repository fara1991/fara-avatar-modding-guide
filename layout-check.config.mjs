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
    // みんなで練習のロビー（練習の部屋の一覧・作成の鍵）。部屋の中の 3D は実機の確認（tools/room-check）で見る
    { path: "/practice/room", name: "みんなで練習（ロビー）" },
    // 存在しないページ。kit の serve-static は /404 を 404.html として返す（Cloudflare の 404-page と同じ見た目）
    { path: "/404", name: "404（存在しないページ）" },
  ],
  viewports: [360, 390, 768, 1024, 1440],
  viewportHeight: 800,
  // Blazor が描画した後にだけある要素。これを待ってから測る（静的の本文だけを測らない）
  readySelector: ".mud-layout",
  // closeSelector: kit v0.7.0 の SiteLayout のドロワーの閉じるボタン（スマホのみ）。ドロワーがメニューボタンを覆うため、閉じる手段はこちらで判定する（kit の docs/layout-check.md の 6.）
  menuEsc: { buttonSelector: ".fsk-menu-toggle", closeSelector: ".fsk-drawer-close" },
  rules: {
    "no-horizontal-scroll": true,
    "single-h1": true,
    "focus-visible": true,
    "no-offscreen-focus": true,
    "tap-target": true,
    "min-font-size": true,
    "menu-esc": true,
    "menu-close-visible": true,
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
      reason: "パンくずが折り返す行で、項目の下の点が次の行の項目に当たる（計測時点）。項目の箱を 44px に広げても、行の箱が低いままで重なる。直すのは、パンくずの行の作り（ol の折り返し）を見直す別の PR",
    },
    {
      rule: "tap-target",
      selector: "button.mud-button-root.fsk-report-submit",
      page: "/report",
      reason: "問い合わせの送信ボタン。計測は Worker の無い静的配信なので、フォームは「送れません」の状態で、ボタンは無効（MudButton の disabled は pointer-events: none。どの点も自分に当たらない）。有効なときの押せる範囲は、確認用（Worker あり）で目視",
    },
    {
      rule: "focus-visible",
      selector: ".mud-input-slot",
      page: "/report",
      reason: "MudBlazor の入力欄（kit の ReportFormView）。入力欄の outline は MudBlazor が 0 にし、フォーカスの表示は枠の下線の色で出す。下線の表示は、計測では見ないので目視で確かめる（kit の docs/layout-check.md の「限界」）",
    },
  ],
};
