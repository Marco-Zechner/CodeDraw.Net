using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class Prototype3
{


    [ConstructorPrototype(3)]
    public Prototype3()
    {
        using var app = CodeDrawHost.Started();
        
        var win1 = new CodeDrawWindow(400, 400 , "Prototype3");
        var orbitShader = CodeDrawShader.CsProject("orbitDots", "PrototypeTest/shaders");
        
        var win2 = new CodeDrawWindow(400, 400 , "Prototype3 - Copy");
        var colorShiftShader = CodeDrawShader.CsProject("colorShift", "PrototypeTest/shaders");
        
        var win3 = new CodeDrawWindow(400, 400 , "Prototype3 - Other");
        var colorShiftPpShader = CodeDrawShader.CsProject("colorShiftPP", "PrototypeTest/shaders/ppShader");

        win1.OnUpdate += context =>
        {
            var win = context.Win;
            
            var input = win.Input;

            var ctrl = input.GetModifierState(ModifierKeys.CONTROL);
            var delta = Vector2<int>.Zero;
            if (input.GetKeyDown(Keys.Left))
                delta = delta with { X = delta.X - 10 };
            if (input.GetKeyDown(Keys.Right))
                delta = delta with { X = delta.X + 10 };
            if (input.GetKeyDown(Keys.Up))
                delta = delta with { Y = delta.Y - 10 };
            if (input.GetKeyDown(Keys.Down))
                delta = delta with { Y = delta.Y + 10 };

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
                    case Keys.L: win.ToggleResizeMode(WindowResizeMode.Limited); //TODO: broken
                        break;
                    case Keys.M: win.ToggleState(WindowState.BorderlessFullscreen); //TODO: worked a few time (but loses focus!) then it crashed on another try.
                        break;
                    case Keys.R: win.ToggleResizeMode(WindowResizeMode.Aspect); //TODO: broken
                        break;
                    case Keys.S when !ctrl: win.ToggleState(WindowState.Maximized); //TODO: no focus, and sometimes crash
                        break;
                    case Keys.S when ctrl: win.ToggleState(WindowState.BorderlessMaximized); //TODO: no focus, and sometimes crash
                        break;                                                              // to be clear, it should "KEEP" focus if it had it, not just get it always.
                    case Keys.T: win.AlwaysOnTop = !win.AlwaysOnTop;
                        break;
                    case Keys.Escape: win.Close();
                        return;
                    case Keys.X: win.Size = new Vector2<int>(1920, 1080);
                        break;
                    case Keys.Number1:
                        if (win2.IsOpen)
                            win2.Close();
                        else
                            win2.Open();
                        break;
                    case Keys.Number2:
                        if (win3.IsOpen)
                            win3.Close();
                        else
                            win3.Open();
                        break;
                }
            }
            
            var layer = win.Layer;

            layer.Clear(1,1,1, 0.5f);
            DrawOrbitingDots(layer, 200, 200, 10, 50, 2, 0, new ColorF(0.5f, 0, 0, 1f), orbitShader);
            DrawOrbitingDots(layer, 200, 200, 10, 80, 2*80/50f, 0, new ColorF(0.0f, 1, 1, 1f), orbitShader);
            DrawOrbitingDots(layer, 350, 350, 4, 35, -2*80/50f, 0, new ColorF(0.0f, 1, 1, 1f), orbitShader);
            layer.DrawDebugRect(100,100,100,100, 1,0,0,0.5f);
            layer.Render();
        };

        win2.Settings = win2.Settings with
        {
            MinSize = new Vector2<int>(200, 200),
            MaxSize = new Vector2<int>(600, 600),
            AspectRatio = new Vector2<int>(1, 1),
        };

        win2.OnUpdate += context =>
        {
            var win = context.Win;
            var layer = win.Layer;

            layer.Clear();
            layer.SetBlendMode(BlendMode.NONE);
            layer.CustomRect(
                layer.FullRect,
                shader: colorShiftShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTexCopy", win1.Layer),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                )
            );
            layer.Render();
        };

        win3.OnUpdate += context =>
        {
            var win = context.Win;
            var layer = win.Layer;

            layer.Clear();
            layer.SetBlendMode(BlendMode.NONE);
            layer.DrawLayer(win1.Layer);
            layer.PostProcess(colorShiftPpShader, 
                UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                );
            layer.Render();
        };
        
        app.WaitForClose();
    }

    private void DrawOrbitingDots(CodeDrawLayer layer, int centerX, int centerY, int radiusDot, int radiusOrbit,
        float period,
        float timeOffset,
        ColorF color,
        CodeDrawShader orbitShader
    )
    {
        var size = radiusOrbit * 2 + radiusDot * 2;
            
        layer.CustomRect(
            new Rect<int>(centerX-size/2, centerY-size/2, size, size),
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