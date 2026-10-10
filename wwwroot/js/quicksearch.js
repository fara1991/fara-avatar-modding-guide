// ヘッダーの検索欄。入力するとすぐに候補を出す（↑↓ で選んで Enter、Esc で閉じる）。
// どのページでも Ctrl+K（Mac は ⌘K）か「/」で検索欄に移動する。Enter だけなら検索ページ（/search?q=）へ。
(function () {
  "use strict";
  var MAX = 8;
  var form, input, list, items = [], active = -1, timer = 0, lastQuery = "";

  function init() {
    form = document.getElementById("quick-search");
    input = document.getElementById("quick-search-input");
    list = document.getElementById("quick-search-list");
    if (!form || !input || !list || !window.faraSearch) return;

    input.addEventListener("focus", function () {
      window.faraSearch.load().catch(function () { /* 失敗したら入力時に表示 */ });
      if (input.value.trim()) update();
    });
    input.addEventListener("input", function () {
      clearTimeout(timer);
      timer = setTimeout(update, 60);
    });
    input.addEventListener("keydown", function (e) {
      if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        if (list.hidden) { update(); return; }
        e.preventDefault();
        move(e.key === "ArrowDown" ? 1 : -1);
      } else if (e.key === "Enter") {
        if (!list.hidden && active >= 0 && items[active]) {
          e.preventDefault();
          location.href = items[active].getAttribute("href");
        }
        // 選んでいなければフォームの送信（検索ページ）
      } else if (e.key === "Escape") {
        if (!list.hidden) { e.preventDefault(); close(); }
        else input.blur();
      }
    });
    list.addEventListener("mousedown", function (e) { e.preventDefault(); }); // クリックで入力欄のフォーカスを外さない
    document.addEventListener("click", function (e) { if (!form.contains(e.target)) close(); });

    document.addEventListener("keydown", function (e) {
      var k = e.key && e.key.toLowerCase();
      if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && k === "k") {
        e.preventDefault();
        focusInput();
        return;
      }
      if (e.key === "/" && !e.ctrlKey && !e.metaKey && !e.altKey && !isTyping(e.target)) {
        e.preventDefault();
        focusInput();
      }
    });
  }

  /** 文字入力中や 3D ビューの操作中は「/」を奪わない */
  function isTyping(el) {
    if (!el || el === document.body) return false;
    if (el.isContentEditable) return true;
    var tag = el.tagName;
    if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return true;
    return !!(el.closest && el.closest(".practice-viewport, .unity-editor"));
  }

  function focusInput() {
    input.focus();
    input.select();
  }

  function update() {
    var q = input.value.trim();
    lastQuery = q;
    if (!q) { close(); return; }
    window.faraSearch.load().then(function (index) {
      if (q !== lastQuery) return; // 古い入力の結果
      render(window.faraSearch.search(index, q), q);
    }, function () {
      list.innerHTML = '<div class="quick-search-empty">検索インデックスを読み込めませんでした</div>';
      open();
    });
  }

  function render(hits, q) {
    var S = window.faraSearch;
    var html = hits.slice(0, MAX).map(function (h, i) {
      var e = h.entry, t = S.TYPES[e.type] || { label: e.type, cls: "bg-secondary" };
      return '<a class="quick-search-item" role="option" id="qs-opt-' + i + '" href="' + S.escapeHtml(e.url) + '" aria-selected="false">' +
        '<span class="badge ' + t.cls + '">' + t.label + "</span>" +
        '<span class="quick-search-title">' + S.highlight(e.title, q) + "</span>" +
        (e.subtitle ? '<span class="quick-search-sub">' + S.highlight(e.subtitle, q) + "</span>" : "") +
        "</a>";
    }).join("");
    if (!hits.length) html = '<div class="quick-search-empty">「' + S.escapeHtml(q) + "」に一致するものはありません</div>";
    html += '<a class="quick-search-all" href="/search?q=' + encodeURIComponent(q) + '">' +
      (hits.length > MAX ? "すべての結果（" + hits.length + " 件）を見る" : "検索ページで見る") + " <span aria-hidden=\"true\">→</span></a>";
    list.innerHTML = html;
    items = Array.prototype.slice.call(list.querySelectorAll(".quick-search-item"));
    active = -1;
    open();
  }

  function move(d) {
    if (!items.length) return;
    // -1 は「どれも選んでいない（入力欄）」。端まで行ったら反対側へ
    active += d;
    if (active >= items.length) active = -1;
    else if (active < -1) active = items.length - 1;
    items.forEach(function (el, i) { el.classList.toggle("is-active", i === active); el.setAttribute("aria-selected", i === active ? "true" : "false"); });
    if (active >= 0) { input.setAttribute("aria-activedescendant", items[active].id); items[active].scrollIntoView({ block: "nearest" }); }
    else input.removeAttribute("aria-activedescendant");
  }

  function open() {
    list.hidden = false;
    input.setAttribute("aria-expanded", "true");
  }

  function close() {
    list.hidden = true;
    active = -1;
    input.setAttribute("aria-expanded", "false");
    input.removeAttribute("aria-activedescendant");
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
