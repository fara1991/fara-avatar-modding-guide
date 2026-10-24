using System.Text.Json;
using System.Text.Json.Nodes;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>突き合わせ 1 ケース分の、C# 側の ViewportCore(と、練習の進行)。</summary>
internal sealed class Session
{
    public required ViewportCore Core { get; init; }
    public PracticeSession? Practice { get; init; }
}

/// <summary>
/// 突き合わせの入力(tests/js/practice-*.json の 1 ケース)を ViewportCore に通し、practice-reference.mjs と同じ形の状態(JSON)を作る。
/// JavaScript 側の createHarness・applyStep・snapshot と対になっている。
/// </summary>
internal static class ViewportDriver
{
    public static Vec3 Vec(JsonElement a) => new(a[0].GetDouble(), a[1].GetDouble(), a[2].GetDouble());

    private static double[] Doubles(JsonElement a) => [.. a.EnumerateArray().Select(x => x.GetDouble())];

    public static MeshData Mesh(JsonElement d) => new([.. d.GetProperty("v").EnumerateArray().Select(Vec)], [.. d.GetProperty("f").EnumerateArray().Select(f => f.EnumerateArray().Select(i => i.GetInt32()).ToArray())])
    {
        Loose = d.TryGetProperty("e", out var e) ? [.. e.EnumerateArray().Select(p => p.EnumerateArray().Select(i => i.GetInt32()).ToArray())] : [],
        VSel = d.TryGetProperty("vs", out var vs) ? [.. vs.EnumerateArray().Select(i => i.GetInt32())] : [],
        FSel = d.TryGetProperty("fs", out var fs) ? [.. fs.EnumerateArray().Select(i => i.GetInt32())] : [],
        ESel = d.TryGetProperty("es", out var es) ? [.. es.EnumerateArray().Select(p => p.EnumerateArray().Select(i => i.GetInt32()).ToArray())] : [],
    };

    /// <summary>部屋の ID に使う決まった列(practice-dom.mjs の seedRandomIds と同じ線形合同法)。</summary>
    public static Func<string> IdSource(uint seed)
    {
        var state = seed;
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        return () =>
        {
            var chars = new char[8];
            for (var i = 0; i < 8; i++)
            {
                state = unchecked(state * 1103515245u + 12345u);
                chars[i] = digits[(int)((state >> 16) & 0xff) % 36];
            }
            return new string(chars);
        };
    }

