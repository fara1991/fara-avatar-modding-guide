using System.Net;
using System.Text.Json;
using Bunit;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Layout;
using FaraAvatarModdingGuide.Web.Pages.Tips;
using FaraAvatarModdingGuide.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>filter.json が読めなかったとき(絞り込みの文字入力で全件が消えない)・#id で隠れているときだけ絞り込みを外す・知らない URL でサイドバーが強調されない。</summary>
public class FilterFallbackTests : WebTestContext
{
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    [Fact]
    public void When_filter_json_cannot_be_read_typing_does_not_hide_every_item()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new FilterTextStore(http));   // 後から登録したものが使われる
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/tips");
        var cut = Render<Tips>();
        cut.Find(".tips-filter input").Input("ピンク");
        var total = TestData.Content.Tips.Count;
        Assert.Equal(total, cut.FindAll("[data-tip-item]").Count(e => !e.HasAttribute("hidden")));
        // タグでの絞り込みは、文字の読み込みと関係なく効く
        cut.FindAll(".tips-filter button").First(b => b.TextContent.Trim() == "Unity").Click();
        var shown = cut.FindAll("[data-tip-item]").Count(e => !e.HasAttribute("hidden"));
        Assert.InRange(shown, 1, total - 1);
    }

    [Fact]
    public void Text_filter_still_narrows_when_filter_json_is_available()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/tips");
        var cut = Render<Tips>();
        cut.Find(".tips-filter input").Input("ピンク");
        Assert.InRange(cut.FindAll("[data-tip-item]").Count(e => !e.HasAttribute("hidden")), 1, TestData.Content.Tips.Count - 1);
    }

    [Fact]
    public void Hash_to_a_visible_item_keeps_the_filter_and_hash_to_a_hidden_item_clears_it()
    {
        var unity = TestData.Content.Tips.First(t => t.Tags.Contains("Unity"));
        var notUnity = TestData.Content.Tips.First(t => !t.Tags.Contains("Unity"));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/tips?tag=Unity#{unity.Id}");
        var visibleCase = Render<Tips>();
        Assert.Equal("Unity", visibleCase.Find(".tips-filter button.btn-primary").TextContent.Trim());   // 見えている項目なので、絞り込みはそのまま
        Services.GetRequiredService<NavigationManager>().NavigateTo($"http://localhost/tips?tag=Unity#{notUnity.Id}");
        var hiddenCase = Render<Tips>();
        hiddenCase.WaitForAssertion(() => Assert.Equal("すべて", hiddenCase.Find(".tips-filter button.btn-primary").TextContent.Trim()));     // 隠れていたので外す(項目の文字を読んだあと)
    }
}

public class UnknownUrlSidebarTests : WebTestContext
{
    [Theory]
    [InlineData("/learn/basics/no-such-article")]
    [InlineData("/learn/no-such-category")]
    public void Nothing_is_active_for_unknown_content_urls(string path)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + path);
        var cut = Render<AvNavMenu>(p => p.Add(x => x.Groups, NavGroups.Build(TestData.Content)));
        Assert.Empty(cut.FindAll("a.mud-nav-link.active"));
    }
}

/// <summary>content.json が JSON として読めても形が違うとき(空の {} など)は、読み込み失敗として扱う(null 参照で空白にならない)。</summary>
public class ContentShapeTests
{
    private static async Task<ContentStore> LoadAsync(string json)
    {
        using var handler = new StubHandler(json);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var store = new ContentStore(http);
        await store.LoadAsync();
        return store;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"sections\":[],\"categories\":[]}")]
    public async Task Wrong_shaped_json_is_a_load_failure(string json)
    {
        var store = await LoadAsync(json);
        Assert.Null(store.Content);
        Assert.NotNull(store.Error);
    }

    [Fact]
    public async Task A_collection_item_with_a_missing_inner_list_is_a_load_failure()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(TestData.Content, SiteJsonContext.Default.SiteContent))!;
        node["articles"]![0]!.AsObject().Remove("shortcuts");
        var store = await LoadAsync(node.ToJsonString());
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task The_real_content_is_valid()
    {
        var store = await LoadAsync(JsonSerializer.Serialize(TestData.Content, SiteJsonContext.Default.SiteContent));
        Assert.NotNull(store.Content);
        Assert.Null(SiteContentValidation.Check(TestData.Settings));
    }

    [Fact]
    public void Wrong_shaped_site_settings_are_rejected()
    {
        Assert.NotNull(SiteContentValidation.Check(JsonSerializer.Deserialize("{}", SiteJsonContext.Default.SiteSettings)));
    }
}
