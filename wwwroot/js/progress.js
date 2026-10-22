// 読み終えたページの記録（ログインなし。このブラウザの localStorage にだけ保存する）。
// 静的 HTML に付けた data 属性を見て、完了の印・件数・ボタンの表示を切り替える。
//
//   data-article="basics/transform"            完了なら is-done クラスを付ける（.done-mark が表示される）
//   data-category-progress="basics" data-articles="basics/select basics/transform ..."
//                                              「3/5」のような件数を表示する（0 件なら非表示）
//   data-progress-toggle="basics/transform"    クリックで完了 / 未完了を切り替えるボタン
//   data-progress-summary data-articles="..."  全体の件数と「続きを読む」リンク
//   data-progress-reset                        クリックで記録をすべて消すボタン
//
// 確認用: ?debug=1&seed=basics/select,basics/transform で指定したページを完了にする
(function () {
  "use strict";
  var KEY = "avatar-modding-guide.progress"; // { "category/slug": 完了日時 (ISO 8601) }

  function load() {
    try {
      var v = JSON.parse(localStorage.getItem(KEY) || "{}");
      return v && typeof v === "object" && !Array.isArray(v) ? v : {};
    } catch (e) {
      return {};
    }
  }

  function save(v) {
    try { localStorage.setItem(KEY, JSON.stringify(v)); } catch (e) { /* プライベートモードなど */ }
  }

  function ids(el) {
    return (el.getAttribute("data-articles") || "").split(/\s+/).filter(Boolean);
  }

  var api = {
    all: load,
    isDone: function (id) { return !!load()[id]; },
    set: function (id, done) {
      var v = load();
      if (done) v[id] = v[id] || new Date().toISOString();
      else delete v[id];
      save(v);
      decorate();
    },
    reset: function () {
      save({});
      try { localStorage.removeItem("avatar-modding-guide.tutorials"); } catch (e) { }
      decorate();
    },
  };
  window.faraProgress = api;

  function decorate() {
    var p = load();
    var each = function (sel, fn) { Array.prototype.forEach.call(document.querySelectorAll(sel), fn); };

    each("[data-article]", function (el) {
      el.classList.toggle("is-done", !!p[el.getAttribute("data-article")]);
    });

    each("[data-category-progress]", function (el) {
      var list = ids(el);
      var n = list.filter(function (id) { return p[id]; }).length;
      el.textContent = n + "/" + list.length;
      el.hidden = n === 0;
      el.classList.toggle("is-complete", list.length > 0 && n === list.length);
      el.title = list.length + " ページ中 " + n + " ページ完了";
    });

    each("[data-progress-toggle]", function (el) {
      var done = !!p[el.getAttribute("data-progress-toggle")];
      el.setAttribute("aria-pressed", done ? "true" : "false");
      el.classList.toggle("is-done", done);
      el.innerHTML = done
        ? '<i class="bi bi-check-circle-fill" aria-hidden="true"></i> 完了済み（クリックで取り消す）'
        : '<i class="bi bi-circle" aria-hidden="true"></i> このページを完了にする';
    });

    each("[data-progress-summary]", function (el) {
      var list = ids(el);
      var n = list.filter(function (id) { return p[id]; }).length;
      var next = list.filter(function (id) { return !p[id]; })[0];
      var count = el.querySelector("[data-progress-count]");
      var bar = el.querySelector("[data-progress-bar]");
      var link = el.querySelector("[data-progress-next]");
      if (count) count.textContent = list.length + " ページ中 " + n + " ページ完了";
      if (bar) bar.style.width = (list.length ? (n / list.length) * 100 : 0) + "%";
      if (link) {
        var a = next && document.querySelector('[data-article="' + next + '"] a, a[data-article="' + next + '"]');
        link.hidden = !next;
        if (next) {
          link.href = "/learn/" + next;
          var title = link.querySelector("[data-progress-next-title]");
          if (title) title.textContent = a ? a.textContent.trim() : next;
        }
      }
      if (el.hasAttribute("data-progress-hide-empty")) el.hidden = n === 0;
    });
  }

  document.addEventListener("click", function (e) {
    var toggle = e.target.closest("[data-progress-toggle]");
    if (toggle) {
      var id = toggle.getAttribute("data-progress-toggle");
      api.set(id, !api.isDone(id));
      return;
    }
    if (e.target.closest("[data-progress-reset]")) {
      if (confirm("完了の記録をすべて消します。よろしいですか？")) api.reset();
    }
  });

  // 別のタブで変わったとき
  window.addEventListener("storage", function (e) { if (e.key === KEY) decorate(); });

  var q = new URLSearchParams(location.search);
  if (q.get("debug") === "1" && q.get("seed")) {
    var v = load();
    q.get("seed").split(",").forEach(function (id) { if (id) v[id.trim()] = v[id.trim()] || new Date().toISOString(); });
    save(v);
  }

  // 読み込みのときと、ページ移動のあとに、印を付け直す（nav.js）
  window.faraNav ? window.faraNav.register(decorate) : decorate();
})();
