using System.Globalization;

namespace FaraAvatarModdingGuide.Practice;

/// <summary>Hierarchy の 1 行(unity.js の renderHierarchy が作る行)。Icon は Bootstrap Icons のクラス。</summary>
public sealed record UnityRow(string Id, string Name, int Depth, bool HasKids, bool Expanded, bool Selected, bool Inactive, bool Prefab, string Icon, bool EditorOnly);

/// <summary>Inspector のコンポーネントの 1 項目。Kind: "muted"(文字)・"obj"(オブジェクトの欄)・"slot"(マテリアルの欄。ドラッグで設定)・"note"(1 行の注釈)。</summary>
public sealed record UnityField(string Kind, string Label, string Text, string? Icon = null, string? Color = null, int Slot = -1, string? Hl = null);

/// <summary>Inspector のコンポーネント 1 つ(見出しと、中身の項目)。</summary>
public sealed record UnityCompView(string Type, string Label, IReadOnlyList<UnityField> Fields);

/// <summary>Inspector(選んでいるノード)。P・R・S は入力欄の文字(JavaScript の数の文字と同じ書き方)。</summary>
public sealed record UnityInspector(string NodeId, string Name, bool Active, string Tag, IReadOnlyList<string> P, IReadOnlyList<string> R, IReadOnlyList<string> S, IReadOnlyList<UnityCompView> Components);

/// <summary>VRChat SDK の画面(Builder)。</summary>
public sealed record UnitySdkModel(string AvatarName, IReadOnlyList<(string Level, string Text)> Notes, int Overall, IReadOnlyList<(string Label, string Value, int Rank)> Rows);

/// <summary>JavaScript の数の文字(`${x}`)と同じ書き方。</summary>
public static class JsText
{
    public static string Num(double v)
    {
        if (v == 0) return "0";
        if (double.IsNaN(v)) return "NaN";
        if (double.IsInfinity(v)) return v > 0 ? "Infinity" : "-Infinity";
        var r = v.ToString("R", CultureInfo.InvariantCulture);
        if (!r.Contains('E')) return r;
        var at = r.IndexOf('E');
        var exp = int.Parse(r[(at + 1)..], CultureInfo.InvariantCulture);
        // JavaScript は、指数が 21 以上・-7 以下のときだけ指数の形(1e+21・1e-7)。それ以外は普通の書き方
        if (exp >= 21 || exp <= -7) return $"{r[..at]}e{(exp >= 0 ? "+" : "-")}{Math.Abs(exp)}";
        return decimal.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>toLocaleString()(桁区切りのカンマ)。</summary>
    public static string Locale(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed partial class UnityEditorCore
{
    /// <summary>Hierarchy に出る行(開いているノードの子だけ。unity.js の renderHierarchy の walk と同じ順)。</summary>
    public IReadOnlyList<UnityRow> Rows()
    {
        var rows = new List<UnityRow>();
        void Walk(string id, int depth)
        {
            var n = Get(id)!;
            var hasKids = n.Children.Count > 0;
            var icon = n.Icon == "camera" ? "bi-camera-video" : n.Icon == "light" ? "bi-sun" : n.Prefab is not null ? "bi-box-fill" : n.Mesh is not null ? "bi-person" : n.Bone ? "bi-dot" : "bi-box";
            rows.Add(new UnityRow(n.Id, n.Name, depth, hasKids, n.Expanded, n.Id == Selected, !IsEffectivelyActive(n), n.Prefab is not null, icon, n.Tag == "EditorOnly"));
            if (hasKids && n.Expanded) foreach (var c in n.Children) Walk(c, depth + 1);
        }
        foreach (var id in roots) Walk(id, 0);
        return rows;
    }

    /// <summary>Inspector(選んでいるノードが無ければ null)。</summary>
    public UnityInspector? Inspector()
    {
        var n = Get(Selected);
        if (n is null) return null;
        string Color(string m) => $"#{(UnityData.Asset(m)?.Color ?? 0xff00ff):x6}";
        var comps = new List<UnityCompView>();
        foreach (var c in n.Components)
        {
            var fields = new List<UnityField>();
            switch (c.Type)
            {
                case "SkinnedMeshRenderer":
                    fields.Add(new("muted", "Materials", $"Size {c.Materials!.Count}"));
                    for (var i = 0; i < c.Materials.Count; i++) fields.Add(new("slot", $"Element {i}", c.Materials[i], "bi-circle-fill", Color(c.Materials[i]), i, $"material-slot-{i}"));
                    fields.Add(new("muted", "BlendShapes", c.Blendshapes!.Count > 0 ? string.Join(", ", c.Blendshapes) : "なし"));
                    fields.Add(new("muted", "Triangles", JsText.Locale(c.Tris)));
                    break;
                case "VRCAvatarDescriptor":
                    fields.Add(new("muted", "View Position", string.Join(", ", c.View!.Select(JsText.Num))));
                    fields.Add(new("muted", "Lip Sync", "Viseme Blend Shape"));
                    break;
                case "PipelineManager":
                    fields.Add(new("muted", "Blueprint ID", string.IsNullOrEmpty(c.BlueprintId) ? "（未アップロード）" : c.BlueprintId));
                    break;
                case "MAMergeArmature":
                    fields.Add(new("obj", "Merge Target", c.Target ?? "None", "bi-diagram-3"));
                    fields.Add(new("muted", "Prefix", string.IsNullOrEmpty(c.Prefix) ? "（なし）" : c.Prefix));
                    fields.Add(new("muted", "Suffix", string.IsNullOrEmpty(c.Suffix) ? "（なし）" : c.Suffix));
                    break;
                case "MABlendshapeSync":
                    if (c.Bindings is { Count: > 0 }) foreach (var (from, bs) in c.Bindings) fields.Add(new("muted", from, bs));
                    else fields.Add(new("note", "", "同期するブレンドシェイプを追加してください"));
                    break;
                case "VRCPhysBone":
                    fields.Add(new("obj", "Root Transform", c.Root ?? ""));
                    fields.Add(new("muted", "Pull / Spring", $"{JsText.Num(c.Pull)} / {JsText.Num(c.Spring)}"));
                    break;
                case "Animator":
                    fields.Add(new("obj", "Avatar", c.Avatar ?? ""));
                    break;
            }
            comps.Add(new UnityCompView(c.Type, UnityData.ComponentLabel(c.Type), fields));
        }
        return new UnityInspector(n.Id, n.Name, n.Active, n.Tag, [.. n.P.Select(JsText.Num)], [.. n.R.Select(JsText.Num)], [.. n.S.Select(JsText.Num)], comps);
    }

    /// <summary>VRChat SDK の画面(開いていなくても作れる。アバターが無ければ null)。</summary>
    public UnitySdkModel? SdkModel()
    {
        var st = Stats();
        if (st is null) return null;
        var rows = new (string Label, string Key)[] { ("Triangles", "tris"), ("Material Slots", "materials"), ("Skinned Meshes", "skinned"), ("Bones", "bones"), ("PhysBone Components", "physbones") };
        return new UnitySdkModel(st.Avatar.Name, st.Notes, st.Overall, [.. rows.Select(r => (r.Label, JsText.Locale(st.Values[r.Key]), st.Ranks[r.Key]))]);
    }
}
