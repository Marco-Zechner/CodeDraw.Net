using Silk.NET.OpenGL;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal unsafe sealed class NativeAction : IRenderAction
{
    private readonly Action<GL, Glfw, nint> _body;
    public NativeAction(Action<GL, Glfw, nint> body) => _body = body;
    public void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH) => _body(gl, glfw, (nint)window);
}