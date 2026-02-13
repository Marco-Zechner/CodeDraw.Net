using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.Input;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(3)]
public class Prototype3 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (new Prototype3(host))
        {
            host.WaitUntilAllWindowsClosed();
        }

        host.Stop();
    }

    private readonly CodeDrawWindow _win;
    private readonly CodeDrawWindow _win2;
    private readonly CodeDrawWindow _winOther;


    public Prototype3(SharedGlfwHost host)
    {
        _win = new CodeDrawWindow(host, 400, 400 , "Prototype3");
        var orbitShader = CustomShader.CsProject("orbitDots", "PrototypeTest/shaders");
        _win2 = new CodeDrawWindow(host, 400, 400 , "Prototype3 - Copy");
        var colorShiftShader = CustomShader.CsProject("colorShift", "PrototypeTest/shaders");
        _winOther = new CodeDrawWindow(host, 400, 400 , "Prototype3 - Other");
        _winOther.SetPresentedLayer(_win.Layer);

        _win.OnUpdate += context =>
        {
            var layer = context.Win.Layer;

            layer.Clear(1,1,1,0.5f);
            DrawOrbitingDots(layer, 200, 200, 10, 50, 2, 0, new Rgba(0.5f, 0, 0, 1f));
            DrawOrbitingDots(layer, 200, 200, 10, 80, 2*80/50f, 0, new Rgba(0.0f, 1, 1, 1f));
            layer.Render();

            if (context.Input.GetKeyDown(Keys.N))
            {
                if (_win2.IsOpen)
                    _win2.Close();
                else
                    _win2.Open();
                
                Console.WriteLine($"win2 is now " + (_win2.IsOpen ? "open" : "closed"));
            }
        };

        _win2.Settings = _win2.Settings with
        {
            MinSize = new Vector2<int>(200, 200),
            MaxSize = new Vector2<int>(600, 600),
            AspectRatio = new Vector2<int>(1, 1),
        };

        _win2.OnUpdate += context =>
        {
            var win = context.Win;
            var input = win.Input;

            var ctrl = input.GetModifierState(ModifierKeys.CONTROL);
            var delta = Vector2<int>.Zero;
            if (input.GetKeyDown(Keys.Left))
                delta = delta.WithX(delta.X - 10);
            if (input.GetKeyDown(Keys.Right))
                delta = delta.WithX(delta.X + 10);
            if (input.GetKeyDown(Keys.Up))
                delta = delta.WithY(delta.Y - 10);
            if (input.GetKeyDown(Keys.Down))
                delta = delta.WithY(delta.Y + 10);

            if (ctrl)
                win.Size += delta;
            else
                win.WindowPosition += delta;

            var keys = input.GetAllKeysDown();
            if (keys.Count != 0)
                Console.WriteLine("Keys down: " + string.Join(", ", keys));

            foreach (var keyDown in keys)
            {
                switch (keyDown)
                {
                    case Keys.A: _win2.TransparentAlpha = !_win2.TransparentAlpha;
                        break;
                    case Keys.C: _win2.ClickThrough = !_win2.ClickThrough;
                        break;
                    case Keys.F: _win2.ToggleResizeMode(WindowResizeMode.Fixed);
                        break;
                    case Keys.H: _win2.ToggleFrameMode();
                        break;
                    case Keys.I: _win2.ToggleState(WindowState.Minimized);
                        break;
                    case Keys.L: _win2.ToggleResizeMode(WindowResizeMode.Limited);
                        break;
                    case Keys.M: _win2.ToggleState(WindowState.BorderlessFullscreen);
                        break;
                    case Keys.R: _win2.ToggleResizeMode(WindowResizeMode.Aspect);
                        break;
                    case Keys.S when !ctrl: _win2.ToggleState(WindowState.Maximized);
                        break;
                    case Keys.S when ctrl: _win2.ToggleState(WindowState.BorderlessMaximized);
                        break;
                    case Keys.T: _win2.AlwaysOnTop = !_win2.AlwaysOnTop;
                        break;
                }
            }
            
            if (input.GetKeyDown(Keys.Escape))
            {
                win.Close();
                return;
            }
            
            if (input.GetKeyDown(Keys.X))
            {
                win.Size = new Vector2<int>(1920, 1080);
                return;
            }

            var layer = win.Layer;

            if (!_win.Layer.TryGetLastRenderTexture(out var lastRenderTexture))
            {
                Console.WriteLine("No render texture available yet.");
                return;
            }

            layer.Clear();
            layer.SetBlendMode(BlendMode.NONE);
            layer.CustomDrawRect(
                shader: colorShiftShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTex", lastRenderTexture),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                )
            );
            layer.Render();
        };

        return;

        void DrawOrbitingDots(CodeDrawLayer layer, float centerX, float centerY, float radiusDot, float radiusOrbit, float period, float timeOffset, Rgba color)
        {
            layer.CustomDrawRect(
                shader: orbitShader,
                uniforms: Uniforms.Of(
                    UniformValue.Float2("uPos", centerX, centerY),
                    UniformValue.Float2("uSize", layer.Width, layer.Height),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds()),
                    UniformValue.Float4("uColor", color.R, color.G, color.B, color.A),
                    UniformValue.Float("uRadius1", radiusDot),
                    UniformValue.Float("uRadius2", radiusOrbit),
                    UniformValue.Float("uPeriod",  period),
                    UniformValue.Float("uOffset",  timeOffset)
                )
            );
        }
    }


    public void Dispose()
    {
        _win.Dispose();
        _win2.Dispose();
        _winOther.Dispose();
    }
}