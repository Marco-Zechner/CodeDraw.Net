
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Text;

public interface IGlyphAtlasBackend
{
    void UploadAlpha8(int page, int x, int y, int w, int h, ReadOnlySpan<byte> alpha);

    Vector2 GetPageSize(int page);

    int EnsurePage(int minW, int minH);
}