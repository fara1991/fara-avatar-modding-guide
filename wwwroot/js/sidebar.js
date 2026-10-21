// サイドバーの表示・非表示（PC）。左上のハンバーガー（[data-sidebar-toggle]）を押すたびに切り替える（Google の管理画面と同じ）。
// 状態は html の sidebar-hidden クラス（最初の反映は App.razor の head のスクリプト）。選んだ状態はこのブラウザに保存する
(function () {
    "use strict";
    var KEY = "avatar-modding-guide.sidebar-hidden";
    var root = document.documentElement;

    // aria-expanded は、サイドバーが開いているとき true、隠しているとき false
    function sync() {
        var open = !root.classList.contains("sidebar-hidden");
        var buttons = document.querySelectorAll("[data-sidebar-toggle]");
        for (var i = 0; i < buttons.length; i++) buttons[i].setAttribute("aria-expanded", open ? "true" : "false");
        return buttons.length > 0;
    }

    document.addEventListener("click", function (e) {
        if (!e.target.closest || !e.target.closest("[data-sidebar-toggle]")) return;
        var hidden = !root.classList.contains("sidebar-hidden");
        root.classList.toggle("sidebar-hidden", hidden);
        try { localStorage.setItem(KEY, hidden ? "1" : "0"); } catch (err) { }
        sync();
        // 記事の幅が変わるので、右の目次の強調（toc.js）を測り直させる
        window.dispatchEvent(new Event("resize"));
    });
    sync();
})();
