using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>
/// 現行の unity.js(Node で本物のまま動かした結果。tests/js/unity-reference.mjs)。入力は tests/js/unity-cases.json(C# も同じものを読む)。Node が無いと失敗する(飛ばさない)。
/// </summary>
internal static class UnityReference
{
    private static readonly Lazy<JsonElement> output = new(Run);
    private static readonly Lazy<JsonElement> cases = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(JsReference.JsDir, "unity-cases.json"), Encoding.UTF8)).RootElement);

    public static JsonElement Output => output.Value;
    public static JsonElement Cases => cases.Value;

    private static JsonElement Run()
    {
        for (var attempt = 1; ; attempt++)
        {
            var (ok, error, result) = RunOnce();
            if (ok) return result;
            if (attempt == 3) throw new Xunit.Sdk.XunitException(error);
            Thread.Sleep(500);
        }
    }

    private static (bool Ok, string Error, JsonElement Result) RunOnce()
    {
        var work = Path.Combine(Path.GetTempPath(), "avatar-unity-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            var outFile = Path.Combine(work, "unity.json");
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            info.ArgumentList.Add("--max-old-space-size=4096");
            foreach (var arg in new[] { Path.Combine(JsReference.JsDir, "unity-reference.mjs"), Path.Combine(JsReference.JsDir, "unity-cases.json"), outFile }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("node を起動できません(CI では actions/setup-node が要ります)");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) return (false, $"unity-reference.mjs が失敗しました(終了コード {process.ExitCode}):" + Environment.NewLine + stderr.Result + stdout.Result, default);
            return (true, "", JsonDocument.Parse(File.ReadAllText(outFile, Encoding.UTF8)).RootElement.Clone());
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }
}

/// <summary>仮想の時計(unity-reference.mjs の setTimeout / performance.now と同じ式)。</summary>
internal sealed class FakeClock
{
    private readonly List<(int Id, double At, Action Fn)> timers = [];
    private int nextId = 1;
    public double Now { get; private set; }

    public IDisposable Schedule(int ms, Action fn)
    {
        var id = nextId++;
        timers.Add((id, Now + ms, fn));
        return new Cancel(this, id);
    }

    private sealed class Cancel(FakeClock clock, int id) : IDisposable
    {
        public void Dispose() => clock.timers.RemoveAll(t => t.Id == id);
    }

    public void Advance(double ms)
    {
        var end = Now + ms;
        for (;;)
        {
            var due = timers.Where(t => t.At <= end).OrderBy(t => t.At).ThenBy(t => t.Id).Cast<(int Id, double At, Action Fn)?>().FirstOrDefault();
            if (due is null) break;
            timers.RemoveAll(t => t.Id == due.Value.Id);
            Now = Math.Max(Now, due.Value.At);
            due.Value.Fn();
        }
        Now = end;
    }
}

/// <summary>
/// 突き合わせの入力(tests/js/unity-cases.json の 1 ケース)を UnityEditorCore に通し、unity-reference.mjs と同じ形の状態(JSON)を作る。
/// JavaScript 側の createHarness・apply・snapshot と対になっている。
/// </summary>
internal sealed class UnityDriver
{
    private readonly UnityEditorCore core;
    private readonly UnityPracticeSession? practice;
    private readonly FakeClock clock = new();
    private readonly List<JsonObject> events = [];

