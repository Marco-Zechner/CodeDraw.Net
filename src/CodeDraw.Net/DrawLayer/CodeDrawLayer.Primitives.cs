using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    /// <summary>
    /// If enabled, every Render() begins with ClearColor+Clear,
    /// and we never CopyFrontToBack(). This prevents "retained" accumulation.
    /// Default is false
    /// </summary>
    public bool AutoClearLastFrame
    {
        get => _clearFirst;
        set => Enqueue(new CmdSetClearFirst { Enabled = value });
    }


    public int Width  => _w;
    public int Height => _h;
    
    public Vector2<int> Size => new(_w, _h);

    public Rect FullRect => new(0, 0, _w, _h);

    private struct Buffer
    {
        public uint Tex;
        public uint Fbo;
        public nint Fence;
        public int W, H;
    }

    private struct Publication
    {
        public nint Fence;
        public int W, H;
        public long Seq;
    }
}