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
    private readonly CodeDrawWindow _win1;
    private readonly CodeDrawWindow _win2;


    public Prototype3(SharedGlfwHost host)
    {
        _win = new CodeDrawWindow(host, 400, 400 , "Prototype3");
        var orbitShader = CustomShader.CsProject("orbitDots", "PrototypeTest/shaders");
        
        _win1 = new CodeDrawWindow(host, 400, 400 , "Prototype3 - Copy");
        var colorShiftShader = CustomShader.CsProject("colorShift", "PrototypeTest/shaders");
        
        _win2 = new CodeDrawWindow(host, 400, 400 , "Prototype3 - Other");
        var colorShiftPPShader = CustomShader.CsProject("colorShiftPP", "PrototypeTest/shaders/ppShader");

        _win.OnUpdate += context =>
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
                    case Keys.A: win.TransparentAlpha = !win.TransparentAlpha;
                        break;
                    case Keys.C: win.ClickThrough = !win.ClickThrough;
                        break;
                    case Keys.F: win.ToggleResizeMode(WindowResizeMode.Fixed);
                        break;
                    case Keys.H: win.ToggleFrameMode();
                        break;
                    case Keys.I: win.ToggleState(WindowState.Minimized);
                        break;
                    case Keys.L: win.ToggleResizeMode(WindowResizeMode.Limited);
                        break;
                    case Keys.M: win.ToggleState(WindowState.BorderlessFullscreen);
                        break;
                    case Keys.R: win.ToggleResizeMode(WindowResizeMode.Aspect);
                        break;
                    case Keys.S when !ctrl: win.ToggleState(WindowState.Maximized);
                        break;
                    case Keys.S when ctrl: win.ToggleState(WindowState.BorderlessMaximized);
                        break;
                    case Keys.T: win.AlwaysOnTop = !win.AlwaysOnTop;
                        break;
                    case Keys.Escape: win.Close();
                        return;
                    case Keys.X: win.Size = new Vector2<int>(1920, 1080);
                        break;
                    case Keys.Number1:
                        if (_win1.IsOpen)
                            _win1.Close();
                        else
                            _win1.Open();
                        break;
                    case Keys.Number2:
                        if (_win2.IsOpen)
                            _win2.Close();
                        else
                            _win2.Open();
                        break;
                }
            }
            
            var layer = win.Layer;

            layer.Clear(1,1,1,0.5f);
            DrawOrbitingDots(layer, 200, 200, 10, 50, 2, 0, new Rgba(0.5f, 0, 0, 1f));
            DrawOrbitingDots(layer, 200, 200, 10, 80, 2*80/50f, 0, new Rgba(0.0f, 1, 1, 1f));
            DrawOrbitingDots(layer, 350, 350, 4, 35, -2*80/50f, 0, new Rgba(0.0f, 1, 1, 1f));
            layer.Render();
        };

        _win1.Settings = _win1.Settings with
        {
            MinSize = new Vector2<int>(200, 200),
            MaxSize = new Vector2<int>(600, 600),
            AspectRatio = new Vector2<int>(1, 1),
        };

        _win1.OnUpdate += context =>
        {
            var win = context.Win;
            var layer = win.Layer;

            layer.Clear();
            layer.SetBlendMode(BlendMode.NONE);
            layer.CustomDrawRect(
                0,0, layer.Width, layer.Height,
                shader: colorShiftShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTexCopy", _win.Layer),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                )
            );
            layer.Render();
        };

        _win2.OnUpdate += context =>
        {
            var win = context.Win;
            var layer = win.Layer;

            layer.Clear();
            layer.SetBlendMode(BlendMode.NONE);
            layer.DrawLayer(_win.Layer);
            layer.PostProcess(colorShiftPPShader, 
                UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                );
            layer.Render();
        };

        return;

        void DrawOrbitingDots(CodeDrawLayer layer, int centerX, int centerY, int radiusDot, int radiusOrbit, float period, float timeOffset, Rgba color)
        {
            var size = radiusOrbit * 2 + radiusDot * 2;
            
            layer.CustomDrawRect(
                centerX-size/2, centerY-size/2, size, size,
                shader: orbitShader,
                uniforms: Uniforms.Of(
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
        _win1.Dispose();
        _win2.Dispose();
    }
}