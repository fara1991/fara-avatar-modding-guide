using System.Diagnostics;
using Csharp3d.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NVector3 = System.Numerics.Vector3;
using NMatrix = System.Numerics.Matrix4x4;
using XColor = Microsoft.Xna.Framework.Color;
using XMatrix = Microsoft.Xna.Framework.Matrix;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace Csharp3d.Kni;

/// <summary>
/// KNI（MonoGame 互換）の描画・入力。ゲームロジックは Practice3D（Shared）にあり、ここは
/// 「KNI の入力を Practice3D に渡す」「Draws() を BasicEffect で描く」だけ。
/// </summary>
public sealed class PracticeGame : Game
{
    readonly Practice3D core;
    readonly GraphicsDeviceManager graphics;
    BasicEffect effect;
    VertexBuffer triBuffer;
    VertexBuffer lineBuffer;
    MouseState prevMouse;
    KeyboardState prevKeys;
    readonly Stopwatch drawWatch = new();

    /// <summary>計測用: Draw の CPU 時間（ms）の合計と回数</summary>
    public double DrawMsTotal { get; private set; }
    public int DrawCount { get; private set; }

    public PracticeGame(Practice3D core)
    {
        this.core = core;
        graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        var verts = Geometry.Vertices;
        var tri = new VertexPositionNormalTexture[verts.Length];
        var line = new VertexPositionColor[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var v = verts[i];
            tri[i] = new VertexPositionNormalTexture(ToX(v.P), ToX(v.N), Vector2.Zero);
            line[i] = new VertexPositionColor(ToX(v.P), new XColor(v.C.X, v.C.Y, v.C.Z));
        }
        triBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), tri.Length, BufferUsage.WriteOnly);
        triBuffer.SetData(tri);
        lineBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), line.Length, BufferUsage.WriteOnly);
        lineBuffer.SetData(line);

        // BasicEffect の既定ライト（平行光源 1 つ + 環境光）で、viewport.js の半球光 + 平行光源（(4,-6,8)）に近づける
        effect = new BasicEffect(GraphicsDevice)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            AmbientLightColor = new XVector3(0.15f),
            DiffuseColor = new XVector3(Geometry.MeshColor.X, Geometry.MeshColor.Y, Geometry.MeshColor.Z),
        };
        var light = NVector3.Normalize(new NVector3(4, -6, 8));
        effect.DirectionalLight0.Enabled = true;
        effect.DirectionalLight0.Direction = -ToX(light);
        effect.DirectionalLight0.DiffuseColor = new XVector3(1.2f);
        effect.DirectionalLight0.SpecularColor = XVector3.Zero;
        effect.DirectionalLight1.Enabled = false;
        effect.DirectionalLight2.Enabled = false;
    }

    protected override void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys = Keyboard.GetState();
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
        float x = mouse.X, y = mouse.Y;

        ButtonEdge(prevMouse.LeftButton, mouse.LeftButton, 0, x, y, shift, alt, ctrl);
        ButtonEdge(prevMouse.MiddleButton, mouse.MiddleButton, 1, x, y, shift, alt, ctrl);
        ButtonEdge(prevMouse.RightButton, mouse.RightButton, 2, x, y, shift, alt, ctrl);
        core.PointerMove(x, y);

        int wheel = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
        if (wheel != 0) core.Wheel(wheel > 0 ? -1 : 1);

        foreach (var k in keys.GetPressedKeys())
        {
            if (!prevKeys.IsKeyDown(k) && KeyCode(k) is { } code) core.KeyDown(code);
        }
        prevMouse = mouse;
        prevKeys = keys;
        base.Update(gameTime);
    }

    void ButtonEdge(ButtonState before, ButtonState now, int button, float x, float y, bool shift, bool alt, bool ctrl)
    {
        if (before == ButtonState.Released && now == ButtonState.Pressed) core.PointerDown(button, x, y, shift, alt, ctrl);
        else if (before == ButtonState.Pressed && now == ButtonState.Released) core.PointerUp(button, x, y);
    }

    protected override void Draw(GameTime gameTime)
    {
        drawWatch.Restart();
        GraphicsDevice.Clear(new XColor(0x3d, 0x3d, 0x3d));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        core.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        effect.View = ToX(core.View);
        effect.Projection = ToX(core.Proj);
        foreach (var d in core.Draws())
        {
            var (start, count) = Geometry.Ranges[d.Mesh];
            effect.World = ToX(d.World);
            if (Geometry.IsLines(d.Mesh))
            {
                effect.LightingEnabled = false;
                effect.VertexColorEnabled = true;
                GraphicsDevice.SetVertexBuffer(lineBuffer);
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, start, count / 2);
                }
            }
            else
            {
                effect.LightingEnabled = true;
                effect.VertexColorEnabled = false;
                GraphicsDevice.SetVertexBuffer(triBuffer);
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, start, count / 3);
                }
            }
        }
        drawWatch.Stop();
        DrawMsTotal += drawWatch.Elapsed.TotalMilliseconds;
        DrawCount++;
        base.Draw(gameTime);
    }

    // ---------------------------------------------------------------- 変換
    static XVector3 ToX(NVector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>System.Numerics と XNA の行列は、どちらも行ベクトル（v * M）で成分の並びも同じ</summary>
    static XMatrix ToX(NMatrix m) => new(
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44);

    /// <summary>KNI の Keys を、KeyboardEvent.code と同じ形の文字列にする（Practice3D.KeyDown が受け取る形）</summary>
    static string? KeyCode(Keys k)
    {
        var name = k.ToString();
        if (name.Length == 1 && char.IsLetter(name[0])) return "Key" + name;
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) return "Digit" + name[1];
        if (name.StartsWith("NumPad", StringComparison.Ordinal) && name.Length == 7) return "Numpad" + name[6];
        return name switch
        {
            "Enter" => "Enter",
            "Escape" => "Escape",
            "Back" => "Backspace",
            "OemMinus" => "Minus",
            "Subtract" => "NumpadSubtract",
            "OemPeriod" => "Period",
            "Decimal" => "NumpadDecimal",
            _ => null,
        };
    }
}
