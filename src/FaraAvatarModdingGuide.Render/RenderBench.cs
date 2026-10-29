using System.Diagnostics;
using FaraAvatarModdingGuide.Practice;

namespace FaraAvatarModdingGuide.Render;

/// <summary>1 つの計測の結果。TotalMs は計った部分だけの合計(準備は含まない)。</summary>
public sealed record BenchResult(string Name, int Iterations, double TotalMs)
{
    public double MsPerOp => Iterations == 0 ? 0 : TotalMs / Iterations;
}

/// <summary>
/// 「AOT なし(IL を解釈実行する WebAssembly)で、毎フレームの C# と重い操作が予算(1 フレーム 16 ms のうち 5 ms)に収まるか」を測る場面
/// (docs/phase3b-design.md の §10.2-4 と §11)。同じコードを、ネイティブの .NET(テストから)と WebAssembly(レンダーラボから)で動かして比べる。
/// 準備(シーンを作る・モードを切り替える)は時間に入れず、計るのは操作 1 回ぶんの呼び出しだけ。球は 482 頂点・960 三角形(UV 球)。
/// </summary>
public static class RenderBench
{
    public static readonly IReadOnlyList<string> Names =
    [
        "frame-object", "frame-edit", "drag-edit-frame", "click-object", "click-vert", "box-select-all", "move-all-vertices", "extrude-start", "undo",
    ];

    /// <summary>直前の drag-edit-frame で、バッファの送り直しが運んだ float の数(1 回あたりの目安を出すため)。</summary>
    public static long Touched { get; private set; }

    private static readonly Modifiers None = new();
    private static readonly Modifiers Ctrl = new(Ctrl: true);

    private sealed class Timer
    {
        public long Ticks;

        public void Time(Action action)
        {
            var t0 = Stopwatch.GetTimestamp();
            action();
            Ticks += Stopwatch.GetTimestamp() - t0;
        }

        public double Ms => Ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static ViewportCore NewCore(string kind = "sphere")
    {
        var core = new ViewportCore(new ViewRect(0, 0, 960, 540), new ViewportOptions { Scene = [new SceneSpec(kind)] });
        core.Focus();
        core.PointerDown(0, 480, 270, None);
        core.PointerUp(480, 270);  // 球の真ん中をクリックして選択
        return core;
    }

    private static void Key(ViewportCore core, string code, Modifiers? m = null) => core.KeyDown(code, "", m ?? None);

    public static BenchResult Run(string name, int iterations)
    {
        var timer = new Timer();
        switch (name)
        {
            case "frame-object":
            case "frame-edit":
            {
                var core = NewCore();
                if (name == "frame-edit") Key(core, "Tab");
                using var renderer = new SceneRenderer(core);
                var packed = Array.Empty<float>();
                renderer.Build();  // 最初の 1 回は、バッファを作るので除く
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        var frame = renderer.Build();
                        if (packed.Length < FramePacker.LengthOf(frame)) packed = new float[FramePacker.LengthOf(frame)];
                        FramePacker.Pack(frame, packed);
                    }
                });
                break;
            }
            case "drag-edit-frame":
            {
                // 編集モードで全頂点を G で動かしながら、マウスの移動のたびに 1 フレーム(Build + 詰める + 変わったバッファのコピー)。形が毎回変わるので、メッシュ・輪郭・点・線・面を毎回作り直す
                var core = NewCore();
                Key(core, "Tab");
                Key(core, "KeyA");
                Key(core, "KeyG");
                using var renderer = new SceneRenderer(core);
                var packed = Array.Empty<float>();
                renderer.Build();
                long touched = 0;
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        core.PointerMove(300 + i % 300, 200 + i % 100, None);
                        var frame = renderer.Build();
                        if (packed.Length < FramePacker.LengthOf(frame)) packed = new float[FramePacker.LengthOf(frame)];
                        FramePacker.Pack(frame, packed);
                        foreach (var u in frame.Uploads) touched += u.Data.Length;   // JS に送る量(float の数)
                    }
                });
                Touched = touched;
                Key(core, "Escape");
                break;
            }
            case "click-object":
            {
                var core = NewCore();
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        core.PointerDown(0, 470 + i % 20, 260 + i % 20, None);
                        core.PointerUp(470 + i % 20, 260 + i % 20);
                    }
                });
                break;
            }
            case "click-vert":
            {
                var core = NewCore();
                Key(core, "Tab");
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        core.PointerDown(0, 470 + i % 20, 260 + i % 20, None);
                        core.PointerUp(470 + i % 20, 260 + i % 20);
                    }
                });
                break;
            }
            case "box-select-all":
            {
                var core = NewCore();
                Key(core, "Tab");
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        core.PointerDown(0, 2, 2, None);
                        core.PointerMove(958, 538, None);
                        core.PointerUp(958, 538);
                    }
                });
                break;
            }
            case "move-all-vertices":
            {
                var core = NewCore();
                Key(core, "Tab");
                Key(core, "KeyA");
                Key(core, "KeyG");
                timer.Time(() =>
                {
                    for (var i = 0; i < iterations; i++) core.PointerMove(300 + i % 300, 200 + i % 100, None);
                });
                Key(core, "Escape");
                break;
            }
            case "extrude-start":
            {
                var core = NewCore();
                Key(core, "Tab");
                Key(core, "Digit3");
                Key(core, "KeyA");
                for (var i = 0; i < iterations; i++)
                {
                    timer.Time(() => Key(core, "KeyE"));
                    Key(core, "Escape");
                    Key(core, "KeyZ", Ctrl);
                    Key(core, "KeyA");
                }
                break;
            }
            case "undo":
            {
                var core = NewCore();
                for (var i = 0; i < iterations; i++)
                {
                    Key(core, "KeyG");
                    core.PointerMove(300 + i % 50, 200, None);
                    Key(core, "Enter");
                    timer.Time(() => Key(core, "KeyZ", Ctrl));
                    Key(core, "KeyZ", new Modifiers(Ctrl: true, Shift: true));  // やり直して、元の状態に戻す
                }
                break;
            }
            default:
                throw new ArgumentException("未知の計測: " + name, nameof(name));
        }
        return new BenchResult(name, iterations, timer.Ms);
    }
}
