using MarcoZechner.CodeDrawDotNet.Tests.Manual;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Monitor = Silk.NET.GLFW.Monitor;

// [Prototype(4)]
public unsafe class RawFullscreenRecreateDemo
{
    private enum FsMode { Windowed, BorderlessFull }
    private struct Pending { public bool Has; public FsMode Next; }

    [StaticPrototype]
    public static void RunTest()
    {
        var glfw = Glfw.GetApi();
        if (!glfw.Init())
        {
            Console.WriteLine("GLFW init failed");
            return;
        }

        void ApplyBaseHints()
        {
            glfw.DefaultWindowHints();
            glfw.WindowHint(WindowHintInt.ContextVersionMajor, 4);
            glfw.WindowHint(WindowHintInt.ContextVersionMinor, 5);
            glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
        }

        ApplyBaseHints();

        var primary = glfw.GetPrimaryMonitor();
        if (primary == null)
        {
            Console.WriteLine("No primary monitor");
            glfw.Terminate();
            return;
        }

        FsMode mode = FsMode.Windowed;
        Pending pending = default;

        int restoreX = 100, restoreY = 100, restoreW = 900, restoreH = 600;

        var win = CreateWindow(glfw, primary, mode, ref restoreX, ref restoreY, ref restoreW, ref restoreH);
        if (win == null)
        {
            Console.WriteLine("CreateWindow failed");
            glfw.Terminate();
            return;
        }

        glfw.MakeContextCurrent(win);
        glfw.SwapInterval(0);
        var gl = GL.GetApi(glfw.GetProcAddress);

        void AttachCallbacks(WindowHandle* w)
        {
            glfw.SetKeyCallback(w, (ww, key, sc, action, mods) =>
            {
                if (action != InputAction.Press) return;

                if (key == Keys.Escape)
                {
                    glfw.SetWindowShouldClose(ww, true);
                    return;
                }

                if (key == Keys.F11)
                {
                    pending.Has = true;
                    pending.Next = (mode == FsMode.Windowed) ? FsMode.BorderlessFull : FsMode.Windowed;
                    return;
                }

                // Optional: keep F12 as "force in-place toggle" for experiments.
                if (key == Keys.F12)
                {
                    var next = (mode == FsMode.Windowed) ? FsMode.BorderlessFull : FsMode.Windowed;
                    ToggleInPlace(glfw, gl, ww, next, ref mode, ref restoreX, ref restoreY, ref restoreW, ref restoreH);
                    return;
                }
            });
        }

        AttachCallbacks(win);

        Console.WriteLine("ESC quits.");
        Console.WriteLine("F11 toggles with mixed strategy:");
        Console.WriteLine("  Windowed -> Borderless: RECREATE");
        Console.WriteLine("  Borderless -> Windowed: IN-PLACE");
        Console.WriteLine("F12 forces IN-PLACE toggle (debug).");

        float t = 0f;

        while (!glfw.WindowShouldClose(win))
        {
            glfw.PollEvents();

            if (pending.Has)
            {
                pending.Has = false;

                var next = pending.Next;

                if (mode == FsMode.Windowed && next == FsMode.BorderlessFull)
                {
                    // --- Windowed -> Borderless: RECREATE ---
                    glfw.GetWindowPos(win, out restoreX, out restoreY);
                    glfw.GetWindowSize(win, out restoreW, out restoreH);

                    var targetMon = GetMonitorForWindowTopLeft(glfw, win);
                    if (targetMon == null) break;

                    glfw.DestroyWindow(win);

                    mode = next;

                    ApplyBaseHints();
                    win = CreateWindow(glfw, targetMon, mode, ref restoreX, ref restoreY, ref restoreW, ref restoreH);
                    if (win == null) break;

                    glfw.MakeContextCurrent(win);
                    glfw.SwapInterval(0);
                    gl = GL.GetApi(glfw.GetProcAddress);

                    AttachCallbacks(win);
                }
                else if (mode == FsMode.BorderlessFull && next == FsMode.Windowed)
                {
                    // --- Borderless -> Windowed: IN-PLACE ---
                    ToggleInPlace(glfw, gl, win, FsMode.Windowed, ref mode, ref restoreX, ref restoreY, ref restoreW, ref restoreH);
                }
                else
                {
                    // Fallback (shouldn't really happen with 2 modes)
                    ToggleInPlace(glfw, gl, win, next, ref mode, ref restoreX, ref restoreY, ref restoreW, ref restoreH);
                }
            }

            int fbW, fbH;
            glfw.GetFramebufferSize(win, out fbW, out fbH);
            if (fbW <= 0 || fbH <= 0)
            {
                glfw.SwapBuffers(win);
                continue;
            }

            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

            t += 0.016f;
            gl.ClearColor(
                0.06f + 0.04f * MathF.Sin(t * 1.3f),
                0.06f + 0.04f * MathF.Sin(t * 0.9f + 1.0f),
                0.06f + 0.04f * MathF.Sin(t * 1.1f + 2.0f),
                1f
            );
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            glfw.SwapBuffers(win);
        }

        if (win != null) glfw.DestroyWindow(win);
        glfw.Terminate();
    }

