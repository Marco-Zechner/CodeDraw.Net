namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

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
        while (_packers.Count <= page)
            _packers.Add(new ShelfPacker(pageW, pageH));

        if (!_packers[page].TryAlloc(w, h, out x, out y))
            throw new InvalidOperationException("Glyph too large for fresh atlas page");

        return page;
    }
}