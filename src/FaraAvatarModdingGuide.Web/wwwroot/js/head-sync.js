// アプリ内の移動(Blazor の画面の切り替え)のあとに、head の description・canonical・OGP・robots を、移った先のページのものに合わせる。
//   avatarHead.sync(path)  … data/pages.json(Build が書く。パス → 題名・説明・og:type・noindex)から、そのパスの値を引いて、head の既存のタグを書き換える
// 静的 HTML の head は、そのページ用に Build が書いたもの。アプリ内で別のページへ移ると、題名(title)だけは PageTitle が替えるが、
// そのほかは最初のページの値のまま残る。検索エンジンは URL ごとに静的 HTML(の描画結果)を読むので、順位には影響しないが、
// 開いている画面の head が別のページの値を持たないように(DOM を読む拡張機能・共有の部品など)、ここで合わせる。
// 既存のタグを書き換える(「全ページに 1 つずつ」を保つ)。無いタグ(404.html から移ったとき)は、固定の meta/link だけを 1 つ作る(同じタグを 2 つにはしない)。robots は noindex のページでだけ足す。pages.json に無いパス(404 など)は何もしない。
(function () {
    "use strict";
    var pages = null;      // 読み込み済みの { パス: { title, description, ogType, noindex } }
    var loading = null;    // 読み込み中の Promise(1 回だけ読む)

    function load() {
        if (pages) return Promise.resolve(pages);
        if (!loading) {
            loading = fetch("data/pages.json", { credentials: "same-origin" })
                .then(function (r) { if (!r.ok) throw new Error(String(r.status)); return r.json(); })
                .then(function (j) { pages = j; return j; })
                .catch(function () { loading = null; return null; });   // 読めなければ何もしない(次の移動で、もう一度試す)
        }
        return loading;
    }

    // 既存のタグの内容を替える。無いタグ(404.html から移ったとき。404.html の head は title と noindex だけ)は、固定の meta/link だけを作って足す(innerHTML は使わない)
    function ensure(tag, keyAttr, keyValue) {
        var el = document.querySelector(tag + "[" + keyAttr + '="' + keyValue + '"]');
        if (!el) {
            el = document.createElement(tag);
            el.setAttribute(keyAttr, keyValue);
            document.head.appendChild(el);
        }
        return el;
    }

    function setContent(keyAttr, keyValue, value) {
        if (typeof value === "string") ensure("meta", keyAttr, keyValue).setAttribute("content", value);
    }

    function apply(path, entry) {
        var canonical = document.querySelector('link[rel="canonical"]');
        var origin = canonical && canonical.getAttribute("href") ? new URL(canonical.getAttribute("href"), location.href).origin : location.origin;
        var url = origin + path;
        ensure("link", "rel", "canonical").setAttribute("href", url);
        setContent("name", "description", entry.description);
        setContent("property", "og:description", entry.description);
        setContent("property", "og:title", entry.title);
        setContent("property", "og:type", entry.ogType);
        setContent("property", "og:url", url);
        var robots = document.querySelector('meta[name="robots"]');
        if (entry.noindex) {
            if (!robots) {
                robots = document.createElement("meta");
                robots.setAttribute("name", "robots");
                robots.setAttribute("content", "noindex");
                document.head.appendChild(robots);
            }
        } else if (robots && robots.getAttribute("content") === "noindex") {
            robots.parentNode.removeChild(robots);
        }
    }

    window.avatarHead = {
        sync: function (path) {
            var key = path.length > 1 ? path.replace(/\/+$/, "") : path;
            return load().then(function (map) {
                var entry = map && Object.prototype.hasOwnProperty.call(map, key) ? map[key] : null;
                // 待っているあいだに、さらに別のページへ移っていたら、古い結果で上書きしない
                var current = location.pathname.length > 1 ? location.pathname.replace(/\/+$/, "") : location.pathname;
                if (entry && current === key) apply(key, entry);
            });
        },
    };
})();