    private static WindowHandle* CreateWindow(
        Glfw glfw,
        Monitor* mon,
        FsMode mode,
        ref int restoreX,
        ref int restoreY,
        ref int restoreW,
        ref int restoreH)
    {
        glfw.GetMonitorPos(mon, out var mx, out var my);
        var vm = glfw.GetVideoMode(mon);
        if (vm == null) return null;

        WindowHandle* win;

        switch (mode)
        {
            case FsMode.Windowed:
            {
                glfw.WindowHint(WindowHintBool.Decorated, true);
                glfw.WindowHint(WindowHintBool.Resizable, true);
                glfw.WindowHint(WindowHintBool.Floating, false);

                win = glfw.CreateWindow(restoreW, restoreH, "Fullscreen Toggle Demo (Windowed)", null, null);
                if (win != null) glfw.SetWindowPos(win, restoreX, restoreY);
                break;
            }

            case FsMode.BorderlessFull:
            {
                glfw.WindowHint(WindowHintBool.Decorated, false);
                glfw.WindowHint(WindowHintBool.Resizable, false);
                glfw.WindowHint(WindowHintBool.AutoIconify, false);
                glfw.WindowHint(WindowHintBool.FocusOnShow, true);
                glfw.WindowHint(WindowHintBool.Floating, true);

                win = glfw.CreateWindow(vm->Width+1, vm->Height, "Fullscreen Toggle Demo (Borderless -1px)", null, null);
                if (win != null)
                {
                    glfw.SetWindowPos(win, mx, my);
                    // cover taskbar: topmost
                    glfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, true);
                }
                break;
            }

            default:
                return null;
        }

        if (win == null) return null;

        glfw.ShowWindow(win);
        glfw.FocusWindow(win);
        return win;
    }

    private static void ToggleInPlace(
        Glfw glfw,
        GL gl,
        WindowHandle* win,
        FsMode next,
        ref FsMode current,
        ref int restoreX,
        ref int restoreY,
        ref int restoreW,
        ref int restoreH)
    {
        try { gl.Finish(); } catch { }

        var mon = GetMonitorForWindowTopLeft(glfw, win);
        if (mon == null) return;

        glfw.GetMonitorPos(mon, out var mx, out var my);
        var vm = glfw.GetVideoMode(mon);
        if (vm == null) return;

        if (current == FsMode.Windowed && next != FsMode.Windowed)
        {
            glfw.GetWindowPos(win, out restoreX, out restoreY);
            glfw.GetWindowSize(win, out restoreW, out restoreH);
        }

        if (next == FsMode.Windowed)
        {
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, true);
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, false);
            glfw.SetWindowSize(win, restoreW, restoreH);
            glfw.SetWindowPos(win, restoreX, restoreY);

            current = FsMode.Windowed;
            Console.WriteLine("Mode -> Windowed (in-place)");
        }
        else
        {
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Decorated, false);
            glfw.SetWindowAttrib(win, WindowAttributeSetter.Floating, true);

            glfw.SetWindowPos(win, mx, my);
            glfw.SetWindowSize(win, vm->Width, vm->Height - 1);

            current = FsMode.BorderlessFull;
            Console.WriteLine("Mode -> BorderlessFull (-1px) (in-place)");
        }

        glfw.ShowWindow(win);
        glfw.FocusWindow(win);
        glfw.MakeContextCurrent(win);
    }

    private static Monitor* GetMonitorForWindowTopLeft(Glfw glfw, WindowHandle* win)
    {
        glfw.GetWindowPos(win, out var wx, out var wy);

        int count = 0;
        var mons = glfw.GetMonitors(out count);
        if (mons == null || count <= 0) return null;

        for (int i = 0; i < count; i++)
        {
            var m = mons[i];
            glfw.GetMonitorPos(m, out var mx, out var my);
            var vm = glfw.GetVideoMode(m);
            if (vm == null) continue;

            int x2 = mx + vm->Width;
            int y2 = my + vm->Height;

            if (wx >= mx && wx < x2 && wy >= my && wy < y2)
                return m;
        }

        return null;
    }
}
