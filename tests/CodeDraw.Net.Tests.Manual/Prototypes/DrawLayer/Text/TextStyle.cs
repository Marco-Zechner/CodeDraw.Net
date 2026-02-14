using System.Buffers;
using System.Numerics;
using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text
{
    public enum TextAlign { Left, Center, Right }
    public enum TextVAlign { Top, Middle, Bottom }

    public enum FontSlant { Normal, Italic }

    public readonly record struct FontVariant(
        int Weight,     // 100..900 typical; for variable fonts you can do arbitrary
        FontSlant Slant // Normal/Italic
    )
    {
        public static FontVariant Regular => new(400, FontSlant.Normal);
        public static FontVariant Bold => new(700, FontSlant.Normal);
        public static FontVariant Italic => new(400, FontSlant.Italic);
        public static FontVariant BoldItalic => new(700, FontSlant.Italic);
    }
    
    public readonly record struct FontRef(string Path, FontVariant Variant)
    {
        public static FontRef FromFile(string path) => new FontRef(path, FontVariant.Regular);
        public FontRef WithVariant(FontVariant v) => new(Path, v);
    }

    public sealed class TextStyle
    {
        private FontRef _font;
        private float _sizePx = 16;

        // Cached monospace ratio: charWidthPx / sizePx
        // Computed using a glyph advance (e.g., 'M') from FreeType.
        private float _cachedMonoWidthRatio;      // 0 => unknown
        private string? _cachedFontKey;           // identifies font+variant
        private int _cachedRatioAtSizePx;         // size used when ratio was last updated (optional sanity)

        // We need a resolver to compute width from glyph metrics at runtime.
        // You set this once when you set up the text system (e.g., in CodeDrawLayer.EnsureTextInit()).
        internal Func<FontRef, int, float>? MonoCellWidthResolver { get; set; }

        public FontRef Font
        {
            get => _font;
            set
            {
                _font = value;
                InvalidateFontCache();
            }
        }

        public float SizePx
        {
            get => _sizePx;
            set
            {
                _sizePx = value;
                // Size change does NOT invalidate ratio; ratio is scale-ish.
                // But if size becomes extreme, you can optionally recompute later.
            }
        }

        /// <summary>
        /// Monospace cell width in pixels for the current Font and SizePx.
        /// Always available immediately; if ratio isn't known yet, it uses:
        ///  - cached ratio if present
        ///  - otherwise a conservative fallback (SizePx * 0.6f) until a resolver is provided or Layout runs.
        /// </summary>
        public float CharacterWidthPx
        {
            get
            {
                if (_sizePx <= 0) return 0;

                // If we have a ratio for this font, use it.
                if (_cachedMonoWidthRatio > 0 && IsFontCacheValid())
                    return _cachedMonoWidthRatio * _sizePx;

                // If we have a resolver, compute once for current size and derive ratio.
                if (MonoCellWidthResolver != null)
                {
                    int px = (int)MathF.Round(_sizePx);
                    float w = MonoCellWidthResolver(_font, px);
                    if (w > 0.01f)
                    {
                        _cachedMonoWidthRatio = w / _sizePx;
                        _cachedFontKey = ComputeFontKey(_font);
                        _cachedRatioAtSizePx = px;
                        return w; // already in px
                    }
                }

                // Fallback: typical monospace is ~0.6em width. Not perfect, but "always works".
                // As soon as Layout runs (or resolver exists) the cache will become accurate.
                return _sizePx * 0.6f;
            }
        }

        public float? RelativeLineSpacing { get; set; } = null;      // null => default
        public float? RelativeCharacterSpacing { get; set; } = null; // null => default
        public float WrapWidthPx { get; set; } = 0;                  // 0 => no wrap
        public TextAlign Align { get; set; } = TextAlign.Left;
        public TextVAlign VAlign { get; set; } = TextVAlign.Top;

        public Rgba Color { get; set; } = new Rgba(1, 1, 1, 1);
        public Rgba? Background { get; set; } = null;

        internal void UpdateMonoWidthFromLayout(float sizePxUsed, float monoCellWidthPxUsed)
        {
            // Layout has the authoritative FreeType-backed value. Cache ratio.
            if (sizePxUsed <= 0) return;
            if (monoCellWidthPxUsed <= 0.01f) return;

            _cachedMonoWidthRatio = monoCellWidthPxUsed / sizePxUsed;
            _cachedFontKey = ComputeFontKey(_font);
            _cachedRatioAtSizePx = (int)MathF.Round(sizePxUsed);
        }

        private void InvalidateFontCache()
        {
            _cachedMonoWidthRatio = 0;
            _cachedFontKey = null;
            _cachedRatioAtSizePx = 0;
        }

        private bool IsFontCacheValid()
        {
            // Cache is valid if key matches.
            var key = ComputeFontKey(_font);
            return _cachedFontKey != null && string.Equals(_cachedFontKey, key, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeFontKey(FontRef f)
        {
            // Include variant so Bold/Italic gets different cache.
            // (If you later add more variant axes, include them here.)
            return $"{f.Path}|w={f.Variant.Weight}|s={f.Variant.Slant}";
        }
    }

    public readonly record struct Rgba(float R, float G, float B, float A);

    public readonly record struct TextMetrics(float Width, float Height);

    // What we feed into the renderer per glyph (before effect)
    public struct GlyphDraw
    {
        public int Index;           // character index in entire text
        public int LineIndex;
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

    public readonly struct GlyphEffectContext(int timeMs)
    {
        public readonly int TimeMs = timeMs;
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

    public readonly struct GlyphKey(string fontPath, int sizePx, uint glyphIndex) : IEquatable<GlyphKey>
    {
        public readonly string FontPath = fontPath;
        public readonly int SizePx = sizePx;
        public readonly uint GlyphIndex = glyphIndex;

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
    internal sealed class ShelfPacker(int w, int h)
    {
        private int _curX, _curY, _shelfH;

        public bool TryAlloc(int w1, int h1, out int x, out int y)
        {
            x = y = 0;
            if (w1 > w || h1 > h) return false;

            if (_curX + w1 > w)
            {
                _curX = 0;
                _curY += _shelfH;
                _shelfH = 0;
            }

            if (_curY + h1 > h) return false;

            x = _curX;
            y = _curY;

            _curX += w1;
            _shelfH = Math.Max(_shelfH, h1);
            return true;
        }
    }

    public sealed class GlyphAtlas(IGlyphAtlasBackend backend, int pageW = 1024, int pageH = 1024)
    {

        private readonly List<ShelfPacker> _packers = new();

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

            page = backend.EnsurePage(pageW, pageH);
            while (_packers.Count <= page) _packers.Add(new ShelfPacker(pageW, pageH));

            if (!_packers[page].TryAlloc(w, h, out x, out y))
                throw new InvalidOperationException("New atlas page cannot fit glyph (too big).");

            return page;
        }
    }

    public sealed class GlyphCache(IGlyphAtlasBackend backend) : IDisposable
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

        private readonly GlyphAtlas _atlas = new(backend);

        // padding helps avoid sampling bleeding
        private const int PAD = 1;

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
            try
            {
                // If SharpFont exposes variation axes, you’d map:
                // Weight -> "wght"
                // Italic -> either "ital" (0/1) or "slnt" (negative degrees)
                //
                // If not supported, this throws and you fall back to regular.
                // ApplyVariationsIfSupported(face.Face, font.Variant); //TODO:
            }
            catch
            {
                // ignore (non-variable font or API not present)
            }
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
                    backend.UploadAlpha8(page, x + PAD, y + PAD, gw, gh, tmp.AsSpan(0, gw * gh));
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

                var pageSize = backend.GetPageSize(page);
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

    public sealed class TextLayoutEngine(GlyphCache glyphs)
    {
        private int ComputeMonoCellWidthPx(FontRef font, int sizePx)
        {
            float best = 0;

            // For terminal-ish usage: basic ASCII + your block.
            // You can widen this later (e.g. 32..126).
            for (int ch = 32; ch <= 126; ch++)
                best = Math.Max(best, NeededWidthForChar(font, sizePx, (char)ch));

            best = Math.Max(best, NeededWidthForChar(font, sizePx, '█'));

            if (best <= 0.01f)
                best = sizePx * 0.6f;

            return (int)MathF.Ceiling(best);

            float NeededWidthForChar(FontRef f, int px, char c)
            {
                var gi = glyphs.GetGlyph(f, px, c);

                // Ignore empty bitmaps (spaces etc) BUT still consider advance if you want.
                // For "visual containment", empty bitmap contributes 0.
                float left = gi.BearingX;
                float right = gi.BearingX + gi.BitmapW;

                float minX = Math.Min(0f, left);
                float maxX = Math.Max(0f, right);

                float span = maxX - minX;

                // Also ensure >= advance if you want cursor semantics to match font advance:
                // span = Math.Max(span, gi.AdvanceX);

                return span;
            }
        }

        private static int ComputeLineHeightPx(float sizePx, float? relLineSpacing)
        {
            float rel = relLineSpacing ?? 1.25f;
            float step = Math.Max(0, sizePx * rel);
            if (step <= 0.01f) step = Math.Max(1, sizePx);
            return (int)MathF.Round(step);
        }

        private static int ComputeCellWidthPx(int baseCellW, float? relCharSpacing)
        {
            float rel = relCharSpacing ?? 1.0f;
            float step = Math.Max(0, baseCellW * rel);
            if (step <= 0.01f) step = Math.Max(1, baseCellW);
            return (int)MathF.Round(step);
        }

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
            draws = new List<GlyphDraw>(text?.Length ?? 0);

            if (string.IsNullOrEmpty(text))
            {
                metrics = new TextMetrics(0, 0);
                return;
            }

            int sizePx = (int)MathF.Round(style.SizePx);
            if (sizePx <= 0)
            {
                metrics = new TextMetrics(0, 0);
                return;
            }

            // ---- grid cell metrics (INTEGER) ----
            int baseCellW = ComputeMonoCellWidthPx(style.Font, sizePx);
            int cellW = ComputeCellWidthPx(baseCellW, style.RelativeCharacterSpacing);
            int cellH = ComputeLineHeightPx(style.SizePx, style.RelativeLineSpacing);

            // Feed ratio back so style.CharacterWidthPx becomes stable & correct.
            style.UpdateMonoWidthFromLayout(style.SizePx, baseCellW);

            // Wrap can be treated in px but we convert to columns to keep it grid-perfect.
            int wrapCols = 0;
            if (style.WrapWidthPx > 0.01f && cellW > 0)
                wrapCols = Math.Max(1, (int)MathF.Floor(style.WrapWidthPx / cellW));

            int row = 0;
            int col = 0;

            // Greedy word wrap bookkeeping (by text index + col)
            int lastBreakTextIndex = -1;   // index of space/tab in text
            int lastBreakCol = -1;         // col at that break
            int lastBreakDrawIndex = -1;   // draw count at that break

            int maxColAfterAdvance = 0;
            int maxRowWithGlyph = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;

                if (c == '\n')
                {
                    row++;
                    col = 0;
                    lastBreakTextIndex = -1;
                    lastBreakCol = -1;
                    lastBreakDrawIndex = -1;
                    continue;
                }

                if (c == ' ' || c == '\t')
                {
                    lastBreakTextIndex = i;
                    lastBreakCol = col;
                    lastBreakDrawIndex = draws.Count;
                }

                // Wrap BEFORE placing glyph (grid-perfect)
                if (wrapCols > 0 && col > 0 && (col + 1) > wrapCols && lastBreakTextIndex >= 0)
                {
                    // Move the word AFTER the last break down one row.
                    // Easiest: retroactively shift already-emitted glyphs for that word.
                    int moveStart = lastBreakDrawIndex + 1; // after the space
                    if (moveStart < draws.Count)
                    {
                        int shiftCols = lastBreakCol + 1; // columns to shift back to 0 (word started at break+1)
                        float dx = -shiftCols * cellW;
                        float dy = cellH;

                        for (int k = moveStart; k < draws.Count; k++)
                        {
                            var g = draws[k];
                            g.X += dx;
                            g.Y += dy;
                            g.LineIndex += 1;
                            draws[k] = g;
                        }
                    }

                    // Move cursor to next row and set col as (current col - (breakCol+1))
                    row++;
                    col = Math.Max(0, col - (lastBreakCol + 1));

                    lastBreakTextIndex = -1;
                    lastBreakCol = -1;
                    lastBreakDrawIndex = -1;
                }

                // Fetch glyph
                var gi = glyphs.GetGlyph(style.Font, sizePx, c);

                // Absolute cell origin (INT grid)
                float cellX = col * cellW;
                float cellY = row * cellH;

                // Visual placement inside cell using bearings (can overhang; that’s allowed visually)
                float gx = cellX + gi.BearingX;
                float gy = cellY + (cellH - gi.BearingY);

                var gd = new GlyphDraw
                {
                    Index = i,
                    LineIndex = row,
                    Char = c,

                    X = gx,
                    Y = gy,
                    WidthPx = gi.BitmapW,
                    HeightPx = gi.BitmapH,

                    Uv = gi.Uv,
                    AtlasPage = gi.AtlasPage,

                    Color = style.Color,
                    HasBackground = style.Background.HasValue,
                    Background = style.Background ?? default,

                    RotationRad = 0f
                };

                if (effects != null)
                {
                    var ctx = new GlyphEffectContext(timeMs);
                    effects(ref gd, ctx);
                }

                draws.Add(gd);

                // Advance cursor in grid columns
                col++;
                maxColAfterAdvance = Math.Max(maxColAfterAdvance, col);
                maxRowWithGlyph = Math.Max(maxRowWithGlyph, row);
            }

            // ---- Alignment shifts (grid-based metrics, not bitmap bounds) ----
            ApplyHorizontalAlignmentGrid(draws, style, cellW, maxColAfterAdvance);
            ApplyVerticalAlignmentGrid(draws, style, cellH, maxRowWithGlyph + 1);

            // ---- Metrics (grid truth) ----
            if (maxRowWithGlyph < 0)
            {
                metrics = new TextMetrics(0, 0);
                return;
            }

            float widthPx = maxColAfterAdvance * cellW;
            float heightPx = (maxRowWithGlyph + 1) * cellH;
            metrics = new TextMetrics(widthPx, heightPx);
        }

        private static void ApplyHorizontalAlignmentGrid(List<GlyphDraw> draws, TextStyle style, int cellW, int maxCols)
        {
            if (draws.Count == 0) return;

            // Determine per-line max col via X position / cellW.
            // Since X includes bearing, we can't invert perfectly. So we track line widths by
            // scanning draws and using the logical grid: max col index for that line is based on order.
            // Easiest robust method: compute line width as (count of glyphs in that line) * cellW
            // assuming you emit one draw per character in order (you do).
            // We'll compute counts per line, ignoring atlasPage<0 doesn't matter for alignment.
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < draws.Count; i++)
            {
                int line = draws[i].LineIndex;
                counts.TryGetValue(line, out int n);
                counts[line] = n + 1;
            }

            float boxW = (style.WrapWidthPx > 0.01f) ? style.WrapWidthPx : (maxCols * cellW);

            foreach (var kv in counts)
            {
                int line = kv.Key;
                float lineW = kv.Value * cellW;

                float shift = style.Align switch
                {
                    TextAlign.Left => 0f,
                    TextAlign.Center => (boxW - lineW) * 0.5f,
                    TextAlign.Right => (boxW - lineW),
                    _ => 0f
                };

                if (Math.Abs(shift) < 0.001f) continue;

                for (int i = 0; i < draws.Count; i++)
                {
                    if (draws[i].LineIndex != line) continue;
                    var g = draws[i];
                    g.X += shift;
                    draws[i] = g;
                }
            }
        }

        private static void ApplyVerticalAlignmentGrid(List<GlyphDraw> draws, TextStyle style, int cellH, int lineCount)
        {
            if (draws.Count == 0) return;
            if (lineCount <= 0) return;

            float blockH = lineCount * cellH;

            float shiftY = style.VAlign switch
            {
                TextVAlign.Top => 0f,
                TextVAlign.Middle => -blockH * 0.5f,
                TextVAlign.Bottom => -blockH,
                _ => 0f
            };

            if (Math.Abs(shiftY) < 0.001f) return;

            for (int i = 0; i < draws.Count; i++)
            {
                var g = draws[i];
                g.Y += shiftY;
                draws[i] = g;
            }
        }
    }
}
