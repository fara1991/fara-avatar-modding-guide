// 記事内の目次（ArticlePage.razor）。今読んでいる h2 に対応する目次のリンクを強調する（aria-current="location"）。
// 見出しが画面の上の方を通ったかどうかで決める。ページの一番下まで来たら最後の見出しにする。
(() => {
  const links = [...document.querySelectorAll(".toc-list a[href^='#']")];
  if (links.length === 0) return;
  const byId = new Map();
  for (const a of links) {
    const id = decodeURIComponent(a.getAttribute("href").slice(1));
    if (!byId.has(id)) byId.set(id, []);
    byId.get(id).push(a);
  }
  const headings = [...byId.keys()].map(id => document.getElementById(id)).filter(Boolean);
  if (headings.length === 0) return;

  // 検索ヘッダーに隠れない位置（html の scroll-padding-top と同じ）より上に出た最後の見出し
  const line = () => parseFloat(getComputedStyle(document.documentElement).scrollPaddingTop) || 72;
  let current = null;

  function update() {
    const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
    let h = null;
    if (atBottom) h = headings[headings.length - 1];
    else for (const x of headings) { if (x.getBoundingClientRect().top <= line() + 8) h = x; else break; }
    if (h === current) return;
    current = h;
    for (const a of links) {
      const on = !!h && a.getAttribute("href") === `#${h.id}`;
      a.classList.toggle("is-current", on);
      if (on) {
        a.setAttribute("aria-current", "location");
        // 目次が縦にあふれて内側でスクロールしているときだけ、強調したリンクが見える位置にする
        const box = a.closest(".article-aside");
        if (box && box.scrollHeight > box.clientHeight && box.offsetParent) {
          const r = a.getBoundingClientRect(), b = box.getBoundingClientRect();
          if (r.top < b.top || r.bottom > b.bottom) box.scrollTop += r.top - b.top - 40;
        }
      } else a.removeAttribute("aria-current");
    }
  }

  // 見出しが画面の上の帯を出入りしたら更新する（スクロールのたびに計算しない）
  const io = new IntersectionObserver(update, { rootMargin: `-${Math.round(line())}px 0px -60% 0px`, threshold: [0, 1] });
  for (const h of headings) io.observe(h);
  addEventListener("scroll", () => { if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2) update(); }, { passive: true });
  addEventListener("resize", update);
  update();
})();
