using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed class GlAction(Action<GL> body) : IRenderAction
{
    private readonly Action<GL> _body = body;

    public unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH) => _body(gl);
}