    public UnityDriver(JsonElement c)
    {
        var instances = c.TryGetProperty("instances", out var inst) ? inst.EnumerateArray().Select(x => x.GetString()!).ToArray() : ["Avatar"];
        core = new UnityEditorCore(new UnityOptions
        {
            Instances = instances,
            WheelNeedsFocus = c.TryGetProperty("wheelNeedsFocus", out var w) && w.GetBoolean(),
            Schedule = clock.Schedule,
            Now = () => clock.Now,
        });
        core.EventEmitted += e => events.Add(Clean(e));
        // unity.js のコンストラクタは、最後に animate() を 1 回実行する(画面の大きさが分かる前)
        core.Animate(clock.Now);
        // お題つき(unity-practice.js の UnityPractice)は、エディターを作ってから reset() する
        if (c.TryGetProperty("practice", out var pr) && pr.GetBoolean()) practice = new UnityPracticeSession(core);
        var rect = c.TryGetProperty("rect", out var r) ? r : default;
        var client = c.TryGetProperty("client", out var cl) ? cl : default;
        SetRect(rect.ValueKind == JsonValueKind.Object ? rect : JsonDocument.Parse("""{"left":100,"top":50,"width":711.5,"height":400.25}""").RootElement,
            client.ValueKind == JsonValueKind.Object ? client : JsonDocument.Parse("""{"w":711,"h":400}""").RootElement);
        Detail = c.TryGetProperty("detail", out var d) && d.GetString() == "light" ? "light" : "full";
    }

    public string Detail { get; }

    public UnityEditorCore Core => core;

    /// <summary>今の状態(tests/js/unity-reference.mjs の snapshot(h, false) と同じ形)。</summary>
    public JsonObject SnapshotLight() => Snapshot(false);

    private ViewRect rect;

    private void SetRect(JsonElement r, JsonElement client)
    {
        rect = new ViewRect(r.GetProperty("left").GetDouble(), r.GetProperty("top").GetDouble(), r.GetProperty("width").GetDouble(), r.GetProperty("height").GetDouble());
        // ビューの座標は左上が原点(gl.js が渡す形)。JS は clientX を使うので、操作を渡すときに rect.Left / Top を引く(JS 側は足している)
        core.Resize(new ViewRect(0, 0, rect.Width, rect.Height), client.GetProperty("w").GetDouble(), client.GetProperty("h").GetDouble());
    }

    private static JsonObject Clean(ViewEvent e)
    {
        var o = new JsonObject { ["type"] = e.Type };
        foreach (var (k, v) in e.Detail)
        {
            if (k is "id" or "parent") continue;
            o[k] = v switch
            {
                null => null,
                string s => s,
                bool b => b,
                int i => i,
                double d => d == 0 ? 0 : d,
                _ => throw new InvalidOperationException($"{e.Type}.{k}: 未対応の型 {v.GetType()}"),
            };
        }
        return o;
    }

    public JsonArray Run(JsonElement ops)
    {
        var steps = new JsonArray { new JsonObject { ["op"] = new JsonObject { ["k"] = "init" }, ["snapshot"] = Snapshot(true), ["events"] = new JsonArray() } };
        foreach (var op in ops.EnumerateArray())
        {
            events.Clear();
            probe = null;
            bool? prevented = null;
            Apply(op, ref prevented);
            var full = Detail == "full" || (op.TryGetProperty("full", out var f) && f.GetBoolean());
            steps.Add(new JsonObject
            {
                ["op"] = JsonNode.Parse(op.GetRawText()),
                ["snapshot"] = Snapshot(full),
                ["events"] = new JsonArray([.. events.Select(e => (JsonNode?)e.DeepClone())]),
                ["prevented"] = prevented == true ? true : null,
                ["probe"] = probe,
            });
        }
        return steps;
    }

    private JsonNode? probe;

    private UnityNode? Find(string? path) => path is null ? null : core.FindByName(path);

    private static string S(JsonElement op, string key) => op.GetProperty(key).GetString()!;

    private static string? OptS(JsonElement op, string key) => op.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double D(JsonElement op, string key, double def = 0) => op.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;

