// ショートカット一覧のカテゴリ絞り込み。?category=xxx を読んで行とボタンの状態を切り替える。
// ページ移動（nav.js）のあとは、新しいページの一覧で init をもう一度呼ぶ。popstate の登録は 1 回だけ。
(function () {
  var currentApply = null;
  window.addEventListener("popstate", function () {
    if (currentApply) currentApply(new URLSearchParams(location.search).get("category") || "");
  });

  function init() {
    currentApply = null;
    const filters = document.getElementById("shortcut-filters");
    const rows = document.getElementById("shortcut-rows");
    if (!filters || !rows) return;

    function apply(category) {
      for (const row of rows.querySelectorAll("tr")) {
        row.hidden = category !== "" && row.dataset.category !== category;
      }
      for (const button of filters.querySelectorAll("a")) {
        const active = button.dataset.category === category;
        button.classList.toggle("btn-primary", active);
        button.classList.toggle("btn-outline-primary", !active);
      }
    }
    currentApply = apply;

    apply(new URLSearchParams(location.search).get("category") || "");

    // ページ遷移なしで切り替え、URL は履歴に残す
    filters.addEventListener("click", (event) => {
      const link = event.target.closest("a[data-category]");
      if (!link) return;
      event.preventDefault();
      history.pushState(null, "", link.getAttribute("href"));
      apply(link.dataset.category);
    });
  }

  window.faraNav.register(init);
})();
