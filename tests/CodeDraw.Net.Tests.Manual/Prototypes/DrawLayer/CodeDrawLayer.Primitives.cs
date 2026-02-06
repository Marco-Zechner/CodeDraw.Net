namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    /// <summary>
    /// If enabled, every Render() begins with ClearColor+Clear,
    /// and we never CopyFrontToBack(). This prevents "retained" accumulation.
    /// </summary>
    public bool AutoClearLastFrame
    {
        get => _clearFirst;
        set => Enqueue(new CmdSetClearFirst { Enabled = value });
    }


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

    public readonly struct LayerTextureRef(uint tex, int w, int h, long seq)
    {
        public readonly uint Tex = tex;
        public readonly int W = w;
        public readonly int H = h;
        public readonly long Seq = seq;

        public bool IsValid => Tex != 0 && W > 0 && H > 0 && Seq > 0;
    }

    public bool TryGetLastRenderTexture(out LayerTextureRef texRef)
    {
        texRef = default;
        if (!TryGetLatest(out var tex, out var w, out var h, out _, out var seq)) return false;
        texRef = new LayerTextureRef(tex, w, h, seq);
        return texRef.IsValid;
    }

}