using Silk.NET.OpenGL;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal sealed class GlAction : IRenderAction
{
    private readonly Action<GL> _body;
    public GlAction(Action<GL> body) => _body = body;
    public unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH) => _body(gl);
}