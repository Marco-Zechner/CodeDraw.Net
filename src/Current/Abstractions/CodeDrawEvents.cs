using MarcoZechner.CodeDrawDotNet.Engine;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Global engine events mirrored from per-window events. Fire AFTER the per-window event has fired.
/// </summary>
public static class CodeDrawEvents
{
    /// <summary>
    /// Fired when a window's GL context is created and ready, on that window's render thread.
    /// Subscribe per-instance (preferred) or via this global hook to initialize per-window resources.
    /// </summary>
    public static event Action<CodeDrawWindow, GL, Glfw, nint>? Loaded;
    internal unsafe static void RaiseLoaded(CodeDrawWindow w, GL gl, Glfw glfw, WindowHandle* window)
    => Loaded?.Invoke(w, gl, glfw, (nint)window);
    

    // Mirrors: include the originating window
    public static event Action<CodeDrawWindow, int, int>? WindowSize;
    public static event Action<CodeDrawWindow, int, int>? FramebufferSize;
    public static event Action<CodeDrawWindow, Keys, int, InputAction, KeyModifiers>? Key;
    public static event Action<CodeDrawWindow, MouseButton, InputAction, KeyModifiers>? MouseButton;
    public static event Action<CodeDrawWindow, double, double>? CursorPos;
    public static event Action<CodeDrawWindow, double, double>? Scroll;
    public static event Action<CodeDrawWindow, string[]>? FileDropped;
    public static event Action<CodeDrawWindow>? Refresh;
    public static event Action<CodeDrawWindow, bool>? Focus;
    public static event Action<CodeDrawWindow, bool>? Iconify;
    public static event Action<CodeDrawWindow, bool>? Maximize;
    public static event CloseRequestedHandler? CloseRequested;
    public static event Action<CodeDrawWindow>? Closed;

    // Internal raisers (engine calls after the per-window raise)
    internal static void RaiseWindowSize(CodeDrawWindow w, int x, int y)                                    => WindowSize?.Invoke(w, x, y);
    internal static void RaiseFramebufferSize(CodeDrawWindow w, int x, int y)                               => FramebufferSize?.Invoke(w, x, y);
    internal static void RaiseKey(CodeDrawWindow w, Keys k, int sc, InputAction a, KeyModifiers m)          => Key?.Invoke(w, k, sc, a, m);
    internal static void RaiseMouseButton(CodeDrawWindow w, MouseButton b, InputAction a, KeyModifiers m)   => MouseButton?.Invoke(w, b, a, m);
    internal static void RaiseCursorPos(CodeDrawWindow w, double x, double y)                               => CursorPos?.Invoke(w, x, y);
    internal static void RaiseScroll(CodeDrawWindow w, double x, double y)                                  => Scroll?.Invoke(w, x, y);
    internal static void RaiseFileDropped(CodeDrawWindow w, string[] paths)                                 => FileDropped?.Invoke(w, paths);
    internal static void RaiseRefresh(CodeDrawWindow w)                                                     => Refresh?.Invoke(w);
    internal static void RaiseFocus(CodeDrawWindow w, bool f)                                               => Focus?.Invoke(w, f);
    internal static void RaiseIconify(CodeDrawWindow w, bool i)                                             => Iconify?.Invoke(w, i);
    internal static void RaiseMaximize(CodeDrawWindow w, bool m)                                            => Maximize?.Invoke(w, m);
    internal static void RaiseCloseRequested(CodeDrawWindow w, CloseEventArgs e, CloseReason r)            => CloseRequested?.Invoke(w, e, r);
    internal static void RaiseClosed(CodeDrawWindow w)                                                      => Closed?.Invoke(w);
}