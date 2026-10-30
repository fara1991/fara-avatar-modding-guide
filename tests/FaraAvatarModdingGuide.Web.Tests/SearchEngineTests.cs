using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Web.Search;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>実際の索引での検索の回帰（tests/js/search.test.mjs の「実際の索引」の部分を C# に移したもの）と、ローマ字・長音の変換。</summary>
public class SearchEngineTests
{
    private static SearchEngine Engine() => new(SearchParityTests.Records, SearchIndexJson.Normalize);

    private static List<string> Titles(string query, int n) => Engine().Search(query).Take(n).Select(h => h.Entry.Title).ToList();

    [Fact]
    public void Yougo_finds_the_glossary_page_first_and_moderingu_finds_modeling()
    {
        Assert.Equal("言葉の意味（用語集）", Titles("yougo", 1)[0]);
        Assert.Equal("モデリング", Titles("moderingu", 1)[0]);
    }

    [Fact]
    public void Two_letter_converted_kana_does_not_pull_in_unrelated_items()
    {
        Assert.DoesNotContain("溶解", Titles("you", 3));                                  // you → よう（2 文字）は読みの一致だけ
        Assert.DoesNotContain(Titles("name", 3), t => t.StartsWith("なめらか", StringComparison.Ordinal));
    }

    [Fact]
    public void Useful_matches_still_appear()
    {
        Assert.Contains("モデリング", Titles("mode", 3));
        Assert.Contains(Titles("kami", 5), t => t.StartsWith('髪'));
    }

    [Theory]
    [InlineData("yougo", "ようご")]
    [InlineData("kennri", "けんり")]
    [InlineData("konna", "こんな")]
    [InlineData("kann", "かん")]
    [InlineData("matte", "まって")]
    [InlineData("ra-men", "らーめん")]
    [InlineData("sha", "しゃ")]
    [InlineData("fa", "ふぁ")]
    [InlineData("YOUGO", "ようご")]
    public void Romaji_is_converted_to_hiragana(string romaji, string expected) => Assert.Equal(expected, RomajiKana.ToHiragana(romaji));

    [Theory]
    [InlineData("xq")]
    [InlineData("q")]
    [InlineData("a1")]
    [InlineData("あいう")]
    [InlineData("")]
    public void Text_that_is_not_romaji_is_not_converted(string text) => Assert.Null(RomajiKana.ToHiragana(text));

    [Theory]
    [InlineData("ひょうき", "ひょき")]
    [InlineData("ひょき", "ひょき")]
    [InlineData("せいじん", "せいじん")]     // えい は別の語なので、そろえない
    [InlineData("らーめん", "らめん")]
    public void Long_vowel_variants_are_treated_alike(string input, string expected) => Assert.Equal(expected, RomajiKana.Loose(input));

    [Fact]
    public void Highlight_is_html_safe_and_full_width_matches_half_width()
    {
        var engine = Engine();
        Assert.Equal("a&lt;b&gt; <mark>Ｘ</mark>&amp;", SearchEngine.ToHtml(engine.Highlight("a<b> Ｘ&", "x")));
        Assert.Equal("", SearchEngine.ToHtml(engine.Highlight("", "x")));
    }
}
