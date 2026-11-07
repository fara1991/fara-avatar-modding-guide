using Xunit;

namespace FaraAvatarModdingGuide.Practice.Tests;

/// <summary>JavaScript と突き合わせられない(または突き合わせの外側の)編集モードの部品の単体テスト: 順序つきの集合・メッシュの出し入れ・操作後の形の健全さ・C# で決めた挙動。</summary>
public class EditMeshTests
{
    // ---------------------------------------------------------------- 順序つきの集合(JavaScript の Set と同じ)
    [Fact]
    public void OrderedSet_keeps_insertion_order_like_a_javascript_set()
    {
        var set = new OrderedSet<int> { };
        foreach (var x in new[] { 3, 1, 2 }) set.Add(x);
        set.Add(1);                                   // すでにあれば順番は変わらない
        Assert.Equal([3, 1, 2], set.ToList());
        set.Remove(3);
        set.Add(3);                                   // 消してから足すと最後
        Assert.Equal([1, 2, 3], set.ToList());
        Assert.Equal([5, 4, 6], new OrderedSet<int>([5, 4, 5, 6, 4]).ToList());   // new Set(配列) は最初の出現を残す
        var copy = set.ToList();
        set.Clear();
        Assert.Empty(set);
        Assert.Equal(3, copy.Count);                  // 繰り返しの途中で集合を変えても壊れないよう、写しを返す
    }

