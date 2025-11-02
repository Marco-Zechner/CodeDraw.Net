using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Helpers;

public static class GLShader
{
    public static uint CreateShader(GL gl, GLEnum type, string src)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, GLEnum.CompileStatus, out int ok);
        if (ok == 0) { var log = gl.GetShaderInfoLog(s); throw new Exception($"Shader compile failed: {log}"); }
        return s;
    }

    public static uint CreateProgram(GL gl, string vs, string fs)
    {
        uint v = CreateShader(gl, GLEnum.VertexShader, vs);
        uint f = CreateShader(gl, GLEnum.FragmentShader, fs);
        uint p = gl.CreateProgram();
        gl.AttachShader(p, v); gl.AttachShader(p, f);
        gl.LinkProgram(p);
        gl.GetProgram(p, GLEnum.LinkStatus, out int ok);
        gl.DeleteShader(v); gl.DeleteShader(f);
        if (ok == 0) { var log = gl.GetProgramInfoLog(p); throw new Exception($"Program link failed: {log}"); }
        return p;
    }

    public unsafe static (uint vao, uint vbo, uint ebo) CreateFullScreenQuad(GL gl)
    {
        // pos (x,y) in NDC, uv (u,v)
        float[] verts = [
            -1, -1, 0, 0,
            1, -1, 1, 0,
            1,  1, 1, 1,
            -1,  1, 0, 1,
        ];
        uint[] idx = [0, 1, 2, 0, 2, 3];

        gl.CreateVertexArrays(1, out uint vao);
        gl.CreateBuffers(1, out uint vbo);
        gl.CreateBuffers(1, out uint ebo);

        fixed (float* p = verts)
        {
            gl.NamedBufferData(vbo, (nuint)(verts.Length * sizeof(float)), p, GLEnum.StaticDraw);
        }
        fixed (uint* p = idx)
        {
            gl.NamedBufferData(ebo, (nuint)(idx.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        }

        gl.VertexArrayVertexBuffer(vao, 0, vbo, 0, 4 * sizeof(float));
        gl.EnableVertexArrayAttrib(vao, 0);
        gl.EnableVertexArrayAttrib(vao, 1);
        gl.VertexArrayAttribFormat(vao, 0, 2, GLEnum.Float, false, 0);
        gl.VertexArrayAttribFormat(vao, 1, 2, GLEnum.Float, false, 2 * sizeof(float));
        gl.VertexArrayAttribBinding(vao, 0, 0);
        gl.VertexArrayAttribBinding(vao, 1, 0);

        gl.VertexArrayElementBuffer(vao, ebo);
        return (vao, vbo, ebo);
    }

    public struct LayerShader
    {
        public const string VS = @"
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUV;
        out vec2 vUV;
        void main() {
            vUV = aUV;
            gl_Position = vec4(aPos, 0.0, 1.0);
        }";

        public const string FS = @"
        #version 330 core
        in vec2 vUV;
        out vec4 FragColor;
        uniform sampler2D uTex;
        void main() {
            FragColor = texture(uTex, vUV);
        }";
    }

    public struct CircleShader
    {
        public const string VS = @"
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUV;
        out vec2 vUV;
        void main() {
            vUV = aUV;
            gl_Position = vec4(aPos, 0.0, 1.0);
        }";

        public const string FS = @"
        // FS (moving circle along circular path)
        #version 330 core
        in vec2 vUV;
        out vec4 FragColor;

        uniform float uTime;          // seconds
        uniform float uPeriod;        // seconds
        uniform float uRadius;        // pixels
        uniform vec4  uColor;         // RGBA 0..1
        uniform vec2  uResolution;    // framebuffer size in pixels
        uniform float uPathRadius;    // path radius in pixels

        const float PI = 3.14159265359;

        void main() {
            // pixel coords
            vec2 uvPx = vUV * uResolution;

            // 0..1 phase along the period
            float phase = (uPeriod > 0.0) ? fract(uTime / uPeriod) : 0.0;

            // angle around the circle
            float angle = phase * 2.0 * PI;

            // center moves on a circle around the screen center
            vec2 screenCenter = 0.5 * uResolution;
            vec2 offset = vec2(cos(angle), sin(angle)) * uPathRadius;
            vec2 center = screenCenter + offset;

            // distance in pixels and soft edge (2px feather)
            float d = length(uvPx - center);
            float edge = 2.0;
            float alpha = smoothstep(uRadius, uRadius - edge, d);

            FragColor = vec4(uColor.rgb, uColor.a * alpha);
        }";
    } 
}