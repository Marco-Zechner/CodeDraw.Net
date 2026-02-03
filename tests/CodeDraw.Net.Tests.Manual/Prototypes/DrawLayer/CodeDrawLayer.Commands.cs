using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    private interface ICmd { void Exec(GL gl, CodeDrawLayer self); }

    private sealed class CmdSetBlendMode : ICmd
    {
        public BlendMode Mode;
        public void Exec(GL gl, CodeDrawLayer self) { self._blendMode = Mode; self.ApplyBlendMode(); }
    }

    private sealed class CmdClear(float r, float g, float b, float a) : ICmd
    {
        public void Exec(GL gl, CodeDrawLayer self)
        {
            self._clearColor = (r, g, b, a);
            gl.ClearColor(self._clearColor.r, self._clearColor.g, self._clearColor.b, self._clearColor.a);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        }
    }

    private sealed class CmdRect : ICmd
    {
        public float X, Y, W, H;
        public float R, G, B, A;
        public void Exec(GL gl, CodeDrawLayer self) => self.ExecRect(gl, X, Y, W, H, R, G, B, A);
    }

    private sealed class CmdLayer : ICmd
    {
        public CodeDrawLayer? Src;
        public CustomShader? Shader;

        public void Exec(GL gl, CodeDrawLayer self)
        {
            var s = Src;
            if (s is null || s._disposed) return;
            self.ExecLayer(gl, s, Shader);
        }
    }

    private sealed class CmdResize(int w, int h) : ICmd
    {
        public readonly int W = w, H = h;
        public void Exec(GL gl, CodeDrawLayer self) => self.ResizeInternal(W, H);
    }

    private sealed class CmdSetClearFirst : ICmd
    {
        public bool Enabled;
        public void Exec(GL gl, CodeDrawLayer self) => self._clearFirst = Enabled;
    }

    private sealed class CmdCustomRect : ICmd
    {
        public float X, Y, W, H;
        public float R, G, B, A;

        public CustomShader? Shader;
        public Uniforms Uniforms;

        public void Exec(GL gl, CodeDrawLayer self)
        {
            var s = Shader;
            if (s is null) return;

            self.ExecCustomRect(
                gl,
                X, Y, W, H,
                R, G, B, A,
                s,
                Uniforms);
        }
    }
}