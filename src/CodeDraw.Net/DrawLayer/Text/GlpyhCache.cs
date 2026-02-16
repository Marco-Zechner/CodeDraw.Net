using System.Buffers;
using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

public sealed class GlyphCache : IDisposable
{
    private readonly FontLibrary _lib = new();

    private readonly Dictionary<(string path, int sizePx), FontFace> _faces = new();
    private readonly Dictionary<GlyphKey, GlyphInfo> _glyphs = new();

    private readonly IGlyphAtlasBackend? _backend;
    private readonly GlyphAtlas? _atlas;

    private const int PAD = 1;

    public GlyphCache(IGlyphAtlasBackend? backend)
    {
        _backend = backend;
        _atlas = backend != null ? new GlyphAtlas(backend) : null;
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

        if (_faces.TryGetValue(key, out var face))
            return face;

        face = new FontFace(_lib, path);
        face.SetPixelSize((uint)sizePx);

        _faces[key] = face;
        return face;
    }

    public GlyphInfo GetGlyph(FontRef font, int sizePx, char c)
    {
        var face = GetFace(font, sizePx);

        uint glyphIndex = face.Face.GetCharIndex(c);
        var key = new GlyphKey(face.Path, sizePx, glyphIndex);

        if (_glyphs.TryGetValue(key, out var cached))
            return cached;

        face.Face.LoadGlyph(glyphIndex, LoadFlags.Render, LoadTarget.Normal);

        var slot = face.Face.Glyph;
        var bmp = slot.Bitmap;

        int gw = bmp.Width;
        int gh = bmp.Rows;

        float advance = slot.Advance.X.ToSingle();
        if (advance < 0.01f)
            advance = slot.Metrics.HorizontalAdvance.ToSingle() / 64f;

        var info = new GlyphInfo
        {
            AtlasPage = -1, // IMPORTANT: default to "not in atlas"
            AdvanceX = advance,
            BearingX = slot.BitmapLeft,
            BearingY = slot.BitmapTop,
            BitmapW = gw,
            BitmapH = gh,
        };

        // CPU-only mode: skip atlas upload entirely
        if (_backend == null || _atlas == null)
        {
            _glyphs[key] = info;
            return info;
        }

        // GPU mode: upload into atlas
        if (gw > 0 && gh > 0)
        {
            int allocW = gw + PAD * 2;
            int allocH = gh + PAD * 2;

            _atlas.Allocate(allocW, allocH, out int page, out int x, out int y);

            byte[] tmp = ArrayPool<byte>.Shared.Rent(gw * gh);

            try
            {
                unsafe
                {
                    var src = (byte*)bmp.Buffer;
                    int pitch = bmp.Pitch;

                    for (int r = 0; r < gh; r++)
                    {
                        var srcRow = src + r * pitch;
                        int dst = r * gw;
                        for (int c2 = 0; c2 < gw; c2++)
                            tmp[dst + c2] = srcRow[c2];
                    }
                }

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
            info.Uv = new(
                info.X / pageSize.X,
                info.Y / pageSize.Y,
                (info.X + info.W) / pageSize.X,
                (info.Y + info.H) / pageSize.Y
            );
        }

        _glyphs[key] = info;
        return info;
    }
}
