// 試作: 検索ページの島（Blazor WebAssembly。islands/search）と、ページの間をつなぐ小さな関数。
// 島（C#）から呼ばれる。島は本文の中の script（この次に _framework/blazor.webassembly.js）で読み込まれる。
(function () {
  "use strict";
  window.faraIslandSearch = {
    // いまの URL の ?q= を返す（ページ移動で URL が変わったあとに島ができるので、その時点の値）
    query: function () {
      try { return new URLSearchParams(location.search).get("q") || ""; } catch (e) { return ""; }
    },
    // 検索欄に語を入れる（SSR の欄は空で返るため）
    setInput: function (q) {
      var input = document.getElementById("search-input");
      if (input) input.value = q;
    },
    // 結果を描いたことを印す（計測用。performance と data 属性）
    ready: function () {
      try { performance.mark("island-search-ready"); } catch (e) { }
      var el = document.querySelector("fara-search");
      if (el) el.setAttribute("data-ready", "1");
    },
  };
})();
