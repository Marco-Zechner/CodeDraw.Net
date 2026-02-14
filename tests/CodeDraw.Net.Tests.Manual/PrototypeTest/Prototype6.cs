using System.Diagnostics;
using System.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

[Prototype(6)]
public class Prototype6 : IDisposable
{
    private static SharedGlfwHost _host = null!;
    
    [StaticPrototype]
    public static void RunTest()
    {
        _host = SharedGlfwHost.Instance;
        _host.Start();

        using (new Prototype6())
        {
            _host.WaitUntilAllWindowsClosed();
        }

        _host.Stop();
        _host.Dispose();
    }
    
    public void Dispose()
    {
        foreach (var w in _windows)
        {
            w.Dispose();
        }
    }
    
    private readonly List<CodeDrawWindow> _windows = [];

    public Prototype6()
    {
        var window = new CodeDrawWindow(_host, 620,740 ,50,50, "Prototype6 - Window1"); 
        // 640x745 fits perfectly with padding... but it according to math it should 620x720 -> (10x60px & 7x100px) + 10px padding
        var postProcessingBloom = CodeDrawShader.CsProject("bloom", "PrototypeTest/shaders/ppShader");
        _windows.Add(window);

        const int FONT_HEIGHT_PX = 100;
        Vector2<int> paddingPx = new(10,10);
        
        var style = new TextStyle
        {
            Font = FontRef.FromFile("C:\\DevProjects\\CodeDraw.Net\\tests\\CodeDraw.Net.Tests.Manual\\resources\\fonts\\FiraCode-VF.ttf")
                .WithVariant(FontVariant.BoldItalic),
            // Font = FontRef.FromFile("C:\\DevProjects\\CodeDraw.Net\\tests\\CodeDraw.Net.Tests.Manual\\resources\\fonts\\SpaceMono-Regular.ttf"),
            SizePx = FONT_HEIGHT_PX,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            RelativeCharacterSpacing = 1,
            RelativeLineSpacing = 1,
            Color = new Rgba(1,1,1,0.5f)
        };

        var fontWidthPx = style.CharacterWidthPx;
        Console.WriteLine($"Font with h:{FONT_HEIGHT_PX}px and w:{fontWidthPx}px");
        
        var gridCount = new Vector2<int>((window.Width-paddingPx.X*2) / (int)fontWidthPx, (window.Height-paddingPx.Y*2) / FONT_HEIGHT_PX);
        Console.WriteLine($"Grid count: {gridCount}");
        
        var textWall = new StringBuilder();
        var textWall2 = new StringBuilder();

        float lastRestTime = 0;
        float resetAfterSeconds = 1;
        
        window.OnUpdate += context =>
        {
            if (context.Input.GetKeyDown(Keys.A))
            {
                context.Win.ToggleResizeMode(WindowResizeMode.Aspect);
            }
            
            var layer = context.Win.Layer;
            layer.Clear(0,0,0,1);

            if (layer.LayerAliveForSeconds() - lastRestTime > resetAfterSeconds)
            {
                lastRestTime = layer.LayerAliveForSeconds();
                textWall.Clear();
                for (var y = 0; y < gridCount.Y; y++)
                {
                    textWall.Append(RandomString(gridCount.X, "█"));
                    if(y != gridCount.Y-1) textWall.Append('\n');
                }
                
                // textWall2.Clear();
                // for (var y = 0; y < gridCount.Y; y++)
                // {
                //     textWall2.Append(RandomString(gridCount.X-2));
                //     if(y != gridCount.Y-1) textWall2.Append('\n');
                // }
            } 

            layer.DrawText(textWall.ToString(), paddingPx.X, paddingPx.Y, style);
            layer.DrawText(textWall2.ToString(), paddingPx.X, paddingPx.Y, style);
            
            var size = layer.MeasureText(textWall.ToString(), style);
            Console.WriteLine($"Measured text size: {size}");
            
            layer.DrawRect(0,0, layer.Width, paddingPx.Y, 1,0,0,0.5f);
            layer.DrawRect(0,layer.Height-paddingPx.Y, layer.Width, paddingPx.Y, 1,0,0,0.5f);
            layer.DrawRect(0,0, paddingPx.X, layer.Height, 1,0,0,0.5f);
            layer.DrawRect(layer.Width-paddingPx.X,0, paddingPx.X, layer.Height, 1,0,0,0.5f);

            for (int i = 0; i < gridCount.Y; i++)
            {
                layer.DrawRect(paddingPx.X + 5 * i, paddingPx.Y + FONT_HEIGHT_PX * i, 5, FONT_HEIGHT_PX, 0, 1, 0, 0.5f);
            }

            for (int i = 0; i < gridCount.X; i++)
            {
                layer.DrawRect(paddingPx.X + fontWidthPx*i, paddingPx.Y + 5 * i, fontWidthPx, 5, 0, 1, 0, 0.5f);
            }

            // var glow = 25 + 25 * MathF.Sin(layer.LayerAliveForSeconds() * 5f);
            //
            // layer.PostProcess(postProcessingBloom,
            //     uniforms: Uniforms.Of(
            //         UniformValue.Float("uGlow", glow)
            //     )
            // );
            
            layer.Render();
        };
    }
    
    private static readonly Random _random = new Random();

    public static string RandomString(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
    {
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}