// 「更新予定」のお知らせの帯を、静的 HTML のままのページ（WebAssembly を読み込まないページ。記事など）に出す。
// WebAssembly のページでは、kit の MaintenanceBanner が出す（同じ見た目の fsk-notice）。このファイルは、Build が WebAssembly を使わないページにだけ読み込ませる
// （index.html の STATIC-ONLY の印）。記事などが WebAssembly になるフェーズ 2〜3 で、不要になった分から外す。
// Worker の GET /api/maintenance（{state, message}）を、ページを開いたときに 1 回だけ読む。2 秒で打ち切り、読めなければ何も出さない。state が notice のときだけ帯を出す。
(function () {
    "use strict";
    var TIMEOUT_MS = 2000;
    var DEFAULT_MESSAGE = "まもなくメンテナンスを行います。";   // kit の MaintenanceInfo.NoticeText と同じ

    /** 応答の JSON から、お知らせの文を取り出す。お知らせでなければ null。 */
    function parse(text) {
        try {
            var json = JSON.parse(text);
            if (!json || typeof json !== "object") return null;
            var state = typeof json.state === "string" ? json.state.trim().toLowerCase() : "";
            if (state !== "notice") return null;
            var message = typeof json.message === "string" ? json.message.trim() : "";
            return { message: message || DEFAULT_MESSAGE };
        } catch {
            return null;   // Worker が無く、HTML が返ってきたときなど
        }
    }

    /** 帯を作って、静的なページの枠（.av-static）の先頭に入れる。文は textContent で入れる（HTML としては扱わない）。 */
    function render(doc, info) {
        var host = doc.querySelector(".av-static");
        if (!host || host.querySelector(".fsk-notice")) return null;
        var band = doc.createElement("div");
        band.className = "fsk-notice";
        band.setAttribute("role", "status");
        var title = doc.createElement("strong");
        title.className = "fsk-notice-title";
        title.textContent = "【お知らせ】";
        var text = doc.createElement("span");
        text.className = "fsk-notice-text";
        text.textContent = info.message;
        band.appendChild(title);
        band.appendChild(text);
        host.insertBefore(band, host.firstChild);
        return band;
    }

    window.avatarNotice = { parse: parse, render: render };

    if (typeof document === "undefined" || typeof fetch !== "function" || !document.querySelector(".av-static")) return;
    var controller = typeof AbortController === "function" ? new AbortController() : null;
    var timer = setTimeout(function () { if (controller) controller.abort(); }, TIMEOUT_MS);
    fetch("/api/maintenance", { cache: "no-store", signal: controller ? controller.signal : undefined })
        .then(function (response) { return response.ok ? response.text() : ""; })
        .then(function (text) {
            clearTimeout(timer);
            var info = parse(text);
            if (info) render(document, info);
        })
        .catch(function () { clearTimeout(timer); });
})();
