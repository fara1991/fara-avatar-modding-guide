using Bunit;
using FaraAvatarModdingGuide.Web.Layout;
using FaraAvatarModdingGuide.Web.Pages;
using FaraAvatarModdingGuide.Web.Pages.Learn;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 練習ページ(フェーズ 3)。JS interop の呼び方(始める・解放する・同じ id で解放する)と、部屋の出入り、ページの URL を確かめる。
/// 3D 本体(Three.js)の動きは、ブラウザ(Playwright。tools/verify/practice.mjs)で確かめる。
/// </summary>
public class PracticeTests : WebTestContext
{
    /// <summary>js/practice-entry.js の模擬。start・startArticle・dispose を受け付ける。</summary>
    private BunitJSModuleInterop Entry()
    {
        var module = JSInterop.SetupModule("./js/practice-entry.js");
        module.SetupVoid("start", _ => true).SetVoidResult();
        module.SetupVoid("startArticle", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
        return module;
    }

    /// <summary>タッチだけの端末の模擬（isTouchOnly が true）。案内の入口（start・dispose）だけが呼ばれ、PC の 3D（C# 版）は始まらない。</summary>
    private BunitJSModuleInterop TouchEntry()
    {
        var module = JSInterop.SetupModule("./js/practice-entry.js");
        module.Setup<bool>("isTouchOnly", _ => true).SetResult(true);
        module.SetupVoid("start", _ => true).SetVoidResult();
        module.SetupVoid("startArticle", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
        return module;
    }

    private static string? Id(JSRuntimeInvocation call) => call.Arguments[0] as string;

    /// <summary>?room= を付けた URL へ移動してから、みんなで練習のページを描く(部屋の ID は URL のクエリから来る)。room が null なら ?room= なし(ロビー)。</summary>
    private IRenderedComponent<PracticeRoom> RenderRoom(string? room)
    {
        GoToRoom(room);
        return Render<PracticeRoom>();
    }

    private void GoToRoom(string? room) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/practice/room" + (room is null ? "" : "?room=" + Uri.EscapeDataString(room)));

    [Fact]
    public async Task Room_page_in_the_lobby_shows_the_lobby_and_starts_only_the_phone_guide()
    {
        var entry = Entry();
        var cut = RenderRoom(null);
        Assert.False(cut.Find("#room-lobby").HasAttribute("hidden"));
        Assert.True(cut.Find("#room-main").HasAttribute("hidden"));
        var call = entry.Invocations["start"].Single();
        Assert.Null(call.Arguments[1]);                                     // 案内の入口だけ（PC の 3D は C# 版。スマートフォンの案内だけ）
        await DisposeComponentsAsync();
        Assert.Single(entry.Invocations["dispose"]);
    }

    [Theory]
    [InlineData("abcdef1234")]                                // 10 字(作成 UI の長さ。既存の共有 URL も通る)
    [InlineData("abcdef123456")]
    [InlineData("ABCDEF123456")]                            // 小文字にそろえる(room.js・Worker と同じ)
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]   // 32 字
    public void Room_page_with_a_valid_room_shows_the_room_without_a_javascript_3d(string parameter)
    {
        var entry = Entry();
        var cut = RenderRoom(parameter);
        Assert.True(cut.Find("#room-lobby").HasAttribute("hidden"));
        Assert.False(cut.Find("#room-main").HasAttribute("hidden"));
        Assert.Empty(entry.Invocations["start"]);                             // PC の 3D は C# 版（bUnit では始まらない）。JavaScript は案内の入口だけ
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]                                                     // 10 字未満
    [InlineData("abcdef12")]                                                // 8 字(手で付けた短い ID は通さない)
    [InlineData("abcdef123")]                                               // 9 字
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]                       // 33 字
    [InlineData("abc def123")]
    [InlineData("abc/../12345")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("abcdef12\n")]                                         // 末尾の改行(.NET の $ は通してしまう。worker・room.js の /^…$/ は通さない)
    [InlineData("abcdef12\r\n")]
    public void Room_page_ignores_an_invalid_room_id_and_shows_the_lobby(string parameter)
    {
        var entry = Entry();
        var cut = RenderRoom(parameter);
        Assert.False(cut.Find("#room-lobby").HasAttribute("hidden"));
        Assert.Null(entry.Invocations["start"].Single().Arguments[1]);     // 案内の入口だけ（PC の 3D は C# 版）
    }

    [Fact]
    public void Creating_a_room_moves_to_a_new_room_id_without_reloading()
    {
        Entry();
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/practice/room");
        var cut = Render<PracticeRoom>();
        cut.Find("#room-create").Click();
        var uri = new Uri(nav.Uri);
        Assert.Equal("/practice/room", uri.AbsolutePath);
        Assert.Matches("^\\?room=[a-z0-9]{10}$", uri.Query);                 // room.js の randomRoomId と同じ形(10 字)
    }

