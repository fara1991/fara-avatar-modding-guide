namespace FaraAvatarModdingGuide.Practice;

/// <summary>Project ウィンドウのアセット(unity.js の ASSETS)。</summary>
public sealed record UnityAsset(string Key, string Type, string Label, int? Color = null, string? Shader = null, string? Note = null, bool Hidden = false);

/// <summary>「Add Component」で追加できるもの(unity.js の COMPONENTS)。</summary>
public sealed record UnityComponentInfo(string Type, string Label, string Path);

/// <summary>unity.js の定数(アセット・コンポーネント・人形のボーンと部品の形・パフォーマンスランクの目安)。</summary>
public static class UnityData
{
    public static readonly IReadOnlyList<UnityAsset> Assets =
    [
        new("Avatar", "prefab", "Avatar", Note: "アバター（練習用の人形）"),
        new("Hoodie", "prefab", "Hoodie", Note: "衣装（パーカー）。Avatar 向け"),
        new("Hoodie.fbx", "model", "Hoodie.fbx", Note: "衣装のモデル"),
        new("Hoodie_lilToon", "material", "Hoodie_lilToon", 0x3c3f8f, "lilToon"),
        new("Hoodie_Red", "material", "Hoodie_Red", 0xb8323a, "lilToon"),
        new("Hoodie_Standard", "material", "Hoodie_Standard", 0xb9b9b9, "Standard", Hidden: true),
        new("Hoodie_Tex", "texture", "Hoodie_Tex", Note: "テクスチャ（2048×2048）"),
        new("Body_lilToon", "material", "Body_lilToon", 0xf1d0bf, "lilToon", Hidden: true),
        new("Face_lilToon", "material", "Face_lilToon", 0xf1d0bf, "lilToon", Hidden: true),
        new("Shirt_Default", "material", "Shirt_Default", 0x8fb8de, "lilToon", Hidden: true),
    ];

    public static UnityAsset? Asset(string? key) => key is null ? null : Assets.FirstOrDefault(a => a.Key == key);

    public static readonly IReadOnlyList<UnityComponentInfo> Components =
    [
        new("MABlendshapeSync", "MA Blendshape Sync", "Modular Avatar"),
        new("MAMergeArmature", "MA Merge Armature", "Modular Avatar"),
        new("MABoneProxy", "MA Bone Proxy", "Modular Avatar"),
        new("MAMeshSettings", "MA Mesh Settings", "Modular Avatar"),
        new("MAMenuItem", "MA Menu Item", "Modular Avatar"),
        new("VRCPhysBone", "VRC Phys Bone", "VRChat"),
        new("VRCContactReceiver", "VRC Contact Receiver", "VRChat"),
        new("BoxCollider", "Box Collider", "Physics"),
        new("AudioSource", "Audio Source", "Audio"),
        new("Light", "Light", "Rendering"),
    ];

    public static string ComponentLabel(string type)
    {
        var c = Components.FirstOrDefault(x => x.Type == type);
        if (c is not null) return c.Label;
        return type switch
        {
            "Transform" => "Transform",
            "SkinnedMeshRenderer" => "Skinned Mesh Renderer",
            "Animator" => "Animator",
            "VRCAvatarDescriptor" => "VRC Avatar Descriptor",
            "PipelineManager" => "Pipeline Manager",
            "Camera" => "Camera",
            _ => type,
        };
    }

    /// <summary>人形のボーン(名前・親・位置。Unity は Y が上、アバターは +Z を向く)。</summary>
    public static readonly (string Name, string? Parent, double[] P)[] Bones =
    [
        ("Hips", null, [0, 0.95, 0]),
        ("Spine", "Hips", [0, 0.1, 0]),
        ("Chest", "Spine", [0, 0.15, 0]),
        ("Neck", "Chest", [0, 0.2, 0]),
        ("Head", "Neck", [0, 0.08, 0]),
        ("LeftUpperArm", "Chest", [0.18, 0.15, 0]),
        ("LeftLowerArm", "LeftUpperArm", [0.26, 0, 0]),
        ("LeftHand", "LeftLowerArm", [0.24, 0, 0]),
        ("RightUpperArm", "Chest", [-0.18, 0.15, 0]),
        ("RightLowerArm", "RightUpperArm", [-0.26, 0, 0]),
        ("RightHand", "RightLowerArm", [-0.24, 0, 0]),
        ("LeftUpperLeg", "Hips", [0.09, -0.05, 0]),
        ("LeftLowerLeg", "LeftUpperLeg", [0, -0.42, 0]),
        ("LeftFoot", "LeftLowerLeg", [0, -0.4, 0]),
        ("RightUpperLeg", "Hips", [-0.09, -0.05, 0]),
        ("RightLowerLeg", "RightUpperLeg", [0, -0.42, 0]),
        ("RightFoot", "RightLowerLeg", [0, -0.4, 0]),
    ];