    private static bool B(JsonElement op, string key) => op.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private void Apply(JsonElement op, ref bool? prevented)
    {
        var k = S(op, "k");
        var path = OptS(op, "path");
        var n = path is not null ? Find(path) : null;
        var need = path is not null && n is null;
        switch (k)
        {
            case "select": if (!need) core.Select(n!.Id); break;
            case "selectNone": core.Select(null); break;
            case "expand": if (!need) core.SetExpanded(n!.Id, B(op, "on")); break;
            case "instantiate":
            {
                var parentPath = OptS(op, "parent");
                var parent = parentPath is not null ? Find(parentPath) : null;
                if (parentPath is not null && parent is null) break;
                core.Instantiate(S(op, "asset"), parent?.Id);
                break;
            }
            case "reparent":
            {
                var parentPath = OptS(op, "parent");
                var parent = parentPath is not null ? Find(parentPath) : null;
                if (need || (parentPath is not null && parent is null)) break;
                core.Reparent(n!.Id, parent?.Id);
                break;
            }
            case "delete": if (!need) core.DeleteNode(n!.Id); break;
            case "active": if (!need) core.SetActive(n!.Id, B(op, "on")); break;
            case "tag": if (!need) core.SetTag(n!.Id, S(op, "tag")); break;
            case "material": if (!need) core.AssignMaterial(n!.Id, (int)D(op, "slot"), S(op, "material")); break;
            case "addComponent": if (!need) core.AddComponent(n!.Id, S(op, "type")); break;
            case "setupOutfit": if (!need) core.SetupOutfit(n!.Id); break;
            case "tool": core.SetTool(S(op, "tool")); break;
            case "edit":
                if (!need) core.SetTransformValue(n!.Id, S(op, "prop"), (int)D(op, "axis"), D(op, "value", double.NaN), B(op, "fromInput"));
                break;
            case "frameSel": core.FrameSelected(); break;
            case "orbit": core.OrbitBy(D(op, "dyaw"), D(op, "dpitch")); break;
            case "openSdk": core.OpenSdk(); break;
            case "closeSdk": core.CloseSdk(B(op, "quiet")); break;
            case "build": core.Build(S(op, "kind")); break;
            case "advance": clock.Advance(D(op, "ms")); break;
            case "undo": core.Undo(); break;
            case "reset": if (practice is not null) practice.Reset(); else core.ResetScene(); break;
            case "key":
                if (core.KeyDown(S(op, "code"), OptS(op, "key") ?? "", new Modifiers(B(op, "shift"), B(op, "ctrl"), B(op, "alt"), B(op, "meta")))) prevented = true;
                break;
            case "ctxByName": core.ContextMenuByName(path!); break;
            case "ctxItem":
                if (core.Popup is { Kind: "context" }) core.ContextItem(S(op, "item"));
                break;
            case "openAdd": core.OpenAddComponent(); break;
            case "addSearch": core.SetAddSearch(S(op, "text")); break;
            case "addEnter": if (core.Popup is { Kind: "add" }) core.AddFirstFromSearch(); break;
            case "addEscape": if (core.Popup is { Kind: "add" }) core.ClosePopup(); break;
            case "openMenu": core.OpenMenu(S(op, "name"), new UnityAnchor("point", 10, 20)); break;
            case "closePopup": core.ClosePopup(); break;
            case "dropAsset":
            {
                var t = op.GetProperty("target");
                var kind = S(t, "kind");
                string? targetId = null;
                if (kind == "node") { targetId = Find(OptS(t, "path"))?.Id; if (targetId is null) break; }
                core.DropAsset(S(op, "asset"), kind, targetId, (int)D(t, "slot"), D(op, "x"), D(op, "y"));
                break;
            }
            case "dropNode":
            {
                if (need) break;
                var t = op.GetProperty("target");
                var kind = S(t, "kind");
                string? targetId = null;
                if (kind == "node") { targetId = Find(OptS(t, "path"))?.Id; if (targetId is null) break; }
                core.DropNode(n!.Id, kind, targetId);
                break;
            }
            case "clearSelection": core.ClearSelection(); break;
            case "dblclick": if (!need) core.DoubleClickNode(n!.Id); break;
            case "clickAsset": core.ClickAsset(S(op, "asset")); break;
            case "sceneDown":
                if (core.ScenePointerDown((int)D(op, "button"), new Modifiers(Alt: B(op, "alt")), D(op, "x"), D(op, "y"))) prevented = true;
                break;
            case "sceneMove": core.ScenePointerMove(D(op, "x"), D(op, "y")); break;
            case "sceneUp": core.ScenePointerUp(D(op, "x"), D(op, "y")); break;
            case "wheel":
                core.Focused = B(op, "focused");
                if (core.SceneWheel(D(op, "deltaY"))) prevented = true;
                break;
            case "viewDistMul": core.View.Dist *= D(op, "f"); break;
            case "viewTargetX": core.View.Target = core.View.Target with { X = core.View.Target.X + D(op, "dx") }; break;
            case "frame":
                clock.Advance(D(op, "dt", 16));
                core.Animate(clock.Now);
                break;
            case "resize": SetRect(op.GetProperty("rect"), op.GetProperty("client")); break;
            case "pick":
            {
                var hd = core.PickGizmoHandle(D(op, "x"), D(op, "y"));
                var mesh = core.PickMesh(D(op, "x"), D(op, "y"));
                probe = new JsonObject
                {
                    ["mesh"] = mesh is null ? null : PathOf(core.Get(mesh)),
                    ["handle"] = hd is null ? null : new JsonArray(hd.Value.Kind, hd.Value.Axis),
                };
                break;
            }
            default: throw new InvalidOperationException($"未知の操作 {k}");
        }
    }

