using MarcoZechner.CodeDrawDotNet.Interfaces;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Api;

/// <summary>
/// Global engine events mirrored from per-window events. Fire AFTER the per-window event has fired.
/// </summary>
public static class CodeDrawEvents
{
    /// <summary>
    /// Fired when a window's GL context is created and ready, on that window's render thread.
    /// Subscribe per-instance (preferred) or via this global hook to initialize per-window resources.
    /// </summary>
    public static event Action<CodeDrawWindowBase, GL, Glfw, nint>? Loaded;
    internal unsafe static void RaiseLoaded(CodeDrawWindowBase w, GL gl, Glfw glfw, WindowHandle* window)
    => Loaded?.Invoke(w, gl, glfw, (nint)window);
    

    // Mirrors: include the originating window
    public static event Action<CodeDrawWindowBase, int, int>? WindowSize;
    public static event Action<CodeDrawWindowBase, int, int>? FramebufferSize;
    public static event Action<CodeDrawWindowBase, Keys, int, InputAction, KeyModifiers>? Key;
    public static event Action<CodeDrawWindowBase, MouseButton, InputAction, KeyModifiers>? MouseButton;
    public static event Action<CodeDrawWindowBase, double, double>? CursorPos;
    public static event Action<CodeDrawWindowBase, double, double>? Scroll;
    public static event Action<CodeDrawWindowBase, string[]>? FileDropped;
    public static event Action<CodeDrawWindowBase>? Refresh;
    public static event Action<CodeDrawWindowBase, bool>? Focus;
    public static event Action<CodeDrawWindowBase, bool>? Iconify;
    public static event Action<CodeDrawWindowBase, bool>? Maximize;
    public static event CloseRequestedHandler? CloseRequested;
    public static event Action<CodeDrawWindowBase>? Closed;

    // Internal raisers (engine calls after the per-window raise)
    internal static void RaiseWindowSize(CodeDrawWindowBase w, int x, int y)                                    => WindowSize?.Invoke(w, x, y);
    internal static void RaiseFramebufferSize(CodeDrawWindowBase w, int x, int y)                               => FramebufferSize?.Invoke(w, x, y);
    internal static void RaiseKey(CodeDrawWindowBase w, Keys k, int sc, InputAction a, KeyModifiers m)          => Key?.Invoke(w, k, sc, a, m);
    internal static void RaiseMouseButton(CodeDrawWindowBase w, MouseButton b, InputAction a, KeyModifiers m)   => MouseButton?.Invoke(w, b, a, m);
    internal static void RaiseCursorPos(CodeDrawWindowBase w, double x, double y)                               => CursorPos?.Invoke(w, x, y);
    internal static void RaiseScroll(CodeDrawWindowBase w, double x, double y)                                  => Scroll?.Invoke(w, x, y);
    internal static void RaiseFileDropped(CodeDrawWindowBase w, string[] paths)                                 => FileDropped?.Invoke(w, paths);
    internal static void RaiseRefresh(CodeDrawWindowBase w)                                                     => Refresh?.Invoke(w);
    internal static void RaiseFocus(CodeDrawWindowBase w, bool f)                                               => Focus?.Invoke(w, f);
    internal static void RaiseIconify(CodeDrawWindowBase w, bool i)                                             => Iconify?.Invoke(w, i);
    internal static void RaiseMaximize(CodeDrawWindowBase w, bool m)                                            => Maximize?.Invoke(w, m);
    internal static void RaiseCloseRequested(CodeDrawWindowBase w, CloseEventArgs e, CloseReason r)            => CloseRequested?.Invoke(w, e, r);
    internal static void RaiseClosed(CodeDrawWindowBase w)                                                      => Closed?.Invoke(w);
}