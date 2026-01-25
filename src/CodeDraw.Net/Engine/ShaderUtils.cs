using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal static class ShaderUtils
{
    public static uint CreateProgram(GL gl, string vertexSrc, string fragmentSrc)
    {
        uint vs = Compile(gl, ShaderType.VertexShader, vertexSrc);
        uint fs = Compile(gl, ShaderType.FragmentShader, fragmentSrc);

        uint prog = gl.CreateProgram();
        gl.AttachShader(prog, vs);
        gl.AttachShader(prog, fs);
        gl.LinkProgram(prog);

        gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0)
        {
            string log = gl.GetProgramInfoLog(prog);
            gl.DeleteProgram(prog);
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            throw new Exception($"Shader program link failed:\n{log}");
        }

        // shaders can be deleted after link
        gl.DetachShader(prog, vs);
        gl.DetachShader(prog, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        return prog;
    }

    private static uint Compile(GL gl, ShaderType type, string src)
    {
        uint sh = gl.CreateShader(type);
        gl.ShaderSource(sh, src);
        gl.CompileShader(sh);

        gl.GetShader(sh, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = gl.GetShaderInfoLog(sh);
            gl.DeleteShader(sh);
            throw new Exception($"{type} compile failed:\n{log}\n--- Source ---\n{src}");
        }
        return sh;
    }
}