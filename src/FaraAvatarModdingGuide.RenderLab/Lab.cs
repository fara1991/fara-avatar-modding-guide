using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FaraAvatarModdingGuide.Practice;
using FaraAvatarModdingGuide.Render;

namespace FaraAvatarModdingGuide.RenderLab;

/// <summary>
/// レンダーラボの本体。ViewportCore(操作のロジック)に、gl.js が数値にしたポインター・ホイール・キーを渡し、フレームごとに SceneRenderer が作った描画の命令を gl.js に渡す。
/// URL の ?scene=[{"kind":"sphere","p":[0,0,0],"r":[0,0,0],"s":[1,1,1]}]&amp;view={"dist":8,"target":[0,0,0]} で最初のシーンを決められる(省略は立方体 1 つ)。
/// ページから window.__labControl(名前, 数) で、停止・再開・計測(RenderBench)・状態の取り出しを呼べる(Playwright の確認用)。
/// </summary>
internal static class Lab
{
    private static ViewportCore? core;
    private static SceneRenderer? renderer;
    private static float[] packed = new float[FramePacker.HeaderFloats + FramePacker.ItemFloats * 32];
    private static ViewRect rect = new(0, 0, 0, 0);
    private static long frameTicks;
    private static int frames;

    public static void Run()
    {
        Gl.ExposeControl(Control);
        if (!StartViewport()) Console.Error.WriteLine("WebGL 2 を使えません");
    }

    private static bool StartViewport()
    {
        var options = ParseQuery(Gl.GetQuery());
        core = new ViewportCore(new ViewRect(0, 0, 960, 540), options);
        renderer = new SceneRenderer(core);
        rect = new ViewRect(0, 0, 0, 0);
        frameTicks = 0;
        frames = 0;
        return Gl.Start("viewport", OnFrame, OnPointerDown, OnPointerMove, OnPointerUp, OnWheel, OnKey, OnKeyUp, OnFocus);
    }

    private static void StopViewport()
    {
        Gl.Dispose();
        renderer?.Dispose();
        renderer = null;
        core = null;
    }

    private static Modifiers Mods(int bits) => new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);

    /// <summary>描く。座標は gl.js が、ビューの左上を原点にして渡す(ViewportCore の Rect は (0, 0, 幅, 高さ))。</summary>
    private static void OnFrame(double width, double height)
    {
        if (core is null || renderer is null) return;
        var t0 = Stopwatch.GetTimestamp();
        var next = new ViewRect(0, 0, width, height);
        if (next != rect)
        {
            rect = next;
            core.Resize(next);
        }
        var frame = renderer.Build();
        foreach (var buffer in frame.Uploads) Gl.UploadBuffer(buffer.Id, (int)buffer.Layout, MemoryMarshal.AsBytes(buffer.Data.AsSpan()));
        foreach (var id in frame.Releases) Gl.ReleaseBuffer(id);
        var length = FramePacker.LengthOf(frame);
        if (packed.Length < length) packed = new float[length * 2];
        FramePacker.Pack(frame, packed);
        Gl.Draw(MemoryMarshal.AsBytes(packed.AsSpan(0, length)));
        frameTicks += Stopwatch.GetTimestamp() - t0;
        frames++;
    }

    /// <summary>button の下位 4 ビットがボタン、その上が修飾キー(1 Shift・2 Ctrl・4 Alt・8 Meta)。</summary>
    private static bool OnPointerDown(int buttonAndMods, double x, double y) => core?.PointerDown(buttonAndMods & 15, x, y, Mods(buttonAndMods >> 4)) ?? false;

    private static void OnPointerMove(double x, double y, int mods) => core?.PointerMove(x, y, Mods(mods));

    private static void OnPointerUp(double x, double y) => core?.PointerUp(x, y);

    private static bool OnWheel(double deltaY) => core?.Wheel(deltaY) ?? false;

    private static bool OnKey(string code, string key, int mods) => core?.KeyDown(code, key, Mods(mods)) ?? false;

    private static void OnKeyUp(int mods) => core?.KeyUp(Mods(mods));

    private static void OnFocus(bool on)
    {
        if (on) core?.Focus(); else core?.Blur();
    }

    // ---------------------------------------------------------------- ページからの制御
    private static double Control(string name, int arg)
    {
        switch (name)
        {
            case "stop": StopViewport(); return 0;
            case "start": return StartViewport() ? 1 : 0;
            case "frames":
                Gl.Report("frames", $"{{\"frames\":{frames},\"msPerFrame\":{Format(frames == 0 ? 0 : frameTicks * 1000.0 / Stopwatch.Frequency / frames)}}}");
                return frames;
            case "state": Gl.Report("state", StateJson()); return 0;
            case var t when t.StartsWith("tool:", StringComparison.Ordinal): core?.ClickTool(t[5..]); return 0;
            default:
                if (name.StartsWith("bench:", StringComparison.Ordinal))
                {
                    var result = RenderBench.Run(name[6..], arg);
                    Gl.Report(name, $"{{\"name\":\"{result.Name}\",\"iterations\":{result.Iterations},\"totalMs\":{Format(result.TotalMs)},\"msPerOp\":{Format(result.MsPerOp)}}}");
                    return result.MsPerOp;
                }
                return -1;
        }
    }

    private static string Format(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string StateJson()
    {
        if (core is null) return "{}";
        var sb = new StringBuilder();
        sb.Append("{\"mode\":").Append(Quote(core.Mode)).Append(",\"selectMode\":").Append(Quote(core.SelectMode)).Append(",\"xray\":").Append(core.Xray ? "true" : "false");
        sb.Append(",\"status\":").Append(Quote(core.Status));
        sb.Append(",\"view\":").Append(Quote(core.View.Label)).Append(",\"selected\":[");
        sb.Append(string.Join(",", core.Selected.Select(o => Quote(o.Name))));
        sb.Append("],\"objects\":[");
        sb.Append(string.Join(",", core.Objects.Select(o => $"{{\"name\":{Quote(o.Name)},\"p\":[{Format(o.Position.X)},{Format(o.Position.Y)},{Format(o.Position.Z)}]}}")));
        sb.Append("]}");
        return sb.ToString();
    }

    private static ViewportOptions ParseQuery(string query)
    {
        List<SceneSpec>? scene = null;
        double? dist = null;
        double[]? target = null;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length != 2) continue;
            var value = Uri.UnescapeDataString(kv[1]);
            if (kv[0] == "scene")
            {
                using var doc = JsonDocument.Parse(value);
                scene = [.. doc.RootElement.EnumerateArray().Select(d => new SceneSpec(
                    d.GetProperty("kind").GetString()!,
                    d.TryGetProperty("name", out var n) ? n.GetString() : null,
                    d.TryGetProperty("p", out var p) ? Doubles(p) : null,
                    d.TryGetProperty("r", out var r) ? Doubles(r) : null,
                    d.TryGetProperty("s", out var s) ? Doubles(s) : null))];
            }
            else if (kv[0] == "view")
            {
                using var doc = JsonDocument.Parse(value);
                if (doc.RootElement.TryGetProperty("dist", out var d)) dist = d.GetDouble();
                if (doc.RootElement.TryGetProperty("target", out var t)) target = Doubles(t);
            }
        }
        return new ViewportOptions { Scene = scene, ViewDist = dist, ViewTarget = target };
    }

    private static double[] Doubles(JsonElement a) => [.. a.EnumerateArray().Select(x => x.GetDouble())];
}
