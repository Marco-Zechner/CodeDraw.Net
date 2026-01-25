using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Engine;

public sealed class SharedLayer : ILayerHandle, IDisposable
{
    public uint Fbo { get; internal set; }              // NOTE: not shareable across contexts
    public uint Tex { get; internal set; }              // shareable across contexts (share group)
    public uint DepthStencilRb { get; internal set; }   // not shareable across contexts
    public int Width { get; internal set; }
    public int Height { get; internal set; }

    // ---- Cross-context sync (GLsync fence) ----
    // We store the latest fence + a ring buffer of older fences so the writer can
    // delete fences safely without racing readers that might still wait on a recent fence.
    private const int FENCE_RING_SIZE = 16; // keep last 16 frame fences
    private readonly nint[] _fenceRing = new nint[FENCE_RING_SIZE];
    private int _fenceHead = -1; // starts at -1 so first increment => 0

    /// <summary>Latest fence that guards the most recent write into Tex.</summary>
    internal nint LatestFence; // written by producer, read by consumers

    /// <summary>
    /// Called by the PRODUCER (the context that rendered into Tex) after it finished writing.
    /// Stores a new fence and retires an old one from the ring (returned for deletion).
    /// </summary>
    internal nint PushFence(nint newFence)
    {
        Volatile.Write(ref LatestFence, newFence);

        int idx = Interlocked.Increment(ref _fenceHead);
        int slot = idx % FENCE_RING_SIZE;

        // Replace a very old fence in the ring; safe to delete that old one now.
        var old = _fenceRing[slot];
        _fenceRing[slot] = newFence;
        return old;
    }

    /// <summary>Called by PRODUCER during disposal to collect all fences.</summary>
    internal nint[] DrainFencesForDisposal()
    {
        var arr = new nint[FENCE_RING_SIZE + 1];
        arr[0] = Volatile.Read(ref LatestFence);
        for (int i = 0; i < FENCE_RING_SIZE; i++) arr[i + 1] = _fenceRing[i];
        // zero out (not strictly necessary)
        LatestFence = 0;
        for (int i = 0; i < FENCE_RING_SIZE; i++) _fenceRing[i] = 0;
        return arr;
    }

    //TODO Who owns disposal? For now: creator decides.
    public Action<SharedLayer>? DisposeImpl { get; init; }

    public void Dispose() => DisposeImpl?.Invoke(this);
}
