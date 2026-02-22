using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    private void ExecLayer(GL gl, CodeDrawLayer src, CodeDrawShader? shader)
    {
        if (!src.TryGetLatest(out var tex, out _, out _, out _, out _)) return;

        uint prog;
        int uTexLoc;

        if (shader == null)
        {
            prog = _progBlit;
            uTexLoc = _uBlitTex;
        }
        else
        {
            lock (_extShaderLock)
            {
                if (!_extCache.TryGetValue(shader.Key, out var entry))
                {
                    prog = _progBlit;
                    uTexLoc = _uBlitTex;
                }
                else
                {
                    prog = entry.Prog;
                    uTexLoc = entry.UTex;
                }
            }
        }

        if (prog == 0) return;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, tex);

        if (uTexLoc >= 0) gl.Uniform1(uTexLoc, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
    
    internal void ExecCustomRect(
        GL gl,
        in Rect<int> rect,
        CodeDrawShader? shader,
        Uniforms uniforms)
    {
        uint prog;
        int uPosSize;
        int uRes;

        if (shader == null)
        {
            prog = _progRect;
            uPosSize = _uRectPosSize;
            uRes = _uRectRes;
        }
        else
        {
            ExtShaderEntry? entry;
            lock (_extShaderLock) _extCache.TryGetValue(shader.Key, out entry);
            if (entry == null)
            {
                prog = _progRect;
                uPosSize = _uRectPosSize;
                uRes = _uRectRes;
            }
            else
            {
                prog = entry.Prog;
                uPosSize = entry.UPosSize;
                uRes = entry.URes;
            }
        }

        if (prog == 0) return;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);

        if (uPosSize >= 0) Uniform4F(gl, uPosSize, rect.Left, rect.Top, rect.Width, rect.Height);
        if (uRes >= 0) Uniform2F(gl, uRes, _w, _h);
        
        // User uniforms
        var usedTexUnits = 0;
        if (shader != null)
        {
            // If resolving a layer-texture fails, skip the draw entirely.
            if (!ApplyUserUniforms(gl, prog, shader.Key, uniforms, providesTexture: false, ["uPosSize", "uRes"], out usedTexUnits))
            {
                gl.BindVertexArray(0);
                gl.UseProgram(0);
                return;
            }
        }
        _gl.Enable(GLEnum.ScissorTest);
        _gl.Scissor(rect.Left, _h - rect.Bottom, (uint)rect.Width, (uint)rect.Height);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);
        _gl.Disable(GLEnum.ScissorTest);
        
        for (var i = 0; i < usedTexUnits; i++)
        {
            gl.ActiveTexture(GLEnum.Texture0 + i);
            gl.BindTexture(GLEnum.Texture2D, 0);
        }
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
    
    internal void ExecPostProcess(GL gl, CodeDrawShader shader, Uniforms uniforms)
    {
        if (_work.Tex == 0 || _tmp.Fbo == 0) return;

        // render into tmp
        gl.BindFramebuffer(GLEnum.Framebuffer, _tmp.Fbo);
        gl.Viewport(0, 0, (uint)_w, (uint)_h);
        gl.Disable(GLEnum.DepthTest);

        // use user shader program
        ExtShaderEntry? entry;
        lock (_extShaderLock) _extCache.TryGetValue(shader.Key, out entry);
        if (entry == null) return;

        uint prog = entry.Prog;
        
        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);

        // built-in uTex = current work texture
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, _work.Tex);
        if (entry.UTex >= 0) gl.Uniform1(entry.UTex, 0);

        // built-in uRes
        if (entry.URes >= 0) Uniform2F(gl, entry.URes, _w, _h);

        // user uniforms (start texture units at 1 because unit 0 is reserved for uTex)
        if (!ApplyUserUniforms(gl, prog, shader.Key, uniforms, providesTexture: true, ["uTex","uRes"], out int usedTexUnits))
            return;

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        // Cleanup
        for (var i = 0; i < usedTexUnits; i++)
        {
            gl.ActiveTexture(GLEnum.Texture0 + i);
            gl.BindTexture(GLEnum.Texture2D, 0);
        }
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);

        (_work, _tmp) = (_tmp, _work);
        
        gl.BindFramebuffer(GLEnum.Framebuffer, _work.Fbo);
        gl.Viewport(0, 0, (uint)_w, (uint)_h);
    }
    
    internal void ExecRect(GL gl, float x, float y, float w, float h, float r, float g, float b, float a, Matrix3x3 xf)
    {
        gl.UseProgram(_progRect);
        gl.BindVertexArray(_vao);

        Uniform4F(gl, _uRectPosSize, x, y, w, h);
        Uniform4F(gl, _uRectColor, r, g, b, a);
        Uniform2F(gl, _uRectRes, _w, _h);
        if (_uRectXf >= 0) UniformMat3(gl, _uRectXf, xf);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
}