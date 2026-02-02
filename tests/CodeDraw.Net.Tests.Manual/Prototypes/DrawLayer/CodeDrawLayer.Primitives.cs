namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    public int Width  => _w;
    public int Height => _h;

    public RectF FullRect => new(0, 0, _w, _h);

    private struct Buffer
    {
        public uint Tex;
        public uint Fbo;
        public nint Fence;
        public int W, H;
    }

    private struct Publication
    {
        public int FrontIndex;
        public nint Fence;
        public int W, H;
        public long Seq;
    }
}