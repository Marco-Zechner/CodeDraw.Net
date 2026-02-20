using System.Text;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Window;
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
        foreach (var w in _windows) w.Dispose();
    }

    private readonly List<CodeDrawWindow> _windows = [];

    public Prototype6()
    {
        var window = new CodeDrawWindow(_host, 1920, 1080, 50, 50, "Prototype6 - Grid Test");
        var textLayer = new CodeDrawLayer(_host, window.Width, window.Height);
        var glowLayer = new CodeDrawLayer(_host, window.Width, window.Height);
        var glowShader = CodeDrawShader.CsProject("glowShader", "PrototypeTest/shaders");
        var circleCopyShader = CodeDrawShader.CsProject("circleCopy", "PrototypeTest/shaders");
        _windows.Add(window);
        
        
        const int FONT_PX = 24;
        var padding = (X: 12, Y: 12);
        float background = 0.3f;
        
        var style = new TextStyle
        {
            Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                .WithVariant(FontVariant.Regular),
            SizePx = FONT_PX,

            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,

            Color = new(background,background,background,1),

            ExtraAbovePx = 0,
            ExtraBelowPx = 0,
            ExtraLineGapPx = 0,
            ExtraCellGapPx = 0,

            DebugMode = TextDebugMode.None
        };
        
        var titleStyle = style with
        {
            Font = style.Font.WithVariant(FontVariant.Bold),
            SizePx = FONT_PX,
            Color = new Rgba(0.9f, 0.9f, 0.9f, 1f),
            MonospaceSnapLineAlignToCells = true
        };

        var wall = new StringBuilder();
        float lastReset = -999;
        var resetAfter = 0.2f;
        
        var mouseMoved = false;
        var lastMousePos = Vector2<double>.Zero;

        StringBuilder displayTitle = new();
        
        
        window.OnUpdate += ctx =>
        {
            var win = ctx.Win;
            var keys = win.Input.GetAllKeysDown();
            if (keys.Count != 0)
                Console.WriteLine("Keys down: " + string.Join(", ", keys));

            foreach (var keyDown in keys)
            {
                switch (keyDown)
                {
                    case Keys.A:
                        win.TransparentAlpha = !win.TransparentAlpha;
                        break;
                    case Keys.C:
                        win.ClickThrough = !win.ClickThrough;
                        break;
                    case Keys.F:
                        win.ToggleResizeMode(WindowResizeMode.Fixed);
                        break;
                    case Keys.H:
                        win.ToggleFrameMode();
                        break;
                    case Keys.I:
                        win.ToggleState(WindowState.Minimized);
                        break;
                    case Keys.L:
                        win.ToggleResizeMode(WindowResizeMode.Limited);
                        break;
                    case Keys.M:
                        win.ToggleState(WindowState.BorderlessFullscreen);
                        break;
                    case Keys.R:
                        win.ToggleResizeMode(WindowResizeMode.Aspect);
                        break;
                    case Keys.S:
                        win.ToggleState(WindowState.Maximized);
                        break;
                    case Keys.T:
                        win.AlwaysOnTop = !win.AlwaysOnTop;
                        break;
                    case Keys.Escape:
                        win.Close();
                        return;
                }
            }

            textLayer.RequestLayerSize(window.Width, window.Height);
            textLayer.Clear();
            
            Vector2<double> mousePos = new(ctx.Win.Input.MouseX, ctx.Win.Input.MouseY);
            if (mousePos != lastMousePos)
            {  
                mouseMoved = true;
                lastMousePos = mousePos;
            }
            else
                mouseMoved = false;
            
            // Find cell metrics by measuring 1x1 and 1x2 blocks (cheap + reliable for now)
            // (This avoids needing a new public API method right now.)
            var m1 = textLayer.MeasureText("█", style);
            var m2 = textLayer.MeasureText("█\n█", style);
            var cellW = m1.X;
            var lineH = m2.Y / 2f;

            var cols = (int)((textLayer.Width - padding.X * 2) / cellW);
            var rows = (int)((textLayer.Height - padding.Y * 2) / lineH);

            
            var title = $"This{(rows % 2 == 0 ? "  " : "\n")}is\nCodeDraw.Net";
            
            var titleLines = title.Split('\n');
            var titleLineCount = titleLines.Length;
            var maxTitleLineLength = titleLines.Max(x => x.Length);
            
            var distanceToCenter = (mousePos - textLayer.Size / 2).Length<double>();
            var maxDistance = (textLayer.Size / 2).Length<double>() / 1.5f;
            var minDistance = (textLayer.Size / 2).Length<double>() / 15;
            var displayedChars = (int)MathG.MapClamped(distanceToCenter, minDistance, maxDistance, title.Length, 0);
            
            if (mouseMoved)
            {
                displayTitle.Clear();
                
                for (var i = 0; i < titleLineCount; i++)
                {
                    displayTitle.Append(' ', maxTitleLineLength);
                    if (i != titleLines.Length - 1) displayTitle.AppendLine();
                }
                
                lastReset = textLayer.LayerAliveForSeconds();
                wall.Clear();

                for (var y = 0; y < rows; y++)
                {
                    wall.Append(RandomString(cols));
                    if (y != rows - 1) wall.Append('\n');
                }
                
                var middleLineIndex = rows / 2;
                var middleLineStart = middleLineIndex * (cols + 1);
                
                var titleMiddleIndex = titleLineCount / 2;
                var titleMiddleLineStart = titleMiddleIndex * (maxTitleLineLength + 1);

                var desiredVisible = displayedChars;
                while (title.Take(displayedChars).Count(c => c != ' ') != desiredVisible)
                {
                    displayedChars++;
                    if (displayedChars > title.Length) break;
                }
                
                var charsToClear = displayedChars;
                for (var i = 0; i < titleLines.Length; i++)
                {
                    var line = titleLines[i];
                    var charsToReplaceThisLine = Math.Min(charsToClear, line.Length);
                    charsToClear -= charsToReplaceThisLine;
                    
                    var lineStartIndex = middleLineStart + (i-1) * (cols + 1);
                    var lineMiddleIndex = lineStartIndex + cols / 2;
                    var lineInsertIndex = lineMiddleIndex - line.Length / 2;
                    for (var j = 0; j < charsToReplaceThisLine; j++)
                    {
                        if (line[j] != ' ')
                            wall.Remove(lineInsertIndex + i, 1).Insert(lineInsertIndex + i, new string(' ', 1));
                    }
                    
                    var titleLineStartIndex = titleMiddleLineStart + (i-1) * (maxTitleLineLength + 1);
                    var titleLineInsertIndex = titleLineStartIndex + maxTitleLineLength / 2;
                    var titleInsertIndex = titleLineInsertIndex - line.Length / 2;
                    if (i != 0) titleInsertIndex += 1;
                    displayTitle.Remove(titleInsertIndex, charsToReplaceThisLine).Insert(titleInsertIndex, line[..charsToReplaceThisLine]);
                }
            }
            
            var textWall = wall.ToString();
            
            var size = textLayer.MeasureText(textWall, style);
            var offsetX = (textLayer.Width - size.X) / 2f;
            var offsetY = (textLayer.Height - size.Y) / 2f;
            
            textLayer.DrawText(textWall, offsetX, offsetY, style);
            
            var titleSize = textLayer.MeasureText(title, style);
            var titleOffsetX = offsetX + size.X/2f - titleSize.X/2f;
            var titleOffsetY = offsetY + size.Y/2f - titleSize.Y/2f;
            
            textLayer.DrawText(displayTitle.ToString(), titleOffsetX, titleOffsetY, titleStyle);
            
            textLayer.Render();


            var glowRadius = 250f;
            float glowIntensity = 1;
            
            window.Layer.Clear(0, 0, 0, 1);
            
            
            window.Layer.CustomDrawRect(
                0, 0, textLayer.Width, textLayer.Height,
                shader: glowShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTex", textLayer),
                    UniformValue.Float2("uResolution", textLayer.Width, textLayer.Height),
                    UniformValue.Float2("uGlowPos", (float)mousePos.X, textLayer.Height - (float)mousePos.Y),
                    UniformValue.Float("uRadius", glowRadius),
                    UniformValue.Float("uIntensity", glowIntensity),
                    UniformValue.Float3("uGlowColor", 0.3f, 0.3f, 0.3f)
                )
            );
            
            window.Layer.CustomDrawRect(
                0, 0, textLayer.Width, textLayer.Height,
                shader: circleCopyShader,
                uniforms: Uniforms.Of(
                    UniformValue.Tex2D("uTex", textLayer),
                    UniformValue.Float2("uResolution", textLayer.Width, textLayer.Height),
                    UniformValue.Float2("uGlowPos", textLayer.Width/2f, textLayer.Height/2f),
                    UniformValue.Float("uRadius", 150f),
                    UniformValue.Float("uEdgeSoftness", 40)
                )
            );
            
            window.Layer.Render();
        };
    }

    private static readonly Random _random = new();

    public static string RandomString(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
    {
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
