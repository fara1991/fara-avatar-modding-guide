// 用語集・何て名前？・TIPS の絞り込み（文字とタグ）。?tag=Unity のように URL でも指定できる。
// #id で飛んできたときは、その項目が隠れないように絞り込みを外し、「詳しく」を開く。
// ページ移動（nav.js）のあとは、新しいページの一覧で init をもう一度呼ぶ。hashchange の登録は 1 回だけ。
(function () {
  "use strict";
  var currentShowHash = null;
  window.addEventListener("hashchange", function () { if (currentShowHash) currentShowHash(); });

  function init() {
    currentShowHash = null;
    var box = document.querySelector("[data-tips-filter]");
    if (!box) return;
    var input = box.querySelector("[data-tips-q]");
    var buttons = Array.prototype.slice.call(box.querySelectorAll("[data-tag]"));
    var count = box.querySelector("[data-tips-count]");
    var items = Array.prototype.slice.call(document.querySelectorAll("[data-tip-item]"));
    var norm = function (s) { return String(s).normalize("NFKC").toLowerCase(); };
    var texts = items.map(function (el) {
      var card = el.querySelector(".tip-card");
      return norm(el.textContent + " " + (card.getAttribute("data-aliases") || ""));
    });
    var params = new URLSearchParams(location.search);
    var tag = params.get("tag") || "";

    function apply() {
      var terms = norm(input.value).split(/\s+/).filter(Boolean);
      var shown = 0;
      items.forEach(function (el, i) {
        var card = el.querySelector(".tip-card");
        var okTag = !tag ||
          (tag === "source:discord" ? card.getAttribute("data-source") === "discord" : card.getAttribute("data-tags").split("|").indexOf(tag) >= 0);
        var okText = terms.every(function (t) { return texts[i].indexOf(t) >= 0; });
        el.hidden = !(okTag && okText);
        if (!el.hidden) shown++;
      });
      buttons.forEach(function (b) {
        var on = b.getAttribute("data-tag") === tag;
        b.classList.toggle("btn-primary", on);
        b.classList.toggle("btn-outline-primary", !on);
        b.setAttribute("aria-pressed", on ? "true" : "false");
      });
      count.textContent = shown === items.length ? items.length + " 件" : items.length + " 件中 " + shown + " 件";
    }

    buttons.forEach(function (b) {
      b.addEventListener("click", function () {
        tag = b.getAttribute("data-tag");
        var p = new URLSearchParams(location.search);
        if (tag) p.set("tag", tag); else p.delete("tag");
        var q = p.toString();
        history.replaceState(null, "", location.pathname + (q ? "?" + q : "") + location.hash);
        apply();
      });
    });
    input.addEventListener("input", apply);

    function showHash() {
      var id = decodeURIComponent(location.hash.slice(1));
      var target = id && document.getElementById(id);
      if (!target || !target.closest("[data-tip-item]")) return;
      if (target.closest("[data-tip-item]").hidden) { tag = ""; input.value = ""; apply(); }
      var details = target.querySelector("details.tip-body");
      if (details) details.open = true;
      target.scrollIntoView({ block: "start" });
    }
    currentShowHash = showHash;
    apply();
    showHash();
  }

  window.faraNav.register(init);
})();
