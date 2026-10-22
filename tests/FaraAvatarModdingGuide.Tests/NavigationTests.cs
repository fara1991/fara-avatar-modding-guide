using Xunit;

namespace FaraAvatarModdingGuide.Tests;

/// <summary>
/// ページ移動（wwwroot/js/nav.js）。静的に書き出した HTML を、JS でページ単位に差し替える仕組みの、つながりを確かめる。
/// 判定の関数は node のテスト（tests/js/nav.test.mjs）で確かめる。
/// </summary>
public class NavigationTests
{
    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void App_loads_nav_first_so_page_scripts_can_register_with_it()
    {
        var app = File.ReadAllText(RepoFile("Components/App.razor"));
        var nav = app.IndexOf("<script src=\"js/nav.js\" defer></script>", StringComparison.Ordinal);
        Assert.True(nav >= 0, "App.razor に nav.js の読み込みがない");

        // head の defer のスクリプトは、書いた順に動く。nav.js が、ほかのどのスクリプトより先に来ること
        var others = new[] { "sidebar.js", "scrollprogress.js", "bgscene.js", "progress.js", "searchcore.js", "quicksearch.js" };
        foreach (var other in others)
        {
            var at = app.IndexOf($"<script src=\"js/{other}\"", StringComparison.Ordinal);
            Assert.True(at >= 0, $"{other} の読み込みがない");
            Assert.True(at > nav, $"{other} は nav.js より前に読み込まれている");
        }
    }

    [Fact]
    public void Navigation_does_not_use_blazor_web_js()
    {
        // 静的に書き出した dist に Blazor のランタイムは含めない（サーバーの応答を前提にしないため）
        var app = File.ReadAllText(RepoFile("Components/App.razor"));
        Assert.DoesNotContain("blazor.web.js", app);
        var program = File.ReadAllText(RepoFile("Program.cs"));
        Assert.DoesNotContain("AddInteractiveServerComponents();", program);
    }

    [Fact]
    public void Layout_keeps_the_header_and_sidebar_outside_the_swapped_main_area()
    {
        // ページ移動では #main-content と .sidebar-panel の中身だけを差し替える。ヘッダー・背景・進捗の線は、その外に置く
        var layout = File.ReadAllText(RepoFile("Components/Layout/MainLayout.razor"));
        Assert.Contains("<main id=\"main-content\" tabindex=\"-1\">", layout);
        Assert.Contains("<div class=\"sidebar-panel\">", layout);
        Assert.Contains("class=\"skip-link\"", layout);
    }
}
