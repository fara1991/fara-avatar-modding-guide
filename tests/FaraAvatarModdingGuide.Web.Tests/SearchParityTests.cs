using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FaraAvatarModdingGuide.Build;
using FaraAvatarModdingGuide.Models;
using FaraAvatarModdingGuide.Web.Search;
using Xunit;

namespace FaraAvatarModdingGuide.Web.Tests;

/// <summary>
/// C# の検索（SearchEngine）が、現行の searchcore.js と同じ結果を返すことの突き合わせ（計画書 Phase 2 の受け入れ: 検索語 100 件程度で件数と上位 10 件の順が一致）。
/// tests/js/search-reference.mjs が searchcore.js を Node で動かし、同じ索引・同じ検索語（tests/js/search-queries.json）の結果を出す。
/// 件数・全件の並び（点数も）・正規化した検索語・上位 12 件の強調と抜き出しを比べる。CI では Node が無いと失敗する（飛ばさない）。
/// </summary>
public class SearchParityTests
{
    private static readonly string JsDir = Path.Combine(TestData.Root, "tests", "js");

    internal static readonly IReadOnlyList<SearchRecord> Records = SearchIndexJson.From(TestData.Service.BuildSearchIndex());
    internal static readonly string[] Queries = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(JsDir, "search-queries.json")))!;

    private static readonly Lazy<JsonElement[]> Reference = new(RunReference);

    private static JsonElement[] RunReference()
    {
        // 稀に Node が起動の途中で落ちる(マシンのメモリが足りないときの V8 の致命的エラー)ので、3 回まで試す。毎回落ちるなら、本当の失敗
        for (var attempt = 1; ; attempt++)
        {
            var (ok, error, result) = RunReferenceOnce();
            if (ok) return result!;
            if (attempt == 3) throw new Xunit.Sdk.XunitException(error);
            Thread.Sleep(500);
        }
    }

    private static (bool Ok, string Error, JsonElement[]? Result) RunReferenceOnce()
    {
        var work = Path.Combine(Path.GetTempPath(), "avatar-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var indexFile = Path.Combine(work, "index.json");
            File.WriteAllText(indexFile, JsonSerializer.Serialize(Records, SiteJsonContext.Default.IReadOnlyListSearchRecord), new UTF8Encoding(false));
            var outFile = Path.Combine(work, "reference.json");
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var arg in new[] { Path.Combine(JsDir, "search-reference.mjs"), indexFile, Path.Combine(JsDir, "search-queries.json"), outFile }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("node を起動できません（CI では actions/setup-node が要ります）");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return (false, $"search-reference.mjs が失敗しました（終了コード {process.ExitCode}）:" + Environment.NewLine + stderr.Result + stdout.Result, null);
            return (true, "", JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(outFile, Encoding.UTF8))!);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static SearchEngine Engine() => new(Records, SearchIndexJson.Normalize);

    [Fact]
    public void There_are_enough_queries_and_the_reference_covers_all_of_them()
    {
        Assert.True(Queries.Length >= 100, "検索語は 100 件以上");
        Assert.Equal(Queries.Length, Reference.Value.Length);
        // 結果のある検索語・ない検索語の両方がある
        Assert.Contains(Reference.Value, r => r.GetProperty("count").GetInt32() > 20);
        Assert.Contains(Reference.Value, r => r.GetProperty("count").GetInt32() == 0);
    }

    [Fact]
    public void Query_normalisation_matches_searchcore()
    {
        for (var i = 0; i < Queries.Length; i++)
            Assert.Equal(Reference.Value[i].GetProperty("norm").GetString(), SearchIndexJson.Normalize(Queries[i]));
    }

    [Fact]
    public void Romaji_conversion_and_long_vowel_folding_match_searchcore_for_every_query()
    {
        var converted = 0;
        for (var i = 0; i < Queries.Length; i++)
        {
            var norm = SearchIndexJson.Normalize(Queries[i]);
            var expected = Reference.Value[i].GetProperty("romaji");
            var actual = RomajiKana.ToHiragana(norm);
            Assert.True(expected.ValueKind == JsonValueKind.Null ? actual is null : expected.GetString() == actual, $"検索語 [{Queries[i]}]: ローマ字変換 searchcore={expected} / C#={actual}");
            Assert.Equal(Reference.Value[i].GetProperty("loose").GetString(), RomajiKana.Loose(Queries[i]));
            if (actual is not null) converted++;
        }
        Assert.True(converted >= 100, "変換表の全キーを通す語が変換されている: " + converted);
    }

    [Fact]
    public void Count_and_order_of_every_hit_match_searchcore()
    {
        var engine = Engine();
        for (var i = 0; i < Queries.Length; i++)
        {
            var expected = Reference.Value[i];
            var actual = engine.Search(Queries[i]);
            var label = $"検索語 [{Queries[i]}]";
            Assert.True(expected.GetProperty("count").GetInt32() == actual.Count, $"{label}: 件数 searchcore={expected.GetProperty("count").GetInt32()} / C#={actual.Count}");
            var hits = expected.GetProperty("hits");
            for (var k = 0; k < actual.Count; k++)
            {
                var h = hits[k];
                var a = actual[k];
                Assert.True(
                    h.GetProperty("type").GetString() == a.Entry.Type && h.GetProperty("url").GetString() == a.Entry.Url && h.GetProperty("title").GetString() == a.Entry.Title,
                    $"{label}: {k + 1} 件目 searchcore=({h.GetProperty("type")} {h.GetProperty("title")}) / C#=({a.Entry.Type} {a.Entry.Title})");
                Assert.True(h.GetProperty("score").GetDouble() == a.Score, $"{label}: {k + 1} 件目の点数 searchcore={h.GetProperty("score")} / C#={a.Score}");
            }
        }
    }

    [Fact]
    public void Highlight_and_snippet_of_the_top_hits_match_searchcore()
    {
        var engine = Engine();
        for (var i = 0; i < Queries.Length; i++)
        {
            var query = Queries[i];
            var top = Reference.Value[i].GetProperty("top");
            var hits = engine.Search(query);
            Assert.Equal(Math.Min(hits.Count, 12), top.GetArrayLength());
            for (var k = 0; k < top.GetArrayLength(); k++)
            {
                var e = hits[k].Entry;
                var t = top[k];
                var label = $"検索語 [{query}] {k + 1} 件目 ({e.Title})";
                Assert.True(t.GetProperty("title").GetString() == SearchEngine.ToHtml(engine.Highlight(e.Title, query)), label + ": 題名の強調");
                var sub = string.IsNullOrEmpty(e.Subtitle) ? "" : SearchEngine.ToHtml(engine.Highlight(e.Subtitle, query));
                Assert.True(t.GetProperty("subtitle").GetString() == sub, label + ": 小見出しの強調");
                var snippet = engine.Snippet(e.Text, query);
                Assert.True(t.GetProperty("snippet").GetString() == snippet, label + ": 抜き出し");
                Assert.True(t.GetProperty("snippetShort").GetString() == engine.Snippet(e.Text, query, 100), label + ": 抜き出し（100 字）");
                Assert.True(t.GetProperty("snippetHtml").GetString() == SearchEngine.ToHtml(engine.Highlight(snippet, query)), label + ": 抜き出しの強調");
            }
        }
    }
}