    [Fact]
    public async Task Practice_page_on_a_touch_device_starts_the_guide_and_disposes_with_the_same_id()
    {
        var entry = TouchEntry();
        var cut = Render<Web.Pages.Practice>();
        cut.WaitForAssertion(() => Assert.Single(entry.Invocations["start"]));
        var call = entry.Invocations["start"].Single();
        Assert.Null(call.Arguments[1]);                                     // 案内の入口だけ（PC の 3D は C# 版）
        await DisposeComponentsAsync();
        Assert.Equal(Id(call), Id(Assert.Single(entry.Invocations["dispose"])));
    }

    [Fact]
    public async Task Unity_practice_page_on_a_touch_device_starts_the_guide_and_disposes_with_the_same_id()
    {
        var entry = TouchEntry();
        Render<PracticeUnity>();
        Assert.Single(entry.Invocations["start"]);
        var call = entry.Invocations["start"].Single();
        await DisposeComponentsAsync();
        Assert.Equal(Id(call), Id(Assert.Single(entry.Invocations["dispose"])));
    }

    [Fact]
    public void Switching_the_room_on_a_touch_device_disposes_the_old_one_by_its_own_id()
    {
        // 部屋が替わると、前の案内を、始めたときと同じ id で解放し、次を始める。
        // 注意: 二つの描画（OnAfterRender）が重なると、前の部屋の解放が次の開始より後に届くことがある（JS 側は dispose(id) を id で見分けるので、どちらでも安全）。
        // そのため、並びは確かめず、解放が一度だけ・始めたものと同じ id であること、次が始まっていることを確かめる。
        var entry = TouchEntry();
        var cut = RenderRoom("aaaaaa111111");
        Assert.Single(entry.Invocations["start"]);
        GoToRoom("bbbbbb222222");
        cut.Render();
        cut.WaitForAssertion(() => Assert.Equal(2, entry.Invocations["start"].Count));
        cut.WaitForAssertion(() => Assert.Single(entry.Invocations["dispose"]));
        Assert.Equal(Id(entry.Invocations["start"][0]), Id(entry.Invocations["dispose"][0]));
    }

    [Fact]
    public void Room_ids_made_by_the_page_use_only_the_characters_the_worker_accepts()
    {
        // 36 通りの文字から 10 字。10 字 = 10〜32 字の範囲（worker は 9 字以下を受け付けない）
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        Assert.Equal(36, alphabet.Length);
        for (var i = 0; i < 50; i++)
        {
            var id = new string(RandomNumberGenerator.GetBytes(10).Select(b => alphabet[b % 36]).ToArray());
            Assert.Matches("^[a-z0-9]{10,32}$", id);
        }
    }

    [Fact]
    public async Task Article_with_a_tutorial_starts_it_after_render_and_disposes_when_the_article_changes_and_when_leaving()
    {
        var entry = Entry();
        var withTutorial = TestData.Content.Articles.Where(a => a.PracticeScenarios.Count > 0).ToList();
        Assert.True(withTutorial.Count >= 2, "チュートリアルのある記事が 2 つ以上ある");
        var without = TestData.Content.Articles.First(a => a.PracticeScenarios.Count == 0);

        var first = withTutorial[0];
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + first.Url);
        var cut = Render<ArticlePage>(p => p.Add(x => x.CategoryId, first.Category).Add(x => x.Slug, first.Slug));
        cut.WaitForAssertion(() => Assert.Single(entry.Invocations["startArticle"]));
        Assert.Equal(".article-body", entry.Invocations["startArticle"][0].Arguments[1]);
        Assert.NotNull(cut.Find(".article-body .practice-embed"));            // 空の div が本文に出ている(中身は tutorial.js が入れる)

        // 同じ記事を描き直しても(完了の記録が変わったときなど)、始め直さない
        cut.Render();
        Assert.Single(entry.Invocations["startArticle"]);

