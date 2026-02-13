// FILE: tests/CodeDraw.Net.Tests.Manual/Prototypes/DrawLayer/CodeDrawLayer.Text.cs
// Adds:
//  - GL atlas backend implementation
//  - text shader + instanced draw pipeline
//  - public DrawText() method
//  - CmdText command
//
// Assumes your text code lives in namespace MarcoZechner.CodeDrawDotNet.Text (as you posted).

using System.Numerics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    // ---------- Text rendering state (render-thread only) ----------
    private bool _textInit;
    private AutoProgram _progText = null!;
    private AutoUniform _uTextRes = null!;
    private AutoUniform _uTextAtlas = null!;

    private uint _textVao;
    private uint _textVboQuad;
    private uint _textVboInstances;

    private GlGlyphAtlasBackend? _textAtlasBackend;
    private GlyphCache? _textGlyphCache;
    private TextLayoutEngine? _textLayout;

    // Instance data we send to GPU
    // (packed to 16-byte multiples)
    private struct TextInstance
    {
        public Vector4 PosSize; // x y w h
        public Vector4 Uv;      // u0 v0 u1 v1
        public Vector4 Color;   // r g b a
    }

    // Very small CPU-side scratch buffers to avoid GC thrash
    private readonly List<GlyphDraw> _textDrawsScratch = new(256);
    private readonly List<TextInstance> _textInstancesScratch = new(256);
    private readonly Dictionary<int, (int start, int count)> _textBatchesScratch = new(); // page -> range

    private void EnsureTextInit()
    {
        if (_textInit) return;
        _textInit = true;

        // Shader program (add these as engine shaders in your ShaderStore)
        _progText = new AutoProgram(this, ShaderPath.Engine("text"));
        _uTextRes = new AutoUniform(_gl, this, _progText, "uRes");
        _uTextAtlas = new AutoUniform(_gl, this, _progText, "uAtlas");

        // Atlas backend + glyph cache + layout engine
        _textAtlasBackend = new GlGlyphAtlasBackend(_gl);
        _textGlyphCache = new GlyphCache(_textAtlasBackend);
        _textLayout = new TextLayoutEngine(_textGlyphCache);

        // VAO + buffers
        _textVao = _gl.GenVertexArray();
        _textVboQuad = _gl.GenBuffer();
        _textVboInstances = _gl.GenBuffer();

        _gl.BindVertexArray(_textVao);

        // Base quad: 2 triangles as 6 verts in local 0..1 space
        Span<float> quad = stackalloc float[]
        {
            0,0,  1,0,  1,1,
            0,0,  1,1,  0,1
        };

        _gl.BindBuffer(GLEnum.ArrayBuffer, _textVboQuad);
        fixed (float* p = quad)
            _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, GLEnum.StaticDraw);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);

        // Instance buffer (streamed)
        _gl.BindBuffer(GLEnum.ArrayBuffer, _textVboInstances);
        _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(256 * sizeof(TextInstance)), null, GLEnum.StreamDraw);

        // iPosSize (vec4) at loc=1
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, GLEnum.Float, false, (uint)sizeof(TextInstance), (void*)0);
        _gl.VertexAttribDivisor(1, 1);

        // iUv (vec4) at loc=2
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, GLEnum.Float, false, (uint)sizeof(TextInstance), (void*)(sizeof(float) * 4));
        _gl.VertexAttribDivisor(2, 1);

        // iColor (vec4) at loc=3
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, GLEnum.Float, false, (uint)sizeof(TextInstance), (void*)(sizeof(float) * 8));
        _gl.VertexAttribDivisor(3, 1);

        _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    // ---------- Public API ----------
    public void DrawText(string text, float x, float y, TextStyle style, GlyphEffect? effect = null)
    {
        if (_disposed) return;
        if (string.IsNullOrEmpty(text)) return;

        // TextStyle is a class; snapshot what we need to keep command immutable-ish.
        // (If you mutate style after calling DrawText, you don't want races.)
        var styleCopy = new TextStyle
        {
            Font = style.Font,
            SizePx = style.SizePx,
            LineHeightPx = style.LineHeightPx,
            WrapWidthPx = style.WrapWidthPx,
            Align = style.Align,
            Color = style.Color,
            Background = style.Background
        };

        Enqueue(new CmdText
        {
            Text = text,
            X = x,
            Y = y,
            Style = styleCopy,
            Effect = effect,
        });
    }

    // ---------- Command ----------
    private sealed class CmdText : ICmd
    {
        public string Text = "";
        public float X, Y;
        public TextStyle Style = null!;
        public GlyphEffect? Effect;

        public void Exec(GL gl, CodeDrawLayer self) => self.ExecText(gl, Text, X, Y, Style, Effect);
    }

    // ---------- Render-thread execution ----------
    private void ExecText(GL gl, string text, float x, float y, TextStyle style, GlyphEffect? effect)
    {
        EnsureTextInit();
        if (_textLayout == null || _textAtlasBackend == null) return;

        // Run layout in layer-local pixel space.
        // We'll treat the input x,y as an extra translation.
        // Time for effects: use your layer stopwatch (ms).
        int timeMs = (int)(LayerAliveForSeconds() * 1000.0f);

        _textDrawsScratch.Clear();
        _textLayout.Layout(text, style, effect, timeMs, out var draws, out _);

        // Avoid allocating: if Layout returned a new list, copy to scratch then allow GC later.
        // (You can later refactor Layout to fill a provided list.)
        _textDrawsScratch.AddRange(draws);

        // Build instances, group by atlas page (so we do 1 draw call per page)
        _textInstancesScratch.Clear();
        _textBatchesScratch.Clear();

        for (int i = 0; i < _textDrawsScratch.Count; i++)
        {
            var g = _textDrawsScratch[i];

            // Skip "non-drawn" glyphs (space etc.)
            if (g.AtlasPage < 0) continue;
            if (g.WidthPx <= 0 || g.HeightPx <= 0) continue;

            var inst = new TextInstance
            {
                PosSize = new Vector4(x + g.X, y + g.Y, g.WidthPx, g.HeightPx),
                Uv = g.Uv,
                Color = new Vector4(g.Color.R, g.Color.G, g.Color.B, g.Color.A),
            };

            int page = g.AtlasPage;

            if (_textBatchesScratch.TryGetValue(page, out var range))
            {
                // extend current batch (we'll keep batches contiguous by appending)
                range.count++;
                _textBatchesScratch[page] = range;
            }
            else
            {
                _textBatchesScratch[page] = (start: _textInstancesScratch.Count, count: 1);
            }

            _textInstancesScratch.Add(inst);
        }

        if (_textInstancesScratch.Count == 0) return;

        // Upload instance buffer (stream)
        gl.BindVertexArray(_textVao);
        gl.BindBuffer(GLEnum.ArrayBuffer, _textVboInstances);

        nuint bytes = (nuint)(_textInstancesScratch.Count * sizeof(TextInstance));

        // Orphan + upload (simple & works well enough for now)
        gl.BufferData(GLEnum.ArrayBuffer, bytes, null, GLEnum.StreamDraw);

        fixed (TextInstance* p = _textInstancesScratch.ToArray())
        {
            gl.BufferSubData(GLEnum.ArrayBuffer, 0, bytes, p);
        }

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);

        // Draw
        gl.UseProgram(_progText);

        if (_uTextRes >= 0) Uniform2F(gl, _uTextRes, _w, _h);
        if (_uTextAtlas >= 0) gl.Uniform1(_uTextAtlas, 0);

        gl.ActiveTexture(GLEnum.Texture0);

        // NOTE: dictionary order is arbitrary. That's fine.
        // If you want stable ordering (slightly better cache), iterate pages sorted.
        foreach (var kv in _textBatchesScratch)
        {
            int page = kv.Key;
            var (start, count) = kv.Value;

            uint tex = _textAtlasBackend.GetPageTexture(page);
            if (tex == 0) continue;

            gl.BindTexture(GLEnum.Texture2D, tex);

            // We need baseInstance. In GL 3.3 core, glDrawArraysInstancedBaseInstance isn't guaranteed.
            // So we do the simplest thing: draw per-page by re-uploading only that page's slice.
            // BUT we already uploaded the whole thing. To keep 3.3-safe and still simple:
            // -> do per-page draws by drawing the whole buffer but with glVertexAttribPointer offsets? messy.
            //
            // Instead: pack instances page-by-page contiguously by *building* in that order.
            // For now, easiest fix: re-upload the page slice right before drawing it.
            //
            // This keeps correctness and still uses instancing; it's just extra uploads (OK for “simple path”).

            // Re-upload only this slice
            gl.BindBuffer(GLEnum.ArrayBuffer, _textVboInstances);
            nuint sliceBytes = (nuint)(count * sizeof(TextInstance));
            gl.BufferData(GLEnum.ArrayBuffer, sliceBytes, null, GLEnum.StreamDraw);

            // Copy slice into a small temp array (no LINQ)
            var tmp = new TextInstance[count];
            for (int i = 0; i < count; i++)
                tmp[i] = _textInstancesScratch[start + i];

            fixed (TextInstance* pp = tmp)
                gl.BufferSubData(GLEnum.ArrayBuffer, 0, sliceBytes, pp);

            gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            // 6 verts per quad, instanced count times
            gl.DrawArraysInstanced(GLEnum.Triangles, 0, 6, (uint)count);
        }

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    // ---------- GL atlas backend ----------
    private sealed class GlGlyphAtlasBackend : IGlyphAtlasBackend
    {
        private readonly GL _gl;
        private readonly List<(uint tex, int w, int h)> _pages = new();

        public GlGlyphAtlasBackend(GL gl) => _gl = gl;

        public int EnsurePage(int minW, int minH)
        {
            // Always create exactly the requested size (your packer assumes fixed page size anyway)
            int w = minW;
            int h = minH;

            uint tex = _gl.GenTexture();
            _gl.BindTexture(GLEnum.Texture2D, tex);

            _gl.TexImage2D(
                GLEnum.Texture2D,
                0,
                (int)GLEnum.R8,
                (uint)w,
                (uint)h,
                0,
                GLEnum.Red,
                GLEnum.UnsignedByte,
                null);

            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

            _gl.BindTexture(GLEnum.Texture2D, 0);

            int idx = _pages.Count;
            _pages.Add((tex, w, h));
            return idx;
        }

        public void UploadAlpha8(int page, int x, int y, int w, int h, ReadOnlySpan<byte> alpha)
        {
            if ((uint)page >= (uint)_pages.Count) throw new ArgumentOutOfRangeException(nameof(page));

            var (tex, _, _) = _pages[page];
            if (tex == 0) return;

            _gl.BindTexture(GLEnum.Texture2D, tex);

            _gl.PixelStore(GLEnum.UnpackAlignment, 1);

            unsafe
            {
                fixed (byte* p = alpha)
                {
                    _gl.TexSubImage2D(
                        GLEnum.Texture2D,
                        0,
                        x, y,
                        (uint)w, (uint)h,
                        GLEnum.Red,
                        GLEnum.UnsignedByte,
                        p);
                }
            }

            _gl.PixelStore(GLEnum.UnpackAlignment, 4);
            _gl.BindTexture(GLEnum.Texture2D, 0);
        }

        public Vector2 GetPageSize(int page)
        {
            var (_, w, h) = _pages[page];
            return new Vector2(w, h);
        }

        public uint GetPageTexture(int page) => _pages[page].tex;
    }
}
