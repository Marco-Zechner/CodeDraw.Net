namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Disposable graphics state guard. Created via <see cref="IGraphics.PushState"/>.
/// Restores the previous state when disposed.
/// </summary>
public readonly struct StateGuard : IDisposable
{
    private readonly Action? _restore;
    internal StateGuard(Action? restore) => _restore = restore;
    /// <summary>Restores the captured state.</summary>
    public void Dispose() => _restore?.Invoke();
}