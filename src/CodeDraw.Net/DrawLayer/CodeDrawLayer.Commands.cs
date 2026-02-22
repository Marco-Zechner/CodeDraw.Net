using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
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
    
    private sealed class CmdLayer : ICmd
    {
        public CodeDrawLayer? Src;
        public CodeDrawShader? Shader;

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
    
    internal sealed class CmdCpuBegin : ICmd
    {
        public bool Clear; // if true, clear CPU buffer to layer clear color

        public void Exec(GL gl, CodeDrawLayer self) => self.ExecCpuBegin(gl, Clear);
    }

    internal sealed class CmdCpuPush : ICmd
    {
        // Upload CPU buffer -> _work texture. Also marks CPU as not-dirty.
        public void Exec(GL gl, CodeDrawLayer self) => self.ExecCpuPush(gl);
    }

    internal sealed class CmdCpuPull : ICmd
    {
        // Read from GPU -> CPU buffer (debug). If FromPublished is true, read _pub.Tex else _work.Tex.
        public bool FromPublished = true;

        public void Exec(GL gl, CodeDrawLayer self) => self.ExecCpuPull(gl, FromPublished);
    }
}