using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Window;

namespace MarcoZechner.CodeDrawDotNet;

public sealed class CodeDrawApp : IDisposable
{
    private readonly Action _onDispose;
    private int _disposed;

    internal SharedGlfwHost Host { get; }

    // Strong ownership: keeps objects alive even if user loses references.
    // (So the app can still Dispose them on shutdown.)
    private readonly ConcurrentDictionary<int, CodeDrawWindow.WindowIdBox> _windows = new();
    private readonly ConcurrentDictionary<int, CodeDrawLayer.LayerIdBox> _layers = new();

    internal CodeDrawApp(SharedGlfwHost host, Action onDispose)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public HostInputHub Input => Host.Input;
    public IEnumerable<MonitorInfo> GetMonitors() => Host.GetMonitors();
    public int WindowsAlive => Host.WindowsAlive;
    public void WaitForClose(int stableMs = 0, CancellationToken ct = default)
        => Host.WaitUntilAllWindowsClosed(stableMs, ct);

    // -------- internal registration API --------

    internal void OwnWindow(CodeDrawWindow? w)
    {
        if (w == null) return;
        if (IsDisposed) return; // during/after shutdown, ignore new registrations

        _windows[w.WindowId] = new CodeDrawWindow.WindowIdBox(w);
    }

    internal void DisownWindow(int windowId)
        => _windows.TryRemove(windowId, out _);

    internal void OwnLayer(CodeDrawLayer? layer)
    {
        if (layer == null) return;
        if (IsDisposed) return;

        _layers[layer.LayerId] = new CodeDrawLayer.LayerIdBox(layer);
    }

    internal void DisownLayer(int layerId)
        => _layers.TryRemove(layerId, out _);

    // -------- shutdown policy --------

    private void DisposeAllOwnedBestEffort()
    {
        // 1) Dispose windows first (they reference layers, host queues, etc.)
        foreach (var kv in _windows)
        {
            try { kv.Value.Window.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[App] Window dispose error: {ex}"); }
        }
        _windows.Clear();

        // 2) Dispose layers (some may already be auto-disposed; this is best-effort)
        foreach (var kv in _layers)
        {
            try { kv.Value.Layer.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[App] Layer dispose error: {ex}"); }
        }
        _layers.Clear();
    }

    /// <summary>Immediate shutdown (does NOT wait for user interaction).</summary>
    public void StopAllNow()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Kill managed objects first (best effort). This will also destroy native windows by id.
        DisposeAllOwnedBestEffort();

        // Then force-close any native leftovers as a last resort.
        try { Host.DestroyAllWindows(); } catch { /* ignored */ }
        try { Host.Stop(); } catch { /* ignored */ }

        _onDispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Deterministic cleanup: dispose your object graph first.
        DisposeAllOwnedBestEffort();

        // Optional: if you still want the “user closes windows” UX, keep WaitUntilAllWindowsClosed
        // but it’s largely redundant once you disposed everything.
        try { Host.WaitUntilAllWindowsClosed(stableMs: 0); } catch { /* ignored */ }

        try { Host.Stop(); } catch { /* ignored */ }

        _onDispose();
    }
}