    // ---------------------------------------------------------------- 状態の読み取り
    private string? PathOf(UnityNode? n) => n is null ? null : string.Join("/", core.Ancestors(n).AsEnumerable().Reverse().Select(a => a.Name).Append(n.Name));

    private static double Num(double v) => v == 0 ? 0 : v;

    private static JsonArray Arr(IEnumerable<double> v) => new([.. v.Select(x => (JsonNode?)Num(x))]);

    private static JsonArray Vec(Vec3 v) => Arr([v.X, v.Y, v.Z]);

    private static JsonArray Mat(Mat4 m) => Arr(m.E);

    private static JsonObject ObjSnap(Obj3D o) => new()
    {
        ["position"] = Vec(o.Position),
        ["quaternion"] = Arr([o.Quaternion.X, o.Quaternion.Y, o.Quaternion.Z, o.Quaternion.W]),
        ["scale"] = Vec(o.Scale),
        ["world"] = Mat(o.MatrixWorld),
    };

    private JsonObject CompSnap(UnityComponent c)
    {
        var o = new JsonObject { ["type"] = c.Type };
        if (c.Materials is not null) o["materials"] = new JsonArray([.. c.Materials.Select(m => (JsonNode?)m)]);
        if (c.Blendshapes is not null) o["blendshapes"] = new JsonArray([.. c.Blendshapes.Select(m => (JsonNode?)m)]);
        if (c.Type == "SkinnedMeshRenderer") o["tris"] = c.Tris;
        if (c.Avatar is not null) o["avatar"] = c.Avatar;
        if (c.View is not null) o["view"] = Arr(c.View);
        if (c.BlueprintId is not null) o["blueprintId"] = c.BlueprintId;
        if (c.Target is not null) o["target"] = c.Target;
        if (c.TargetId is not null) o["targetPath"] = PathOf(core.Get(c.TargetId));
        if (c.Prefix is not null) o["prefix"] = c.Prefix;
        if (c.Suffix is not null) o["suffix"] = c.Suffix;
        if (c.Bindings is not null) o["bindings"] = new JsonArray([.. c.Bindings.Select(b => (JsonNode?)new JsonArray(b.From, b.Blendshape))]);
        if (c.Root is not null) { o["root"] = c.Root; o["pull"] = c.Pull; o["spring"] = c.Spring; }
        return o;
    }

