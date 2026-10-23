using System.Text.Json;
using System.Text.Json.Nodes;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>JavaScript の出力(JsonElement)と C# の出力(JsonNode)の比較。数値は倍精度の誤差(1e-9)まで、文字・真偽は完全一致、null と無いものは同じ扱い。</summary>
internal static partial class JsonCompare
{
    public const double Tolerance = 1e-9;

    public static List<string> Diff(JsonElement expected, JsonNode? actual, int max = 12)
    {
        var errors = new List<string>();
        Walk(expected, actual, "$", errors, max);
        return errors;
    }

    /// <summary>
    /// 文字の比較。状態表示(status)の「-0.000」と「0.000」は同じとみなす: 値が 1e-16 ほどの誤差のとき、符号は計算の順序で変わり(three.js の行列と C# の式で順序が違う)、
    /// toFixed が「-0.000」を出すか「0.000」を出すかが分かれる。値そのもの(位置・回転など)は 1e-9 の誤差で比べている。
    /// </summary>
    private static bool SameText(string path, string actual, string expected) =>
        actual == expected || (path.EndsWith(".status", StringComparison.Ordinal) && NegativeZero().Replace(actual, "$1") == NegativeZero().Replace(expected, "$1"));

    [System.Text.RegularExpressions.GeneratedRegex(@"-(0\.0+)(?![0-9])")]
    private static partial System.Text.RegularExpressions.Regex NegativeZero();

    private static void Walk(JsonElement e, JsonNode? a, string path, List<string> errors, int max)
    {
        if (errors.Count >= max) return;
        switch (e.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                if (a is not null) errors.Add($"{path}: JS=null / C#={a.ToJsonString()}");
                return;
            case JsonValueKind.Number:
                if (a is null || a.GetValueKind() != JsonValueKind.Number) { errors.Add($"{path}: JS={e.GetRawText()} / C#={a?.ToJsonString() ?? "null"}"); return; }
                var x = e.GetDouble();
                var y = double.Parse(a.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
                if (!(Math.Abs(x - y) <= Tolerance * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))))) errors.Add($"{path}: JS={x:R} / C#={y:R}");
                return;
            case JsonValueKind.String:
                if (a is null || a.GetValueKind() != JsonValueKind.String || !SameText(path, a.GetValue<string>(), e.GetString()!)) errors.Add($"{path}: JS=\"{e.GetString()}\" / C#={a?.ToJsonString() ?? "null"}");
                return;
            case JsonValueKind.True or JsonValueKind.False:
                if (a is null || a.GetValueKind() != e.ValueKind) errors.Add($"{path}: JS={e.GetRawText()} / C#={a?.ToJsonString() ?? "null"}");
                return;
            case JsonValueKind.Array:
                if (a is not JsonArray arr) { errors.Add($"{path}: JS=配列 / C#={a?.ToJsonString() ?? "null"}"); return; }
                if (arr.Count != e.GetArrayLength()) { errors.Add($"{path}: 配列の長さ JS={e.GetArrayLength()} / C#={arr.Count}"); return; }
                for (var i = 0; i < arr.Count; i++) Walk(e[i], arr[i], $"{path}[{i}]", errors, max);
                return;
            case JsonValueKind.Object:
                if (a is not JsonObject obj) { errors.Add($"{path}: JS=オブジェクト / C#={a?.ToJsonString() ?? "null"}"); return; }
                var keys = e.EnumerateObject().Select(p => p.Name).Union(obj.Select(p => p.Key));
                foreach (var key in keys)
                {
                    var has = e.TryGetProperty(key, out var child);
                    Walk(has ? child : default, obj[key], $"{path}.{key}", errors, max);
                }
                return;
        }
    }
}
