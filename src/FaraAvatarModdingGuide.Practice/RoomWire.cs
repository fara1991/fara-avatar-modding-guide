using System.Globalization;
using System.Text;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>
/// 共同練習(room.js)の通信の文字。現行の JavaScript の JSON.stringify と同じ文字を作る(サーバーの読み方は同じでも、差分の比べ(server の対応表)が文字で比べるため)。
/// 数は JavaScript の Number → 文字の規則(最短の表記・指数の付け方・負のゼロは "0")、文字列は JSON.stringify のエスケープ(非 ASCII はそのまま)。
/// </summary>
public static class RoomWire
{
    private const char Quote = (char)34;
    private const char Back = (char)92;

    /// <summary>JavaScript の Number の文字(ECMAScript Number::toString)。有限の数だけを想定する(NaN・無限大は null)。</summary>
    public static string Num(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return "null";
        if (x == 0) return "0";                       // -0 も "0"
        var sign = x < 0 ? "-" : "";
        var r = Math.Abs(x).ToString("R", CultureInfo.InvariantCulture);
        // 最短の桁 d と、小数点の位置 n(値 = 0.d × 10^n)を取り出す
        var exp = 0;
        var e = r.IndexOfAny(['E', 'e']);
        if (e >= 0) { exp = int.Parse(r[(e + 1)..], CultureInfo.InvariantCulture); r = r[..e]; }
        var dot = r.IndexOf('.');
        var digits = dot >= 0 ? r.Remove(dot, 1) : r;
        var n = (dot >= 0 ? dot : r.Length) + exp;
        var lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') { lead++; n--; }
        digits = digits[lead..].TrimEnd('0');
        if (digits.Length == 0) return "0";
        var k = digits.Length;
        string body;
        if (k <= n && n <= 21) body = digits + new string('0', n - k);
        else if (0 < n && n <= 21) body = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0) body = "0." + new string('0', -n) + digits;
        else
        {
            var ex = n - 1;
            body = digits[..1] + (k > 1 ? "." + digits[1..] : "") + "e" + (ex >= 0 ? "+" : "-") + Math.Abs(ex).ToString(CultureInfo.InvariantCulture);
        }
        return sign + body;
    }

    /// <summary>JSON.stringify の文字列(引用符つき)。引用符と逆斜線と制御文字だけを escape し、非 ASCII はそのまま。孤立したサロゲートは 4 桁の escape。</summary>
    public static string Str(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append(Quote);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == Quote || c == Back) { sb.Append(Back).Append(c); continue; }
            switch ((int)c)
            {
                case 8: sb.Append(Back).Append('b'); break;
                case 9: sb.Append(Back).Append('t'); break;
                case 10: sb.Append(Back).Append('n'); break;
                case 12: sb.Append(Back).Append('f'); break;
                case 13: sb.Append(Back).Append('r'); break;
                default:
                    var pair = char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]);
                    if (c < 0x20 || char.IsSurrogate(c) && !pair)
                    {
                        sb.Append(Back).Append('u').Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (pair) { sb.Append(c).Append(s[i + 1]); i++; }
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append(Quote);
        return sb.ToString();
    }

    /// <summary>[a,b,c]（JSON.stringify の配列。要素は数）。</summary>
    public static string Nums(IEnumerable<double> values) => "[" + string.Join(",", values.Select(Num)) + "]";

    /// <summary>部屋で共有するオブジェクトの形(roomState の 1 件。キーの順は id・kind・name・p・r・s)。</summary>
    public static string Object(RoomObject o) =>
        "{" + Key("id") + Str(o.Id) + "," + Key("kind") + Str(o.Kind) + "," + Key("name") + Str(o.Name) + ","
        + Key("p") + Nums(o.P) + "," + Key("r") + Nums(o.R) + "," + Key("s") + Nums(o.S) + "}";

    /// <summary>
    /// 参加（hello）。owner(作成者の鍵)・pass(鍵)・create(作成の指定)は、あるときだけ付ける（無いときは従来の文字と同じ）。
    /// create の中の pass は、作成するときの鍵。キーの順は t・name・owner・pass・create。
    /// </summary>
    public static string Hello(string name, string? owner = null, string? pass = null, RoomCreateOptions? create = null)
    {
        var sb = new StringBuilder("{" + Key("t") + Str("hello") + "," + Key("name") + Str(name));
        if (owner is not null) sb.Append(',').Append(Key("owner")).Append(Str(owner));
        if (pass is not null) sb.Append(',').Append(Key("pass")).Append(Str(pass));
        if (create is not null)
        {
            sb.Append(',').Append(Key("create")).Append('{').Append(Key("name")).Append(Str(create.Name))
                .Append(',').Append(Key("listed")).Append(create.Listed ? "true" : "false");
            if (create.Pass is not null) sb.Append(',').Append(Key("pass")).Append(Str(create.Pass));
            sb.Append('}');
        }
        return sb.Append('}').ToString();
    }

    /// <summary>部屋を閉じる（作成者だけ。サーバーが確かめる）。</summary>
    public static string Close() => "{" + Key("t") + Str("close") + "}";
    public static string Name(string name) => "{" + Key("t") + Str("name") + "," + Key("name") + Str(name) + "}";
    public static string Select(IEnumerable<string> ids) => "{" + Key("t") + Str("select") + "," + Key("ids") + Ids(ids) + "}";
    public static string Lock(IEnumerable<string> ids) => "{" + Key("t") + Str("lock") + "," + Key("ids") + Ids(ids) + "}";
    public static string Unlock() => "{" + Key("t") + Str("unlock") + "}";
    public static string Ping() => "{" + Key("t") + Str("ping") + "}";

    /// <summary>カーソル(3D の点。null は画面の外・離れたとき)。</summary>
    public static string Cursor(double[]? p) => "{" + Key("t") + Str("cursor") + "," + Key("p") + (p is null ? "null" : Nums(p)) + "}";

    /// <summary>視点。キーの順は target・dist・theta・phi・ortho・name(getView のまま)。</summary>
    public static string View(ViewState v) =>
        "{" + Key("t") + Str("view") + "," + Key("view") + "{" + Key("target") + Nums(v.Target) + "," + Key("dist") + Num(v.Dist) + ","
        + Key("theta") + Num(v.Theta) + "," + Key("phi") + Num(v.Phi) + "," + Key("ortho") + (v.Ortho ? "true" : "false") + "," + Key("name") + Str(v.Name) + "}}";

    /// <summary>変形の途中経過(id・p・r・s)。</summary>
    public static string Preview(IEnumerable<RoomPreview> objs) =>
        "{" + Key("t") + Str("preview") + "," + Key("objs") + "[" + string.Join(",", objs.Select(o =>
            "{" + Key("id") + Str(o.Id) + "," + Key("p") + Nums(o.P) + "," + Key("r") + Nums(o.R) + "," + Key("s") + Nums(o.S) + "}")) + "]}";

    /// <summary>差分。upsert は roomState の形の文字(呼び出し側が作る)、delete は id の並び。</summary>
    public static string Ops(IEnumerable<string> upsertJson, IEnumerable<string> delete) =>
        "{" + Key("t") + Str("ops") + "," + Key("upsert") + "[" + string.Join(",", upsertJson) + "]," + Key("delete") + Ids(delete) + "}";

    /// <summary>キー(引用符つき)と :。</summary>
    public static string Key(string name) => Str(name) + ":";

    private static string Ids(IEnumerable<string> ids) => "[" + string.Join(",", ids.Select(Str)) + "]";
}

/// <summary>途中経過の 1 件(preview)。</summary>
public sealed record RoomPreview(string Id, double[] P, double[] R, double[] S);
