using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed unsafe class RenderStateSyncAction(BlendMode2D blendMode) : IRenderAction
{
    private readonly BlendMode2D _blendMode = blendMode;

    public void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
    {
        // Always start in a known "valid" state:
        // (Reset anything that can get stuck, like ColorMask)
        gl.ColorMask(true, true, true, true);

        // Apply the window's persistent blend mode:
        SetBlendMode2DAction.Apply(gl, _blendMode);
    }
}