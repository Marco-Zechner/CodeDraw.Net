using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe partial class CodeDrawWindowBase
{
    // ── per-window events (no lifecycle/teardown here) ──────────────────────────
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

    // NOTE: CloseRequested / Closed events are declared in the core partial
    // so we keep all teardown state and raisers in one place.

    // ── raisers (first per-window, then global mirror) ──────────────────────────
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