    public static Session Build(JsonElement c)
    {
        var root = c.GetProperty("root");
        var rect = new ViewRect(root.GetProperty("left").GetDouble(), root.GetProperty("top").GetDouble(), root.GetProperty("width").GetDouble(), root.GetProperty("height").GetDouble());
        var emulate = c.GetProperty("emulate").GetBoolean();
        var practice = c.TryGetProperty("practice", out var pr) && pr.GetBoolean();
        var seed = c.TryGetProperty("seed", out var sd) ? sd.GetUInt32() : 1u;
        var opt = c.TryGetProperty("options", out var o) ? o : default;
        var locked = opt.ValueKind == JsonValueKind.Object && opt.TryGetProperty("locked", out var l) ? l.EnumerateArray().Select(x => x.GetString()!).ToArray() : null;
        double? viewDist = null;
        double[]? viewTarget = null;
        if (opt.ValueKind == JsonValueKind.Object && opt.TryGetProperty("view", out var ov))
        {
            if (ov.TryGetProperty("dist", out var dd)) viewDist = dd.GetDouble();
            if (ov.TryGetProperty("target", out var tt)) viewTarget = Doubles(tt);
        }
        var specs = c.GetProperty("objects").EnumerateArray().Select(d => new SceneSpec(
            d.GetProperty("kind").GetString()!,
            d.TryGetProperty("name", out var n) ? n.GetString() : null,
            d.TryGetProperty("p", out var p) ? Doubles(p) : null,
            d.TryGetProperty("r", out var r) ? Doubles(r) : null,
            d.TryGetProperty("s", out var s) ? Doubles(s) : null)).ToList();
        var options = new ViewportOptions
        {
            Scene = practice ? null : specs,
            EmulateNumpad = () => emulate,
            WheelNeedsFocus = opt.ValueKind == JsonValueKind.Object && opt.TryGetProperty("wheelNeedsFocus", out var w) && w.GetBoolean(),
            ViewDist = viewDist,
            ViewTarget = viewTarget,
            CanTransform = locked is null ? null : sel => sel.Any(x => locked.Contains(x.Name)) ? "ほかの人が操作中です" : null,
            NewId = IdSource(seed),
        };
        var core = new ViewportCore(rect, options);
        var session = practice ? new PracticeSession(core) : null;
        var index = 0;
        foreach (var d in c.GetProperty("objects").EnumerateArray())
        {
            if (d.TryGetProperty("mesh", out var mesh) && !practice) core.ReplaceMesh(core.Objects[index], Mesh(mesh));
            index++;
        }
        if (c.TryGetProperty("view", out var view))
        {
            core.View.Target = Vec(view.GetProperty("target"));
            core.View.Dist = view.GetProperty("dist").GetDouble();
            core.View.Theta = view.GetProperty("theta").GetDouble();
            core.View.Phi = view.GetProperty("phi").GetDouble();
            core.View.Name = view.GetProperty("name").GetString()!;
            core.View.Ortho = view.GetProperty("ortho").GetBoolean();
        }
        if (c.TryGetProperty("selected", out var sel2)) core.SetSelection(sel2.EnumerateArray().Select(i => core.Objects[i.GetInt32()]));
        else core.SetSelection([]);
        core.TakeEvents();
        return new Session { Core = core, Practice = session };
    }

    private static Modifiers Mods(JsonElement s) => new(Flag(s, "shift"), Flag(s, "ctrl"), Flag(s, "alt"), Flag(s, "meta"));

    private static bool Flag(JsonElement s, string name) => s.TryGetProperty(name, out var v) && v.GetBoolean();

    private static double Num(JsonElement s, string name) => s.TryGetProperty(name, out var v) ? v.GetDouble() : 0;

    private static RoomObject Room(JsonElement d) => new(d.GetProperty("id").GetString()!, d.GetProperty("kind").GetString()!, d.GetProperty("name").GetString()!, Doubles(d.GetProperty("p")), Doubles(d.GetProperty("r")), Doubles(d.GetProperty("s")));

