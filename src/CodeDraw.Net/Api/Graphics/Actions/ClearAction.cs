using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.ColorDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed class ClearAction(in Color c, ClearMask mask, BlendMode2D restoreMode) : IRenderAction
{
    private readonly Color _c = c;

    public unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
    {
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

        // Force deterministic clear:
        gl.Disable(EnableCap.Blend);
        gl.ColorMask(true, true, true, true);

        if ((mask & ClearMask.COLOR) != 0)
            gl.ClearColor(_c.R, _c.G, _c.B, _c.A);

        uint bits = 0;
        if ((mask & ClearMask.COLOR) != 0)   bits |= (uint)ClearBufferMask.ColorBufferBit;
        if ((mask & ClearMask.DEPTH) != 0)   bits |= (uint)ClearBufferMask.DepthBufferBit;
        if ((mask & ClearMask.STENCIL) != 0) bits |= (uint)ClearBufferMask.StencilBufferBit;

        if (bits != 0) gl.Clear(bits);

        // Restore to a *reachable* persistent mode:
        SetBlendMode2DAction.Apply(gl, restoreMode);
    }
}