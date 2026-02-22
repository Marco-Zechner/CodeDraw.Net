using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    private void ExecCpuBegin(GL gl, bool clear)
    {
        // Ensure CPU buffer matches current layer size
        if (_w <= 0 || _h <= 0) return;

        if (_cpuRgba8 == null || _cpuW != _w || _cpuH != _h)
        {
            _cpuRgba8 = new uint[_w * _h];
            _cpuW = _w;
            _cpuH = _h;
            _cpuValidThisFrame = false;
            _cpuDirty = false;
        }

        // If we want retained rendering: initialize CPU buffer from _pub if we haven't yet this frame.
        // (Matches your "CopyPubToWork" logic when not clearing.)
        if (!_cpuValidThisFrame && !_clearFirst && !_clearRequested && !clear)
        {
            // Pull from published texture (so CPU starts from last frame)
            ExecCpuPull(gl, fromPublished: true);
        }

        if (clear || _clearFirst || _clearRequested)
        {
            // Clear to current clear color (same as GPU clear)
            var c = PackClearColor(_clearColor.r, _clearColor.g, _clearColor.b, _clearColor.a);
            Array.Fill(_cpuRgba8!, c);
            _cpuDirty = true;
            _cpuValidThisFrame = true;
        }
        else
        {
            // If we pulled from pub above, we are valid; otherwise still valid if we already were.
            _cpuValidThisFrame = true;
        }
    }

    private void ExecCpuPush(GL gl)
    {
        if (_cpuRgba8 == null || _cpuW != _w || _cpuH != _h) return;
        if (!_cpuDirty) return;
        if (_work.Tex == 0) return;

        gl.BindTexture(GLEnum.Texture2D, _cpu.Tex);

        // Upload whole texture. Simple & correct. Optimize later (dirty rects).
        fixed (uint* p = _cpuRgba8)
        {
            gl.PixelStore(GLEnum.UnpackAlignment, 4);
            gl.TexSubImage2D(
                GLEnum.Texture2D,
                0,
                0, 0,
                (uint)_w, (uint)_h,
                GLEnum.Rgba,
                GLEnum.UnsignedByte,
                p
            );
        }

        gl.BindTexture(GLEnum.Texture2D, 0);

        _cpuDirty = false;
        _cpuValidThisFrame = true;
    }
    
    private void ExecCpuComposite(GL gl)
    {
        if (_cpu.Tex == 0) return;

        gl.UseProgram(_progBlit);
        gl.BindVertexArray(_vao);

        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, _cpu.Tex);

        if (_uBlitTex >= 0) gl.Uniform1(_uBlitTex, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void ExecCpuPull(GL gl, bool fromPublished)
    {
        if (_w <= 0 || _h <= 0) return;

        if (_cpuRgba8 == null || _cpuW != _w || _cpuH != _h)
        {
            _cpuRgba8 = new uint[_w * _h];
            _cpuW = _w;
            _cpuH = _h;
        }

        var tex = fromPublished ? _pub.Tex : _work.Tex;
        if (tex == 0) return;

        // Readback via FBO attach (portable).
        var fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, fbo);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, tex, 0);

        // ReadPixels origin is lower-left in GL; your layer coords are top-left.
        // For debug it's fine as-is, but let's flip in CPU to match your UI expectation.
        // We'll read into a temp and flip rows.
        var tmp = new uint[_w * _h];

        fixed (uint* p = tmp)
        {
            gl.PixelStore(GLEnum.PackAlignment, 4);
            gl.ReadPixels(0, 0, (uint)_w, (uint)_h, GLEnum.Rgba, GLEnum.UnsignedByte, p);
        }

        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        gl.DeleteFramebuffer(fbo);

        // Flip vertically into _cpuRgba8
        var dst = _cpuRgba8!;
        var rowLen = _w;
        for (var y = 0; y < _h; y++)
        {
            var srcRow = (_h - 1 - y) * rowLen;
            var dstRow = y * rowLen;
            Array.Copy(tmp, srcRow, dst, dstRow, rowLen);
        }

        _cpuDirty = false;
        _cpuValidThisFrame = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackClearColor(float r, float g, float b, float a)
    {
        var R = To8(r);
        var G = To8(g);
        var B = To8(b);
        var A = To8(a);
        return R | G << 8 | B << 16 | A << 24;

        static uint To8(float v)
        {
            v = Math.Clamp(v, 0f, 1f);
            return (uint)(v * 255f + 0.5f);
        }
    }
}