    /// <summary>衣装のアーマチュアは上半身だけ(同じ名前のボーン)。</summary>
    public static readonly string[] OutfitBones = ["Hips", "Spine", "Chest", "LeftUpperArm", "LeftLowerArm", "RightUpperArm", "RightLowerArm"];

    /// <summary>メッシュの部品(ボーンに付ける)。Axis が "x" なら z まわりに 90° 回す。</summary>
    public sealed record Part(string Bone, string Shape, double[] Size, double[] Pos, string? Axis = null);

    public static readonly IReadOnlyDictionary<string, Part[]> MeshParts = new Dictionary<string, Part[]>
    {
        ["body"] =
        [
            new("Head", "sphere", [0.1], [0, 0.1, 0.005]),
            new("Neck", "capsule", [0.04, 0.06], [0, 0.03, 0]),
            new("Chest", "capsule", [0.12, 0.12], [0, 0.05, 0]),
            new("Spine", "capsule", [0.11, 0.08], [0, 0.04, 0]),
            new("Hips", "capsule", [0.12, 0.06], [0, 0.01, 0]),
            new("LeftUpperArm", "capsule", [0.042, 0.18], [0.13, 0, 0], "x"),
            new("LeftLowerArm", "capsule", [0.036, 0.17], [0.12, 0, 0], "x"),
            new("LeftHand", "sphere", [0.045], [0.04, 0, 0]),
            new("RightUpperArm", "capsule", [0.042, 0.18], [-0.13, 0, 0], "x"),
            new("RightLowerArm", "capsule", [0.036, 0.17], [-0.12, 0, 0], "x"),
            new("RightHand", "sphere", [0.045], [-0.04, 0, 0]),
            new("LeftUpperLeg", "capsule", [0.06, 0.3], [0, -0.21, 0]),
            new("LeftLowerLeg", "capsule", [0.048, 0.3], [0, -0.2, 0]),
            new("LeftFoot", "box", [0.08, 0.05, 0.18], [0, -0.03, 0.04]),
            new("RightUpperLeg", "capsule", [0.06, 0.3], [0, -0.21, 0]),
            new("RightLowerLeg", "capsule", [0.048, 0.3], [0, -0.2, 0]),
            new("RightFoot", "box", [0.08, 0.05, 0.18], [0, -0.03, 0.04]),
            new("Head", "eye", [0.014], [0.035, 0.11, 0.092]),
            new("Head", "eye", [0.014], [-0.035, 0.11, 0.092]),
            new("Head", "hair", [0.108], [0, 0.125, -0.006]),
        ],
        ["shirt"] =
        [
            new("Chest", "capsule", [0.13, 0.12], [0, 0.05, 0]),
            new("Spine", "capsule", [0.122, 0.09], [0, 0.03, 0]),
            new("LeftUpperArm", "capsule", [0.05, 0.08], [0.07, 0, 0], "x"),
            new("RightUpperArm", "capsule", [0.05, 0.08], [-0.07, 0, 0], "x"),
        ],
        ["hoodie"] =
        [
            new("Chest", "capsule", [0.142, 0.13], [0, 0.05, 0]),
            new("Spine", "capsule", [0.134, 0.1], [0, 0.03, 0]),
            new("Hips", "capsule", [0.132, 0.04], [0, 0.02, 0]),
            new("Chest", "hood", [0.1], [0, 0.22, -0.07]),
            new("LeftUpperArm", "capsule", [0.056, 0.19], [0.13, 0, 0], "x"),
            new("LeftLowerArm", "capsule", [0.05, 0.17], [0.12, 0, 0], "x"),
            new("RightUpperArm", "capsule", [0.056, 0.19], [-0.13, 0, 0], "x"),
            new("RightLowerArm", "capsule", [0.05, 0.17], [-0.12, 0, 0], "x"),
        ],
    };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, UnityMesh> shapeCache = new();   // テストが並列に動くので、スレッドごとに壊れない（ブラウザの WASM は 1 スレッド）

