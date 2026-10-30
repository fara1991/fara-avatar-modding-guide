// テーマが保存されていない（初めて・保存が使えない）ときの既定を、SSR 版と同じダークにする。
// kit の theme-init.js は、保存を読めないときは light にするので、その直後（描画の前）に読んで、ダークに直す。
// storage-migrate.js が、保存が使えるときは dark を書いておく。保存が使えない・書けないとき（プライベートモード等）は、ここでダークにする。
// 保存された値があれば（ユーザーが選んだ light / dark）、何もしない。起動の後の C# 側（StartupError.ApplyDefaultThemeAsync）も同じ規則。
(function () {
    "use strict";
    var KEY = "avatar-modding-guide.theme";
    var saved = null;
    try { saved = localStorage.getItem(KEY); } catch { saved = null; }
    if (!saved) document.documentElement.setAttribute("data-bs-theme", "dark");
})();
