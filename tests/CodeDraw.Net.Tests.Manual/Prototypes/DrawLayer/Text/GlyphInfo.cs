using System.Numerics;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

public sealed class GlyphInfo
{
    public int AtlasPage;

    public int X, Y, W, H;
    public Vector4 Uv;

    public float AdvanceX;
    public float BearingX;
    public float BearingY;

    public float BitmapW;
    public float BitmapH;
}