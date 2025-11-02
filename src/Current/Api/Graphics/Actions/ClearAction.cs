using MarcoZechner.ColorDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed class ClearAction(in Color c, ClearMask mask = ClearMask.Color) : IRenderAction
{
    private readonly Color _c = c;
    private readonly ClearMask _mask = mask;

    public unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
    {
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
        if ((_mask & ClearMask.Color) != 0)
            gl.ClearColor(_c.R, _c.G, _c.B, _c.A);

        uint bits = 0;
        if ((_mask & ClearMask.Color) != 0)    bits |= (uint)ClearBufferMask.ColorBufferBit;
        if ((_mask & ClearMask.Depth) != 0)    bits |= (uint)ClearBufferMask.DepthBufferBit;
        if ((_mask & ClearMask.Stencil) != 0)  bits |= (uint)ClearBufferMask.StencilBufferBit;

        if (bits != 0) gl.Clear(bits);
    }
}