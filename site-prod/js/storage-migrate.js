// 保存されたテーマの形を、描画の前に直す（head で、kit の theme-init.js より前に読む。インラインの script は CSP で使えないので外部ファイル）。
//
// SSR 版は localStorage の "avatar-modding-guide.theme" に、文字列 "light" / "dark" をそのまま入れていた。
// kit（fara-site-kit）は同じキーに JSON（{"Theme":"dark"}）を入れて読む。SSR 版の保存値のままだと読めず、保存していたテーマが失われるので、
// 文字列の値を JSON に書き換える（同じキー。ユーザーが選んだテーマは変わらない）。
// 何も保存されていない（初めて・消した）ときは、SSR 版の既定のダークにする。
// "avatar-modding-guide.sidebar-hidden"（"1" で隠す）は、kit と同じ形なので、そのまま使う。
(function () {
    "use strict";
    var KEY = "avatar-modding-guide.theme";
    try {
        var value = localStorage.getItem(KEY);
        if (value === "light" || value === "dark") {
            localStorage.setItem(KEY, JSON.stringify({ Theme: value }));
        } else if (value === null) {
            localStorage.setItem(KEY, JSON.stringify({ Theme: "dark" }));
        }
    } catch (e) {
        // localStorage が使えない（プライベートモード等）ときは何もしない（theme-init.js が light にする。SiteLayout は保存できなくても動く）
    }
})();
