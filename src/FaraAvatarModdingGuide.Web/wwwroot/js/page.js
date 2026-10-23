// ページの移動まわりの小さな部品(C# から呼ぶ)。
//   avatarPage.scrollToId(id)  … その id の要素が見える位置までスクロールする(#id で飛んできたとき)
//   avatarPage.scrollToHash()  … URL の #id の要素へスクロールする。ページの描画が済むのを待つ(最大 2 秒)。直接開いたとき(静的 HTML で動いたスクロールは、WebAssembly が描き直すと失われる)と、見出し付きの URL へ移動したとき
//   avatarPage.resetScroll()   … 本文のスクロールを一番上に戻す(別のページへ移ったとき。PC は本文の領域 .fsk-content が自分でスクロールするので、Blazor の既定の動き(window)では戻らない)
(function () {
    "use strict";
    window.avatarPage = {
        scrollToId: function (id) {
            var el = document.getElementById(id);
            if (el) el.scrollIntoView({ block: "start", behavior: "instant" });
        },
        scrollToHash: function (smooth) {
            var id;
            try { id = decodeURIComponent(location.hash.slice(1)); } catch (e) { id = location.hash.slice(1); }
            if (!id) return;
            var tries = 0;
            (function find() {
                var el = document.getElementById(id);
                if (el) { el.scrollIntoView({ block: "start", behavior: smooth ? "smooth" : "instant" }); return; }
                if (++tries < 40) setTimeout(find, 50);
            })();
        },
        resetScroll: function () {
            var el = document.querySelector(".fsk-content");
            if (el) el.scrollTop = 0;
            window.scrollTo(0, 0);
        },
    };

    // #id が変わったとき(同じページの中の # のリンク・戻る/進む)に C# へ知らせる(一覧の絞り込みの #id の処理。FilterPage)。
    // Blazor は、同じページの中の # のリンクでは、URL だけ変えて LocationChanged を起こさないので、ここで受ける。
    var hashWatchers = [];
    function notifyHash() {
        hashWatchers.forEach(function (w) { w.invokeMethodAsync("OnHashChanged").catch(function () { }); });
    }
    window.avatarPage.watchHash = function (target) { hashWatchers.push(target); };
    window.avatarPage.unwatchHash = function (target) { hashWatchers = hashWatchers.filter(function (w) { return w !== target; }); };
    window.addEventListener("popstate", notifyHash);
    window.addEventListener("hashchange", notifyHash);

    // 同じページの中の # のリンク(記事の目次など): Blazor がリンクを引き受けて(URL だけ変えて)、ブラウザの既定のスクロールが起きない。
    // Blazor が URL を変えたあとに、その見出しへ動かす。別のページへの # 付きのリンクは、MainLayout(LocationChanged)が動かす。
    document.addEventListener("click", function (e) {
        if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var a = e.target && e.target.closest ? e.target.closest("a[href]") : null;
        if (!a || (a.target && a.target !== "_self") || a.origin !== location.origin || a.pathname !== location.pathname || a.hash.length <= 1) return;
        setTimeout(function () { window.avatarPage.scrollToHash(true); notifyHash(); }, 30);
    });
})();
