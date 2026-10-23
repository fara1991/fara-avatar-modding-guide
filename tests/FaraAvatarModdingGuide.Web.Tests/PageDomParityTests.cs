using AngleSharp.Dom;
using Bunit;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Pages;
using FaraAvatarModdingGuide.Web.Pages.Learn;
using FaraAvatarModdingGuide.Web.Pages.Tips;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 静的 HTML（Build が書く、起動の前の本文）と、WebAssembly の画面（Razor）が、同じ DOM になっていること（計画書 4.10: 起動の前後でちらつかない）。
/// 全ページ（記事 50・カテゴリ 10・固定のページ）で、本文の文字と、要素の並び・class・id・href を比べる。
/// 完了の記録（is-done）・目次の強調（is-current）は、起動の後に付くものなので、比べない。
/// </summary>
public class PageDomParityTests : WebTestContext
{
    /// <summary>本文の文字（空白を除く）と、要素の並び（タグ・class・id・href）。</summary>
    private static (string Text, List<string> Shape) Describe(INodeList nodes)
    {
        var shape = new List<string>();
        var text = new System.Text.StringBuilder();
        void Walk(INode node)
        {
            switch (node)
            {
                case IElement el:
                    if (el.LocalName is "noscript" or "script" or "style") return;
                    var classes = el.ClassList.Where(c => c is not ("is-done" or "is-current")).Order(StringComparer.Ordinal);
                    shape.Add($"{el.LocalName}.{string.Join('.', classes)}#{el.Id}[{el.GetAttribute("href")}]{(el.HasAttribute("hidden") ? " hidden" : "")}");
                    foreach (var child in el.ChildNodes) Walk(child);
                    break;
                case IText t:
                    text.Append(t.Data);
                    break;
            }
        }
        foreach (var n in nodes) Walk(n);
        return (System.Text.RegularExpressions.Regex.Replace(text.ToString(), @"\s+", ""), shape);
    }

    private static INodeList ParseStatic(string html) => new AngleSharp.Html.Parser.HtmlParser().ParseDocument("<body>" + html + "</body>").Body!.ChildNodes;

    private static PagePlan Plan(string path) => PageCatalog.Build(TestData.Content, TestData.Settings).Single(p => p.Path == path);

    private void AssertSame<T>(string path, IRenderedComponent<T> cut) where T : IComponent
    {
        var (staticText, staticShape) = Describe(ParseStatic(Plan(path).Main));
        var (text, shape) = Describe(cut.Nodes);
        Assert.True(staticText == text, $"{path}: 本文の文字が違う（静的 {staticText.Length} 字 / WebAssembly {text.Length} 字）。最初の差: " + FirstDiff(staticText, text));
        for (var i = 0; i < Math.Max(shape.Count, staticShape.Count); i++)
        {
            var a = i < staticShape.Count ? staticShape[i] : "(なし)";
            var b = i < shape.Count ? shape[i] : "(なし)";
            Assert.True(a == b, $"{path}: {i + 1} 番目の要素が違う 静的={a} / WebAssembly={b}（前 3 つ: {string.Join(" | ", staticShape.Skip(Math.Max(0, i - 3)).Take(3))}）");
        }
    }

    private static string FirstDiff(string a, string b)
    {
        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        return $"静的「{a[Math.Max(0, i - 10)..Math.Min(a.Length, i + 30)]}」 / WebAssembly「{b[Math.Max(0, i - 10)..Math.Min(b.Length, i + 30)]}」";
    }

    private IRenderedComponent<T> Page<T>(string path, Action<ComponentParameterCollectionBuilder<T>>? parameters = null) where T : IComponent
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        return Render<T>(parameters ?? (_ => { }));
    }

    [Fact]
    public void Home_and_list_pages_have_the_same_dom_as_the_static_pages()
    {
        AssertSame("/", Page<Home>("/"));
        AssertSame("/learn", Page<LearnIndex>("/learn"));
        AssertSame("/shortcuts", Page<Shortcuts>("/shortcuts"));
        AssertSame("/addons", Page<Addons>("/addons"));
        AssertSame("/links", Page<Links>("/links"));
        AssertSame("/changelog", Page<Changelog>("/changelog"));
        AssertSame("/privacy", Page<Privacy>("/privacy"));
        AssertSame("/search", Page<SearchPage>("/search"));
    }

    [Fact]
    public void Glossary_howto_tips_and_qa_have_the_same_dom_as_the_static_pages()
    {
        AssertSame("/glossary", Page<Glossary>("/glossary"));
        AssertSame("/howto", Page<Howto>("/howto"));
        AssertSame("/tips", Page<Tips>("/tips"));
        AssertSame("/qa", Page<Qa>("/qa"));
    }

    [Fact]
    public void All_ten_categories_have_the_same_dom_as_the_static_pages()
    {
        foreach (var category in TestData.Content.Categories)
            AssertSame($"/learn/{category.Id}", Page<CategoryPage>($"/learn/{category.Id}", p => p.Add(x => x.CategoryId, category.Id)));
    }

    [Fact]
    public void All_fifty_articles_have_the_same_dom_as_the_static_pages()
    {
        Assert.Equal(50, TestData.Content.Articles.Count);
        foreach (var article in TestData.Content.Articles)
            AssertSame(article.Url, Page<ArticlePage>(article.Url, p => p.Add(x => x.CategoryId, article.Category).Add(x => x.Slug, article.Slug)));
    }

    [Fact]
    public void Article_text_in_the_wasm_page_is_exactly_the_article_html_text()
    {
        // 記事の本文（.article-body）のテキストが、Build の HTML のテキストと一致する（WebAssembly の画面が本文を欠かない・足さない）
        foreach (var article in TestData.Content.Articles)
        {
            var cut = Page<ArticlePage>(article.Url, p => p.Add(x => x.CategoryId, article.Category).Add(x => x.Slug, article.Slug));
            Assert.Equal(TestData.TextOf(article.Html), TestData.TextOf(cut.Find(".article-body").InnerHtml));
        }
    }

    [Fact]
    public void Unknown_article_and_category_show_the_not_found_view()
    {
        Assert.Contains("見つかりません", Page<ArticlePage>("/learn/basics/nope", p => p.Add(x => x.CategoryId, "basics").Add(x => x.Slug, "nope")).Markup);
        Assert.Contains("見つかりません", Page<CategoryPage>("/learn/nope", p => p.Add(x => x.CategoryId, "nope")).Markup);
    }
}
