// File: tests/CodeDraw.Net.Tests.Manual/Prototypes/DrawLayer/CodeDrawLayer.Text.cs
using System.Numerics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    // -------- Text state (render thread) --------
    private bool _textInit;

    private AutoProgram _progText = null!;
    private AutoUniform _uTextRes = null!;
    private AutoUniform _uTextAtlas = null!;

    private uint _textVao;
    private uint _textVbo; // streamed vertices (no instancing -> no glVertexAttribDivisor)

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

        // layout: pos(2), uv(2), color(4) => 8 floats
        uint stride = (uint)(sizeof(float) * 8);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, stride, (void*)0);

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, GLEnum.Float, false, stride, (void*)(sizeof(float) * 2));

        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, GLEnum.Float, false, stride, (void*)(sizeof(float) * 4));

        _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    public Vector2 MeasureText(string text, TextStyle style)
    {
        EnsureTextInit();
        if (_textLayout == null) return Vector2.Zero;

        var m = _textLayout.Measure(text, style);
        return new Vector2(m.Width, m.Height);
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

    private void DrawDebugRect(GL gl, float x, float y, float w, float h, Rgba c, DebugRectMode mode, float outlinePx)
    {
        outlinePx = MathF.Max(1, outlinePx);

        if (mode is DebugRectMode.Fill or DebugRectMode.FillAndOutline)
            ExecRect(gl, x, y, w, h, c.R, c.G, c.B, c.A);

        if (mode is DebugRectMode.Outline or DebugRectMode.FillAndOutline)
        {
            float a = MathF.Min(1f, c.A * 2f);

            ExecRect(gl, x, y, w, outlinePx, c.R, c.G, c.B, a);                 // top
            ExecRect(gl, x, y + h - outlinePx, w, outlinePx, c.R, c.G, c.B, a); // bottom
            ExecRect(gl, x, y, outlinePx, h, c.R, c.G, c.B, a);                 // left
            ExecRect(gl, x + w - outlinePx, y, outlinePx, h, c.R, c.G, c.B, a); // right
        }
    }

    private void ExecText(GL gl, string text, float x, float y, TextStyle style)
    {
        EnsureTextInit();
        if (_textLayout == null || _textAtlasBackend == null) return;

        _glyphScratch.Clear();
        _debugScratch.Clear();

        _textLayout.Layout(text, x, y, style, _glyphScratch, _debugScratch);

        // Debug rects first (glyphs on top)
        for (int i = 0; i < _debugScratch.Count; i++)
        {
            var r = _debugScratch[i];
            DrawDebugRect(gl, r.X, r.Y, r.W, r.H, r.Color, style.DebugRects, style.DebugOutlinePx);
        }

        if (_glyphScratch.Count == 0) return;

        gl.BindVertexArray(_textVao);
        gl.UseProgram(_progText);

        if (_uTextRes >= 0) Uniform2F(gl, _uTextRes, _w, _h);
        if (_uTextAtlas >= 0) gl.Uniform1(_uTextAtlas, 0);

        gl.ActiveTexture(GLEnum.Texture0);

        // Group by atlas page (usually small count)
        var byPage = new Dictionary<int, List<MonospaceLayout.GlyphInstance>>();
        for (int i = 0; i < _glyphScratch.Count; i++)
        {
            var g = _glyphScratch[i];
            if (!byPage.TryGetValue(g.Page, out var list))
            {
                list = new List<MonospaceLayout.GlyphInstance>(256);
                byPage[g.Page] = list;
            }
            list.Add(g);
        }

        foreach (var kv in byPage)
        {
            int page = kv.Key;
            var glyphs = kv.Value;
            if (glyphs.Count == 0) continue;

            uint tex = _textAtlasBackend.GetPageTexture(page);
            if (tex == 0) continue;

            gl.BindTexture(GLEnum.Texture2D, tex);

            int vertCount = glyphs.Count * 6;
            nuint bytes = (nuint)(vertCount * sizeof(float) * 8);

            // Stream vertices
            gl.BindBuffer(GLEnum.ArrayBuffer, _textVbo);
            gl.BufferData(GLEnum.ArrayBuffer, bytes, null, GLEnum.StreamDraw);

            var data = new float[vertCount * 8];
            int o = 0;

            for (int i = 0; i < glyphs.Count; i++)
            {
                var g = glyphs[i];

                float x0 = g.X;
                float y0 = g.Y;
                float x1 = g.X + g.W;
                float y1 = g.Y + g.H;

                float u0 = g.Uv.X;
                float v0 = g.Uv.Y;
                float u1 = g.Uv.Z;
                float v1 = g.Uv.W;

                float r = g.Color.R;
                float gg = g.Color.G;
                float b = g.Color.B;
                float a = g.Color.A;

                // tri 1
                Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
                Write(data, ref o, x1, y0, u1, v0, r, gg, b, a);
                Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);

                // tri 2
                Write(data, ref o, x0, y0, u0, v0, r, gg, b, a);
                Write(data, ref o, x1, y1, u1, v1, r, gg, b, a);
                Write(data, ref o, x0, y1, u0, v1, r, gg, b, a);
            }

            unsafe
            {
                fixed (float* p = data)
                    gl.BufferSubData(GLEnum.ArrayBuffer, 0, bytes, p);
            }

            gl.DrawArrays(GLEnum.Triangles, 0, (uint)vertCount);

            gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        }

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        static void Write(float[] dst, ref int o, float x, float y, float u, float v, float r, float g, float b, float a)
        {
            dst[o++] = x;
            dst[o++] = y;
            dst[o++] = u;
            dst[o++] = v;
            dst[o++] = r;
            dst[o++] = g;
            dst[o++] = b;
            dst[o++] = a;
        }
    }

    // -------- GL atlas backend --------
    private sealed class GlGlyphAtlasBackend(GL gl) : IGlyphAtlasBackend
    {
        private readonly List<(uint tex, int w, int h)> _pages = new();

        public int EnsurePage(int minW, int minH)
        {
            int w = minW;
            int h = minH;

            uint tex = gl.GenTexture();
            gl.BindTexture(GLEnum.Texture2D, tex);

            gl.TexImage2D(
                GLEnum.Texture2D,
                0,
                (int)GLEnum.R8,
                (uint)w,
                (uint)h,
                0,
                GLEnum.Red,
                GLEnum.UnsignedByte,
                null);

            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.BindTexture(GLEnum.Texture2D, 0);

            int idx = _pages.Count;
            _pages.Add((tex, w, h));
            return idx;
        }

        public void UploadAlpha8(int page, int x, int y, int w, int h, ReadOnlySpan<byte> alpha)
        {
            var (tex, _, _) = _pages[page];
            if (tex == 0) return;

            gl.BindTexture(GLEnum.Texture2D, tex);
            gl.PixelStore(GLEnum.UnpackAlignment, 1);

            unsafe
            {
                fixed (byte* p = alpha)
                {
                    gl.TexSubImage2D(
                        GLEnum.Texture2D,
                        0,
                        x, y,
                        (uint)w, (uint)h,
                        GLEnum.Red,
                        GLEnum.UnsignedByte,
                        p);
                }
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