    private static string GeometryType(string shape) => shape switch { "capsule" => "CapsuleGeometry", "box" => "BoxGeometry", _ => "SphereGeometry" };

    private JsonObject Snapshot(bool full)
    {
        var nodes = new JsonArray();
        foreach (var n in core.Nodes)
        {
            var o = new JsonObject
            {
                ["path"] = PathOf(n),
                ["active"] = n.Active,
                ["tag"] = n.Tag,
                ["expanded"] = n.Expanded,
                ["p"] = Arr(n.P),
                ["r"] = Arr(n.R),
                ["s"] = Arr(n.S),
                ["prefab"] = n.Prefab,
                ["mesh"] = n.Mesh,
                ["icon"] = n.Icon,
                ["bone"] = n.Bone,
                ["children"] = new JsonArray([.. n.Children.Select(id => (JsonNode?)core.Get(id)!.Name)]),
                ["comps"] = new JsonArray([.. n.Components.Select(c => (JsonNode?)CompSnap(c))]),
            };
            if (full) o["obj"] = n.Obj is null ? null : ObjSnap(n.Obj);
            var parts = new JsonArray();
            foreach (var p in n.Parts ?? [])
            {
                var po = new JsonObject
                {
                    ["geometry"] = GeometryType(p.Shape),
                    ["vertices"] = p.Geometry.VertexCount,
                    ["indices"] = p.Geometry.Indices.Length,
                    ["visible"] = p.Mesh.Visible,
                    ["color"] = p.Color,
                    ["doubleSided"] = p.DoubleSided,
                };
                if (full) po["world"] = Mat(p.Mesh.MatrixWorld);
                parts.Add(po);
            }
            o["parts"] = parts;
            nodes.Add(o);
        }
        var gizmo = new JsonObject { ["visible"] = core.GizmoRoot.Visible };
        if (full) { gizmo["position"] = Vec(core.GizmoRoot.Position); gizmo["scale"] = Vec(core.GizmoRoot.Scale); }
        var items = new JsonArray();
        var handles = new JsonArray();
        foreach (var it in core.GizmoItems)
        {
            var io = new JsonObject
            {
                ["geometry"] = it.GeometryType,
                ["vertices"] = it.Geometry.VertexCount,
                ["indices"] = it.Geometry.Indices.Length,
                ["visible"] = !it.Hit,
                ["color"] = it.Color,
            };
            if (full) io["world"] = Mat(it.Obj.MatrixWorld);
            items.Add(io);
            if (it.Hit)
            {
                var ho = new JsonObject { ["handle"] = new JsonObject { ["kind"] = it.HandleKind, ["axis"] = it.HandleAxis } };
                if (full) ho["world"] = Mat(it.Obj.MatrixWorld);
                handles.Add(ho);
            }
        }
        gizmo["items"] = items;
        gizmo["handles"] = handles;
        var snap = new JsonObject
        {
            ["nodes"] = nodes,
            ["roots"] = new JsonArray([.. core.Roots.Select(id => (JsonNode?)core.Get(id)!.Name)]),
            ["selected"] = PathOf(core.Get(core.Selected)),
            ["tool"] = core.Tool,
            ["status"] = core.Status,
            ["info"] = core.InfoText,
            ["popup"] = PopupSnap(),
            ["sdkOpen"] = core.SdkOpen,
            ["preview"] = core.Preview is null ? null : new JsonObject { ["text"] = core.Preview.Text, ["busy"] = core.Preview.Busy },
            ["posing"] = core.Posing,
            ["undo"] = core.UndoDepth,
            ["view"] = new JsonObject { ["target"] = Vec(core.View.Target), ["dist"] = core.View.Dist, ["yaw"] = core.View.Yaw, ["pitch"] = core.View.Pitch },
            ["rows"] = new JsonArray([.. core.Rows().Select(r => (JsonNode?)new JsonObject
            {
                ["name"] = r.Name, ["depth"] = r.Depth, ["hasKids"] = r.HasKids, ["expanded"] = r.HasKids && r.Expanded, ["selected"] = r.Selected, ["inactive"] = r.Inactive, ["prefab"] = r.Prefab, ["icon"] = r.Icon, ["editorOnly"] = r.EditorOnly,
            })]),
            ["inspector"] = InspectorSnap(),
            ["stats"] = StatsSnap(),
            ["gizmo"] = gizmo,
        };
        if (practice is not null) snap["tasks"] = new JsonObject { ["index"] = practice.Index, ["sdk"] = practice.State.Sdk, ["builtMerged"] = practice.State.BuiltMerged };
        if (full)
        {
            snap["camera"] = new JsonObject { ["position"] = Vec(core.Camera.Position), ["world"] = Mat(core.Camera.MatrixWorld), ["projection"] = Mat(core.Camera.Projection) };
        }
        return snap;
    }