    /// <summary>1 手順を渡す。戻り値は、現行が preventDefault を呼ぶ手順で true。</summary>
    public static bool Apply(Session session, JsonElement step)
    {
        var core = session.Core;
        switch (step.GetProperty("e").GetString())
        {
            case "move": core.PointerMove(Num(step, "x"), Num(step, "y"), Mods(step)); return false;
            case "down": return core.PointerDown((int)Num(step, "button"), Num(step, "x"), Num(step, "y"), Mods(step));
            case "up": core.PointerUp(Num(step, "x"), Num(step, "y")); return false;
            case "wheel": return core.Wheel(Num(step, "dy"));
            case "key": return core.KeyDown(step.GetProperty("code").GetString()!, step.TryGetProperty("key", out var k) ? k.GetString()! : "", Mods(step));
            case "keyup": core.KeyUp(Mods(step)); return false;
            case "tool": core.ClickTool(step.GetProperty("tool").GetString()!); return false;
            case "nchange": core.ChangeNPanel(step.GetProperty("prop").GetString()!, step.GetProperty("axis").GetString()!, step.GetProperty("value").GetString()!); return false;
            case "menu": core.ChooseMenu(step.GetProperty("i").GetInt32()); return false;
            case "blur": core.Blur(); return false;
            case "focus": core.Focus(); return false;
            case "resize": core.Resize(new ViewRect(core.Rect.Left, core.Rect.Top, step.GetProperty("w").GetDouble(), step.GetProperty("h").GetDouble())); return false;
            case "reset": if (session.Practice is { } p) p.Reset(); else core.ResetScene(); return false;
            case "room":
                core.ApplyRemote([.. (step.TryGetProperty("upsert", out var u) ? u.EnumerateArray().Select(Room) : [])],
                    [.. (step.TryGetProperty("delete", out var del) ? del.EnumerateArray().Select(x => x.GetString()!) : [])], Flag(step, "keepUndo"));
                return false;
            case "roomload": core.LoadRoomScene([.. step.GetProperty("objects").EnumerateArray().Select(Room)]); return false;
            case "setview":
            {
                var v = step.GetProperty("view");
                core.SetView(new ViewState(Doubles(v.GetProperty("target")), v.GetProperty("dist").GetDouble(), v.GetProperty("theta").GetDouble(), v.GetProperty("phi").GetDouble(), v.GetProperty("ortho").GetBoolean(), v.GetProperty("name").GetString()!));
                return false;
            }
            case "highlight":
            {
                var h = step.TryGetProperty("h", out var hh) ? hh : default;
                string? S(string k) => h.ValueKind == JsonValueKind.Object && h.TryGetProperty(k, out var v) ? v.GetString() : null;
                core.SetHighlight(new TutorialHighlight(S("object"), S("label"), S("axis"), S("hud"), h.ValueKind == JsonValueKind.Object && h.TryGetProperty("at", out var at) ? Doubles(at) : null, S("tool")));
                return false;
            }
            case "orbit": core.OrbitBy(step.GetProperty("dTheta").GetDouble(), step.TryGetProperty("dPhi", out var dp) ? dp.GetDouble() : 0); return false;
            default: throw new InvalidOperationException("未知の手順: " + step.GetProperty("e").GetString());
        }
    }

    // ---------------------------------------------------------------- 状態(JSON)
    /// <summary>NaN・∞ は JSON にできない(JavaScript の JSON.stringify は null にする)ので null にする。</summary>
    private static JsonNode? N(double d) => double.IsFinite(d) ? JsonValue.Create(d == 0 ? 0 : d) : null;

    private static JsonArray Arr(Vec3 v) => new(N(v.X), N(v.Y), N(v.Z));

    private static JsonArray Arr(IEnumerable<double> values) => new(values.Select(N).ToArray());

