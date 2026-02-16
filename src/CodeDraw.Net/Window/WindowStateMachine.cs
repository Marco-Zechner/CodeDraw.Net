using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Monitor = Silk.NET.GLFW.Monitor;

namespace MarcoZechner.CodeDrawDotNet.Window;

/// <summary>
/// Host-thread-only: owns ALL window state application (state, border/constraints, pos/size, always-on-top, click-through).
/// SharedGlfwHost should only:
/// - provide the GLFW window handle + id
/// - gather MonitorInfo once per Apply call
/// - call Apply(...)
/// </summary>
internal sealed unsafe partial class WindowStateMachine
{
    internal readonly record struct RectI(int X, int Y, int W, int H);

    // Saved rect for manual modes so we can restore to the previous windowed rect.
    private readonly ConcurrentDictionary<int, RectI> _manualRestoreRects = new();
    private readonly ConcurrentDictionary<int, RectI> _lastWindowedRects  = new();

    public void NotifyWindowedRect(int windowId, int x, int y, int w, int h)
    {
        _lastWindowedRects[windowId] = new RectI(x, y, Math.Max(1,w), Math.Max(1,h));
    }
    
    public void Apply(
        WindowHandle* win,
        int windowId,
        WindowSettingsSnapshot desired,
        WindowDirty dirty,
        IReadOnlyList<SharedGlfwHost.MonitorInfo> monitors
    )
    {
        if (win == null) return;

        desired = desired.Normalize();

        // Independent properties (safe in all modes)
        if (dirty.HasFlag(WindowDirty.Title))
            LockedGlfw.SetWindowTitle(win, desired.Title);

        if (dirty.HasFlag(WindowDirty.AlwaysOnTop))
            LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, desired.AlwaysOnTop);

        if (dirty.HasFlag(WindowDirty.ClickThrough))
            ApplyClickThrough(win, desired.ClickThrough);

        // State first (may force border/size/pos)
        if (dirty.HasFlag(WindowDirty.WindowState))
            ApplyState(win, windowId, desired, monitors);

        var manual = IsManual(desired.State);
        var windowed = desired.State == WindowState.Windowed;
        var maximized = desired.State == WindowState.Maximized;
        
        // Border/constraints:
        // - Windowed: respect intent
        // - Manual: force borderless+fixed
        // - Maximized/Minimized: ignore (meaningless)
        if (dirty.HasFlag(WindowDirty.Border) || dirty.HasFlag(WindowDirty.WindowState))
        {
            if (windowed || maximized)
            {
                ApplyBorderFromIntent_NoConstraintsIfMaximized(win, desired, maximized);
            }
            else if (manual)
            {
                ForceBorderlessFixed(win);
            }
        }

