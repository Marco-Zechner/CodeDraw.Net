using Silk.NET.GLFW;
using Silk.NET.Core.Native;

namespace MarcoZechner.CodeDrawDotNet.Engine;

/// <summary>
/// Installs GLFW callbacks for one window and relays them to host/window.
/// No public events; strictly internal plumbing.
/// </summary>
internal unsafe sealed class GlfwCallbackHub
{
    private readonly Glfw _glfw;
    private readonly WindowHandle* _win;
    private readonly CodeDrawHost _host;

    // Keep delegates alive
    private GlfwCallbacks.WindowSizeCallback? _winSize;
    private GlfwCallbacks.FramebufferSizeCallback? _fbSize;
    private GlfwCallbacks.KeyCallback? _key;
    private GlfwCallbacks.MouseButtonCallback? _mb;
    private GlfwCallbacks.CursorPosCallback? _cursor;
    private GlfwCallbacks.ScrollCallback? _scroll;
    private GlfwCallbacks.WindowRefreshCallback? _refresh;
    private GlfwCallbacks.WindowFocusCallback? _focus;
    private GlfwCallbacks.WindowIconifyCallback? _iconify;
    private GlfwCallbacks.WindowMaximizeCallback? _maximize;
    private GlfwCallbacks.WindowCloseCallback? _close;
    private GlfwCallbacks.DropCallback? _drop;

    public GlfwCallbackHub(Glfw glfw, WindowHandle* win, CodeDrawHost host)
    {
        _glfw = glfw; _win = win; _host = host;
        Install();
    }

    private void Install()
    {
        _winSize = (w, x, y) => Dispatch(() => _host.ResolveWindow(w)?.RaiseWindowSize(x, y));
        _fbSize  = (w, x, y) => Dispatch(() => _host.ResolveWindow(w)?.RaiseFramebufferSize(x, y));
        _key     = (w, k, sc, a, m) => Dispatch(() => _host.ResolveWindow(w)?.RaiseKey(k, sc, a, m));
        _mb      = (w, b, a, m) => Dispatch(() => _host.ResolveWindow(w)?.RaiseMouseButton(b, a, m));
        _cursor  = (w, x, y) => Dispatch(() => _host.ResolveWindow(w)?.RaiseCursorPos(x, y));
        _scroll  = (w, x, y) => Dispatch(() => _host.ResolveWindow(w)?.RaiseScroll(x, y));
        _refresh = (w) => Dispatch(() => _host.ResolveWindow(w)?.RaiseRefresh());
        _focus   = (w, f) => Dispatch(() => _host.ResolveWindow(w)?.RaiseFocus(f));
        _iconify = (w, i) => Dispatch(() => _host.ResolveWindow(w)?.RaiseIconify(i));
        _maximize= (w, m) => Dispatch(() => _host.ResolveWindow(w)?.RaiseMaximize(m));
        _close   = (w) => Dispatch(() => _host.ResolveWindow(w)?.OnNativeCloseRequestedFromUI());
        _drop    = (w, count, paths) => Dispatch(() =>
        {
            var list = new string[count];
            for (int i = 0; i < count; i++)
                list[i] = SilkMarshal.PtrToString(((nint*)paths)[i])!;
            _host.ResolveWindow(w)?.RaiseFileDropped(list);
        });

        _glfw.SetWindowSizeCallback(_win, _winSize);
        _glfw.SetFramebufferSizeCallback(_win, _fbSize);
        _glfw.SetKeyCallback(_win, _key);
        _glfw.SetMouseButtonCallback(_win, _mb);
        _glfw.SetCursorPosCallback(_win, _cursor);
        _glfw.SetScrollCallback(_win, _scroll);
        _glfw.SetWindowRefreshCallback(_win, _refresh);
        _glfw.SetWindowFocusCallback(_win, _focus);
        _glfw.SetWindowIconifyCallback(_win, _iconify);
        _glfw.SetWindowMaximizeCallback(_win, _maximize);
        _glfw.SetWindowCloseCallback(_win, _close);
        _glfw.SetDropCallback(_win, _drop);
    }

    private void Dispatch(Action body)
    {
        using (_host.BeginGlfwEventScope()) body(); // busy time
        _host.OnGlfwEvent();                        // count one event
    }

    public void Uninstall()
    {
        // Optional: clearing is not strictly necessary before destroying the window.
        _glfw.SetWindowSizeCallback(_win, null);
        _glfw.SetFramebufferSizeCallback(_win, null);
        _glfw.SetKeyCallback(_win, null);
        _glfw.SetMouseButtonCallback(_win, null);
        _glfw.SetCursorPosCallback(_win, null);
        _glfw.SetScrollCallback(_win, null);
        _glfw.SetWindowRefreshCallback(_win, null);
        _glfw.SetWindowFocusCallback(_win, null);
        _glfw.SetWindowIconifyCallback(_win, null);
        _glfw.SetWindowMaximizeCallback(_win, null);
        _glfw.SetWindowCloseCallback(_win, null);
        _glfw.SetDropCallback(_win, null);
    }
}
