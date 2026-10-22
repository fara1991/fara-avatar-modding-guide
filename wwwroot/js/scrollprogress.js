// スクロールの進み具合を、画面の最上部の細い線（.scroll-progress）に流す。
//  - 0〜1 の数字を html の --scroll-progress に入れる（CSS が scaleX で伸ばす）
//  - ページが短くて、スクロールできないときは 0 のまま
//  - Blazor の画面の切り替えでページの高さが変わったときも、合わせ直す
(function () {
    "use strict";
    var root = document.documentElement;
    var ticking = false;

    function update() {
        ticking = false;
        var max = root.scrollHeight - window.innerHeight;
        var p = max > 0 ? window.scrollY / max : 0;
        p = Math.min(1, Math.max(0, p));
        root.style.setProperty("--scroll-progress", p.toFixed(4));
    }

    function schedule() {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(update);
    }

    window.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", schedule, { passive: true });
    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
    update();
})();
