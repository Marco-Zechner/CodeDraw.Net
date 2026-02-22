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

    public Rect<int> FullRect => new(0, 0, _w, _h);

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
    
    // CPU debug buffer (RGBA8 packed: R|G<<8|B<<16|A<<24)
    // NOTE: only touched on render thread via cmds.
    private uint[]? _cpuRgba8;
    private int _cpuW, _cpuH;
    private bool _cpuDirty;          // cpu buffer has new data not yet pushed
    private bool _cpuValidThisFrame; // cpu buffer has meaningful contents for current size
    private Buffer _cpu;
    
    public bool TryCopyCpuPixels(out uint[] rgba8, out int w, out int h)
    {
        // Not thread-safe: intended for debug usage after Render() / WaitForPublish()
        // If you want hard safety, add a lock or a copy cmd that returns via callback.
        if (_cpuRgba8 == null || _cpuW <= 0 || _cpuH <= 0) { rgba8 = []; w = h = 0; return false; }
        rgba8 = (uint[])_cpuRgba8.Clone();
        w = _cpuW; h = _cpuH;
        return true;
    }
}