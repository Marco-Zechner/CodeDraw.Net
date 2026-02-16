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
        var copy = new TextStyle
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
            DebugOutlinePx = style.DebugOutlinePx
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

        // Debug rects first (glyphs on top)
        foreach (var r in _debugScratch)
        {
            DrawDebugRect(gl, r.X, r.Y, r.W, r.H, r.Color, style.DebugRects, style.DebugOutlinePx);
        }

        if (_glyphScratch.Count == 0) return;

        if (style.Background is { } bg)
        {
            var m = _textLayout.Measure(text, style); // returns width/height

            var ax = style.Align switch
            {
                TextAlign.Left => 0,
                TextAlign.Center => m.Width * 0.5f,
                TextAlign.Right => m.Width,
                _ => 0,
            };

            var ay = style.VAlign switch
            {
                TextVAlign.Top => 0,
                TextVAlign.Middle => m.Height * 0.5f,
                TextVAlign.Bottom => m.Height,
                _ => 0,
            };

            var bx = x - ax;
            var by = y - ay;

            ExecRect(gl, bx, by, m.Width, m.Height, bg.R, bg.G, bg.B, bg.A);
        }
        
        gl.BindVertexArray(_textVao);
        gl.UseProgram(_progText);

        if (_uTextRes >= 0) Uniform2F(gl, _uTextRes, _w, _h);
        if (_uTextAtlas >= 0) gl.Uniform1(_uTextAtlas, 0);

        gl.ActiveTexture(GLEnum.Texture0);

        // Group by atlas page
        var byPage = new Dictionary<int, List<MonospaceLayout.GlyphInstance>>();
        foreach (var g in _glyphScratch)
        {
            if (!byPage.TryGetValue(g.Page, out var list))
            {
                list = new List<MonospaceLayout.GlyphInstance>(256);
                byPage[g.Page] = list;
            }
            list.Add(g);
        }

        foreach (var kv in byPage)
        {
            var page = kv.Key;
            var glyphs = kv.Value;
            if (glyphs.Count == 0) continue;

            var tex = _textAtlasBackend.GetPageTexture(page);
            if (tex == 0) continue;

            gl.BindTexture(GLEnum.Texture2D, tex);

            var vertCount = glyphs.Count * 6;
            var bytes = (nuint)(vertCount * sizeof(float) * 8);

            gl.BindBuffer(GLEnum.ArrayBuffer, _textVbo);
            gl.BufferData(GLEnum.ArrayBuffer, bytes, null, GLEnum.StreamDraw);

            var data = new float[vertCount * 8];
            var o = 0;

            foreach (var g in glyphs)
            {
                var x0 = g.X;
                var y0 = g.Y;
                var x1 = g.X + g.W;
                var y1 = g.Y + g.H;

                var u0 = g.Uv.X;
                var v0 = g.Uv.Y;
                var u1 = g.Uv.Z;
                var v1 = g.Uv.W;

                float r = g.Color.R, gg = g.Color.G, b = g.Color.B, a = g.Color.A;

                Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
                Write(data, ref o, x1, y0, u1, v0, r, gg, b, a);
                Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);

                Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
                Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);
                Write(data, ref o, x0, y1, u0, v1, r, gg, b, a);
            }

            fixed (float* p = data)
                gl.BufferSubData(GLEnum.ArrayBuffer, 0, bytes, p);

            gl.DrawArrays(GLEnum.Triangles, 0, (uint)vertCount);

            gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        }

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        return;
        
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

        if (mode is DebugRectMode.Outline or DebugRectMode.FillAndOutline)
        {
            var a = MathF.Min(1f, c.A * 2f);

            ExecRect(gl, x, y, w, outlinePx, c.R, c.G, c.B, a);
            ExecRect(gl, x, y + h - outlinePx, w, outlinePx, c.R, c.G, c.B, a);
            ExecRect(gl, x, y, outlinePx, h, c.R, c.G, c.B, a);
            ExecRect(gl, x + w - outlinePx, y, outlinePx, h, c.R, c.G, c.B, a);
        }
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
}
