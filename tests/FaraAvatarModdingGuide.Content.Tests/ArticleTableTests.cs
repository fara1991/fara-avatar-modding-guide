using System.Text.RegularExpressions;
using FaraAvatarModdingGuide.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>
/// 記事の表。3 列以上の表は、狭い画面で 1 行ずつのカードにするため、table-cards の class と、各セルの data-label（見出しの文字）を持つ。
/// </summary>
public class ArticleTableTests
{
    private static ContentService Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Content", "learn", "categories.yml"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return new ContentService(Path.Combine(dir!.FullName, "Content"), NullLogger<ContentService>.Instance);
    }

    [Fact]
    public void Three_column_table_gets_card_class_and_cell_labels()
    {
        var html = Load().GetArticles("vrchat").Single(a => a.Slug == "upload-errors").Html;
        Assert.Contains("class=\"table-cards\"", html);
        Assert.Contains("<td data-label=\"よくある原因\">", html);
        Assert.Contains("<td data-label=\"直し方\">", html);
    }

    [Fact]
    public void Every_table_cell_in_a_card_table_has_a_label()
    {
        var content = Load();
        var bad = new List<string>();
        foreach (var article in content.Categories.SelectMany(c => content.GetArticles(c.Id)))
        {
            foreach (Match table in Regex.Matches(article.Html, "<table class=\"table-cards\">.*?</table>", RegexOptions.Singleline))
            {
                var body = table.Value[table.Value.IndexOf("<tbody>", StringComparison.Ordinal)..];
                var plainCells = Regex.Matches(body, "<td>").Count;
                if (plainCells > 0) bad.Add(article.Category + "/" + article.Slug);
            }
        }
        Assert.True(bad.Count == 0, "見出しのラベルが付かないセルがある表: " + string.Join(", ", bad.Distinct()));
    }
}
