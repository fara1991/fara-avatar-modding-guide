// 検索欄(AvSearchBox.razor)のキー操作。Blazor の @onkeydown では間に合わないものだけ、ここで受ける。
//   ・日本語入力の変換中(確定の Enter 含む)のキーは、候補の選択に使わせない。Safari は確定の Enter を keyCode=229 で送る。capture で止めて、Blazor に届かせない
//   ・候補を開いているあいだは、↑↓ Enter で欄の中の文字のカーソルを動かさない(←→ は別の開き方を選んでいるときだけ。このサイトは使わない)
//   ・Ctrl+K(Mac は ⌘K)か「/」(文字を入力中は奪わない)で、見えている検索欄へ移る。スマホなどで欄が見えないときは、ヘッダーの検索ボタンを押す
//   ・候補をクリックしても、欄からフォーカスを外さない(外れると、クリックが届く前に候補が閉じる)
// インラインの script は CSP で使えないので、外部ファイルにしている。
(function () {
    "use strict";
    var INPUT = ".av-search-input";

    document.addEventListener("keydown", function (e) {
        var t = e.target;
        if (t && t.matches && t.matches(INPUT)) {
            if (e.isComposing || e.keyCode === 229) { e.stopImmediatePropagation(); return; }
            if (t.getAttribute("aria-expanded") === "true" && (e.key === "ArrowDown" || e.key === "ArrowUp" || e.key === "Enter")) e.preventDefault();
        }
    }, true);

    /** 文字入力中や 3D ビューの操作中は「/」を奪わない */
    function isTyping(el) {
        if (!el || el === document.body) return false;
        if (el.isContentEditable) return true;
        var tag = el.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return true;
        return !!(el.closest && el.closest(".practice-viewport, .unity-editor"));
    }

    document.addEventListener("keydown", function (e) {
        var k = e.key && e.key.toLowerCase();
        var ctrlK = (e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && k === "k";
        var slash = e.key === "/" && !e.ctrlKey && !e.metaKey && !e.altKey && !isTyping(e.target);
        if ((!ctrlK && !slash) || e.isComposing) return;
        var inputs = document.querySelectorAll(INPUT);
        var input = null;
        for (var i = 0; i < inputs.length; i++) { if (inputs[i].offsetParent !== null) { input = inputs[i]; break; } }
        if (!input) {
            // スマホ: ヘッダーの検索ボタン(kit の SiteLayout)で欄を開く
            var toggle = document.querySelector(".fbc-search-toggle");
            if (!toggle) return;
            e.preventDefault();
            toggle.click();
            return;
        }
        e.preventDefault();
        input.focus();
        input.select();
    });

    document.addEventListener("mousedown", function (e) {
        if (e.target && e.target.closest && e.target.closest(".av-search-list")) e.preventDefault();
    }, true);
})();
