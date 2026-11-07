// 記事の目次(ArticlePage.razor)。今読んでいる h2 を見つけて、C# に知らせる(C# が目次のリンクを is-current・aria-current="location" にする)。
// 見出しが画面の上の方を通ったかどうかで決める。ページの一番下まで来たら最後の見出しにする。旧 SSR 版の toc.js 相当。
// スクロールするのは、PC は本文の領域(.fbc-content。kit のレイアウト)、スマホ(MobilePageScroll)は window。どちらでも動くよう、document で scroll を捕まえる。
// 記事が替わったら avatarToc.start を呼び直す(前の監視は止める)。
(function () {
    "use strict";
    var state = null;

    /** 本文の領域(.fbc-content)が自分でスクロールしているなら、それ。そうでなければ null(window がスクロールする) */
    function scroller() {
        var el = document.querySelector(".fbc-content");
        if (!el) return null;
        var overflow = getComputedStyle(el).overflowY;
        return overflow === "auto" || overflow === "scroll" ? el : null;
    }

    function atBottom(sc) {
        return sc
            ? sc.scrollTop + sc.clientHeight >= sc.scrollHeight - 2
            : window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
    }

    /** 見出しが「上に出た」とみなす位置(スクロール領域の上端からの距離)。scroll-padding-top と同じ(見出しへのジャンプで止まる位置) */
    function line(sc) {
        var v = parseFloat(getComputedStyle(sc || document.documentElement).scrollPaddingTop);
        return isNaN(v) ? 72 : v;
    }

    function decode(a) {
        try { return decodeURIComponent(a.hash.slice(1)); } catch { return a.hash.slice(1); }
    }

    /** 目次が縦にあふれて内側でスクロールしているときだけ、強調するリンクが見える位置にする */
    function reveal(id) {
        var links = document.querySelectorAll(".toc-list a[href*='#']");
        for (var i = 0; i < links.length; i++) {
            if (decode(links[i]) !== id) continue;
            var box = links[i].closest(".article-toc");
            if (box && box.scrollHeight > box.clientHeight && box.offsetParent) {
                var r = links[i].getBoundingClientRect(), b = box.getBoundingClientRect();
                if (r.top < b.top || r.bottom > b.bottom) box.scrollTop += r.top - b.top - 40;
            }
        }
    }

    function stop() {
        if (!state) return;
        document.removeEventListener("scroll", state.onScroll, true);
        window.removeEventListener("resize", state.onScroll);
        if (state.frame) cancelAnimationFrame(state.frame);
        state = null;
    }

    function start(target, ids) {
        stop();
        var headings = [];
        for (var i = 0; i < ids.length; i++) {
            var h = document.getElementById(ids[i]);
            if (h) headings.push(h);
        }
        if (headings.length === 0) return;
        var s = { target: target, current: undefined, frame: 0, onScroll: null };
        function update() {
            s.frame = 0;
            var sc = scroller();
            var top = sc ? sc.getBoundingClientRect().top : 0;
            var found = null;
            if (atBottom(sc)) found = headings[headings.length - 1];
            else for (var j = 0; j < headings.length; j++) { if (headings[j].getBoundingClientRect().top - top <= line(sc) + 8) found = headings[j]; else break; }
            var id = found ? found.id : null;
            if (id === s.current) return;
            s.current = id;
            target.invokeMethodAsync("SetCurrentHeading", id).catch(function () { /* 記事を離れたあと */ });
            if (id) reveal(id);
        }
        // スクロールのたびに計算せず、描画の直前に 1 回だけ
        s.onScroll = function () { if (!s.frame) s.frame = requestAnimationFrame(update); };
        document.addEventListener("scroll", s.onScroll, { capture: true, passive: true });
        window.addEventListener("resize", s.onScroll);
        state = s;
        update();
    }

    window.avatarToc = { start: start, stop: stop };
})();
