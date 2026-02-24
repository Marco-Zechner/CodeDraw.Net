using System.Text;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class Prototype6
{
    
    [ConstructorPrototype(6)]
    public Prototype6()
    {
        using var app = CodeDrawHost.Start();
        
        var window = new CodeDrawWindow(1920, 1080, 50, 50, "Prototype6 - Grid Test");
        var textLayer = new CodeDrawLayer(window.Width, window.Height);
        var glowShader = CodeDrawShader.CsProject("glowShader", "PrototypeTest/shaders");
        var circleCopyShader = CodeDrawShader.CsProject("circleCopy", "PrototypeTest/shaders");
        
        const int FONT_PX = 24;
        var padding = (X: 12, Y: 12);
        const float BACKGROUND = 0.3f;
        
        var style = new TextStyle
        {
            Font = FontRef.FromFile(@"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf")
                .WithVariant(FontVariant.Regular),
            SizePx = FONT_PX,

            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,

            Color = new ColorF(BACKGROUND,BACKGROUND,BACKGROUND,1),

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
            Color = new ColorF(0.9f, 0.9f, 0.9f, 1f),
            MonospaceSnapLineAlignToCells = true
        };

        var wall = new StringBuilder();

        bool mouseMoved;
        var lastMousePos = Vector2<double>.Zero;

        StringBuilder displayTitle = new();
        
        bool firstFrame = true;
        
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

            var resized = textLayer.RequestLayerSize(window.Width, window.Height);
            window.Layer.RequestLayerSize(window.Width, window.Height); // important to have a good text quality. TODO: let window do that automatically in some cases. see todo.md
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
            
            cellW = MathF.Round(cellW * 2)/2f;    // or Round(cellW * 2)/2 for half-pixel
            lineH = MathF.Round(lineH * 2)/2f;

            var cols = (int)((textLayer.Width - padding.X * 2) / cellW);
            var rows = (int)((textLayer.Height - padding.Y * 2) / lineH);

            
            var title = (rows & 1) == 0
                ? "This  is\nCodeDraw.Net"
                : "This  is\n\nCodeDraw.Net";
            
            var titleLines = title.Split('\n');
            var titleLineCount = titleLines.Length;

            var titleGlyphCount = title.Count(t => t != '\n' && t != '\r');

            // Distance -> how many title characters are “revealed”
            var distanceToCenter = (mousePos - textLayer.Size / 2).LengthT<double>();
            var maxDistance = (textLayer.Size / 2).LengthT<double>() / 2f;
            var minDistance = (textLayer.Size / 2).LengthT<double>() / 15;

            var desiredVisible = (int)MathG.MapClamped(
                distanceToCenter,
                minDistance,
                maxDistance,
                titleGlyphCount,
                0
            );

            // only rebuild when mouse moved OR after some time has passed (optional)
            var shouldRebuild = mouseMoved || resized || firstFrame;
            firstFrame = false;

            string textWall;
            string displayTitleStr;

            if (shouldRebuild)
            {
                // Build fresh random wall buffer
                var wallBuf = CreateRandomWallBuffer(rows, cols);

                // Build a title buffer (same grid size) filled with spaces/newlines later
                var titleBuf = CreateBlankBuffer(rows, cols);

                // Reveal characters linearly across the title (left->right, top->bottom).
                // Also: ensure we only count non-space chars as "revealed".
                var remainingVisible = desiredVisible;

                // Centering that works for odd/even:
                // Example: 3 lines => startRow = centerRow - 1
                //          4 lines => startRow = centerRow - 1  (slightly top-biased, stable)
                var centerRow = rows / 2;
                var centerCol = cols / 2;
                var startRow = centerRow - (titleLineCount - 1) / 2;
                if ((rows & 1) == 0) startRow--;

                for (var li = 0; li < titleLineCount; li++)
                {
                    var line = titleLines[li];
                    var row = startRow + li;

                    if ((uint)row >= (uint)rows) continue; // out of bounds (tiny windows)

                    var colStart = centerCol - (line.Length / 2);

                    // clamp start so we never write outside
                    if (colStart < 0) colStart = 0;
                    if (colStart + line.Length > cols) colStart = Math.Max(0, cols - line.Length);

                    // Determine how many chars of THIS line are revealed, counting only non-spaces.
                    var revealThisLine = 0;
                    for (var j = 0; j < line.Length && remainingVisible > 0; j++)
                    {
                        if (line[j] == ' ') continue;

                        revealThisLine++;
                        remainingVisible--;
                    }

                    // Now actually write revealed chars (and carve the wall underneath).
                    var writtenNonSpace = 0;
                    for (var j = 0; j < line.Length; j++)
                    {
                        var c = line[j];
                        if (c == ' ') continue;

                        if (writtenNonSpace >= revealThisLine) break;

                        var col = colStart + j;
                        if ((uint)col >= (uint)cols) continue;

                        // carve wall behind the title
                        wallBuf[row][col] = ' ';

                        // put title char into title buffer
                        titleBuf[row][col] = c;

                        writtenNonSpace++;
                    }
                }

                // Convert buffers to strings
                textWall = BufferToString(wallBuf);
                displayTitleStr = BufferToString(titleBuf);

                // stash for reuse if you want (instead of re-creating each time)
                wall.Clear();
                wall.Append(textWall);
                displayTitle.Clear();
                displayTitle.Append(displayTitleStr);
            }
            else
            {
                // reuse previous (cheap)
                textWall = wall.ToString();
                displayTitleStr = displayTitle.ToString();
            }

            // draw wall
            var gridW = cols * cellW;
            var gridH = rows * lineH;

            // symmetric leftover space
            var offsetX = (textLayer.Width  - gridW) * 0.5f;
            var offsetY = (textLayer.Height - gridH) * 0.5f;

            offsetX = MathF.Round(offsetX);
            offsetY = MathF.Round(offsetY);

            offsetX = MathF.Max(offsetX, padding.X);
            offsetY = MathF.Max(offsetY, padding.Y);

            // Now draw using these offsets:
            textLayer.DrawText(textWall, offsetX, offsetY, style); //TODO: pass in "effect" method that can set stuff for each character individually
            textLayer.DrawText(displayTitleStr, offsetX, offsetY, titleStyle);
            
            textLayer.Render();


            var glowRadius = 250f;
            float glowIntensity = 1;
            
            window.Layer.Clear(0, 0, 0, 1);
            
            
            window.Layer.DrawCustomRect(
                textLayer.FullRect,
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
            
            window.Layer.DrawCustomRect(
                textLayer.FullRect,
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
        
        app.WaitForClose();
    }

    private static readonly Random _random = new();
    
    private static char[][] CreateRandomWallBuffer(int rows, int cols, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
    {
        var buf = new char[rows][];
        for (var r = 0; r < rows; r++)
        {
            var row = new char[cols];
            for (var c = 0; c < cols; c++)
                row[c] = chars[_random.Next(chars.Length)];
            buf[r] = row;
        }
        return buf;
    }

    private static char[][] CreateBlankBuffer(int rows, int cols)
    {
        var buf = new char[rows][];
        for (var r = 0; r < rows; r++)
        {
            var row = new char[cols];
            Array.Fill(row, ' ');
            buf[r] = row;
        }
        return buf;
    }

    private static string BufferToString(char[][] buf)
    {
        var rows = buf.Length;
        var cols = rows > 0 ? buf[0].Length : 0;

        // rows*(cols + newline)
        var sb = new StringBuilder(rows * (cols + 1));
        for (var r = 0; r < rows; r++)
        {
            sb.Append(buf[r]);
            if (r != rows - 1) sb.Append('\n');
        }
        return sb.ToString();
    }
}
