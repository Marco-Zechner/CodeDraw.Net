using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Engine;

public sealed class SharedLayer : ILayerHandle, IDisposable
{
    public uint Fbo { get; internal set; }
    public uint Tex { get; internal set; }
    public uint DepthStencilRb { get; internal set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }

    //TODO Who owns disposal? For now: creator decides.
    public Action<SharedLayer>? DisposeImpl { get; init; }

    public void Dispose() => DisposeImpl?.Invoke(this);
}