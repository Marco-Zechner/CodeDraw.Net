using MarcoZechner.ColorLib;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal sealed class ClearAction : IRenderAction
{
    private readonly Color _c;
    public ClearAction(in Color c) => _c = c;

    public unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
    {
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
        gl.ClearColor(_c.R, _c.G, _c.B, _c.A);
        gl.Clear((uint)ClearBufferMask.ColorBufferBit);
    }
}