using Bunit;
using FaraAvatarModdingGuide.Web.Components;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 作成者の画面のパスワード（伏せ字・表示の切り替え・コピー）。既定は伏せ字で、本物の文字は DOM に出ない。部屋が替わると、伏せ字に戻る。
/// </summary>
public class OwnerPassViewTests : WebTestContext
{
    private const string Pass = "Kq7mZ2xR9tV4bN8wL3pY6hJ1cF5sD0gAbCdEfGhI";   // 40 字（クライアントが作る形）
    private const string Room = "roomaaaa1111";

    private IRenderedComponent<OwnerPassView> Show(string room = Room, bool copied = false, List<string>? copies = null) =>
        Render<OwnerPassView>(p => p
            .Add(x => x.RoomId, room)
            .Add(x => x.Pass, Pass)
            .Add(x => x.Copied, copied)
            .Add(x => x.OnCopy, EventCallback.Factory.Create<string>(this, (string s) =>
            {
                copies?.Add(s);
                return Task.CompletedTask;
            })));

    [Fact]
    public void By_default_the_password_is_masked_and_the_real_text_is_not_in_the_markup()
    {
        var cut = Show();
        Assert.DoesNotContain(Pass, cut.Markup);   // 検索・スクリーンリーダー・拡張機能に、本物が出ない
        Assert.Equal(new string('●', Pass.Length), cut.Find("#room-pass").TextContent);
        Assert.Empty(cut.FindAll("input"));       // 入力欄の value にも入れない
        Assert.Equal("false", cut.Find("#room-pass-toggle").GetAttribute("aria-pressed"));
        Assert.Contains("is-masked", cut.Find("#room-pass").ClassName);
    }

    [Fact]
    public void The_toggle_shows_the_password_and_hides_it_again_and_says_its_state()
    {
        var cut = Show();
        cut.Find("#room-pass-toggle").Click();
        Assert.Equal(Pass, cut.Find("#room-pass").TextContent);
        Assert.Equal("true", cut.Find("#room-pass-toggle").GetAttribute("aria-pressed"));
        Assert.Equal("パスワードを表示", cut.Find("#room-pass-toggle").TextContent.Trim());   // ラベルは固定（状態は aria-pressed）

        cut.Find("#room-pass-toggle").Click();
        Assert.DoesNotContain(Pass, cut.Markup);
        Assert.Equal("false", cut.Find("#room-pass-toggle").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Copy_gives_the_real_password_even_while_it_is_masked()
    {
        var copies = new List<string>();
        var cut = Show(copies: copies);
        cut.Find("#room-copy").Click();
        Assert.Equal([Pass], copies);
        Assert.DoesNotContain(Pass, cut.Markup);   // コピーしても、伏せ字のままにする
    }

    [Fact]
    public void After_a_copy_the_label_says_copied_until_the_parent_resets_it()
    {
        Assert.Contains("コピーしました", Show(copied: true).Find("#room-copy").TextContent);
        Assert.Contains("コピー", Show(copied: false).Find("#room-copy").TextContent);
    }

    [Fact]
    public void Changing_the_room_hides_the_password_again()
    {
        var cut = Show();
        cut.Find("#room-pass-toggle").Click();
        Assert.Contains(Pass, cut.Markup);

        cut.Render(p => p.Add(x => x.RoomId, "roombbbb2222").Add(x => x.Pass, Pass));   // ほかの部屋へ移った（同じ画面の使い回し。パスワードは、その部屋のもの）
        Assert.DoesNotContain(Pass, cut.Markup);
        Assert.Equal(new string('●', Pass.Length), cut.Find("#room-pass").TextContent);
        Assert.Equal("false", cut.Find("#room-pass-toggle").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void The_password_length_is_shown_as_dots_so_the_masked_box_keeps_its_width()
    {
        var cut = Show();
        Assert.Equal(40, cut.Find("#room-pass").TextContent.Length);
    }
}
