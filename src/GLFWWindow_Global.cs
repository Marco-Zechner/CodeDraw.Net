using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

public unsafe partial class GLFWWindow
{
    public static int WindowCount { get; private set; } = 0;
    private static Glfw? _glfw;
    public static Glfw Glfw
    {
        get
        {
            return _glfw == null
                ? throw new InvalidOperationException("GLFW is not initialized. Make sure to create at least one window before accessing GLFW.")
                : _glfw;
        }
        private set
        {
            _glfw = value;
        }
    }

    private static WindowHandle* _sharedWindow;

    private static void InitializeGLFW()
    {
        Glfw = Glfw.GetApi();
        Glfw.Init();
        Glfw.WindowHint(WindowHintBool.TransparentFramebuffer, true);
        Glfw.WindowHint(WindowHintBool.Resizable, true);
        Glfw.WindowHint(WindowHintBool.Decorated, true);
        Glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
        Glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        Glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        Glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
        // _glfw.WindowHint(WindowHintBool.OpenGLDebugContext, true);

        _sharedWindow = Glfw.CreateWindow(1, 1, "Shared Context Window", null, null);
        Glfw.HideWindow(_sharedWindow);

        // Glfw.MakeContextCurrent(_sharedWindow);
        // var glRoot = GL.GetApi(Glfw.GetProcAddress);

        // create shared resources here if needed.
        // you don't have to create them here, you can also


        Glfw.MakeContextCurrent(null);

    }

    public static void WaitForOpenWindows()
    {
        while (WindowCount > 0)
        {
            Thread.Sleep(100);
        }
    }
}