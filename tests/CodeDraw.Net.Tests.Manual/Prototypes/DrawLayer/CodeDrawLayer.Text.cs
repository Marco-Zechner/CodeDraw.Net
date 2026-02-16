// File: tests/CodeDraw.Net.Tests.Manual/Prototypes/DrawLayer/CodeDrawLayer.Text.cs
using System.Numerics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    // =========================
    // CPU-only text (NO GL)
    // =========================
    private bool _textCpuInit;
    private GlyphCache? _cpuGlyphs;
    private FontMetricsProvider? _cpuMetrics;
    private MonospaceLayout? _cpuLayout;

    private void EnsureTextCpuInit()
    {
        if (_textCpuInit) return;
        _textCpuInit = true;

        // backend=null => NO atlas upload => safe on any thread
        _cpuGlyphs = new GlyphCache(null);
        _cpuMetrics = new FontMetricsProvider(_cpuGlyphs);
        _cpuLayout = new MonospaceLayout(_cpuGlyphs, _cpuMetrics);
    }

    public Vector2 MeasureText(string text, TextStyle style)
    {
        EnsureTextCpuInit();
        if (_cpuLayout == null) return Vector2.Zero;

        var m = _cpuLayout.Measure(text, style);
        return new Vector2(m.Width, m.Height);
    }

    // =========================
    // GPU text (render thread)
    // =========================
    private bool _textInit;

    private AutoProgram _progText = null!;
    private AutoUniform _uTextRes = null!;
    private AutoUniform _uTextAtlas = null!;

    private uint _textVao;
    private uint _textVbo;

    private GlGlyphAtlasBackend? _textAtlasBackend;
    private GlyphCache? _textGlyphCache;
    private FontMetricsProvider? _textMetrics;
    private MonospaceLayout? _textLayout;

    private readonly List<MonospaceLayout.GlyphInstance> _glyphScratch = new(2048);
    private readonly List<MonospaceLayout.DebugRect> _debugScratch = new(2048);
    private readonly List<MonospaceLayout.GlyphInstance> _glyphSorted = new(2048);

    private void EnsureTextInit()
    {
        if (_textInit) return;
        _textInit = true;

        // MUST be called on render thread with current context
        _progText = new AutoProgram(this, ShaderPath.Engine("text"));
        _uTextRes = new AutoUniform(_gl, this, _progText, "uRes");
        _uTextAtlas = new AutoUniform(_gl, this, _progText, "uAtlas");

        _textAtlasBackend = new GlGlyphAtlasBackend(_gl);
        _textGlyphCache = new GlyphCache(_textAtlasBackend);
        _textMetrics = new FontMetricsProvider(_textGlyphCache);
        _textLayout = new MonospaceLayout(_textGlyphCache, _textMetrics);

        _textVao = _gl.GenVertexArray();
        _textVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(GLEnum.ArrayBuffer, _textVbo);

        // pos(2), uv(2), color(4) => 8 floats
        var stride = (uint)(sizeof(float) * 8);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, stride, (void*)0);

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, GLEnum.Float, false, stride, (void*)(sizeof(float) * 2));

        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, GLEnum.Float, false, stride, (void*)(sizeof(float) * 4));

        _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public void DrawText(string text, float x, float y, TextStyle style)
    {
        if (_disposed) return;
        if (string.IsNullOrEmpty(text)) return;

        // snapshot style to avoid mutation during render-thread exec
        var copy = new TextStyle //TODO: make TextStyle immutable and remove this copying, or move it into the TextStyle class
        {
            Font = style.Font,
            SizePx = style.SizePx,
            Align = style.Align,
            VAlign = style.VAlign,
            Color = style.Color,

            Background = style.Background,

            ExtraAbovePx = style.ExtraAbovePx,
            ExtraBelowPx = style.ExtraBelowPx,
            ExtraLineGapPx = style.ExtraLineGapPx,
            ExtraCellGapPx = style.ExtraCellGapPx,
            OverrideCellWidthPx = style.OverrideCellWidthPx,
            OverrideLineHeightPx = style.OverrideLineHeightPx,
            MonospaceSnapLineAlignToCells = style.MonospaceSnapLineAlignToCells,
            DebugMode = style.DebugMode,
            DebugRects = style.DebugRects,
            DebugOutlinePx = style.DebugOutlinePx,

            BackgroundMode = style.BackgroundMode,
            BackgroundPaddingPx = style.BackgroundPaddingPx,
            BackgroundIncludeSpaces = style.BackgroundIncludeSpaces,
            BackgroundBlendMode = style.BackgroundBlendMode,
            FontBlendMode = style.FontBlendMode,
        };

        Enqueue(new CmdText { Text = text, X = x, Y = y, Style = copy });
    }

    private sealed class CmdText : ICmd
    {
        public string Text = "";
        public float X, Y;
        public TextStyle Style = null!;
        public void Exec(GL gl, CodeDrawLayer self) => self.ExecText(gl, Text, X, Y, Style);
    }

    private void ExecText(GL gl, string text, float x, float y, TextStyle style)
    {
        EnsureTextInit();
        if (_textLayout == null || _textAtlasBackend == null) return;

        _glyphScratch.Clear();
        _debugScratch.Clear();

        _textLayout.Layout(text, x, y, style, _glyphScratch, _debugScratch);
        
        if (_glyphScratch.Count == 0)
        {
            // Try to draw Debug rects first
            foreach (var r in _debugScratch)
                DrawDebugRect(gl, r.X, r.Y, r.W, r.H, r.Color, style.DebugRects, style.DebugOutlinePx);
            return;
        }

        var userBlend = GetBlendMode();

        // Only run if Background is set and BackgroundMode != None.
        if (style.Background is { } bg && style.BackgroundMode != TextBackgroundMode.None)
        {
            ApplyBlendMode(style.BackgroundBlendMode);

            DrawTextBackgrounds(gl, text, x, y, style, bg);
        }
        
        // ---- Glyphs ----
        // Apply user-chosen font blending, independent of "global layer blend"
        ApplyBlendMode(style.FontBlendMode);
        
        // Draw debug rects with fontBlendMode and on top of background
        foreach (var r in _debugScratch)
            DrawDebugRect(gl, r.X, r.Y, r.W, r.H, r.Color, style.DebugRects, style.DebugOutlinePx);
        
        gl.BindVertexArray(_textVao);
        gl.UseProgram(_progText);

        if (_uTextRes >= 0) Uniform2F(gl, _uTextRes, _w, _h);
        if (_uTextAtlas >= 0) gl.Uniform1(_uTextAtlas, 0);

        gl.ActiveTexture(GLEnum.Texture0);

        // Sort by page and draw page-runs (no Dictionary allocs)
        _glyphSorted.Clear();
        _glyphSorted.AddRange(_glyphScratch);
        _glyphSorted.Sort(static (a, b) => a.Page.CompareTo(b.Page));

        int start = 0;
        while (start < _glyphSorted.Count)
        {
            int page = _glyphSorted[start].Page;
            int end = start + 1;
            while (end < _glyphSorted.Count && _glyphSorted[end].Page == page) end++;

            uint tex = _textAtlasBackend.GetPageTexture(page);
            if (tex != 0)
            {
                gl.BindTexture(GLEnum.Texture2D, tex);
                DrawGlyphRun(gl, _glyphSorted, start, end);
            }

            start = end;
        }

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        SetBlendMode(userBlend);
    }
    
    private void DrawGlyphRun(GL gl, List<MonospaceLayout.GlyphInstance> glyphs, int start, int end)
    {
        var glyphCount = end - start;
        var vertCount = glyphCount * 6;

        var bytes = (nuint)(vertCount * sizeof(float) * 8);

        gl.BindBuffer(GLEnum.ArrayBuffer, _textVbo);
        gl.BufferData(GLEnum.ArrayBuffer, bytes, null, GLEnum.StreamDraw);

        // You can pool this later; keep simple for now.
        var data = new float[vertCount * 8];
        var o = 0;

        for (var i = start; i < end; i++)
        {
            var g = glyphs[i];

            var x0 = g.X;
            var y0 = g.Y;
            var x1 = g.X + g.W;
            var y1 = g.Y + g.H;

            var u0 = g.Uv.X;
            var v0 = g.Uv.Y;
            var u1 = g.Uv.Z;
            var v1 = g.Uv.W;

            float r = g.Color.R, gg = g.Color.G, b = g.Color.B, a = g.Color.A;

            // tri 1
            Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
            Write(data, ref o, x1, y0, u1, v0, r, gg, b, a);
            Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);

            // tri 2
            Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
            Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);
            Write(data, ref o, x0, y1, u0, v1, r, gg, b, a);
        }

        fixed (float* p = data)
            gl.BufferSubData(GLEnum.ArrayBuffer, 0, bytes, p);

        gl.DrawArrays(GLEnum.Triangles, 0, (uint)vertCount);

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);

        static void Write(float[] dst, ref int o, float x, float y, float u, float v, float r, float g, float b, float a)
        {
            dst[o++] = x; dst[o++] = y;
            dst[o++] = u; dst[o++] = v;
            dst[o++] = r; dst[o++] = g; dst[o++] = b; dst[o++] = a;
        }
    }
    

    private void DrawDebugRect(GL gl, float x, float y, float w, float h, Rgba c, DebugRectMode mode, float outlinePx)
    {
        outlinePx = MathF.Max(1, outlinePx);

        if (mode is DebugRectMode.Fill or DebugRectMode.FillAndOutline)
            ExecRect(gl, x, y, w, h, c.R, c.G, c.B, c.A);

        if (mode is not (DebugRectMode.Outline or DebugRectMode.FillAndOutline)) return;

        var a = MathF.Min(1f, c.A * 2f);

        ExecRect(gl, x, y, w, outlinePx, c.R, c.G, c.B, a);
        ExecRect(gl, x, y + h - outlinePx, w, outlinePx, c.R, c.G, c.B, a);
        ExecRect(gl, x, y, outlinePx, h, c.R, c.G, c.B, a);
        ExecRect(gl, x + w - outlinePx, y, outlinePx, h, c.R, c.G, c.B, a);
    }

    // -------- GL atlas backend --------
    private sealed class GlGlyphAtlasBackend(GL gl) : IGlyphAtlasBackend
    {
        private readonly List<(uint tex, int w, int h)> _pages = new();

        public int EnsurePage(int minW, int minH)
        {
            var tex = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, tex);

            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.R8,
                (uint)minW, (uint)minH, 0, GLEnum.Red, GLEnum.UnsignedByte, null);

            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.BindTexture(GLEnum.Texture2D, 0);

            var idx = _pages.Count;
            _pages.Add((tex, minW, minH));
            return idx;
        }

        public void UploadAlpha8(int page, int x, int y, int w, int h, ReadOnlySpan<byte> alpha)
        {
            var (tex, _, _) = _pages[page];
            if (tex == 0) return;

            gl.BindTexture(GLEnum.Texture2D, tex);
            gl.PixelStore(GLEnum.UnpackAlignment, 1);

            fixed (byte* p = alpha)
            {
                gl.TexSubImage2D(GLEnum.Texture2D, 0, x, y, (uint)w, (uint)h,
                    GLEnum.Red, GLEnum.UnsignedByte, p);
            }

            gl.PixelStore(GLEnum.UnpackAlignment, 4);
            gl.BindTexture(GLEnum.Texture2D, 0);
        }

        public Vector2 GetPageSize(int page)
        {
            var (_, w, h) = _pages[page];
            return new Vector2(w, h);
        }

        public uint GetPageTexture(int page) => _pages[page].tex;
    }
    
    private void DrawTextBackgrounds(GL gl, string text, float x, float y, TextStyle style, Rgba bg)
    {
        // We need cellW/lineH/baselineFromTop and the same anchor math as Layout().
        _textLayout!.GetCellMetrics(style, out var cellW, out var lineH, out var baselineFromTop);

        if (cellW <= 0 || lineH <= 0) return;

        // line columns
        var lineCols = new List<int>(32);
        var cols = 0;
        foreach (var c in text.Where(c => c != '\r'))
        {
            if (c == '\n')
            {
                lineCols.Add(cols);
                cols = 0;
                continue;
            }
            cols++;
        }
        lineCols.Add(cols);

        var maxCols = lineCols.Prepend(0).Max();

        var totalW = maxCols * cellW;
        var totalH = lineCols.Count * lineH;

        var ax = style.Align switch
        {
            TextAlign.Left => 0,
            TextAlign.Center => totalW * 0.5f,
            TextAlign.Right => totalW,
            _ => 0
        };

        var ay = style.VAlign switch
        {
            TextVAlign.Top => 0,
            TextVAlign.Middle => totalH * 0.5f,
            TextVAlign.Bottom => totalH,
            _ => 0
        };

        var originX = x - ax;
        var originY = y - ay;

        var pad = MathF.Max(0, style.BackgroundPaddingPx);

        var includeSpaces = style.BackgroundIncludeSpaces;

        switch (style.BackgroundMode)
        {
            case TextBackgroundMode.PerLine:
            {
                for (var row = 0; row < lineCols.Count; row++)
                {
                    var lc = lineCols[row];
                    if (lc <= 0) continue;

                    var lineOff = LineOffsetPx(row);
                    var bx = originX + lineOff;
                    var by = originY + row * lineH;
                    var bw = lc * cellW;

                    ExecRect(gl, bx - pad, by - pad, bw + 2 * pad, lineH + 2 * pad, bg.R, bg.G, bg.B, bg.A);
                }
                break;
            }

            case TextBackgroundMode.PerCell:
            {
                var col = 0;
                var row = 0;

                foreach (var c in text.Where(c => c != '\r'))
                {
                    if (c == '\n')
                    {
                        col = 0;
                        row++;
                        continue;
                    }

                    if (!includeSpaces && c == ' ')
                    {
                        col++;
                        continue;
                    }

                    var lineOff = LineOffsetPx(row);
                    var cx = originX + lineOff + col * cellW;
                    var cy = originY + row * lineH;

                    ExecRect(gl, cx - pad, cy - pad, cellW + 2 * pad, lineH + 2 * pad, bg.R, bg.G, bg.B, bg.A);

                    col++;
                }
                break;
            }

            case TextBackgroundMode.PerGlyphBox:
            {
                // You already computed glyph positions in _glyphScratch; use those.
                // This backgrounds only where glyph bitmap exists (nice for "tight highlight").
                foreach (var g in _glyphScratch)
                    ExecRect(gl, g.X - pad, g.Y - pad, g.W + 2 * pad, g.H + 2 * pad, bg.R, bg.G, bg.B, bg.A);
                break;
            }
        }

        return;

        // Per-line offsets like Layout()
        float LineOffsetPx(int r)
        {
            var lc = (r >= 0 && r < lineCols.Count) ? lineCols[r] : 0;
            var diff = maxCols - lc;

            if (style.Align == TextAlign.Left) return 0;

            if (style.MonospaceSnapLineAlignToCells)
            {
                var offCols = style.Align == TextAlign.Center ? (diff / 2) : diff;
                return offCols * cellW;
            }

            var off = style.Align == TextAlign.Center ? (diff * 0.5f) : diff;
            return off * cellW;
        }
    }
}
