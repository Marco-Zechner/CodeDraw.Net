using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    public int Width  => _w;
    public int Height => _h;

    public RectF FullRect => new(0, 0, _w, _h);

    public sealed class CodeDrawShader : IDisposable
    {
        private readonly SharedGlfwHost _host;
        private readonly WindowHandle* _ctxWin;
        private readonly GL _gl;

        public uint Program { get; private set; }
        public bool IsDisposed { get; private set; }

        public CodeDrawShader(SharedGlfwHost host, string vs, string fs)
        {
            _host = host;
            _ctxWin = host.CreateHiddenWindow(1, 1, "shader-ctx");
            var glfw = host.Glfw;

            glfw.MakeContextCurrent(_ctxWin);
            glfw.SwapInterval(0);
            _gl = GL.GetApi(glfw.GetProcAddress);

            Program = ShaderCompiler.CreateProgram(_gl, vs, fs);

            glfw.MakeContextCurrent(null);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            var glfw = _host.Glfw;
            glfw.MakeContextCurrent(_ctxWin);

            if (Program != 0)
            {
                _gl.DeleteProgram(Program);
                Program = 0;
            }

            glfw.MakeContextCurrent(null);
            _host.DestroyWindow(_ctxWin);
        }
    }

    private struct Buffer
    {
        public uint Tex;
        public uint Fbo;
        public nint Fence;
        public int W, H;
    }

    private struct Publication
    {
        public int FrontIndex;
        public nint Fence;
        public int W, H;
        public long Seq;
    }
}