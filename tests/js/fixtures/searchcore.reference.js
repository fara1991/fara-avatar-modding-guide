// 検索の「正解」として固定した写し。Phase 5 で SSR 版(wwwroot/js/searchcore.js)を消す前に、そのまま写した(以下は本体。この 4 行だけが追加)。
// C# の検索(src/FaraAvatarModdingGuide.Web/Search)と、ビルド時の索引の正規化(Build)・ブラウザの正規化(search-norm.js)が、この実装と同じ結果を返すことを確かめるための基準で、
// 検索の仕様を変えるとき以外は書き換えない(書き換えるなら、突き合わせの期待値 tests/js/search-queries.json も作り直す: node tests/js/search-reference.mjs)。
// 元のファイルは Git の履歴にある(PR #98 までの wwwroot/js/searchcore.js)。
// サイト内検索の共通部分（/search-index.json の読み込み・一致の判定・強調表示）。
// ヘッダーの検索（quicksearch.js）と検索ページ（search.js）の両方で使う。
// 日本語は単語区切りがないため形態素解析はせず、スペース区切りの各語が「すべて含まれる」ものを一致とする。
// 全角・半角の違い（Ｇ と G、ｶﾅ とカナ）は NFKC で吸収し、カタカナはひらがなにそろえる（あどおん・アドオンのどちらでも探せる）。
// ローマ字（adoon）も、ひらがな（あどおん）にした形で探す。
(function () {
  "use strict";
  var indexPromise = null;

  /** 検索インデックス。最初に呼ばれたときに一度だけ取得する */
  function load() {
    if (!indexPromise) {
      indexPromise = fetch("/search-index.json").then(function (r) {
        if (!r.ok) throw new Error("search index " + r.status);
        return r.json();
      }).then(function (list) {
        // 正規化した文字列を先に作っておく
        list.forEach(prepare);
        return list;
      }).catch(function (err) { indexPromise = null; throw err; });
    }
    return indexPromise;
  }

  /** 検索用の正規化した文字列を、項目に付ける（_t 題名・_s 小見出し・_x 本文・_r 題名の読み） */
  function prepare(e) {
    e._t = norm(e.title);
    e._s = norm(e.subtitle || "");
    e._x = norm(e.text || "");
    e._r = norm(e.reading || "");
    return e;
  }

  // 全角・半角、大文字・小文字をそろえる。全角の ～（Windows の日本語入力）は NFKC で ~ になるので、データの 〜 にそろえる
  function norm(s) {
    return String(s).normalize("NFKC").toLowerCase().replace(/~/g, "〜").replace(/[ァ-ヶ]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x60); });
  }

  // ---- ローマ字 → ひらがな（検索語用。変換できない文字が残るときは null） ----
  var ROMAJI = (function () {
    var t = {};
    function add(keys, kana) { keys.split(" ").forEach(function (k, i) { t[k] = kana.split(" ")[i]; }); }
    add("a i u e o", "あ い う え お");
    add("ka ki ku ke ko", "か き く け こ");
    add("sa si shi su se so", "さ し し す せ そ");
    add("ta ti chi tu tsu te to", "た ち ち つ つ て と");
    add("na ni nu ne no", "な に ぬ ね の");
    add("ha hi hu fu he ho", "は ひ ふ ふ へ ほ");
    add("ma mi mu me mo", "ま み む め も");
    add("ya yu yo", "や ゆ よ");
    add("ra ri ru re ro", "ら り る れ ろ");
    add("wa wo", "わ を");
    add("ga gi gu ge go", "が ぎ ぐ げ ご");
    add("za zi ji zu ze zo", "ざ じ じ ず ぜ ぞ");
    add("da di du de do", "だ ぢ づ で ど");
    add("ba bi bu be bo", "ば び ぶ べ ぼ");
    add("pa pi pu pe po", "ぱ ぴ ぷ ぺ ぽ");
    add("kya kyu kyo", "きゃ きゅ きょ");
    add("sha shu sho sya syu syo", "しゃ しゅ しょ しゃ しゅ しょ");
    add("cha chu cho tya tyu tyo cya cyu cyo", "ちゃ ちゅ ちょ ちゃ ちゅ ちょ ちゃ ちゅ ちょ");
    add("nya nyu nyo hya hyu hyo mya myu myo rya ryu ryo", "にゃ にゅ にょ ひゃ ひゅ ひょ みゃ みゅ みょ りゃ りゅ りょ");
    add("gya gyu gyo bya byu byo pya pyu pyo dya dyu dyo", "ぎゃ ぎゅ ぎょ びゃ びゅ びょ ぴゃ ぴゅ ぴょ ぢゃ ぢゅ ぢょ");
    add("ja ju jo jya jyu jyo zya zyu zyo", "じゃ じゅ じょ じゃ じゅ じょ じゃ じゅ じょ");
    add("fa fi fe fo va vi vu ve vo", "ふぁ ふぃ ふぇ ふぉ ゔぁ ゔぃ ゔ ゔぇ ゔぉ");
    add("wi we thi dhi", "うぃ うぇ てぃ でぃ");
    add("xa xi xu xe xo la li lu le lo", "ぁ ぃ ぅ ぇ ぉ ぁ ぃ ぅ ぇ ぉ");
    add("xya xyu xyo lya lyu lyo xtu xtsu ltu ltsu", "ゃ ゅ ょ ゃ ゅ ょ っ っ っ っ");
    return t;
  })();

  function romajiToHiragana(src) {
    var s = String(src).toLowerCase();
    if (!/^[a-z'-]+$/.test(s)) return null;
    var out = "", i = 0, n = s.length;
    while (i < n) {
      var c = s[i];
      if (c === "-") { out += "ー"; i++; continue; }
      if (c === "'") { i++; continue; }
      // 促音: 同じ子音が続く（n と母音は除く）
      if (i + 1 < n && s[i + 1] === c && "aiueon".indexOf(c) < 0) { out += "っ"; i++; continue; }
      var hit = false;
      for (var len = 4; len >= 1; len--) {
        var k = s.substr(i, len);
        if (k.length === len && Object.prototype.hasOwnProperty.call(ROMAJI, k)) { out += ROMAJI[k]; i += len; hit = true; break; }
      }
      if (hit) continue;
      if (c === "n" && s[i + 1] === "n" && "aiueoy".indexOf(s[i + 2] || "x") < 0) { out += "ん"; i += 2; continue; }   // nn は「ん」1 つ（kennri → けんり）
      if (c === "n") { out += "ん"; i++; if (s[i] === "'") i++; continue; }   // 母音・y の前以外の n
      return null;
    }
    return out;
  }

  /** 検索語の別の形。ローマ字に見える語（3 文字以上）は、ひらがなにした形も探す */
  function alternatives(term) {
    var alts = [{ t: term, roman: false }];
    if (term.length >= 3) {
      var k = romajiToHiragana(term);
      // ローマ字から変換した形（ひらがな）。2 文字のかなは、本文・題名の部分一致には使わず、読みの一致だけに使う（you → 溶解 のような誤ヒットを防ぐ）
      if (k && k !== term && k.length >= 2) alts.push({ t: k, roman: true });
    }
    return alts;
  }

  /** 1 文字ずつ正規化した文字列と、正規化後の位置から元の位置への対応（… が ... のように文字数が変わっても、位置がずれないように） */
  function foldMap(text) {
    var normed = "", map = [];
    for (var i = 0; i < text.length; i++) {
      var n = norm(text[i]);
      for (var k = 0; k < n.length; k++) { normed += n[k]; map.push(i); }
    }
    return { normed: normed, map: map };
  }

  /** 検索語ごとの、探す形の一覧（[[入力どおり, ひらがな], ...]）。語ごとに、どれか 1 つが含まれればよい */
  function termGroups(query) {
    return norm(query).split(/\s+/).filter(Boolean).map(alternatives);
  }

  /** 強調・抜き出し用に、すべての形を 1 列にしたもの */
  function terms(query) {
    var all = [];
    termGroups(query).forEach(function (g) {
      g.forEach(function (a) {
        if (a.roman && a.t.length <= 2) return; // 2 文字の変換形は、文字として一致しないので強調しない
        if (all.indexOf(a.t) < 0) all.push(a.t);
      });
    });
    return all;
  }

  // 長音の書き方の揺れ（ひょうき・ひょき、ー）を同じに扱う。お段＋う・お（ほうき→ほき）だけ。えい（せいじん）は別の語になるので、そろえない
  var LONG_O = "おこそとのほもよろごぞどぼぽょぉ";
  function looseKana(s) {
    var t = String(s).replace(/ー/g, ""), out = "";
    for (var i = 0; i < t.length; i++) {
      var c = t[i], prev = i > 0 ? t[i - 1] : "";
      if ((c === "う" || c === "お") && prev && LONG_O.indexOf(prev) >= 0) continue;
      out += c;
    }
    return out;
  }

  /** 読み（ひらがな）との一致の点数。1 文字は見ない。2 文字のかなは完全一致と前方一致だけ（「ぎゅ」で「ほんぎゅう」に当てない） */
  function readingScore(e, t) {
    if (!e._r || t.length < 2) return 0;
    var key = looseKana(t), rk = looseKana(e._r);
    if (rk === key) return 30;
    // 2 文字のかなは前方一致だけ（5 点。部分一致はしない）
    if (key.length <= 2) return rk.indexOf(key) === 0 ? 5 : 0;
    if (rk.indexOf(key) === 0) return 15;
    return rk.indexOf(key) >= 0 ? 10 : 0;
  }

  /** 検索語の 1 つの形の点数。ローマ字から変換した 2 文字のかなは、読みの完全・前方一致だけ */
  function groupScore(e, a) {
    if (a.roman && a.t.length <= 2) return readingScore(e, a.t);
    return scoreOf(e, a.t);
  }

  /** 1 つの語の点数（含まれなければ 0）。読みは題名の一致の後、小見出し・本文より前 */
  function scoreOf(e, t) {
    if (e._t === t) return 30;
    if (e._t.indexOf(t) === 0) return 15;
    if (e._t.indexOf(t) >= 0) return 10;
    var r = readingScore(e, t);
    if (r) return r;
    if (e._s.indexOf(t) >= 0) return 5;
    if (e._x.indexOf(t) >= 0) return 1;
    return 0;
  }

  /** すべての語を含むものを、タイトル > 小見出し > 本文の順に点数を付けて並べる */
  function search(index, query) {
    var gs = termGroups(query);
    if (!gs.length) return [];
    var hits = [];
    index.forEach(function (e) {
      var score = 0;
      for (var i = 0; i < gs.length; i++) {
        var best = 0;
        for (var j = 0; j < gs[i].length; j++) best = Math.max(best, groupScore(e, gs[i][j]));
        if (!best) { score = 0; break; }
        score += best;
      }
      if (score > 0) hits.push({ entry: e, score: score + (TYPE_BONUS[e.type] || 0) });
    });
    hits.sort(function (a, b) { return b.score - a.score; });
    return hits;
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; });
  }

  function escapeRegExp(s) {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }

  /**
   * 一致した部分を <mark> で囲む。エスケープ前の文字列で一度に探すので、後の語が前に挿入した <mark> や &amp; などの中に一致することはない。
   * 全角・半角が違っても一致するように、1 文字ずつ正規化した文字列で位置を探す
   */
  function highlight(text, query) {
    text = String(text);
    var ts = terms(query);
    if (!ts.length) return escapeHtml(text);
    var folded = foldMap(text), normed = folded.normed, map = folded.map;
    var pattern = new RegExp(ts.slice().sort(function (a, b) { return b.length - a.length; }).map(escapeRegExp).join("|"), "g");
    var html = "", last = 0, m;
    while ((m = pattern.exec(normed)) !== null) {
      if (!m[0].length) { pattern.lastIndex++; continue; }
      var s = map[m.index], e = map[m.index + m[0].length - 1] + 1;
      if (s < last) continue;
      html += escapeHtml(text.slice(last, s)) + "<mark>" + escapeHtml(text.slice(s, e)) + "</mark>";
      last = e;
    }
    return html + escapeHtml(text.slice(last));
  }

  /** 本文から、最初に一致した語のまわりを切り出す */
  function snippet(text, query, width) {
    text = String(text || "");
    width = width || 120;
    var folded = foldMap(text), ts = terms(query), pos = -1;
    for (var i = 0; i < ts.length && pos < 0; i++) pos = folded.normed.indexOf(ts[i]);
    if (pos < 0) return text.slice(0, width) + (text.length > width ? "…" : "");
    pos = folded.map[pos];   // 元の文字列の位置に戻す
    var start = Math.max(0, pos - Math.floor(width / 3));
    var end = Math.min(text.length, start + width);
    return (start > 0 ? "…" : "") + text.slice(start, end) + (end < text.length ? "…" : "");
  }

  // 種類の表示（用語集・何て名前？・TIPS・Q&A は Content/tips/、リンク集は Content/links.yml のデータ）
  var TYPES = {
    article: { label: "記事", cls: "bg-primary" },
    glossary: { label: "用語", cls: "bg-warning text-dark" },
    howto: { label: "何て名前？", cls: "bg-danger" },
    tip: { label: "TIPS", cls: "bg-info text-dark" },
    qa: { label: "Q&A", cls: "bg-light text-dark" },
    category: { label: "カテゴリ", cls: "bg-secondary" },
    page: { label: "ページ", cls: "bg-secondary" },
    shortcut: { label: "ショートカット", cls: "bg-success" },
    addon: { label: "アドオン", cls: "bg-secondary" },
    link: { label: "リンク", cls: "bg-primary-subtle text-primary-emphasis" },
  };
  // 同じくらい一致したときは、記事や用語を先に出す
  var TYPE_BONUS = { article: 0.6, category: 0.5, glossary: 0.5, howto: 0.5, tip: 0.4, qa: 0.4, shortcut: 0.2, addon: 0, link: 0 };

  window.faraSearch = { load: load, search: search, highlight: highlight, snippet: snippet, escapeHtml: escapeHtml, prepare: prepare, TYPES: TYPES, romajiToHiragana: romajiToHiragana, looseKana: looseKana };
})();
