// サイト内の検索欄（PC はヘッダー、スマホは本文の上）。入力するとすぐに候補を出す（↑↓ で選んで Enter、Esc で閉じる）。
// 候補には、種類・小見出し（記事なら章・難しさ）と、一致した部分の前後の文章を、強調付きで出す（どこにヒットしたかが分かる）。
// どのページでも Ctrl+K（Mac は ⌘K）か「/」で、見えている検索欄に移動する。Enter だけなら検索ページ（/search?q=）へ。
(function () {
  "use strict";
  var MAX = 8;
  var boxes = [];        // { form, input, close }。ページ移動で消えた欄は、init のたびに外す
  var globalBound = false;

  function setup(form) {
    var input = form.querySelector("input[type=search]");
    var list = form.querySelector(".quick-search-list");
    if (!input || !list) return;
    if (form.hasAttribute("data-qs-ready")) return; // ヘッダーの欄は、ページが替わっても残るので、二重に付けない
    form.setAttribute("data-qs-ready", "");
    var items = [], active = -1, timer = 0, lastQuery = "";

    function open() { list.hidden = false; input.setAttribute("aria-expanded", "true"); }
    // 結果を開く。ページ移動（nav.js）で開く。同じページの見出し（#）も、ページを読み直さずに移る
    function go(href) {
      close();
      input.blur();
      if (window.faraNav) window.faraNav.navigate(href);
      else location.href = href;
    }
    function close() {
      list.hidden = true;
      active = -1;
      input.setAttribute("aria-expanded", "false");
      input.removeAttribute("aria-activedescendant");
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
        return '<a class="quick-search-item" role="option" id="' + list.id + "-" + i + '" href="' + S.escapeHtml(e.url) + '" aria-selected="false">' +
          '<span class="badge ' + t.cls + '">' + S.escapeHtml(t.label) + "</span>" +
          '<span class="quick-search-body">' +
          '<span class="quick-search-line">' +
          '<span class="quick-search-title">' + S.highlight(e.title, q) + "</span>" +
          (e.subtitle ? '<span class="quick-search-sub">' + S.highlight(e.subtitle, q) + "</span>" : "") +
          "</span>" +
          '<span class="quick-search-snippet">' + S.highlight(S.snippet(e.text, q, 100), q) + "</span>" +
          "</span></a>";
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

    input.addEventListener("focus", function () {
      window.faraSearch.load().catch(function () { /* 失敗したら入力時に表示 */ });
      if (input.value.trim()) update();
    });
    input.addEventListener("input", function () {
      clearTimeout(timer);
      timer = setTimeout(update, 60);
    });
    input.addEventListener("keydown", function (e) {
      if (e.isComposing || e.keyCode === 229) return; // 日本語入力の変換中は、Enter などを奪わない（Safari は、確定の Enter で isComposing が false になるので keyCode でも見る）
      if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        if (list.hidden) { update(); return; }
        e.preventDefault();
        move(e.key === "ArrowDown" ? 1 : -1);
      } else if (e.key === "Enter") {
        // 入力してすぐ（検索の待ち時間の間）に Enter を押したら、古い入力の結果を開かず、先に今の入力で探す
        if (input.value.trim() !== lastQuery) { e.preventDefault(); clearTimeout(timer); update(); return; }
        if (!list.hidden && active >= 0 && items[active]) {
          e.preventDefault();
          go(items[active].getAttribute("href"));
        }
        // 選んでいなければフォームの送信（検索ページ）
      } else if (e.key === "Escape") {
        if (!list.hidden) { e.preventDefault(); close(); }
        else input.blur();
      }
    });
    list.addEventListener("mousedown", function (e) { e.preventDefault(); }); // クリックで入力欄のフォーカスを外さない
    list.addEventListener("click", function (e) {
      var item = e.target.closest && e.target.closest(".quick-search-item");
      if (!item) return;
      e.preventDefault();
      go(item.getAttribute("href"));
    });
    boxes.push({ form: form, input: input, close: close });
  }

  function init() {
    if (!window.faraSearch) return;
    boxes = boxes.filter(function (b) { return b.form.isConnected; });
    Array.prototype.forEach.call(document.querySelectorAll("[data-quick-search]"), setup);
    if (globalBound) return;
    globalBound = true;

    document.addEventListener("click", function (e) {
      boxes.forEach(function (b) { if (!b.form.contains(e.target)) b.close(); });
    });
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

  /** いま見えている検索欄（PC はヘッダー、スマホは本文の上）に移る */
  function focusInput() {
    var box = boxes.filter(function (b) { return b.form.offsetParent !== null; })[0];
    if (!box) return;
    box.input.focus();
    box.input.select();
  }

  window.faraNav.register(init);
})();