        // Pos/Size only applied in Windowed (manual modes set their own)
        if (windowed)
        {
            if (dirty.HasFlag(WindowDirty.WindowPos))
                LockedGlfw.SetWindowPos(win, desired.WindowPosition.X, desired.WindowPosition.Y);

            if (dirty.HasFlag(WindowDirty.CanvasSize))
                LockedGlfw.SetWindowSize(win, desired.Size.X, desired.Size.Y);
        }
    }

    // -------------------------
    // State
    // -------------------------

    private void ApplyState(
        WindowHandle* win,
        int windowId,
        WindowSettingsSnapshot desired,
        IReadOnlyList<SharedGlfwHost.MonitorInfo> monitors
    )
    {
        switch (desired.State)
        {
            case WindowState.Windowed:
                RestoreFromManualIfNeeded(win, windowId);
                LockedGlfw.RestoreWindow(win);
                break;

            case WindowState.Minimized:
                RestoreFromManualIfNeeded(win, windowId);
                LockedGlfw.IconifyWindow(win);
                break;

            case WindowState.Maximized:
                RestoreFromManualIfNeeded(win, windowId);
                LockedGlfw.MaximizeWindow(win); // real OS maximize
                break;

            case WindowState.BorderlessMaximized:
                EnterBorderlessMaximized(win, windowId, monitors);
                break;

            case WindowState.BorderlessFullscreen:
                EnterBorderlessFullscreen(win, windowId, monitors);
                break;
        }
    }

    private void EnterBorderlessMaximized(
        WindowHandle* win,
        int windowId,
        IReadOnlyList<SharedGlfwHost.MonitorInfo> monitors
    )
    {
        CaptureManualRestoreIfMissing(win, windowId);

        LockedGlfw.RestoreWindow(win); // ensure not maximized/minimized while we manually size
        ForceBorderlessFixed(win);

        var mi = FindBestMonitorIndexForWindow(win, monitors);
        if ((uint)mi >= (uint)monitors.Count) return;
        var m = monitors[mi];

        LockedGlfw.SetWindowPos(win, m.WorkX, m.WorkY);
        LockedGlfw.SetWindowSize(win, m.WorkWidth, m.WorkHeight);
    }

    private void EnterBorderlessFullscreen(
        WindowHandle* win,
        int windowId,
        IReadOnlyList<SharedGlfwHost.MonitorInfo> monitors
    )
    {
        CaptureManualRestoreIfMissing(win, windowId);

        LockedGlfw.RestoreWindow(win);
        ForceBorderlessFixed(win);

        var mi = FindBestMonitorIndexForWindow(win, monitors);
        if ((uint)mi >= (uint)monitors.Count) return;

        var monPtr = (Monitor*)monitors[mi].GlfwHandle;

        LockedGlfw.GetMonitorPos(monPtr, out var mx, out var my);
        var mode = LockedGlfw.GetVideoMode(monPtr);
        if (mode == null) return;

        var w = mode->Width;
        var h = mode->Height;

        LockedGlfw.SetWindowPos(win, mx, my);
        LockedGlfw.SetWindowSize(win, w + 1, h); // +1px width hack
    }

    private void RestoreFromManualIfNeeded(WindowHandle* win, int windowId)
    {
        if (!_manualRestoreRects.TryRemove(windowId, out var rr))
            return;

        LockedGlfw.RestoreWindow(win);
        LockedGlfw.SetWindowPos(win, rr.X, rr.Y);
        LockedGlfw.SetWindowSize(win, rr.W, rr.H);
    }
    
    private void CaptureWindowedRect(WindowHandle* win, int windowId)
    {
        // Call this ONLY when you are sure the window is actually windowed.
        _lastWindowedRects[windowId] = QueryWindowRect(win);
    }

    private void CaptureManualRestoreIfMissing(WindowHandle* win, int windowId)
    {
        // Only capture once per "manual session"
        if (_manualRestoreRects.ContainsKey(windowId))
            return;

        // Prefer the last known stable windowed rect.
        if (_lastWindowedRects.TryGetValue(windowId, out var rr))
        {
            _manualRestoreRects.TryAdd(windowId, rr);
            return;
        }

        // Absolute fallback: current rect (but this should only happen if you entered manual before ever being windowed)
        _manualRestoreRects.TryAdd(windowId, QueryWindowRect(win));
    }

    private static RectI QueryWindowRect(WindowHandle* win)
    {
        LockedGlfw.GetWindowPos(win, out var x, out var y);
        LockedGlfw.GetWindowSize(win, out var w, out var h);
        if (w < 1) w = 1;
        if (h < 1) h = 1;
        return new RectI(x, y, w, h);
    }

    private static bool IsManual(WindowState s)
        => s is WindowState.BorderlessMaximized or WindowState.BorderlessFullscreen;

    // -------------------------
    // Border + constraints
    // -------------------------

    private static void ForceBorderlessFixed(WindowHandle* win)
    {
        ClearAllConstraints(win);
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, false);
    }

    private static void ApplyBorderFromIntent_NoConstraintsIfMaximized(WindowHandle* win, WindowSettingsSnapshot d, bool isMaximized)
    {
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, d.FrameMode == WindowFrameMode.Decorated);

        var resizable = d.ResizeMode != WindowResizeMode.Fixed;
        LockedGlfw.SetWindowAttrib(win, WindowAttributeSetter.Resizable, resizable);

        // Constraints: only apply if NOT maximized and in windowed mode.
        ClearAllConstraints(win);
        if (isMaximized) return;

        if (d.State != WindowState.Windowed) return;
        if (!resizable) return;

        switch (d.ResizeMode)
        {
            case WindowResizeMode.Limited:
                LockedGlfw.SetWindowSizeLimits(win, d.MinSize.X, d.MinSize.Y, d.MaxSize.X, d.MaxSize.Y);
                break;
            case WindowResizeMode.Aspect:
                LockedGlfw.SetWindowAspectRatio(win, d.AspectRatio.X, d.AspectRatio.Y);
                break;
        }
    }

    private static void ClearAllConstraints(WindowHandle* win)
    {
        LockedGlfw.SetWindowSizeLimits(win, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare, Glfw.DontCare);
        LockedGlfw.SetWindowAspectRatio(win, Glfw.DontCare, Glfw.DontCare);
    }

    // -------------------------
    // Monitor picking
    // -------------------------

    /// <summary>
    /// Picks the monitor that contains the window center (by workarea), otherwise best overlap with workareas.
    /// Assumes you pass in monitors from GetMonitorsInternal_HostThreadUnsafe().
    /// </summary>
    private static int FindBestMonitorIndexForWindow(WindowHandle* win, IReadOnlyList<SharedGlfwHost.MonitorInfo> mons)
    {
        if (mons.Count == 0) return 0;

        LockedGlfw.GetWindowPos(win, out var wx, out var wy);
        LockedGlfw.GetWindowSize(win, out var ww, out var wh);

        var cx = wx + ww / 2;
        var cy = wy + wh / 2;

        // 1) center hit test
        for (var i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (cx >= m.WorkX && cx < m.WorkX + m.WorkWidth &&
                cy >= m.WorkY && cy < m.WorkY + m.WorkHeight)
                return i;
        }

        // 2) best overlap area
        long bestArea = -1;
        var best = 0;

        int x1 = wx, y1 = wy, x2 = wx + ww, y2 = wy + wh;

        for (var i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            int mx1 = m.WorkX, my1 = m.WorkY, mx2 = m.WorkX + m.WorkWidth, my2 = m.WorkY + m.WorkHeight;

            var ix1 = Math.Max(x1, mx1);
            var iy1 = Math.Max(y1, my1);
            var ix2 = Math.Min(x2, mx2);
            var iy2 = Math.Min(y2, my2);

            var iw = Math.Max(0, ix2 - ix1);
            var ih = Math.Max(0, iy2 - iy1);

            var area = (long)iw * ih;
            if (area <= bestArea) continue;

            bestArea = area;
            best = i;
        }

        return best;
    }

    // -------------------------
    // Click-through (Windows only)
    // -------------------------

    private static void ApplyClickThrough(WindowHandle* win, bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var hwnd = Win32ClickThrough.GetHwndOrZero(win);
        if (hwnd == nint.Zero)
            return;

        Win32ClickThrough.SetClickThrough(hwnd, enabled);
    }

    private static partial class Win32ClickThrough
    {
        [System.Runtime.InteropServices.LibraryImport("glfw3.dll", EntryPoint = "glfwGetWin32Window")]
        private static partial nint glfwGetWin32Window(WindowHandle* window);

        public static nint GetHwndOrZero(WindowHandle* win)
        {
            try { return glfwGetWin32Window(win); }
            catch { return nint.Zero; }
        }

        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        public static void SetClickThrough(nint hwnd, bool enabled)
        {
            var ex = GetWindowLongPtr(hwnd, GWL_EX_STYLE).ToInt64();
            if (enabled)
            {
                ex |= WS_EX_LAYERED;
                ex |= WS_EX_TRANSPARENT;
            }
            else
            {
                ex &= ~WS_EX_TRANSPARENT;
                // keep WS_EX_LAYERED (handy for transparency); clear if you prefer:
                // ex &= ~WS_EX_LAYERED;
            }

            SetWindowLongPtr(hwnd, GWL_EX_STYLE, new nint(ex));
        }
    }
}
