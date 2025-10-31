using MarcoZechner.CodeDrawDotNet.Engine;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

public unsafe partial class CodeDrawWindow
{
    public event Action<int, int>? WindowSizeChanged;
    public event Action<int, int>? FramebufferSizeChanged;
    public event Action<Keys, int, InputAction, KeyModifiers>? Key;
    public event Action<MouseButton, InputAction, KeyModifiers>? MouseButton;
    public event Action<double, double>? CursorMoved;
    public event Action<double, double>? Scrolled;
    public event Action<string[]>? FileDropped;
    public event Action? Refreshed;
    public event Action<bool>? FocusChanged;
    public event Action<bool>? IconifyChanged;
    public event Action<bool>? MaximizeChanged;
    public event CloseRequestedHandler? CloseRequested;
    public event Action? Closed; 

    // ----- close/wait latches -----
    // WaitForClose() returns only after render thread joined AND native window destroyed.
    private readonly ManualResetEventSlim _closedMre = new(initialState: false);

    private CloseReason _closeReason = CloseReason.Unknown;

    /// Non-blocking: behaves like clicking X (lets the cancelable flow run).
    public unsafe void RequestClose(CloseReason reason = CloseReason.RequestedByUser)
    {
        if (_native == null) return;
        var host = CodeDrawHost.Instance;
        host.Glfw.SetWindowShouldClose(_native, true);
        OnNativeCloseRequestedFromUI(reason);
    }

    /// <summary>Block until the window has fully closed (render joined & native destroyed).</summary>
    public CloseReason WaitForClose()
    {
        if (_closedMre.IsSet) return CloseReason.AlreadyClosed;

        _closedMre.Wait();
        return _closeReason;
    }

    /// <summary>
    /// Wait until close; if the user presses <paramref name="triggerKey"/> in the console,
    /// this will call <see cref="RequestClose"/> (same as clicking X).
    /// </summary>
    public CloseReason WaitForClose(ConsoleKey triggerKey) => WaitForClose(k => k.Key == triggerKey);

    /// <summary>
    /// Wait until close; for each console key pressed, invoke <paramref name="shouldClose"/>.
    /// If it returns true, this calls <see cref="RequestClose"/> (non-blocking) and continues waiting
    /// until the window actually finishes closing.
    /// </summary>
    /// <param name="shouldClose">Return true to request closing (e.g., on Enter), false to ignore.</param>
    /// <param name="pollIntervalMs">Console/event poll quantum; smaller = snappier, more CPU.</param>
    public CloseReason WaitForClose(Func<ConsoleKeyInfo, bool> shouldClose, int pollIntervalMs = 50)
    {
        // Fast exit if already closed
        _closeReason = CloseReason.AlreadyClosed;
        if (_closedMre.IsSet) return _closeReason;
        _closeReason = CloseReason.Unknown;

        // Small cooperative loop: wait with timeout so we can peek Console without blocking.
        while (!_closedMre.Wait(pollIntervalMs))
        {
            // If there’s no console (or input redirected), KeyAvailable may throw. Guard lightly.
            try
            {
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    bool wantClose = false;
                    try { wantClose = shouldClose(key); }
                    catch { /* ignore user callback exceptions */ }

                    if (wantClose)
                    {
                        RequestClose(CloseReason.WaitForCloseEvent);
                    }
                }
            }
            catch
            {
                // No interactive console (e.g., test runner). Just keep waiting for X.
            }
        }
        return _closeReason;
    }


    // ---- internal UI-thread entry when GLFW fires close ----
    internal unsafe void OnNativeCloseRequestedFromUI(CloseReason reason = CloseReason.UserClosedWindow)
    {
        // 1) fire cancelable CloseRequested (UI thread)
        var args = new CloseEventArgs();
        CloseRequested?.Invoke(this, args, reason);
        CodeDrawEvents.RaiseCloseRequested(this, args, reason);

        var host = CodeDrawHost.Instance;

        if (args.Cancel)
        {
            // User vetoed closing — clear the GLFW flag and continue.
            host.Glfw.SetWindowShouldClose(_native, false);
            return;
        }



        // 2) proceed with teardown off the UI thread so we don't block event delivery
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Stop update + render, then destroy the native window on UI thread
            Dispose();
        });
        _closeReason = reason;
    }


    internal void RaiseWindowSize(int w, int h)
    {
        WindowSizeChanged?.Invoke(w, h);
        CodeDrawEvents.RaiseWindowSize(this, w, h);
    }

    internal void RaiseFramebufferSize(int w, int h)
    {
        FramebufferSizeChanged?.Invoke(w, h);
        CodeDrawEvents.RaiseFramebufferSize(this, w, h);
    }

    internal void RaiseKey(Keys k, int sc, InputAction a, KeyModifiers m)
    {
        Key?.Invoke(k, sc, a, m);
        CodeDrawEvents.RaiseKey(this, k, sc, a, m);
    }

    internal void RaiseMouseButton(MouseButton b, InputAction a, KeyModifiers m)
    {
        MouseButton?.Invoke(b, a, m);
        CodeDrawEvents.RaiseMouseButton(this, b, a, m);
    }

    internal void RaiseCursorPos(double x, double y)
    {
        CursorMoved?.Invoke(x, y);
        CodeDrawEvents.RaiseCursorPos(this, x, y);
    }

    internal void RaiseScroll(double xoff, double yoff)
    {
        Scrolled?.Invoke(xoff, yoff);
        CodeDrawEvents.RaiseScroll(this, xoff, yoff);
    }

    internal void RaiseFileDropped(string[] paths)
    {
        FileDropped?.Invoke(paths);
        CodeDrawEvents.RaiseFileDropped(this, paths);
    }

    internal void RaiseRefresh()
    {
        Refreshed?.Invoke();
        CodeDrawEvents.RaiseRefresh(this);
    }

    internal void RaiseFocus(bool focused)
    {
        FocusChanged?.Invoke(focused);
        CodeDrawEvents.RaiseFocus(this, focused);
    }

    internal void RaiseIconify(bool iconified)
    {
        IconifyChanged?.Invoke(iconified);
        CodeDrawEvents.RaiseIconify(this, iconified);
    }

    internal void RaiseMaximize(bool maximized)
    {
        MaximizeChanged?.Invoke(maximized);
        CodeDrawEvents.RaiseMaximize(this, maximized);
    }
}