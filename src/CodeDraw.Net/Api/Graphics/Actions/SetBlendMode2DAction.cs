using MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;
using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed unsafe class SetBlendMode2DAction(BlendMode2D mode) : IRenderAction
{
    public void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
        => Apply(gl, mode);

    public static void Apply(GL gl, BlendMode2D mode)
    {
        // Always make this "reachable" and never leave the pipeline in a half-masked state.
        gl.ColorMask(true, true, true, true);

        switch (mode)
        {
            case BlendMode2D.OPAQUE_REPLACE:
                gl.Disable(EnableCap.Blend);
                break;
            case BlendMode2D.WRITE_ALPHA_REPLACE:
                // Only alpha writes, RGB untouched
                gl.ColorMask(false, false, false, true);
                gl.Enable(EnableCap.Blend);
                gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
                gl.BlendFuncSeparate(
                    BlendingFactor.Zero, BlendingFactor.One,
                    BlendingFactor.One,  BlendingFactor.Zero
                );
                break;
            case BlendMode2D.RGB_BLEND_SOURCEOVER_ALPHA:
                gl.Enable(EnableCap.Blend);
                gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.Max);
                gl.BlendFuncSeparate(
                    BlendingFactor.SrcAlpha,
                    BlendingFactor.OneMinusSrcAlpha,
                    BlendingFactor.One,
                    BlendingFactor.One
                );
                break;
            case BlendMode2D.RGB_BLEND_KEEP_DST_ALPHA:
            default:
                gl.Enable(EnableCap.Blend);
                gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
                gl.BlendFuncSeparate(
                    BlendingFactor.SrcAlpha,
                    BlendingFactor.OneMinusSrcAlpha,
                    BlendingFactor.Zero,
                    BlendingFactor.One
                );
                break;
        }
    }
}