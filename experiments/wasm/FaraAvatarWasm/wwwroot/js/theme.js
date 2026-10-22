// ライト／ダークの保存（本体と同じキー）。localStorage が使えない環境（プライベート閲覧など）では何もしない
// 読み込んだ時点で、保存された値を html の data-bs-theme に入れる（背景の色を、描画の前に合わせる）
(function () {
    try {
        var saved = localStorage.getItem("avatar-modding-guide.theme");
        if (saved === "light" || saved === "dark") document.documentElement.setAttribute("data-bs-theme", saved);
    } catch (e) { }
})();
window.faraTheme = {
    get: function () {
        try { return localStorage.getItem("avatar-modding-guide.theme"); } catch (e) { return null; }
    },
    set: function (theme) {
        try { localStorage.setItem("avatar-modding-guide.theme", theme); } catch (e) { }
        document.documentElement.setAttribute("data-bs-theme", theme);
    }
};