    private JsonNode? PopupSnap()
    {
        var p = core.Popup;
        if (p is null) return null;
        if (p.Kind == "add") return new JsonObject { ["kind"] = "add", ["list"] = new JsonArray([.. core.AddList().Select(c => (JsonNode?)c.Type)]), ["search"] = p.Search };
        if (p.Kind == "context") return new JsonObject { ["kind"] = "context", ["title"] = core.Get(p.NodeId)!.Name, ["node"] = PathOf(core.Get(p.NodeId)) };
        return new JsonObject { ["kind"] = "menu", ["menu"] = p.MenuName == "VRChat SDK" ? "VRChat SDK" : "other" };
    }

    private JsonNode? InspectorSnap()
    {
        var ins = core.Inspector();
        if (ins is null) return null;
        var fields = new JsonArray();
        void F(string label, string? cls, string? text) => fields.Add(new JsonArray(label, cls, text));
        F("Tag", null, null);
        F("Position", null, null);
        F("Rotation", null, null);
        F("Scale", null, null);
        var notes = new JsonArray();
        var slots = new JsonArray();
        var slotColors = new JsonArray();
        foreach (var c in ins.Components)
        {
            foreach (var f in c.Fields)
            {
                switch (f.Kind)
                {
                    case "muted": F(f.Label, "ue-muted", f.Text); break;
                    case "obj": F(f.Label, "ue-objfield", f.Text); break;
                    case "slot": F(f.Label, "ue-objfield", f.Text); slots.Add(new JsonArray(f.Slot, f.Hl)); slotColors.Add(f.Color); break;
                    case "note": notes.Add(f.Text); break;
                }
            }
        }
        return new JsonObject
        {
            ["active"] = ins.Active,
            ["name"] = ins.Name,
            ["tag"] = ins.Tag,
            ["values"] = new JsonArray([.. ins.P.Concat(ins.R).Concat(ins.S).Select(v => (JsonNode?)v)]),
            ["inputs"] = new JsonArray("p0", "p1", "p2", "r0", "r1", "r2", "s0", "s1", "s2"),
            ["comps"] = new JsonArray([.. new[] { "Transform" }.Concat(ins.Components.Select(c => c.Type)).Select(t => (JsonNode?)t)]),
            ["fields"] = fields,
            ["notes"] = notes,
            ["slots"] = slots,
            ["slotColors"] = slotColors,
        };
    }

    private JsonNode? StatsSnap()
    {
        var st = core.Stats();
        if (st is null) return null;
        return new JsonObject
        {
            ["avatar"] = st.Avatar.Name,
            ["values"] = new JsonObject([.. st.Values.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))]),
            ["ranks"] = new JsonObject([.. st.Ranks.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))]),
            ["overall"] = st.Overall,
            ["notes"] = new JsonArray([.. st.Notes.Select(x => (JsonNode?)new JsonArray(x.Level, x.Text))]),
            ["merged"] = new JsonArray([.. st.Merged.Select(a => (JsonNode?)PathOf(a))]),
        };
    }
}