    [Fact]
    public void Splice_follows_array_prototype_splice_including_negative_start()
    {
        var a = new List<int> { 1, 2, 3, 4 };
        Js.Splice(a, 1, 2, [9]);
        Assert.Equal([1, 9, 4], a);
        Js.Splice(a, -1, 1, [7, 8]);                  // 負の start は末尾から数える
        Assert.Equal([1, 9, 7, 8], a);
        Js.Splice(a, 99, 5, [0]);                     // 範囲外は末尾
        Assert.Equal([1, 9, 7, 8, 0], a);
        Js.Splice(a, -99, 0, [5]);                    // 負の範囲外は先頭
        Assert.Equal(5, a[0]);
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("  -2", -2)]
    [InlineData("1e1", 10)]
    [InlineData("  3.5abc", 3.5)]
    [InlineData(".5", 0.5)]
    [InlineData("-.5", -0.5)]
    [InlineData("5.", 5)]
    public void ParseFloat_reads_the_leading_number(string text, double expected) => Assert.Equal(expected, Js.ParseFloat(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData(".")]
    public void ParseFloat_is_NaN_when_there_is_no_number(string text) => Assert.True(double.IsNaN(Js.ParseFloat(text)));

    // ---------------------------------------------------------------- メッシュの出し入れ
    [Theory]
    [InlineData("cube")]
    [InlineData("plane")]
    [InlineData("sphere")]
    [InlineData("cylinder")]
    [InlineData("cone")]
    [InlineData("torus")]
    public void FromData_and_ToData_round_trip_every_primitive(string kind)
    {
        var data = MeshShapes.Create(kind);
        var back = EditMesh.FromData(data).ToData();
        Assert.Equal(data.Verts, back.Verts);
        Assert.Equal(data.Faces.Select(f => string.Join(",", f)), back.Faces.Select(f => string.Join(",", f)));
        Assert.Empty(back.VSel);
    }

    [Fact]
    public void ToData_keeps_the_selection_in_the_order_it_was_made()
    {
        var em = EditMesh.FromData(MeshShapes.Create("cube"));
        em.Vsel.Add(em.Verts[5]); em.Vsel.Add(em.Verts[2]); em.Vsel.Add(em.Verts[7]);
        Assert.Equal([5, 2, 7], em.ToData().VSel);
        Assert.Equal([5, 2, 7], em.Clone().ToData().VSel);
    }

    // ---------------------------------------------------------------- 操作後の形が壊れていない(JavaScript と同じ結果であることとは別に、閉じた立体のままであること)
    private static (int V, int E, int F) Counts(EditMesh em) => (em.Verts.Count, em.EdgeMap().Count, em.Faces.Count);

    /// <summary>どの辺も面がちょうど 2 つで、有向辺(a → b)が 1 つの面にだけ現れる(向きがそろった閉じた多様体)。</summary>
    private static void AssertClosedManifold(EditMesh em)
    {
        em.Changed();
        Assert.All(em.Edges(), e => Assert.Equal(2, e.Faces.Count));
        var directed = new HashSet<(EmVert, EmVert)>();
        foreach (var f in em.Faces)
            for (var i = 0; i < f.V.Count; i++) Assert.True(directed.Add((f.V[i], f.V[(i + 1) % f.V.Count])), "同じ向きの辺が 2 回出る");
        var (v, e2, f2) = Counts(em);
        Assert.Equal(2, v - e2 + f2);   // オイラーの標数(球と同じ形)
    }

    private static EditMesh Cube() => EditMesh.FromData(MeshShapes.Create("cube"));

    [Fact]
    public void Extruding_a_face_region_of_a_cube_keeps_it_closed()
    {
        var em = Cube();
        var top = em.Faces.First(f => EditMesh.FaceNormal(f).Z > 0.9);
        em.Fsel.Add(top);
        em.Flush("face");
        var result = em.Extrude("face")!;
        Assert.Equal("region", result.Type);
        Assert.Equal(4, result.Verts.Count);
        Assert.Equal((12, 20, 10), Counts(em));
        AssertClosedManifold(em);
        Assert.Equal(Vec3.UnitZ, result.Normal);
    }

    [Fact]
    public void Insetting_then_loop_cutting_then_beveling_a_cube_keeps_it_closed()
    {
        var em = Cube();
        em.Fsel.Add(em.Faces.First(f => EditMesh.FaceNormal(f).Z > 0.9));
        em.Flush("face");
        var inset = em.Inset("face")!;
        Assert.Equal(4, inset.Moves.Count);
        AssertClosedManifold(em);

        em.Changed();
        var side = em.Edges().First(e => Math.Abs(e.A.Co.Z - e.B.Co.Z) > 1.5 && e.Faces.All(f => f.V.Count == 4));
        var cut = em.LoopCut(side, 2, "vert")!;
        Assert.Equal(2 * 4, cut.Verts.Count);   // 側面を 1 周する輪に 2 本
        AssertClosedManifold(em);

        em.SelectAll("edge", false);
        em.Changed();
        em.Esel.Add(em.Edges().First(e => e.Faces.Count == 2).Key);
        em.Flush("edge");
        var bevel = em.Bevel("edge");
        Assert.Null(bevel.Error);
        AssertClosedManifold(em);
    }

    [Fact]
    public void Bevel_reports_why_it_cannot_run()
    {
        var em = Cube();
        Assert.Equal("辺が選ばれていません", em.Bevel("edge").Error);
        var plane = EditMesh.FromData(MeshShapes.Create("plane"));
        plane.Esel.Add(plane.Edges()[0].Key);
        Assert.Equal("面が 2 つ接している辺だけベベルできます", plane.Bevel("edge").Error);
    }

    [Fact]
    public void Dissolving_an_edge_joins_the_two_faces_and_merging_by_distance_removes_duplicates()
    {
        var em = Cube();
        em.Esel.Add(em.Edges()[0].Key);
        em.Flush("edge");
        em.Remove("dissolve");
        // 隣り合う 2 面が 1 つになり、辺が 2 本だけ残った両端の頂点も消える(Blender の「辺を溶解」と同じ)
        Assert.Equal(5, em.Faces.Count);
        Assert.Equal(6, em.Verts.Count);
        Assert.Contains(em.Faces, f => f.V.Count == 4 && f.V.All(v => em.Verts.Contains(v)));

        var dup = Cube();
        dup.Fsel.Add(dup.Faces[1]); dup.Flush("face");
        dup.Extrude("face");               // 動かさない押し出し: 同じ位置に頂点が重なる
        dup.SelectAll("vert");
        var removed = dup.Merge("distance", Vec3.Zero);
        Assert.Equal(4, removed);
        Assert.Equal(8, dup.Verts.Count);
    }

    [Fact]
    public void Separate_moves_the_selected_faces_into_a_new_mesh()
    {
        var em = Cube();
        em.Fsel.Add(em.Faces[0]); em.Fsel.Add(em.Faces[1]);
        em.Flush("face");
        var part = em.Separate()!;
        Assert.Equal(2, part.Faces.Count);
        Assert.Equal(4, em.Faces.Count);
        Assert.Empty(em.Fsel);
    }

    // ---------------------------------------------------------------- ViewportCore: JavaScript と違う(または JavaScript が壊れる)所の、C# で決めた挙動
    [Fact]
    public void Extrude_event_arrives_with_the_type_in_place_of_the_event_type_like_viewport_js()
    {
        // 現行の viewport.js は { type, ...detail } でイベントを作るので、押し出しのイベントは type が "region" などで上書きされる(現行の不具合。挙動を写している)
        var core = new ViewportCore(new ViewRect(0, 0, 800, 600));
        core.SetSelection(core.Objects);
        core.KeyDown("Tab", "Tab", default);
        core.KeyDown("Digit3", "3", default);
        core.KeyDown("KeyA", "a", default);
        core.KeyDown("KeyE", "e", default);
        var types = core.TakeEvents().Select(e => e.Type).ToArray();
        Assert.Contains("region", types);
        Assert.DoesNotContain("extrude", types);
    }

    [Fact]
    public void Duplicate_that_cannot_start_the_move_keeps_the_copy_and_does_not_throw()
    {
        // 現行は、複製の直後の移動が「ほかの人が操作中」で始められないと TypeError(op が null)になり、複製の取り消しの記録も消える。
        // 複製は新しいオブジェクトを選び直すので、実際の部屋では起きないが、C# は例外にせず、複製と取り消しの記録を残す
        var core = new ViewportCore(new ViewRect(0, 0, 800, 600), new ViewportOptions { CanTransform = _ => "ロック中" });
        core.SetSelection(core.Objects);
        core.KeyDown("KeyD", "D", new Modifiers(Shift: true));
        Assert.Equal(2, core.Objects.Count);
        Assert.Null(core.Op);
        Assert.Equal(1, core.UndoDepth);
    }

    [Fact]
    public void Room_ids_default_to_eight_lowercase_alphanumerics_and_are_all_different()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 800, 600));
        for (var i = 0; i < 20; i++) core.KeyDown("KeyA", "A", new Modifiers(Shift: true));
        var ids = core.RoomState().Select(r => r.Id).ToList();
        Assert.Single(ids);   // メニューを開いただけ(項目を選んでいない)
        Assert.Matches("^[0-9a-z]{8}$", ids[0]);
        core.ChooseMenu(1); core.KeyDown("KeyA", "A", new Modifiers(Shift: true)); core.ChooseMenu(2);
        var ids2 = core.RoomState().Select(r => r.Id).ToList();
        Assert.Equal(ids2.Count, ids2.Distinct().Count());
    }

    [Fact]
    public void The_practice_session_ticks_tasks_off_from_events_and_resets()
    {
        var core = new ViewportCore(new ViewRect(0, 0, 800, 600));
        var session = new PracticeSession(core);
        Assert.Equal(0, session.Progress.Index);
        core.SetSelection(core.Objects);           // 立方体を選択
        core.KeyDown("KeyG", "g", default);        // イベントのたびに判定する
        Assert.Equal(1, session.Progress.Index);   // 「立方体をクリックして選択する」
        core.KeyDown("Escape", "Escape", default);
        session.Reset();
        Assert.Equal(0, session.Progress.Index);
        Assert.False(session.Flags.UndoUsed);
    }

    [Fact]
    public void Practice_one_operations_of_the_browser_check_give_the_baseline_result()
    {
        // tools/verify/practice.mjs が /practice で行う操作(立方体を選ぶ → G Z 2 → R Z 45 → S 2 → UV 球を追加 → 球を選んで G X 3 → Ctrl+Z → Numpad1 → 模倣をオンにして 5)を
        // C# の ViewportCore に通した結果が、基準(tools/verify/practice-baseline.json。SSR 版と一致したときの値)と同じになる。ブラウザを使わずに確かめられる範囲の、S1 の完了条件
        var baseline = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(JsReference.Root, "tools", "verify", "practice-baseline.json"))).RootElement.GetProperty("practice");
        var emulate = false;
        var core = new ViewportCore(new ViewRect(37, 61, 1280, 700), new ViewportOptions { EmulateNumpad = () => emulate });
        var session = new PracticeSession(core);
        var center = core.View.ToScreen(Vec3.Zero, 1280, 700);
        void Type(params string[] codes) { foreach (var c in codes) core.KeyDown(c.Length == 1 ? "Key" + c.ToUpperInvariant() : c, c, default); }
        core.PointerMove(37 + center.X, 61 + center.Y - 12, default);
        core.PointerDown(0, 37 + center.X, 61 + center.Y - 12, default); core.PointerUp(37 + center.X, 61 + center.Y - 12);
        Type("g", "z"); core.KeyDown("Digit2", "2", default); Type("Enter");
        Type("r", "z"); core.KeyDown("Digit4", "4", default); core.KeyDown("Digit5", "5", default); Type("Enter");
        Type("s"); core.KeyDown("Digit2", "2", default); Type("Enter");
        core.KeyDown("KeyA", "A", new Modifiers(Shift: true)); core.ChooseMenu(2);
        core.SetSelection(core.Objects.Where(o => o.Kind == "sphere"));
        Type("g", "x"); core.KeyDown("Digit3", "3", default); Type("Enter");
        core.KeyDown("KeyZ", "z", new Modifiers(Ctrl: true));
        core.KeyDown("Numpad1", "1", default);
        emulate = true;
        core.KeyDown("Digit5", "5", default);

        double R(double n) => Math.Round(n * 100, MidpointRounding.AwayFromZero) / 100;   // practice.mjs の r: Math.round(n * 100) / 100
        var expected = baseline.GetProperty("objects").EnumerateArray().ToList();
        Assert.Equal(expected.Count, core.Objects.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var o = core.Objects[i];
            Assert.Equal(expected[i].GetProperty("kind").GetString(), o.Kind);
            Assert.Equal(expected[i].GetProperty("p").EnumerateArray().Select(x => x.GetDouble()), new[] { R(o.Position.X), R(o.Position.Y), R(o.Position.Z) });
            Assert.Equal(expected[i].GetProperty("r").EnumerateArray().Select(x => x.GetDouble()), new[] { R(o.Rotation.X), R(o.Rotation.Y), R(o.Rotation.Z) });
            Assert.Equal(expected[i].GetProperty("s").EnumerateArray().Select(x => x.GetDouble()), new[] { R(o.Scale.X), R(o.Scale.Y), R(o.Scale.Z) });
        }
        Assert.Equal(baseline.GetProperty("mode").GetString(), core.Mode);
        Assert.Equal(baseline.GetProperty("ortho").GetBoolean(), core.View.Ortho);
        Assert.Equal(baseline.GetProperty("view").GetString(), core.View.Name);
        Assert.Equal(baseline.GetProperty("done").GetInt32(), session.Progress.Done.Count(d => d));
        Assert.Equal(baseline.GetProperty("current").GetString(), PracticeTasks.Tasks[session.Progress.Index].Text);
        Assert.Equal(baseline.GetProperty("tasks").GetInt32(), PracticeTasks.Tasks.Count);
    }

    [Fact]
    public void Camera_matrices_are_a_rigid_transform_and_projection_round_trips()
    {
        var view = new OrbitView { Aspect = 1.7, Theta = 1.1, Phi = 0.9, Dist = 8, Target = new Vec3(1, -2, 0.5) };
        var (right, up, back) = view.Basis;
        Assert.Equal(1, right.Length, 12);
        Assert.Equal(0, right.Dot(up), 12);
        Assert.Equal(0, up.Dot(back), 12);
        var p = new Vec3(0.3, 0.2, 0.4);
        foreach (var ortho in new[] { false, true })
        {
            view.Ortho = ortho;
            var (nx, ny) = view.ProjectToNdc(p);
            var ray = view.RayFromNdc(nx, ny);
            // レイは p を通る(平行投影でも透視でも)
            var along = (p - ray.Origin).Dot(ray.Direction);
            Assert.True((ray.At(along) - p).Length < 1e-9, $"ortho={ortho}");
        }
    }
}
