using MarcoZechner.CodeDrawDotNet.Interfaces;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe partial class CodeDrawWindowBase : IWindowEventSink
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

    void IWindowEventSink.RaiseWindowSize(int w, int h)
    {
        RaiseWindowSize(w, h);
    }

    void IWindowEventSink.RaiseFramebufferSize(int w, int h)
    {
        RaiseFramebufferSize(w, h);
    }

    void IWindowEventSink.RaiseKey(Keys k, int sc, InputAction a, KeyModifiers m)
    {
        RaiseKey(k, sc, a, m);
    }

    void IWindowEventSink.RaiseMouseButton(MouseButton b, InputAction a, KeyModifiers m)
    {
        RaiseMouseButton(b, a, m);
    }

    void IWindowEventSink.RaiseCursorPos(double x, double y)
    {
        RaiseCursorPos(x, y);
    }

    void IWindowEventSink.RaiseScroll(double xoff, double yoff)
    {
        RaiseScroll(xoff, yoff);
    }

    void IWindowEventSink.RaiseFileDropped(string[] paths)
    {
        RaiseFileDropped(paths);
    }

    void IWindowEventSink.RaiseRefresh()
    {
        RaiseRefresh();
    }

    void IWindowEventSink.RaiseFocus(bool focused)
    {
        RaiseFocus(focused);
    }

    void IWindowEventSink.RaiseIconify(bool iconified)
    {
        RaiseIconify(iconified);
    }

    void IWindowEventSink.RaiseMaximize(bool maximized)
    {
        RaiseMaximize(maximized);
    }

    void IWindowEventSink.OnNativeCloseRequestedFromUI(CloseReason reason)
    {
        OnNativeCloseRequestedFromUI(reason);
    }
}
