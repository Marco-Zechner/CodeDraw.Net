using System.Buffers;
using System.Numerics;
using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text
{
    public enum TextAlign { Left, Center, Right }

    public readonly record struct FontRef(string Path)
    {
        public static FontRef FromFile(string path) => new FontRef(path);
    }

    public sealed class TextStyle
    {
        public FontRef Font { get; set; }
        public float SizePx { get; set; } = 16;
        public float LineHeightPx { get; set; } = 0; // 0 => auto
        public float WrapWidthPx { get; set; } = 0;  // 0 => no wrap
        public TextAlign Align { get; set; } = TextAlign.Left;

        public Rgba Color { get; set; } = new Rgba(1, 1, 1, 1);
        public Rgba? Background { get; set; } = null;
    }

    public readonly record struct Rgba(float R, float G, float B, float A);

    public readonly record struct TextMetrics(float Width, float Height);

    // What we feed into the renderer per glyph (before effect)
    public struct GlyphDraw
    {
        public int Index;           // character index in entire text
        public char Char;

        public float X;
        public float Y;
        public float WidthPx;
        public float HeightPx;

        public Vector4 Uv;          // u0 v0 u1 v1
        public int AtlasPage;
        
        public Rgba Color;
        public Rgba Background;
        public bool HasBackground;

        public float RotationRad;   // optional
    }

    public readonly struct GlyphEffectContext
    {
        public readonly int TimeMs;
        public GlyphEffectContext(int timeMs) => TimeMs = timeMs;
    }

    public delegate void GlyphEffect(ref GlyphDraw g, in GlyphEffectContext ctx);

    // ---- SharpFont management ----

    public sealed class FontLibrary : IDisposable
    {
        public Library Lib { get; } = new Library();
        public void Dispose() => Lib.Dispose();
    }

    public sealed class FontFace : IDisposable
    {
        public Face Face { get; }
        public string Path { get; }

        public FontFace(FontLibrary lib, string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            Path = System.IO.Path.GetFullPath(path);
            Face = new Face(lib.Lib, Path);
        }

        public void SetPixelSize(uint px)
        {
            // width=0 means "compute from height" in FreeType terms
            Face.SetPixelSizes(0, px);
        }

        public void Dispose() => Face.Dispose();
    }

    // ---- Glyph cache + atlas ----

    public readonly struct GlyphKey : IEquatable<GlyphKey>
    {
        public readonly string FontPath;
        public readonly int SizePx;
        public readonly uint GlyphIndex;

        public GlyphKey(string fontPath, int sizePx, uint glyphIndex)
        {
            FontPath = fontPath;
            SizePx = sizePx;
            GlyphIndex = glyphIndex;
        }

        public bool Equals(GlyphKey other) =>
            SizePx == other.SizePx &&
            GlyphIndex == other.GlyphIndex &&
            string.Equals(FontPath, other.FontPath, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is GlyphKey o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = StringComparer.OrdinalIgnoreCase.GetHashCode(FontPath);
                h = (h * 397) ^ SizePx;
                h = (h * 397) ^ (int)GlyphIndex;
                return h;
            }
        }
    }

    public sealed class GlyphInfo
    {
        // Placement in atlas
        public int AtlasPage;
        public int X, Y, W, H;
        public Vector4 Uv;

        // Metrics (pixel space)
        public float AdvanceX;
        public float BearingX;
        public float BearingY; // top bearing
        public float BitmapW;
        public float BitmapH;
    }

    public interface IGlyphAtlasBackend
    {
        // Upload grayscale alpha bitmap into atlas page at x,y
        void UploadAlpha8(int page, int x, int y, int w, int h, ReadOnlySpan<byte> alpha);
        Vector2 GetPageSize(int page);
        int EnsurePage(int minW, int minH); // returns page index
    }

    // A very simple packer: shelves (good enough to start).
    internal sealed class ShelfPacker
    {
        private readonly int _w, _h;
        private int _curX, _curY, _shelfH;

        public ShelfPacker(int w, int h) { _w = w; _h = h; }

        public bool TryAlloc(int w, int h, out int x, out int y)
        {
            x = y = 0;
            if (w > _w || h > _h) return false;

            if (_curX + w > _w)
            {
                _curX = 0;
                _curY += _shelfH;
                _shelfH = 0;
            }

            if (_curY + h > _h) return false;

            x = _curX;
            y = _curY;

            _curX += w;
            _shelfH = Math.Max(_shelfH, h);
            return true;
        }
    }

    public sealed class GlyphAtlas
    {
        private readonly IGlyphAtlasBackend _backend;
        private readonly int _pageW;
        private readonly int _pageH;

        private readonly List<ShelfPacker> _packers = new();

        public GlyphAtlas(IGlyphAtlasBackend backend, int pageW = 1024, int pageH = 1024)
        {
            _backend = backend;
            _pageW = pageW;
            _pageH = pageH;
        }

        public int Allocate(int w, int h, out int page, out int x, out int y)
        {
            for (int i = 0; i < _packers.Count; i++)
            {
                if (_packers[i].TryAlloc(w, h, out x, out y))
                {
                    page = i;
                    return i;
                }
            }

            page = _backend.EnsurePage(_pageW, _pageH);
            while (_packers.Count <= page) _packers.Add(new ShelfPacker(_pageW, _pageH));

            if (!_packers[page].TryAlloc(w, h, out x, out y))
                throw new InvalidOperationException("New atlas page cannot fit glyph (too big).");

            return page;
        }
    }

    public sealed class GlyphCache : IDisposable
    {
        private readonly FontLibrary _lib = new();

        private sealed class FaceKeyComparer : IEqualityComparer<(string path, int sizePx)>
        {
            public static readonly FaceKeyComparer Instance = new();

            public bool Equals((string path, int sizePx) x, (string path, int sizePx) y)
                => x.sizePx == y.sizePx &&
                   StringComparer.OrdinalIgnoreCase.Equals(x.path, y.path);

            public int GetHashCode((string path, int sizePx) obj)
            {
                unchecked
                {
                    int h = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.path);
                    h = (h * 397) ^ obj.sizePx;
                    return h;
                }
            }
        }

        private readonly Dictionary<(string path, int sizePx), FontFace> _faces = new(FaceKeyComparer.Instance);
        private readonly Dictionary<GlyphKey, GlyphInfo> _glyphs = new();

        private readonly GlyphAtlas _atlas;
        private readonly IGlyphAtlasBackend _backend;

        // padding helps avoid sampling bleeding
        private const int PAD = 1;

        public GlyphCache(IGlyphAtlasBackend backend)
        {
            _backend = backend;
            _atlas = new GlyphAtlas(backend);
        }

        public void Dispose()
        {
            foreach (var f in _faces.Values) f.Dispose();
            _faces.Clear();
            _lib.Dispose();
        }

        private FontFace GetFace(FontRef font, int sizePx)
        {
            var path = Path.GetFullPath(font.Path);
            var key = (path, sizePx);

            if (_faces.TryGetValue(key, out var face)) return face;

            face = new FontFace(_lib, path);
            face.SetPixelSize((uint)sizePx);
            _faces[key] = face;
            return face;
        }

        public GlyphInfo GetGlyph(FontRef font, int sizePx, char c)
        {
            var face = GetFace(font, sizePx);

            uint glyphIndex = face.Face.GetCharIndex(c);
            var gk = new GlyphKey(face.Path, sizePx, glyphIndex);

            if (_glyphs.TryGetValue(gk, out var cached))
                return cached;

            // Load glyph (render bitmap)
            face.Face.LoadGlyph(glyphIndex, LoadFlags.Render, LoadTarget.Normal);

            var slot = face.Face.Glyph;
            var bmp = slot.Bitmap;

            int gw = bmp.Width;
            int gh = bmp.Rows;

            // even if bitmap is 0x0, we still want metrics (advance)
            float advanceX = slot.Advance.X.ToSingle();

            if (advanceX < 0.01f)
            {
                // HorizontalAdvance is in 26.6 fixed-point in many FreeType APIs
                advanceX = slot.Metrics.HorizontalAdvance.ToSingle() / 64f;
            }

            var info = new GlyphInfo
            {
                AdvanceX = advanceX,
                BearingX = slot.BitmapLeft,
                BearingY = slot.BitmapTop,
                BitmapW = gw,
                BitmapH = gh,
            };

            if (gw > 0 && gh > 0)
            {
                int allocW = gw + PAD * 2;
                int allocH = gh + PAD * 2;

                _atlas.Allocate(allocW, allocH, out int page, out int x, out int y);

                // Copy row-by-row because pitch can differ.
                byte[] tmp = ArrayPool<byte>.Shared.Rent(gw * gh);
                try
                {
                    unsafe
                    {
                        var srcPtr = (byte*)bmp.Buffer;
                        int pitch = bmp.Pitch;

                        for (int row = 0; row < gh; row++)
                        {
                            var srcRow = srcPtr + row * pitch;
                            var dstOff = row * gw;
                            for (int col = 0; col < gw; col++)
                                tmp[dstOff + col] = srcRow[col];
                        }
                    }

                    // upload into atlas with padding offset
                    _backend.UploadAlpha8(page, x + PAD, y + PAD, gw, gh, tmp.AsSpan(0, gw * gh));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tmp);
                }

                info.AtlasPage = page;
                info.X = x + PAD;
                info.Y = y + PAD;
                info.W = gw;
                info.H = gh;

                var pageSize = _backend.GetPageSize(page);
                float u0 = info.X / pageSize.X;
                float v0 = info.Y / pageSize.Y;
                float u1 = (info.X + info.W) / pageSize.X;
                float v1 = (info.Y + info.H) / pageSize.Y;
                info.Uv = new Vector4(u0, v0, u1, v1);
            }
            else
            {
                info.AtlasPage = -1;
                info.Uv = default;
            }

            _glyphs[gk] = info;
            return info;
        }
    }

    // ---- Layout (wrap/newlines/measure) ----

    public sealed class TextLayoutEngine
    {
        private readonly GlyphCache _glyphs;

        public TextLayoutEngine(GlyphCache glyphs) => _glyphs = glyphs;

        public TextMetrics Measure(string text, TextStyle style)
        {
            Layout(text, style, effects: null, timeMs: 0, out _, out var metrics);
            return metrics;
        }

        public void Layout(
            string text,
            TextStyle style,
            GlyphEffect? effects,
            int timeMs,
            out List<GlyphDraw> draws,
            out TextMetrics metrics)
        {
            draws = new List<GlyphDraw>(text.Length);

            int sizePx = (int)MathF.Round(style.SizePx);
            float lineH = style.LineHeightPx > 0 ? style.LineHeightPx : (style.SizePx * 1.25f);

            float wrap = style.WrapWidthPx;

            float x = 0;
            float y = 0;

            float maxX = 0;
            float maxY = lineH;

            int index = 0;

            // Greedy wrap at spaces: track last break
            int lastBreakDrawIndex = -1;
            float xAtLastBreak = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\r') continue;

                if (c == '\n')
                {
                    NewLine();
                    continue;
                }

                // record break opportunities
                if (c == ' ' || c == '\t')
                {
                    lastBreakDrawIndex = draws.Count;
                    xAtLastBreak = x;
                }

                var gi = _glyphs.GetGlyph(style.Font, sizePx, c);

                // Wrap check before placing next glyph
                if (wrap > 0 && x > 0 && (x + gi.AdvanceX) > wrap && lastBreakDrawIndex >= 0)
                {
                    int moveStart = lastBreakDrawIndex + 1; // after the space

                    float shiftLeft = 0f;

                    if (moveStart < draws.Count)
                    {
                        shiftLeft = draws[moveStart].X; // move so it starts at 0
                        float shiftDown = lineH;

                        for (int k = moveStart; k < draws.Count; k++)
                        {
                            var g = draws[k];
                            g.X -= shiftLeft;
                            g.Y += shiftDown;
                            draws[k] = g;
                        }
                    }

                    // update cursor: now on next line
                    x = (draws.Count > moveStart) ? (x - xAtLastBreak - shiftLeft) : 0;
                    y += lineH;

                    lastBreakDrawIndex = -1;
                    xAtLastBreak = 0;
                }

                float gx = x + gi.BearingX;
                float gy = y + (style.SizePx - gi.BearingY);

                var gd = new GlyphDraw
                {
                    Index = index,
                    Char = c,
                    X = gx,
                    Y = gy,
                    WidthPx = gi.BitmapW,
                    HeightPx = gi.BitmapH,
                    Uv = gi.Uv,
                    Color = style.Color,
                    HasBackground = style.Background.HasValue,
                    Background = style.Background ?? default,
                    RotationRad = 0
                };

                if (effects != null)
                {
                    var ctx = new GlyphEffectContext(timeMs);
                    effects(ref gd, ctx);
                }

                draws.Add(gd);

                x += gi.AdvanceX;
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y + lineH);

                index++;
            }

            metrics = new TextMetrics(maxX, maxY);

            void NewLine()
            {
                x = 0;
                y += lineH;
                maxY = Math.Max(maxY, y + lineH);
                lastBreakDrawIndex = -1;
                xAtLastBreak = 0;
            }
        }
    }
}
