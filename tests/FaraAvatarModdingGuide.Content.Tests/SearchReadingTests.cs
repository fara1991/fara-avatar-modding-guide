using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>
/// サイト内検索の読み（題名のひらがな。ローマ字・かなで探すため）。題名に漢字があるのに読みが無い、読みに漢字や英字が混じる、
/// を CI で検出する。読みの判定そのもの（LooseKana・ローマ字）は tests/js/search.test.mjs で確かめる。
/// </summary>
public class SearchReadingTests
{
    private static readonly Regex HasKanjiOrLatin = new(@"[\p{IsCJKUnifiedIdeographs}A-Za-z]");
    private static readonly Regex HasKanji = new(@"[\p{IsCJKUnifiedIdeographs}]");

    private static ContentService Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Content", "learn", "categories.yml"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return new ContentService(Path.Combine(dir!.FullName, "Content"), NullLogger<ContentService>.Instance);
    }

    [Fact]
    public void Every_title_with_kanji_has_a_reading()
    {
        var content = Load();
        var titles = new List<(string What, string Title, string Reading)>();
        titles.AddRange(content.Categories.Select(c => ("カテゴリ " + c.Id, c.Title, c.Reading)));
        titles.AddRange(content.Categories.SelectMany(c => content.GetArticles(c.Id)).Select(a => ("記事 " + a.Id, a.Title, a.Reading)));
        titles.AddRange(content.Howtos.Select(h => ("How-to " + h.Id, h.Question, h.Reading)));
        titles.AddRange(content.Tips.Select(t => ("TIPS " + t.Id, t.Title, t.Reading)));
        titles.AddRange(content.Qas.Select(q => ("Q&A " + q.Id, q.Question, q.Reading)));

        var missing = titles.Where(t => HasKanji.IsMatch(t.Title) && string.IsNullOrWhiteSpace(t.Reading)).Select(t => t.What).ToList();
        Assert.True(missing.Count == 0, "読み（reading:）が無い: " + string.Join(", ", missing));
    }

    [Fact]
    public void Readings_are_kana_only()
    {
        var content = Load();
        var readings = new List<(string What, string Reading)>();
        readings.AddRange(content.Categories.Select(c => ("カテゴリ " + c.Id, c.Reading)));
        readings.AddRange(content.Categories.SelectMany(c => content.GetArticles(c.Id)).Select(a => ("記事 " + a.Id, a.Reading)));
        readings.AddRange(content.Howtos.Select(h => ("How-to " + h.Id, h.Reading)));
        readings.AddRange(content.Tips.Select(t => ("TIPS " + t.Id, t.Reading)));
        readings.AddRange(content.Qas.Select(q => ("Q&A " + q.Id, q.Reading)));
        var bad = readings.Where(r => HasKanjiOrLatin.IsMatch(r.Reading ?? "")).Select(r => r.What + " = " + r.Reading).ToList();
        Assert.True(bad.Count == 0, "読みに漢字・英字が混じっている（カタカナで書く）: " + string.Join(", ", bad));
    }

    [Fact]
    public void Search_index_includes_pages_and_categories_with_readings()
    {
        var index = Load().BuildSearchIndex();
        var glossary = index.Single(e => e.Type == "page" && e.Url == "/glossary");
        Assert.Equal("言葉の意味（用語集）", glossary.Title);
        Assert.Equal("ようごしゅうことばのいみ", glossary.Reading);

        var modeling = index.Single(e => e.Type == "category" && e.Url == "/learn/modeling");
        Assert.Equal("もでりんぐ", modeling.Reading);
        Assert.Equal("Modeling", modeling.Subtitle);

        // 読みのある項目は、読みにまで漢字が残っていない（用語集の読みは元からひらがな）
        Assert.All(index.Where(e => !string.IsNullOrEmpty(e.Reading)), e => Assert.DoesNotMatch(HasKanji, e.Reading!));
    }

    /// <summary>
    /// 索引の全項目の検査。題名に漢字がある項目は、読みが必須（記事・用語・TIPS・Q&amp;A・How-to・リンク・カテゴリ・固定ページ）。
    /// 対象外: ショートカット（題名はキーの表記）・アドオン（題名は英語の名前。読みの欄を持たない）。対象外の題名に漢字が入ったら、読みを足すか対象に含めるかを決めるまで落とす。
    /// </summary>
    [Fact]
    public void Every_search_entry_with_kanji_title_has_a_kana_reading()
    {
        var index = Load().BuildSearchIndex();
        var readingTypes = new[] { "article", "glossary", "howto", "tip", "qa", "link", "category", "page" };
        var excludedTypes = new[] { "shortcut", "addon" };

        var missing = index.Where(e => readingTypes.Contains(e.Type) && HasKanji.IsMatch(e.Title) && string.IsNullOrWhiteSpace(e.Reading))
            .Select(e => e.Type + ": " + e.Title).ToList();
        Assert.True(missing.Count == 0, "読みが無い（索引の項目）: " + string.Join(", ", missing));

        var kanjiExcluded = index.Where(e => excludedTypes.Contains(e.Type) && HasKanji.IsMatch(e.Title)).Select(e => e.Type + ": " + e.Title).ToList();
        Assert.True(kanjiExcluded.Count == 0, "対象外の種類の題名に漢字がある（読みを持たせるか、対象に含めること）: " + string.Join(", ", kanjiExcluded));

        var notKana = index.Where(e => !string.IsNullOrEmpty(e.Reading) && HasKanjiOrLatin.IsMatch(e.Reading!)).Select(e => e.Type + ": " + e.Reading).ToList();
        Assert.True(notKana.Count == 0, "読みに漢字・英字がある: " + string.Join(", ", notKana));
    }

    [Fact]
    public void Search_pages_have_hiragana_readings()
    {
        var pages = Load().BuildSearchIndex().Where(e => e.Type == "page").ToList();
        Assert.True(pages.Count >= 10, "固定ページが索引に入っていない");
        Assert.All(pages, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Reading), p.Title);
            Assert.Matches(@"^[\p{IsHiragana}ー]+$", p.Reading!);
        });
    }

    [Fact]
    public void Link_names_with_kanji_have_readings()
    {
        var links = Load().BuildSearchIndex().Where(e => e.Type == "link").ToList();
        Assert.True(links.Count > 50);
        Assert.All(links.Where(l => HasKanji.IsMatch(l.Title)), l => Assert.False(string.IsNullOrWhiteSpace(l.Reading), l.Title));
    }
}
