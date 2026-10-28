// ページの移動まわりの小さな部品(C# から呼ぶ)。
//   avatarPage.scrollToId(id)  … その id の要素が見える位置までスクロールする(#id で飛んできたとき)
//   avatarPage.scrollToHash()  … URL の #id の要素へスクロールする。ページの描画が済むのを待つ(最大 2 秒)。直接開いたとき(静的 HTML で動いたスクロールは、WebAssembly が描き直すと失われる)と、見出し付きの URL へ移動したとき
//   avatarPage.restoreBootScroll() … 起動の前の静的な本文で動かしたスクロールを、起動の後に引き継ぐ(起動の後の最初の描画のあとに 1 回)
//   avatarPage.resetScroll()   … 本文のスクロールを一番上に戻す(別のページへ移ったとき。PC は本文の領域 .fsk-content が自分でスクロールするので、Blazor の既定の動き(window)では戻らない)
(function () {
    "use strict";
    // 戻る/進むのとき、ブラウザの既定のスクロール復元は、本文の領域(.fsk-content。PC)の中では効かない(window だけ)。
// ページごとのスクロール位置を sessionStorage に覚えておき、popstate(戻る/進む)のあとに、描画が済むのを待って戻す。
try { history.scrollRestoration = "manual"; } catch { /* 対応しない環境では、既定のまま */ }   // 既定の復元は、本文の領域の中では効かず、効く(window の)ときも、描き直しとぶつかる
var lastPopAt = -1e9;
var restoreTarget = null;   // popstate から、復元が終わるまで(その間は、保存し直さない)
function poppingNow() { return performance.now() - lastPopAt < 1500; }
function scrollKey() { return "avatar-scroll:" + location.pathname + location.search; }
function scroller() {
    var el = document.querySelector(".fsk-content");
    return el && /^(auto|scroll)$/.test(getComputedStyle(el).overflowY) ? el : null;
}
function currentTop() { var el = scroller(); return el ? el.scrollTop : window.scrollY; }
var saveFrame = 0;
document.addEventListener("scroll", function () {
    if (restoreTarget !== null || saveFrame) return;
    saveFrame = requestAnimationFrame(function () {
        saveFrame = 0;
        try { sessionStorage.setItem(scrollKey(), String(Math.round(currentTop()))); } catch { /* 保存できないときは、位置を覚えないだけ（プライベートモードなど） */ }
    });
}, { capture: true, passive: true });
window.addEventListener("popstate", function () {
    lastPopAt = performance.now();
    var saved = null;
    try { saved = sessionStorage.getItem(scrollKey()); } catch { /* 読めないときは、保存された位置なしとして扱う */ }
    restoreTarget = saved === null ? null : Number(saved);
    if (restoreTarget === null) return;
    var target = restoreTarget, tries = 0, cancelled = false;
    function cancel() { cancelled = true; }
    ["wheel", "touchstart", "mousedown", "keydown"].forEach(function (t) { window.addEventListener(t, cancel, { once: true, passive: true, capture: true }); });
    // Blazor が新しいページを描くまでの間に、本文の高さが変わって位置がずれるので、約 1.5 秒のあいだ、位置を合わせ続ける(利用者が動かしたら止める)
    (function apply() {
        var el = scroller();
        var max = el ? el.scrollHeight - el.clientHeight : document.documentElement.scrollHeight - window.innerHeight;
        var y = Math.min(target, Math.max(0, max));
        if (el) el.scrollTop = y; else window.scrollTo(0, y);
        if (!cancelled && ++tries < 30) { setTimeout(apply, 50); return; }
        ["wheel", "touchstart", "mousedown", "keydown"].forEach(function (t) { window.removeEventListener(t, cancel, true); });
        restoreTarget = null;
    })();
});

// 起動の前(静的 HTML。.av-static がある間)の window のスクロール位置を覚えておく(restoreBootScroll)
var bootScrollY = 0, bootTracking = true;
window.addEventListener("scroll", function () {
    if (bootTracking && document.querySelector(".av-static")) bootScrollY = window.scrollY;
}, { passive: true });

window.avatarPage = {
        scrollToId: function (id) {
            var el = document.getElementById(id);
            if (el) el.scrollIntoView({ block: "start", behavior: "instant" });
        },
        scrollToHash: function (smooth) {
            var id;
            try { id = decodeURIComponent(location.hash.slice(1)); } catch { id = location.hash.slice(1); }
            if (!id || (poppingNow() && restoreTarget !== null)) return;   // 戻る/進むで、保存した位置があるときは、その位置を優先する
            var tries = 0;
            (function find() {
                var el = document.getElementById(id);
                if (el) { el.scrollIntoView({ block: "start", behavior: smooth ? "smooth" : "instant" }); return; }
                if (++tries < 40) setTimeout(find, 50);
            })();
        },
        // 起動の前の静的な本文で動かしたスクロール(window)を、起動の後に引き継ぐ。起動すると Blazor が #app の中身を置き換え、
        // PC は本文の領域(.fsk-content)の中がスクロールする画面に替わる(スマホは window のまま)ので、そのままだと一番上に戻ってしまう。
        // 一番上で見ていたとき・戻る/進むの復元中・#見出しの URL(scrollToHash が動かす)では何もしない
        restoreBootScroll: function () {
            bootTracking = false;
            var y = bootScrollY;
            if (!y || poppingNow() || location.hash.length > 1) return;
            var tries = 0;
            (function apply() {
                var el = scroller();
                var max = el ? el.scrollHeight - el.clientHeight : document.documentElement.scrollHeight - window.innerHeight;
                if (max >= y || tries >= 20) {
                    if (el) el.scrollTop = Math.min(y, Math.max(0, max)); else window.scrollTo(0, Math.min(y, Math.max(0, max)));
                    return;
                }
                tries++;
                setTimeout(apply, 50);
            })();
        },
        resetScroll: function () {
            if (poppingNow()) return;   // 戻る/進むのときは、戻さない(スクロール位置は、保存した値に復元する)
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
