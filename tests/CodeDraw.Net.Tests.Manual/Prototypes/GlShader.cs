using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public static class GlShader
{
    public static uint CreateShader(GL gl, GLEnum type, string src)
    {
        var s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok == 0)
        {
            var log = gl.GetShaderInfoLog(s);
            throw new Exception($"Shader compile failed: {log}");
        }
        return s;
    }

    public static uint CreateProgram(GL gl, string vs, string fs)
    {
        var v = CreateShader(gl, GLEnum.VertexShader, vs);
        var f = CreateShader(gl, GLEnum.FragmentShader, fs);

        var p = gl.CreateProgram();
        gl.AttachShader(p, v);
        gl.AttachShader(p, f);
        gl.LinkProgram(p);
        gl.GetProgram(p, GLEnum.LinkStatus, out var ok);

        gl.DeleteShader(v);
        gl.DeleteShader(f);

        if (ok == 0)
        {
            var log = gl.GetProgramInfoLog(p);
            throw new Exception($"Program link failed: {log}");
        }
        return p;
    }

    public static unsafe (uint vao, uint vbo, uint ebo) CreateFullScreenQuad(GL gl)
    {
        // pos(x,y) uv(u,v)
        float[] verts =
        [
            -1, -1, 0, 0,
             1, -1, 1, 0,
             1,  1, 1, 1,
            -1,  1, 0, 1,
        ];
        uint[] idx = [0, 1, 2, 0, 2, 3];

        var vao = gl.GenVertexArray();
        var vbo = gl.GenBuffer();
        var ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (float* p = verts)
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), p, GLEnum.StaticDraw);

        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        fixed (uint* p = idx)
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(idx.Length * sizeof(uint)), p, GLEnum.StaticDraw);

        // location 0: vec2 pos
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)0);

        // location 1: vec2 uv
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        gl.BindVertexArray(0);
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

        return (vao, vbo, ebo);
    }

    public struct LayerShader
    {
        public const string VS = """
                                 #version 330 core
                                 layout(location=0) in vec2 aPos;
                                 layout(location=1) in vec2 aUV;
                                 out vec2 vUV;
                                 void main(){
                                     vUV = aUV;
                                     gl_Position = vec4(aPos, 0.0, 1.0);
                                 }
                                 """;
        public const string FS = """
                                 #version 330 core
                                 in vec2 vUV;
                                 out vec4 FragColor;
                                 uniform sampler2D uTex;
                                 void main(){
                                     FragColor = texture(uTex, vUV);
                                 }
                                 """;
    }

    public struct RectShader
    {
        public const string VS = """
                                 #version 330 core
                                 layout(location=0) in vec2 aPos;
                                 layout(location=1) in vec2 aUV;
                                 uniform vec4 uPosSize; // x,y,w,h in pixels
                                 uniform vec2 uRes;     // framebuffer size in pixels
                                 void main(){
                                     vec2 p = uPosSize.xy + aUV * uPosSize.zw;
                                     vec2 ndc = vec2((p.x / uRes.x) * 2.0 - 1.0, 1.0 - (p.y / uRes.y) * 2.0);
                                     gl_Position = vec4(ndc, 0.0, 1.0);
                                 }
                                 """;
        public const string FS = """
                                 #version 330 core
                                 out vec4 FragColor;
                                 uniform vec4 uColor;
                                 void main(){ FragColor = uColor; }
                                 """;
    }

    public struct LayerRectShader
    {
        public const string VS = """
                                 #version 330 core
                                 layout(location=0) in vec2 aPos; // full-screen quad: [-1..1]
                                 layout(location=1) in vec2 aUv;  // [0..1]

                                 uniform vec4 uDstRectPx;   // x,y,w,h in pixels (dest)
                                 uniform vec2 uDstResPx;    // dest canvas size in pixels
                                 uniform vec4 uSrcUvRect;   // x,y,w,h in UV space (source)

                                 out vec2 vUv;

                                 void main()
                                 {
                                     // Convert pixel rect to NDC
                                     vec2 dstMinPx = uDstRectPx.xy;
                                     vec2 dstMaxPx = uDstRectPx.xy + uDstRectPx.zw;

                                     vec2 ndcMin = vec2(
                                         (dstMinPx.x / uDstResPx.x) * 2.0 - 1.0,
                                         1.0 - (dstMinPx.y / uDstResPx.y) * 2.0
                                     );
                                     vec2 ndcMax = vec2(
                                         (dstMaxPx.x / uDstResPx.x) * 2.0 - 1.0,
                                         1.0 - (dstMaxPx.y / uDstResPx.y) * 2.0
                                     );

                                     // Map the full-screen quad pos (-1..1) into our dst rect in NDC.
                                     // aPos.x=-1 => ndcMin.x, aPos.x=+1 => ndcMax.x (same for y)
                                     vec2 t = (aPos * 0.5) + 0.5;      // [-1..1] -> [0..1]
                                     vec2 ndc = mix(ndcMin, ndcMax, t);

                                     gl_Position = vec4(ndc, 0.0, 1.0);

                                     // UV: map quad uv into source uv rect
                                     vUv = uSrcUvRect.xy + aUv * uSrcUvRect.zw;
                                 }

                                 """;
        public const string FS = """
                                 #version 330 core
                                 uniform sampler2D uTex;
                                 in vec2 vUv;
                                 out vec4 FragColor;

                                 void main()
                                 {
                                     FragColor = texture(uTex, vUv);
                                 }

                                 """;
    }

}
