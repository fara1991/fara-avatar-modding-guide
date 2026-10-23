// localStorage の変更を見張る(ほかのタブで完了の記録が変わったときに、このタブの表示を合わせる。SSR 版の progress.js の storage イベントと同じ)。
// C# の ProgressStore.EnsureLoaded が avatarStorage.watch を呼ぶ。
(function () {
    "use strict";
    var watchers = [];
    window.addEventListener("storage", function (e) {
        watchers.forEach(function (w) {
            // key が null は localStorage.clear()。どちらも読み直す
            if (e.key === null || e.key === w.key) w.target.invokeMethodAsync("OnStorageChanged");
        });
    });
    window.avatarStorage = {
        watch: function (key, target) { watchers.push({ key: key, target: target }); },
        // SSR 版の progress.js が公開していた window.faraProgress(isDone・set)を、C# の ProgressStore につなぐ。練習の tutorial.js(フェーズ 3)は、これをそのまま使う。
        // WebAssembly では、JavaScript から C# を同期で呼べる(invokeMethod)
        bridge: function (target) {
            window.faraProgress = {
                isDone: function (id) { return target.invokeMethod("IsDoneForScript", id); },
                set: function (id, done) { target.invokeMethod("SetForScript", id, !!done); },
            };
        },
    };
})();