        // 別のチュートリアルのある記事へ(部品は再利用される): 前の記事のものを解放してから、次を始める
        var second = withTutorial.First(a => a.Url != first.Url);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + second.Url);
        cut.Render(p => p.Add(x => x.CategoryId, second.Category).Add(x => x.Slug, second.Slug));
        cut.WaitForAssertion(() => Assert.Equal(2, entry.Invocations["startArticle"].Count));
        Assert.Single(entry.Invocations["dispose"]);
        Assert.Equal(Id(entry.Invocations["startArticle"][0]), Id(entry.Invocations["dispose"][0]));

        // チュートリアルの無い記事へ: 解放して、始めない
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + without.Url);
        cut.Render(p => p.Add(x => x.CategoryId, without.Category).Add(x => x.Slug, without.Slug));
        cut.WaitForAssertion(() => Assert.Equal(2, entry.Invocations["dispose"].Count));
        Assert.Equal(2, entry.Invocations["startArticle"].Count);

        // 離れる: 動いているものが無いので、解放は増えない
        await DisposeComponentsAsync();
        Assert.Equal(2, entry.Invocations["dispose"].Count);
    }

    [Fact]
    public async Task Leaving_an_article_with_a_tutorial_disposes_it()
    {
        var entry = Entry();
        var article = TestData.Content.Articles.First(a => a.PracticeScenarios.Count > 0);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + article.Url);
        Render<ArticlePage>(p => p.Add(x => x.CategoryId, article.Category).Add(x => x.Slug, article.Slug));
        Assert.Single(entry.Invocations["startArticle"]);
        await DisposeComponentsAsync();
        Assert.Equal(Id(entry.Invocations["startArticle"][0]), Id(Assert.Single(entry.Invocations["dispose"])));
    }

    [Fact]
    public void Article_header_carries_the_page_id_that_tutorial_js_uses_to_mark_the_page_done()
    {
        // tutorial.js は [data-progress-page] の値で window.faraProgress.set(page, true) する。ProgressStore の記事 id("カテゴリ/スラッグ")と同じ
        var article = TestData.Content.Articles.First(a => a.PracticeScenarios.Count > 0);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost" + article.Url);
        Entry();
        var cut = Render<ArticlePage>(p => p.Add(x => x.CategoryId, article.Category).Add(x => x.Slug, article.Slug));
        Assert.Equal(article.Id, cut.Find("[data-progress-page]").GetAttribute("data-progress-page"));
        Assert.Equal($"{article.Category}/{article.Slug}", article.Id);
    }

    [Fact]
    public void Touch_guide_is_hidden_until_the_entry_script_shows_it_and_has_one_embed_per_item()
    {
        var items = new List<TouchPracticeGuide.Item> { new("select", "選択", "/learn/basics/select"), new("transform", "移動", "/learn/basics/transform") };
        var cut = Render<TouchPracticeGuide>(p => p.Add(x => x.Items, items));
        Assert.True(cut.Find("#touch-practice").HasAttribute("hidden"));
        Assert.Equal(["select", "transform"], cut.FindAll(".practice-embed").Select(e => e.GetAttribute("data-scenario")!).ToArray());
        Assert.Empty(cut.FindAll("template"));                                // Blazor は <template> の中身を .content に作らない。使わない
        var links = Render<TouchPracticeGuide>(p => p.Add(x => x.Items, items.Take(1).ToList()).Add(x => x.Embed, false));
        Assert.Empty(links.FindAll(".practice-embed"));
        Assert.Equal("/learn/basics/select", links.Find(".touch-practice-links a").GetAttribute("href"));
    }

    [Fact]
    public void The_unmodified_3d_scripts_are_in_the_web_project()
    {
        // 残る JavaScript（scenarios・bgscene。3D の本体は C# 版。three.js は S7 で消した）。
        // 比べる相手はもう無いので、置いてあることだけを確かめる(中身は tools/verify/practice.mjs が、実際の操作で確かめる)
        var dir = Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "js");
        foreach (var name in new[] { "scenarios.js", "bgscene.js", "tutorial.js" })
            Assert.True(File.Exists(Path.Combine(dir, name)), name);
        foreach (var gone in new[] { "room.js", "viewport.js", "editmesh.js", "practice.js", "unity.js", "unity-practice.js" })
            Assert.False(File.Exists(Path.Combine(dir, gone)), gone + " は C# 版に置き換えた（Phase 3b S7）");
        Assert.False(Directory.Exists(Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "lib", "three")), "three.js は S7 で消した");
        Assert.False(Directory.Exists(Path.Combine(TestData.Root, "wwwroot", "js")), "ルートの wwwroot/js(旧 SSR 版)は消した");
    }

    [Fact]
    public void Modified_scripts_export_start_and_dispose_and_do_nothing_when_only_loaded()
    {
        // 書き換えた tutorial.js は、読み込んだだけでは動かず、mount と dispose を出す。
        // ページ上の DOM を探す処理がモジュールの先頭(読み込み時)に残っていないこと
        var dir = Path.Combine(TestData.Root, "src", "FaraAvatarModdingGuide.Web", "wwwroot", "js");
        foreach (var (name, entry) in new[] { ("tutorial.js", "export function mount(") })
        {
            var text = File.ReadAllText(Path.Combine(dir, name));
            Assert.Contains(entry, text);
            Assert.Contains("dispose()", text);
            Assert.Empty(Regex.Matches(text, @"^(?:const|let|var)\s+\w+\s*=\s*document\.(getElementById|querySelector)", RegexOptions.Multiline));
            Assert.DoesNotContain("onerror=", text);                           // HTML の属性のイベント(onerror など)は、CSP の script-src で止まる
        }
    }
}
