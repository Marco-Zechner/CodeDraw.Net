using Silk.NET.GLFW;
using static Silk.NET.GLFW.GlfwCallbacks;
using Monitor = Silk.NET.GLFW.Monitor;

namespace MarcoZechner.CodeDrawDotNet;

public static unsafe class LockedGlfw
{
    private static bool _isInitialized;
    private static Glfw _glfwInstance = null!;

    private static readonly Lock _glfwLock = new();

    public static void SetGlfwInstance(Glfw? glfw)
    {
        if (glfw == null)
        {
            _isInitialized = false;
            _glfwInstance = null!;
            return;
        }

        lock (_glfwLock)
        {
            _glfwInstance = glfw;
            _isInitialized = true;
        }
    }

    public static bool Init()
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.Init();
    }

    public static void WindowHint(WindowHintInt hint, int value)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.WindowHint(hint, value);
    }

    public static void WindowHint(WindowHintOpenGlProfile hint, OpenGlProfile value)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.WindowHint(hint, value);
    }

    public static void WindowHint(WindowHintBool hint, bool value)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.WindowHint(hint, value);
    }

    public static WindowHandle* CreateWindow(int width,
        int height,
        string title,
        Monitor* monitor,
        WindowHandle* share)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.CreateWindow(width, height, title, monitor, share);
    }

    public static void HideWindow(WindowHandle* shareRoot)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.HideWindow(shareRoot);
    }

    public static void MakeContextCurrent(WindowHandle* shareRoot)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.MakeContextCurrent(shareRoot);
    }

    public static IntPtr GetProcAddress(string arg)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetProcAddress(arg);
    }

    public static void PollEvents()
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        // Triggers callbacks, which might call back into this class, so we must not hold the lock while calling it.
        _glfwInstance.PollEvents();
    }

    public static void DestroyWindow(WindowHandle* shareRoot)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.DestroyWindow(shareRoot);
    }

    public static void Terminate()
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.Terminate();
    }

    public static void SetInputMode(WindowHandle* win, StickyAttributes stickyAttributes, bool b)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetInputMode(win, stickyAttributes, b);
    }

    public static void SetCursorPosCallback(WindowHandle* win, CursorPosCallback cbsCursorPos)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetCursorPosCallback(win, cbsCursorPos);
    }

    public static void SetMouseButtonCallback(WindowHandle* win, MouseButtonCallback cbsMouseButton)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetMouseButtonCallback(win, cbsMouseButton);
    }

    public static void SetScrollCallback(WindowHandle* win, ScrollCallback cbsScroll)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetScrollCallback(win, cbsScroll);
    }

    public static void SetKeyCallback(WindowHandle* win, KeyCallback cbsKey)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetKeyCallback(win, cbsKey);
    }

    public static void SetCharCallback(WindowHandle* win, CharCallback cbsChar)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetCharCallback(win, cbsChar);
    }

    public static void SetWindowCloseCallback(WindowHandle* win, WindowCloseCallback cbsClose)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowCloseCallback(win, cbsClose);
    }

    public static void SetWindowPosCallback(WindowHandle* win, WindowPosCallback cbsWindowPos)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowPosCallback(win, cbsWindowPos);
    }

    public static void SetWindowSizeCallback(WindowHandle* win, WindowSizeCallback cbsWindowSize)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowSizeCallback(win, cbsWindowSize);
    }

    public static void SetFramebufferSizeCallback(WindowHandle* win, FramebufferSizeCallback cbsFramebufferSize)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetFramebufferSizeCallback(win, cbsFramebufferSize);
    }

    public static void SetWindowRefreshCallback(WindowHandle* win, WindowRefreshCallback cbsWindowRefresh)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowRefreshCallback(win, cbsWindowRefresh);
    }
    
    public static void SetMonitorCallback(MonitorCallback cbsMonitor)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetMonitorCallback(cbsMonitor);
    }

    public static void GetWindowPos(WindowHandle* win, out int x, out int y)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetWindowPos(win, out x, out y);
    }

    public static void GetWindowSize(WindowHandle* win, out int width, out int height)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetWindowSize(win, out width, out height);
    }

    public static Monitor** GetMonitors(out int count)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetMonitors(out count);
    }

    public static string GetMonitorName(Monitor* mPtr)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetMonitorName(mPtr);
    }

    public static VideoMode* GetVideoMode(Monitor* mPtr)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetVideoMode(mPtr);
    }

    public static void GetMonitorWorkarea(Monitor* mPtr, out int x, out int y, out int width, out int height)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetMonitorWorkarea(mPtr, out x, out y, out width, out height);
    }

    public static void GetMonitorContentScale(Monitor* mPtr, out float scaleX, out float scaleY)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetMonitorContentScale(mPtr, out scaleX, out scaleY);
    }

    public static void SetWindowTitle(WindowHandle* win, string title)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowTitle(win, title);
    }

    public static void SetWindowAttrib(WindowHandle* win, WindowAttributeSetter attrib, bool value)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowAttrib(win, attrib, value);
    }

    public static void SetWindowPos(WindowHandle* win, int x, int y)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowPos(win, x, y);
    }

    public static void SetWindowSize(WindowHandle* win, int width, int height)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowSize(win, width, height);
    }

    public static void SetWindowSizeLimits(WindowHandle* win, int minWidth, int minHeight, int maxWidth, int maxHeight)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowSizeLimits(win, minWidth, minHeight, maxWidth, maxHeight);
    }

    public static void SetWindowAspectRatio(WindowHandle* win, int numerator, int denominator)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowAspectRatio(win, numerator, denominator);
    }

    public static void RestoreWindow(WindowHandle* win)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.RestoreWindow(win);
    }

    public static void IconifyWindow(WindowHandle* win)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.IconifyWindow(win);
    }

    public static void GetWindowFrameSize(WindowHandle* win, out int left, out int top, out int right, out int bottom)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetWindowFrameSize(win, out left, out top, out right, out bottom);
    }

    public static void FocusWindow(WindowHandle* win)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.FocusWindow(win);
    }

    public static void GetMonitorPos(Monitor* monitor, out int x, out int y)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetMonitorPos(monitor, out x, out y);
    }

    public static void SwapInterval(int interval)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SwapInterval(interval);
    }

    public static void SetWindowShouldClose(WindowHandle* win, bool value)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowShouldClose(win, value);
    }

    public static void SwapBuffers(WindowHandle* win)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SwapBuffers(win);
    }

    public static bool GetWindowAttrib(WindowHandle* win, WindowAttributeGetter attribute)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetWindowAttrib(win, attribute);
    }

    public static void MaximizeWindow(WindowHandle* win)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.MaximizeWindow(win);
    }

    public static Monitor* GetPrimaryMonitor()
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) return _glfwInstance.GetPrimaryMonitor();
    }

    public static void SetWindowMaximizeCallback(WindowHandle* win, WindowMaximizeCallback cbsMaximize)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowMaximizeCallback(win, cbsMaximize);
    }

    public static void SetWindowIconifyCallback(WindowHandle* win, WindowIconifyCallback cbsIconify)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.SetWindowIconifyCallback(win, cbsIconify);
    }

    public static void GetCursorPos(WindowHandle* win, out double cx, out double cy)
    {
        if (!_isInitialized) throw new InvalidOperationException("Glfw instance not set");
        lock (_glfwLock) _glfwInstance.GetCursorPos(win, out cx, out cy);
    }
}