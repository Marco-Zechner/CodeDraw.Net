using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Test3;

public unsafe class Experiment_1
{
    private static Glfw _glfw;
    private static WindowHandle* _winA;
    private static WindowHandle* _winB;
    private static bool _running = true;

    public static void Run()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // Create first window (share root)
        _winA = _glfw.CreateWindow(400, 400, "Window A", null, null);
        if (_winA == null) throw new Exception("Failed to create window A");

        // Create second window (sharing context with A)
        _winB = _glfw.CreateWindow(400, 400, "Window B", null, _winA);
        if (_winB == null) throw new Exception("Failed to create window B");

        // Initialize both contexts once on the creator thread
        _glfw.MakeContextCurrent(_winA);
        _glfw.MakeContextCurrent(null);
        _glfw.MakeContextCurrent(_winB);
        _glfw.MakeContextCurrent(null);

        // Start render threads
        var tA = new Thread(() => RenderThread(_winA, 1.0f, 0.2f, 0.2f, "A")) { IsBackground = true };
        var tB = new Thread(() => RenderThread(_winB, 0.2f, 0.2f, 1.0f, "B")) { IsBackground = true };
        tA.Start();
        tB.Start();

        // Main UI thread: only handles events
        while (!_glfw.WindowShouldClose(_winA) && !_glfw.WindowShouldClose(_winB))
        {
            _glfw.PollEvents();
            Thread.Sleep(1);
        }

        _running = false;

        tA.Join();
        tB.Join();

        _glfw.DestroyWindow(_winA);
        _glfw.DestroyWindow(_winB);
        _glfw.Terminate();
    }

    static void RenderThread(WindowHandle* window, float r, float g, float b, string label)
    {
        var gl = GL.GetApi(_glfw.GetProcAddress);
        _glfw.MakeContextCurrent(window);
        _glfw.SwapInterval(0); // no vsync

        Console.WriteLine($"Render thread {label} started");

        while (_running && !_glfw.WindowShouldClose(window))
        {
            _glfw.GetFramebufferSize(window, out int fbW, out int fbH);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

            gl.ClearColor(r, g, b, 1.0f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // Slightly change color per frame to prove it’s updating
            r = (r + 0.01f) % 1f;
            g = (g + 0.005f) % 1f;
            b = (b + 0.002f) % 1f;

            _glfw.SwapBuffers(window);
            Thread.Sleep(16); // ~60 fps
        }

        _glfw.MakeContextCurrent(null);
        Console.WriteLine($"Render thread {label} exited");
    }
}