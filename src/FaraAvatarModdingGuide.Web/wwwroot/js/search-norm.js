// 検索語の正規化（計画書 D9）。ブラウザの WebAssembly では C# の string.Normalize が使えないので、JavaScript の normalize を C# から呼ぶ。
// 規則は wwwroot/js/searchcore.js の norm() と同じ（NFKC・小文字・全角チルダを 〜 に・カタカナをひらがなに）。同じ結果になることを tests/js/search-norm-fn.test.mjs が確かめる。
// 索引（data/search.json）の正規化はビルド時に済んでいる。
(function () {
    "use strict";

    function norm(s) {
        return String(s).normalize("NFKC").toLowerCase().replace(/~/g, "〜").replace(/[ァ-ヶ]/g, function (c) {
            return String.fromCharCode(c.charCodeAt(0) - 0x60);
        });
    }

    window.avatarSearch = { norm: norm };
})();
