using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// 旧 SSR 版(Blazor Web App。ルートの FaraAvatarModdingGuide.csproj)を消した後の、リポジトリの形(Phase 5)。
/// 消したものが、戻ってきていないこと(誤ったマージ・取り込みで、SSR のコードや設定が復活しない)と、残したものが残っていることを確かめる。
/// </summary>
public class RepoLayoutTests
{
    private static string Path_(string relative) => Path.Combine(TestData.Root, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void The_old_ssr_project_is_gone()
    {
        Assert.Empty(Directory.GetFiles(TestData.Root, "*.csproj", SearchOption.TopDirectoryOnly));
        foreach (var gone in new[] { "Components", "Publishing", "Data", "Properties", "Program.cs", "appsettings.Development.json", "wwwroot/js", "wwwroot/css" })
            Assert.False(File.Exists(Path_(gone)) || Directory.Exists(Path_(gone)), $"{gone} は旧 SSR 版のもの。消した");
        Assert.DoesNotContain("FaraAvatarModdingGuide.csproj", File.ReadAllText(Path_("FaraAvatarModdingGuide.sln")));
        Assert.False(File.Exists(Path_("tests/FaraAvatarModdingGuide.Tests/FaraAvatarModdingGuide.Tests.csproj")), "旧 SSR 版のテストプロジェクトは消した");
        Assert.False(Directory.Exists(Path_("tools/compare")), "SSR との本文比較ツールは消した(SSR が無いと比べる相手がない)");
    }

    [Fact]
    public void What_the_build_and_the_tests_still_read_is_kept()
    {
        // Build が画像をここからコピーする(SiteBuilder)。tools/tutorial-images がここへ書く。ルート直下の wwwroot は images と og.png(OGP。Build が出力のルートへコピーする)だけ
        Assert.True(Directory.Exists(Path_("wwwroot/images/tutorials")));
        Assert.Equal("images", Path.GetFileName(Assert.Single(Directory.GetDirectories(Path_("wwwroot")))));
        Assert.Equal("og.png", Path.GetFileName(Assert.Single(Directory.GetFiles(Path_("wwwroot")))));
        // サイトの設定(名前・バージョン・SNS)。Build とテストが読む
        Assert.True(File.Exists(Path_("appsettings.json")));
        Assert.True(File.Exists(Path_("tools/site.json")));
        // 検索の突き合わせの正解(旧 searchcore.js の固定の写し)と、期待値
        Assert.True(File.Exists(Path_("tests/js/fixtures/searchcore.reference.js")));
        Assert.True(File.Exists(Path_("tests/js/search-queries.json")));
        Assert.True(File.Exists(Path_("tools/verify/practice-baseline.json")));
    }

    [Fact]
    public void The_frozen_search_reference_keeps_the_original_body()
    {
        // 固定の写しは、先頭に説明を足した以外は、元の searchcore.js のまま。入口の関数(window.faraSearch)が残っている
        var js = File.ReadAllText(Path_("tests/js/fixtures/searchcore.reference.js"));
        Assert.StartsWith("// 検索の「正解」として固定した写し", js);
        Assert.Contains("window.faraSearch", js);
        Assert.Contains("romajiToHiragana", js);
        Assert.Contains("looseKana", js);
    }
}
