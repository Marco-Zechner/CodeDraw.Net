using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using MarcoZechner.CodeDrawDotNet.Api.Graphics;

namespace MarcoZechner.CodeDrawDotNet.Engine.Implementations.Actions;

internal unsafe sealed class NativeAction(Action<GL, Glfw, nint> body) : IRenderAction
{
    private readonly Action<GL, Glfw, nint> _body = body;

    public void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH) => _body(gl, glfw, (nint)window);
}