    private static JsonNode? EventValue(object? v) => v switch
    {
        null => null,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        double d => N(d),
        string[] a => new JsonArray(a.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        IReadOnlyDictionary<string, object?> dict => new JsonObject(dict.Select(kv => KeyValuePair.Create(kv.Key, EventValue(kv.Value)))),
        _ => throw new InvalidOperationException(v.GetType().Name),
    };

    private static JsonArray Ints(IEnumerable<int> values) => new(values.Select(i => (JsonNode)JsonValue.Create(i)).ToArray());

    private static JsonObject MeshJson(MeshData d) => new()
    {
        ["v"] = new JsonArray(d.Verts.Select(v => (JsonNode)Arr(v)).ToArray()),
        ["f"] = new JsonArray(d.Faces.Select(f => (JsonNode)Ints(f)).ToArray()),
        ["e"] = new JsonArray(d.Loose.Select(f => (JsonNode)Ints(f)).ToArray()),
        ["vs"] = Ints(d.VSel),
        ["fs"] = Ints(d.FSel),
        ["es"] = new JsonArray(d.ESel.Select(f => (JsonNode)Ints(f)).ToArray()),
    };

    public static JsonObject Snapshot(Session session, bool? prevented = null)
    {
        var core = session.Core;
        var view = core.View;
        var (right, up, _) = view.Basis;
        var h = view.OrthoHalfHeight;
        var op = core.Op;
        var lc = core.LoopCut;
        var editObj = core.EditObj;
        var info = core.Mode == "edit" ? core.GetEditInfo() : null;
        var s = new JsonObject
        {
            ["objects"] = new JsonArray(core.Objects.Select(o =>
            {
                var visible = core.OutlineVisible(o);
                return (JsonNode)new JsonObject
                {
                    ["name"] = o.Name, ["kind"] = o.Kind, ["rid"] = o.Rid, ["p"] = Arr(o.Position),
                    ["q"] = new JsonArray(N(o.Quaternion.X), N(o.Quaternion.Y), N(o.Quaternion.Z), N(o.Quaternion.W)),
                    ["s"] = Arr(o.Scale), ["euler"] = Arr(o.Rotation), ["mirror"] = o.Mirror,
                    ["outline"] = new JsonObject { ["visible"] = visible, ["active"] = visible && core.OutlineIsActive(o) },
                    ["mesh"] = MeshJson(o.Em.ToData()),
                };
            }).ToArray()),
            ["selected"] = new JsonArray(core.Selected.Select(o => (JsonNode)JsonValue.Create(o.Name)!).ToArray()),
            ["active"] = core.Active?.Name,
            ["mode"] = core.Mode, ["selectMode"] = core.SelectMode, ["editObj"] = editObj?.Name, ["xray"] = core.Xray, ["showStats"] = core.ShowStats,
            ["view"] = new JsonObject { ["target"] = Arr(view.Target), ["dist"] = N(view.Dist), ["theta"] = N(view.Theta), ["phi"] = N(view.Phi), ["name"] = view.Name },
            ["ortho"] = view.Ortho,
            ["camera"] = new JsonObject
            {
                ["position"] = Arr(view.Position), ["up"] = Arr(view.Up), ["right"] = Arr(right), ["camUp"] = Arr(up), ["dir"] = Arr(view.Forward),
                ["ortho"] = new JsonArray(N(-h * view.Aspect), N(h * view.Aspect), N(h), N(-h)),
            },
            ["status"] = core.Status,
            ["numericHud"] = core.NumericHud,
            ["viewHud"] = view.Label,
            ["modeHud"] = core.ModeLabel,
            ["statsHidden"] = core.StatsHidden,
            ["statsLines"] = core.StatsHidden ? null : new JsonArray(core.StatsLines.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["toolsEditHidden"] = core.ToolsEditHidden,
            ["tools"] = new JsonObject(core.Tools.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonObject { ["on"] = kv.Value.On, ["disabled"] = kv.Value.Disabled }))),
            ["npanel"] = new JsonObject
            {
                ["hidden"] = core.NPanel.Hidden, ["name"] = core.NPanel.Name,
                ["rows"] = new JsonArray(core.NPanel.Rows.Select(r => (JsonNode)new JsonObject { ["prop"] = r.Prop, ["axis"] = r.Axis, ["value"] = r.Value, ["disabled"] = r.Disabled }).ToArray()),
            },
            ["op"] = op is null ? null : new JsonObject
            {
                ["kind"] = op.Kind.Name(), ["axis"] = op.Axis, ["exclude"] = op.Exclude, ["numeric"] = op.Numeric, ["edit"] = op.Edit,
                ["fromExtrude"] = op.Kind.IsTransform() ? op.FromExtrude : null, ["fromDuplicate"] = op.Kind.IsTransform() ? op.FromDuplicate : null,
                ["normal"] = op.Normal is { } nm ? Arr(nm) : null,
                ["limit"] = op.Kind is TransformKind.I or TransformKind.B ? N(op.Limit) : null,
                ["cuts"] = op.Kind == TransformKind.Slide ? op.Cuts : null,
            },
            ["loopcut"] = lc is null ? null : new JsonObject
            {
                ["cuts"] = lc.Cuts,
                ["edge"] = lc.Edge is { } edge ? new JsonArray(editObj!.Em.Verts.IndexOf(edge.A), editObj.Em.Verts.IndexOf(edge.B)) : null,
                // three.js の Float32Array と同じ丸め
                ["preview"] = lc.Preview is { } pv ? Arr(pv.SelectMany(p => new[] { (double)(float)p.X, (double)(float)p.Y, (double)(float)p.Z })) : null,
            },
            ["menu"] = core.Menu is { } menu ? new JsonObject { ["title"] = menu.Title, ["items"] = new JsonArray(menu.Items.Select(i => (JsonNode)JsonValue.Create(i.Label)!).ToArray()) } : null,
            ["axisLine"] = core.AxisLine.Visible,
            ["undoDepth"] = core.UndoDepth, ["redoDepth"] = core.RedoDepth,
            ["focused"] = core.Focused,
            ["editInfo"] = info is null ? null : new JsonObject
            {
                ["faces"] = new JsonArray(info.Faces.Select(f => (JsonNode)new JsonObject { ["center"] = Arr(f.Center), ["normal"] = f.Area >= 1e-4 ? Arr(f.Normal) : null, ["sides"] = f.Sides }).ToArray()),
                ["stats"] = info.Stats is { } st ? new JsonObject { ["verts"] = st.Verts, ["edges"] = st.Edges, ["faces"] = st.Faces, ["tris"] = st.Tris } : null,
            },
            ["room"] = new JsonArray(core.RoomState().Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["kind"] = r.Kind, ["name"] = r.Name, ["p"] = Arr(r.P), ["r"] = Arr(r.R), ["s"] = Arr(r.S) }).ToArray()),
            ["tasks"] = session.Practice is { } pr ? new JsonObject
            {
                ["index"] = pr.Progress.Index,
                ["done"] = new JsonArray(pr.Progress.Done.Select(b => (JsonNode)JsonValue.Create(b)).ToArray()),
                ["state"] = new JsonObject
                {
                    ["undoUsed"] = pr.Flags.UndoUsed, ["sphereAdded"] = pr.Flags.SphereAdded, ["extruded"] = pr.Flags.Extruded,
                    ["inset"] = pr.Flags.Inset, ["loopcut"] = pr.Flags.Loopcut, ["bevel"] = pr.Flags.Bevel,
                },
            } : null,
            ["events"] = new JsonArray(core.TakeEvents().Select(e =>
            {
                var o = new JsonObject { ["type"] = e.Type };
                foreach (var (k, v) in e.Detail) o[k] = EventValue(v);
                return (JsonNode)o;
            }).ToArray()),
        };
        if (prevented is { } pd) s["prevented"] = pd;
        return s;
    }

    public static JsonObject Probe(ViewportCore core, JsonElement p)
    {
        var view = core.View;
        var rect = core.Rect;
        double x = p.GetProperty("x").GetDouble(), y = p.GetProperty("y").GetDouble();
        var ndcX = (x - rect.Left) / rect.Width * 2 - 1;
        var ndcY = -((y - rect.Top) / rect.Height) * 2 + 1;
        var ray = view.RayFromNdc(ndcX, ndcY);
        var hit = Picking.Pick(ray, core.Objects);
        var (sx, sy) = view.ToScreen(Vec(p.GetProperty("world")), rect.Width, rect.Height);
        var at = core.PointAt(x, y);
        return new JsonObject
        {
            ["ndc"] = new JsonArray(N(ndcX), N(ndcY)), ["origin"] = Arr(ray.Origin), ["dir"] = Arr(ray.Direction),
            ["onPlane"] = Arr(view.UnprojectOnPlane(ndcX, ndcY, Vec(p.GetProperty("pivot")))),
            ["pick"] = hit?.Name, ["screen"] = new JsonArray(N(sx), N(sy)), ["wpp"] = N(view.WorldPerPixel(Vec(p.GetProperty("world")))),
            ["pointAt"] = at is { } pt ? Arr(pt) : null,
        };
    }
}
