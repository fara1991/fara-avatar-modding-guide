// 検索ページ（/search?q=...）。一致の判定と強調表示は searchcore.js。
// ページ移動（nav.js）のあとは、新しい画面の検索結果を作り直す（faraNav.register で呼ばれる）。
(function () {
  "use strict";

  async function init() {
    const input = document.getElementById("search-input");
    const results = document.getElementById("search-results");
    const S = window.faraSearch;
    if (!input || !results || !S) return;

    const query = (new URLSearchParams(location.search).get("q") || "").trim();
    input.value = query;
    if (!query) {
      results.innerHTML = '<p class="text-body-secondary">キーワードを入力してください。どのページでも <kbd>Ctrl</kbd>+<kbd>K</kbd> か <kbd>/</kbd> で上の検索欄に移動できます。</p>';
      return;
    }

    let index;
    try {
      index = await S.load();
    } catch {
      results.innerHTML = '<p class="text-danger">検索インデックスを読み込めませんでした。</p>';
      return;
    }

    const hits = S.search(index, query);
    if (hits.length === 0) {
      results.innerHTML = `<p>「${S.escapeHtml(query)}」に一致するものは見つかりませんでした。</p>`;
      return;
    }

    results.innerHTML =
      `<p class="text-body-secondary">${hits.length} 件</p>` +
      hits.map(({ entry }) => {
        const t = S.TYPES[entry.type] || { label: entry.type, cls: "bg-secondary" };
        return `
      <a href="${S.escapeHtml(entry.url)}" class="search-result">
        <div class="d-flex align-items-center gap-2 flex-wrap">
          <span class="badge ${t.cls}">${t.label}</span>
          <span class="search-result-title">${S.highlight(entry.title, query)}</span>
          ${entry.subtitle ? `<span class="text-body-secondary small">${S.highlight(entry.subtitle, query)}</span>` : ""}
        </div>
        <div class="search-result-snippet">${S.highlight(S.snippet(entry.text || "", query), query)}</div>
      </a>`;
      }).join("");
  }

  // 失敗（検索の読み込みなど）は、ここで受けて表示しない（未処理の拒否にしない）
  function start() {
    init().catch(function (err) { console.error(err); });
  }
  window.faraNav ? window.faraNav.register(start) : start();
})();
