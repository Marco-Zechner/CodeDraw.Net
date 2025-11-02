namespace MarcoZechner.CodeDrawDotNet.Engine.Abstractions;

/// <summary>
/// Triple-buffered render target with mailbox-style publication (index, fence, seq, generation).
/// </summary>
internal interface IRenderTarget
{
    /// <summary>Current width in pixels.</summary>
    int Width { get; }

    /// <summary>Current height in pixels.</summary>
    int Height { get; }

    /// <summary>Monotonic generation number; increases on resize or reallocation.</summary>
    int Generation { get; }

    /// <summary>
    /// Recreates the underlying attachments to the given size, incrementing <see cref="Generation"/>.
    /// </summary>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Begins a producer write into the ring buffer, returning the slot index to render into.
    /// The returned slot is not the last published one (mailbox semantics).
    /// </summary>
    /// <returns>Slot index (0..2) to render into.</returns>
    int BeginWrite();

    /// <summary>
    /// Publishes the given slot to consumers along with a GPU fence handle.
    /// </summary>
    /// <param name="slotIndex">Slot index that was just rendered.</param>
    /// <param name="fence">GPU sync object (opaque handle).</param>
    /// <param name="sequence">Monotonic sequence counter for freshness debugging.</param>
    void EndWrite(int slotIndex, nint fence, long sequence);

    /// <summary>
    /// Attempts to acquire the most recent ready slot for sampling.
    /// Non-blocking: if the latest fence is not signaled, the implementation should keep the previous one.
    /// </summary>
    /// <param name="drawIndex">On success, the slot index to sample.</param>
    /// <param name="generationChanged">True if the generation bumped since last call.</param>
    /// <returns>True if a slot is available (ready or reused); otherwise false.</returns>
    bool TryAcquireReady(out int drawIndex, out bool generationChanged);

    /// <summary>
    /// Returns the texture name/handle associated with a slot index.
    /// </summary>
    /// <param name="slotIndex">Slot index (0..2).</param>
    /// <returns>Opaque texture handle (API-specific).</returns>
    nint GetTexture(int slotIndex);
}