    /// <summary>部品の形(unity.js の makePart)。同じ形・大きさは 1 つを使い回す。</summary>
    public static UnityMesh PartMesh(string shape, double[] size)
    {
        var key = $"{shape}|{string.Join(",", size.Select(s => s.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}";
        if (shapeCache.TryGetValue(key, out var cached)) return cached;
        var mesh = shape switch
        {
            "sphere" or "eye" => UnityMesh.Sphere(size[0], 24, 16),
            "capsule" => UnityMesh.Capsule(size[0], size[1], 6, 16),
            "box" => UnityMesh.Box(size[0], size[1], size[2]),
            "hair" => UnityMesh.Sphere(size[0], 24, 16, 0, Math.PI * 2, 0, Math.PI * 0.55),
            "hood" => UnityMesh.Sphere(size[0], 20, 12, Math.PI * 0.15, Math.PI * 1.7, 0.2, Math.PI * 0.6),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "未知の形"),
        };
        shapeCache[key] = mesh;
        return mesh;
    }

    /// <summary>パフォーマンスランクの名前と、PC の目安の上限(Excellent / Good / Medium / Poor)。</summary>
    public static readonly string[] Ranks = ["Excellent", "Good", "Medium", "Poor", "Very Poor"];

    public static readonly IReadOnlyDictionary<string, int[]> Limits = new Dictionary<string, int[]>
    {
        ["tris"] = [32000, 70000, 70000, 70000],
        ["materials"] = [4, 8, 16, 32],
        ["skinned"] = [1, 2, 8, 16],
        ["bones"] = [75, 150, 256, 400],
        ["physbones"] = [4, 8, 16, 32],
    };
}

/// <summary>ノードが持つコンポーネント(Inspector に並ぶもの)。種類によって使う項目が違う(unity.js のオブジェクトと同じ)。</summary>
public sealed class UnityComponent
{
    public required string Type { get; init; }
    // SkinnedMeshRenderer
    public List<string>? Materials { get; set; }
    public List<string>? Blendshapes { get; set; }
    public int Tris { get; set; }
    // Animator
    public string? Avatar { get; set; }
    // VRCAvatarDescriptor
    public double[]? View { get; set; }
    // PipelineManager
    public string? BlueprintId { get; set; }
    // MAMergeArmature
    public string? Target { get; set; }
    public string? TargetId { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    // MABlendshapeSync
    public List<(string From, string Blendshape)>? Bindings { get; set; }
    // VRCPhysBone
    public string? Root { get; set; }
    public double Pull { get; set; }
    public double Spring { get; set; }

    public UnityComponent Clone() => new()
    {
        Type = Type,
        Materials = Materials is null ? null : [.. Materials],
        Blendshapes = Blendshapes is null ? null : [.. Blendshapes],
        Tris = Tris,
        Avatar = Avatar,
        View = View is null ? null : [.. View],
        BlueprintId = BlueprintId,
        Target = Target,
        TargetId = TargetId,
        Prefix = Prefix,
        Suffix = Suffix,
        Bindings = Bindings is null ? null : [.. Bindings],
        Root = Root,
        Pull = Pull,
        Spring = Spring,
    };
}

/// <summary>メッシュの部品 1 つ(three.js の Mesh に当たる)。</summary>
public sealed class UnityPart
{
    public required Obj3D Mesh { get; init; }
    public required UnityMesh Geometry { get; init; }
    public required string Shape { get; init; }
    public required string NodeId { get; init; }
    public required int Color { get; init; }
    /// <summary>材質の side: hood・hair は両面、それ以外は表だけ(three.js の FrontSide は裏を描かず、レイも当たらない)。</summary>
    public bool DoubleSided { get; init; }
}

/// <summary>Hierarchy のノード 1 つ(unity.js の node)。P・R(度)・S は Inspector に出す値。Obj は 3D のオブジェクト(作り直すたびに新しくなる)。</summary>
public sealed class UnityNode
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Parent { get; set; }
    public List<string> Children { get; set; } = [];
    public bool Active { get; set; } = true;
    public string Tag { get; set; } = "Untagged";
    public bool Expanded { get; set; }
    public double[] P { get; set; } = [0, 0, 0];
    public double[] R { get; set; } = [0, 0, 0];
    public double[] S { get; set; } = [1, 1, 1];
    public List<UnityComponent> Components { get; set; } = [];
    public string? Prefab { get; set; }
    public string? Mesh { get; set; }
    public string? Icon { get; set; }
    public bool Bone { get; set; }
    public Obj3D? Obj { get; set; }
    public List<UnityPart>? Parts { get; set; }

    /// <summary>取り消し用の複製(3D のオブジェクトと部品は含めない。作り直す)。</summary>
    public UnityNode Clone() => new()
    {
        Id = Id,
        Name = Name,
        Parent = Parent,
        Children = [.. Children],
        Active = Active,
        Tag = Tag,
        Expanded = Expanded,
        P = [.. P],
        R = [.. R],
        S = [.. S],
        Components = [.. Components.Select(c => c.Clone())],
        Prefab = Prefab,
        Mesh = Mesh,
        Icon = Icon,
        Bone = Bone,
    };

    public UnityComponent? Smr => Components.FirstOrDefault(c => c.Type == "SkinnedMeshRenderer");
}
