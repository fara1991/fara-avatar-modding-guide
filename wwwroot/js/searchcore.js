// サイト内検索の共通部分（/search-index.json の読み込み・一致の判定・強調表示）。
// ヘッダーの検索（quicksearch.js）と検索ページ（search.js）の両方で使う。
// 日本語は単語区切りがないため形態素解析はせず、スペース区切りの各語が「すべて含まれる」ものを一致とする。
// 全角・半角の違い（Ｇ と G、ｶﾅ とカナ）は NFKC で吸収する。
(function () {
  "use strict";
  var indexPromise = null;

  /** 検索インデックス。最初に呼ばれたときに一度だけ取得する */
  function load() {
    if (!indexPromise) {
      indexPromise = fetch("/search-index.json").then(function (r) {
        if (!r.ok) throw new Error("search index " + r.status);
        return r.json();
      }).then(function (list) {
        // 正規化した文字列を先に作っておく
        list.forEach(function (e) {
          e._t = norm(e.title);
          e._s = norm(e.subtitle || "");
          e._x = norm(e.text || "");
        });
        return list;
      }).catch(function (err) { indexPromise = null; throw err; });
    }
    return indexPromise;
  }

  function norm(s) {
    return String(s).normalize("NFKC").toLowerCase();
  }

  function terms(query) {
    return norm(query).split(/\s+/).filter(Boolean);
  }

  /** すべての語を含むものを、タイトル > 小見出し > 本文の順に点数を付けて並べる */
  function search(index, query) {
    var ts = terms(query);
    if (!ts.length) return [];
    var hits = [];
    index.forEach(function (e) {
      var score = 0;
      for (var i = 0; i < ts.length; i++) {
        var t = ts[i];
        if (e._t === t) score += 30;
        else if (e._t.indexOf(t) === 0) score += 15;
        else if (e._t.indexOf(t) >= 0) score += 10;
        else if (e._s.indexOf(t) >= 0) score += 5;
        else if (e._x.indexOf(t) >= 0) score += 1;
        else { score = 0; break; }
      }
      if (score > 0) hits.push({ entry: e, score: score + (TYPE_BONUS[e.type] || 0) });
    });
    hits.sort(function (a, b) { return b.score - a.score; });
    return hits;
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; });
  }

  function escapeRegExp(s) {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }

  /**
   * 一致した部分を <mark> で囲む。エスケープ前の文字列で一度に探すので、後の語が前に挿入した <mark> や &amp; などの中に一致することはない。
   * 全角・半角が違っても一致するように、1 文字ずつ正規化した文字列で位置を探す
   */
  function highlight(text, query) {
    text = String(text);
    var ts = terms(query);
    if (!ts.length) return escapeHtml(text);
    var normed = "", map = [];
    for (var i = 0; i < text.length; i++) {
      var n = norm(text[i]);
      for (var k = 0; k < n.length; k++) { normed += n[k]; map.push(i); }
    }
    var pattern = new RegExp(ts.slice().sort(function (a, b) { return b.length - a.length; }).map(escapeRegExp).join("|"), "g");
    var html = "", last = 0, m;
    while ((m = pattern.exec(normed)) !== null) {
      if (!m[0].length) { pattern.lastIndex++; continue; }
      var s = map[m.index], e = map[m.index + m[0].length - 1] + 1;
      if (s < last) continue;
      html += escapeHtml(text.slice(last, s)) + "<mark>" + escapeHtml(text.slice(s, e)) + "</mark>";
      last = e;
    }
    return html + escapeHtml(text.slice(last));
  }

  /** 本文から、最初に一致した語のまわりを切り出す */
  function snippet(text, query, width) {
    text = String(text || "");
    width = width || 120;
    var lower = norm(text), ts = terms(query), pos = -1;
    for (var i = 0; i < ts.length && pos < 0; i++) pos = lower.indexOf(ts[i]);
    if (pos < 0) return text.slice(0, width) + (text.length > width ? "…" : "");
    var start = Math.max(0, pos - Math.floor(width / 3));
    var end = Math.min(text.length, start + width);
    return (start > 0 ? "…" : "") + text.slice(start, end) + (end < text.length ? "…" : "");
  }

  // 種類の表示（用語集・何て名前？・TIPS・Q&A は Content/tips/、リンク集は Content/links.yml のデータ）
  var TYPES = {
    article: { label: "記事", cls: "bg-primary" },
    glossary: { label: "用語", cls: "bg-warning text-dark" },
    howto: { label: "何て名前？", cls: "bg-danger" },
    tip: { label: "TIPS", cls: "bg-info text-dark" },
    qa: { label: "Q&A", cls: "bg-light text-dark" },
    shortcut: { label: "ショートカット", cls: "bg-success" },
    addon: { label: "アドオン", cls: "bg-secondary" },
    link: { label: "リンク", cls: "bg-primary-subtle text-primary-emphasis" },
  };
  // 同じくらい一致したときは、記事や用語を先に出す
  var TYPE_BONUS = { article: 0.6, glossary: 0.5, howto: 0.5, tip: 0.4, qa: 0.4, shortcut: 0.2, addon: 0, link: 0 };

  window.faraSearch = { load: load, search: search, highlight: highlight, snippet: snippet, escapeHtml: escapeHtml, TYPES: TYPES };
})();
