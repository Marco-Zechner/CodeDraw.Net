using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(3)]
public class Prototype3 : IDisposable
{
    [StaticPrototype]
    public static void RunTest()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        using (var session = new Prototype3(host))
        {
            session.WaitForClose();
        }

        host.Stop();
    }

    private void WaitForClose()
    {
        _win.WaitForClose();
        _win2.WaitForClose();
    }

    private readonly CodeDrawWindow _win;
    private readonly CodeDrawWindow _win2;


    public Prototype3(SharedGlfwHost host)
    {
        _win = new CodeDrawWindow(host, 400, 400 , "Prototype3");
        var orbitShader = CustomShader.CsProject("orbitDots", "PrototypeTest/shaders");
        _win2 = new CodeDrawWindow(host, 400, 400 , "Prototype3 - Copy");
        var colorShiftShader = CustomShader.CsProject("colorShift", "PrototypeTest/shaders");

        _win.OnUpdate += context =>
        {
            var layer = context.Win.Layer;

            layer.Clear(1,1,1,1);
            DrawOrbitingDots(layer, 200, 200, 10, 50, 2, 0, new Rgba(0.5f, 0, 0, 1f));
            DrawOrbitingDots(layer, 200, 200, 10, 80, 2*80/50f, 0, new Rgba(0.0f, 1, 1, 1f));
            layer.Render();
        };

        _win2.OnUpdate += context =>
        {
            var layer = context.Win.Layer;

            if (!_win.Layer.TryGetLastRenderTexture(out var lastRenderTexture))
            {
                Console.WriteLine("No render texture available yet.");
                return;
            }

            layer.Clear();
            layer.CustomDrawRect(
                shader: colorShiftShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTex", lastRenderTexture),
                    UniformValue.Float("uTime", layer.LayerAliveForSeconds())
                )
            );
            layer.Render();
            Console.WriteLine(_win.WindowSettings.CurrentSnapshot().WindowPosition);
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
    }
}