// 上のヘッダーの開閉するメニュー(details.av-more「もっと見る」・details.av-menu「メニュー」)の後始末。
// ブラウザの details は、クリックで開閉するが、Esc・外側のクリック・項目を選んだあとには閉じないので、ここで閉じる。
// 文書全体で受ける(静的 HTML のあいだも、起動の後も同じ。Blazor は details の open を持たないので、描き直しても競合しない)。
(function () {
    "use strict";
    var SELECTOR = "details.av-more[open], details.av-menu[open]";
    function opened() { return Array.prototype.slice.call(document.querySelectorAll(SELECTOR)); }

    // Esc: 開いているメニューを閉じ、フォーカスを見出し(summary)に戻す
    document.addEventListener("keydown", function (e) {
        if (e.key !== "Escape") return;
        var list = opened();
        if (list.length === 0) return;
        var summary = list[0].querySelector("summary");
        list.forEach(function (d) { d.removeAttribute("open"); });
        if (summary) summary.focus();
    });

    // 外側のクリック、または項目(リンク)を選んだとき: 閉じる
    document.addEventListener("click", function (e) {
        var target = e.target;
        opened().forEach(function (d) {
            if (!d.contains(target) || (target.closest && target.closest("a[href]"))) d.removeAttribute("open");
        });
    });

    // Tab などで、メニューの外にフォーカスが移ったとき: 閉じる
    document.addEventListener("focusin", function (e) {
        var target = e.target;
        opened().forEach(function (d) { if (!d.contains(target)) d.removeAttribute("open"); });
    });
})();
