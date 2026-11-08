using System.Text.Json;
using System.Text.Json.Nodes;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>JavaScript の出力と C# の出力(どちらも JSON)の比較。数値は倍精度の誤差(1e-9)まで、文字・真偽は完全一致、null と無いものは同じ扱い。</summary>
internal static partial class JsonCompare
{
    public const double Tolerance = 1e-9;

    public static List<string> Diff(JsonElement expected, JsonNode? actual, int max = 12) => Diff(JsonNode.Parse(expected.GetRawText()), actual, max);

    public static List<string> Diff(JsonNode? expected, JsonNode? actual, int max = 12)
    {
        var errors = new List<string>();
        Walk(expected, actual, "$", errors, max);
        return errors;
    }

    /// <summary>
    /// 文字の比較。状態表示(status)の「-0.000」と「0.000」は同じとみなす: 値が 1e-16 ほどの誤差のとき、符号は計算の順序で変わり(three.js の行列と C# の式で順序が違う)、
    /// toFixed が「-0.000」を出すか「0.000」を出すかが分かれる。値そのもの(位置・回転など)は 1e-9 の誤差で比べている。
    /// 同じ理由で、N パネルの入力欄の文字(.value)と、統計・イベントの文字も、同じ形(-0.0...)の違いだけは無視する。
    /// </summary>
    private static bool SameText(string path, string actual, string expected) =>
        actual == expected || (IsDisplayText(path) && NegativeZero().Replace(actual, "$1") == NegativeZero().Replace(expected, "$1"));

    private static bool IsDisplayText(string path) => path.EndsWith(".status", StringComparison.Ordinal) || path.EndsWith(".value", StringComparison.Ordinal);

    [System.Text.RegularExpressions.GeneratedRegex(@"-(0\.0+)(?![0-9])")]
    private static partial System.Text.RegularExpressions.Regex NegativeZero();

    private static void Walk(JsonNode? e, JsonNode? a, string path, List<string> errors, int max)
    {
        if (errors.Count >= max) return;
        if (e is null)
        {
            if (a is not null) errors.Add($"{path}: JS=null / C#={a.ToJsonString()}");
            return;
        }
        switch (e)
        {
            case JsonArray ea:
                if (a is not JsonArray arr) { errors.Add($"{path}: JS=配列 / C#={Short(a)}"); return; }
                if (arr.Count != ea.Count) { errors.Add($"{path}: 配列の長さ JS={ea.Count} / C#={arr.Count}"); return; }
                for (var i = 0; i < arr.Count; i++) Walk(ea[i], arr[i], $"{path}[{i}]", errors, max);
                return;
            case JsonObject eo:
                if (a is not JsonObject obj) { errors.Add($"{path}: JS=オブジェクト / C#={Short(a)}"); return; }
                foreach (var key in eo.Select(p => p.Key).Union(obj.Select(p => p.Key)))
                {
                    eo.TryGetPropertyValue(key, out var child);
                    obj.TryGetPropertyValue(key, out var achild);
                    Walk(child, achild, $"{path}.{key}", errors, max);
                }
                return;
        }
        var kind = e.GetValueKind();
        switch (kind)
        {
            case JsonValueKind.Number:
                if (a is null || a.GetValueKind() != JsonValueKind.Number) { errors.Add($"{path}: JS={e.ToJsonString()} / C#={Short(a)}"); return; }
                var x = double.Parse(e.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
                var y = double.Parse(a.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
                if (!(Math.Abs(x - y) <= Tolerance * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))))) errors.Add($"{path}: JS={x:R} / C#={y:R}");
                return;
            case JsonValueKind.String:
                if (a is null || a.GetValueKind() != JsonValueKind.String || !SameText(path, a.GetValue<string>(), e.GetValue<string>())) errors.Add($"{path}: JS=\"{e.GetValue<string>()}\" / C#={Short(a)}");
                return;
            case JsonValueKind.True or JsonValueKind.False:
                if (a is null || a.GetValueKind() != kind) errors.Add($"{path}: JS={e.ToJsonString()} / C#={Short(a)}");
                return;
        }
    }

    private static string Short(JsonNode? n)
    {
        if (n is null) return "null";
        var s = n.ToJsonString();
        return s.Length > 200 ? s[..200] + "…" : s;
    }
}
