// サイドバーの表示・非表示（PC）。ボタンは [data-sidebar-toggle]（サイドバーの「隠す」と、隠したときの左上の「開く」）。
// 状態は html の sidebar-hidden クラス（最初の反映は App.razor の head のスクリプト）。選んだ状態はこのブラウザに保存する。
// 「隠す」ボタンは開いているときだけ、「開く」ボタンは隠しているときだけ見えるので、aria-expanded は Razor に固定で書く
(function () {
    "use strict";
    var KEY = "avatar-modding-guide.sidebar-hidden";
    var root = document.documentElement;

    document.addEventListener("click", function (e) {
        if (!e.target.closest || !e.target.closest("[data-sidebar-toggle]")) return;
        var hidden = !root.classList.contains("sidebar-hidden");
        root.classList.toggle("sidebar-hidden", hidden);
        try { localStorage.setItem(KEY, hidden ? "1" : "0"); } catch (err) { }
        // 記事の幅が変わるので、右の目次の強調（toc.js）を測り直させる
        window.dispatchEvent(new Event("resize"));
        // 隠したら「開く」ボタンへ、開いたら「隠す」ボタンへ、キーボードの位置を移す
        var next = document.querySelector(hidden ? ".sidebar-reveal" : ".sidebar-hide");
        if (next) next.focus();
    });
})();
