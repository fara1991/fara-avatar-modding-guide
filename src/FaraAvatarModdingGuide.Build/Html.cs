using System.Net;
using System.Text;

namespace FaraAvatarModdingGuide.Build;

/// <summary>静的 HTML を組み立てるための小さな道具（エスケープ）。</summary>
internal static class Html
{
    /// <summary>本文・属性値の両方に使えるエスケープ。</summary>
    public static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>空白をひとつにまとめて前後を落とす（属性値や説明文の整形用）。</summary>
    public static string Squash(string? text) =>
        System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();

    /// <summary>文章を説明文（meta description）の長さに収める。長すぎるときは末尾を … にする。</summary>
    public static string Describe(string? text, int max = 150)
    {
        var squashed = Squash(text);
        if (squashed.Length <= max) return squashed;
        return squashed[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>StringBuilder に 1 行ずつ書く（可読性のために改行を入れる）。</summary>
    public static StringBuilder Line(this StringBuilder sb, string line) => sb.Append(line).Append('\n');
}
