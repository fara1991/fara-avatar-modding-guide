using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// サイト内検索の読み（題名のひらがな。ローマ字・かなで探すため）。題名に漢字があるのに読みが無い、読みに漢字や英字が混じる、
/// を CI で検出する。読みの判定そのもの（LooseKana・ローマ字）は tests/js/search.test.mjs で確かめる。
/// </summary>
public class SearchReadingTests
{
    private sealed class Env : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FaraAvatarModdingGuide";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = "";
    }

    private static readonly Regex HasKanjiOrLatin = new(@"[\p{IsCJKUnifiedIdeographs}A-Za-z]");
    private static readonly Regex HasKanji = new(@"[\p{IsCJKUnifiedIdeographs}]");

    private static ContentService Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Content", "learn", "categories.yml"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return new ContentService(new Env { ContentRootPath = dir!.FullName }, NullLogger<ContentService>.Instance);
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
        Assert.Equal("用語集", glossary.Title);
        Assert.Equal("ようごしゅう", glossary.Reading);

        var modeling = index.Single(e => e.Type == "category" && e.Url == "/learn/modeling");
        Assert.Equal("もでりんぐ", modeling.Reading);
        Assert.Equal("Modeling", modeling.Subtitle);

        // 読みのある項目は、読みにまで漢字が残っていない（用語集の読みは元からひらがな）
        Assert.All(index.Where(e => !string.IsNullOrEmpty(e.Reading)), e => Assert.DoesNotMatch(HasKanji, e.Reading!));
    }
}
