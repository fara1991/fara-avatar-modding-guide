using System.Numerics;

namespace Csharp3d.Shared;

/// <summary>頂点（位置・法線・色）。描画側（KNI / WebGL）がこの並びをそのまま、または変換して GPU に送る。</summary>
public struct Vertex
{
    public Vector3 P;
    public Vector3 N;
    public Vector3 C;

    public Vertex(Vector3 p, Vector3 n, Vector3 c) { P = p; N = n; C = c; }
}

/// <summary>GPU に送る静的な形の一覧。Shared 側で 1 回だけ作り、描画側は範囲（Start / Count）で指す。</summary>
public static class Geometry
{
    public enum Mesh { Cube, Sphere, Outline, Grid, AxisX, AxisY }

    /// <summary>すべての形を 1 本の配列にまとめたもの（Vertex の並び）</summary>
    public static readonly Vertex[] Vertices;
    public static readonly Dictionary<Mesh, (int Start, int Count)> Ranges = new();
    /// <summary>三角形（Cube / Sphere）か、線（Outline / Grid / Axis）か</summary>
    public static bool IsLines(Mesh m) => m is Mesh.Outline or Mesh.Grid or Mesh.AxisX or Mesh.AxisY;

    public static readonly Vector3 MeshColor = Rgb(0x9c9c9c);
    public static readonly Vector3 OutlineColor = Rgb(0xff9d2b);
    public static readonly Vector3 AxisXColor = Rgb(0x9a3a4a);
    public static readonly Vector3 AxisYColor = Rgb(0x5a8a2a);

    static Geometry()
    {
        var list = new List<Vertex>();
        void Mark(Mesh m, int start) => Ranges[m] = (start, list.Count - start);

        int s = list.Count;
        AddCube(list);
        Mark(Mesh.Cube, s);

        s = list.Count;
        AddSphere(list, 32, 16);
        Mark(Mesh.Sphere, s);

        // 選択中の外枠は、単位立方体（-1〜1）の 12 辺
        s = list.Count;
        AddBoxEdges(list);
        Mark(Mesh.Outline, s);

        // 床の方眼: GridHelper(40, 40) と同じ。-20〜20 の 41 本ずつ、中心線だけ色が違う
        s = list.Count;
        for (int i = -20; i <= 20; i++)
        {
            var c = i == 0 ? Rgb(0x4e4e4e) : Rgb(0x484848);
            list.Add(new Vertex(new Vector3(-20, i, 0), Vector3.Zero, c));
            list.Add(new Vertex(new Vector3(20, i, 0), Vector3.Zero, c));
            list.Add(new Vertex(new Vector3(i, -20, 0), Vector3.Zero, c));
            list.Add(new Vertex(new Vector3(i, 20, 0), Vector3.Zero, c));
        }
        Mark(Mesh.Grid, s);

        // X 軸・Y 軸の線（床の少し上に浮かせる）
        s = list.Count;
        list.Add(new Vertex(new Vector3(-20, 0, 0.002f), Vector3.Zero, AxisXColor));
        list.Add(new Vertex(new Vector3(20, 0, 0.002f), Vector3.Zero, AxisXColor));
        Mark(Mesh.AxisX, s);
        s = list.Count;
        list.Add(new Vertex(new Vector3(0, -20, 0.002f), Vector3.Zero, AxisYColor));
        list.Add(new Vertex(new Vector3(0, 20, 0.002f), Vector3.Zero, AxisYColor));
        Mark(Mesh.AxisY, s);

        Vertices = list.ToArray();
    }

    public static Vector3 Rgb(int hex) => new(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    /// <summary>三角形を足す。面の向きは中心（原点）の外向きに揃え、法線は面ごとに 1 つ（平面シェーディング）</summary>
    static void AddTri(List<Vertex> list, Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        var centroid = (a + b + c) / 3f;
        if (Vector3.Dot(n, centroid) < 0)
        {
            (b, c) = (c, b);
            n = -n;
        }
        list.Add(new Vertex(a, n, MeshColor));
        list.Add(new Vertex(b, n, MeshColor));
        list.Add(new Vertex(c, n, MeshColor));
    }

    static void AddQuad(List<Vertex> list, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTri(list, a, b, c);
        AddTri(list, a, c, d);
    }

    /// <summary>一辺 2 の立方体（SHAPES.cube と同じ寸法）。6 面 × 2 三角形</summary>
    static void AddCube(List<Vertex> list)
    {
        Vector3 V(float x, float y, float z) => new(x, y, z);
        AddQuad(list, V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1), V(1, -1, -1)); // 下
        AddQuad(list, V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1));     // 上
        AddQuad(list, V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1)); // -Y
        AddQuad(list, V(-1, 1, -1), V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1));     // +Y
        AddQuad(list, V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1)); // -X
        AddQuad(list, V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), V(1, -1, 1));     // +X
    }

    /// <summary>半径 1 の UV 球（既定 32 分割 × 16 リング。SHAPES.sphere と同じ分割）。極は点で、面ごとの平面シェーディング</summary>
    static void AddSphere(List<Vertex> list, int seg, int rings)
    {
        Vector3 Ring(int k, int i)
        {
            float th = k / (float)rings * MathF.PI;
            float ph = i / (float)seg * MathF.PI * 2;
            return new Vector3(MathF.Sin(th) * MathF.Cos(ph), MathF.Sin(th) * MathF.Sin(ph), MathF.Cos(th));
        }
        var top = new Vector3(0, 0, 1);
        var bottom = new Vector3(0, 0, -1);
        for (int i = 0; i < seg; i++)
        {
            AddTri(list, top, Ring(1, i), Ring(1, i + 1));
            AddTri(list, bottom, Ring(rings - 1, i + 1), Ring(rings - 1, i));
        }
        for (int k = 1; k < rings - 1; k++)
        {
            for (int i = 0; i < seg; i++)
            {
                AddQuad(list, Ring(k, i), Ring(k + 1, i), Ring(k + 1, i + 1), Ring(k, i + 1));
            }
        }
    }

    static void AddBoxEdges(List<Vertex> list)
    {
        var c = OutlineColor;
        var n = Vector3.Zero;
        void Edge(Vector3 a, Vector3 b)
        {
            list.Add(new Vertex(a, n, c));
            list.Add(new Vertex(b, n, c));
        }
        var p = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            p[i] = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
        }
        for (int i = 0; i < 8; i++)
        {
            for (int bit = 1; bit <= 4; bit <<= 1)
            {
                if ((i & bit) == 0) Edge(p[i], p[i | bit]);
            }
        }
    }
}
