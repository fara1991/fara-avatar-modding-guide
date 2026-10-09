// サイト内検索。/search-index.json を取得し、キーワードの部分一致で絞り込む。
// 日本語は単語区切りがないため形態素解析はせず、スペース区切りの各語が「すべて含まれる」ものを一致とする。
(async function () {
  const input = document.getElementById("search-input");
  const results = document.getElementById("search-results");
  if (!input || !results) return;

  const query = (new URLSearchParams(location.search).get("q") || "").trim();
  input.value = query;
  if (!query) {
    results.innerHTML = '<p class="text-body-secondary">キーワードを入力してください。</p>';
    return;
  }

  let index;
  try {
    const response = await fetch("/search-index.json");
    index = await response.json();
  } catch {
    results.innerHTML = '<p class="text-danger">検索インデックスを読み込めませんでした。</p>';
    return;
  }

  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  const hits = [];
  for (const entry of index) {
    const title = entry.title.toLowerCase();
    const subtitle = (entry.subtitle || "").toLowerCase();
    const text = (entry.text || "").toLowerCase();
    let score = 0;
    for (const term of terms) {
      if (title.includes(term)) score += 10;
      else if (subtitle.includes(term)) score += 5;
      else if (text.includes(term)) score += 1;
      else { score = 0; break; } // すべての語を含むものだけ
    }
    if (score > 0) hits.push({ entry, score });
  }
  hits.sort((a, b) => b.score - a.score);

  if (hits.length === 0) {
    results.innerHTML = `<p>「${escapeHtml(query)}」に一致するものは見つかりませんでした。</p>`;
    return;
  }

  const typeLabel = { article: "記事", shortcut: "ショートカット", addon: "アドオン" };
  const typeClass = { article: "bg-primary", shortcut: "bg-success", addon: "bg-info text-dark" };
  results.innerHTML =
    `<p class="text-body-secondary">${hits.length} 件</p>` +
    hits.map(({ entry }) => `
      <a href="${escapeHtml(entry.url)}" class="search-result">
        <div class="d-flex align-items-center gap-2">
          <span class="badge ${typeClass[entry.type] || "bg-secondary"}">${typeLabel[entry.type] || entry.type}</span>
          <span class="search-result-title">${highlight(entry.title, terms)}</span>
          ${entry.subtitle ? `<span class="text-body-secondary small">${highlight(entry.subtitle, terms)}</span>` : ""}
        </div>
        <div class="search-result-snippet">${highlight(snippet(entry.text || "", terms), terms)}</div>
      </a>`).join("");

  function snippet(text, terms) {
    const lower = text.toLowerCase();
    let pos = -1;
    for (const term of terms) { pos = lower.indexOf(term); if (pos >= 0) break; }
    if (pos < 0) return text.slice(0, 120) + (text.length > 120 ? "…" : "");
    const start = Math.max(0, pos - 40);
    const end = Math.min(text.length, pos + 80);
    return (start > 0 ? "…" : "") + text.slice(start, end) + (end < text.length ? "…" : "");
  }

  function highlight(text, terms) {
    let html = escapeHtml(text);
    for (const term of terms) {
      const pattern = new RegExp(escapeRegExp(escapeHtml(term)), "gi");
      html = html.replace(pattern, (m) => `<mark>${m}</mark>`);
    }
    return html;
  }

  function escapeHtml(s) {
    return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  function escapeRegExp(s) {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }
})();
