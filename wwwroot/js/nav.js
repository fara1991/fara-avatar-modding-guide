// ページ移動を、画面を読み直さずに行う（拡張ナビゲーション）。
// 同じサイトのリンクを押すと、次のページの HTML を取ってきて、本文（#main-content）と左のメニュー（.sidebar-panel）だけを差し替える。
// ヘッダー・背景の 3D・スクロールの線・サイドバーの開閉は、そのまま残る（差し替えない）。
//  - 検索エンジンや直接アクセスには、今までどおりページごとの完全な HTML が返る（この仕組みは、JS がないときには何もしない）
//  - 3D（three.js）や WebSocket を使うページ（type="module" / importmap の script を持つページ）は、従来どおり読み込み直す
//  - ページ専用の JS は faraNav.register(init) で登録する。読み込み時に 1 回、移動のあとにもう一度呼ばれる。init は何度呼ばれても二重に登録しないこと
//  - 戻る・進むは history の state に保存したスクロール位置に戻す
//  - 失敗したら、通常のページ読み込みに戻す
(function () {
  "use strict";
  if (window.faraNav) return;

  var MAIN_ID = "main-content";
  var MODULE_SEL = 'script[type="module"], script[type="importmap"]';
  // 差し替えるときに、ページごとに変わる <head> の要素（説明文・正規の URL・OGP など）。viewport と charset は変えない
  var HEAD_SEL = 'meta[name]:not([name="viewport"]), meta[property], link[rel="canonical"]';

  var handlers = [];
  var loading = 0;   // ページ専用スクリプトを読み込んでいる間は、登録された init をすぐには呼ばない（移動のあとにまとめて呼ぶ）
  var seq = 0;       // 移動の番号。追い越された古い移動は捨てる
  var loaded = {};   // 読み込み済みのページ専用スクリプト（絶対 URL）
  var rendered = ""; // いま画面に出ているページ（パス + 検索）
  var saveTimer = 0;

  function safeCall(fn) {
    try { fn(); } catch (err) { console.error(err); }
  }

  /** ページ専用の初期化を登録する。読み込み中でなければ、いま 1 回呼ぶ。 */
  function register(fn) {
    handlers.push(fn);
    if (loading === 0) safeCall(fn);
  }

  function emit() {
    handlers.forEach(function (fn) { safeCall(fn); });
  }

  /** 同じサイトのページとして、ページの差し替えで開いてよいか（画像・JSON・API は対象外）。 */
  function isRoutable(url, origin) {
    if (url.origin !== origin) return false;
    var p = url.pathname;
    if (p.indexOf("/api/") === 0 || p.indexOf("/_") === 0) return false;
    if (/\.[A-Za-z0-9]+$/.test(p) && !/\.html$/.test(p)) return false;
    return true;
  }

  var api = {
    register: register,
    isRoutable: isRoutable,
    /** 同じサイトのリンクを、ページの差し替えで開く（検索の候補など、JS から開くとき）。 */
    navigate: function (href) { return navigate(href, { push: true, y: 0 }); },
  };
  window.faraNav = api;

  if (typeof document === "undefined") return; // テスト（node）では、判定の関数だけを使う

  function hasModuleScripts(root) {
    return !!root.querySelector(MODULE_SEL);
  }

  function currentKey(loc) {
    return loc.pathname + loc.search;
  }

  function saveScroll() {
    try {
      history.replaceState(Object.assign({}, history.state, { faraNav: 1, y: window.scrollY }), "");
    } catch (err) { /* 履歴の変更が制限されているとき */ }
  }

  function loadScript(src) {
    return new Promise(function (resolve, reject) {
      var s = document.createElement("script");
      s.src = src;
      s.async = false;
      s.onload = function () { loaded[src] = true; resolve(); };
      s.onerror = function () { reject(new Error("読み込めません: " + src)); };
      document.body.appendChild(s);
    });
  }

  /** 新しいページの本文にある、まだ読んでいないページ専用スクリプトの URL。 */
  function pageScriptSources(doc) {
    var out = [];
    var base = location.origin + "/";
    Array.prototype.forEach.call(doc.querySelectorAll("#" + MAIN_ID + " script[src]"), function (s) {
      var abs = new URL(s.getAttribute("src"), base).href;
      if (!loaded[abs] && out.indexOf(abs) < 0) out.push(abs);
    });
    return out;
  }

  function swap(doc) {
    var newMain = doc.getElementById(MAIN_ID);
    var oldMain = document.getElementById(MAIN_ID);
    // 本文のスクリプトは loadScript で読むので、ここで入れると二重に動く。取り除いてから入れる
    Array.prototype.forEach.call(newMain.querySelectorAll("script"), function (s) { s.parentNode.removeChild(s); });
    oldMain.parentNode.replaceChild(document.importNode(newMain, true), oldMain);

    // 左のメニュー（いま開いているページの印）と、「本文へスキップ」のリンク
    var newPanel = doc.querySelector(".sidebar-panel");
    var oldPanel = document.querySelector(".sidebar-panel");
    if (newPanel && oldPanel) oldPanel.innerHTML = newPanel.innerHTML;
    var newSkip = doc.querySelector(".skip-link");
    var oldSkip = document.querySelector(".skip-link");
    if (newSkip && oldSkip) oldSkip.setAttribute("href", newSkip.getAttribute("href"));

    Array.prototype.forEach.call(document.head.querySelectorAll(HEAD_SEL), function (el) {
      el.parentNode.removeChild(el);
    });
    Array.prototype.forEach.call(doc.head.querySelectorAll(HEAD_SEL), function (el) {
      document.head.appendChild(document.importNode(el, true));
    });
    document.title = doc.title;
  }

  function scrollToHash(hash, y) {
    var id = "";
    try { id = decodeURIComponent((hash || "").slice(1)); } catch (err) { id = ""; }
    var target = id ? document.getElementById(id) : null;
    if (target) target.scrollIntoView();
    else window.scrollTo(0, y || 0);
  }

  function finish(hash, y) {
    var h = document.querySelector("#" + MAIN_ID + " h1");
    if (h) {
      h.setAttribute("tabindex", "-1");
      h.focus({ preventScroll: true });
    }
    scrollToHash(hash, y);
  }

  /** 同じページの中だけの移動（検索の候補で、いま開いているページの見出しを選んだときなど）。 */
  function sameDocument(url, push) {
    if (push) {
      saveScroll();
      history.pushState({ faraNav: 1, y: 0 }, "", url.pathname + url.search + url.hash);
    }
    scrollToHash(url.hash, 0);
  }

  async function navigate(href, opts) {
    var push = !!opts.push;
    var url = new URL(href, location.href);
    var token = ++seq;

    if (!isRoutable(url, location.origin) || hasModuleScripts(document)) {
      location.assign(url.href);
      return;
    }
    if (url.pathname + url.search === rendered) {
      sameDocument(url, push);
      return;
    }

    if (push) saveScroll();
    try {
      var res = await fetch(url.href, { headers: { Accept: "text/html" }, credentials: "same-origin" });
      var type = res.headers.get("content-type") || "";
      if (!res.ok || type.indexOf("text/html") < 0) throw new Error("HTML ではありません: " + res.status);
      var text = await res.text();
      if (token !== seq) return;

      var final = new URL(res.url || url.href);
      var doc = new DOMParser().parseFromString(text, "text/html");
      if (hasModuleScripts(doc) || !doc.getElementById(MAIN_ID)) {
        location.assign(url.href);
        return;
      }

      var srcs = pageScriptSources(doc);
      loading++;
      try {
        for (var i = 0; i < srcs.length; i++) await loadScript(srcs[i]);
      } finally {
        loading--;
      }
      if (token !== seq) return;

      rendered = final.pathname + final.search;
      if (push) history.pushState({ faraNav: 1, y: 0 }, "", rendered + url.hash);
      swap(doc);
      emit();
      finish(url.hash, opts.y);
    } catch (err) {
      // うまく移れなかったら、通常のページ読み込みに任せる
      location.assign(url.href);
    }
  }

  // 同じサイトのリンクのクリックを拾う（修飾キー・新しいタブ・ダウンロード・data-no-spa は、ブラウザに任せる）
  document.addEventListener("click", function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
    var a = e.target && e.target.closest ? e.target.closest("a[href]") : null;
    if (!a || a.hasAttribute("download") || a.hasAttribute("data-no-spa")) return;
    if (a.target && a.target !== "_self") return;
    if (/\bexternal\b/.test(a.getAttribute("rel") || "")) return;
    var url = new URL(a.href);
    if (!isRoutable(url, location.origin) || hasModuleScripts(document)) return;
    if (url.pathname + url.search === rendered) {
      // 同じページ: # への移動はブラウザに任せる。# がなければ、ページの先頭へ（読み直さない）
      if (url.hash) return;
      e.preventDefault();
      window.scrollTo(0, 0);
      return;
    }
    e.preventDefault();
    navigate(url.href, { push: true, y: 0 });
  });

  // 戻る・進む。同じページの中の # だけなら、何もしない（ブラウザに任せる）
  window.addEventListener("popstate", function () {
    if (currentKey(location) === rendered) return;
    var y = history.state && typeof history.state.y === "number" ? history.state.y : 0;
    navigate(location.href, { push: false, y: y });
  });

  // 読んでいる位置を、履歴の state に覚えておく（戻ったときに同じ位置へ戻すため）。連続してスクロールしている間は、止まってから保存する
  window.addEventListener("scroll", function () {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveScroll, 200);
  }, { passive: true });

  // 読み込み済みのスクリプトを覚えておく（同じスクリプトを二度読まないため）
  Array.prototype.forEach.call(document.querySelectorAll("script[src]"), function (s) { loaded[s.src] = true; });
  rendered = currentKey(location);
  if ("scrollRestoration" in history) history.scrollRestoration = "manual"; // 戻ったときの位置は、ここで合わせる
})();
